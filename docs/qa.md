# Entorno QA Independiente — CRM Team Benavides

Este documento define la arquitectura, el aprovisionamiento, la operación y los controles de seguridad del entorno **QA independiente** para validar cambios de la rama `develop` (incluyendo WhatsApp Fases 1–4) con **aislamiento total respecto de producción**.

> **Regla de oro:** El entorno QA corre en un servidor independiente, despliega exclusivamente commits de `origin/develop`, utiliza su propia base de datos (`crm_teambenavides_qa`), su propio usuario (`crm_qa`), sus propios secretos y mantiene las integraciones externas (`WhatsApp`, `WhatsAppAi`, `YamahaApi`) deshabilitadas (`false` / `Mock`) hasta contar con autorización expresa de Santiago.

---

## 1. Arquitectura QA

El entorno QA replica la topología de 4 contenedores de producción sobre un servidor dedicado (**AWS Lightsail Ubuntu 24.04 de 2 GB RAM, 60 GB SSD y 2 GB de swap**):

| Archivo | Propósito en QA |
|---|---|
| `despliegue/docker-compose.qa.yml` | Define el proyecto `crm-team-benavides-qa` con los servicios `db`, `api`, `respaldo` y `caddy`, volúmenes exclusivos (`crm-team-benavides-qa_*`) y red `172.31.0.0/24`. |
| `despliegue/Caddyfile.qa` | Servidor HTTPS con certificado Let's Encrypt automático para `{$DOMINIO}`, SPA React en `/srv`, proxy `/api/*` → `api:8080` y encabezado `X-Robots-Tag: noindex, nofollow`. |
| `despliegue/.env.qa.ejemplo` | Plantilla de variables para los 4 contenedores con `COMPOSE_FILE=docker-compose.qa.yml`, `COMPOSE_PROJECT_NAME=crm-team-benavides-qa` y marcadores `REEMPLAZAR_CON_*`. |
| `despliegue/preparar-servidor-qa.sh` | Script POSIX `/bin/sh` idempotente que valida Ubuntu 24.04, aborta si detecta producción, crea `/swapfile` (2 GB), instala Docker Engine + Compose y clona `develop` en `/opt/crm-team-benavides-qa`. |
| `despliegue/actualizar-qa.sh` | Orquestador exclusivo de QA con cuatro modos (`validar`, `generar-sql`, `desplegar`, `migrar`), validación de SHA de 40 caracteres en `origin/develop`, verificación de manifiesto SHA-256 de migraciones y compilación secuencial. |
| `despliegue/respaldo-qa.sh` | Script exclusivo de copias diarias y bajo demanda de QA (`crm-qa-YYYYMMDD-HHMM.dump`) con verificación obligatoria de `crm_teambenavides_qa` / `crm_qa`. |
| `.github/workflows/desplegar-qa.yml` | Workflow de GitHub Actions disparado por etiquetas `qa-validar-*` y `qa-desplegar-*` sobre commits de `develop` (sin modificar `main`), protegido por el GitHub Environment `qa`. |

---

## 2. Diferencias entre QA y Producción

| Dimensión | Producción | QA Independiente |
|---|---|---|
| **Servidor** | Instancia Lightsail Producción | Instancia Lightsail QA dedicada |
| **Directorio en host** | `/opt/crm-team-benavides` | `/opt/crm-team-benavides-qa` |
| **Rama autorizada** | `main` (`origin/main`) | `develop` (`origin/develop`) |
| **Proyecto Docker Compose** | `crm-team-benavides` | `crm-team-benavides-qa` (`COMPOSE_PROJECT_NAME`) |
| **Archivo Compose** | `despliegue/docker-compose.yml` | `despliegue/docker-compose.qa.yml` (`COMPOSE_FILE`) |
| **Entorno ASP.NET Core** | `Production` | `Staging` |
| **Base de datos PostgreSQL 18** | `crm_teambenavides` | `crm_teambenavides_qa` |
| **Usuario PostgreSQL** | `crm` | `crm_qa` |
| **Red Docker interna** | `172.30.0.0/24` | `172.31.0.0/24` (`crm-team-benavides-qa_interna`) |
| **Volúmenes Docker** | `base`, `datos-api`, `caddy-datos`, `caddy-config` | `crm-team-benavides-qa_base`, `crm-team-benavides-qa_datos-api`, `crm-team-benavides-qa_caddy-datos`, `crm-team-benavides-qa_caddy-config` |
| **Script y carpeta de respaldos** | `respaldo.sh` en `despliegue/respaldos/` (`crm-*.dump`, 14 días) | `respaldo-qa.sh` en `despliegue/respaldos-qa/` (`crm-qa-*.dump`, 7 días) |
| **Carpeta de SQL de migraciones** | `despliegue/migraciones/` | `despliegue/migraciones-qa/` (`migraciones.sql` + `migraciones.manifest`) |
| **Caddyfile** | `despliegue/Caddyfile` | `despliegue/Caddyfile.qa` (añade `X-Robots-Tag: noindex, nofollow`) |
| **GitHub Environment y secretos** | `produccion` (`SERVIDOR_*`) | `qa` (`QA_SERVIDOR_*` + variable `QA_DOMINIO`) |

