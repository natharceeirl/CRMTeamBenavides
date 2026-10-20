# Despliegue

Lo que la API necesita para correr en un servidor. Por ahora cubre la base nueva, el primer usuario de Gerencia, los límites del login y la configuración de producción. Los archivos de Docker, Caddy y las copias diarias se agregan en el paso siguiente.

## Base nueva, paso a paso

1. Crear la base vacía en PostgreSQL.
2. Generar el script de migraciones, revisarlo y aplicarlo. La API no migra sola al arrancar.

   ```
   dotnet ef migrations script --idempotent --project src/CRMTeamBenavides.Api --output migraciones.sql
   psql -v ON_ERROR_STOP=1 -f migraciones.sql <base>
   ```

   El script se puede volver a correr: solo aplica las migraciones que faltan. Se probó el 19/10 en una base vacía: aplicó las 21 migraciones y la segunda corrida no cambió nada.
3. Crear el primer usuario de Gerencia (sección siguiente).
4. Levantar la API con la configuración de producción.

## Primer usuario de Gerencia

En producción la base nace sin usuarios: el usuario de desarrollo (`DevelopmentUser`) solo se crea en el entorno Development. El primero se crea con un comando de la misma API, sin levantar el servidor web:

```
dotnet CRMTeamBenavides.Api.dll crear-gerencia
```

- Pide correo, nombre completo y la contraseña dos veces. La contraseña no se muestra, así que no queda en el historial ni en variables.
- Usa la misma cadena de conexión que la API y deja creados los roles si todavía no estaban.
- Si ya hay un usuario de Gerencia activo, no crea otro y termina con código 1.

### Si Gerencia olvida su contraseña

No hay recuperación por correo, y solo Gerencia restablece contraseñas. Si la única cuenta de Gerencia pierde la suya:

```
dotnet CRMTeamBenavides.Api.dll crear-gerencia --restablecer
```

Cambia la contraseña de un usuario de Gerencia activo, le quita el bloqueo por intentos fallidos y cierra sus sesiones abiertas. Al resto de usuarios Gerencia les restablece la contraseña desde Usuarios, y eso también les quita el bloqueo.

## Límites del inicio de sesión

| Límite | Valor | Qué responde la API |
|---|---|---|
| Por cuenta | 5 contraseñas fallidas bloquean la cuenta 15 minutos | 401 con el mismo mensaje que una contraseña incorrecta, para no revelar qué cuentas existen |
| Por IP | 10 inicios de sesión por minuto | 429 con el motivo y `Retry-After` |

Los valores están en `Configuration/SeguridadLogin.cs`. Todo el taller sale a internet por la misma IP: si 10 por minuto se queda corto, se sube ahí.

## Configuración de producción

Va en variables de entorno, nunca en `appsettings.json`. En las variables, `__` reemplaza a `:`.

| Variable | Para qué |
|---|---|
| `ConnectionStrings__DefaultConnection` | La base de datos |
| `JwtSettings__SecretKey` | Firma de los tokens: al menos 32 caracteres al azar |
| `Proxy__RedesConfiables__0` | La red desde la que Caddy reenvía las peticiones, en notación CIDR (por ejemplo, la red de Docker). Sin esto la API no ve la IP real ni el HTTPS, y el límite por IP bloquearía a todos a la vez. De otras redes no se aceptan los encabezados `X-Forwarded-*`. |
| `DataProtection__RutaLlaves` | Carpeta, en un volumen, donde se guardan las llaves de Data Protection para que sigan después de reiniciar. Al arrancar, la API avisa que se guardan sin cifrar; en un volumen del servidor es aceptable. |
| `Notificaciones__*` | Avisos al teléfono por Firebase: ver [notificaciones.md](notificaciones.md) |
| `YamahaApi__Enabled` | Queda en `false` hasta tener las credenciales y la autorización para enviar a Yamaha |
