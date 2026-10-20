# Despliegue

La plataforma corre en un solo servidor con Docker: Caddy recibe el HTTPS y sirve la web, la API atiende `/api` y PostgreSQL guarda los datos. Los archivos están en [despliegue/](../despliegue). Las copias diarias de la base se agregan en el paso siguiente.

| Archivo | Qué es |
|---|---|
| `despliegue/docker-compose.yml` | Los tres contenedores, sus volúmenes y la red interna. Solo Caddy publica puertos. |
| `despliegue/Dockerfile.api` | La API compilada. Tiene además una etapa que genera el script de migraciones. |
| `despliegue/Dockerfile.web` | La web compilada dentro de la imagen de Caddy. |
| `despliegue/Caddyfile` | HTTPS automático, `/api` hacia la API y el resto, la web. |
| `despliegue/.env.ejemplo` | Las variables del servidor, sin valores. Se copia como `.env`, que no se sube al repositorio. |

## Primera vez en un servidor

Hace falta un servidor Linux con Docker, el DNS del dominio apuntando a él y los puertos 80 y 443 abiertos. Los comandos se corren en `despliegue/`, salvo el del paso 3.

1. Copiar el repositorio al servidor y crear el `.env`:

   ```
   cp .env.ejemplo .env
   ```

   Completar `DOMINIO`, `POSTGRES_PASSWORD` y `JWT_SECRET_KEY`. Las claves se generan con `openssl rand -hex 32` y `openssl rand -hex 48`.
2. Levantar solo la base:

   ```
   docker compose up -d db
   ```

3. Generar el script de migraciones desde la raíz del repositorio y revisarlo. La API no migra sola al arrancar.

   ```
   docker build -f despliegue/Dockerfile.api --target migraciones --output despliegue/migraciones .
   ```

4. Aplicarlo. El script se puede volver a correr: solo aplica las migraciones que faltan.

   ```
   docker compose exec -T db psql -U crm -d crm_teambenavides -v ON_ERROR_STOP=1 < migraciones/migraciones.sql
   ```

5. Crear el primer usuario de Gerencia (sección siguiente):

   ```
   docker compose run --rm api crear-gerencia
   ```

6. Levantar todo:

   ```
   docker compose up -d --build
   ```

   `docker compose ps` tiene que mostrar los tres contenedores en `healthy` o `running`. La primera vez Caddy tarda unos segundos en obtener el certificado.

   Al arrancar, la API deja dos avisos que no son errores:
   - las llaves de Data Protection se guardan sin cifrar, lo que en un volumen del servidor es aceptable;
   - no encuentra un puerto HTTPS para redirigir. Lo provoca el chequeo de salud interno, que entra por HTTP; detrás de Caddy la API ya ve HTTPS.

## Actualizar

1. `git pull` en el servidor.
2. Generar el script de migraciones (paso 3) y aplicarlo si trae migraciones nuevas, siempre con una copia de la base hecha antes.
3. `docker compose up -d --build`. Se reconstruyen la API y la web; la base y los volúmenes no se tocan.

Los logs se ven con `docker compose logs -f api` o `caddy`. Docker los rota: guarda cinco archivos de 10 MB por contenedor.

## Copias de la base

El contenedor `respaldo` hace una copia diaria con `pg_dump` a la hora `HORA_RESPALDO` de Perú (03:00 por defecto). La guarda en `despliegue/respaldos/` y borra las de más de `RETENCION_DIAS` días (14 por defecto). Al arrancar hace una copia si no hay ninguna de las últimas 24 horas.

- Cada copia se comprueba con `pg_restore --list` antes de darla por buena; una copia cortada a la mitad nunca queda con nombre de copia buena.
- Si una copia falla, se reintenta en una hora. `docker compose ps` muestra `respaldo` como `unhealthy` si la última copia tiene más de 26 horas.
- Una copia ya, por ejemplo antes de actualizar:

  ```
  docker compose exec respaldo sh /respaldo.sh ahora
  ```

### Restaurar una copia

Reemplaza la base entera por la copia elegida. Antes guarda la base como está, en `respaldos/antes-de-restaurar-*.dump`, por si la copia no era la correcta. Esas copias no se borran solas.

```
docker compose stop api
docker compose exec respaldo sh /respaldo.sh restaurar /respaldos/crm-AAAAMMDD-HHMM.dump
docker compose start api
```

Si la API sigue conectada a la base, el script no hace nada y lo avisa.

La copia también se puede restaurar fuera de Docker, en cualquier PostgreSQL 18:

```
createdb <base>
pg_restore --no-owner --no-privileges --exit-on-error -d <base> crm-AAAAMMDD-HHMM.dump
```

### Fuera del servidor

Una copia que vive solo en el servidor se pierde con él. Falta llevar `despliegue/respaldos/` a otro lugar cada día: el plan es Cloudflare R2, cuando haya cuenta. Mientras tanto, se pueden copiar a mano con `scp`.

## Ensayar en una PC

Con Docker Desktop, el mismo paquete corre en Windows:

