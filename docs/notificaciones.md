# Notificaciones

Avisos a cada usuario: una bandeja en la web y en la app (campanita) y, en la app, notificaciones al teléfono por Firebase.

> **Cambio de alcance.** Las notificaciones y la bandeja de aprobaciones de Gerencia en la app no estaban en el alcance aprobado. Se construyeron el 11/10 a pedido del equipo; falta registrarlas con el cliente.

## Qué se notifica

| Evento | Quién la recibe | Pantalla al tocarla |
|---|---|---|
| Nueva solicitud de aprobación | Quien tiene `ordenes.aprobar_gerencia`, menos quien la pidió | Bandeja de aprobaciones |
| Presupuesto enviado | Cliente | Su orden |
| Orden pasa a «Lista» | Cliente | Su orden |
| Pedido a Lima pasa a «Recibido» | Cliente | Su inicio |
| Cita confirmada | Cliente | Su inicio (personal: agenda) |
| Se asigna o reasigna una orden | Técnico | La orden |

- Al cliente solo le llega si tiene cuenta en la app. La activación con DNI y código está planificada para el 27 y 28/10.
- Los textos no llevan montos ni DNI, porque se ven en la pantalla bloqueada.
- Varias solicitudes de la misma orden o pedido se juntan en un solo aviso mientras siguen en la cola, por ejemplo «3 solicitudes en OS-000123».

## Cómo funciona

1. **Detección.** `NotificacionesInterceptor` revisa cada guardado de EF y agrega la notificación en la misma transacción. Si la operación falla no se avisa nada; si un guardado falla y el servicio reintenta, los avisos de ese intento se descartan.
2. **Presupuesto.** No se detecta solo, porque el técnico agrega los ítems de a uno. Recepción lo envía con «Enviar presupuesto al cliente» (`POST /api/ordenes-servicio/{id}/enviar-presupuesto`). Las condiciones son: la orden está en diagnóstico, tiene total, el presupuesto no está aprobado y no hay precios esperando a Gerencia.
3. **Envío.** `EnvioNotificacionesService`, un proceso dentro de la API, toma la cola cada 10 segundos.
   - Reserva un lote con `FOR UPDATE SKIP LOCKED` y envía fuera de la transacción.
   - Reintenta con espera creciente y da el aviso por fallido tras 5 intentos.
   - Deja de usar los teléfonos cuyo token ya no sirve.
   - Lo agrupable espera 60 segundos sin cambios, con un máximo de 5 minutos.
4. **Bandeja.** `GET /api/notificaciones`, `/conteo`, `PUT /{id}/leida` y `PUT /leidas`. Cada usuario ve solo lo suyo. La web pregunta el contador cada minuto; la app también, mientras está abierta.
5. **Teléfonos.** `POST /api/notificaciones/dispositivos` al iniciar sesión y `POST /api/notificaciones/dispositivos/baja` al cerrarla. Si otra persona entra en ese teléfono, el token pasa a ella.

## Configuración

Sección `Notificaciones` de `appsettings.json`; las credenciales nunca van en el repositorio.

| Clave | Valor |
|---|---|
| `Proveedor` | `Registro` (desarrollo: solo se anota en el log) o `Firebase` |
| `Firebase:CredencialesRuta` | Ruta al JSON de la cuenta de servicio, en `dotnet user-secrets` |
| `Firebase:CredencialesJson` | El JSON completo, en una variable de entorno del servidor |

Si se pide Firebase y faltan las credenciales, la API sigue en modo registro y lo avisa en el log.

`FirebaseAdmin` 3.7 marca el token de registro como obsoleto en favor del FID (identificador de instalación). Se sigue usando el token porque es lo que entrega `firebase_messaging` en Flutter, que no lo marca como obsoleto.

## En la app

`lib/avisos_telefono.dart`:

- `main` inicia Firebase antes de abrir la app. Si no puede (falta `google-services.json`, pruebas o escritorio), la app sigue igual, solo con la campanita.
- Al entrar pide el permiso de notificaciones (Android 13+ e iOS) y registra el token. Si la persona no da el permiso, no se registra. Si Firebase renueva el token, se registra de nuevo.
- Al cerrar sesión, `ApiHttp.cerrarSesion` da de baja el token antes del logout.
- Con la app abierta, la notificación se muestra igual (en Android con `flutter_local_notifications`) y la campanita se refresca.
- Al tocarla se marca como leída y se abre su pantalla. Si abrió la app cerrada, espera a que la sesión cargue.
- El canal de Android se llama `avisos`, como pide la API, con el ícono `ic_notificacion` (una llave, porque Android pinta el ícono de un solo color) y el acento del tema.

## Configurar Firebase en tu máquina

Los archivos de Firebase no se suben al repositorio (están en `.gitignore`). Sin ellos todo compila y funciona, sin avisos al teléfono.

1. En la consola de Firebase, abrir el proyecto y agregar la app Android `pe.natharce.crm_team_benavides`.
2. Descargar `google-services.json` y copiarlo en `src/movil/android/app/`. Gradle aplica el plugin de Google Services solo si el archivo existe.
3. En «Configuración del proyecto → Cuentas de servicio», generar una clave privada y guardarla fuera del repositorio.
4. En la API:

   ```
   dotnet user-secrets set "Notificaciones:Proveedor" "Firebase"
   dotnet user-secrets set "Notificaciones:Firebase:CredencialesRuta" "C:/secretos/firebase/cuenta-servicio.json"
   ```

5. Probar en un teléfono real o en un emulador con imagen **Google APIs**: la imagen AOSP («default») no trae Google Play Services y no recibe notificaciones.

## Pendiente

- Proyecto de Firebase: hoy se usa uno de prueba de NATHARCE para Android; para producción, el de la empresa.
- iPhone: necesita la cuenta de Apple (para la clave de notificaciones), `GoogleService-Info.plist` y una Mac.
