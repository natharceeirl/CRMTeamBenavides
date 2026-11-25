#!/bin/sh
# Actualizador exclusivo del entorno QA (/opt/crm-team-benavides-qa).
# Totalmente independiente del flujo de producción: solo acepta commits de origin/develop
# y opera únicamente sobre el proyecto Compose «crm-team-benavides-qa».
#
# En ~/.ssh/authorized_keys del servidor QA, la llave de GitHub Actions se restringe a:
#   restrict,command="sh /opt/crm-team-benavides-qa/despliegue/actualizar-qa.sh" ssh-ed25519 ...
#
# Modos de operación:
#   1) sh despliegue/actualizar-qa.sh <SHA_40_HEX> validar [--dominio-esperado=DOMINIO]
#        Inspecciona los archivos reales del <SHA_40_HEX> (extraídos en un directorio temporal
#        sin modificar el checkout activo), valida .env, Compose, scripts, ascendencia en
#        origin/develop y estado de migraciones sin levantar ni reiniciar servicios ni aplicar SQL.
#
#   2) sh despliegue/actualizar-qa.sh <SHA_40_HEX> generar-sql [--dominio-esperado=DOMINIO]
#        Genera el script SQL idempotente desde el código exacto de <SHA_40_HEX> en
#        despliegue/migraciones-qa/migraciones.sql y registra su manifiesto SHA-256
#        (migraciones.manifest) para inspección humana previa sin aplicarlo a la base.
#
#   3) sh despliegue/actualizar-qa.sh <SHA_40_HEX> desplegar [--dominio-esperado=DOMINIO]
#        Comprueba que la base QA ya esté inicializada y no tenga migraciones pendientes para
#        <SHA_40_HEX> ANTES de cambiar el checkout activo; toma respaldo previo recuperable,
#        posiciona el código en <SHA_40_HEX>, compila secuencialmente api y caddy, levanta los
#        servicios y verifica healthchecks y HTTPS.
#
#   4) sh despliegue/actualizar-qa.sh <SHA_40_HEX> migrar CONFIRMO_MIGRAR_QA:crm_teambenavides_qa [--dominio-esperado=DOMINIO]
#        Verifica que exista despliegue/migraciones-qa/migraciones.sql generado para <SHA_40_HEX>
#        y que su hash SHA-256 coincida con migraciones.manifest; asegura db y respaldo activos,
#        toma respaldo obligatorio previo, aplica el SQL contra crm_teambenavides_qa, verifica
#        el historial EF resultante, compila secuencialmente api y caddy y levanta los servicios.
#
# Seguridad de logs: el repositorio es público; este script jamás imprime el dominio real,
# la IP del servidor ni valores de contraseñas o tokens.
set -eu

if [ -d "/opt/crm-team-benavides-qa" ] || [ -d "/opt/crm-team-benavides" ]; then
  unset QA_TEST_SANDBOX_ROOT QA_TEST_PROD_DIR QA_TEST_SIN_SETSID
fi

REPOSITORIO="${QA_TEST_SANDBOX_ROOT:-/opt/crm-team-benavides-qa}"
DIRECTORIO_PROHIBIDO_PROD="${QA_TEST_PROD_DIR:-/opt/crm-team-benavides}"
SCRIPT="$REPOSITORIO/despliegue/actualizar-qa.sh"
NOMBRE_ARCHIVO_COMPOSE="docker-compose.qa.yml"
ARCHIVO_COMPOSE="$REPOSITORIO/despliegue/$NOMBRE_ARCHIVO_COMPOSE"
ARCHIVO_ENV="$REPOSITORIO/despliegue/.env"
PROYECTO_COMPOSE="crm-team-benavides-qa"
BASE_QA_ESPERADA="crm_teambenavides_qa"
USUARIO_QA_ESPERADO="crm_qa"
SUBRED_QA_ESPERADA="172.31.0.0/24"
TOKEN_CONFIRMACION_MIGRAR="CONFIRMO_MIGRAR_QA:crm_teambenavides_qa"

SALIDA_MIGRACIONES="$REPOSITORIO/despliegue/migraciones-qa"
ARCHIVO_SQL_MIGRACIONES="$SALIDA_MIGRACIONES/migraciones.sql"
MANIFIESTO_SQL_MIGRACIONES="$SALIDA_MIGRACIONES/migraciones.manifest"
ESTADO_DESPLIEGUE="$REPOSITORIO/despliegue/.ultimo-despliegue-qa"
REGISTROS="${QA_TEST_SANDBOX_ROOT:+$QA_TEST_SANDBOX_ROOT/logs-qa}${QA_TEST_SANDBOX_ROOT:-$HOME/despliegues-qa}"
CANDADO="${QA_TEST_SANDBOX_ROOT:+$QA_TEST_SANDBOX_ROOT/despliegue-qa.lock}${QA_TEST_SANDBOX_ROOT:-$HOME/despliegue-qa.lock}"
OCUPADO=75

TMP_QA=""
limpiar_temporales() {
  if [ -n "${TMP_QA:-}" ] && [ -d "$TMP_QA" ]; then
    rm -rf "$TMP_QA"
  fi
  rm -f "$HOME/.migraciones-aplicadas-qa" 2>/dev/null || true
}
trap limpiar_temporales EXIT INT TERM HUP

paso() { echo "== [QA] $*"; }
falla() { echo "ERROR [QA]: $*" >&2; exit 1; }

crear_tmp_qa() {
  if [ -z "${TMP_QA:-}" ] || [ ! -d "$TMP_QA" ]; then
    TMP_QA=$(mktemp -d /tmp/crm-qa-XXXXXX)
    chmod 700 "$TMP_QA"
  fi
}

compose_qa() {
  docker compose -p "$PROYECTO_COMPOSE" -f "$ARCHIVO_COMPOSE" "$@"
}

