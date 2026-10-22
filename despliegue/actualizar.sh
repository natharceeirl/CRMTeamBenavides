#!/bin/sh
# Actualiza el servidor a un commit de main: imágenes nuevas, copia de la base,
# migraciones si las hay y arranque. Lo llama el workflow «Desplegar» de GitHub
# (.github/workflows/desplegar.yml) por SSH, con una llave que en ~/.ssh/authorized_keys
# solo puede correr este script:
#
#   restrict,command="sh /opt/crm-team-benavides/despliegue/actualizar.sh" ssh-ed25519 ...
#
# El commit llega en SSH_ORIGINAL_COMMAND. A mano, en el servidor:
#
#   sh despliegue/actualizar.sh COMMIT              sin migraciones nuevas
#   sh despliegue/actualizar.sh COMMIT migrar       aplica las migraciones nuevas
#
# Todo lo que imprime queda en el log del workflow, que es público porque el repositorio
# lo es: nunca el dominio, la IP ni una clave.
set -eu

REPOSITORIO=/opt/crm-team-benavides
SCRIPT="$REPOSITORIO/despliegue/actualizar.sh"
MIGRACIONES="$REPOSITORIO/src/CRMTeamBenavides.Api/Migrations"
REGISTROS="$HOME/despliegues"
CANDADO="$HOME/despliegue.lock"
# flock sale con este código si otro despliegue tiene el candado.
OCUPADO=75

paso() { echo "== $*"; }
falla() { echo "ERROR: $*" >&2; exit 1; }

# 1. En la sesión SSH: valida lo pedido y lanza el despliegue en una sesión aparte, con su
# propio registro. Si se corta la conexión con GitHub, el despliegue sigue igual y una
# migración nunca queda a medias.
iniciar() {
  commit=${1:-}
  migrar=${2:-sin-migrar}
  case $commit in
    *[!0-9a-f]* | '') falla "El commit tiene que ser un SHA completo, en minúsculas." ;;
  esac
  [ ${#commit} -eq 40 ] || falla "El commit tiene que ser un SHA completo, de 40 caracteres."
  case $migrar in
    migrar | sin-migrar) ;;
    *) falla "El segundo argumento es «migrar» o nada." ;;
  esac

  mkdir -p "$REGISTROS"
  find "$REGISTROS" -name '*.log' -mtime +60 -delete
  registro="$REGISTROS/$(date -u +%Y%m%d-%H%M%S)-$(echo "$commit" | cut -c1-7).log"
  : > "$registro"
  echo "Registro en el servidor: ~/despliegues/$(basename "$registro")"

  setsid -w flock -n -E "$OCUPADO" "$CANDADO" sh "$SCRIPT" --codigo "$commit" "$migrar" \
    >> "$registro" 2>&1 < /dev/null &
  despliegue=$!
  tail -n +1 -f --pid="$despliegue" "$registro" &
  if wait "$despliegue"; then estado=0; else estado=$?; fi
  wait
  [ "$estado" -ne "$OCUPADO" ] || falla "Ya hay un despliegue en curso; su registro está en ~/despliegues."
  return "$estado"
}

# 2. Con el candado: deja el código en el commit pedido, que tiene que estar en main de
# GitHub. Después sigue con la versión del script de ese commit, no con la de antes.
codigo() {
  cd "$REPOSITORIO"
  [ -z "$(git status --porcelain --untracked-files=no)" ] \
    || falla "El repositorio del servidor tiene cambios sin commitear; no se toca nada."
  git fetch --quiet origin main
  git merge-base --is-ancestor "$1" origin/main 2>/dev/null \
    || falla "El commit $1 no está en main de GitHub."
  anterior=$(git rev-parse --short HEAD)
  # La rama local main queda en el commit desplegado, aunque sea uno anterior.
  git checkout --quiet --force -B main "$1"
  paso "Código en $(git log --oneline -1) (antes: $anterior)"
  exec sh "$SCRIPT" --resto "$1" "$2"
}