---

## 3. Variables necesarias (`.env` en QA)

En `/opt/crm-team-benavides-qa/despliegue/.env` (copiado desde `.env.qa.ejemplo` con permisos `chmod 600 .env`):

| Variable | Valor requerido en QA | Notas de seguridad |
|---|---|---|
| `COMPOSE_FILE` | `docker-compose.qa.yml` | **Obligatorio.** Impide que un comando `docker compose` sin `-f` seleccione `docker-compose.yml`. |
| `COMPOSE_PROJECT_NAME` | `crm-team-benavides-qa` | **Obligatorio.** Impide que un comando `docker compose` sin `-p` use el nombre de proyecto productivo. |
| `DOMINIO` | Subdominio exclusivo de QA (ej. `qa.<dominio-empresa>`) | Obligatorio. Debe coincidir con `QA_DOMINIO` del GitHub Environment `qa`. No puede ser `app.teambenavides.com`. |
| `HSTS_SEGUNDOS` | `0` (inicialmente en QA) | Evita fijar HSTS en navegadores durante pruebas iniciales de certificado. |
| `PUERTO_HTTP` / `PUERTO_HTTPS` | `80` / `443` | Meta Cloud API exige puerto estándar `443` con TLS válido. |
| `POSTGRES_DB` | `crm_teambenavides_qa` | Exigido tanto por `actualizar-qa.sh` como por `ValidacionConfiguracionEntorno` en `Staging`. |
| `POSTGRES_USER` | `crm_qa` | Exigido por `actualizar-qa.sh`, `respaldo-qa.sh` y `ValidacionConfiguracionEntorno`. |
| `POSTGRES_PASSWORD` | `openssl rand -hex 32` (64 caracteres hex) | Exclusiva de QA. Mínimo 32 caracteres y alta entropía (sin repeticiones ni placeholders). |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | Activa las reglas estrictas de aislamiento QA al iniciar la API. |
| `JWT_SECRET_KEY` | `openssl rand -hex 48` (96 caracteres hex) | Exclusiva de QA. Mínimo 48 caracteres y alta entropía (sin repeticiones ni placeholders). |
| `RED_INTERNA_QA` | `172.31.0.0/24` | Subred confiable para `Proxy__RedesConfiables__0` (obligatorio en `Staging`). |
| `HORA_RESPALDO` / `RETENCION_DIAS` | `03:00` / `7` | Respaldos diarios `crm-qa-*.dump` en `despliegue/respaldos-qa/`. |
| `NOTIFICACIONES_PROVEEDOR` | `Registro` | Solo registra en logs sin enviar notificaciones push reales. |
| `YAMAHA_ENABLED` | `false` | Deshabilitado obligatoriamente en QA (`Staging`). |
| `WHATSAPP_ENABLED` / `WHATSAPP_PROVIDER` | `false` / `Mock` | Deshabilitado y en proveedor simulado determinista. |
| `WHATSAPP_AI_ENABLED` / `WHATSAPP_AI_PROVIDER` | `false` / `Mock` | Deshabilitada y en proveedor simulado determinista. |

---

## 4. Aprovisionamiento, DNS y TLS