calcular_sha256() {
  archivo_hash="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$archivo_hash" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$archivo_hash" | awk '{print $1}'
  else
    openssl dgst -sha256 "$archivo_hash" | awk '{print $NF}'
  fi
}

# ---------------------------------------------------------------------------
# Lectura segura de .env sin source ni eval (maneja comillas, espacios y comentarios)
# ---------------------------------------------------------------------------
leer_env_desde_archivo() {
  archivo_origen="$1"
  clave="$2"
  [ -f "$archivo_origen" ] || return 0
  awk -v k="$clave" '
    {
      sub(/\r$/, "")
      line = $0
      sub(/^[[:space:]]+/, "", line)
      if (line ~ /^#/ || index(line, k "=") != 1) next
      val = substr(line, length(k) + 2)
      sub(/^[[:space:]]+/, "", val)
      if (val ~ /^".*"/) {
        sub(/^"/, "", val)
        sub(/"[^"]*$/, "", val)
      } else if (val ~ /^\047.*\047/) {
        sub(/^\047/, "", val)
        sub(/\047[^\047]*$/, "", val)
      } else {
        sub(/[[:space:]]+#.*$/, "", val)
        sub(/[[:space:]]+$/, "", val)
      }
      last = val
    }
    END { printf "%s", last }
  ' "$archivo_origen"
}

leer_env() {
  leer_env_desde_archivo "$ARCHIVO_ENV" "$1"
}

contiene_placeholder() {
  valor_evaluar="$1"
  printf '%s' "$valor_evaluar" | grep -Eqi 'REEMPLAZAR|CAMBIAR|PLACEHOLDER|CHANGEME|CHANGE_ME|TODO_|TU_CLAVE|TU_SECRETO|TU_DOMINIO|EJEMPLO_SECRETO|VALOR_FICTICIO|INSERTAR_AQUI'
}

es_secreto_repetitivo_o_debil() {
  secreto_evaluar="$1"
  # Rechazar cadenas donde los primeros 12 caracteres sean idénticos o patrones triviales
  if printf '%s' "$secreto_evaluar" | awk '
    {
      s = tolower($0)
      if (s == "password" || s == "postgres" || s == "admin" || s == "secret" || s == "crm_teambenavides" || s == "crm_teambenavides_qa") exit 0
      if (s ~ /^1234567890/) exit 0
      c = substr(s, 1, 1)
      same = 1
      for (i = 2; i <= length(s); i++) {
        if (substr(s, i, 1) != c) { same = 0; break }
      }
      if (same == 1) exit 0
      exit 1
    }
  '; then
    return 0
  fi
  return 1
}

# ---------------------------------------------------------------------------
# Validación estricta de argumentos (SHA de 40 hex en minúsculas, modo y opciones)
# ---------------------------------------------------------------------------
CONFIRMACION_RESUELTA=""
DOMINIO_ESPERADO_WORKFLOW=""

validar_argumentos() {
  commit_arg=${1:-}
  modo_arg=${2:-}
  arg3=${3:-}
  arg4=${4:-}

  CONFIRMACION_RESUELTA=""
  DOMINIO_ESPERADO_WORKFLOW=""

  [ -n "$commit_arg" ] || falla "Debe especificar el SHA completo de 40 caracteres del commit en develop."
  case $commit_arg in
    *[!0-9a-f]*) falla "El commit debe ser un SHA hexadecimal de 40 caracteres en minúsculas (sin nombres de rama ni referencias ambiguas)." ;;
  esac
  [ ${#commit_arg} -eq 40 ] || falla "El commit debe tener exactamente 40 caracteres (recibido: ${#commit_arg})."

  case $modo_arg in
    validar | generar-sql | desplegar)
      if [ -n "$arg4" ]; then
        falla "El modo «$modo_arg» recibió demasiados argumentos."
      fi
      if [ -n "$arg3" ]; then
        case "$arg3" in
          --dominio-esperado=*)
            DOMINIO_ESPERADO_WORKFLOW=${arg3#--dominio-esperado=}
            [ -n "$DOMINIO_ESPERADO_WORKFLOW" ] || falla "El parámetro --dominio-esperado no puede estar vacío."
            ;;
          *)
            falla "Argumento no reconocido para el modo «$modo_arg»: se permite únicamente --dominio-esperado=<DOMINIO>."
            ;;
        esac
      fi
      ;;
    migrar)
      confirmacion_candidata=""
      for extra in "$arg3" "$arg4"; do
        [ -n "$extra" ] || continue
        case "$extra" in
          --dominio-esperado=*)
            DOMINIO_ESPERADO_WORKFLOW=${extra#--dominio-esperado=}
            [ -n "$DOMINIO_ESPERADO_WORKFLOW" ] || falla "El parámetro --dominio-esperado no puede estar vacío."
            ;;
          *)
            if [ -z "$confirmacion_candidata" ]; then
              confirmacion_candidata="$extra"
            else
              falla "Argumento adicional no reconocido en modo migrar."
            fi
            ;;
        esac
      done

      echo "DESTINO DE MIGRACIÓN QA:"
      echo "  Directorio       : $REPOSITORIO"
      echo "  Proyecto Compose : $PROYECTO_COMPOSE"
      echo "  Base de datos    : $BASE_QA_ESPERADA"
      echo "  Usuario BD       : $USUARIO_QA_ESPERADO"
      if [ -z "$confirmacion_candidata" ] && [ -t 0 ] && [ -z "${SSH_ORIGINAL_COMMAND:-}" ]; then
        printf "Escriba exactamente «%s» para autorizar la migración: " "$TOKEN_CONFIRMACION_MIGRAR"
        read -r confirmacion_candidata || true
      fi
      if [ "$confirmacion_candidata" != "$TOKEN_CONFIRMACION_MIGRAR" ]; then
        falla "Confirmación inequívoca ausente o inválida. Se requiere exactamente: $TOKEN_CONFIRMACION_MIGRAR"
      fi
      CONFIRMACION_RESUELTA="$confirmacion_candidata"
      ;;
    '')
      falla "Debe indicar el modo de operación: «validar», «generar-sql», «desplegar» o «migrar»."
      ;;
    *)
      falla "Modo inválido «$modo_arg». Modos permitidos: «validar», «generar-sql», «desplegar» o «migrar»."
      ;;
  esac
}

