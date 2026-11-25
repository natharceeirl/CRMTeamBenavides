#!/bin/sh
# Copias de la base de datos QA (crm_teambenavides_qa) con pg_dump, en el contenedor
# «respaldo» de docker-compose.qa.yml.
#
#   sh /respaldo-qa.sh                     copia diaria a HORA_RESPALDO (hora de Perú)
#   sh /respaldo-qa.sh ahora               una copia inmediata (prefijo crm-qa-YYYYMMDD-HHMM.dump)
#   sh /respaldo-qa.sh restaurar ARCHIVO   reemplaza crm_teambenavides_qa por esa copia (con API detenida)
#
# Aislamiento QA:
# - Rechaza operar si PGDATABASE no es exactamente crm_teambenavides_qa o PGUSER no es crm_qa.
# - Utiliza el patrón de archivo exclusivo «crm-qa-YYYYMMDD-HHMM.dump» (y «antes-de-restaurar-qa-*»).
# - En restauración solo acepta archivos con patrón «crm-qa-*.dump» o «antes-de-restaurar-qa-*.dump».
set -eu

DESTINO="${DESTINO_RESPALDOS_QA:-/respaldos}"
HORA="${HORA_RESPALDO:-03:00}"
DIAS="${RETENCION_DIAS:-7}"
BASE_QA_ESPERADA=crm_teambenavides_qa
USUARIO_QA_ESPERADO=crm_qa
DESFASE_PERU=18000

registrar() { echo "$(date -u -d "@$(( $(date -u +%s) - DESFASE_PERU ))" '+%Y-%m-%d %H:%M') respaldo-qa: $*"; }
falla() { registrar "ERROR: $*" >&2; exit 1; }

verificar_destino_qa() {
  [ "${PGDATABASE:-}" = "$BASE_QA_ESPERADA" ] \
    || falla "PGDATABASE («${PGDATABASE:-}») debe ser exactamente «$BASE_QA_ESPERADA»."
  [ "${PGUSER:-}" = "$USUARIO_QA_ESPERADO" ] \
    || falla "PGUSER («${PGUSER:-}») debe ser exactamente «$USUARIO_QA_ESPERADO»."
}

copiar() {
  verificar_destino_qa
  prefijo="${1:-crm-qa}"
  marca=$(date -u -d "@$(( $(date -u +%s) - DESFASE_PERU ))" +%Y%m%d-%H%M)
  copia="$DESTINO/${prefijo}-$marca.dump"
  # Si ya existe un respaldo en el mismo minuto, añadir segundos para no sobrescribir el último respaldo bueno
  if [ -e "$copia" ]; then
    marca_seg=$(date -u -d "@$(( $(date -u +%s) - DESFASE_PERU ))" +%Y%m%d-%H%M%S)
    copia="$DESTINO/${prefijo}-$marca_seg.dump"
  fi
  if ! pg_dump --format=custom --file="$copia.parcial"; then
    rm -f "$copia.parcial"
    return 1
  fi
  if ! pg_restore --list "$copia.parcial" > /dev/null; then
    rm -f "$copia.parcial"
    return 1
  fi
  mv "$copia.parcial" "$copia"
  registrar "copia QA lista: $(basename "$copia") ($(du -h "$copia" | cut -f1))"
}

