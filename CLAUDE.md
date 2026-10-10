# CRM-MECANICA · Plataforma Team Benavides

Contexto del proyecto para quien trabaje en este repositorio, sea una persona o Claude Code. Responder y documentar en español.

## Qué es

Sistema de gestión a medida (no SaaS) para el taller de post-venta y la tienda de repuestos de **Team Benavides S.R.L.**, concesionario autorizado Yamaha en Arequipa, Perú. Lo desarrolla **NATHARCE EIRL**.

- Canales: web administrativa y app Android/iOS, todos sobre un backend/API central.
- El taller atiende cuatro líneas: motocicletas, cuatrimotos, línea náutica (motos acuáticas) y línea de fuerza (generadores). Por eso una unidad puede no tener placa y su medidor puede estar en km **o en horas**.

## Equipo

| Persona | Rol |
|---|---|
| Líder, «el Ingeniero» | Coordinación con el cliente, arquitectura y QA; gestiona credenciales y documentación de Yamaha |
| Santiago Callocondo Garay | Front-end: web y app móvil |
| Paolo Sebastián Ramírez Melendres | Back-end |

- Reunión semanal con el cliente: lunes de 12:00 a 13:00.
- Cierre diario de 10 minutos entre front y back: qué quedó listo, qué quedó bloqueado y qué necesita el otro al día siguiente.

## Cronograma

**El Go Live se aplazó (27/09).** Todo el alcance, incluidos los cambios de la reunión del 25/09, se entrega junto antes del Go Live: no se divide en fases ni se deja nada para después. Si el trabajo no entra, se mueve la fecha, no el alcance.

**Fecha propuesta: viernes 20/11/2026**, pendiente de confirmar con el cliente. El plan día por día está en [docs/plan-implementacion.md](docs/plan-implementacion.md).

| Semana | Back-end | Front-end |
|---|---|---|
| 1 · 28/09–02/10 | Permisos y reglas por rol, unidades, clientes, costo y umbrales, OS base | Permisos en web y app, unidades, clientes, timeline de la OS |
| 2 · 05–09/10 | OS completa: servicios, IGV, correlativo, búsqueda, formato de atención, avances y aprobación de Gerencia | Servicios y OS en la web; app del técnico |
| 3 · 12–16/10 | Configuración, tipo de cambio, ventas con servicios y pagos, OS con su venta, comprobantes, importador de Excel | OS imprimible, configuración, ventas y comprobantes |
| 4 · 19–23/10 | Caja, adelantos, pedidos de Lima | Caja, adelantos, pedidos de Lima |
| 5 · 26–30/10 | Auditoría, portal del cliente, fotos | Auditoría, perfil Cliente en la app, fotos |
| 6 · 02–06/11 | Ganancias, reportes por rol, citas | Ganancias, reportes, agenda |
| 7 · 09–13/11 | Almacenes y margen | Citas en la app, almacenes, pruebas de punta a punta |
| 8 · 16–20/11 | UAT 16–17/11, capacitación 18/11, Go Live 20/11 | UAT, capacitación y salida |

- Cada lunes a las 12:00, en la reunión con el cliente, se muestra en el ambiente de pruebas lo terminado la semana anterior y se recogen las respuestas que necesita la semana siguiente.
- El jueves 08/10 es feriado. Los sábados son margen, no plan.
- Antes del aplazamiento el Go Live era el 02/10/2026, según la guía `guia.pdf`. Lo construido hasta el 25/09 está en `develop` (`23af577`).

## Alcance

La referencia es el **alcance funcional y técnico aprobado**, no el Gantt del cliente, que parece una plantilla de CRM SaaS.

Módulos aprobados: análisis y diseño funcional, UI/UX, backend/API, base de datos, clientes (CRM), unidades, órdenes de servicio, cotizaciones y ventas de repuestos y servicios, inventario, comprobantes, usuarios y roles, reportes y dashboard, configuración, chatbot inicial, integración Yamaha, web administrativa, app Android/iOS, QA, despliegue, capacitación y documentación. La migración de datos es una fase del cronograma.

### Cambios de la reunión del 25/09

El detalle por módulo está en [docs/resumen-cambios-reunion.md](docs/resumen-cambios-reunion.md). Todo entra antes del Go Live.

