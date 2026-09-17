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

**Go Live fijo: viernes 02/10/2026, con todo el alcance.** Nada se mueve a después del Go Live. Los sábados 19/09 y 26/09 son días de trabajo. El detalle día por día está en la guía del equipo (`guia.pdf`).

| Semana | Back-end | Front-end |
|---|---|---|
| 14–19/09 | Modelo de datos, autenticación, API de clientes, unidades, usuarios, roles, configuración y órdenes de servicio | Wireframes, login web, clientes y unidades en web y Flutter, flujo de órdenes en web |
| 21–26/09 | Inventario, adaptador Yamaha con mock, ventas y cotizaciones, comprobantes, reportes, chatbot y script de migración | Órdenes en móvil, inventario, ventas, dashboard en web y móvil, widget de chatbot |
| 28/09–02/10 | Migración real, importación y backup, correcciones de UAT, despliegue | Validación de datos migrados, pruebas de punta a punta, correcciones de UAT, soporte al arranque |

UAT: 30/09 y 01/10. Capacitación: 01/10.

## Alcance

La referencia es el **alcance funcional y técnico aprobado**, no el Gantt del cliente, que parece una plantilla de CRM SaaS.

Módulos aprobados: análisis y diseño funcional, UI/UX, backend/API, base de datos, clientes (CRM), unidades, órdenes de servicio, cotizaciones y ventas de repuestos y servicios, inventario, comprobantes, usuarios y roles, reportes y dashboard, configuración, chatbot inicial, integración Yamaha, web administrativa, app Android/iOS, QA, despliegue, capacitación y documentación. La migración de datos es una fase del cronograma.

### Integraciones

- **Yamaha:** adaptador con modo mock y modo real, elegido por configuración, con tareas en segundo plano en Hangfire. El Ingeniero ya gestiona credenciales y documentación con el cliente; mientras tanto se trabaja con el mock.
- **Chatbot:** atención inicial, preguntas frecuentes y derivación a un asesor. Va como widget en la web y la app, e integrado con WhatsApp. Los costos de Meta, del proveedor de mensajería o de IA son del cliente; proveedor y cuenta sin definir.
- **Comprobantes:** solo se registra la información (tipo, serie, número, cliente, detalle, IGV 18 %, total y estado). No hay emisión electrónica ante SUNAT.
- **ERP:** no es una integración. En el tablero de Trello, «ERP» nombra los módulos internos. Falta confirmar qué quiere decir en el Gantt.
- **Web:** es un canal, no una integración.

### Fuera de alcance

No se construye sin aprobación del cliente: emisión electrónica ante SUNAT, chatbot con IA generativa, campañas de marketing, agenda de citas, venta de unidades nuevas, pagos en línea, compras a proveedores, integración con un ERP externo e integración de correo.

Una funcionalidad que no esté en el alcance se marca «por confirmar» y se registra como cambio de alcance antes de construirla.

## Funcionalidad

El detalle está en el mapa funcional: https://claude.ai/code/artifact/708d0351-bbde-48cd-9768-7d63fc62c3cc. Incluye roles, flujos F1–F8, módulos M01–M11 y las pantallas del mockup.

- **Flujo central, la orden de servicio (propuesta, pendiente de validar con el cliente):** `Recepción → Diagnóstico → Esperando aprobación → En reparación ⇄ Esperando repuesto → Control de calidad → Lista para entrega → Entregada`. Puede pasar a `Anulada` desde Recepción o Diagnóstico, con motivo.
- **Roles propuestos (por confirmar):** Administrador, Gerencia, Asesor de servicio, Técnico, Almacén, Vendedor/caja y Cliente.
- **App:** una sola app con perfil Técnico y perfil Cliente (por confirmar).

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
  - `src/web`: la web administrativa en React + TypeScript + Vite + Ant Design. Por ahora son wireframes con datos de ejemplo; ver `src/web/README.md`.
  - `src/movil`: la app en Flutter (Riverpod, Dio y go_router) con login, navegación, clientes y unidades contra la misma API; ver `src/movil/README.md`.
- Convenciones del backend: dominio en español, identificadores `Guid`, `BaseEntity` con campos de auditoría y borrado lógico (`Activo`).
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

El Ingeniero la solicitó el 15/09:

- Servicios, mantenimiento, inventario y una función adicional: ejemplos reales de operaciones y los datos necesarios para crear las tablas.
- Matriz de roles y permisos.
- Flujo exacto de estados de las órdenes.
- Logotipo en PNG, tipografía y colores.

## Decisiones pendientes

- Hosting y proveedor.
- Detalles de la orden: control de calidad, aprobación por ítem y cobro del diagnóstico.
- Acceso del cliente a la app: invitación del taller o autorregistro.
- Proveedor y cuenta de WhatsApp, y si habrá avisos salientes.
- Operaciones reales de la API de Yamaha.
- Almacenes, sucursales y medios de pago.
- Qué significa «ERP» en el Gantt.

## Recursos

- Documento técnico: `Team_Benavides_Resumen Técnico.pdf` (no está en el repositorio).
- Guía día a día del equipo: `guia.pdf` (no está en el repositorio).
- Mapa funcional: https://claude.ai/code/artifact/708d0351-bbde-48cd-9768-7d63fc62c3cc
- Tablero de Trello (privado): https://trello.com/b/7arP1dsL. Para leerlo, usar la exportación JSON; el conector de Trello está autorizado en otro espacio de trabajo.
- Prompt del mockup para Claude Design: [docs/prompt-claude-design.md](docs/prompt-claude-design.md)