1. **Instancia AWS Lightsail QA:**
   - Sistema operativo: **Ubuntu 24.04 LTS**.
   - Plan: **2 GB RAM, 2 vCPU, 60 GB SSD, 3 TB transferencia** (~US$10–13/mes).
   - Asignar una **IP estática dedicada a QA** en la consola de Lightsail.
   - Firewall de Lightsail: abrir únicamente puertos `22/tcp`, `80/tcp`, `443/tcp` y `443/udp`. **Nunca abrir `5432` ni `8080`.**
2. **DNS y certificado TLS:**
   - Crear un registro DNS tipo `A` para el subdominio QA apuntando a la IP estática del servidor QA.
   - Verificar propagación DNS (`dig +short <DOMINIO_QA>`) antes de iniciar Caddy para que Let's Encrypt emita el certificado TLS en el primer intento.
   - `Caddyfile.qa` incluye `X-Robots-Tag "noindex, nofollow"` y no aplica Basic Auth global para no interferir con `/api/*` ni con `/api/whatsapp/webhook`.

---

## 5. Configuración del GitHub Environment `qa` y custodia SSH

En GitHub (**Settings → Environments → New environment → `qa`**):

1. **Deployment branches and tags:** seleccionar *Selected branches and tags* y agregar:
   - Rama: `develop`
   - Tags: `qa-validar-*` y `qa-desplegar-*`
2. **Deployment protection rules:** activar **Required reviewers** asignando al responsable autorizado. Esto garantiza que el trabajo `desplegar-qa` quede pausado esperando aprobación humana **antes** de que GitHub exponga los secretos SSH al runner.
3. **Environment secrets** (exclusivos de QA, jamás reutilizar los de `produccion`):
   - `QA_SERVIDOR_HOST`: IP estática del servidor QA.
   - `QA_SERVIDOR_SSH_CLAVE`: llave privada Ed25519 generada exclusivamente para desplegar en QA.
   - `QA_SERVIDOR_SSH_HUELLA`: salida de `ssh-keyscan -t ed25519 <IP_QA>`.
4. **Environment variables:**
   - `QA_DOMINIO`: subdominio público de QA (debe coincidir exactamente con `DOMINIO` del `.env` en el servidor QA).

### Generación y registro de la llave SSH exclusiva de QA

`preparar-servidor-qa.sh` **no genera llaves SSH automáticamente**. El administrador debe generar el par de llaves en una estación segura:

```sh
ssh-keygen -t ed25519 -N "" -C "github-actions-desplegar-qa" -f llave_qa
```

- Registrar `llave_qa.pub` en `/home/ubuntu/.ssh/authorized_keys` del servidor QA con restricción de comando obligatoria:

```text
restrict,command="sh /opt/crm-team-benavides-qa/despliegue/actualizar-qa.sh" ssh-ed25519 AAAA... github-actions-desplegar-qa
```

- Guardar el contenido de `llave_qa` en el secreto `QA_SERVIDOR_SSH_CLAVE` del Environment `qa` y eliminar la copia temporal local.

---

## 6. Preparación inicial del servidor QA y primer despliegue sobre base vacía

### 6.1. Cómo transferir y ejecutar `preparar-servidor-qa.sh` en un servidor nuevo

En una instancia Ubuntu 24.04 recién creada aún no existe `/opt/crm-team-benavides-qa`. Existen tres alternativas para ejecutar `preparar-servidor-qa.sh`:

| Alternativa | Descripción | Recomendación |
|---|---|---|
| **A. Copia por `scp` y ejecución por SSH (Recomendada)** | Se transfiere el archivo desde una estación con la rama `develop` hacia `/tmp/preparar-servidor-qa.sh` y se ejecuta viendo la salida en tiempo real. | **Recomendada** por visibilidad inmediata de cada comprobación y código de salida. |
| **B. Script de lanzamiento (`cloud-init`) en Lightsail** | Se pega el contenido de `despliegue/preparar-servidor-qa.sh` en *«Agregar script de lanzamiento»* al crear la instancia. Corre como `root` con `/bin/sh` en el primer arranque y registra en `/var/log/cloud-init-output.log`. | Soportada (el script es 100 % compatible con POSIX `/bin/sh`). |
| **C. Creación manual en consola** | Conectar por SSH, pegar el script en `/tmp/preparar-servidor-qa.sh` y ejecutarlo. | Más propensa a errores de copiado manual. |

**Procedimiento recomendado (Alternativa A):**

