# Plan de implementación

Todo lo del [resumen de cambios de la reunión del 25/09](resumen-cambios-reunion.md) entra antes del Go Live, que se aplaza. El plan parte de lo que ya funciona en `develop` (`23af577`, 25/09) y dice, día por día, qué entrega Paolo en el back y qué construye Santiago en el front con esa entrega.

- Versión visual: https://claude.ai/artifact/UyERKrdu6H5VUbopk3GTXT. Si el plan cambia, se actualizan las dos.
- Estimaciones en días de trabajo de una persona. Paolo las valida el lunes 28/09 a primera hora.
- Paolo hace todo el back: API, base de datos e integraciones.

## Fecha

**Go Live propuesto: viernes 20/11/2026.** Pendiente de confirmar con el cliente.

El back es la ruta crítica: unos 30 días de trabajo desde el lunes 28/09, con el jueves 08/10 feriado, terminan el martes 10/11. Quedan tres días y medio de margen y después la semana de salida: UAT el 16 y 17/11, capacitación el 18/11 y Go Live el 20/11. Si el cliente fija otra fecha, el plan se ajusta desde el final: primero se consume el margen de la semana 7.

| Back · Paolo | Front · Santiago | Margen | Demos al cliente |
|---|---|---|---|
| ≈ 30 días | ≈ 26 días, web y app | 3,5 días en la semana 7 | 6, una por reunión de los lunes, del 05/10 al 09/11 |

## Cronograma por semana

| Semana | Back · Paolo | Front · Santiago | Ingeniero y cliente |
|---|---|---|---|
| 1 · 28/09 – 02/10 | Permisos, unidades, OS base | Permisos, unidades, timeline | Hosting y usuarios |
| 2 · 05 – 09/10 | OS completa | Servicios y OS | Ambiente de pruebas |
| 3 · 12 – 16/10 | Configuración, ventas, importador | OS imprimible, configuración, ventas | Excel de migración |
| 4 · 19 – 23/10 | Caja, adelantos, pedidos de Lima | Caja, adelantos, pedidos | Cuentas de tiendas |
| 5 · 26 – 30/10 | Auditoría, portal, fotos | Auditoría y app del cliente | Respuestas de la semana y demo del lunes |
| 6 · 02 – 06/11 | Ganancias, reportes, citas | Fotos, ganancias, citas | Respuestas de la semana y demo del lunes |
| 7 · 09 – 13/11 | Almacenes y margen | Citas en la app, almacenes, pruebas | Respuestas de la semana y demo del lunes |
| 8 · 16 – 20/11 | UAT 16–17, capacitación 18, Go Live 20/11 | UAT y salida | UAT y capacitación |

## Día por día

La **Entrega** es la hora a la que Paolo sube a `develop` lo que Santiago usa al día siguiente. Quien termina una entrega avisa en el momento, y el cierre diario de 10 minutos sigue igual. Los sábados no están planificados: son margen si una semana se atrasa.

Cada entrega del back lleva su área: `BD` tablas, migraciones de EF, índices y datos · `API` endpoints, reglas y permisos · `Integración` Excel, archivos y servicios externos.

### Semana 1 · 28/09 – 02/10 · Seguridad y datos base

**Ingeniero:** confirmar la nueva fecha en la reunión del lunes, contratar el hosting y conseguir la lista de usuarios con su rol. **Cliente:** identificadores de motos acuáticas y generadores; catálogo de servicios con precio antes del jueves.

| Día | Back · Paolo | Front · Santiago |
|---|---|---|
| **Lun 28/09** · Reunión 12:00 | `BD` `API` 09:00, 15 minutos con Santiago para cerrar la lista de permisos de `/auth/me` y los DTO de unidad, cliente, contraseña e historial de OS. Roles definitivos, catálogo de permisos y policies en todos los endpoints. **Entrega: 18:00 → permisos y /auth/me** | Menú, botones y precios según permisos, con `/auth/me` simulado, en web y app. Borrar el código sin uso. |
| **Mar 29/09** | `API` Reglas por rol en el servidor: el técnico solo ve sus OS y no fija precios; el vendedor no fija precios ni descuentos; las acciones de Gerencia quedan protegidas. Restablecer y cambiar contraseña. **Entrega: 17:00 → reglas y contraseñas** | Permisos contra la API real, con aviso claro si responde 403. |
| **Mié 30/09** | `BD` `API` Unidad con tipo, placa opcional, VIN o serie, motor y medidor km u horas. Cliente con tipo de documento y documento único. Repuesto con costo y umbral de 4 por defecto. **Entrega: 12:00 → unidades y clientes · 18:00 → costo y umbral** | Restablecer contraseña en Usuarios y cambiar la propia. Desde las 12:00, unidades y clientes en la web. |
| **Jue 01/10** | `BD` `API` OS: historial de estados con usuario y hora, fecha estimada, medidor al ingresar y fecha de salida. Umbral por categoría y ajuste de stock según permiso. **Entrega: 16:00 → OS base · 18:00 → umbrales** | Unidades y clientes en la app. Costo y umbral en Repuestos. |
| **Vie 02/10** | `BD` `API` Detalle de la OS con tipo: repuesto, servicio, mano de obra y terceros. Catálogo de servicios con precio. **Entrega: 18:00 → detalle con tipo** | Línea de tiempo de la OS, fecha estimada y medidor en web y app. Umbral por categoría y ajuste de stock. |