# ---------------------------------------------------------------------------
# Validación de archivo Compose y archivo .env de QA
# ---------------------------------------------------------------------------
validar_archivo_compose_qa() {
  ruta_compose_validar="$1"
  [ -f "$ruta_compose_validar" ] || falla "No existe el archivo Compose QA: $ruta_compose_validar"

  nombre_compose=$(grep -E '^name:[[:space:]]*' "$ruta_compose_validar" | head -n 1 | awk '{print $2}' | tr -d '\r')
  [ "$nombre_compose" = "$PROYECTO_COMPOSE" ] \
    || falla "El proyecto en $(basename "$ruta_compose_validar") («$nombre_compose») no coincide con «$PROYECTO_COMPOSE»."
}

validar_entorno_qa() {
  [ -d "$REPOSITORIO" ] || falla "No existe el directorio exclusivo de QA: $REPOSITORIO"
  if [ "$REPOSITORIO" = "/opt/crm-team-benavides" ]; then
    falla "El directorio de trabajo no puede ser el directorio de producción (/opt/crm-team-benavides)."
  fi
  if [ -e "$DIRECTORIO_PROHIBIDO_PROD" ]; then
    falla "Se detectó el directorio $DIRECTORIO_PROHIBIDO_PROD en este servidor. Abortando por aislamiento de QA."
  fi
  if command -v docker >/dev/null 2>&1; then
    if docker ps -a --format '{{.Names}}' 2>/dev/null | grep -Ev '^crm-team-benavides-qa[-_]' | grep -Eq '^crm-team-benavides[-_]'; then
      falla "Se detectaron contenedores del proyecto de producción en este host. Abortando por aislamiento de QA."
    fi
  fi

  validar_archivo_compose_qa "$ARCHIVO_COMPOSE"
  [ -f "$ARCHIVO_ENV" ] || falla "No existe $ARCHIVO_ENV. Copie .env.qa.ejemplo como .env y complete los valores de QA."

  # Verificar que ninguna línea activa del .env conserve marcadores placeholder
  if grep -Ev '^[[:space:]]*#' "$ARCHIVO_ENV" | grep -Eqi 'REEMPLAZAR|CAMBIAR|PLACEHOLDER|CHANGEME|CHANGE_ME|TODO_|TU_CLAVE|TU_SECRETO|TU_DOMINIO|EJEMPLO_SECRETO|VALOR_FICTICIO|INSERTAR_AQUI'; then
    falla "El archivo .env de QA contiene marcadores placeholder sin reemplazar."
  fi

  compose_file_env=$(leer_env COMPOSE_FILE)
  compose_proj_env=$(leer_env COMPOSE_PROJECT_NAME)
  dominio_env=$(leer_env DOMINIO)
  db_env=$(leer_env POSTGRES_DB)
  user_env=$(leer_env POSTGRES_USER)
  pass_env=$(leer_env POSTGRES_PASSWORD)
  jwt_env=$(leer_env JWT_SECRET_KEY)
  red_env=$(leer_env RED_INTERNA_QA)
  asp_env=$(leer_env ASPNETCORE_ENVIRONMENT)
  wa_enabled=$(leer_env WHATSAPP_ENABLED)
  wa_provider=$(leer_env WHATSAPP_PROVIDER)
  ai_enabled=$(leer_env WHATSAPP_AI_ENABLED)
  ai_provider=$(leer_env WHATSAPP_AI_PROVIDER)
  ym_enabled=$(leer_env YAMAHA_ENABLED)

  [ "$compose_file_env" = "$NOMBRE_ARCHIVO_COMPOSE" ] \
    || falla "COMPOSE_FILE en .env de QA debe ser obligatoriamente «$NOMBRE_ARCHIVO_COMPOSE» (encontrado: «$compose_file_env»)."
  [ "$compose_proj_env" = "$PROYECTO_COMPOSE" ] \
    || falla "COMPOSE_PROJECT_NAME en .env de QA debe ser obligatoriamente «$PROYECTO_COMPOSE» (encontrado: «$compose_proj_env»)."

  [ -n "$dominio_env" ] || falla "Falta DOMINIO en .env de QA."
  [ "$dominio_env" != "app.teambenavides.com" ] || falla "DOMINIO en QA no puede ser el dominio de producción."
  contiene_placeholder "$dominio_env" && falla "DOMINIO contiene un placeholder inválido."

  if [ -n "$DOMINIO_ESPERADO_WORKFLOW" ]; then
    [ "$DOMINIO_ESPERADO_WORKFLOW" != "app.teambenavides.com" ] \
      || falla "El dominio recibido desde el workflow no puede ser el dominio de producción."
    [ "$dominio_env" = "$DOMINIO_ESPERADO_WORKFLOW" ] \
      || falla "El DOMINIO configurado en el .env del servidor QA no coincide con QA_DOMINIO del workflow."
  fi

  [ "$db_env" = "$BASE_QA_ESPERADA" ] \
    || falla "POSTGRES_DB en QA debe ser exactamente «$BASE_QA_ESPERADA» (encontrado: «$db_env»)."
  [ "$user_env" = "$USUARIO_QA_ESPERADO" ] \
    || falla "POSTGRES_USER en QA debe ser exactamente «$USUARIO_QA_ESPERADO» (encontrado: «$user_env»)."

  [ ${#pass_env} -ge 32 ] || falla "POSTGRES_PASSWORD en QA debe tener al menos 32 caracteres."
  contiene_placeholder "$pass_env" && falla "POSTGRES_PASSWORD contiene un placeholder inválido."
  es_secreto_repetitivo_o_debil "$pass_env" && falla "POSTGRES_PASSWORD es una cadena repetitiva o débil no permitida."

  [ ${#jwt_env} -ge 48 ] || falla "JWT_SECRET_KEY en QA debe tener al menos 48 caracteres."
  contiene_placeholder "$jwt_env" && falla "JWT_SECRET_KEY contiene un placeholder inválido."
  es_secreto_repetitivo_o_debil "$jwt_env" && falla "JWT_SECRET_KEY es una cadena repetitiva o débil no permitida."

  [ "$red_env" = "$SUBRED_QA_ESPERADA" ] \
    || falla "RED_INTERNA_QA debe ser «$SUBRED_QA_ESPERADA» (encontrado: «$red_env»)."

  [ "$asp_env" = "Staging" ] \
    || falla "ASPNETCORE_ENVIRONMENT en QA debe ser exactamente «Staging» (encontrado: «$asp_env»)."

  [ "$wa_enabled" = "false" ] || falla "WHATSAPP_ENABLED debe permanecer en «false» en QA."
  [ "$wa_provider" = "Mock" ] || falla "WHATSAPP_PROVIDER debe permanecer en «Mock» en QA."
  [ "$ai_enabled" = "false" ] || falla "WHATSAPP_AI_ENABLED debe permanecer en «false» en QA."
  [ "$ai_provider" = "Mock" ] || falla "WHATSAPP_AI_PROVIDER debe permanecer en «Mock» en QA."
  [ "$ym_enabled" = "false" ] || falla "YAMAHA_ENABLED debe permanecer en «false» en QA."
}

# ---------------------------------------------------------------------------
# Verificación del commit en origin/develop (sin modificar el checkout activo)
# ---------------------------------------------------------------------------
verificar_commit_en_develop() {
  commit_verificar="$1"
  cd "$REPOSITORIO"
  [ -z "$(git status --porcelain --untracked-files=no)" ] \
    || falla "El repositorio QA tiene modificaciones locales sin confirmar; abortando sin tocar nada."

  git fetch --quiet origin develop
  git rev-parse --verify --quiet "${commit_verificar}^{commit}" >/dev/null \
    || falla "El commit $commit_verificar no existe en el repositorio."
  git merge-base --is-ancestor "$commit_verificar" origin/develop 2>/dev/null \
    || falla "El commit $commit_verificar NO pertenece a la historia de origin/develop."
}

# ---------------------------------------------------------------------------
# Gestión segura de PostgreSQL QA, historial EF Core y contenedor de respaldo
# ---------------------------------------------------------------------------
db_qa_esta_corriendo() {
  compose_qa ps --status running --services 2>/dev/null | grep -qx 'db'
}

asegurar_db_qa_saludable() {
  if ! db_qa_esta_corriendo; then
    paso "Iniciando servicio db exclusivo de QA ($PROYECTO_COMPOSE)"
    compose_qa up -d --wait --wait-timeout 120 db \
      || falla "El servicio db de QA no pudo iniciarse o no alcanzó el estado healthy."
  fi

  intentos_db=0
  until compose_qa exec -T db sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB" -q' >/dev/null 2>&1; do
    intentos_db=$(( intentos_db + 1 ))
    [ "$intentos_db" -lt 30 ] || falla "El servicio PostgreSQL de QA no acepta conexiones tras 60 segundos."
    sleep 2
  done
}

asegurar_respaldo_qa_activo() {
  if ! compose_qa ps --status running --services 2>/dev/null | grep -qx 'respaldo'; then
    paso "Iniciando servicio de respaldo exclusivo de QA ($PROYECTO_COMPOSE)"
    compose_qa up -d respaldo \
      || falla "No se pudo iniciar el contenedor de respaldo de QA."
  fi
}

ejecutar_respaldo_obligatorio_qa() {
  asegurar_db_qa_saludable
  asegurar_respaldo_qa_activo
  mkdir -p "$REPOSITORIO/despliegue/respaldos-qa"

  paso "Generando respaldo previo recuperable de $BASE_QA_ESPERADA"
  if ! compose_qa exec -T respaldo sh /respaldo-qa.sh ahora; then
    falla "Falló la generación del respaldo previo de QA. Operación cancelada para proteger los datos."
  fi

  ultimo_dump=$(find "$REPOSITORIO/despliegue/respaldos-qa" -name 'crm-qa-*.dump' -mmin -5 2>/dev/null | sort | tail -n 1 || true)
  [ -n "$ultimo_dump" ] && [ -s "$ultimo_dump" ] \
    || falla "No se encontró un archivo crm-qa-*.dump reciente y no vacío en despliegue/respaldos-qa."
  echo "Respaldo QA verificado: despliegue/respaldos-qa/$(basename "$ultimo_dump")"
}

verificar_identidad_base_qa() {
  crear_tmp_qa
  err_id="$TMP_QA/psql-id.err"
  if ! identidad_bd=$(compose_qa exec -T db sh -c \
    'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tAF ":" -c "select current_database(), current_user"' \
    2>"$err_id" | tr -d '\r'); then
    detalle_err=$(tr '\n' ' ' < "$err_id" || true)
    falla "Error de conexión o autenticación al verificar la base de datos QA: $detalle_err"
  fi
  [ "$identidad_bd" = "${BASE_QA_ESPERADA}:${USUARIO_QA_ESPERADO}" ] \
    || falla "Identidad de base de datos inesperada («$identidad_bd»). Se exigía «${BASE_QA_ESPERADA}:${USUARIO_QA_ESPERADO}»."
}

# Inspecciona el estado de __EFMigrationsHistory diferenciando:
#   - SIN_TABLA      : Base nueva donde aún no existe public."__EFMigrationsHistory"
#   - TABLA_VACIA    : La tabla existe pero tiene 0 migraciones registradas
#   - CON_HISTORIAL  : La tabla existe y tiene >= 1 migraciones registradas
# Si ocurre un error de conexión, autenticación o SQL, aborta inmediatamente.
ESTADO_HISTORIAL_EF=""
ARCHIVO_MIGRACIONES_APLICADAS=""
ARCHIVO_MIGRACIONES_PENDIENTES=""

analizar_migraciones_commit() {
  commit_consulta="$1"
  crear_tmp_qa
  ARCHIVO_MIGRACIONES_APLICADAS="$TMP_QA/migraciones-aplicadas.txt"
  ARCHIVO_MIGRACIONES_PENDIENTES="$TMP_QA/migraciones-pendientes.txt"
  err_ef="$TMP_QA/psql-ef.err"
  : > "$ARCHIVO_MIGRACIONES_APLICADAS"
  : > "$ARCHIVO_MIGRACIONES_PENDIENTES"

  verificar_identidad_base_qa

  if ! existe_tabla=$(compose_qa exec -T db sh -c \
    'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tAc "select case when to_regclass('\''public.\"__EFMigrationsHistory\"'\'') is null then '\''NO'\'' else '\''SI'\'' end"' \
    2>"$err_ef" | tr -d ' \r\n'); then
    detalle_ef=$(tr '\n' ' ' < "$err_ef" || true)
    falla "Error SQL o de conexión al comprobar la existencia de __EFMigrationsHistory: $detalle_ef"
  fi

  case "$existe_tabla" in
    NO)
      ESTADO_HISTORIAL_EF="SIN_TABLA"
      ;;
    SI)
      if ! compose_qa exec -T db sh -c \
        'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tAc "select \"MigrationId\" from \"__EFMigrationsHistory\" order by \"MigrationId\""' \
        2>"$err_ef" | tr -d '\r' | sed '/^[[:space:]]*$/d' > "$ARCHIVO_MIGRACIONES_APLICADAS"; then
        detalle_ef=$(tr '\n' ' ' < "$err_ef" || true)
        falla "Error SQL al leer los registros de __EFMigrationsHistory: $detalle_ef"
      fi
      if [ -s "$ARCHIVO_MIGRACIONES_APLICADAS" ]; then
        ESTADO_HISTORIAL_EF="CON_HISTORIAL"
      else
        ESTADO_HISTORIAL_EF="TABLA_VACIA"
      fi
      ;;
    *)
      falla "Respuesta inesperada al verificar __EFMigrationsHistory: «$existe_tabla»."
      ;;
  esac

  migraciones_en_commit=0
  for archivo in $(git -C "$REPOSITORIO" ls-tree --name-only "$commit_consulta" src/CRMTeamBenavides.Api/Migrations/ 2>/dev/null || true); do
    case $archivo in *.cs) ;; *) continue ;; esac
    nombre=$(basename "$archivo" .cs)
    case $nombre in *.Designer | *ModelSnapshot) continue ;; esac
    migraciones_en_commit=$(( migraciones_en_commit + 1 ))
    if ! grep -qx "$nombre" "$ARCHIVO_MIGRACIONES_APLICADAS"; then
      echo "$nombre" >> "$ARCHIVO_MIGRACIONES_PENDIENTES"
    fi
  done

  [ "$migraciones_en_commit" -gt 0 ] \
    || falla "No se encontraron migraciones EF Core en el commit $commit_consulta."
}