- En el `.env`: `DOMINIO=localhost`, `HSTS_SEGUNDOS=0`, `PUERTO_HTTP=8080` y `PUERTO_HTTPS=8443`.
- Se entra por https://localhost:8443. El certificado lo emite Caddy y el navegador avisa que no lo conoce.
- El paso 4 usa `<`, que PowerShell no tiene: correrlo en Git Bash.
- `docker compose down -v` borra todo, incluida la base. Las copias de `despliegue/respaldos/` quedan en la PC y se borran a mano.

Se ensayó así el 20/10, siguiendo los pasos de arriba:
- las imágenes se construyeron sin errores;
- el script aplicó las 21 migraciones y una segunda corrida no cambió nada;
- `crear-gerencia` creó la cuenta y rechazó una segunda.

Con todo levantado:
- las 17 pantallas del menú cargaron sin errores;
- la API guardó la IP del usuario y no la de Caddy;
- el intento 11 del minuto recibió un 429;
- las llaves y las fotos siguieron ahí después de reiniciar la API;
- la plantilla de Excel se generó bien en Linux.

Las copias se ensayaron el mismo día:
- la primera salió al arrancar `respaldo` y otra salió sola a la hora programada;
- la retención borró una copia de hace 20 días y dejó la de 10, junto con las de antes de restaurar;
- con la API conectada, `restaurar` no hizo nada y lo avisó;
- con la API detenida, `restaurar` dejó en Docker una copia de la base de desarrollo idéntica a la original: las mismas filas en las 45 tablas;
- la web mostró esos datos sin errores;
- una copia hecha en Docker se restauró en el PostgreSQL de Windows con las mismas filas.

## Primer usuario de Gerencia

En producción la base nace sin usuarios: el usuario de desarrollo (`DevelopmentUser`) solo se crea en el entorno Development. El primero se crea con un comando de la misma API, sin levantar el servidor web:

```
docker compose run --rm api crear-gerencia                # con Docker
dotnet CRMTeamBenavides.Api.dll crear-gerencia            # sin Docker
```

- Pide correo, nombre completo y la contraseña dos veces. La contraseña no se muestra, así que no queda en el historial ni en variables.
- Usa la misma cadena de conexión que la API y deja creados los roles si todavía no estaban.
- Si ya hay un usuario de Gerencia activo, no crea otro y termina con código 1.

### Si Gerencia olvida su contraseña

No hay recuperación por correo, y solo Gerencia restablece contraseñas. Si la única cuenta de Gerencia pierde la suya:

```
docker compose run --rm api crear-gerencia --restablecer
```

Cambia la contraseña de un usuario de Gerencia activo, le quita el bloqueo por intentos fallidos y cierra sus sesiones abiertas. Al resto de usuarios Gerencia les restablece la contraseña desde Usuarios, y eso también les quita el bloqueo.

## Límites del inicio de sesión

| Límite | Valor | Qué responde la API |
|---|---|---|
| Por cuenta | 5 contraseñas fallidas bloquean la cuenta 15 minutos | 401 con el mismo mensaje que una contraseña incorrecta, para no revelar qué cuentas existen |
| Por IP | 10 inicios de sesión por minuto | 429 con el motivo y `Retry-After` |

Los valores están en `Configuration/SeguridadLogin.cs`. Todo el taller sale a internet por la misma IP: si 10 por minuto se queda corto, se sube ahí.

## Configuración de la API

En Docker, `docker-compose.yml` arma estas variables a partir del `.env`. Sin Docker van igual en variables de entorno, nunca en `appsettings.json`. En las variables, `__` reemplaza a `:`.

| Variable | Para qué |
|---|---|
| `ConnectionStrings__DefaultConnection` | La base de datos |
| `JwtSettings__SecretKey` | Firma de los tokens: al menos 32 caracteres al azar |
| `Proxy__RedesConfiables__0` | La red desde la que Caddy reenvía las peticiones, en notación CIDR. Sin esto la API no ve la IP real ni el HTTPS, y el límite por IP bloquearía a todos a la vez. De otras redes no se aceptan los encabezados `X-Forwarded-*`. |
| `DataProtection__RutaLlaves` | Carpeta, en un volumen, donde se guardan las llaves de Data Protection para que sigan después de reiniciar. Al arrancar, la API avisa que se guardan sin cifrar; en un volumen del servidor es aceptable. |
| `Almacenamiento__RutaBase` | Carpeta de las fotos de las órdenes, también en un volumen |
| `Notificaciones__*` | Avisos al teléfono por Firebase: ver [notificaciones.md](notificaciones.md) |
| `YamahaApi__Enabled` | Queda en `false` hasta tener las credenciales y la autorización para enviar a Yamaha |

## Pendiente

- **Copia de los respaldos fuera del servidor**, por ejemplo a Cloudflare R2.
- **Ambiente de pruebas en el mismo servidor:** otra API y otra base con otro nombre de proyecto, y un segundo dominio en el mismo Caddy.