### Semana 2 · 05/10 – 09/10 · Orden de servicio completa

**Ingeniero:** ambiente de pruebas en el hosting y unir `main` con `develop`; primera demo el lunes 05/10. **Cliente:** campos del formato de atención y qué aprueba Gerencia, antes del martes 06/10.

| Día | Back · Paolo | Front · Santiago |
|---|---|---|
| **Lun 05/10** · Reunión 12:00 | `API` IGV y totales automáticos en la OS. **Entrega: 18:00 → IGV y totales** | Detalle con tipo y catálogo de servicios en la OS de la web. |
| **Mar 06/10** | `BD` `API` Correlativo de OS. Búsqueda por OS, placa, serie, modelo, cliente, fecha y medidor. **Entrega: 18:00 → correlativo y búsqueda** | IGV y totales en la OS. En la app, el técnico agrega repuestos y servicios sin tocar precios. |
| **Mié 07/10** | `BD` `API` Campos del formato de atención y avances del técnico. **Entrega: 18:00 → formato y avances** | Búsqueda ampliada y correlativo en web y app. |
| **Jue 08/10** · Feriado | Combate de Angamos. | Combate de Angamos. |
| **Vie 09/10** | `BD` `API` Aprobación de Gerencia en la OS. **Entrega: 18:00 → aprobación de Gerencia** | Formato de atención en la web y avances en la app del técnico. |

### Semana 3 · 12/10 – 16/10 · Configuración, ventas y comprobantes

**Cliente:** datos de la empresa, series y correlativos, métodos de pago y uso del tipo de cambio, antes del lunes 12/10. Excel de migración antes del jueves 15/10.

| Día | Back · Paolo | Front · Santiago |
|---|---|---|
| **Lun 12/10** · Reunión 12:00 | `BD` `API` Configuración: empresa, IGV, series y correlativos, parámetros generales. **Entrega: 18:00 → configuración** | Aprobación de Gerencia en la OS. |
| **Mar 13/10** | `BD` `API` Tipo de cambio diario. Venta con servicios, mano de obra y métodos de pago. **Entrega: 18:00 → tipo de cambio y venta ampliada** | OS imprimible con firmas. |
| **Mié 14/10** | `BD` `API` Entregar la OS genera su venta, sin volver a descontar los repuestos que ya descontó la OS. **Entrega: 18:00 → cierre de OS con venta** | Pantallas de configuración y tipo de cambio. |
| **Jue 15/10** | `BD` `API` Comprobante ligado a OS y venta, con servicios, IGV, método de pago y observaciones. **Entrega: 18:00 → comprobante** | Ventas con servicios y métodos de pago. Cierre de la OS con su venta. |
| **Vie 16/10** | `Integración` `BD` Importador de Excel con errores por fila. Muestra de migración con el Excel del cliente. **Entrega: 18:00 → muestra de migración** | Comprobante ampliado. A las 18:00, revisar la muestra de migración. |

### Semana 4 · 19/10 – 23/10 · Caja, adelantos y pedidos de Lima

**Cliente:** caja (una o por usuario, apertura y cierre diario), adelantos y qué son los pedidos de Lima, antes del lunes 19/10. Si la app se publica en tiendas para el Go Live, las cuentas de Play Store y App Store deben estar abiertas esta semana.