# ---------------------------------------------------------------------------
# Inspección de archivos del SHA objetivo y generación/verificación de SQL auditable
# ---------------------------------------------------------------------------
extraer_archivos_despliegue_del_commit() {
  commit_extraer="$1"
  crear_tmp_qa
  destino_extract="$TMP_QA/commit-$commit_extraer"
  rm -rf "$destino_extract"
  mkdir -p "$destino_extract"

  git -C "$REPOSITORIO" archive "$commit_extraer" despliegue | tar -xf - -C "$destino_extract" \
    || falla "No se pudieron extraer los archivos de despliegue del commit $commit_extraer."

  [ -f "$destino_extract/despliegue/$NOMBRE_ARCHIVO_COMPOSE" ] \
    || falla "El commit $commit_extraer no contiene despliegue/$NOMBRE_ARCHIVO_COMPOSE."
  [ -f "$destino_extract/despliegue/Caddyfile.qa" ] \
    || falla "El commit $commit_extraer no contiene despliegue/Caddyfile.qa."
  [ -f "$destino_extract/despliegue/actualizar-qa.sh" ] \
    || falla "El commit $commit_extraer no contiene despliegue/actualizar-qa.sh."

  validar_archivo_compose_qa "$destino_extract/despliegue/$NOMBRE_ARCHIVO_COMPOSE"
  sh -n "$destino_extract/despliegue/actualizar-qa.sh" \
    || falla "El script actualizar-qa.sh del commit $commit_extraer tiene errores de sintaxis shell."
  if [ -f "$destino_extract/despliegue/preparar-servidor-qa.sh" ]; then
    sh -n "$destino_extract/despliegue/preparar-servidor-qa.sh" \
      || falla "El script preparar-servidor-qa.sh del commit $commit_extraer tiene errores de sintaxis shell."
  fi
  if [ -f "$destino_extract/despliegue/respaldo-qa.sh" ]; then
    sh -n "$destino_extract/despliegue/respaldo-qa.sh" \
      || falla "El script respaldo-qa.sh del commit $commit_extraer tiene errores de sintaxis shell."
  fi

  docker compose -p "$PROYECTO_COMPOSE" -f "$destino_extract/despliegue/$NOMBRE_ARCHIVO_COMPOSE" \
    --env-file "$ARCHIVO_ENV" config -q \
    || falla "La validación de docker-compose.qa.yml del commit $commit_extraer falló."
}