1. Desde la estación del administrador (ubicada en la rama `develop` del repositorio local):

   ```sh
   scp despliegue/preparar-servidor-qa.sh ubuntu@<IP_QA>:/tmp/preparar-servidor-qa.sh
   ssh ubuntu@<IP_QA> "sudo sh /tmp/preparar-servidor-qa.sh && rm -f /tmp/preparar-servidor-qa.sh"
   ```

   El script verifica Ubuntu 24.04, aborta si detecta `/opt/crm-team-benavides` o contenedores productivos, crea `/swapfile` de 2 GB, instala Docker Engine + Compose y clona `develop` en `/opt/crm-team-benavides-qa` con propietario `ubuntu:ubuntu` y permisos `750`.

### 6.2. Configuración de `.env` y primer despliegue controlado (base vacía)

2. Iniciar una nueva sesión SSH con el usuario `ubuntu` (para aplicar la pertenencia al grupo `docker`) y crear `/opt/crm-team-benavides-qa/despliegue/.env`:

   ```sh
   cd /opt/crm-team-benavides-qa/despliegue
   cp .env.qa.ejemplo .env
   chmod 600 .env
   ```

   Editar `.env` manteniendo `COMPOSE_FILE=docker-compose.qa.yml` y `COMPOSE_PROJECT_NAME=crm-team-benavides-qa`, y reemplazando `DOMINIO`, `POSTGRES_PASSWORD` (`openssl rand -hex 32`) y `JWT_SECRET_KEY` (`openssl rand -hex 48`).

3. Obtener el SHA completo de 40 caracteres de `origin/develop` y ejecutar el modo **`validar`**:

   ```sh
   cd /opt/crm-team-benavides-qa
   git fetch origin develop
   SHA=$(git rev-parse origin/develop)
   sh despliegue/actualizar-qa.sh "$SHA" validar
   ```

4. Generar el artefacto SQL idempotente auditable y su manifiesto SHA-256 con el modo **`generar-sql`**:

   ```sh
   sh despliegue/actualizar-qa.sh "$SHA" generar-sql
   ```

5. **Inspeccionar el SQL generado y su manifiesto** antes de autorizar la migración:

   ```sh
   cat despliegue/migraciones-qa/migraciones.manifest
   less despliegue/migraciones-qa/migraciones.sql
   ```

6. Aplicar la migración inicial y levantar los servicios con el modo **`migrar`** y confirmación inequívoca:

   ```sh
   sh despliegue/actualizar-qa.sh "$SHA" migrar CONFIRMO_MIGRAR_QA:crm_teambenavides_qa
   ```

   En este primer arranque, `actualizar-qa.sh`:
   - Inicia automáticamente `db` y `respaldo` del proyecto `crm-team-benavides-qa` si aún no estaban corriendo.
   - Comprueba que la base sea `crm_teambenavides_qa` (`crm_qa`) y detecta limpiamente el estado inicial `SIN_TABLA` (ausencia de `__EFMigrationsHistory`).
   - Verifica que `migraciones.sql` corresponda exactamente a `$SHA` y que su `SHA-256` coincida con `migraciones.manifest`.
   - Toma un respaldo previo recuperable en `despliegue/respaldos-qa/crm-qa-*.dump`.
   - Compila secuencialmente `api` y `caddy`, aplica `migraciones.sql`, verifica que `__EFMigrationsHistory` quede en estado `CON_HISTORIAL` con 0 migraciones pendientes, levanta los 4 contenedores y comprueba HTTPS (`200`, `401`, `503` en webhook y `X-Robots-Tag: noindex`).

7. Crear el primer usuario de Gerencia en QA:

   ```sh
   cd /opt/crm-team-benavides-qa/despliegue
   docker compose -p crm-team-benavides-qa -f docker-compose.qa.yml run --rm api crear-gerencia
   ```

---

## 7. Operación cotidiana: los 4 modos de `actualizar-qa.sh` y el Workflow de GitHub Actions

### 7.1. Los 4 modos de `actualizar-qa.sh`