| Día | Back · Paolo | Front · Santiago |
|---|---|---|
| **Lun 19/10** · Reunión 12:00 | `BD` `API` Caja: movimientos, y cada venta confirmada entra sola como ingreso. **Entrega: 18:00 → caja base** | Corregir con Paolo lo que salga de la muestra de migración. |
| **Mar 20/10** | `API` Egresos, gastos con motivo, saldo y cierre de caja si se confirma. **Entrega: 18:00 → egresos y saldo** | Caja: movimientos e ingresos. |
| **Mié 21/10** | `BD` `API` Adelantos y saldo en OS y ventas. **Entrega: 18:00 → adelantos** | Egresos, gastos y saldo de caja. |
| **Jue 22/10** | `BD` `API` Pedidos de Lima. **Entrega: 18:00 → pedidos** | Adelantos y saldo en OS y ventas. |
| **Vie 23/10** | `API` `BD` Estados y tiempo de entrega de los pedidos. Empieza la auditoría automática. **Entrega: 13:00 → estados de pedidos** | Pedidos de Lima con sus estados. |

### Semana 5 · 26/10 – 30/10 · Auditoría, portal del cliente y fotos

**Cliente:** qué ve el cliente en la app y si el historial de placa es de placa o de propietario, antes del lunes 26/10.

| Día | Back · Paolo | Front · Santiago |
|---|---|---|
| **Lun 26/10** · Reunión 12:00 | `BD` `API` Auditoría automática y su consulta. Historial de placa y propietario. **Entrega: 18:00 → auditoría e historial** | Reporte de caja. |
| **Mar 27/10** | `BD` `API` Portal: rol Cliente, vínculo usuario-cliente y activación con DNI y código. **Entrega: 18:00 → activación** | Consulta de auditoría. Historial de placa en la ficha de la unidad. |
| **Mié 28/10** | `API` Endpoints de «mis» unidades, OS, timeline, presupuestos y comprobantes. **Entrega: 18:00 → endpoints del cliente** | Activación en la app y código de activación en la ficha del cliente. |
| **Jue 29/10** | `BD` `API` Aprobar o rechazar el presupuesto. **Entrega: 18:00 → respuesta del cliente** | Perfil Cliente: mis unidades, orden en curso, historial y documentos. |
| **Vie 30/10** | `Integración` `BD` Fotos de la OS. **Entrega: 18:00 → fotos** | Aprobar el presupuesto en la app y ver la respuesta en la OS de la web. |

### Semana 6 · 02/11 – 06/11 · Ganancias, reportes y citas

**Cliente:** si servicios y mano de obra entran en las ganancias y con qué costo; quién pide las citas y si la agenda es por técnico; cuántos almacenes hay. Todo antes del lunes 02/11.

| Día | Back · Paolo | Front · Santiago |
|---|---|---|
| **Lun 02/11** · Reunión 12:00 | `API` Costo guardado en cada venta. Empieza el reporte de ganancias. | Fotos con la cámara de la app y vista en la web. |
| **Mar 03/11** | `API` Ganancias por venta, repuesto y período. Reportes filtrados por rol; caja y ganancias en el tablero. **Entrega: 18:00 → ganancias y reportes** | Ayuda y chatbot en el perfil Cliente; ajustes del portal. |
| **Mié 04/11** | `BD` `API` Citas: solicitud con fecha y rango, cliente, unidad y motivo. **Entrega: 18:00 → citas** | Reporte de ganancias. Reportes por rol y tablero. |
| **Jue 05/11** | `BD` `API` Agenda del personal, bloqueo de horarios y aprobación de citas. **Entrega: 18:00 → agenda** | Solicitudes de cita en la web. |
| **Vie 06/11** | `API` Pedir cita desde el portal del cliente. **Entrega: 18:00 → citas del cliente** | Agenda del personal, bloqueos y aprobación en la web. |

### Semana 7 · 09/11 – 13/11 · Almacenes, pruebas integrales y margen

Desde el miércoles es margen: si una semana anterior se atrasó, se recupera aquí. Si no, se usa para pruebas por rol y el ensayo completo de migración.

| Día | Back · Paolo | Front · Santiago |
|---|---|---|
| **Lun 09/11** · Reunión 12:00 | `BD` `API` Almacenes y destino del repuesto, si se confirman. **Entrega: 18:00 → almacenes** | Pedir cita desde la app del cliente. |
| **Mar 10/11** | `BD` Cerrar almacenes. Correcciones. | Almacenes en Repuestos y en la OS. |
| **Mié 11/11** | `BD` `Integración` Margen. Pruebas integrales por rol y ensayo completo de la migración en el ambiente de pruebas. | Margen. Pruebas de punta a punta por rol en web y app. |
| **Jue 12/11** | `API` Margen y correcciones. | APK firmado y envío a las tiendas si hay cuentas. Guía de capacitación. |
| **Vie 13/11** | `BD` Congelar `develop` para el UAT. Backup. | Congelar. Ambiente de pruebas listo para el UAT. |