limpiar() {
  find "$DESTINO" -name 'crm-qa-*.dump' -mtime +"$DIAS" -print -delete | while read -r viejo; do
    registrar "borrada por retención ($DIAS días): $(basename "$viejo")"
  done
  rm -f "$DESTINO"/*.parcial
}

segundos_hasta_la_proxima() {
  ahora=$(date -u +%s)
  hoy_peru=$(date -u -d "@$(( ahora - DESFASE_PERU ))" +%Y-%m-%d)
  objetivo=$(( $(date -u -d "$hoy_peru $HORA UTC" +%s) + DESFASE_PERU ))
  [ "$objetivo" -le "$ahora" ] && objetivo=$(( objetivo + 86400 ))
  echo $(( objetivo - ahora ))
}

hay_copia_de_hoy() {
  [ -n "$(find "$DESTINO" -name 'crm-qa-*.dump' -mmin -1440 | head -n 1)" ]
}

esperar_base() {
  intentos=0
  until pg_isready --quiet; do
    [ "$intentos" -eq 0 ] && registrar "esperando a que la base QA acepte conexiones"
    intentos=$(( intentos + 1 ))
    [ "$intentos" -ge 60 ] && return 1
    sleep 5
  done
}

copiar_con_reintento() {
  esperar_base || registrar "la base QA no respondió en 5 minutos"
  if copiar crm-qa; then
    limpiar
    return 0
  fi
  registrar "ERROR: la copia QA falló; se reintenta en una hora"
  sleep 3600
  esperar_base || registrar "la base QA no respondió en 5 minutos"
  if copiar crm-qa; then
    limpiar
  else
    registrar "ERROR: el reintento de copia QA también falló"
  fi
}

restaurar() {
  verificar_destino_qa
  origen="$1"
  [ -f "$origen" ] || falla "No existe el archivo de respaldo $origen"

  nombre_archivo=$(basename "$origen")
  case "$nombre_archivo" in
    crm-qa-*.dump | antes-de-restaurar-qa-*.dump) ;;
    *)
      falla "Nombre de archivo no válido para restauración QA («$nombre_archivo»). Se exige patrón «crm-qa-*.dump» o «antes-de-restaurar-qa-*.dump»."
      ;;
  esac

  pg_restore --list "$origen" > /dev/null || falla "$origen no es una copia de pg_dump válida"

  otras=$(psql --dbname=postgres --tuples-only --no-align --command \
    "select count(*) from pg_stat_activity where datname = '$PGDATABASE' and pid <> pg_backend_pid()")
  if [ "$otras" -gt 0 ]; then
    falla "Hay $otras conexiones activas a $PGDATABASE: detenga la API (docker compose -p crm-team-benavides-qa -f docker-compose.qa.yml stop api) y vuelva a intentar"
  fi

  if ! copiar antes-de-restaurar-qa; then
    falla "No se pudo guardar el respaldo previo antes-de-restaurar-qa; abortando restauración"
  fi

  dropdb --if-exists "$PGDATABASE"
  createdb "$PGDATABASE"
  pg_restore --no-owner --no-privileges --exit-on-error --dbname="$PGDATABASE" "$origen"
  migraciones=$(psql --tuples-only --no-align --command \
    "select case when to_regclass('public.\"__EFMigrationsHistory\"') is null then 0 else (select count(*) from \"__EFMigrationsHistory\") end")
  registrar "base QA ($PGDATABASE) restaurada desde $nombre_archivo, con $migraciones migraciones registradas. Ya se puede levantar la API."
}

verificar_destino_qa
mkdir -p "$DESTINO"

case "${1:-}" in
  ahora)
    copiar crm-qa || falla "No se pudo generar el respaldo inmediato de QA."
    limpiar
    ;;
  restaurar)
    [ $# -eq 2 ] || { echo "Uso: sh /respaldo-qa.sh restaurar /respaldos/crm-qa-AAAAMMDD-HHMM.dump" >&2; exit 1; }
    restaurar "$2"
    ;;
  "")
    registrar "copia diaria QA a las $HORA (hora de Perú); retención $DIAS días"
    if ! hay_copia_de_hoy; then
      copiar_con_reintento
    fi
    while true; do
      espera=$(segundos_hasta_la_proxima)
      registrar "próxima copia QA en $(( espera / 3600 )) h $(( espera % 3600 / 60 )) min"
      sleep "$espera"
      copiar_con_reintento
    done
    ;;
  *)
    echo "Uso: sh /respaldo-qa.sh [ahora | restaurar /respaldos/crm-qa-AAAAMMDD-HHMM.dump]" >&2
    exit 1
    ;;
esac