generar_sql_migraciones_desde_commit() {
  commit_sql="$1"
  crear_tmp_qa
  fuente_sql="$TMP_QA/fuente-$commit_sql"
  rm -rf "$fuente_sql"
  mkdir -p "$fuente_sql" "$SALIDA_MIGRACIONES"

  paso "Extrayendo árbol fuente del commit $commit_sql en directorio temporal aislado"
  git -C "$REPOSITORIO" archive "$commit_sql" | tar -xf - -C "$fuente_sql" \
    || falla "No se pudo extraer el código fuente del commit $commit_sql."

  paso "Generando script SQL idempotente auditable desde el commit $commit_sql"
  docker build --quiet -f "$fuente_sql/despliegue/Dockerfile.api" --target migraciones --output "$SALIDA_MIGRACIONES" "$fuente_sql" > /dev/null \
    || falla "Falló la generación de migraciones.sql mediante Dockerfile.api para el commit $commit_sql."

  [ -s "$ARCHIVO_SQL_MIGRACIONES" ] \
    || falla "El archivo $ARCHIVO_SQL_MIGRACIONES no se generó o está vacío."

  hash_sql=$(calcular_sha256 "$ARCHIVO_SQL_MIGRACIONES")
  lineas_sql=$(wc -l < "$ARCHIVO_SQL_MIGRACIONES" | tr -d ' ')
  fecha_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)

  cat > "$MANIFIESTO_SQL_MIGRACIONES" <<EOF