### Semana 8 · 16/11 – 20/11 · UAT, capacitación y Go Live

Lo que falle en datos, permisos o integraciones lo corrige Paolo; lo que falle en pantallas o flujo, Santiago. El Ingeniero decide qué se corrige antes del Go Live.

| Día | Back · Paolo | Front · Santiago |
|---|---|---|
| **Lun 16/11** · UAT día 1 | `BD` Carga de datos reales en el ambiente de pruebas. Correcciones. | Acompañar el UAT por rol. Correcciones. |
| **Mar 17/11** · UAT día 2 | `API` Correcciones. | Correcciones. |
| **Mié 18/11** · Capacitación | `API` Correcciones finales. | Apoyo en la capacitación. Correcciones finales. |
| **Jue 19/11** | `BD` 18:00, congelar. Backup y carga final preparada. | Versión final de la web y del APK. |
| **Vie 20/11** · Go Live | `BD` `API` Desplegar la API y cargar los datos reales. Soporte. | Publicar la web e instalar la app en los equipos del taller. Soporte. |

## Back: base de datos e integraciones

Lo hace Paolo junto con la API. Hoy hay cuatro migraciones de EF; la última es `AgregarModuloChatbotInicial`, del 18/09.

### Reglas para todas las migraciones

1. Una migración de EF por bloque, con nombre en español como las actuales. En producción se aplica con un script revisado (`dotnet ef migrations script --idempotent`). La API no migra sola al arrancar y así debe seguir.
2. Los índices únicos se filtran por `Activo`, porque los registros se dan de baja sin borrarse. Hoy `Vehiculos.Placa` y `Productos.Codigo` son únicos sin filtro: una placa dada de baja no se puede volver a registrar. Se corrige en la primera migración.
3. Montos con precisión fija: `numeric(12,2)` para dinero y `numeric(10,4)` para el tipo de cambio. Hoy los `decimal` no tienen precisión definida.
4. Estados y tipos como enum o tabla de catálogo, nunca texto libre. `Comprobante.Tipo` y `Comprobante.Estado` hoy son texto y pasan a catálogo en la semana 3.
5. Todo cambio de stock usa el bloqueo `FOR UPDATE` que ya tiene `InventarioService`, incluidos el cierre de OS con venta y los almacenes.
6. Los correlativos de OS, comprobantes y pedidos salen de una secuencia de PostgreSQL o de una fila bloqueada, nunca de `MAX()+1`.
7. `CreadoPorId` y `ModificadoPorId` existen en todas las tablas, pero hoy solo el chatbot los llena. El interceptor de auditoría de la semana 5 los completa en todas.
8. Toda columna obligatoria nueva trae valor por defecto o relleno en la misma migración, para no romper los datos existentes.

### Migraciones por semana

#### Semana 1 · Seguridad y datos base

| Día | Migración | Qué cambia | Índices y reglas | Datos existentes |
|---|---|---|---|---|
| **Lun 28/09** | Semilla, sin migración | Los cinco roles, catálogo de permisos con códigos `modulo.accion` y la matriz rol-permiso. | Semilla idempotente, como `RolSeeder`. | Admin pasa a Gerencia/Admin; Recepción se conserva. |
| **Mié 30/09** | AmpliarUnidadesYClientes | `Vehiculos`: tipo de unidad, VIN o serie, número de motor, tipo de medidor y lectura, que reemplaza a `Kilometraje`; placa opcional. `Clientes`: tipo de documento. | Únicos filtrados por `Activo`: placa cuando existe, VIN o serie, y tipo más número de documento. Se corrige también `Productos.Codigo`. | El kilometraje pasa a la lectura con medidor en km. Las unidades actuales quedan como motocicleta. El tipo de documento se deduce por largo: 8 dígitos DNI, 11 RUC. |
| **Mié 30/09** | AgregarCostoYUmbrales | `Productos`: costo; el stock mínimo pasa a opcional. `CategoriasProducto`: stock mínimo. `MovimientosInventario`: costo unitario de cada entrada. | Umbral efectivo: el del repuesto; si no hay, el de la categoría; si no, 4. | Costo en 0 hasta la migración real. |
| **Jue 01/10** | AgregarHistorialDeOrden | Tabla `HistorialEstadosOrden`: orden, estado anterior y nuevo, usuario, fecha y observación. `OrdenesServicio`: fecha estimada de entrega y lectura del medidor al ingresar. `FechaCierre` queda como fecha de salida. | Índice por orden y fecha. | Una fila por orden con su estado actual. |
| **Vie 02/10** | TiparDetalleYServicios | Tabla `Servicios`: nombre, tipo, precio, afecto a IGV y código opcional. `DetallesServicio`: tipo, servicio, costo unitario, y subtotal, IGV y total guardados. | Hoy el subtotal se calcula y no se guarda. Se guarda para que comprobantes y ganancias no cambien si después cambia el precio. | Línea con repuesto: tipo repuesto. Sin repuesto: mano de obra. |

