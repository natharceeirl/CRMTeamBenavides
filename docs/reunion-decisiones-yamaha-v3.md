# Documento de Trabajo para Reunión Técnica y Funcional: Decisiones Pendientes de Integración Yamaha Motor del Perú v3

- **Proyecto:** CRM Team Benavides (`CRMTeamBenavides.Api` — .NET 10 / EF Core / PostgreSQL)
- **Rama actual:** `develop`
- **Commit base publicado:** `65da898` (`feat(yamaha): agregar contratos v3, validaciones previas, dry-run y sanitizacion de logs`)
- **Fecha de preparación:** 12 de octubre de 2026
- **Propósito del documento:** Servir como guía estructurada y acta de acuerdos para la reunión con el ingeniero responsable y la gerencia de negocio, con el fin de resolver las **9 decisiones funcionales/técnicas (`D-01` a `D-09`)** y **1 ambigüedad de especificación (`AMB-3.4-8`)** antes de implementar los extractores de base de datos y antes de habilitar cualquier envío HTTP productivo hacia Yamaha Motor del Perú S.A. (YMDP).

---

## 1. Resumen Ejecutivo

### 1.1 Estado Actual del Proyecto
En el commit `65da898` (publicado en `origin/develop`) se completó la **Primera Entrega Segura** de la integración con Yamaha Motor del Perú v3:
1. **Contratos JSON v3 exactos e inmutables** ([`YamahaV3Contracts.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs)):
   - [`YamahaVentaV3Dto`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs#L153-L282) (39 campos exactos, Sección 4).
   - [`YamahaStockV3Dto`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs#L289-L328) (12 campos exactos, excluyendo `SitioDealer` y `FechaVenta`, Sección 5.3).
   - [`YamahaServicioTecnicoV3Dto`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs#L335-L440) (30 campos exactos, excluyendo `FechaApertura`, `FechaCierre`, `MOSinIGV`, `RepuestosSinIGV` e `IGV`, Sección 6.2).
2. **Motor determinista de validación pre-envío** ([`YamahaV3Validator.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Validator.cs)): valida en memoria tipos, longitudes (`VARCHAR(255)`), precisión `DECIMAL(10,2)` y `DECIMAL(5,2)`, fechas `DD/MM/YYYY`, signos negativos en Notas de Crédito (Sección 4.2), regla de stock cero por línea (Sección 5.2) y consistencia de los 6 campos de cabecera por `NumeroOrdenTrabajo` (Sección 6.1).
3. **Servicio y endpoints de Simulación Local (Dry-Run)** ([`YamahaDryRunService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Yamaha/YamahaDryRunService.cs) y [`YamahaEndpoints.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaEndpoints.cs#L107-L220)): permiten evaluar lotes en memoria sin tocar la base de datos ni abrir conexiones HTTP hacia Yamaha (`EnviadoAYamaha = false`, `PersistidoEnBaseDeDatos = false`).
4. **Sanitización de credenciales** ([`YamahaSecurityHelper.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaSecurityHelper.cs) y [`YamahaApiClient.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaApiClient.cs)): enmascara cualquier `?code={Function_Key}` en logs y excepciones.
5. **Suite de pruebas unitarias** ([`YamahaV3FirstDeliveryTests.cs`](file:///d:/Projects/CRMTeamBenavides/tests/CRMTeamBenavides.Api.Tests/YamahaV3FirstDeliveryTests.cs)): 32/32 pruebas automatizadas en verde.

### 1.2 Qué Falta Acordar en Esta Reunión
El modelo transaccional actual del CRM Team Benavides fue diseñado para la operación interna del concesionario (mostrador, taller, almacén, compras y caja chica). Para construir la **Fase B (Extractores de Base de Datos y Adaptación de Reglas de Dominio)** sin asumir reglas contables o comerciales no autorizadas, es indispensable tomar postura sobre **10 puntos (`D-01` a `D-09` y `AMB-3.4-8`)** detallados en la Sección 3 de este documento.

---

## 2. Clasificación Epistemológica: Hechos, Interpretaciones, Decisiones y Dudas Externas

Para evitar confusiones durante la reunión, toda la información de este documento se divide estrictamente en cuatro categorías:

### 2.1 Hechos Verificados (Constatados en la Especificación v3 y en el Código Fuente)
- **H-01 (Tres servicios HTTP POST independientes):** La especificación oficial v3 (Sección 2 y Sección 7) define tres endpoints Azure Functions (`ReceiveRetailVentas`, `ReceiveRetailStock`, `ReceiveRetailServicioTecnico`) autenticados mediante `?code={Function_Key}` y `Content-Type: application/json`.
- **H-02 (Frecuencia y alcance de envíos — Sección 3.1 y Sección 8):**
  - `Ventas` y `Servicio Técnico`: cada 4 horas. El **primer envío** debe incluir el histórico de los **últimos 3 meses**; del **segundo envío en adelante**, solo registros nuevos o modificados (deltas).
  - `Stock`: una vez al día al cierre de operaciones, enviando siempre la foto completa del stock disponible del día.
- **H-03 (Correlativo actual de órdenes de taller en el CRM):** En [`OrdenServicioService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Taller/OrdenServicioService.cs#L33-L39), el método `GenerarNumeroOrdenAsync()` genera correlativos con prefijo `OS-` (`$"OS-{seqVal:D6}"`, ej. `OS-000001`). La especificación Yamaha v3 (Sección 6.2, fila `NumeroOrdenTrabajo`) exige: *"Debe iniciar con `OT-` seguido de números; cualquier otro formato es rechazado."*
- **H-04 (Duplicación estructural entre Orden de Servicio facturada y Venta en el CRM):** En [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L230-L255), al facturar una `OrdenServicio`, el CRM crea un registro en `Ventas` con `Venta.OrdenServicioId != null` y copia todos sus `DetalleServicio` (tanto `Repuesto` como `Servicio`, `ManoDeObra` y `Terceros`) hacia `DetalleVenta`.
- **H-05 (Almacenamiento de costos y precios en `Producto`):** En [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L28-L30), la entidad `Producto` almacena dos columnas monetarias distintas: `PrecioVenta` (precio de venta sugerido) y `Costo` (costo de adquisición/reposición).
- **H-06 (Anulaciones in-place sin entidad Nota de Crédito):** En [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1111-L1249) (`AnularAsync`) y [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1417-L1456) (`AnularComprobanteAsync`), las anulaciones cambian el estado del registro a `Anulada` / `"Anulado"`. El enum [`TipoComprobante`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L6-L10) solo contiene `Boleta` y `Factura`.
- **H-07 (Descuentos no persistidos en líneas de venta ni de taller):** Aunque [`CreateDetalleVentaRequest`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Ventas/VentaContracts.cs#L15) declara `decimal? Descuento`, las entidades [`DetalleVenta`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L90-L115) y [`DetalleServicio`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L156-L194) solo persisten el `PrecioUnitario` final pactado sin conservar columnas de descuento.
- **H-08 (Modelo de `Vehiculo` orientado a clientes de taller):** En [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L53-L80), `Vehiculo` exige `ClienteId` obligatorio y sus campos `Placa` (`L58`) y `NumeroSerieVIN` (`L63`) son anulables (`string?`). `Producto` no tiene `VIN`, `NumeroMotor` ni `AnioModelo`.
- **H-09 (Catálogo `TipoAtencion` vs `TipoServicio`):** En [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L14-L20), el enum `TipoAtencion` tiene 4 valores (`MantenimientoPreventivo`, `MantenimientoCorrectivo`, `ReclamoGarantia`, `Gratuito`), mientras que la Sección 6.3 de Yamaha define 10 códigos (`MP`, `D`, `R`, `MP+R`, `RC`, `G`, `I`, `L`, `SG`, `-`).

### 2.2 Interpretaciones Técnicas (Análisis de Ingeniería sobre el CRM y la Especificación)
- **I-01:** Si el extractor de `ReceiveRetailVentas` exportara todas las filas de `DetalleVenta` sin filtrar las originadas por `OrdenServicioId`, las líneas de mano de obra (`Servicio`, `ManoDeObra`) causarían el rechazo del lote porque `STE` no es una `LineaNegocio` válida en el Servicio 1 (Sección 4 solo admite `MC`, `ATV`, `OM`, `SP`).
- **I-02:** Transformar el prefijo `OS-` a `OT-` en la capa de proyección hacia Yamaha (`OS-000123` $\rightarrow$ `OT-000123`) cumple al 100 % la regla sintáctica de la Sección 6.2 sin romper referencias históricas en `MovimientoInventario.Motivo`, `EventoAuditoria`, notificaciones push ni búsquedas de los usuarios en el frontend web y app móvil.
- **I-03:** En la especificación oficial v3 existe una tensión redactiva entre la **Sección 3.4** (*"Para los campos que no aplican, se debe enviar una cadena vacía"*), la **Sección 6.2** (`Cantidad` `INT Condicional`, `PrecioUnit` `DECIMAL(10,2) No/Condicional`), la **Sección 4** (`AnioModelo` `INT Condicional`) y la **Sección 8** (*"Campos numéricos con valor cero: Ingresar `0` o `0.00` según el tipo. No dejar vacío"*).

### 2.3 Decisiones de Negocio Aún No Aprobadas
- **D-01:** Alcance de líneas de negocio a reportar desde el CRM (`SP` y `STE` únicamente vs. incluir venta/stock de unidades nuevas `MC`, `ATV`, `OM`).
- **D-04:** Estrategia operativa y contable para reportar anulaciones / Notas de Crédito posteriores a un envío.
- **D-05:** Si el concesionario opera ventas a `CREDITO` con entidades financieras de la tabla 4.1 o si todas las ventas del CRM son al `CONTADO`.
- **D-06:** Si se reportará `0.00` en los campos opcionales de descuento o si se modificará el modelo para persistir descuentos por línea.
- **D-07:** Regla para distinguir repuestos oficiales Yamaha frente a productos multimarca o insumos internos en `Productos`.
- **D-08:** Aprobación de la conversión no destructiva `OS-` $\rightarrow$ `OT-` frente a un cambio del correlativo interno.
- **D-09:** Mapeo del enum `TipoAtencion` hacia los códigos `TipoServicio` de la Sección 6.3 y código aplicable a las líneas de repuesto (`SP`) dentro de una orden de taller.

### 2.4 Limitaciones o Dudas que Requieren Confirmación de Yamaha (YMDP)
- **D-02:** Confirmar con YMDP si los repuestos facturados dentro de una Orden de Trabajo deben enviarse **solo** en el Servicio 3 (`ReceiveRetailServicioTecnico` con `SubLineaNegocio = "SP"`) o **también** en el Servicio 1 (`ReceiveRetailVentas` con `Canal = "TALLER"`).
- **D-03:** Confirmar con YMDP si `CostoVentaSinIGV` y `CostoVentaConIGV` en el Servicio 2 (`Stock`, Sección 5.3) corresponden al **costo de compra del concesionario** (`Producto.Costo`) o al **precio de venta al público** (`Producto.PrecioVenta`).
- **AMB-3.4-8:** Confirmar con el equipo técnico de YMDP qué espera exactamente su deserializador JSON en Azure Functions para los tres campos numéricos condicionales cuando no aplican a la línea (`AnioModelo` en `SP`/`OM`, y `Cantidad` / `PrecioUnit` en `SubLineaNegocio = "STE"`): cadena vacía `""` (Sección 3.4) o valor cero `0` / `0.00` (Sección 8).
- **Parámetros oficiales del concesionario:** Recepción del `CodigoPuntoVenta` asignado por YMDP a Team Benavides y las `Function_Key` de los entornos de pruebas/certificación y producción.

---

## 3. Desarrollo Detallado de las Decisiones Pendientes (`D-01` a `D-09` y `AMB-3.4-8`)

---

### Decisión D-01: Alcance de líneas de negocio (`SP`, `MC`, `ATV` y `OM`)

1. **Situación actual y motivo de la decisión:**
   - La especificación Yamaha v3 contempla en los Servicios de Ventas y Stock cuatro líneas de negocio (Sección 3.3): `MC` (Motocicletas), `ATV` (Cuatrimotos), `OM` (Motores Fuera de Borda) y `SP` (Spare Parts: Repuestos, Aceites y Accesorios).
   - En el CRM actual, el módulo de Inventario y Ventas modela productos por cantidad (`Producto` con `Codigo`, `StockActual`, `Costo`, `PrecioVenta`), lo cual se ajusta nativamente a **`SP` (Repuestos, Aceites y Accesorios)**. No existe en el CRM un módulo de inventario propio ni facturación de vehículos nuevos seriados por `VIN`, `NumeroMotor`, `AnioModelo` y `Color` propiedad del concesionario (la entidad `Vehiculo` pertenece siempre a un `Cliente` de taller).
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 3.3 (líneas 42–49), Sección 4 (campos condicionales `VIN`, `Modelo`, `Color`, `AnioModelo`, `NumeroMotor` obligatorios para `MC`/`ATV`/`OM`, líneas 72–75 y 86), y Sección 5.2/5.3 (`StockDisponible` por fila `VIN` para `MC`/`ATV`/`OM`, líneas 160–190).
   - **Código del CRM:** [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto` carece de `VIN`, `NumeroMotor`, `AnioModelo`, `Color` y `LineaNegocio`); [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L53-L80) (`Vehiculo.ClienteId` es obligatorio para taller); [`YamahaDryRunService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Yamaha/YamahaDryRunService.cs#L89) y [` YamahaDryRunService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Yamaha/YamahaDryRunService.cs#L99).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L28) (Hallazgo 2.5, ítems `V-05` a `V-08`, `V-19`, `S-12`) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L245-L248).
3. **Alternativas viables:**
   - **Alternativa D-01A (Alcance acotado a `SP` y `STE`):** El CRM reportará en `Ventas` y `Stock` exclusivamente la línea `SP` (Repuestos, Aceites y Accesorios) y en `Servicio Técnico` la línea `STE` (`SubLineaNegocio` = `SP` y `STE`).
   - **Alternativa D-01B (Alcance extendido a unidades `MC`/`ATV`/`OM`):** Construir un nuevo submódulo de inventario y venta de unidades vehiculares seriadas por `VIN` y `NumeroMotor` antes de salir a producción con Yamaha.
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - **D-01A:** Cero cambios estructurales en las tablas `Productos` y `DetallesVenta`; permite salir a producción rápidamente con los datos reales que hoy opera el CRM (`SP` y `STE`).
   - **D-01B:** Impacto alto en base de datos (nuevas tablas o columnas de unidades seriadas), pantallas de recepción/compra de motos por `VIN`, selección de `VIN` al facturar y validaciones adicionales. Retrasaría la entrega si el negocio no vende motos nuevas desde este CRM o las gestiona por otro canal.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Adoptar la **Alternativa D-01A** para la puesta en marcha inicial si la operación del CRM está centrada en Repuestos (`SP`) y Taller (`STE`), dejando la arquitectura de contratos (`YamahaVentaV3Dto` y `YamahaStockV3Dto`) ya preparada (como se hizo en la Entrega 1) por si en el futuro se incorpora venta de unidades.
6. **Preguntas para la reunión (Ingeniero / Negocio):**
   - ¿Team Benavides factura motocicletas (`MC`), cuatrimotos (`ATV`) o motores fuera de borda (`OM`) nuevos a través de este CRM, o este sistema se utiliza únicamente para Repuestos/Accesorios (`SP`) y Taller (`STE`)?
   - En caso de vender unidades `MC`/`ATV`/`OM`, ¿se facturan en otro sistema o deben incorporarse al alcance de una fase posterior en el CRM?
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Decisión D-02: Tratamiento de repuestos del taller en Ventas frente a Servicio Técnico

1. **Situación actual y motivo de la decisión:**
   - Cuando una Orden de Servicio se factura en el CRM, [`VentaService.CreateAsync`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L230-L255) crea automáticamente una `Venta` vinculada (`Venta.OrdenServicioId = orden.Id`) que contiene como `DetalleVenta` tanto los repuestos como la mano de obra de esa orden, y luego emite el `Comprobante` (`Boleta` o `Factura`).
   - Por un lado, la Sección 6.1 de la especificación exige reportar en el **Servicio 3 (`ReceiveRetailServicioTecnico`)** cada repuesto usado en la orden (`SubLineaNegocio = "SP"`) y la mano de obra (`SubLineaNegocio = "STE"`).
   - Por otro lado, la Sección 4 (**Servicio 1 — `ReceiveRetailVentas`**) incluye el campo opcional `Canal` con valores permitidos `MOSTRADOR / TALLER`.
   - Si el extractor de `Ventas` incluye las ventas con `OrdenServicioId != null`, los repuestos de la orden de trabajo se enviarían **dos veces** a Yamaha (una vez en `ReceiveRetailVentas` con `Canal = "TALLER"` y otra vez en `ReceiveRetailServicioTecnico` con `SubLineaNegocio = "SP"`).
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 4, fila `Canal` (`MOSTRADOR / TALLER`, línea 83); Sección 6.1 (*Multiplicidad de Registros por Orden de Trabajo*, líneas 195–199: *"3 registros con `SubLineaNegocio` = `SP` (uno por cada repuesto) y 1 registro con `SubLineaNegocio` = `STE` (mano de obra)"*).
   - **Código del CRM:** [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L60) (`Venta.OrdenServicioId`); [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L230-L255) y [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1360-L1378).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L125) (regla `R-V5`, Sección 7 riesgo 2) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L161-L167).
3. **Alternativas viables:**
   - **Alternativa D-02A (Separación estricta sin doble reporte):**
     - `ReceiveRetailVentas` exporta únicamente ventas directas de mostrador (`Venta.OrdenServicioId == null`, con `Canal = "MOSTRADOR"`).
     - Las órdenes de servicio facturadas (`Venta.OrdenServicioId != null`) se exportan íntegramente (repuestos `SP` + mano de obra `STE`) en `ReceiveRetailServicioTecnico`.
   - **Alternativa D-02B (Reporte dual de repuestos de taller):**
     - `ReceiveRetailServicioTecnico` exporta la orden completa (repuestos `SP` + mano de obra `STE`), y además `ReceiveRetailVentas` exporta **únicamente las líneas de tipo `Repuesto`** (`DetalleVenta.TipoItem == TipoItemServicio.Repuesto`) de las ventas con `OrdenServicioId != null` marcándolas con `Canal = "TALLER"` (excluyendo siempre la mano de obra del Servicio 1).
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - **D-02A:** Evita duplicar la facturación de repuestos en los tableros de BI de Yamaha; cada sol facturado por repuesto se informa en un único servicio. Riesgo: si el área comercial de repuestos de Yamaha solo mira `ReceiveRetailVentas` filtrando `Canal = "TALLER"`, no vería allí el consumo de taller.
   - **D-02B:** Poblaría `Canal = "TALLER"` en `ReceiveRetailVentas`, pero si Yamaha suma ambos servicios sin deduplicar, duplicaría las unidades y soles vendidos de repuestos en taller.
   - **Regla invariante en ambas alternativas:** Las líneas con `TipoItem != Repuesto` (`Servicio`, `ManoDeObra`, `Terceros`) **jamás** deben enviarse a `ReceiveRetailVentas`.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Parametrizar una bandera en [`YamahaSettings`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaSettings.cs) (por ejemplo, `IncluirRepuestosDeTallerEnVentas`, por defecto `false` bajo **Alternativa D-02A**) y elevar la consulta a YMDP para activar `true` (**Alternativa D-02B**) solo si Yamaha confirma que espera los repuestos de OT en ambos endpoints.
6. **Preguntas para la reunión (Ingeniero / YMDP):**
   - ¿Yamaha requiere que los repuestos incluidos en una Orden de Trabajo facturada se envíen **únicamente** en `ReceiveRetailServicioTecnico` (`SubLineaNegocio = "SP"`), o **también** en `ReceiveRetailVentas` con `Canal = "TALLER"`?
   - ¿En qué escenario utiliza Yamaha el valor `Canal = "TALLER"` dentro del Servicio 1 (`Ventas`)? (¿Por ejemplo, cuando un cliente compra un repuesto en el mostrador del taller sin abrir Orden de Trabajo, o para todas las OTs?).
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Decisión D-03: Fuente del costo de inventario en Stock (`CostoVentaSinIGV` y `CostoVentaConIGV`)

1. **Situación actual y motivo de la decisión:**
   - En la Sección 5.3 (Servicio de Stock), Yamaha exige dos campos obligatorios de tipo `DECIMAL(10,2)`: `CostoVentaSinIGV` y `CostoVentaConIGV`.
   - El nombre compuesto `CostoVenta...` es semánticamente ambiguo en contabilidad: puede referirse al **costo de adquisición/inventario del concesionario** ("Costo de Venta" contable) o al **precio de venta al público** del producto.
   - En el CRM, la entidad [`Producto`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L28-L29) tiene ambos campos por separado:
     - `Producto.Costo` (costo de compra/reposición del concesionario).
     - `Producto.PrecioVenta` (precio de lista para la venta).
   - Además, los productos recién creados mediante el **Importador Excel** (`Catalogo`: solo `Codigo` y `Nombre`) nacen con `Costo = 0` y `PrecioVenta = 0` hasta que se registra su compra o precio.
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 5.3, filas `CostoVentaSinIGV` (`DECIMAL(10,2)`, Obligatorio, *"Separador decimal: punto (.). Ej: 850.00"*, línea 186) y `CostoVentaConIGV` (`DECIMAL(10,2)`, Obligatorio, *"Separador decimal: punto (.). Ej: 1003.00"*, línea 187). Nótese que $850.00 \times 1.18 = 1003.00$.
   - **Código del CRM:** [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L28-L29) (`Producto.PrecioVenta`, `Producto.Costo`); [`Compras.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Compras.cs#L131-L135) (`DetalleCompra.CostoUnitarioSoles`); [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L18) (`ConfiguracionEmpresa.PorcentajeIgv`, 18 %).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L143-L144) (ítems `S-09`, `S-10`) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L73-L74).
3. **Alternativas viables:**
   - **Alternativa D-03A (Usar `Producto.Costo` — Costo de adquisición del concesionario):**
     - `CostoVentaSinIGV` = `Producto.Costo` (asumiendo o normalizando base sin IGV).
     - `CostoVentaConIGV` = $\text{Round}(\text{CostoVentaSinIGV} \times (1 + \frac{\text{PorcentajeIgv}}{100}), 2)$.
   - **Alternativa D-03B (Usar `Producto.PrecioVenta` — Precio de venta del concesionario):**
     - `CostoVentaSinIGV` = `Producto.PrecioVenta` (base sin IGV).
     - `CostoVentaConIGV` = $\text{Round}(\text{Producto.PrecioVenta} \times (1 + \frac{\text{PorcentajeIgv}}{100}), 2)$.
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - Si se envía `PrecioVenta` cuando Yamaha esperaba el costo de inventario (`Costo`), el valorizado del stock del dealer ante Yamaha estará inflado por el margen comercial.
   - Si se envía `Costo` cuando Yamaha esperaba `PrecioVenta`, el concesionario revelaría su costo interno o reportaría un monto menor al esperado.
   - También es crítico confirmar si los montos almacenados hoy en `Producto.Costo` y `Producto.PrecioVenta` dentro de la base de datos de Team Benavides están expresados **sin IGV** o **con IGV**, para aplicar correctamente la multiplicación $\times 1.18$ o la división $/ 1.18$.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Consultar a YMDP la definición contable exacta de `CostoVentaSinIGV`/`CostoVentaConIGV` y confirmar con el área contable de Team Benavides si `Producto.Costo` se ingresa sin IGV en el módulo de Compras.
6. **Preguntas para la reunión (Ingeniero / Negocio / YMDP):**
   - **Para YMDP:** En `ReceiveRetailStock`, ¿`CostoVentaSinIGV` y `CostoVentaConIGV` representan el **costo de compra del dealer** o el **precio de venta al público**?
   - **Para Negocio / Ingeniería interna:** En la operación actual del CRM, ¿`Producto.Costo` y `Producto.PrecioVenta` se registran en el sistema **sin IGV** (como asume [`VentaService.ConstruirDetalleVenta`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L596-L621) al sumar el 18 % encima de `PrecioUnitario`)?
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Decisión D-04: Notas de Crédito y reversión de ventas (Sección 4.2)

1. **Situación actual y motivo de la decisión:**
   - La Sección 4.2 de la especificación Yamaha v3 establece:
     > *"Cuando `TipoDocumentoVenta` corresponda a una Nota de Crédito (valor exacto: `NOTA DE CREDITO`, ver sección 4.3), el registro representa la reversión total o parcial de una venta ya reportada y debe enviarse con signo negativo en los siguientes campos: `MontoSinIGV`, `MontoConIGV`, `PrecioFinal`, `CantidadVendida`."*
   - En el CRM actual:
     - [`TipoComprobante`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L6-L10) solo tiene `Boleta = 0` y `Factura = 1`.
     - Cuando se anula una venta ([`VentaService.AnularAsync`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1111-L1249)) o se anula un comprobante ([`VentaService.AnularComprobanteAsync`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1417-L1456)), el sistema cambia el estado del registro existente a `EstadoVenta.Anulada` / `Comprobante.Estado = "Anulado"` sin crear un nuevo comprobante `NOTA DE CREDITO`.
   -Dado que el envío a Yamaha ocurre **cada 4 horas**:
     - Si una venta se emite y se anula **dentro de la misma ventana de 4 horas** (antes de enviarse a Yamaha), aún no fue reportada.
     - Si una venta se emite, **se envía a Yamaha en el corte de las 12:00**, y luego **se anula a las 15:00**, Yamaha ya tiene registrada la venta positiva; si no se le envía un registro `NOTA DE CREDITO` con valores negativos, la venta quedará activa para siempre en los sistemas de Yamaha.
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 4.2 (líneas 125–133), Sección 4.3 (líneas 134–142) y Sección 8 (línea 309).
   - **Código del CRM:** [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L6-L10) y [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L117-L139); [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1111-L1249) y [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1417-L1456); [`YamahaV3Validator.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Validator.cs#L378-L440) (`ValidarSignosMontosYCantidadVentas`, reglas `YAM-V-NC-001` a `004`).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L122) (regla `R-V2`) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L143-L152).
3. **Alternativas viables:**
   - **Alternativa D-04A (Reversión sintética automática sin alterar el módulo de facturación):**
     - Si un comprobante se anula **antes** de haber sido enviado a Yamaha, se omite tanto la venta como su anulación.
     - Si un comprobante se anula **después** de haber sido enviado en un lote exitoso previo, el extractor de Yamaha genera automáticamente para el siguiente delta los registros de reversión con `TipoDocumentoVenta = "NOTA DE CREDITO"` y signos negativos (`-CantidadVendida`, `-MontoSinIGV`, `-MontoConIGV`, `-PrecioFinal`), conservando trazabilidad del comprobante anulado.
   - **Alternativa D-04B (Capturar el número de Nota de Crédito al anular un comprobante):**
     - Mantener el flujo actual de `AnularAsync` / `AnularComprobanteAsync`, pero agregar un campo opcional `NumeroNotaCredito` / `FechaNotaCredito` en `Comprobante` para que, cuando se anule un comprobante fiscal, se guarde el número real de la Nota de Crédito emitida en SUNAT/facturador externo y ese sea el `NumeroComprobante` enviado a Yamaha bajo `TipoDocumentoVenta = "NOTA DE CREDITO"`.
   - **Alternativa D-04C (Módulo completo de Notas de Crédito y Débito en el CRM):**
     - Ampliar `TipoComprobante` con `NotaCredito` y `NotaDebito`, series propias (`FC01`, `BC01`) y pantallas en frontend web y móvil.
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - **D-04A:** Cero cambios en base de datos ni en pantallas. Riesgo: ¿qué `NumeroComprobante` espera Yamaha en el registro `NOTA DE CREDITO`? Si Yamaha espera la serie/número de la Nota de Crédito (`BC01-000001`) en lugar del número de la boleta original (`B001-000123`), la reversión sintética sin número de NC real podría chocar por clave duplicada o formato.
   - **D-04B:** Bajo impacto y alta fidelidad fiscal: permite informar el número real de la Nota de Crédito sin reescribir la contabilidad interna del CRM.
   - **D-04C:** Alto impacto (requiere cambios en backend, migración, frontend web y móvil).
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Si una venta se anula **antes** de su envío a Yamaha, no enviarla. Para anulaciones **posteriores** al envío, implementar **D-04A** (o **D-04B** si el negocio emite un correlativo de Nota de Crédito en su facturador electrónico que debe informarse en `NumeroComprobante`).
6. **Preguntas para la reunión (Ingeniero / Negocio / YMDP):**
   - Cuando se anula en el CRM una Boleta o Factura de días anteriores, ¿cómo se emite hoy la Nota de Crédito ante SUNAT (en un facturador externo / portal SUNAT) y qué serie/correlativo tiene?
   - En el registro `NOTA DE CREDITO` enviado a Yamaha, ¿`NumeroComprobante` debe contener el correlativo de la **Nota de Crédito** (ej. `BC01-000005`) o el del **comprobante original** que se está revirtiendo?
   - ¿Qué ocurre en el Servicio Técnico (`ReceiveRetailServicioTecnico`) si se anula el comprobante de una Orden de Trabajo que ya había sido enviada como `FACTURADO`? (La Sección 6 no contempla Notas de Crédito ni montos negativos).
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Decisión D-05: Contado, crédito y entidades financieras (Sección 4.1)

1. **Situación actual y motivo de la decisión:**
   - En el Servicio 1 (`Ventas`), el campo `TipoPago` es **Obligatorio** (`CONTADO / CREDITO`) y el campo `EntidadFinanciera` se rige por la tabla de la Sección 4.1:
     - Si `TipoPago == "CONTADO"`, `EntidadFinanciera` **debe enviarse como `"CONTADO"`**.
     - Si `TipoPago == "CREDITO"`, `EntidadFinanciera` debe indicar la entidad (`EFECTIVA`, `INTEGRA`, `MERCADO PAGO`, `GLOBAL GO`, `CREDITO DIRECTO`, `FONBIENES`, `CALIDDA`, `BCP`, `BBVA` u otra especificada libremente).
   - En el CRM actual, [`Pago`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L28-L55) y [`MetodoPago`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L22-L26) registran medios de cobro (`EFECTIVO`, `TARJETA`, `TRANSFERENCIA`, `YAPE_PLIN`), pero no existe un campo `TipoPago` (`CONTADO`/`CREDITO`) ni `EntidadFinanciera` en `Venta` ni en `Comprobante`.
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 4 (filas `TipoPago` y `EntidadFinanciera`, líneas 79–80) y Sección 4.1 (tabla de valores permitidos, líneas 108–124).
   - **Código del CRM:** [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L22-L88); [`MetodosPagoSeeder.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Data/Seed/MetodosPagoSeeder.cs#L16-L21); [`YamahaV3Validator.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Validator.cs#L442-L484) (`ValidarPagoYEntidadFinanciera`).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L88-L89) (`V-12`, `V-13`, `R-V1`) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L153-L160).
3. **Alternativas viables:**
   - **Alternativa D-05A (Operación 100 % al Contado para efectos del reporte):**
     - Proyectar siempre `TipoPago = "CONTADO"` y `EntidadFinanciera = "CONTADO"` en todas las ventas exportadas por el CRM, sin modificar base de datos ni pantallas.
   - **Alternativa D-05B (Derivación automática por saldo pendiente -> `CREDITO DIRECTO`):**
     - Si al momento de emitir el comprobante la venta está totalmente pagada (`Saldo == 0`), enviar `TipoPago = "CONTADO"` y `EntidadFinanciera = "CONTADO"`. Si tiene saldo pendiente (`Saldo > 0`), enviar `TipoPago = "CREDITO"` y `EntidadFinanciera = "CREDITO DIRECTO"`.
   - **Alternativa D-05C (Selector explícito de Entidad Financiera en Venta/Comprobante):**
     - Agregar campos opcionales `TipoPago` y `EntidadFinanciera` en `Venta`/`Comprobante` para seleccionar una entidad del catálogo 4.1 cuando sea financiada.
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - **D-05A:** Cero impacto en UI y base de datos; cumple siempre la validación de la Sección 4.1.
   - **D-05B:** Tampoco requiere cambios en UI ni migraciones, y refleja fielmente cuándo el concesionario otorgó crédito directo al cliente (`Saldo > 0`).
   - **D-05C:** Solo es necesaria si Team Benavides vende productos en el CRM financiados por entidades externas como Efectiva, Global Go, Fonbienes, Calidda, etc.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Adoptar **D-05A** si todas las ventas de repuestos en el CRM se consideran al contado (o **D-05B** si las ventas con saldo pendiente deben informarse como `"CREDITO"` / `"CREDITO DIRECTO"`).
6. **Preguntas para la reunión (Ingeniero / Negocio):**
   - ¿En el CRM se registran ventas financiadas mediante entidades externas de la tabla 4.1 (`EFECTIVA`, `INTEGRA`, `GLOBAL GO`, `FONBIENES`, `CALIDDA`, `BCP`, `BBVA`, `MERCADO PAGO`), o todas las ventas de mostrador son al `CONTADO` (o a lo sumo `CREDITO DIRECTO` del concesionario)?
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Decisión D-06: Tratamiento de descuentos (`DescuentoPorcentaje`, `DescuentoSoles` y `Descuento`)

1. **Situación actual y motivo de la decisión:**
   - En Ventas (Sección 4), existen los campos **opcionales**:
     - `DescuentoPorcentaje` (`DECIMAL(5,2)`, No obligatorio: *"Ingresar `0.00` si no aplica"*).
     - `DescuentoSoles` (`DECIMAL(10,2)`, No obligatorio: *"Ingresar `0.00` si no aplica"*).
     - `PrecioUnitarioConIGV` (`DECIMAL(10,2)`, No obligatorio: *"Precio unitario antes de descuentos, incluye IGV"*).
   - En Servicio Técnico (Sección 6.2), existe el campo **opcional**:
     - `Descuento` (`DECIMAL(5,2)`, No obligatorio: *"Porcentaje de descuento aplicado. Ingresar `0.00` si no aplica"*).
   - En el CRM actual, cuando un vendedor o asesor modifica hacia abajo el precio de un producto o servicio, el sistema valida/solicita aprobación gerencial si baja del precio de catálogo, pero en [`DetalleVenta`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L90-L115) y [`DetalleServicio`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L156-L194) **solo guarda el `PrecioUnitario` final acordado**, sin guardar una columna separada con el descuento ni el precio de lista histórico.
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 4 (líneas 92–94), Sección 6.2 (línea 249) y Sección 8 (línea 302: *"Campos numéricos con valor cero: Ingresar `0` o `0.00` según el tipo. No dejar vacío"*).
   - **Código del CRM:** [`VentaContracts.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Ventas/VentaContracts.cs#L15); [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L90-L115); [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L156-L194); [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L287-L295).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L101-L103) (`V-25` a `V-27`, `ST-29`) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L252-L254).
3. **Alternativas viables:**
   - **Alternativa D-06A (Reportar `0.00` en descuentos y el precio pactado como base):**
     - Enviar siempre `DescuentoPorcentaje = 0.00m`, `DescuentoSoles = 0.00m` (en Ventas) y `Descuento = 0.00m` (en Servicio Técnico), y calcular `PrecioUnitarioConIGV` a partir del `DetalleVenta.PrecioUnitario` facturado más IGV.
   - **Alternativa D-06B (Inferir descuento comparando contra `Producto.PrecioVenta` actual):**
     - Si `DetalleVenta.PrecioUnitario < Producto.PrecioVenta`, calcular la diferencia como descuento. *(No recomendado porque `Producto.PrecioVenta` puede cambiar con el tiempo y alteraría el cálculo de ventas pasadas).*
   - **Alternativa D-06C (Persistir `PrecioLista` y `Descuento` en `DetalleVenta` y `DetalleServicio`):**
     - Agregar columnas en base de datos mediante migración para guardar el descuento en futuras ventas/órdenes.
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - **D-06A:** Cero migraciones de base de datos; total consistencia matemática entre `PrecioUnitarioConIGV`, `CantidadVendida`, `MontoConIGV` y `PrecioFinal` tanto para el histórico de 3 meses como para los envíos futuros.
   - **D-06B:** Riesgo alto de inconsistencia retroactiva si el precio de catálogo de un repuesto sube o baja después de la venta.
   - **D-06C:** Requiere migración de esquema PostgreSQL y aun así las ventas de los últimos 3 meses tendrían `0.00`.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Adoptar la **Alternativa D-06A** (`0.00` en campos opcionales de descuento), dado que la propia especificación los define como opcionales e indica expresamente *"Ingresar `0.00` si no aplica"*.
6. **Preguntas para la reunión (Ingeniero / Negocio):**
   - ¿Están de acuerdo en que el precio unitario pactado en la venta/orden se informe con `DescuentoPorcentaje = 0.00`, `DescuentoSoles = 0.00` y `Descuento = 0.00` (Alternativa D-06A) para mantener consistencia con el historial de los últimos 3 meses y evitar migraciones en base de datos?
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Decisión D-07: Productos Yamaha frente a todos los productos activos del catálogo

1. **Situación actual y motivo de la decisión:**
   - El Servicio 2 (`ReceiveRetailStock`), el Servicio 1 (`ReceiveRetailVentas`) y el Servicio 3 (`ReceiveRetailServicioTecnico` en líneas `SubLineaNegocio = "SP"`) exigen el campo `CodigoYamaha` (*"Código del SKU o producto asignado por Yamaha. Solo letras, números y guión (-), sin espacios"*).
   - En la tabla `Productos` del CRM ([`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54)):
     - Existe la propiedad opcional `Marca` (`string?`, `MaxLength(100)`).
     - Los productos creados mediante el **Importador Excel** (`Catalogo`: `Codigo` y `Nombre`) se insertan con `Marca = null`.
     - Asimismo, en el almacén del concesionario podrían existir consumibles, lubricantes o repuestos multimarca o códigos internos con espacios que no son SKUs de Yamaha.
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 4 (`CodigoYamaha`, línea 89), Sección 5.1 y 5.3 (`CodigoYamaha`, líneas 157–159 y 183), Sección 6.2 (`CodigoYamaha`, línea 243) y Sección 8 (línea 305).
   - **Código del CRM:** [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto.Codigo`, `Producto.Marca`, `Producto.Activo`); [`ImportacionExcelService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Importaciones/ImportacionExcelService.cs) (importa `Codigo` y `Nombre`).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L140) (`S-06`, Sección 7 riesgo 5) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L255-L257).
3. **Alternativas viables:**
   - **Alternativa D-07A (Todos los productos activos con `Codigo` compatible son Yamaha):**
     - Considerar que todo el catálogo activo (`Producto.Activo == true`) cuyo `Codigo` cumpla el patrón de SKU Yamaha (`^[A-Za-z0-9-]+$`) es reportable a Yamaha (excepto aquellos donde explícitamente `Producto.Marca` tenga otra marca distinta de `"YAMAHA"`).
   - **Alternativa D-07B (Filtro estricto por `Producto.Marca == "YAMAHA"` o categoría):**
     - Exportar únicamente productos donde `Producto.Marca` sea `"YAMAHA"` (case-insensitive). *(Nota: requeriría poblar `Marca = "YAMAHA"` en los productos importados por Excel que hoy tienen `Marca = null`).*
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - **D-07A:** Funciona inmediatamente con los productos cargados por el Importador Excel (que tienen `Marca == null`) y permite excluir productos que explícitamente tengan otra marca registrada (`!string.IsNullOrWhiteSpace(p.Marca) && !p.Marca.Equals("YAMAHA", OrdinalIgnoreCase)`).
   - **D-07B:** Si se exige `p.Marca == "YAMAHA"` y los productos importados por Excel tienen `Marca == null`, el reporte de Stock saldría vacío hasta actualizar masivamente la columna `Marca`.
   - **Riesgo en Servicio Técnico (`STE`):** ¿Qué pasa si en una Orden de Trabajo de una moto Yamaha se utiliza un insumo o repuesto que no tiene SKU Yamaha? Si se excluye solo esa línea `SP`, la orden sigue teniendo sus demás líneas; si se incluye con un código inválido, Yamaha rechazará la orden.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Adoptar la **Alternativa D-07A**: incluir todos los productos activos (`Producto.Activo == true`) donde `Marca` sea `null`, vacía o `"YAMAHA"`, y cuyo `Codigo` cumpla el formato alfanumérico con guión (`^[A-Za-z0-9-]+$`).
6. **Preguntas para la reunión (Ingeniero / Negocio):**
   - ¿En el catálogo `Productos` del CRM se registran exclusivamente repuestos/productos oficiales de Yamaha, o también se registran repuestos de otras marcas?
   - En caso de existir productos de otras marcas, ¿cómo los distingue hoy el personal de almacén (por el campo `Marca`, por `Categoria`, o por el formato del `Codigo`)?
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Decisión D-08: Evaluación de conversión de órdenes `OS-` a `OT-` sin cambios destructivos

1. **Situación actual y motivo de la decisión:**
   - En la Sección 6.2 de la especificación Yamaha v3, el campo `NumeroOrdenTrabajo` es **Obligatorio** y tiene una regla bloqueante explícita:
     > *"Ej: `OT-2025-001` u `OT-0001-8337`. Debe iniciar con `OT-` seguido de números; cualquier otro formato es rechazado."*
   - En el CRM actual, [`OrdenServicioService.GenerarNumeroOrdenAsync`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Taller/OrdenServicioService.cs#L33-L39) genera todas las órdenes con el formato `OS-000001` (`$"OS-{seqVal:D6}"`).
   - Todas las órdenes históricas de los últimos 3 meses almacenadas en la base de datos tienen prefijo `OS-`. Además, el código `OS-xxxxxx` está embebido en los textos de `MovimientoInventario.Motivo` (Kardex), en `EventoAuditoria`, en notificaciones push enviadas a teléfonos Android y en las pantallas de taller.
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 6.1 (líneas 196–198) y Sección 6.2, fila `NumeroOrdenTrabajo` (línea 227).
   - **Código del CRM:** [`OrdenServicioService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Taller/OrdenServicioService.cs#L33-L39); [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L89); [`YamahaV3Validator.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Validator.cs#L78-L79) (`NumeroOrdenTrabajoOtRegex`: `^OT-\d+(?:-\d+)*$`) y [`YamahaV3Validator.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Validator.cs#L654-L668).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L24) (Hallazgo 2.1, ítem `ST-07`) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L119-L129) (Sección 4.1).
3. **Alternativas viables:**
   - **Alternativa D-08A (Proyección no destructiva `OS-` $\rightarrow$ `OT-` al exportar a Yamaha):**
     - Mantener intacto `OrdenServicio.NumeroOrden` (`OS-000001`) en la base de datos y en la interfaz del CRM.
     - En el proyector hacia Yamaha v3, transformar determinísticamente el prefijo inicial `OS-` en `OT-` (ej. `OS-000045` $\rightarrow$ `OT-000045`). Como la secuencia `OrdenServicioNumeroSeq` es única y estrictamente creciente, la correspondencia `OS-xxxxxx` $\leftrightarrow$ `OT-xxxxxx` es 1 a 1 (biyectiva) tanto para órdenes nuevas como para el histórico de los últimos 3 meses.
   - **Alternativa D-08B (Cambiar el generador interno a `OT-` y renombrar en BD):**
     - Modificar `GenerarNumeroOrdenAsync()` para que emita `OT-` y ejecutar un `UPDATE` sobre las órdenes existentes.
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - **D-08A:** **Cero riesgo de regresión** en Kardex, auditoría, frontend web, app móvil e impresiones previas; resuelve simultáneamente las órdenes históricas de los últimos 3 meses y las futuras; trazabilidad inmediata (la orden `OT-000045` en Yamaha es exactamente la `OS-000045` en el CRM).
   - **D-08B:** Rompería la consistencia con los textos históricos guardados en `MovimientoInventario.Motivo` (`"Consumo en OS-000045"`), confundiría al personal habituado a buscar `OS-` y alteraría datos históricos en producción.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Aprobar la **Alternativa D-08A** (proyección determinista `OS-` $\rightarrow$ `OT-` en la capa de exportación hacia Yamaha, sin tocar la base de datos ni el generador interno).
6. **Preguntas para la reunión (Ingeniero / Negocio):**
   - ¿Se aprueba mantener `OS-xxxxxx` internamente en el CRM y proyectarlo automáticamente como `OT-xxxxxx` en el JSON enviado a Yamaha (Alternativa D-08A)?
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Decisión D-09: Mapeo de `TipoAtencion` a `TipoServicio` (Sección 6.3)

1. **Situación actual y motivo de la decisión:**
   - En el Servicio 3 (`Servicio Técnico`), el campo `TipoServicio` es **Obligatorio** en todas las filas (tanto `SubLineaNegocio = "STE"` como `SubLineaNegocio = "SP"`).
   - La Sección 6.3 define 10 códigos permitidos:
     - `MP` (MANT. PREV.)
     - `D` (DIAGNOSTICO)
     - `R` (REPARACION)
     - `MP+R` (MANT. PREV. + REP.)
     - `RC` (RECALL)
     - `G` (GARANTIA)
     - `I` (INSPECCION)
     - `L` (LAVADO)
     - `SG` (SERVICIO GRATUITO)
     - `-` (NO APLICA — *válido únicamente para líneas de repuesto `SubLineaNegocio = "SP"`; prohibido en `SubLineaNegocio = "STE"`*).
   - En el CRM actual, [`TipoAtencion`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L14-L20) en `OrdenServicio` tiene 4 valores:
     - `MantenimientoPreventivo = 0` ($\rightarrow$ `MP`)
     - `MantenimientoCorrectivo = 1` ($\rightarrow$ `R`)
     - `ReclamoGarantia = 2` ($\rightarrow$ `G`)
     - `Gratuito = 3` ($\rightarrow$ `SG`)
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:** Sección 6.2, filas `TipoServicio` y `Actividad` (líneas 241–242) y Sección 6.3 (tabla de 10 códigos, líneas 252–266).
   - **Código del CRM:** [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L14-L20) (`enum TipoAtencion`) y [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L101) (`OrdenServicio.TipoAtencion`); [`YamahaV3Validator.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Validator.cs#L942-L981) (`ValidarTipoServicioYActividad`).
   - **Auditoría y Plan:** [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md#L16) (`ST-21`, `R-ST5` en bloque 2) y [`docs/plan-implementacion-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/plan-implementacion-yamaha-v3.md#L258-L260).
3. **Alternativas viables:**
   - **Alternativa D-09A (Mapeo directo de los 4 valores actuales sin modificar UI):**
     - Mapear `MantenimientoPreventivo` $\rightarrow$ `"MP"`, `MantenimientoCorrectivo` $\rightarrow$ `"R"`, `ReclamoGarantia` $\rightarrow$ `"G"`, `Gratuito` $\rightarrow$ `"SG"`.
     - Para las líneas de repuestos (`SubLineaNegocio == "SP"`), heredar el mismo código `TipoServicio` de la orden (o usar `"-"` con `Actividad = ""` si se prefiere).
   - **Alternativa D-09B (Ampliar `TipoAtencion` con los 5 valores adicionales de Yamaha):**
     - Conservar los valores numéricos `0..3` existentes (para no afectar órdenes históricas) y agregar `Diagnostico = 4` (`D`), `MantenimientoPreventivoYReparacion = 5` (`MP+R`), `Recall = 6` (`RC`), `Inspeccion = 7` (`I`) y `Lavado = 8` (`L`).
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - **D-09A:** No requiere cambios en los formularios de creación de Orden de Servicio en web ni móvil; todas las órdenes históricas y nuevas producen un código 100 % válido para Yamaha (`MP`, `R`, `G`, `SG`).
   - **D-09B:** Permite reportar con mayor granularidad si el taller realiza campañas de `RC` (Recall), `D` (Diagnóstico), `MP+R`, `I` o `L`, pero requiere agregar esas opciones en los selectores de frontend web y móvil si el usuario debe elegirlas al abrir la OT.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Implementar de inmediato el mapeo base de los 4 valores actuales (**D-09A**) haciendo que tanto las líneas `STE` como las líneas `SP` de la orden lleven el `TipoServicio` de la cabecera y la descripción del ítem/servicio en `Actividad` (o ampliar el enum con `4..8` según **D-09B** si el taller atiende Recalls `RC` o mantenimientos combinados `MP+R`).
6. **Preguntas para la reunión (Ingeniero / Negocio):**
   - ¿El taller de Team Benavides necesita registrar explícitamente órdenes de tipo `RC` (Recall), `D` (Diagnóstico), `MP+R`, `I` (Inspección) o `L` (Lavado), o basta con los 4 tipos actuales (`MP`, `R`, `G`, `SG`)?
   - En las filas de repuestos (`SubLineaNegocio = "SP"`) de una Orden de Trabajo, ¿prefieren enviar el mismo `TipoServicio` de la orden (ej. `"MP"`) o enviar `"-"`?
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

### Ambigüedad Técnica AMB-3.4-8: Representación de campos numéricos condicionales cuando no aplican (`""` vs `0` / `0.00`)

1. **Situación actual y motivo de la decisión:**
   - En la especificación oficial Yamaha v3 existen **tres campos de tipo numérico (`INT` / `DECIMAL`) cuya obligatoriedad es `Condicional` o solo aplica a una sub-línea**:
     1. **Servicio 1 (`Ventas`) — `AnioModelo` (`INT`, Condicional):** *"Obligatorio para MC, ATV. No aplica para OM"* (ni para `SP`).
     2. **Servicio 3 (`Servicio Técnico`) — `Cantidad` (`INT`, Condicional):** *"Obligatorio para `SubLineaNegocio` = `SP`. No aplica para `STE` (mano de obra)."*
     3. **Servicio 3 (`Servicio Técnico`) — `PrecioUnit` (`DECIMAL(10,2)`, No/Condicional):** *"Precio unitario del repuesto. Solo aplica para `SubLineaNegocio` = `SP`."*
   - La especificación contiene dos instrucciones generales que entran en conflicto directo para estos 3 campos cuando no aplican:
     - **Sección 3.4 (*Campos Condicionales*, línea 51):** *"Para los campos que no aplican, se debe enviar una cadena vacía."* (`""`)
     - **Sección 8 (*Notas Importantes*, línea 302):** *"Campos numéricos con valor cero: Ingresar `0` o `0.00` según el tipo. No dejar vacío."*
2. **Evidencia concreta:**
   - **Especificación Yamaha v3:**
     - Sección 3.4 (línea 51): exige `""` para campos condicionales que no aplican.
     - Sección 4 (línea 75): define `AnioModelo` como `INT | Condicional`.
     - Sección 6.2 (líneas 245 y 248): define `Cantidad` (`INT | Condicional | No aplica para STE`) y `PrecioUnit` (`DECIMAL(10,2) | No | Solo aplica para SubLineaNegocio = SP`).
     - Sección 8 (línea 302): indica ingresar `0` o `0.00` en campos numéricos con valor cero y no dejar vacío.
   - **Código implementado en Entrega 1:**
     - En [`YamahaV3Contracts.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs#L476-L576), los convertidores [`YamahaNullableIntJsonConverter`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs#L533-L576) y [`YamahaNullableDecimal2JsonConverter`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs#L481-L526) están centralizados en un único lugar: si la propiedad es `null` (no aplica), emiten `""` (Sección 3.4); si tiene valor (`0` o `0.00`), emiten el número JSON (Sección 8).
3. **Alternativas viables:**
   - **Alternativa AMB-A (Seguir Sección 3.4: enviar `""` cuando el campo condicional no aplica a la línea):**
     - En ventas `SP`/`OM`: `"AnioModelo": ""`.
     - En mano de obra `STE`: `"Cantidad": ""`, `"PrecioUnit": ""`.
     - En campos de descuento que sí aplican como numéricos: `"DescuentoPorcentaje": 0.00`, `"DescuentoSoles": 0.00`, `"Descuento": 0.00`.
   - **Alternativa AMB-B (Seguir Sección 8 estricta para todo tipo numérico: enviar `0` / `0.00`):**
     - En ventas `SP`/`OM`: `"AnioModelo": 0`.
     - En mano de obra `STE`: `"Cantidad": 0`, `"PrecioUnit": 0.00`.
4. **Ventajas, riesgos e impacto (operación, datos e integración):**
   - Si el backend de Yamaha en Azure Functions deserializa en C# con tipos primitivos no anulables (`int AnioModelo`, `int Cantidad`, `decimal PrecioUnit`) sin un `JsonConverter` que acepte `""`, enviar `""` devolverá `HTTP 400 Bad Request` al parsear el JSON.
   - Por el contrario, si el validador de Yamaha verifica que en `STE` el campo `Cantidad` venga como `""` según la Sección 3.4, enviar `0` podría ser rechazado.
   - Gracias al diseño de [`YamahaNullableIntJsonConverter`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs#L533-L576) y [`YamahaNullableDecimal2JsonConverter`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaV3Contracts.cs#L481-L526), cambiar entre `""` y `0`/`0.00` requiere modificar únicamente **2 líneas** en los convertidores o una bandera en `YamahaSettings`.
5. **Recomendación técnica preliminar *(Propuesta sujeta a aprobación)*:**
   - Consultar esta duda puntual al equipo técnico de YMDP (o probar un payload de 1 registro contra el endpoint de pruebas de Yamaha cuando entreguen la `Function_Key`) y parametrizar el comportamiento en `YamahaJsonSerializer`.
6. **Preguntas para la reunión (Ingeniero / YMDP):**
   - **Para el equipo técnico de YMDP:** Cuando `LineaNegocio = "SP"` en Ventas (donde `AnioModelo` no aplica) y cuando `SubLineaNegocio = "STE"` en Servicio Técnico (donde `Cantidad` y `PrecioUnit` no aplican), ¿el endpoint de Azure Functions espera recibir `""` (conforme a la Sección 3.4) o `0` / `0.00` (conforme a la Sección 8)?
7. **Registro de acuerdo en reunión:**
   - **Acuerdo final:** `[ PENDIENTE DE ACUERDO EN REUNIÓN ]`
   - **Responsable de validar:** `[ PENDIENTE ]`
   - **Estado:** `PENDIENTE`

---

## 4. Matriz Consolidada de Decisiones para Completar en la Reunión

| Código | Tema / Decisión | Servicio(s) Afectado(s) | Alternativas en Evaluación | Propuesta Técnica Preliminar *(No aprobada aún)* | Acuerdo Tomado en Reunión | Responsable | Estado |
| :---: | :--- | :---: | :--- | :--- | :--- | :--- | :---: |
| **D-01** | Alcance de líneas de negocio (`SP` vs `MC`/`ATV`/`OM`) | Ventas, Stock | **D-01A:** Solo `SP` y `STE`.<br>**D-01B:** Agregar módulo de unidades `MC`/`ATV`/`OM`. | **D-01A** (acotar primera salida productiva a `SP` y `STE`). | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **D-02** | Repuestos de una Orden de Trabajo en Ventas vs Servicio Técnico | Ventas, STE | **D-02A:** Solo en STE (`SubLineaNegocio = SP`).<br>**D-02B:** En STE y también en Ventas (`Canal = TALLER`). | **D-02A** por defecto (parametrizable), sujeto a confirmación de YMDP. | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **D-03** | Origen de `CostoVentaSinIGV` y `CostoVentaConIGV` | Stock | **D-03A:** `Producto.Costo` (compra).<br>**D-03B:** `Producto.PrecioVenta` (venta). | Confirmar con YMDP y verificar si montos en BD están sin IGV. | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **D-04** | Notas de Crédito y anulación de ventas ya enviadas | Ventas | **D-04A:** Reversión sintética.<br>**D-04B:** Guardar N° de NC al anular.<br>**D-04C:** Módulo completo de NC. | Omitir si se anula antes del envío; **D-04A** o **D-04B** si se anula post-envío. | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **D-05** | `TipoPago` (`CONTADO`/`CREDITO`) y `EntidadFinanciera` | Ventas | **D-05A:** Siempre `CONTADO`.<br>**D-05B:** `CREDITO DIRECTO` si hay saldo.<br>**D-05C:** Selector tabla 4.1. | **D-05A** (o **D-05B** si se desea reflejar crédito directo por saldo). | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **D-06** | Tratamiento de descuentos (`DescuentoPorcentaje`, `DescuentoSoles`, `Descuento`) | Ventas, STE | **D-06A:** Enviar `0.00`.<br>**D-06B:** Inferir vs catálogo.<br>**D-06C:** Persistir nuevas columnas. | **D-06A** (enviar `0.00` según Sección 4 y 6.2). | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **D-07** | Filtro de productos Yamaha en catálogo `Productos` | Stock, Ventas, STE | **D-07A:** Activos con SKU válido y sin marca ajena.<br>**D-07B:** Exigir `Marca == "YAMAHA"`. | **D-07A** (compatible con productos importados por Excel). | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **D-08** | Conversión del correlativo `OS-` al formato obligatorio `OT-` | STE | **D-08A:** Proyección no destructiva `OS-` $\rightarrow$ `OT-`.<br>**D-08B:** Renombrar en BD. | **D-08A** (cero impacto en BD, Kardex, UI e historial). | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **D-09** | Mapeo de `TipoAtencion` a `TipoServicio` (Sección 6.3) | STE | **D-09A:** Mapear los 4 valores actuales (`MP`, `R`, `G`, `SG`).<br>**D-09B:** Ampliar enum con `D`, `MP+R`, `RC`, `I`, `L`. | **D-09A** inmediato (y **D-09B** si el taller requiere los otros 5 códigos). | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |
| **AMB-3.4-8** | Serialización de `AnioModelo`, `Cantidad` y `PrecioUnit` cuando no aplican | Ventas, STE | **AMB-A:** Enviar `""` (Sec. 3.4).<br>**AMB-B:** Enviar `0` / `0.00` (Sec. 8). | Confirmar con YMDP qué espera su parser en Azure Functions. | `[ PENDIENTE ]` | `[ POR ASIGNAR ]` | `PENDIENTE` |

---

## 5. Riesgos que Impiden Habilitar Envíos Reales en Este Momento

Habilitar hoy el envío HTTP real hacia las URLs de la Sección 7 sin cerrar primero los puntos anteriores conlleva los siguientes riesgos bloqueantes:

1. **Riesgo de contaminación de la Carga Histórica Inicial de 3 Meses (Sección 3.1):**
   - La especificación advierte que el histórico de los últimos 3 meses en `Ventas` y `Servicio Técnico` se envía **una única vez en el primer envío**. Si el primer envío se dispara con una regla de negocio incorrecta (por ejemplo, duplicando repuestos de taller entre `Ventas` y `STE` por no haber cerrado `D-02`, o con el costo equivocado en `Stock` por no haber cerrado `D-03`), los datos históricos quedarán mal registrados en los sistemas de YMDP.
2. **Riesgo de rechazo de lotes en Servicio Técnico por datos históricos incompletos en `Vehiculo` y `Cliente`:**
   - En la base de datos del CRM, `Vehiculo.Placa`, `Vehiculo.NumeroSerieVIN` (`Chasis`) y `OrdenServicio.KilometrajeIngreso` son opcionales (`null`), pero en Yamaha v3 son **obligatorios** y con formato estricto (`Placa` con guión `-` entre 4 y 10 alfanuméricos; `Chasis` entre 15 y 20 caracteres alfanuméricos). Antes del primer envío real, se debe ejecutar el Dry-Run sobre los datos reales de los últimos 3 meses para identificar y completar cualquier vehículo Yamaha con placa o VIN faltante.
3. **Riesgo de rechazo por tipos de documento de clientes (`CE` y `DOC NO DOMICILIADO`):**
   - El enum [`TipoDocumentoCliente`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L3-L8) actual agrupa bajo `Otro` tanto el Carné de Extranjería (`CE`, exactamente 9 caracteres) como documentos extranjeros (`DOC NO DOMICILIADO`, que requiere envío explícito en la Sección 4.4 y 6.4).
4. **Falta de parámetros oficiales de YMDP:**
   - Aún no se cuenta con el `CodigoPuntoVenta` oficial asignado por YMDP a Team Benavides ni con la(s) `Function_Key` de autenticación.

---

## 6. Criterios Mínimos de Aceptación para Iniciar la Siguiente Fase (Fase B)

Para autorizar el inicio de la **Fase B (Implementación de Extractores de Base de Datos y Reglas de Dominio)** y posteriormente la **Fase C (Envío HTTP y Programación Automática)**, deben cumplirse los siguientes criterios verificables:

1. **Cierre de Acuerdos Internos:** Aprobación formal en el acta de reunión (Sección 4) de las decisiones internas **`D-01`**, **`D-04`**, **`D-05`**, **`D-06`**, **`D-07`**, **`D-08`** y **`D-09`**.
2. **Confirmación de Consultas a YMDP:** Respuesta técnica de Yamaha sobre **`D-02`** (repuestos de taller en Ventas vs STE), **`D-03`** (`Costo` vs `PrecioVenta` en Stock) y **`AMB-3.4-8`** (`""` vs `0`/`0.00` en campos numéricos condicionales), más la entrega del `CodigoPuntoVenta`.
3. **Validación Dry-Run con Datos Reales de QA (`0` Errores Bloqueantes):**
   - Los futuros extractores de `Ventas`, `Stock` y `Servicio Técnico` deberán conectarse primero exclusivamente a [`IYamahaDryRunService`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Yamaha/YamahaDryRunService.cs#L45-L67) en la base `crm_teambenavides_qa`, verificando que el lote extraído produzca `EsLoteValido == true` y `TotalErroresBloqueantes == 0`.
4. **Cero Regresiones en el CRM:**
   - `dotnet build` y `dotnet test` con 0 errores y 0 advertencias, manteniendo intactos los flujos de Mostrador, Taller, Inventario, Compras, Caja Chica e Importador Excel.

---

## 7. Próximos Pasos y Responsables por Asignar

| # | Acción / Paso Siguiente | Entregable Esperado | Responsable Sugerido | Fecha Objetivo | Estado |
| :---: | :--- | :--- | :--- | :---: | :---: |
| **1** | Realizar la reunión técnica/funcional utilizando la Matriz de la Sección 4 de este documento. | Decisiones internas `D-01`, `D-04`, `D-05`, `D-06`, `D-07`, `D-08` y `D-09` acordadas. | Ingeniero Responsable / Gerencia Team Benavides | `[ POR DEFINIR ]` | `PENDIENTE` |
| **2** | Remitir a Yamaha Motor del Perú (YMDP) las 3 consultas puntuales (`D-02`, `D-03`, `AMB-3.4-8`) y solicitar `CodigoPuntoVenta` y `Function_Key`. | Correo/confirmación oficial de YMDP y credenciales de integración. | Contacto Técnico / Comercial ante YMDP | `[ POR DEFINIR ]` | `PENDIENTE` |
| **3** | Implementar en rama `develop` (bajo autorización previa) los extractores de lectura de BD hacia el servicio Dry-Run según los acuerdos de la reunión. | Extractores de `Stock`, `Servicio Técnico` y `Ventas` probados en modo Dry-Run sobre `crm_teambenavides_qa`. | Backend (`Paolo`) | `[ POR DEFINIR ]` | `PENDIENTE` |
| **4** | Ejecutar auditoría Dry-Run sobre los últimos 3 meses de datos de Team Benavides para depurar placas, chasis (`VIN`) o códigos con formato inválido. | Reporte de registros históricos corregidos con `0` errores de validación local. | Operaciones / Administración Taller y Almacén | `[ POR DEFINIR ]` | `PENDIENTE` |
| **5** | Implementar el envío HTTP real, control de marca de agua (`WatermarkUtc`) y programación automática (`BackgroundService`), y ejecutar la carga inicial controlada. | Primer envío histórico (`HTTP 200 OK`) y sincronización periódica activa. | Backend (`Paolo`) / Ingeniero Responsable | `[ POR DEFINIR ]` | `PENDIENTE` |
