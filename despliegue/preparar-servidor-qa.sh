#!/bin/sh
# Prepara un servidor Ubuntu 24.04 dedicado exclusivamente al entorno QA de CRM Team Benavides.
#
# Características de seguridad:
# - Compatible con POSIX sh (/bin/sh) para ejecución manual o cloud-init en AWS Lightsail.
# - Verifica Ubuntu 24.04, ejecución como root y existencia del usuario no root (ubuntu).
# - SE DETIENE si detecta una instalación de producción (/opt/crm-team-benavides o proyecto
#   crm-team-benavides) en la misma máquina.
# - Configura swap de 2 GB (/swapfile, vm.swappiness=10) si no existe.
# - Instala Docker Engine y Docker Compose Plugin desde el repositorio oficial de Docker.
# - Clona la rama «develop» en /opt/crm-team-benavides-qa con permisos mínimos para «ubuntu».
# - Es idempotente y se detiene de forma segura si encuentra una instalación incompatible.
# - NO genera llaves SSH automáticamente ni modifica servicios productivos.
set -eu

REPOSITORIO=https://github.com/natharceeirl/CRMTeamBenavides.git
RAMA=develop
DESTINO=/opt/crm-team-benavides-qa
DESTINO_PRODUCCION=/opt/crm-team-benavides
USUARIO=ubuntu

paso() { echo "== [QA-PREP] $*"; }
falla() { echo "ERROR [QA-PREP]: $*" >&2; exit 1; }

# ---------------------------------------------------------------------------
# 1. Comprobaciones previas de seguridad y aislamiento
# ---------------------------------------------------------------------------
paso "Verificando privilegios y sistema operativo"

[ "$(id -u)" -eq 0 ] || falla "Este script debe ejecutarse como root (sudo sh preparar-servidor-qa.sh)."

[ -f /etc/os-release ] || falla "No se encontró /etc/os-release."
# shellcheck source=/dev/null
. /etc/os-release

[ "${ID:-}" = "ubuntu" ] || falla "Sistema operativo incompatible (${ID:-desconocido}). Se requiere Ubuntu 24.04."
[ "${VERSION_ID:-}" = "24.04" ] || falla "Versión de Ubuntu incompatible (${VERSION_ID:-desconocida}). Se requiere Ubuntu 24.04."

id "$USUARIO" >/dev/null 2>&1 || falla "El usuario no root «$USUARIO» no existe en el servidor."

# Prohibir aprovisionar QA dentro del servidor de producción
if [ -e "$DESTINO_PRODUCCION" ]; then
  falla "Se detectó el directorio de producción ($DESTINO_PRODUCCION). QA debe aprovisionarse en un servidor independiente."
fi

if command -v docker >/dev/null 2>&1; then
  if docker ps -a --format '{{.Names}}' 2>/dev/null | grep -Ev '^crm-team-benavides-qa[-_]' | grep -Eq '^crm-team-benavides[-_]'; then
    falla "Se detectaron contenedores del proyecto de producción en este host. Abortando para proteger producción."
  fi
fi

# Si el directorio destino ya existe, validar que sea compatible antes de continuar
if [ -d "$DESTINO" ]; then
  if [ -d "$DESTINO/.git" ]; then
    remoto_actual=$(git -C "$DESTINO" remote get-url origin 2>/dev/null || true)
    rama_actual=$(git -C "$DESTINO" branch --show-current 2>/dev/null || true)
    [ "$remoto_actual" = "$REPOSITORIO" ] \
      || falla "El repositorio en $DESTINO apunta a un remoto incompatible: $remoto_actual"
    [ "$rama_actual" = "$RAMA" ] \
      || falla "El repositorio en $DESTINO está en la rama «$rama_actual» en lugar de «$RAMA». Deteniendo por seguridad."
  elif [ -n "$(ls -A "$DESTINO" 2>/dev/null)" ]; then
    falla "El directorio $DESTINO ya existe, no está vacío y no es un repositorio Git válido."
  fi
fi

# ---------------------------------------------------------------------------
# 2. Swap de 2 GB (necesario para compilación secuencial en instancias de 2 GB RAM)
# ---------------------------------------------------------------------------
paso "Configurando swap de 2 GB"
if ! swapon --show | grep -q '/swapfile'; then
  if [ ! -f /swapfile ]; then
    fallocate -l 2G /swapfile
  fi
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
fi
grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
echo 'vm.swappiness=10' > /etc/sysctl.d/99-swap.conf
sysctl -p /etc/sysctl.d/99-swap.conf >/dev/null

# ---------------------------------------------------------------------------
# 3. Docker Engine y Docker Compose Plugin (repositorio oficial de Docker)
# ---------------------------------------------------------------------------
paso "Instalando o verificando Docker Engine y Compose Plugin"
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y ca-certificates curl git
install -m 0755 -d /etc/apt/keyrings
if [ ! -f /etc/apt/keyrings/docker.asc ]; then
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
fi
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
  > /etc/apt/sources.list.d/docker.list
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker
usermod -aG docker "$USUARIO"

# ---------------------------------------------------------------------------
# 4. Clonación de la rama develop en /opt/crm-team-benavides-qa
# ---------------------------------------------------------------------------
paso "Configurando repositorio QA en $DESTINO (rama $RAMA)"
if [ ! -d "$DESTINO/.git" ]; then
  git clone --branch "$RAMA" --single-branch "$REPOSITORIO" "$DESTINO"
fi

mkdir -p "$DESTINO/despliegue/respaldos-qa" "$DESTINO/despliegue/migraciones-qa"
chown -R "$USUARIO:$USUARIO" "$DESTINO"
chmod 750 "$DESTINO" "$DESTINO/despliegue/respaldos-qa" "$DESTINO/despliegue/migraciones-qa"

# ---------------------------------------------------------------------------
# 5. Comprobaciones posteriores
# ---------------------------------------------------------------------------
paso "Ejecutando comprobaciones posteriores"

swapon --show | grep -q '/swapfile' || falla "El archivo /swapfile no quedó activo."
systemctl is-active --quiet docker || falla "El servicio Docker no está activo."
docker --version >/dev/null || falla "El binario docker no responde."
docker compose version >/dev/null || falla "El plugin docker compose no responde."
[ -f "$DESTINO/despliegue/docker-compose.qa.yml" ] || falla "No se encontró docker-compose.qa.yml en $DESTINO/despliegue."
[ -f "$DESTINO/despliegue/actualizar-qa.sh" ] || falla "No se encontró actualizar-qa.sh en $DESTINO/despliegue."
[ -f "$DESTINO/despliegue/respaldo-qa.sh" ] || falla "No se encontró respaldo-qa.sh en $DESTINO/despliegue."
[ "$(git -C "$DESTINO" branch --show-current)" = "$RAMA" ] || falla "La rama activa en $DESTINO no es $RAMA."

echo "Servidor QA listo: $(docker --version), $(docker compose version), swap de $(swapon --show --noheadings | awk '{print $3}'), repositorio en $DESTINO ($RAMA)."
echo "IMPORTANTE: Este script NO genera llaves SSH automáticamente."
echo "Siga el procedimiento de custodia de llaves, .env y primer despliegue en docs/qa.md."