#### Semana 2 · Orden de servicio completa

| Día | Migración | Qué cambia | Índices y reglas | Datos existentes |
|---|---|---|---|---|
| **Mar 06/10** | NumerarOrdenes | `OrdenesServicio`: número con secuencia. | Número único. Extensión `pg_trgm` e índices GIN para buscar por nombre, placa, serie y modelo con texto parcial. | Se numeran por fecha de apertura. |
| **Mié 07/10** | AgregarFormatoDeAtencion | `OrdenesServicio`: tipo de servicio, falla, solución y recomendaciones. Tabla `AvancesOrden`: técnico, fecha y descripción. | Los campos se ajustan a lo que el cliente pida estructurar. | Vacíos. |
| **Vie 09/10** | AgregarAprobaciones | Tabla `AprobacionesOrden`: qué se aprueba, estado, quién la pide, quién resuelve, fechas y comentario. | La estructura final depende de qué acciones aprueba Gerencia. | — |

#### Semana 3 · Configuración, ventas y comprobantes

| Día | Migración | Qué cambia | Índices y reglas | Datos existentes |
|---|---|---|---|---|
| **Lun 12/10** | AgregarConfiguracion | Tablas `Empresa` (una fila), `Parametros` (IGV, umbral de stock y otros) y `SeriesComprobante` (tipo, serie y último número). | Serie única por tipo. El siguiente número se toma con la fila bloqueada. | IGV 0,18 y umbral 4. |
| **Mar 13/10** | AgregarPagosYTipoDeCambio | Tablas `MetodosPago`, `Pagos` (monto, método, fecha y usuario, ligado a venta u OS) y `TiposCambio` (fecha, valor y usuario). `DetallesVenta`: tipo, servicio, descripción, costo, subtotal, IGV y total; el repuesto pasa a opcional. | Un tipo de cambio por fecha. Precisión fija en todos los montos. | Las ventas actuales son de prueba y no se migran. |
| **Mié 14/10** | LigarVentaYOrden | `DetallesVenta`: línea de la OS de la que viene. | Una sola venta vigente por OS, con índice único filtrado. La línea que viene de la OS no genera otra salida de stock. | — |
| **Jue 15/10** | AmpliarComprobantes | `Comprobantes`: OS, serie, subtotal, IGV, total, método de pago y observaciones. Tipo y estado pasan a catálogo. | Único por tipo, serie y número. | «Boleta» y «Factura» pasan al catálogo; «Emitido» y «Anulado», al enum. |
| **Vie 16/10** | AgregarImportaciones | Tabla `ImportacionesLote`: tipo, archivo, filas, errores, usuario y fecha. | Cada archivo entra en una transacción: si una fila falla, no entra nada y se devuelven los errores por fila. | — |

#### Semana 4 · Caja, adelantos y pedidos de Lima

| Día | Migración | Qué cambia | Índices y reglas | Datos existentes |
|---|---|---|---|---|
| **Lun 19/10** | AgregarCaja | Tablas `Cajas`, `SesionesCaja` (apertura y cierre, si se confirma) y `MovimientosCaja` (ingreso o egreso, concepto, monto, método, venta u OS, motivo). Catálogo de conceptos de gasto. | Índices por fecha y por sesión. | — |
| **Mié 21/10** | AgregarAdelantos | `Pagos`: marca de adelanto y vínculo con la OS. | El saldo es el total menos los pagos. Se calcula, no se guarda. | — |
| **Jue 22/10** | AgregarPedidosLima | Tablas `PedidosEspeciales` (número, cliente, OS, estado, fecha pedida, estimada y de llegada) y `DetallesPedido`. | Número con secuencia. | — |

#### Semana 5 · Auditoría, portal del cliente y fotos