# Las migraciones del commit que la base todavía no tiene, una por línea.
pendientes() {
  docker compose exec -T db sh -c \
    'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "select \"MigrationId\" from \"__EFMigrationsHistory\""' \
    > "$HOME/.migraciones-aplicadas" \
    || falla "No se pudo leer qué migraciones tiene la base."
  for archivo in "$MIGRACIONES"/*.cs; do
    nombre=$(basename "$archivo" .cs)
    case $nombre in *.Designer | *ModelSnapshot) continue ;; esac
    grep -qx "$nombre" "$HOME/.migraciones-aplicadas" || echo "$nombre"
  done
}

# 3. El resto, ya con el código nuevo.
resto() {
  cd "$REPOSITORIO/despliegue"

  # Antes de compilar: si va a parar por esto, que pare enseguida.
  nuevas=$(pendientes)
  if [ -n "$nuevas" ]; then
    echo "Migraciones nuevas:"
    echo "$nuevas" | sed 's/^/  /'
    [ "$2" = migrar ] || falla "Revisa esas migraciones y vuelve a correr el workflow con «Aplicar migraciones» marcado."
  fi

  # De a una: el servidor tiene 2 GB. La versión actual sigue atendiendo mientras tanto.
  paso "Imagen de la API"
  docker compose build api
  paso "Imagen de la web"
  docker compose build caddy

  paso "Copia de la base"
  docker compose exec -T respaldo sh /respaldo.sh ahora

  if [ -n "$nuevas" ]; then
    paso "Migraciones"
    (cd .. && docker build --quiet -f despliegue/Dockerfile.api --target migraciones --output despliegue/migraciones .) > /dev/null
    # Cada migración va en su transacción: si una falla, las anteriores quedan y esa no.
    docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -q' \
      < migraciones/migraciones.sql \
      || falla "Falló una migración. La versión anterior sigue atendiendo y la copia de recién está en despliegue/respaldos."
    faltan=$(pendientes)
    [ -z "$faltan" ] || falla "Después del script todavía faltan migraciones: $faltan"
  fi

  paso "Arranque"
  if ! docker compose up -d --wait --wait-timeout 300; then
    docker compose ps --format '{{.Service}}: {{.Status}}'
    falla "Algún contenedor no quedó sano. Los logs se ven en el servidor con «docker compose logs api»."
  fi
  docker compose ps --format '{{.Service}}: {{.Status}}'

  # Por Caddy y con su certificado, pero sin salir del servidor.
  dominio=$(grep '^DOMINIO=' .env | cut -d= -f2-)
  web=$(curl -s -o /dev/null -w '%{http_code}' --resolve "$dominio:443:127.0.0.1" "https://$dominio/" || true)
  api=$(curl -s -o /dev/null -w '%{http_code}' --resolve "$dominio:443:127.0.0.1" "https://$dominio/api/aprobaciones" || true)
  [ "$web" = 200 ] || falla "La web respondió $web en lugar de 200."
  # Sin sesión la API responde 401: llegó a la API y no se quedó en Caddy.
  [ "$api" = 401 ] || falla "La API respondió $api en lugar de 401."
  echo "Web: $web. API: $api sin sesión, como corresponde."

  # Las imágenes de la versión anterior ya no las usa nadie.
  docker image prune -f > /dev/null
  paso "Desplegado $(git log --oneline -1)"
}

principal() {
  case ${1:-} in
    --codigo) codigo "$2" "$3" ;;
    --resto) resto "$2" "$3" ;;
    *)
      if [ $# -eq 0 ] && [ -n "${SSH_ORIGINAL_COMMAND:-}" ]; then
        # Sin comodines: lo que manda el cliente son solo palabras.
        set -f
        # shellcheck disable=SC2086
        set -- $SSH_ORIGINAL_COMMAND
        set +f
      fi
      [ $# -le 2 ] || falla "Sobran argumentos."
      iniciar "$@"
      ;;
  esac
}

# En la misma línea: git reemplaza este archivo durante el despliegue y sh no debe leer
# nada más de él después.
principal "$@"; exit $?