| Modo | Comando en `/opt/crm-team-benavides-qa` | Qué hace |
|---|---|---|
| **`validar`** | `sh despliegue/actualizar-qa.sh <SHA_40_HEX> validar` | Extrae los archivos del `<SHA_40_HEX>` en un directorio temporal sin mover el checkout activo, valida `.env`, `docker-compose.qa.yml` y scripts del commit objetivo, e informa el estado de `__EFMigrationsHistory` si `db` está activo. |
| **`generar-sql`** | `sh despliegue/actualizar-qa.sh <SHA_40_HEX> generar-sql` | Extrae el árbol fuente de `<SHA_40_HEX>` en un directorio temporal y construye `despliegue/migraciones-qa/migraciones.sql` junto con `migraciones.manifest` (`COMMIT`, `SHA256`, `LINES`, `DATABASE`) sin aplicarlo a la base. |
| **`desplegar`** | `sh despliegue/actualizar-qa.sh <SHA_40_HEX> desplegar` | Verifica **antes de mover el checkout** que la base esté inicializada y no tenga migraciones pendientes para `<SHA_40_HEX>`. Si hay migraciones pendientes, aborta sin alterar el árbol Git. Si no hay pendientes, toma respaldo obligatorio `crm-qa-*.dump`, actualiza el código, compila secuencialmente `api` y `caddy`, levanta contenedores y verifica HTTPS. |
| **`migrar`** | `sh despliegue/actualizar-qa.sh <SHA_40_HEX> migrar CONFIRMO_MIGRAR_QA:crm_teambenavides_qa` | Exige haber corrido antes `generar-sql` para `<SHA_40_HEX>`. Verifica el `SHA-256` del SQL contra `migraciones.manifest` (rechazándolo si fue alterado), toma respaldo obligatorio `crm-qa-*.dump`, aplica el SQL contra `crm_teambenavides_qa`, comprueba el historial EF y despliega los contenedores. |

### 7.2. Ejecución desde GitHub Actions sin modificar `main`

**Limitación real de GitHub Actions:** GitHub solo habilita el botón manual *Run workflow* (`workflow_dispatch`) en la interfaz web cuando el archivo de workflow existe en la rama predeterminada del repositorio (`main`). Para **no modificar `main`** y aun así disparar validaciones y despliegues manuales en GitHub Actions desde `develop`, `.github/workflows/desplegar-qa.yml` utiliza etiquetas (`tags`) con prefijo `qa-validar-*` y `qa-desplegar-*`:

1. **Disparar una validación en QA (`modo=validar`):**

   ```sh
   git fetch origin develop
   SHA=$(git rev-parse origin/develop)
   git tag qa-validar-20261124-01 "$SHA"
   git push origin qa-validar-20261124-01
   ```

2. **Disparar un despliegue sin migraciones en QA (`modo=desplegar`):**

   ```sh
   git fetch origin develop
   SHA=$(git rev-parse origin/develop)
   git tag qa-desplegar-20261124-01 "$SHA"
   git push origin qa-desplegar-20261124-01
   ```

3. **Flujo de seguridad del workflow:**
   - El trabajo `pruebas-qa` (sin acceso a secretos) verifica que el commit de la etiqueta sea ancestro de `origin/develop`, ejecuta las pruebas backend (`Category!=Integration`) y las verificaciones frontend (`npm run verificar`).
   - El trabajo `desplegar-qa` solicita la aprobación humana configurada en *Required reviewers* del GitHub Environment `qa`.
   - Una vez aprobado, valida los secretos `QA_SERVIDOR_*` y envía a `actualizar-qa.sh` el comando `$COMMIT $MODO --dominio-esperado=$QA_DOMINIO`.
   - **El workflow jamás ejecuta migraciones (`migrar` no está permitido en el workflow).**

4. **Cómo detener o cancelar una ejecución del workflow:**
   - En GitHub → pestaña **Actions** → seleccionar la ejecución en curso de *Desplegar QA* → pulsar **Cancel workflow** (o **Review deployments → Reject** si está esperando aprobación en el Environment `qa`).
   - Para eliminar la etiqueta utilizada:
     ```sh
     git tag -d qa-desplegar-20261124-01
     git push --delete origin qa-desplegar-20261124-01
     ```

---

## 8. Respaldos (`crm-qa-*.dump`), Restauración y Rollback