| Día | Migración | Qué cambia | Índices y reglas | Datos existentes |
|---|---|---|---|---|
| **Lun 26/10** | ActivarAuditoria | `EventosAuditoria`: índices. Tabla `HistorialUnidad`: placa o propietario anterior y nuevo. | Un interceptor de EF escribe la auditoría y llena `CreadoPorId` y `ModificadoPorId` en la misma transacción. | — |
| **Mar 27/10** | AgregarPortalCliente | `Usuarios`: cliente vinculado. Tabla `CodigosActivacion`: cliente, hash del código, vencimiento, uso, intentos y quién lo generó. `OrdenesServicio`: respuesta del cliente, fecha, canal y quién respondió. | Un usuario por cliente. El código se guarda con hash, vence a las 48 horas y se bloquea tras 5 intentos fallidos. | — |
| **Vie 30/10** | AgregarFotosDeOrden | Tabla `FotosOrden`: orden, ruta, tipo (ingreso, diagnóstico o salida), tamaño y quién la subió. | Las imágenes van al almacenamiento de archivos; la base solo guarda la ruta. | — |

#### Semanas 6 a 8 · Citas, almacenes y datos reales

| Día | Migración | Qué cambia | Índices y reglas | Datos existentes |
|---|---|---|---|---|
| **Lun 02/11** | Sin migración | Reporte de ganancias con el costo que cada línea guarda desde la semana 1. | Índice por fecha de venta. | Si el cliente elige costo promedio, se recalcula en cada entrada con su costo unitario. |
| **Mié 04/11** | AgregarCitas | Tablas `Citas` (cliente, unidad, motivo, inicio, fin, estado, asignado a, quién la pidió y OS que la atendió), `BloqueosAgenda` y `HorariosAtencion`. | Restricción de exclusión con `btree_gist`: la base impide dos citas confirmadas del mismo técnico que se crucen. | — |
| **Lun 09/11** | AgregarAlmacenes | Tablas `Almacenes` y `StockAlmacen`. Almacén en movimientos y en líneas de OS y de venta. | Stock por almacén con el mismo bloqueo por fila. | Todo el stock actual pasa al almacén principal. |
| **Mié 11/11** | Ensayo de migración | Carga completa en el ambiente de pruebas. | Conteo por tabla contra el Excel y total de stock por repuesto. | — |
| **16 y 19/11** | Carga real | Datos reales en pruebas el 16/11 y carga final preparada el 19/11. | Backup antes y después de cada carga. | Orden: categorías, repuestos con costo y stock, clientes, unidades, e historial si lo entregan. |

> **Almacenes es el cambio más delicado de la base.** Si cada almacén lleva su propio stock, el cambio toca repuestos, movimientos, OS, ventas y caja. Hecho en la semana 7, obliga a rehacer lo de las semanas 3 y 4. Por eso la pregunta se adelantó al 05/10: si la respuesta es sí, la migración pasa a la semana 3, antes de ventas, y ese tiempo se descuenta del margen.

### Integraciones

| Integración | Qué se hace | Cuándo | Depende de |
|---|---|---|---|
| Importación de Excel | Lector de Excel, validación fila por fila, carga en una transacción y reporte de errores. | 16/10, 11/11 y 16/11 | Excel del cliente, el 15/10 |
| Fotos de la OS | Una interfaz de almacenamiento con dos implementaciones: disco del servidor o almacenamiento en la nube. Límite de tamaño y solo tipos de imagen. | 30/10 | Hosting |
| Backup | Paolo deja el script de `pg_dump` con retención; el Ingeniero lo programa en el servidor y prueba una restauración. | Semana 2 y antes del UAT | Hosting |
| Yamaha | Hoy hay un cliente HTTP con timeout y configuración, pero no hay forma de elegir mock o real. En el margen de la semana 7 se agregan el modo mock o real por configuración y la tabla de registro de llamadas y reintentos. Las operaciones, cuando llegue la documentación. | Semana 7 y sin fecha | Credenciales y documentación |
| Hangfire | Se instala con almacenamiento en PostgreSQL cuando haya trabajos reales: sincronizar con Yamaha y vencer códigos de activación. | Con Yamaha | Documentación de Yamaha |
| WhatsApp | Adaptador de mensajería y webhook que reutiliza las preguntas frecuentes y la bandeja del chatbot. | Sin fecha | Proveedor y número |
| SUNAT, OSE o PSE | No se construye. El comprobante queda con serie, correlativo, IGV y totales para conectarlo después. | Sin fecha | Validar el alcance |

## Lo que necesitamos del cliente y para cuándo

Cada respuesta tiene fecha porque el módulo que la necesita empieza ese día. Si una llega tarde, ese módulo y lo que depende de él se corren. El Ingeniero las pide en la reunión del lunes anterior.

