#!/bin/bash
# Prepara un Ubuntu 24.04 recién creado para la plataforma: swap, Docker y el repositorio.
#
# En Lightsail se pega en «Agregar script de lanzamiento» al crear la instancia: corre una
# sola vez, como root, en el primer arranque, y deja su salida en /var/log/cloud-init-output.log.
# En otro servidor: sudo bash preparar-servidor.sh. Se puede volver a correr sin romper nada.
set -euo pipefail

REPOSITORIO=https://github.com/natharceeirl/CRMTeamBenavides.git
RAMA=develop
DESTINO=/opt/crm-team-benavides
USUARIO=ubuntu

# 1. Swap de 2 GB: compilar una imagen llega a 1,24 GB y el servidor tiene 2 GB (docs/despliegue.md).
if ! swapon --show | grep -q /swapfile; then
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
fi
grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
# Que lo use solo cuando falte memoria, no para mover lo que está quieto.
echo 'vm.swappiness=10' > /etc/sysctl.d/99-swap.conf
sysctl -p /etc/sysctl.d/99-swap.conf

# 2. Docker Engine y Compose, del repositorio oficial de Docker.
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y ca-certificates curl git
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
# shellcheck source=/dev/null
. /etc/os-release
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
  > /etc/apt/sources.list.d/docker.list
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker
# El usuario de la instancia usa docker sin sudo (vale desde su próxima sesión).
usermod -aG docker "$USUARIO"

# 3. El repositorio. Es público: se clona sin credenciales.
if [ ! -d "$DESTINO/.git" ]; then
  git clone --branch "$RAMA" "$REPOSITORIO" "$DESTINO"
fi
chown -R "$USUARIO:$USUARIO" "$DESTINO"

echo "Servidor listo: $(docker --version), $(docker compose version), swap de $(swapon --show --noheadings | awk '{print $3}'), repositorio en $DESTINO ($RAMA)."
echo "Sigue en docs/despliegue.md desde «Primera vez en un servidor», paso 1."