### Respaldos automáticos y manuales
- El servicio `respaldo` ejecuta `/respaldo-qa.sh`, que verifica que `PGDATABASE=crm_teambenavides_qa` y `PGUSER=crm_qa`, y genera copias diarias a las `03:00` (hora Perú) en `/opt/crm-team-benavides-qa/despliegue/respaldos-qa/` con nombres **`crm-qa-YYYYMMDD-HHMM.dump`** (o `crm-qa-YYYYMMDD-HHMMSS.dump` si ya existe una copia del mismo minuto, para nunca sobrescribir el último respaldo bueno) y retención de **7 días**.
- Para generar un respaldo manual inmediato:

  ```sh
  cd /opt/crm-team-benavides-qa/despliegue
  docker compose -p crm-team-benavides-qa -f docker-compose.qa.yml exec respaldo sh /respaldo-qa.sh ahora
  ```

### Recuperación de versión anterior de la aplicación (Rollback de código)
- Si entre el commit actual y el commit anterior de `develop` **no hubo migraciones nuevas**, basta con desplegar el SHA anterior:

  ```sh
  cd /opt/crm-team-benavides-qa
  sh despliegue/actualizar-qa.sh <SHA_ANTERIOR_40_HEX> desplegar
  ```

### Restauración de la base de datos QA desde un respaldo
- Si es necesario revertir una migración o restaurar el estado previo de `crm_teambenavides_qa`:

  ```sh
  cd /opt/crm-team-benavides-qa/despliegue
  docker compose -p crm-team-benavides-qa -f docker-compose.qa.yml stop api
  docker compose -p crm-team-benavides-qa -f docker-compose.qa.yml exec respaldo \
    sh /respaldo-qa.sh restaurar /respaldos/crm-qa-AAAAMMDD-HHMM.dump
  docker compose -p crm-team-benavides-qa -f docker-compose.qa.yml start api
  ```

  `respaldo-qa.sh restaurar` verifica que no haya conexiones activas, exige que el archivo tenga prefijo `crm-qa-*.dump` o `antes-de-restaurar-qa-*.dump` (rechazando dumps de producción), guarda antes una copia de seguridad `antes-de-restaurar-qa-YYYYMMDD-HHMM.dump` y restaura la base.

---

## 9. Estrategia de Datos Sintéticos y Controles de Seguridad

- **Prohibición de PII real:** No transferir `backup_crm_teambenavides_20261016_230031.sql` ni datos reales de clientes a QA sin autorización expresa y anonimización previa.
- **Fixture de pruebas (`QaSyntheticDataFixture`):** Exclusivo del proyecto de pruebas (`tests/CRMTeamBenavides.Api.Tests/QaSyntheticDataFixture.cs`). Cuenta con la guarda `ValidarDestinoAutorizado` que bloquea su ejecución contra `crm_teambenavides` (producción) y `crm_teambenavides_qa` (QA compartido), y genera hashes PBKDF2 reales con contraseña fuerte (`Qa.Sintetico#2026!Segura9`).
- **Validador de arranque (`ValidacionConfiguracionEntorno`):** En `Staging`, la API aborta inmediatamente si `JwtSettings:SecretKey` (< 48 caracteres, repetitivo o placeholder) o la contraseña de PostgreSQL (< 32 caracteres, repetitiva o placeholder) son inválidos, si la base no es `crm_teambenavides_qa`, si el usuario no es `crm_qa`, si `Proxy:RedesConfiables` está vacío o incluye `172.30.0.0/24`, o si `WhatsApp`, `WhatsAppAi` o `YamahaApi` están habilitados.

---

## 10. Operaciones que requieren autorización de Santiago

1. **Presupuesto y servidor AWS Lightsail QA:** creación de la instancia Ubuntu 24.04 (2 GB RAM / 60 GB SSD) e IP estática.
2. **Dominio y DNS:** asignación del subdominio QA y creación del registro `A`.
3. **GitHub Environment `qa`:** creación del Environment, activación de *Required reviewers*, restricción a `develop` / `qa-*` y carga de `QA_SERVIDOR_HOST`, `QA_SERVIDOR_SSH_CLAVE`, `QA_SERVIDOR_SSH_HUELLA` y `QA_DOMINIO`.
4. **Política de datos y activación futura de integraciones externas:** cualquier uso de datos distintos de los sintéticos o la activación futura de Meta Sandbox / proveedor IA en QA.