COMMIT=$commit_sql
SHA256=$hash_sql
LINES=$lineas_sql
GENERATED_AT_UTC=$fecha_utc
DATABASE=$BASE_QA_ESPERADA
EOF

  echo "Artefacto SQL auditable generado con éxito:"
  echo "  Archivo SQL   : despliegue/migraciones-qa/migraciones.sql ($lineas_sql líneas)"
  echo "  Manifiesto    : despliegue/migraciones-qa/migraciones.manifest"
  echo "  Commit origen : $commit_sql"
  echo "  SHA-256       : $hash_sql"
  echo "Inspeccione el archivo SQL antes de autorizar la migración con:"
  echo "  sh despliegue/actualizar-qa.sh $commit_sql migrar $TOKEN_CONFIRMACION_MIGRAR"
}

verificar_sql_auditable_para_migrar() {
  commit_esperado="$1"
  [ -s "$ARCHIVO_SQL_MIGRACIONES" ] \
    || falla "No existe despliegue/migraciones-qa/migraciones.sql. Ejecute primero «sh despliegue/actualizar-qa.sh $commit_esperado generar-sql» e inspeccione el SQL antes de migrar."
  [ -f "$MANIFIESTO_SQL_MIGRACIONES" ] \
    || falla "No existe despliegue/migraciones-qa/migraciones.manifest. Genere e inspeccione el SQL primero con el modo «generar-sql»."

  commit_manifiesto=$(leer_env_desde_archivo "$MANIFIESTO_SQL_MIGRACIONES" COMMIT)
  sha256_manifiesto=$(leer_env_desde_archivo "$MANIFIESTO_SQL_MIGRACIONES" SHA256)
  base_manifiesto=$(leer_env_desde_archivo "$MANIFIESTO_SQL_MIGRACIONES" DATABASE)

  [ "$commit_manifiesto" = "$commit_esperado" ] \
    || falla "El SQL en migraciones-qa pertenece al commit «$commit_manifiesto» y no al commit solicitado «$commit_esperado». Regenere e inspeccione el SQL con «generar-sql»."
  [ "$base_manifiesto" = "$BASE_QA_ESPERADA" ] \
    || falla "El manifiesto SQL no corresponde a la base $BASE_QA_ESPERADA."

  sha256_actual=$(calcular_sha256 "$ARCHIVO_SQL_MIGRACIONES")
  [ -n "$sha256_manifiesto" ] && [ "$sha256_actual" = "$sha256_manifiesto" ] \
    || falla "El archivo migraciones.sql fue modificado o su SHA-256 ($sha256_actual) no coincide con el manifiesto ($sha256_manifiesto). Abortando migración."

  echo "Artefacto SQL verificado para migrar: commit=$commit_esperado, SHA-256=$sha256_actual"
}

registrar_resultado() {
  commit_res="$1"
  modo_res="$2"
  estado_res="$3"
  printf '%s | modo=%s | commit=%s | resultado=%s\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$modo_res" "$commit_res" "$estado_res" >> "$ESTADO_DESPLIEGUE"
}

verificar_endpoints_post_despliegue() {
  dominio=$(leer_env DOMINIO)
  puerto_https=$(leer_env PUERTO_HTTPS)
  puerto_https=${puerto_https:-443}

  web=$(curl -s -o /dev/null -w '%{http_code}' --resolve "$dominio:$puerto_https:127.0.0.1" "https://$dominio:$puerto_https/" || true)
  api=$(curl -s -o /dev/null -w '%{http_code}' --resolve "$dominio:$puerto_https:127.0.0.1" "https://$dominio:$puerto_https/api/aprobaciones" || true)
  webhook=$(curl -s -o /dev/null -w '%{http_code}' --resolve "$dominio:$puerto_https:127.0.0.1" "https://$dominio:$puerto_https/api/whatsapp/webhook" || true)
  cabeceras=$(curl -s -I --resolve "$dominio:$puerto_https:127.0.0.1" "https://$dominio:$puerto_https/" || true)

  [ "$web" = "200" ] || falla "La web de QA respondió $web en lugar de 200."
  [ "$api" = "401" ] || falla "La API de QA respondió $api en lugar de 401 sin sesión."
  [ "$webhook" = "503" ] || falla "El webhook de WhatsApp en QA respondió $webhook en lugar de 503 (debe permanecer deshabilitado)."
  printf '%s\n' "$cabeceras" | grep -Eqi '^x-robots-tag:.*noindex' \
    || falla "Caddy QA no devolvió el encabezado X-Robots-Tag: noindex, nofollow."

  echo "Verificación post-despliegue QA: Web=$web, API=$api (sin sesión), Webhook WhatsApp=$webhook (deshabilitado), X-Robots-Tag=noindex."
}