- **Se agrega:** portal del cliente, permisos reales en el backend, aprobaciones de Gerencia, historial de la OS, servicios y mano de obra con IGV, nuevo modelo de unidades, stock configurable, métodos de pago, adelantos y saldo, pedidos de Lima, caja, ganancias, citas y agenda, tipo de cambio manual, configuración ampliada, auditoría y el formato de atención del cliente en la OS.
- **Se cambia:** modelo de unidades, flujo de la OS, permisos por rol, estructura de servicios, ventas y comprobantes, alertas de stock y la relación OS–venta–comprobante, sin doble descuento de stock.
- **Se mantiene:** los estados actuales de la OS y las pantallas del portal del cliente como referencia visual.

### Integraciones

- **Yamaha:** adaptador con modo mock y modo real, elegido por configuración, con tareas en segundo plano en Hangfire. Hoy solo existe el cliente HTTP con su configuración. El modo mock o real y el registro de llamadas se agregan en la semana 7; las operaciones, cuando el Ingeniero consiga la documentación y las credenciales. Hangfire se instala cuando haya trabajos reales.
- **Chatbot:** atención inicial, preguntas frecuentes y derivación a un asesor. Va como widget en la web y la app, e integrado con WhatsApp. Los costos de Meta, del proveedor de mensajería o de IA son del cliente; proveedor y cuenta sin definir.
- **Comprobantes:** solo se registra la información: tipo, serie, número, cliente, OS y venta, repuestos y servicios, IGV 18 %, método de pago, total, observaciones y estado. No hay emisión electrónica; el alcance de SUNAT, OSE o PSE se valida después.
- **Excel:** importación de clientes, unidades y repuestos con costo y stock, con errores por fila.
- **ERP:** no es una integración. En el tablero de Trello, «ERP» nombra los módulos internos. Falta confirmar qué quiere decir en el Gantt.
- **Web:** es un canal, no una integración.

### Fuera de alcance

No se construye sin aprobación del cliente: emisión electrónica ante SUNAT, chatbot con IA generativa, campañas de marketing, venta de unidades nuevas, pagos en línea, integración con un ERP externo e integración de correo. La agenda de citas estaba en esta lista y entró con los cambios del 25/09. Las compras a proveedores también estaban: se construyeron el 10/10 con autorización de Paolo y falta registrarlas con el cliente como cambio de alcance; las reglas están en [docs/modulo-compras.md](docs/modulo-compras.md).

Una funcionalidad que no esté en el alcance se marca «por confirmar» y se registra como cambio de alcance antes de construirla.

## Funcionalidad

