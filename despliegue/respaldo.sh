#!/bin/sh
# Copias de la base con pg_dump, en el contenedor «respaldo» (ver docker-compose.yml).
#
#   sh /respaldo.sh                     copia diaria a HORA_RESPALDO (hora de Perú), sin parar
#   sh /respaldo.sh ahora               una copia ya, por ejemplo antes de actualizar
#   sh /respaldo.sh restaurar ARCHIVO   reemplaza la base por esa copia (con la API detenida)
#
# Las copias quedan en /respaldos (despliegue/respaldos en el servidor) en formato custom
# de pg_dump, y se borran las de más de RETENCION_DIAS días. La conexión sale de
# PGHOST, PGDATABASE, PGUSER y PGPASSWORD.
set -eu

DESTINO=/respaldos
HORA="${HORA_RESPALDO:-03:00}"
DIAS="${RETENCION_DIAS:-14}"
# Perú está en UTC-5 todo el año, sin horario de verano: no hace falta la base de zonas horarias.
DESFASE_PERU=18000

registrar() { echo "$(date -u -d "@$(( $(date -u +%s) - DESFASE_PERU ))" '+%Y-%m-%d %H:%M') respaldo: $*"; }

# En sh las variables de una función son globales: cada función usa nombres propios
# para no pisar las de quien la llama (restaurar llama a copiar).
copiar() {
  marca=$(date -u -d "@$(( $(date -u +%s) - DESFASE_PERU ))" +%Y%m%d-%H%M)
  copia="$DESTINO/${1:-crm}-$marca.dump"
  # Se escribe con otro nombre y se renombra al final: una copia cortada a la mitad
  # nunca queda con nombre de copia buena. Los errores se revisan a mano porque, cuando
  # la función se llama dentro de un if o un ||, sh no aplica set -e.
  if ! pg_dump --format=custom --file="$copia.parcial"; then
    rm -f "$copia.parcial"
    return 1
  fi
  # Que se pueda leer de vuelta: un archivo vacío o dañado no cuenta como copia.
  if ! pg_restore --list "$copia.parcial" > /dev/null; then
    rm -f "$copia.parcial"
    return 1
  fi
  mv "$copia.parcial" "$copia"
  registrar "copia lista: $(basename "$copia") ($(du -h "$copia" | cut -f1))"
}

limpiar() {
  find "$DESTINO" -name 'crm-*.dump' -mtime +"$DIAS" -print -delete | while read -r viejo; do
    registrar "borrada por antigua: $(basename "$viejo")"
  done
  rm -f "$DESTINO"/*.parcial
}

# Segundos que faltan para la próxima HORA_RESPALDO en Perú.
segundos_hasta_la_proxima() {
  ahora=$(date -u +%s)
  hoy_peru=$(date -u -d "@$(( ahora - DESFASE_PERU ))" +%Y-%m-%d)
  objetivo=$(( $(date -u -d "$hoy_peru $HORA UTC" +%s) + DESFASE_PERU ))
  [ "$objetivo" -le "$ahora" ] && objetivo=$(( objetivo + 86400 ))
  echo $(( objetivo - ahora ))
}

hay_copia_de_hoy() {
  [ -n "$(find "$DESTINO" -name 'crm-*.dump' -mmin -1440 | head -n 1)" ]
}

restaurar() {
  origen="$1"
  [ -f "$origen" ] || { registrar "no existe $origen"; exit 1; }
  pg_restore --list "$origen" > /dev/null || { registrar "$origen no es una copia de pg_dump válida"; exit 1; }

  otras=$(psql --dbname=postgres --tuples-only --no-align --command \
    "select count(*) from pg_stat_activity where datname = '$PGDATABASE' and pid <> pg_backend_pid()")
  if [ "$otras" -gt 0 ]; then
    registrar "hay $otras conexiones a $PGDATABASE: detener la API (docker compose stop api) y volver a correr"
    exit 1
  fi

  # Por si la copia elegida no era la correcta, primero se guarda la base como está.
  copiar antes-de-restaurar
  dropdb --if-exists "$PGDATABASE"
  createdb "$PGDATABASE"
  # Sin dueños ni permisos de la base de origen: sus roles pueden no existir en este servidor.
  pg_restore --no-owner --no-privileges --exit-on-error --dbname="$PGDATABASE" "$origen"
  migraciones=$(psql --tuples-only --no-align --command 'select count(*) from "__EFMigrationsHistory"')
  registrar "base restaurada desde $(basename "$origen"), con $migraciones migraciones. Ya se puede levantar la API."
}

mkdir -p "$DESTINO"

case "${1:-}" in
  ahora)
    copiar
    limpiar
    ;;
  restaurar)
    [ $# -eq 2 ] || { echo "Uso: sh /respaldo.sh restaurar /respaldos/ARCHIVO.dump"; exit 1; }
    restaurar "$2"
    ;;
  "")
    registrar "copia diaria a las $HORA (hora de Perú); se guardan $DIAS días"
    # Al arrancar (primer despliegue o después de una caída) no se espera a la noche.
    if ! hay_copia_de_hoy; then
      copiar || registrar "ERROR: la copia falló"
    fi
    while true; do
      espera=$(segundos_hasta_la_proxima)
      registrar "próxima copia en $(( espera / 3600 )) h $(( espera % 3600 / 60 )) min"
      sleep "$espera"
      if copiar; then
        limpiar
      else
        # Se reintenta en una hora; mientras, el chequeo de salud marca el contenedor.
        registrar "ERROR: la copia falló; se reintenta en una hora"
        sleep 3600
        copiar || registrar "ERROR: el reintento también falló"
      fi
    done
    ;;
  *)
    echo "Uso: sh /respaldo.sh [ahora | restaurar ARCHIVO]"
    exit 1
    ;;
esac