| Para cuándo | Qué | Lo necesita |
|---|---|---|
| Lun 28/09 | Nueva fecha de Go Live confirmada. Hosting: quién lo contrata y a nombre de quién. Lista de usuarios con su rol. Cómo se identifican las motos acuáticas y los generadores: VIN, serie o motor, y cuáles van en horas. | Semana 1 |
| Jue 01/10 | Catálogo de servicios con su precio de mano de obra. | Detalle de la OS, 02/10 |
| Lun 05/10 | Almacenes: cuántos hay y si cada uno lleva su propio stock. Si lo llevan, el cambio se adelanta a la semana 3, antes de ventas y caja. | Base de datos |
| Mar 06/10 | Campos del formato de atención: cuáles van estructurados y si la firma es impresa o en pantalla. Qué acciones aprueba Gerencia. | Semana 2 |
| Lun 12/10 | Datos de la empresa, series y correlativos, métodos de pago y para qué se usa el tipo de cambio. | Semana 3 |
| Jue 15/10 | Excel de migración: clientes, unidades y repuestos con costo y stock. | Muestra del 16/10 |
| Lun 19/10 | Caja: una o por usuario, con apertura y cierre diario o no. Adelantos. Qué son los pedidos de Lima y qué estados tienen. | Semana 4 |
| Lun 19/10 | Cuentas de desarrollador en Play Store y App Store a nombre de la empresa, si la app sale en tiendas. Apple verifica la empresa y puede tardar semanas. | Publicación, 12/11 |
| Lun 26/10 | Qué información ve el cliente en la app. Historial de placa: cambio de placa o de propietario. | Semana 5 |
| Lun 02/11 | Ganancias: si entran servicios y mano de obra, y si se usa costo promedio o último costo. Citas: quién las pide y si la agenda es por técnico. | Semana 6 |
| Lun 09/11 | Logo en negativo e ícono cuadrado de la app. | APK y tiendas, 12/11 |
| Sin fecha | Credenciales y documentación de Yamaha, proveedor y número de WhatsApp, y la validación del alcance de SUNAT. Se conectan cuando lleguen; no detienen el Go Live. | Integraciones |

## El resumen de cambios, módulo por módulo

Qué existe hoy en `develop`, qué se construye y en qué semana. «Origen» distingue lo que ya estaba en el alcance aprobado de lo nuevo.

| # | Módulo | Hoy en develop | Qué se construye | Semana | Origen |
|---|---|---|---|---|---|
| 1 | Roles y permisos | Se guardan, pero ningún endpoint los revisa. Solo existen Admin y Recepción. | Cinco roles definitivos, catálogo de permisos, policies y permisos en `/auth/me`. | 1 | Aprobado |
| 2 | Portal cliente | Nada, más allá de las pantallas de referencia. | Rol Cliente, activación con DNI y código, mis unidades y OS, timeline, presupuestos, aprobación, comprobantes, historial y ayuda. | 5–6 | Aprobado |
| 3 | Órdenes de servicio | Apertura, diagnóstico, detalle y 7 estados. Solo fecha de apertura y cierre. | Historial, fecha estimada, medidor y salida (S1). Correlativo, búsqueda, formato, avances y aprobación de Gerencia (S2). Vínculo con venta y comprobante (S3). | 1–3 | Aprobado · Nuevo: Gerencia |
| 4 | Servicios | Mano de obra con descripción y precio libres, sin tipo ni IGV. | Detalle con tipo, catálogo de servicios con precio, IGV y totales. Almacén o destino del repuesto (S7, o S3 si cada almacén lleva stock). | 1–2, 7 | Aprobado · Nuevo: Almacenes |
| 5 | Unidades | Placa obligatoria y solo kilometraje. | Tipo, placa opcional, VIN o serie, motor y medidor km u horas (S1). Historial de placa (S5). | 1, 5 | Aprobado |
| 6 | Repuestos | Kardex, entradas, salidas, ajustes y stock mínimo por producto. | Costo, umbral de 4, umbral por categoría y ajuste de stock según permiso. | 1 | Aprobado |
| 7 | Ventas | Cotización, venta y anulación. Solo repuestos, con el precio del catálogo. | Servicios, métodos de pago y OS con su venta sin doble descuento (S3). Adelantos, saldo y pedidos de Lima (S4). | 3–4 | Aprobado · Nuevo: Adelantos, Lima |
| 8 | Comprobantes | Tipo, serie, número y estado, ligados a la venta. | OS, servicios, IGV, método de pago y observaciones. SUNAT se valida después. | 3 | Aprobado |
| 9 | Ganancias | Los repuestos no tienen costo. | Costo desde la semana 1 y en la migración. Costo en cada venta y reporte (S6). | 1, 6 | Nuevo |
| 10 | Caja | Nada. | Ingresos automáticos por venta, adelantos, egresos, gastos con motivo, saldo y reportes. | 4 | Nuevo |
| 11 | Citas y agenda | Nada. Estaba fuera del alcance aprobado. | Solicitud, agenda del personal, bloqueos, aprobación y cita desde el portal. | 6–7 | Nuevo |
| 12 | Configuración | Solo las preguntas del chatbot. | Empresa, catálogos, IGV, umbrales, series y correlativos, parámetros generales. | 3 | Aprobado |
| 13 | Tipo de cambio | Nada. | Registro manual diario PEN/USD con usuario y hora. | 3 | Nuevo |
| 14 | Formato de atención | OS con motivo de ingreso y diagnóstico. | Tipo de servicio, falla, solución, terceros, recomendaciones y OS imprimible con firmas. | 2–3 | Aprobado |
| 15 | Reportes | Tablero y reportes de OS, ventas y stock bajo. | Filtrados por rol, con caja y ganancias. | 6 | Aprobado |
| 16 | Auditoría | La tabla existe, pero nada escribe en ella. | Registro automático de aprobaciones, estados, stock, precios, anulaciones y configuración, con consulta. | 4–5 | Aprobado |
| 17 | Seguridad | Solo exige sesión iniciada. | Reglas por rol en el servidor; el cliente solo ve lo suyo desde la semana 5. | 1, 5 | Aprobado |
| 18 | Pendientes técnicos | Conexión con Yamaha preparada, sin operaciones. | Contraseñas (S1), importador y migración (S3 y S8), fotos (S5). Yamaha y Hangfire cuando haya documentación. | 1–8 | Aprobado |