# ---------------------------------------------------------------------------
# Ejecución de los 4 modos (validar | generar-sql | desplegar | migrar)
# ---------------------------------------------------------------------------
ejecutar_operacion() {
  commit="$1"
  modo="$2"
  confirmacion="${3:-}"
  dominio_workflow="${4:-}"

  DOMINIO_ESPERADO_WORKFLOW="$dominio_workflow"
  validar_entorno_qa
  verificar_commit_en_develop "$commit"

  # 1. Validación estática de los archivos reales del commit solicitado (sin cambiar el checkout)
  extraer_archivos_despliegue_del_commit "$commit"

  case "$modo" in
    validar)
      paso "Modo VALIDAR: archivos de despliegue del commit $commit verificados (HEAD local intacto en $(git -C "$REPOSITORIO" rev-parse --short HEAD))."
      if db_qa_esta_corriendo; then
        analizar_migraciones_commit "$commit"
        echo "Estado de historial EF en $BASE_QA_ESPERADA: $ESTADO_HISTORIAL_EF"
        if [ -s "$ARCHIVO_MIGRACIONES_PENDIENTES" ]; then
          echo "Migraciones pendientes para el commit $commit:"
          sed 's/^/  /' "$ARCHIVO_MIGRACIONES_PENDIENTES"
        else
          echo "No hay migraciones pendientes para el commit $commit en $BASE_QA_ESPERADA."
        fi
      else
        echo "Contenedor db de QA no está en ejecución; validación completada sin iniciar servicios."
      fi
      registrar_resultado "$commit" "$modo" "OK"
      paso "Validación QA finalizada sin modificar el árbol de trabajo, servicios ni datos."
      return 0
      ;;

    generar-sql)
      generar_sql_migraciones_desde_commit "$commit"
      registrar_resultado "$commit" "$modo" "OK"
      paso "Generación de SQL auditable finalizada (sin aplicar cambios en la base de datos)."
      return 0
      ;;

    desplegar)
      asegurar_db_qa_saludable
      analizar_migraciones_commit "$commit"

      if [ "$ESTADO_HISTORIAL_EF" = "SIN_TABLA" ] || [ "$ESTADO_HISTORIAL_EF" = "TABLA_VACIA" ]; then
        falla "La base $BASE_QA_ESPERADA no está inicializada ($ESTADO_HISTORIAL_EF). Genere el SQL con «generar-sql», inspecciónelo y aplíquelo con «migrar»."
      fi

      if [ -s "$ARCHIVO_MIGRACIONES_PENDIENTES" ]; then
        echo "Migraciones pendientes detectadas en $BASE_QA_ESPERADA para el commit $commit:" >&2
        sed 's/^/  /' "$ARCHIVO_MIGRACIONES_PENDIENTES" >&2
        falla "El modo «desplegar» no aplica migraciones. El árbol Git local permanece intacto en $(git -C "$REPOSITORIO" rev-parse --short HEAD). Use «generar-sql» y luego «migrar»."
      fi

      # Respaldo previo obligatorio antes de actualizar servicios (RolSeeder/MetodosPagoSeeder corren al iniciar API)
      ejecutar_respaldo_obligatorio_qa

      anterior=$(git -C "$REPOSITORIO" rev-parse --short HEAD)
      git -C "$REPOSITORIO" checkout --quiet --force -B develop "$commit"
      paso "Código QA posicionado en $(git -C "$REPOSITORIO" log --oneline -1) (anterior: $anterior)"

      cd "$REPOSITORIO/despliegue"
      paso "Compilando secuencialmente imagen 1/2: api"
      compose_qa build api
      paso "Compilando secuencialmente imagen 2/2: caddy"
      compose_qa build caddy

      paso "Levantando contenedores del proyecto $PROYECTO_COMPOSE"
      if ! compose_qa up -d --wait --wait-timeout 300; then
        compose_qa ps --format '{{.Service}}: {{.Status}}'
        falla "Algún contenedor de QA no quedó sano. No se realiza rollback destructivo automático."
      fi
      compose_qa ps --format '{{.Service}}: {{.Status}}'

      verificar_endpoints_post_despliegue
      docker image prune -f > /dev/null
      registrar_resultado "$commit" "$modo" "OK"
      paso "Despliegue QA completado en modo «desplegar»: $(git -C "$REPOSITORIO" log --oneline -1)"
      return 0
      ;;

    migrar)
      [ "$confirmacion" = "$TOKEN_CONFIRMACION_MIGRAR" ] \
        || falla "Falta confirmación explícita ($TOKEN_CONFIRMACION_MIGRAR) para migrar $BASE_QA_ESPERADA."

      verificar_sql_auditable_para_migrar "$commit"
      asegurar_db_qa_saludable
      analizar_migraciones_commit "$commit"

      echo "Estado previo de $BASE_QA_ESPERADA: $ESTADO_HISTORIAL_EF"
      if [ -s "$ARCHIVO_MIGRACIONES_PENDIENTES" ]; then
        echo "Migraciones que se aplicarán sobre $BASE_QA_ESPERADA:"
        sed 's/^/  /' "$ARCHIVO_MIGRACIONES_PENDIENTES"
        cp "$ARCHIVO_MIGRACIONES_PENDIENTES" "$TMP_QA/migraciones-antes.txt"
      else
        echo "La base $BASE_QA_ESPERADA ya registra todas las migraciones del commit $commit; se verificará el script idempotente."
        : > "$TMP_QA/migraciones-antes.txt"
      fi

      # Respaldo obligatorio antes de tocar el checkout o aplicar SQL
      ejecutar_respaldo_obligatorio_qa

      anterior=$(git -C "$REPOSITORIO" rev-parse --short HEAD)
      git -C "$REPOSITORIO" checkout --quiet --force -B develop "$commit"
      paso "Código QA posicionado en $(git -C "$REPOSITORIO" log --oneline -1) (anterior: $anterior)"

      cd "$REPOSITORIO/despliegue"
      paso "Compilando secuencialmente imagen 1/2: api"
      compose_qa build api
      paso "Compilando secuencialmente imagen 2/2: caddy"
      compose_qa build caddy

      paso "Aplicando migraciones auditadas únicamente contra $BASE_QA_ESPERADA (usuario $USUARIO_QA_ESPERADO)"
      verificar_identidad_base_qa
      compose_qa exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -q' \
        < "$ARCHIVO_SQL_MIGRACIONES" \
        || falla "Falló la aplicación de migraciones en QA. No se ejecuta rollback destructivo automático; el respaldo previo está en despliegue/respaldos-qa."

      analizar_migraciones_commit "$commit"
      [ "$ESTADO_HISTORIAL_EF" = "CON_HISTORIAL" ] \
        || falla "Después de aplicar el SQL, la base $BASE_QA_ESPERADA no quedó con historial EF válido ($ESTADO_HISTORIAL_EF)."
      if [ -s "$ARCHIVO_MIGRACIONES_PENDIENTES" ]; then
        faltantes=$(tr '\n' ' ' < "$ARCHIVO_MIGRACIONES_PENDIENTES")
        falla "Después de aplicar migraciones.sql aún figuran migraciones pendientes en QA: $faltantes"
      fi

      total_aplicadas=$(wc -l < "$ARCHIVO_MIGRACIONES_APLICADAS" | tr -d ' ')
      if [ -s "$TMP_QA/migraciones-antes.txt" ]; then
        echo "Migraciones nuevas aplicadas y verificadas en __EFMigrationsHistory:"
        sed 's/^/  + /' "$TMP_QA/migraciones-antes.txt"
      fi
      echo "Total de migraciones registradas en $BASE_QA_ESPERADA: $total_aplicadas"

      paso "Levantando contenedores del proyecto $PROYECTO_COMPOSE"
      if ! compose_qa up -d --wait --wait-timeout 300; then
        compose_qa ps --format '{{.Service}}: {{.Status}}'
        falla "Algún contenedor de QA no quedó sano tras migrar. No se realiza rollback destructivo automático."
      fi
      compose_qa ps --format '{{.Service}}: {{.Status}}'

      verificar_endpoints_post_despliegue
      docker image prune -f > /dev/null
      registrar_resultado "$commit" "$modo" "OK"
      paso "Migración y despliegue QA completados: $(git -C "$REPOSITORIO" log --oneline -1)"
      return 0
      ;;
  esac
}