El mapa funcional (https://claude.ai/code/artifact/708d0351-bbde-48cd-9768-7d63fc62c3cc) sigue sirviendo para módulos y pantallas. En estados, roles y acceso del cliente manda el resumen de cambios del 25/09.

- **Orden de servicio:** se mantienen los estados actuales: `Abierta → Diagnóstico → Aprobada → En proceso → Lista → Entregada`. Puede pasar a `Cancelada` desde cualquier estado anterior a `Entregada`, y de `Lista` vuelve a `En proceso` si hay un reingreso. Se agregan historial de estados con usuario y hora, fecha estimada de entrega, aprobación de Gerencia y los campos del formato de atención del cliente.
- **Roles definitivos:**
  - Gerencia/Admin: acceso total, administra usuarios y permisos, y aprueba los cambios importantes de la OS.
  - Recepción: clientes, unidades y OS, según permisos.
  - Técnico: solo sus OS; registra diagnóstico, servicios, repuestos, mano de obra y avances. No cambia precios ni da la aprobación final.
  - Vendedor: ventas y comprobantes de sus ventas. No cambia precios ni aplica descuentos.
  - Cliente: solo sus unidades, OS, documentos y comprobantes; aprueba o rechaza presupuestos.
- Los permisos se aplican en el backend con policies; ocultar botones no basta.
- **App:** una sola app con perfil Técnico y perfil Cliente. El cliente activa su cuenta con su DNI y un código que genera el personal autorizado. Referencia visual del perfil Cliente: [docs/referencias/app-cliente.html](docs/referencias/app-cliente.html).

## Stack

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core 10 (LTS) + C# |
| Datos | PostgreSQL 17/18 + EF Core 10 (Npgsql) |
| Autenticación | ASP.NET Core Identity + JWT + refresh tokens + autorización por policies |
| Documentación de API | OpenAPI integrado de .NET + cliente TypeScript generado |
| Integraciones | Adaptadores e interfaces + Hangfire |
| Web | React + TypeScript + Vite, TanStack Query, React Hook Form + Zod, Ant Design |
| Móvil | Flutter + Riverpod (o Bloc) + Dio + go_router |
| Arquitectura | Una sola API, modular por área, sin microservicios |
| IDE | VS 2026, Rider o VS Code + C# Dev Kit |
| Hosting | Sin definir |

Principios del documento técnico que no se negocian:

- Las reglas de negocio y los permisos viven en el backend; la web y la app no los duplican.
- Validación en el servidor y transacciones en ventas y movimientos de inventario.
- Estados y catálogos controlados, nunca texto libre.
- Auditoría con usuario, fecha, acción y entidad afectada.
- Secretos en variables de entorno o `dotnet user-secrets`; nunca en el repositorio.
- Una caída de Yamaha o de WhatsApp no bloquea el sistema.

## Repositorio

- GitHub: https://github.com/natharceeirl/CRMTeamBenavides
- Ramas (desde el 16/09): todo el trabajo va directo sobre `develop`. Front y back hacen sus commits ahí; no se crean ramas `feature/*` ni Pull Requests para el día a día.

  ```
  main       (historia antigua, sin relación con develop)
  └─ develop ← Santiago y Paolo
  ```

- Flujo: `git pull` antes de empezar y antes de subir → commits en `develop` → `git push`.
- Pendiente: `main` (`f4806a0`) y `develop` (raíz `7775b93`) tienen historias sin relación, porque `develop` se rehízo sobre la rama del back. Hay que resolverlo antes del despliegue.
- Estructura:
  - `CRMTeamBenavides.slnx` y `src/CRMTeamBenavides.Api`: la API .NET 10 con entidades de dominio, servicios, endpoints por área en `Features/`, `ApplicationDbContext` y migraciones.
  - `src/web`: la web administrativa en React + TypeScript + Vite + Ant Design, conectada a la API: tablero, órdenes, clientes, unidades, repuestos, ventas y comprobantes, reportes, chatbot y usuarios; ver `src/web/README.md`.
  - `src/movil`: la app en Flutter (Riverpod, Dio y go_router) contra la misma API: tablero, órdenes con diagnóstico, tienda con repuestos y ventas, clientes, unidades y asistente; ver `src/movil/README.md`.
  - `docs/`: resumen de cambios del 25/09, plan de implementación, referencia visual del portal del cliente y prompt del mockup.
- Convenciones del backend: dominio en español, identificadores `Guid`, `BaseEntity` con campos de auditoría y borrado lógico (`Activo`).
- Reglas de base de datos (detalle en el plan):
  - Una migración de EF por bloque, con nombre en español. En producción se aplica con un script idempotente revisado; la API no migra sola al arrancar.
  - Índices únicos filtrados por `Activo`. Hoy `Vehiculos.Placa` y `Productos.Codigo` no lo están y una placa dada de baja no se puede volver a registrar.
  - Montos en `numeric(12,2)` y tipo de cambio en `numeric(10,4)`.
  - Estados y tipos como enum o catálogo, nunca texto libre.
  - Todo cambio de stock usa el bloqueo `FOR UPDATE` de `InventarioService`.
  - Correlativos con secuencia de PostgreSQL o fila bloqueada, nunca `MAX()+1`.
  - `CreadoPorId` y `ModificadoPorId` hoy solo los llena el chatbot; el interceptor de auditoría los completará en todas las tablas.
- Configuración local: la cadena de conexión y `JwtSettings:SecretKey` van en `dotnet user-secrets`, nunca en `appsettings.json`.
- En desarrollo, el documento OpenAPI se sirve en `/openapi/v1.json`.

## Identidad visual

Web, Android e iOS comparten la identidad de Team Benavides.

- **Diseño de referencia:** proyecto de Claude Design https://claude.ai/design/p/dcd21182-9c07-44c0-bba5-5bbdd0966672 (archivo `Team Benavides.dc.html`). Al implementarlo:
  - usar solo la primera variante, la de barra lateral negra;
  - letra un poco más grande que en el diseño;
  - indicadores sin subíndices ni textos secundarios: mostrar solo lo necesario;
  - no implementar las pantallas de recomendaciones ni de alertas;
  - tomar del diseño solo los colores y la tipografía.
- La web aplica los colores y la tipografía de ese diseño: Space Grotesk para títulos, IBM Plex Sans para texto, fondo `#f3f2f2`, texto `#201e1d` y acento `#ec3013`. Los botones usan `#dd2b0f` para que el texto blanco cumpla contraste. Todo vive en `src/web/src/theme/tokens.ts`.
- El Ingeniero subió «Mockups - CRMTeamBenavides.docx» a la carpeta «7. Entregables» para revisión.
- Colores del logo oficial y del sitio teambenavides.com/4.0 (el logo los conserva):

| Uso | Color |
|---|---|
| Rojo de marca: logo, barras, fondos grandes | `#FF0000` |
| Rojo interactivo: botones, enlaces, texto rojo | `#D70000` (contraste 5,4:1 con blanco) |
| Hover y presionado | `#B30000` |
| Negro | `#000000` |
| Gris de fondo (aproximado) | `#EEEEEE` |

- Sobre `#FF0000` el texto blanco solo es legible en tamaño grande o negrita (contraste 4:1).
- Colores y fuentes van en un único archivo de tokens; nunca escritos directamente en los componentes.
- Pendientes: logo en negativo, ícono cuadrado para la app y reglas de uso del logo de Yamaha.

## Información pedida al cliente

Cada respuesta tiene fecha límite porque el módulo que la necesita empieza ese día; la lista completa está en el plan, en «Lo que necesitamos del cliente y para cuándo». Lo más próximo:

- Lunes 28/09: confirmar la fecha del Go Live, hosting, lista de usuarios con su rol e identificadores de motos acuáticas y generadores.
- Jueves 01/10: catálogo de servicios con su precio de mano de obra.
- Lunes 05/10: almacenes y si cada uno lleva stock propio.
- Martes 06/10: campos del formato de atención y qué aprueba Gerencia.
- Jueves 15/10: Excel de migración.

De lo pedido el 15/09, la matriz de roles y el flujo de estados quedaron resueltos en el resumen de cambios. Falta el logo en negativo y el ícono cuadrado de la app.

## Decisiones pendientes

- Fecha del Go Live: propuesta el viernes 20/11/2026.
- Hosting y proveedor.
- Qué acciones aprueba Gerencia.
- Presupuesto: aprobación completa o por ítem, y cobro del diagnóstico si se rechaza.
- Formato de atención: qué campos van estructurados y si la firma es impresa o en pantalla.
- Almacenes: cuántos hay y si cada uno lleva su propio stock. Si lo llevan, el cambio se adelanta a la semana 3.
- Caja: una o por usuario, y si se abre y se cierra cada día.
- Pedidos de Lima: qué son y qué estados tienen.
- Ganancias: si entran servicios y mano de obra, y si se usa costo promedio o último costo.
- Citas: quién las pide y si la agenda es por técnico.
- Métodos de pago, series y correlativos, y para qué se usa el tipo de cambio.
- Cuentas de Play Store y App Store a nombre de la empresa.
- Proveedor y cuenta de WhatsApp, y si habrá avisos salientes.
- Operaciones reales de la API de Yamaha.
- Alcance de SUNAT, OSE o PSE.
- Qué significa «ERP» en el Gantt.

## Recursos

- Plan de implementación: [docs/plan-implementacion.md](docs/plan-implementacion.md). Versión visual: https://claude.ai/artifact/UyERKrdu6H5VUbopk3GTXT. Las páginas de claude.ai son privadas hasta que su dueño las comparte, así que la referencia del equipo es la copia del repositorio.
- Resumen de cambios de la reunión del 25/09: [docs/resumen-cambios-reunion.md](docs/resumen-cambios-reunion.md).
- Reglas del módulo de compras: [docs/modulo-compras.md](docs/modulo-compras.md).
- Referencia visual del portal del cliente: [docs/referencias/app-cliente.html](docs/referencias/app-cliente.html). Versión publicada: https://claude.ai/artifact/JqzMRP5fjiEZXdRDvFmjDL
- Documento técnico: `Team_Benavides_Resumen Técnico.pdf` (no está en el repositorio).
- Guía día a día original, con el Go Live del 02/10: `guia.pdf` (no está en el repositorio). La reemplaza el plan de implementación.
- Mapa funcional: https://claude.ai/code/artifact/708d0351-bbde-48cd-9768-7d63fc62c3cc
- Tablero de Trello (privado): https://trello.com/b/7arP1dsL. Para leerlo, usar la exportación JSON; el conector de Trello está autorizado en otro espacio de trabajo.
- Prompt del mockup para Claude Design: [docs/prompt-claude-design.md](docs/prompt-claude-design.md)