## Lo que ya faltaba antes de la reunión

| Pendiente | Dónde queda | Responsable |
|---|---|---|
| Hosting y despliegue | Hosting en la semana 1 y ambiente de pruebas en la semana 2, para las demos de los lunes. Producción el 20/11. | Ingeniero |
| `main` y `develop` sin historia común | Semana 2, antes del primer despliegue. | Ingeniero |
| Backup | Automático desde que exista el ambiente de pruebas, con una restauración de prueba antes del UAT. | Ingeniero |
| Doble descuento de stock | Miércoles 14/10, con el cierre de la OS con su venta. Hoy no ocurre porque la web nunca liga una venta a una OS. | Paolo |
| Recuperar la contraseña sin ayuda | El martes 29/09 entra el restablecimiento por el administrador. La recuperación por correo necesita la integración de correo, que está fuera del alcance. | Paolo · Santiago |
| Publicación en Play Store y App Store | Jueves 12/11, si las cuentas están abiertas el 19/10. Si no, el Go Live sale con el APK de Android. | Cliente · Santiago |
| WhatsApp del chatbot | Cuando haya proveedor y número. El asistente de la web y de la app ya funciona. | Cliente · Paolo |
| Operaciones de Yamaha y Hangfire | Cuando lleguen la documentación y las credenciales. La conexión ya está preparada. | Ingeniero · Paolo |
| Datos de prueba en las bases locales | No se migran. Se limpian antes de la primera demo del 05/10. | Paolo · Santiago |
| Código sin uso en la web | Lunes 28/09: `EstadoOrdenTag` y `data/ejemplo.ts`. | Santiago |

## Riesgos y qué hacer si ocurren

- **Una respuesta del cliente llega tarde.** El módulo se corre y consume margen. Si el margen de la semana 7 se agota, el Ingeniero acuerda con el cliente una nueva fecha de Go Live.
- **Las estimaciones se quedan cortas.** Hay 3,5 días de margen, un 12 % sobre el back. Si una semana se atrasa más de un día, se usa el sábado de esa semana y se avisa en la reunión del lunes.
- **El cliente pide más cosas durante el desarrollo.** Se anotan y se estiman en la reunión del lunes. No entran al plan sin mover la fecha o sacar otra cosa.
- **Un solo desarrollador por lado.** Cualquier ausencia de Paolo o de Santiago mueve todo lo que depende de él. Las entregas diarias en `develop` reducen lo que se pierde.
- **Las cuentas de las tiendas no se abren a tiempo.** El Go Live sale con el APK de Android y los iPhone esperan a que Apple apruebe la cuenta.