iniciar() {
  commit=${1:-}
  modo=${2:-}
  arg3=${3:-}
  arg4=${4:-}

  validar_argumentos "$commit" "$modo" "$arg3" "$arg4"
  confirmacion="$CONFIRMACION_RESUELTA"
  dominio_wf="$DOMINIO_ESPERADO_WORKFLOW"
  validar_entorno_qa

  if [ "${QA_TEST_SIN_SETSID:-0}" = "1" ] || ! command -v setsid >/dev/null 2>&1 || ! command -v flock >/dev/null 2>&1; then
    ejecutar_operacion "$commit" "$modo" "$confirmacion" "$dominio_wf"
    return $?
  fi

  mkdir -p "$REGISTROS"
  find "$REGISTROS" -name '*.log' -mtime +30 -delete
  registro="$REGISTROS/$(date -u +%Y%m%d-%H%M%S)-$modo-$(echo "$commit" | cut -c1-7).log"
  : > "$registro"
  echo "Registro en el servidor QA: $(basename "$registro")"

  setsid -w flock -n -E "$OCUPADO" "$CANDADO" sh "$SCRIPT" --ejecutar "$commit" "$modo" "$confirmacion" "$dominio_wf" \
    >> "$registro" 2>&1 < /dev/null &
  despliegue=$!
  tail -n +1 -f --pid="$despliegue" "$registro" &
  if wait "$despliegue"; then estado=0; else estado=$?; fi
  wait
  [ "$estado" -ne "$OCUPADO" ] || falla "Ya hay una operación QA en curso; revise ~/despliegues-qa."
  return "$estado"
}

principal() {
  case ${1:-} in
    --ejecutar)
      ejecutar_operacion "$2" "$3" "${4:-}" "${5:-}"
      ;;
    *)
      if [ $# -eq 0 ] && [ -n "${SSH_ORIGINAL_COMMAND:-}" ]; then
        set -f
        # shellcheck disable=SC2086
        set -- $SSH_ORIGINAL_COMMAND
        set +f
      fi
      [ $# -ge 2 ] && [ $# -le 4 ] \
        || falla "Uso: sh despliegue/actualizar-qa.sh <SHA_40_HEX> <validar|generar-sql|desplegar|migrar> [CONFIRMO_MIGRAR_QA:crm_teambenavides_qa] [--dominio-esperado=DOMINIO]"
      iniciar "$@"
      ;;
  esac
}

principal "$@"; exit $?
