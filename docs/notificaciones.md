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

**Por revisar al conectar Firebase:** `FirebaseAdmin` 3.7 marca el token de registro como obsoleto en favor del FID (identificador de instalación). Hoy se usa el token, que es lo que entrega el plugin de Flutter.

## Pendiente

- Proyecto de Firebase: uno de prueba de NATHARCE para Android y, para producción, el de la empresa.
- En la app: permisos, registro del teléfono y abrir la pantalla al tocar la notificación del sistema.
- iPhone: necesita la cuenta de Apple (para la clave de notificaciones) y una Mac.
