# Plan Maestro de Implementación: Integración de Servicios de Datos Yamaha Motor del Perú S.A. (v3)

- **Proyecto:** CRM Team Benavides (`CRMTeamBenavides.Api` — .NET 10 / EF Core / PostgreSQL)
- **Rama base:** `develop` (HEAD en `0b1b38b`)
- **Documentos de referencia:**
  - *YAMAHA MOTOR DEL PERÚ S.A. — Especificación Técnica de Servicios de Datos | Contrato de Datos para Concesionarios Autorizados | v3*
  - [`docs/auditoria-yamaha-v3.md`](file:///d:/Projects/CRMTeamBenavides/docs/auditoria-yamaha-v3.md)
- **Naturaleza del documento:** Planificación técnica y funcional previa a la implementación. **Ningún cambio en código, esquema de base de datos, contratos o configuración ha sido ejecutado en esta etapa.**

---

## 1. Matriz Completa de Requisitos y Criterios de Aceptación Verificables

Cada fila enlaza la **sección exacta de la especificación Yamaha v3**, el **campo o regla**, su **estado actual**, la **evidencia en el código del CRM**, la **brecha** y el **criterio de aceptación verificable** para darlo por cumplido en QA.

### 1.1 Servicio 1 — Ventas (Sección 4 de la Especificación)

| ID | Sección Spec | Campo / Regla | Estado Actual | Evidencia en el Código del CRM | Brecha Identificada | Criterio de Aceptación Verificable |
| :---: | :---: | :--- | :---: | :--- | :--- | :--- |
| **V-01** | Sec. 4 | `RazonSocial` (`VARCHAR`, Obligatorio) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L14) (`ConfiguracionEmpresa.RazonSocial`); [`ConfiguracionService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Configuracion/ConfiguracionService.cs#L19-L37) | Existe en BD pero no se proyecta al JSON de Ventas. | El JSON serializado contiene `"RazonSocial"` no vacío, `<= 255` caracteres, idéntico a `ConfiguracionEmpresa.RazonSocial`. |
| **V-02** | Sec. 4 | `RUC` (`VARCHAR(11)`, Obligatorio) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L15) (`ConfiguracionEmpresa.Ruc`) | `Ruc` es anulable (`string?`) y tiene valor semilla `"20000000001"`. | El validador rechaza el lote si `RUC` no coincide con `^\d{11}$` (o si sigue en el valor semilla en entorno productivo). |
| **V-03** | Sec. 4 | `CodigoPuntoVenta` (`VARCHAR`, Obligatorio) | `FALTA` | [`YamahaSettings.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaSettings.cs#L8-L32); [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L11-L24) | No existe en configuración ni en BD. | El JSON incluye `"CodigoPuntoVenta"` no vacío leído desde configuración; si falta, el validador pre-envío bloquea la salida con error explícito. |
| **V-04** | Sec. 4 | `NombreComercialPuntoVenta` (`VARCHAR`, Opcional) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L13) (`ConfiguracionEmpresa.NombreEmpresa`) | No se proyecta al DTO de Ventas. | El JSON incluye `"NombreComercialPuntoVenta"` (`<= 255` chars) desde `YamahaSettings` o `ConfiguracionEmpresa.NombreEmpresa` (o `""` si no se configura). |
| **V-05** | Sec. 4 / Sec. 8 | `VIN` (`VARCHAR(20)`, Condicional: obligatorio en `MC`, `ATV`, `OM`) | `FALTA` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L90-L115) (`DetalleVenta`); [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto`) | `DetalleVenta` y `Producto` no almacenan `VIN`. | Para `LineaNegocio == "SP"`, se serializa `"VIN": ""`. Si se reporta `MC`/`ATV`/`OM`, cumple `^[A-Za-z0-9-]{1,20}$` y `VIN != CodigoYamaha`. |
| **V-06** | Sec. 4 | `Modelo` (`VARCHAR`, Condicional: obligatorio en `MC`, `ATV`, `OM`; no aplica en `SP`) | `FALTA` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto`) | `Producto` no tiene campo `Modelo` de unidad. | Para `LineaNegocio == "SP"`, se serializa `"Modelo": ""`. En `MC`/`ATV`/`OM`, es obligatorio no vacío. |
| **V-07** | Sec. 4 | `Color` (`VARCHAR`, Opcional) | `FALTA` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto`) | No existe en `Producto` ni `DetalleVenta`. | Para `SP` se serializa `"Color": ""`. Si tiene valor, el validador verifica que no sea puramente numérico (`!\d+`). |
| **V-08** | Sec. 4 / Sec. 3.4 | `AnioModelo` (`INT`, Condicional: obligatorio en `MC`, `ATV`; no aplica en `OM`, `SP`) | `FALTA` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto`) | No existe en `Producto` ni `DetalleVenta`; ambigüedad en spec entre Sec. 3.4 (`""`) y Sec. 8 (`0`) para `INT` condicional cuando no aplica. | En `SP`/`OM` se serializa según la regla confirmada con Yamaha (`0` o `""`); en `MC`/`ATV` es un año entero válido (ej. `2024`–`2027`). |
| **V-09** | Sec. 4 / Sec. 8 | `FechaFacturacion` (`DATE`, Obligatorio, `DD/MM/YYYY`) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L117-L139) (`Comprobante.FechaCreacion`); [`HoraPeru.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Common/HoraPeru.cs#L12-L15) | `Comprobante.FechaCreacion` está en UTC y sin formatear. | Se convierte con `HoraPeru.DesdeUtc(comprobante.FechaCreacion)` y se serializa con formato exacto `dd/MM/yyyy` (`^\d{2}/\d{2}/\d{4}$`). |
| **V-10** | Sec. 4 | `NumeroComprobante` (`VARCHAR`, Obligatorio) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L122-L123) (`Comprobante.Serie`, `Comprobante.Numero`); [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1356-L1367) | Almacenados en dos columnas separadas (`Serie` y `Numero`). | Se serializa como `"{Serie}-{Numero}"` sin espacios internos (ej. `"B001-000001"`). |
| **V-11** | Sec. 4 / Sec. 4.3 | `TipoDocumentoVenta` (`MODIFICADO`, `VARCHAR`, Obligatorio) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L6-L10) (`TipoComprobante`: `Boleta`, `Factura`); [`Comprobante.Tipo`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L121) | `Comprobante.Tipo` guarda `"Boleta"` o `"Factura"`. Faltan literales exactos de Sec. 4.3 y soporte de Notas de Crédito/Débito. | Solo se emiten valores del conjunto `{"FACTURA", "BOLETA DE VENTA", "NOTA DE CREDITO", "NOTA DE DEBITO"}`. `"Boleta"` se transforma siempre en `"BOLETA DE VENTA"`. |
| **V-12** | Sec. 4 / Sec. 4.1 | `TipoPago` (`VARCHAR`, Obligatorio: `CONTADO` / `CREDITO`) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L22-L55) (`MetodoPago`, `Pago`); [`MetodosPagoSeeder.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Data/Seed/MetodosPagoSeeder.cs#L16-L21) | No existe propiedad explícita `TipoPago` (`CONTADO`/`CREDITO`). | El JSON contiene exclusivamente `"CONTADO"` o `"CREDITO"` según la regla de negocio aprobada. |
| **V-13** | Sec. 4 / Sec. 4.1 | `EntidadFinanciera` (`VARCHAR`, Opcional/Condicional) | `FALTA` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L57-L88) (`Venta`, `Comprobante`) | No se almacena entidad financiera. | Cuando `TipoPago == "CONTADO"`, se serializa `"EntidadFinanciera": "CONTADO"`. Cuando `TipoPago == "CREDITO"`, contiene una entidad no vacía según tabla 4.1. |
| **V-14** | Sec. 4 / Sec. 8 | `MontoSinIGV` (`DECIMAL(10,2)`, Obligatorio) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L109) (`DetalleVenta.SubtotalGravado`); [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L596-L621) | Falta proyección con 2 decimales y punto decimal, más signo negativo en Notas de Crédito. | Se serializa con punto decimal (`.`) y 2 decimales; positivo en facturas/boletas/ND y negativo en `NOTA DE CREDITO`. |
| **V-15** | Sec. 4 / Sec. 8 | `MontoConIGV` (`DECIMAL(10,2)`, Obligatorio) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L114) (`DetalleVenta.Total`) | Falta proyección al JSON y regla de signo en Notas de Crédito. | Igual a `DetalleVenta.Total` con 2 decimales y separador `.`; negativo cuando `TipoDocumentoVenta == "NOTA DE CREDITO"`. |
| **V-16** | Sec. 4 | `Canal` (`VARCHAR`, Opcional: `MOSTRADOR` / `TALLER`) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L60) (`Venta.OrdenServicioId`) | No existe columna `Canal`, pero existe `OrdenServicioId`. | Se deriva como `"TALLER"` si `OrdenServicioId.HasValue` y `"MOSTRADOR"` si es `null`. |
| **V-17** | Sec. 3.3 / Sec. 4 | `LineaNegocio` (`VARCHAR`, Obligatorio: `MC` / `ATV` / `OM` / `SP`) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L94) (`DetalleVenta.TipoItem`) | `Producto` no clasifica línea de negocio (`MC`/`ATV`/`OM`/`SP`). | Todo registro de repuesto lleva `"LineaNegocio": "SP"`. Ningún registro de Ventas lleva `"STE"` ni un valor fuera de `{"MC", "ATV", "OM", "SP"}`. |
| **V-18** | Sec. 4 | `TipoPuntoVenta` (`VARCHAR`, Opcional, Valor fijo: `3S`) | `FALTA` | [`YamahaSettings.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaSettings.cs#L8-L32) | No configurado ni proyectado. | El JSON incluye `"TipoPuntoVenta": "3S"`. |
| **V-19** | Sec. 4 | `NumeroMotor` (`VARCHAR`, Condicional: obligatorio en `MC`, `ATV`; no aplica en `OM`, `SP`) | `FALTA` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto`) | No existe en `Producto` ni `DetalleVenta`. | En `SP` y `OM` se serializa `"NumeroMotor": ""`. En `MC` y `ATV` es obligatorio no vacío. |
| **V-20** | Sec. 4 | `Moneda` (`VARCHAR(3)`, Opcional: `PEN` / `USD`) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L19) (`ConfiguracionEmpresa.MonedaBase`) | No se proyecta al JSON de Ventas. | Se serializa `"Moneda": "PEN"` (o `"USD"` si aplica), validando pertenencia a `{"PEN", "USD"}`. |
| **V-21** | Sec. 4 / Sec. 4.2 | `PrecioFinal` (`DECIMAL(10,2)`, Obligatorio) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L114) (`DetalleVenta.Total`) | Falta proyección al JSON y regla de signo negativo en Notas de Crédito. | Igual al importe final con IGV pagado por la línea (`DetalleVenta.Total`), con 2 decimales y negativo en `NOTA DE CREDITO`. |
| **V-22** | Sec. 4 / Sec. 8 | `CodigoYamaha` (`VARCHAR`, Condicional: obligatorio en `SP`) | `PARCIAL` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L16) (`Producto.Codigo`) | `Producto.Codigo` permite espacios o caracteres no restringidos por regex. | Para `SP`, `CodigoYamaha` es obligatorio, cumple `^[A-Za-z0-9-]+$` sin espacios y es distinto de `VIN`. |
| **V-23** | Sec. 4 | `Descripcion` (`VARCHAR`, Condicional: obligatorio en `SP`) | `PARCIAL` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L17-L18) (`Producto.Nombre`, `Producto.Descripcion`) | Falta mapeo y recorte preventivo a 255 caracteres. | Para `SP`, contiene `Producto.Nombre` no vacío (`<= 255` caracteres). |
| **V-24** | Sec. 4 / Sec. 4.2 | `CantidadVendida` (`INT`, Obligatorio) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L105) (`DetalleVenta.Cantidad`) | Falta proyección y signo negativo en Notas de Crédito. | Entero `> 0` en ventas normales y `< 0` en `NOTA DE CREDITO`. |
| **V-25** | Sec. 4 / Sec. 8 | `DescuentoPorcentaje` (`DECIMAL(5,2)`, Opcional: `0.00` si no aplica) | `FALTA` | [`VentaContracts.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Ventas/VentaContracts.cs#L15); [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L90-L115) | `DetalleVenta` no persiste el descuento. | Se serializa como `0.00` cuando no hay descuento (nunca vacío ni `null`). |
| **V-26** | Sec. 4 / Sec. 8 | `DescuentoSoles` (`DECIMAL(10,2)`, Opcional: `0.00` si no aplica) | `FALTA` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L90-L115) (`DetalleVenta`) | `DetalleVenta` no persiste el descuento en soles. | Se serializa como `0.00` cuando no hay descuento (nunca vacío ni `null`). |
| **V-27** | Sec. 4 | `PrecioUnitarioConIGV` (`DECIMAL(10,2)`, Opcional) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L106-L110) (`DetalleVenta.PrecioUnitario`, `PorcentajeIgvAplicado`) | `DetalleVenta.PrecioUnitario` se almacena sin IGV. | Se calcula como `Math.Round(PrecioUnitario * (1 + PorcentajeIgvAplicado / 100m), 2, MidpointRounding.AwayFromZero)` con 2 decimales y punto decimal. |
| **V-28** | Sec. 4 | `TipoCliente` (`VARCHAR`, Opcional: `CLIENTE FINAL` / `TALLERISTA` / `MINORISTA` / `CORPORATIVO`) | `FALTA` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L10-L36) (`Cliente`) | No existe en `Cliente`. | Se envía `""` o un valor dentro del conjunto permitido (`CLIENTE FINAL`, `TALLERISTA`, `MINORISTA`, `CORPORATIVO`). |
| **V-29** | Sec. 4 | `NombreCompletoCliente` (`VARCHAR`, Opcional) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L12-L13) (`Cliente.NombreCompleto`, `Cliente.RazonSocial`) | Falta proyección al DTO de Ventas. | Se serializa `Cliente.NombreCompleto` (`<= 255` caracteres). |
| **V-30** | Sec. 4 / Sec. 4.4 | `TipoDocumentoCliente` (`MODIFICADO`, `VARCHAR`, Opcional) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L3-L8) (`TipoDocumentoCliente`: `DNI`, `RUC`, `Otro`) | El enum actual no separa `CE` de `DOC NO DOMICILIADO`. | Solo emite valores en `{"DNI", "CE", "RUC", "DOC NO DOMICILIADO", ""}` coherentes con `NumeroDocumentoCliente`. |
| **V-31** | Sec. 4 / Sec. 4.4 | `NumeroDocumentoCliente` (`VARCHAR`, Obligatorio) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L16) (`Cliente.NumeroDocumento`); [`ClienteService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Clientes/ClienteService.cs#L249-L278) | Para `Otro` permite 4–20 caracteres sin validar 9 dígitos de `CE`. | Obligatorio no vacío; si `TipoDocumentoCliente` es `DNI` tiene 8 dígitos, si es `CE` tiene 9 dígitos, si es `RUC` tiene 11 dígitos. |
| **V-32** | Sec. 4 | `CanalContactoCliente` (`VARCHAR`, Opcional: `PRESENCIAL` / `WEB` / `TELEFÓNICO`) | `FALTA` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L10-L36) | No se almacena en `Cliente` ni en `Venta`. | Se serializa `""` (o `"PRESENCIAL"` si se define por defecto). |
| **V-33** | Sec. 4 | `DistritoCliente` (`VARCHAR`, Opcional) | `FALTA` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L20) (`Cliente.Direccion`) | No existe campo `Distrito` estructurado. | Se serializa `""`. |
| **V-34** | Sec. 4 | `CiudadCliente` (`VARCHAR`, Opcional) | `FALTA` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L20) | No existe campo `Ciudad` estructurado. | Se serializa `""`. |
| **V-35** | Sec. 4 | `DepartamentoCliente` (`VARCHAR`, Opcional) | `FALTA` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L20) | No existe campo `Departamento` estructurado. | Se serializa `""`. |
| **V-36** | Sec. 4 | `DireccionCliente` (`VARCHAR`, Opcional) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L20) (`Cliente.Direccion`) | Falta proyección al DTO (`null` -> `""`). | Se serializa `Cliente.Direccion ?? ""` (`<= 255` caracteres). |
| **V-37** | Sec. 4 | `MotoActualCliente` (`VARCHAR`, Opcional) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L29) (`Cliente.Vehiculos`) | No existe propiedad directa en `Cliente`. | Se serializa `""` (o el `Modelo` del vehículo activo del cliente si existe). |
| **V-38** | Sec. 4 | `CelularCliente` (`VARCHAR`, Opcional: 9 dígitos sin espacios) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L17) (`Cliente.Telefono`) | `Telefono` acepta espacios, guiones, `+51` o fijos. | Tras sanitizar espacios/guiones/prefijo `+51`, si cumple `^\d{9}$` se envía; si no, se envía `""`. |
| **V-39** | Sec. 4 | `CorreoCliente` (`VARCHAR`, Opcional) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L19) (`Cliente.Email`) | `Email` no se valida al guardar en `ClienteService`. | Si `Cliente.Email` tiene formato de correo válido se envía; si es nulo o inválido se envía `""`. |
| **R-V1** | Sec. 4.2 | Regla de Signo en Notas de Crédito | `FALTA` | [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1111-L1249) (`AnularAsync`, `AnularComprobanteAsync`) | Las anulaciones mutan estado sin emitir Nota de Crédito ni invertir signos. | Todo registro con `TipoDocumentoVenta == "NOTA DE CREDITO"` tiene `MontoSinIGV < 0`, `MontoConIGV < 0`, `PrecioFinal < 0` y `CantidadVendida < 0`. |

---

### 1.2 Servicio 2 — Stock (Sección 5 de la Especificación)

| ID | Sección Spec | Campo / Regla | Estado Actual | Evidencia en el Código del CRM | Brecha Identificada | Criterio de Aceptación Verificable |
| :---: | :---: | :--- | :---: | :--- | :--- | :--- |
| **S-01** | Sec. 5.3 | `RazonSocial` (`VARCHAR`, Obligatorio) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L14) (`ConfiguracionEmpresa.RazonSocial`) | Falta proyección al DTO de Stock. | Presente y no vacío en cada fila del JSON de Stock. |
| **S-02** | Sec. 5.3 | `RUC` (`VARCHAR(11)`, Obligatorio) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L15) (`ConfiguracionEmpresa.Ruc`) | Falta validación de 11 dígitos y proyección. | Cumple `^\d{11}$` en cada fila del JSON de Stock. |
| **S-03** | Sec. 5.3 | `CodigoPuntoVenta` (`VARCHAR`, Obligatorio) | `FALTA` | [`YamahaSettings.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaSettings.cs#L8-L32) | No existe en configuración ni en BD. | Presente y no vacío en cada fila del JSON de Stock. |
| **S-04** | Sec. 5.3 | `NombreComercialPuntoVenta` (`VARCHAR`, Opcional) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L13) (`ConfiguracionEmpresa.NombreEmpresa`) | Falta proyección al DTO de Stock. | Presente como string (`<= 255` chars) en el JSON de Stock. |
| **S-05** | Sec. 5.3 | `LineaNegocio` (`VARCHAR`, Obligatorio: `MC` / `ATV` / `OM` / `SP`) | `PARCIAL` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto`) | `Producto` no tiene campo `LineaNegocio`. | En productos de inventario de repuestos se serializa `"LineaNegocio": "SP"`. |
| **S-06** | Sec. 5.3 / Sec. 8 | `CodigoYamaha` (`VARCHAR`, Obligatorio) | `PARCIAL` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L16) (`Producto.Codigo`) | Puede haber productos con espacios en `Codigo` o de otras marcas. | Cumple `^[A-Za-z0-9-]+$`, no vacío y `CodigoYamaha != VIN`. |
| **S-07** | Sec. 5.3 | `Descripcion` (`VARCHAR`, Obligatorio) | `PARCIAL` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L17) (`Producto.Nombre`) | Falta proyección al DTO de Stock. | Contiene `Producto.Nombre` no vacío (`<= 255` chars). |
| **S-08** | Sec. 5.1 / 5.2 / 5.3 | `StockDisponible` (`MODIFICADO`, `INT`, Obligatorio) | `PARCIAL` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L30) (`Producto.StockActual`) | Falta proyección y filtro por línea de negocio. | Para `SP`, entero `>= 0` igual a `Producto.StockActual`. Para `MC`/`ATV`/`OM`, entero `> 0` (filas con `0` se excluyen). |
| **S-09** | Sec. 5.3 | `CostoVentaSinIGV` (`DECIMAL(10,2)`, Obligatorio) | `PARCIAL` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L28-L29) (`Producto.Costo`, `Producto.PrecioVenta`) | Ambigüedad de negocio (`Costo` vs `PrecioVenta`) y falta proyección. | Formateado a 2 decimales con separador `.` (`>= 0.00`). |
| **S-10** | Sec. 5.3 | `CostoVentaConIGV` (`DECIMAL(10,2)`, Obligatorio) | `PARCIAL` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L28-L29); [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L18) (`PorcentajeIgv`) | No se almacena con IGV; debe calcularse. | Igual a `Math.Round(CostoVentaSinIGV * (1 + PorcentajeIgv / 100m), 2, MidpointRounding.AwayFromZero)`. |
| **S-11** | Sec. 5.3 | `FechaIngreso` (`DATE`, Opcional, `DD/MM/YYYY`) | `PARCIAL` | [`Compras.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Compras.cs#L40) (`Compra.FechaEmision`); [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L56-L81) (`MovimientoInventario`) | No está en `Producto`; se obtiene de la última compra/entrada registrada. | Si el producto tiene compra/entrada registrada, se formatea como `dd/MM/yyyy`; si no tiene entradas previas, se serializa `""`. |
| **S-12** | Sec. 5.3 | `VIN` (`VARCHAR(20)`, Condicional: obligatorio en `MC`, `ATV`, `OM`) | `FALTA` | [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) (`Producto`) | `Producto` solo modela ítems por cantidad (`SP`). | Para `LineaNegocio == "SP"`, se serializa `"VIN": ""`. |
| **R-S1** | Sec. 5.3 | Exclusión de campos eliminados (`SitioDealer`, `FechaVenta`) | `PARCIAL` | [`YamahaContracts.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaContracts.cs#L1-L30) | No existe DTO aún. | El JSON de Stock tiene exactamente 12 propiedades por elemento y jamás incluye `SitioDealer` ni `FechaVenta`. |

---

### 1.3 Servicio 3 — Servicio Técnico (Sección 6 de la Especificación)

| ID | Sección Spec | Campo / Regla | Estado Actual | Evidencia en el Código del CRM | Brecha Identificada | Criterio de Aceptación Verificable |
| :---: | :---: | :--- | :---: | :--- | :--- | :--- |
| **ST-01** | Sec. 6.2 | `LineaNegocio` (`VARCHAR`, Obligatorio, Valor fijo: `STE`) | `FALTA` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L79-L154) (`OrdenServicio`) | Falta DTO de Servicio Técnico. | Todas las filas del payload tienen `"LineaNegocio": "STE"`. |
| **ST-02** | Sec. 6.1 / 6.2 | `SubLineaNegocio` (`MODIFICADO`, `VARCHAR`, Obligatorio: `SP` / `STE`) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L34-L40) (`TipoItemServicio`); [`DetalleServicio.TipoItem`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L160) | `DetalleServicio.TipoItem` tiene 4 valores (`Repuesto`, `Servicio`, `ManoDeObra`, `Terceros`). | Ítems `Repuesto` llevan `"SubLineaNegocio": "SP"`; ítems de mano de obra/servicio llevan `"SubLineaNegocio": "STE"`. |
| **ST-03** | Sec. 6.2 | `RazonSocial` (`VARCHAR`, Obligatorio) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L14) | Falta proyección al DTO de Servicio Técnico. | Presente y no vacío en todas las filas. |
| **ST-04** | Sec. 6.1 / 6.2 | `RUC` (`VARCHAR(11)`, Obligatorio) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L15) | Falta validación de 11 dígitos y proyección. | Cumple `^\d{11}$` y es idéntico en todas las líneas de una misma `NumeroOrdenTrabajo`. |
| **ST-05** | Sec. 6.2 | `NombreComercialPuntoVenta` (`VARCHAR`, Opcional) | `PARCIAL` | [`Configuracion.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L13) | Falta proyección al DTO. | Presente como string (`<= 255` chars). |
| **ST-06** | Sec. 6.2 | `CodigoPuntoVenta` (`VARCHAR`, Obligatorio) | `FALTA` | [`YamahaSettings.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaSettings.cs#L8-L32) | No existe en configuración ni en BD. | Presente y no vacío en todas las filas. |
| **ST-07** | Sec. 6.2 | `NumeroOrdenTrabajo` (`VARCHAR`, Obligatorio: debe iniciar con `OT-` seguido de números) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L89) (`OrdenServicio.NumeroOrden`); [`OrdenServicioService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Taller/OrdenServicioService.cs#L33-L39) | El CRM genera `OS-000001` (`$"OS-{seqVal:D6}"`). Yamaha rechaza cualquier formato que no inicie con `OT-` seguido de números. | El valor exportado cumple `^OT-\d+(-\d+)?$` (ej. `OT-000001`) y todas las líneas de la misma orden comparten exactamente el mismo `NumeroOrdenTrabajo`. |
| **ST-08** | Sec. 6.1 / 6.2 | `FechaFacturacion` (`MODIFICADO`, `DATE`, Obligatorio, `DD/MM/YYYY`) | `PARCIAL` | [`Ventas.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L135) (`Comprobante.OrdenServicioId`); [`VentaService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1360-L1378) | La fecha de facturación está en `Comprobante.FechaCreacion`, no en `OrdenServicio`. | Se obtiene de `HoraPeru.DesdeUtc(comprobante.FechaCreacion).ToString("dd/MM/yyyy")` del comprobante activo emitido y es idéntica en todas las líneas de la misma OT. |
| **ST-09** | Sec. 6.2 / Sec. 8 | `Estado` (`MODIFICADO`, `VARCHAR`, Obligatorio, Valor fijo: `FACTURADO`) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L3-L12) (`EstadoOrdenServicio`) | `OrdenServicio.Estado` usa estados operativos de taller, no `FACTURADO`. | Solo se exportan órdenes que tienen un `Comprobante` con `Estado == "Emitido"`, y el JSON lleva siempre `"Estado": "FACTURADO"`. |
| **ST-10** | Sec. 6.2 / 6.4 | `TipoDocumentoCliente` (`MODIFICADO`, `VARCHAR`, Opcional) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L3-L8) (`TipoDocumentoCliente`) | Falta distinguir `CE` y `DOC NO DOMICILIADO`. | Si el cliente es extranjero no domiciliado, se envía explícitamente `"DOC NO DOMICILIADO"`; para nacionales se envía `"DNI"`, `"CE"` o `"RUC"`. |
| **ST-11** | Sec. 6.1 / 6.2 / 6.4 | `NumeroDocumentoCliente` (`MODIFICADO`, `VARCHAR`, Obligatorio) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L16) (`Cliente.NumeroDocumento`) | Falta validación de longitud para `CE` (9 dígitos) y verificación de consistencia entre líneas. | Cumple longitud según tipo de documento y es idéntico en todas las líneas de la misma OT. |
| **ST-12** | Sec. 6.1 / 6.2 | `NombreCliente` (`MODIFICADO`, `VARCHAR`, Obligatorio) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L12) (`Cliente.NombreCompleto`) | En Servicio Técnico la propiedad se llama `NombreCliente`. | No vacío (`<= 255` chars) e idéntico en todas las líneas de la misma OT. |
| **ST-13** | Sec. 6.2 | `Celular` (`VARCHAR`, Opcional: 9 dígitos sin espacios) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L17) (`Cliente.Telefono`) | Puede contener espacios, guiones o prefijo `+51`. | Si tras sanitizar cumple `^\d{9}$`, se envía; de lo contrario se envía `""`. |
| **ST-14** | Sec. 6.2 | `Correo` (`VARCHAR`, Opcional: formato email válido) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L19) (`Cliente.Email`) | No se valida formato de email al registrar cliente. | Si es un email válido se envía; si es nulo o inválido se envía `""`. |
| **ST-15** | Sec. 6.1 / 6.2 | `Placa` (`MODIFICADO`, `VARCHAR`, Obligatorio: 4–10 alfanuméricos + guión `-`, sin espacios ni `--`) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L58) (`Vehiculo.Placa`); [`VehiculoService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Vehiculos/VehiculoService.cs#L275-L283) | `Vehiculo.Placa` es anulable y `NormalizarPlaca` no exige guión (`-`). | No vacío, sin espacios, distinto de `--`, contiene `-` y entre 4 y 10 caracteres alfanuméricos, idéntico en todas las líneas de la OT. |
| **ST-16** | Sec. 6.1 / 6.2 / Sec. 8 | `Chasis` (`MODIFICADO`, `VARCHAR`, Obligatorio: 15–20 alfanuméricos y `-`, `!= CodigoYamaha`) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L63) (`Vehiculo.NumeroSerieVIN`) | `NumeroSerieVIN` es anulable y admite hasta 50 caracteres sin validar rango 15–20. | Cumple `^[A-Za-z0-9-]{15,20}$`, `Chasis != CodigoYamaha`, e idéntico en todas las líneas de la OT. |
| **ST-17** | Sec. 6.2 | `Marca` (`MODIFICADO`, `VARCHAR`, Obligatorio, Valor fijo: `YAMAHA`) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L60) (`Vehiculo.Marca`) | `Vehiculo.Marca` es texto libre (pueden ingresar motos de otras marcas al taller). | Solo se seleccionan órdenes de vehículos marca Yamaha y se serializa siempre `"Marca": "YAMAHA"`. |
| **ST-18** | Sec. 6.2 | `Modelo` (`VARCHAR`, Opcional) | `PARCIAL` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L61) (`Vehiculo.Modelo`) | Falta proyección al DTO. | Se serializa `Vehiculo.Modelo ?? ""` (`<= 255` chars). |
| **ST-19** | Sec. 6.2 | `Version` (`VARCHAR`, Opcional) | `FALTA` | [`Clientes.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L53-L80) (`Vehiculo`) | No existe propiedad `Version` en `Vehiculo`. | Se serializa `"Version": ""` (o el valor si se agrega opcionalmente). |
| **ST-20** | Sec. 6.2 | `Kilometraje` (`MODIFICADO`, `INT`, Obligatorio) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L107-L109) (`OrdenServicio.KilometrajeIngreso`, `LecturaMedidorIngreso`) | `KilometrajeIngreso` es anulable (`int?`). | Se serializa siempre como un entero (`INT >= 0`), nunca `null`. |
| **ST-21** | Sec. 6.2 / 6.3 | `TipoServicio` (`VARCHAR`, Obligatorio: `MP`, `D`, `R`, `MP+R`, `RC`, `G`, `I`, `L`, `SG`, `-`) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L14-L20) (`TipoAtencion`) | `TipoAtencion` solo tiene 4 valores (`MP`, `R`, `G`, `SG`). Faltan `D`, `MP+R`, `RC`, `I`, `L`, `-`. | Pertenece estrictamente a los 10 códigos de la sección 6.3 y nunca es `"-"` cuando `SubLineaNegocio == "STE"`. |
| **ST-22** | Sec. 6.2 / 6.3 | `Actividad` (`VARCHAR`, Condicional: obligatorio salvo cuando `TipoServicio == "-"`) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L163) (`DetalleServicio.Descripcion`) | `DetalleServicio.Descripcion` admite hasta 300 chars; falta mapeo condicional. | Si `TipoServicio == "-"`, se serializa `""`; en cualquier otro caso es un string no vacío `<= 255` caracteres. |
| **ST-23** | Sec. 6.2 / Sec. 8 | `CodigoYamaha` (`VARCHAR`, Condicional: obligatorio en `SubLineaNegocio == "SP"`, máx. 50 chars, `!= Chasis`) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L161-L162) (`DetalleServicio.Producto`); [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L16) | Falta proyección condicional y validación `!= Chasis`. | Para `SubLineaNegocio == "SP"`, cumple `^[A-Za-z0-9-]{1,50}$` y `CodigoYamaha != Chasis`. Para `SubLineaNegocio == "STE"`, se serializa `""`. |
| **ST-24** | Sec. 6.2 | `Descripcion` (`VARCHAR`, Condicional: obligatorio en `SubLineaNegocio == "SP"`) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L163); [`Inventario.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L17) | Falta proyección condicional. | Para `SubLineaNegocio == "SP"`, contiene el nombre del repuesto (`<= 255` chars); para `"STE"` se serializa `""`. |
| **ST-25** | Sec. 6.2 | `Cantidad` (`NUEVO`, `INT`, Condicional: obligatorio en `SP`, no aplica en `STE`) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L164) (`DetalleServicio.Cantidad`) | Ambigüedad entre Sec. 3.4 (`""` cuando no aplica) y tipo `INT` (Sec. 8: `0` cuando es numérico). | Para `SP`, entero `> 0`. Para `STE`, se serializa según confirmación de Yamaha (`""` o `0`). |
| **ST-26** | Sec. 6.2 | `MontoSinIGV` (`NUEVO`, `DECIMAL(10,2)`, Obligatorio) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L169) (`DetalleServicio.SubtotalGravado`) | Falta proyección al nuevo campo unificado `MontoSinIGV`. | Presente en líneas `SP` y `STE` con 2 decimales y separador `.`. |
| **ST-27** | Sec. 6.2 | `MontoConIGV` (`NUEVO`, `DECIMAL(10,2)`, Obligatorio) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L174) (`DetalleServicio.Total`) | Falta proyección al nuevo campo `MontoConIGV`. | Presente en líneas `SP` y `STE` con 2 decimales y separador `.`. |
| **ST-28** | Sec. 6.2 | `PrecioUnit` (`NUEVO`, `DECIMAL(10,2)`, Opcional/Condicional: solo aplica en `SP`) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L165) (`DetalleServicio.PrecioUnitario`) | Falta proyección condicional para `SP`. | Para `SP`, precio unitario del repuesto con 2 decimales; para `STE`, `0.00` (o `""` según confirmación de Yamaha). |
| **ST-29** | Sec. 6.2 / Sec. 8 | `Descuento` (`DECIMAL(5,2)`, Opcional: `0.00` si no aplica) | `FALTA` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L156-L194) (`DetalleServicio`) | No se persiste porcentaje de descuento en `DetalleServicio`. | Se serializa `0.00` cuando no aplica descuento (nunca vacío ni `null`). |
| **ST-30** | Sec. 6.2 | `TotalConIGV` (`DECIMAL(10,2)`, Obligatorio) | `PARCIAL` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L174) (`DetalleServicio.Total`) | Falta proyección a `TotalConIGV`. | Igual al monto total con IGV de la línea (`DetalleServicio.Total`) con 2 decimales. |
| **R-ST1** | Sec. 6.1 | Consistencia de 6 campos entre líneas de la misma OT y exclusión de 5 campos eliminados | `FALTA` | [`Taller.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L79-L194) | Falta validador de lote por OT y DTO sin campos obsoletos. | Todas las filas con igual `NumeroOrdenTrabajo` tienen idéntico `RUC`, `Chasis`, `Placa`, `NombreCliente`, `NumeroDocumentoCliente` y `FechaFacturacion`, y el JSON no contiene `FechaApertura`, `FechaCierre`, `MOSinIGV`, `RepuestosSinIGV` ni `IGV`. |

---

### 1.4 Mecánica Transversal de Envío (Secciones 3, 7 y 8 de la Especificación)

| ID | Sección Spec | Requisito Transversal | Estado Actual | Evidencia en el Código del CRM | Brecha Identificada | Criterio de Aceptación Verificable |
| :---: | :---: | :--- | :---: | :--- | :--- | :--- |
| **M-01** | Sec. 3.1 / Sec. 8 | Frecuencia y alcance: Ventas y STE cada 4h (1er envío: 3 meses; siguientes: deltas); Stock 1 vez al día al cierre | `FALTA` | [`Program.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Program.cs#L199-L241) | No existe scheduler ni persistencia de marca de agua (watermark) por servicio. | El primer envío de Ventas/STE toma exactamente los últimos 3 meses; el segundo envío toma solo registros creados/modificados tras el corte anterior; Stock envía siempre la foto completa del cierre diario. |
| **M-02** | Sec. 3.2 / Sec. 8 | Formato JSON estricto (PascalCase exacto, sin campos extra, fechas `DD/MM/YYYY`, decimales con `.`) | `FALTA` | [`YamahaContracts.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaContracts.cs#L1-L30) | No existen contratos JSON v3 ni `JsonSerializerOptions` dedicado. | Los payloads serializados pasan validación de esquema exacto (39 propiedades en Ventas, 12 en Stock, 30 en Servicio Técnico) bajo cualquier cultura de sistema operativo. |
| **M-03** | Sec. 7 | Envío `HTTP POST` a las 3 Azure Functions con `?code={Function_Key}` y `Content-Type: application/json` sin filtrar secretos | `PARCIAL` | [`YamahaApiClient.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaApiClient.cs#L45-L90); [`YamahaSettings.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaSettings.cs#L8-L32) | Faltan métodos `POST` por servicio y `YamahaApiClient.cs#L68-L70` registra `request.RequestUri` con query string en los logs. | El cliente envía `POST` con `Content-Type: application/json` a las 3 URLs oficiales y enmascara `?code=***` en cualquier log de éxito o error. |

---

## 2. Plan por Fases y Módulos Técnicos

El trabajo se estructura en **10 módulos desacoplados**, agrupados en **4 fases secuenciales**:

### Fase A — Cimientos de Seguridad, Configuración y Contratos JSON (Sin tocar lógica de negocio ni BD)

1. **Módulo 1 — Seguridad y Configuración (`YamahaSettings`):**
   - Ampliar [`YamahaSettings.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaSettings.cs) para configurar:
     - Datos del punto de venta: `CodigoPuntoVenta`, `NombreComercialPuntoVenta`, `TipoPuntoVenta` (`"3S"`).
     - Endpoints y credenciales independientes o compartidas para los 3 servicios (`VentasUrl` / `VentasFunctionKey`, `StockUrl` / `StockFunctionKey`, `ServicioTecnicoUrl` / `ServicioTecnicoFunctionKey`), leídas exclusivamente desde variables de entorno o User Secrets.
     - Parámetros de programación: `IntervaloHorasVentasYSte` (default `4`), `HoraCierreStockPeru` (ej. `"20:00"`), `VentanaCargaInicialMeses` (default `3`).
   - Implementar un sanitizador de URIs (`SanitizeUriForLogging`) en [`YamahaApiClient.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaApiClient.cs) y silenciar el log por defecto de `System.Net.Http.HttpClient` que imprime query strings.

2. **Módulo 2 — Contratos JSON v3 y Serialización Determinista:**
   - Definir los 3 contratos inmutables (`YamahaVentaRecordDto`, `YamahaStockRecordDto`, `YamahaServicioTecnicoRecordDto`) con atributos `[JsonPropertyName("...")]` exactos según las secciones 4, 5.3 y 6.2.
   - Configurar un `YamahaJsonSerializer` estático con `PropertyNamingPolicy = null`, `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` y convertidores para formatear `decimal` siempre con punto decimal y 2 decimales (`0.00` en `CultureInfo.InvariantCulture`) y `DateTime` en `dd/MM/yyyy`.

3. **Módulo 3 — Motor de Validación Pre-Envío (Dry-Run & Gatekeeper):**
   - Implementar validadores puros en memoria (`YamahaPayloadValidator`) que verifiquen cada registro y el lote completo antes de abrir una conexión HTTP:
     - Longitudes máximas (`VARCHAR(255)`), `RUC` de 11 dígitos, `VIN != CodigoYamaha`, `Chasis != CodigoYamaha`.
     - Reglas de Servicio Técnico: prefijo `OT-`, `Estado == "FACTURADO"`, formato de `Placa` con guión sin `--`, `Chasis` de 15–20 caracteres, consistencia de los 6 campos de cabecera por `NumeroOrdenTrabajo`, y prohibición de `TipoServicio == "-"` en `SubLineaNegocio == "STE"`.
     - Reglas de Ventas: signos negativos en los 4 campos cuando `TipoDocumentoVenta == "NOTA DE CREDITO"` y coherencia `TipoPago` / `EntidadFinanciera`.
     - Reglas de Stock: `StockDisponible > 0` en `MC`/`ATV`/`OM` y `>= 0` en `SP`.

4. **Módulo 4 — Integración HTTP Real (`IYamahaApiClient` / `YamahaApiClient` / `FakeYamahaApiClient`):**
   - Extender [`IYamahaApiClient.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/IYamahaApiClient.cs) con métodos tipados para enviar lotes a `ReceiveRetailVentas`, `ReceiveRetailStock` y `ReceiveRetailServicioTecnico`.
   - Mantener soporte completo en [`FakeYamahaApiClient.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/FakeYamahaApiClient.cs) para almacenar en memoria/disco de pruebas el último lote enviado y simular respuestas HTTP en QA sin tráfico externo.

---

### Fase B — Adaptación de Reglas de Dominio (Clientes, Taller, Inventario, Ventas y Notas de Crédito)

5. **Módulo 5 — Catálogo/Inventario (Servicio de Stock):**
   - Implementar el extractor de cierre de inventario sobre [`Productos`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13-L54) activos filtrados según el criterio de productos Yamaha aprobado.
   - Calcular `CostoVentaSinIGV` y `CostoVentaConIGV` usando [`ConfiguracionEmpresa.PorcentajeIgv`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L18), y resolver `FechaIngreso` desde la última [`Compra`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Compras.cs#L29-L100) registrada (`Estado == EstadoCompra.Registrada`) o movimiento de entrada.

6. **Módulo 6 — Servicio Técnico (`STE`):**
   - Implementar el extractor de órdenes facturadas uniendo [`OrdenServicio`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L79-L154), [`Comprobante`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L117-L139) (`Estado == "Emitido"`), [`Vehiculo`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L53-L80) (`Marca` Yamaha) y [`Cliente`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L10-L36).
   - Aplicar el mapeo no destructivo de `NumeroOrdenTrabajo` (`OS-` -> `OT-` en la capa de proyección o generación según decisión aprobada), desglose de [`DetalleServicio`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L156-L194) en filas `SP` y `STE`, y mapeo de `TipoServicio` (Sección 6.3).
   - Reforzar las validaciones de entrada en taller para vehículos Yamaha (`Placa` con guión, `NumeroSerieVIN` de 15–20 caracteres y `KilometrajeIngreso`).

7. **Módulo 7 — Ventas (`SP` / Unidades):**
   - Implementar el extractor de ventas facturadas desde [`Comprobantes`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L117-L139) y [`DetallesVenta`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L90-L115), aplicando la regla de aislamiento frente a Servicio Técnico para evitar doble reporte de repuestos o envío accidental de mano de obra.
   - Mapear `TipoDocumentoVenta` (`"FACTURA"`, `"BOLETA DE VENTA"`), `TipoPago` / `EntidadFinanciera` y documentos de cliente (`DNI`, `CE`, `RUC`, `DOC NO DOMICILIADO`).

8. **Módulo 8 — Notas de Crédito y Anulaciones (Sección 4.2):**
   - Implementar el flujo aprobado para reversiones de ventas/comprobantes ya reportados a Yamaha, generando los registros con `TipoDocumentoVenta = "NOTA DE CREDITO"` y signo negativo en `MontoSinIGV`, `MontoConIGV`, `PrecioFinal` y `CantidadVendida`.

---

### Fase C — Automatización, Programación de Envíos y Monitoreo

9. **Módulo 9 — Programación de Envíos (`BackgroundService`) y Control Incremental:**
   - Implementar `YamahaSyncBackgroundService` siguiendo el patrón probado de [`EnvioNotificacionesService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Notificaciones/EnvioNotificacionesService.cs) y [`HoraPeru.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Common/HoraPeru.cs):
     - Ejecución cada 4 horas para `Ventas` y `Servicio Técnico` (carga inicial de 3 meses si no hay lote exitoso previo; deltas por marca de agua en envíos subsiguientes).
     - Ejecución diaria al cierre de operaciones para `Stock`.

10. **Módulo 10 — Monitoreo, Trazabilidad y Endpoints Administrativos:**
    - Registrar cada lote (dry-run, exitoso, rechazado por validación interna o fallido en HTTP) con su resumen en [`EventoAuditoria`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Auditoria.cs) (o tabla de lotes si se autoriza migración).
    - Exponer endpoints administrativos protegidos por permiso (ej. `ConfiguracionEditar` en [`Permisos.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Security/Authorization/Permisos.cs)) para:
      - Previsualizar en modo **dry-run** el payload exacto que se enviaría a cada servicio y ver las advertencias/errores de validación sin llamar a Yamaha.
      - Disparar manualmente un envío de contingencia y consultar el estado del último lote por servicio.

---

## 3. Mapa Completo de Datos (Entidad y Campo Origen -> Campo Yamaha v3)

### 3.1 Mapa de Datos — Servicio 1: Ventas (39 campos)

| Campo Yamaha v3 | Entidad y Propiedad Origen en el CRM | Estrategia de Obtención / Derivación | ¿Requiere Cambio o Decisión? |
| :--- | :--- | :--- | :--- |
| `RazonSocial` | `ConfiguracionEmpresa.RazonSocial` ([`Configuracion.cs#L14`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L14)) | Lectura directa de configuración de empresa. | No (solo validar no vacío). |
| `RUC` | `ConfiguracionEmpresa.Ruc` ([`Configuracion.cs#L15`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L15)) | Lectura directa de configuración de empresa. | Validar 11 dígitos reales. |
| `CodigoPuntoVenta` | *No existe hoy* | Leer desde `YamahaSettings.CodigoPuntoVenta`. | **Sí:** Configurar código oficial asignado por YMDP. |
| `NombreComercialPuntoVenta` | `ConfiguracionEmpresa.NombreEmpresa` ([`Configuracion.cs#L13`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L13)) | Leer desde `YamahaSettings.NombreComercialPuntoVenta` con fallback a `NombreEmpresa`. | No. |
| `VIN` | *No existe en `Producto`/`DetalleVenta`* | En línea `SP` enviar `""`. | **Sí (Decisión D-01):** Confirmar si el CRM venderá unidades `MC`/`ATV`/`OM`. |
| `Modelo` | *No existe en `Producto`/`DetalleVenta`* | En línea `SP` enviar `""`. | Depende de D-01. |
| `Color` | *No existe en `Producto`/`DetalleVenta`* | En línea `SP` enviar `""`. | Depende de D-01. |
| `AnioModelo` | *No existe en `Producto`/`DetalleVenta`* | En línea `SP` no aplica (`0` o `""` según confirmación de tipo `INT`). | Depende de D-01 y confirmación de formato `INT` vacío. |
| `FechaFacturacion` | `Comprobante.FechaCreacion` ([`Ventas.cs#L117`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L117)) | `HoraPeru.DesdeUtc(comprobante.FechaCreacion).ToString("dd/MM/yyyy")`. | No (derivación segura). |
| `NumeroComprobante` | `Comprobante.Serie` + `Comprobante.Numero` ([`Ventas.cs#L122-L123`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L122-L123)) | Interpolación `$"{comprobante.Serie}-{comprobante.Numero}"`. | No (derivación segura). |
| `TipoDocumentoVenta` | `Comprobante.Tipo` ([`Ventas.cs#L121`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L121)) | `"Factura"` -> `"FACTURA"`, `"Boleta"` -> `"BOLETA DE VENTA"`; reversiones -> `"NOTA DE CREDITO"`. | **Sí (Decisión D-04):** Estrategia de Notas de Crédito. |
| `TipoPago` | `Venta.Pagos` / `Comprobante.MetodoPagoPrincipal` ([`Ventas.cs#L87`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L87)) | Si todas las ventas son al contado -> `"CONTADO"`; si hay crédito -> `"CREDITO"`. | **Sí (Decisión D-05):** Confirmar si existen ventas a crédito con entidades de la tabla 4.1. |
| `EntidadFinanciera` | *No existe hoy* | Si `TipoPago == "CONTADO"` -> `"CONTADO"`; si `"CREDITO"` -> entidad de tabla 4.1. | Depende de D-05. |
| `MontoSinIGV` | `DetalleVenta.SubtotalGravado` (+ exonerado/inafecto si aplica) ([`Ventas.cs#L109`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L109)) | Valor directo con 2 decimales (multiplicado por `-1` si es `NOTA DE CREDITO`). | No (derivación segura). |
| `MontoConIGV` | `DetalleVenta.Total` ([`Ventas.cs#L114`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L114)) | Valor directo con 2 decimales (multiplicado por `-1` si es `NOTA DE CREDITO`). | No (derivación segura). |
| `Canal` | `Venta.OrdenServicioId` ([`Ventas.cs#L60`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L60)) | `venta.OrdenServicioId.HasValue ? "TALLER" : "MOSTRADOR"`. | Depende de **Decisión D-02** (alcance de ventas de taller). |
| `LineaNegocio` | `DetalleVenta.TipoItem` ([`Ventas.cs#L94`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L94)) | Para `TipoItem == Repuesto` -> `"SP"`. | Depende de D-01 si hay vehículos. |
| `TipoPuntoVenta` | `YamahaSettings.TipoPuntoVenta` | Constante/configuración `"3S"`. | No (valor fijo `"3S"` en especificación). |
| `NumeroMotor` | *No existe en `Producto`/`DetalleVenta`* | En línea `SP` enviar `""`. | Depende de D-01. |
| `Moneda` | `ConfiguracionEmpresa.MonedaBase` ([`Configuracion.cs#L19`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L19)) | `"PEN"`. | No (derivación segura). |
| `PrecioFinal` | `DetalleVenta.Total` ([`Ventas.cs#L114`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L114)) | Importe final con IGV de la línea (multiplicado por `-1` en `NOTA DE CREDITO`). | No (derivación segura). |
| `CodigoYamaha` | `DetalleVenta.Producto.Codigo` ([`Inventario.cs#L16`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L16)) | Lectura directa validando `^[A-Za-z0-9-]+$`. | No (con validación de formato). |
| `Descripcion` | `DetalleVenta.Producto.Nombre` ([`Inventario.cs#L17`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L17)) | Lectura directa truncada a 255 caracteres. | No (derivación segura). |
| `CantidadVendida` | `DetalleVenta.Cantidad` ([`Ventas.cs#L105`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L105)) | Entero directo (multiplicado por `-1` en `NOTA DE CREDITO`). | No (derivación segura). |
| `DescuentoPorcentaje` | *No persistido en `DetalleVenta`* | `0.00` si no aplica descuento (o cálculo frente a precio catálogo si se aprueba persistirlo). | **Sí (Decisión D-06):** Definir si se envía `0.00` o se persiste descuento. |
| `DescuentoSoles` | *No persistido en `DetalleVenta`* | `0.00` si no aplica descuento. | Depende de D-06. |
| `PrecioUnitarioConIGV` | `DetalleVenta.PrecioUnitario` y `PorcentajeIgvAplicado` ([`Ventas.cs#L106-L110`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L106-L110)) | `Math.Round(d.PrecioUnitario * (1m + d.PorcentajeIgvAplicado / 100m), 2)`. | No (derivación matemática exacta). |
| `TipoCliente` | *No existe en `Cliente`* | Opcional: enviar `""` (o `"CORPORATIVO"` para RUC 20 / `"CLIENTE FINAL"` para DNI/RUC 10). | Opcional en especificación. |
| `NombreCompletoCliente` | `Venta.Cliente.NombreCompleto` ([`Clientes.cs#L12`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L12)) | Lectura directa (`<= 255` chars). | No. |
| `TipoDocumentoCliente` | `Venta.Cliente.TipoDocumento` ([`Clientes.cs#L15`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L15)) | `DNI` -> `"DNI"`, `RUC` -> `"RUC"`, `Otro` -> `"CE"` (si 9 dígitos) o `"DOC NO DOMICILIADO"`. | **Sí:** Ajustar mapeo/enum para separar `CE` y `DOC NO DOMICILIADO`. |
| `NumeroDocumentoCliente` | `Venta.Cliente.NumeroDocumento` ([`Clientes.cs#L16`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L16)) | Lectura directa validada por longitud según tipo. | No. |
| `CanalContactoCliente` | *No existe en `Cliente`/`Venta`* | Opcional: enviar `""`. | No (campo opcional). |
| `DistritoCliente` | *No existe en `Cliente`* | Opcional: enviar `""`. | No (campo opcional). |
| `CiudadCliente` | *No existe en `Cliente`* | Opcional: enviar `""`. | No (campo opcional). |
| `DepartamentoCliente` | *No existe en `Cliente`* | Opcional: enviar `""`. | No (campo opcional). |
| `DireccionCliente` | `Venta.Cliente.Direccion` ([`Clientes.cs#L20`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L20)) | `cliente.Direccion ?? ""` (`<= 255` chars). | No. |
| `MotoActualCliente` | `Venta.Cliente.Vehiculos` ([`Clientes.cs#L29`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L29)) | Opcional: enviar `""` o el modelo del primer vehículo activo del cliente. | No (campo opcional). |
| `CelularCliente` | `Venta.Cliente.Telefono` ([`Clientes.cs#L17`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L17)) | Sanitizar y enviar solo si tiene 9 dígitos numéricos (`^\d{9}$`); si no, `""`. | No (derivación segura). |
| `CorreoCliente` | `Venta.Cliente.Email` ([`Clientes.cs#L19`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L19)) | Enviar si tiene formato de email válido; si no, `""`. | No (derivación segura). |

---

### 3.2 Mapa de Datos — Servicio 2: Stock (12 campos)

| Campo Yamaha v3 | Entidad y Propiedad Origen en el CRM | Estrategia de Obtención / Derivación | ¿Requiere Cambio o Decisión? |
| :--- | :--- | :--- | :--- |
| `RazonSocial` | `ConfiguracionEmpresa.RazonSocial` ([`Configuracion.cs#L14`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L14)) | Lectura directa. | No. |
| `RUC` | `ConfiguracionEmpresa.Ruc` ([`Configuracion.cs#L15`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L15)) | Lectura directa validada a 11 dígitos. | No. |
| `CodigoPuntoVenta` | `YamahaSettings.CodigoPuntoVenta` | Lectura desde configuración. | Sí (configurar código oficial). |
| `NombreComercialPuntoVenta` | `ConfiguracionEmpresa.NombreEmpresa` ([`Configuracion.cs#L13`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L13)) | Lectura desde `YamahaSettings` o `NombreEmpresa`. | No. |
| `LineaNegocio` | `Producto` ([`Inventario.cs#L13`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L13)) | Valor `"SP"` para repuestos. | Depende de D-01 si hay vehículos. |
| `CodigoYamaha` | `Producto.Codigo` ([`Inventario.cs#L16`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L16)) | Lectura directa validando `^[A-Za-z0-9-]+$`. | **Sí (Decisión D-07):** Criterio de filtro de productos Yamaha. |
| `Descripcion` | `Producto.Nombre` ([`Inventario.cs#L17`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L17)) | Lectura directa (`<= 255` chars). | No. |
| `StockDisponible` | `Producto.StockActual` ([`Inventario.cs#L30`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L30)) | `Math.Max(0, producto.StockActual)` para `SP`. | No. |
| `CostoVentaSinIGV` | `Producto.Costo` o `Producto.PrecioVenta` ([`Inventario.cs#L28-L29`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L28-L29)) | Según definición confirmada, redondeado a 2 decimales. | **Sí (Decisión D-03):** Confirmar si es `Costo` (compra) o `PrecioVenta` (público). |
| `CostoVentaConIGV` | Calculado con `ConfiguracionEmpresa.PorcentajeIgv` ([`Configuracion.cs#L18`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L18)) | `Math.Round(CostoVentaSinIGV * (1m + PorcentajeIgv / 100m), 2)`. | Depende de D-03. |
| `FechaIngreso` | `DetalleCompra.Compra.FechaEmision` ([`Compras.cs#L40`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Compras.cs#L40)) o `MovimientoInventario.FechaCreacion` ([`Inventario.cs#L56`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L56)) | Fecha de última compra/entrada en formato `dd/MM/yyyy` (o `""` si nunca tuvo entradas). | No (derivación segura, campo opcional). |
| `VIN` | *No aplica para `SP`* | Enviar `""` para `SP`. | Depende de D-01. |

---

### 3.3 Mapa de Datos — Servicio 3: Servicio Técnico (30 campos)

| Campo Yamaha v3 | Entidad y Propiedad Origen en el CRM | Estrategia de Obtención / Derivación | ¿Requiere Cambio o Decisión? |
| :--- | :--- | :--- | :--- |
| `LineaNegocio` | Constante de contrato | Valor fijo `"STE"`. | No. |
| `SubLineaNegocio` | `DetalleServicio.TipoItem` ([`Taller.cs#L160`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L160)) | `Repuesto` -> `"SP"`; `Servicio`/`ManoDeObra` -> `"STE"`. | Confirmar manejo de `TipoItemServicio.Terceros`. |
| `RazonSocial` | `ConfiguracionEmpresa.RazonSocial` ([`Configuracion.cs#L14`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L14)) | Lectura directa. | No. |
| `RUC` | `ConfiguracionEmpresa.Ruc` ([`Configuracion.cs#L15`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L15)) | Lectura directa (11 dígitos), idéntico en toda la OT. | No. |
| `NombreComercialPuntoVenta` | `ConfiguracionEmpresa.NombreEmpresa` ([`Configuracion.cs#L13`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Configuracion.cs#L13)) | Lectura desde `YamahaSettings` o `NombreEmpresa`. | No. |
| `CodigoPuntoVenta` | `YamahaSettings.CodigoPuntoVenta` | Lectura desde configuración. | Sí (configurar código oficial). |
| `NumeroOrdenTrabajo` | `OrdenServicio.NumeroOrden` ([`Taller.cs#L89`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L89)) | Transformar prefijo `OS-` -> `OT-` al serializar (ej. `OS-000123` -> `OT-000123`). | **Sí (Decisión D-08):** Ver sección 4.1 de riesgos de regresión. |
| `FechaFacturacion` | `Comprobante.FechaCreacion` ([`Ventas.cs#L117`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Ventas.cs#L117)) | `HoraPeru.DesdeUtc(comprobanteEmitido.FechaCreacion).ToString("dd/MM/yyyy")`, idéntica en toda la OT. | No (derivación segura). |
| `Estado` | Estado del `Comprobante` (`"Emitido"`) | Valor fijo `"FACTURADO"` (solo se incluyen órdenes con comprobante emitido). | No. |
| `TipoDocumentoCliente` | `OrdenServicio.Cliente.TipoDocumento` ([`Clientes.cs#L15`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L15)) | `"DNI"`, `"CE"`, `"RUC"`, `"DOC NO DOMICILIADO"`. | Sí (soportar `CE` y `DOC NO DOMICILIADO`). |
| `NumeroDocumentoCliente` | `OrdenServicio.Cliente.NumeroDocumento` ([`Clientes.cs#L16`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L16)) | Lectura directa de cabecera de la OT, idéntica en todas sus líneas. | No. |
| `NombreCliente` | `OrdenServicio.Cliente.NombreCompleto` ([`Clientes.cs#L12`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L12)) | Lectura directa de cabecera de la OT, idéntica en todas sus líneas. | No. |
| `Celular` | `OrdenServicio.Cliente.Telefono` ([`Clientes.cs#L17`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L17)) | Sanitizar a 9 dígitos numéricos o `""`. | No. |
| `Correo` | `OrdenServicio.Cliente.Email` ([`Clientes.cs#L19`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L19)) | Email válido o `""`. | No. |
| `Placa` | `OrdenServicio.Vehiculo.Placa` ([`Clientes.cs#L58`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L58)) | Obligatoria con guión (`-`), 4–10 alfanuméricos, sin espacios ni `--`. | **Sí:** Validar formato en vehículos Yamaha. |
| `Chasis` | `OrdenServicio.Vehiculo.NumeroSerieVIN` ([`Clientes.cs#L63`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L63)) | Obligatorio, 15–20 caracteres alfanuméricos + `-`, `!= CodigoYamaha`. | **Sí:** Validar obligatoriedad y longitud 15–20 en vehículos Yamaha. |
| `Marca` | `OrdenServicio.Vehiculo.Marca` ([`Clientes.cs#L60`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L60)) | Filtrar vehículos cuya marca sea Yamaha y enviar literal fijo `"YAMAHA"`. | No. |
| `Modelo` | `OrdenServicio.Vehiculo.Modelo` ([`Clientes.cs#L61`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Clientes.cs#L61)) | `vehiculo.Modelo ?? ""`. | No. |
| `Version` | *No existe en `Vehiculo`* | Enviar `""`. | No (campo opcional). |
| `Kilometraje` | `OrdenServicio.KilometrajeIngreso` ?? `LecturaMedidorIngreso` ([`Taller.cs#L107-L109`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L107-L109)) | Entero obligatorio (`>= 0`). | **Sí:** Exigir kilometraje al facturar OT de vehículo Yamaha. |
| `TipoServicio` | `OrdenServicio.TipoAtencion` ([`Taller.cs#L101`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L101)) | Mapeo a `MP`, `D`, `R`, `MP+R`, `RC`, `G`, `I`, `L`, `SG`, `-`. | **Sí (Decisión D-09):** Ampliar o mapear catálogo de `TipoAtencion`. |
| `Actividad` | `DetalleServicio.Descripcion` ([`Taller.cs#L163`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L163)) | Descripción de la línea (`<= 255` chars), o `""` si `TipoServicio == "-"`. | No. |
| `CodigoYamaha` | `DetalleServicio.Producto.Codigo` ([`Inventario.cs#L16`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L16)) | En `SubLineaNegocio == "SP"` -> `Producto.Codigo` (`<= 50` chars, `!= Chasis`); en `"STE"` -> `""`. | No. |
| `Descripcion` | `DetalleServicio.Producto.Nombre` ([`Inventario.cs#L17`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Inventario.cs#L17)) | En `SubLineaNegocio == "SP"` -> nombre del repuesto; en `"STE"` -> `""`. | No. |
| `Cantidad` | `DetalleServicio.Cantidad` ([`Taller.cs#L164`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L164)) | En `SP` -> `DetalleServicio.Cantidad`; en `STE` -> no aplica. | Confirmar serialización de `Cantidad` en `STE` (`""` vs `0`). |
| `MontoSinIGV` | `DetalleServicio.SubtotalGravado` ([`Taller.cs#L169`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L169)) | Importe neto sin IGV de la línea con 2 decimales. | No. |
| `MontoConIGV` | `DetalleServicio.Total` ([`Taller.cs#L174`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L174)) | Importe con IGV de la línea con 2 decimales. | No. |
| `PrecioUnit` | `DetalleServicio.PrecioUnitario` ([`Taller.cs#L165`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L165)) | Precio unitario para `SP`; `0.00` (o `""`) para `STE`. | No. |
| `Descuento` | *No persistido en `DetalleServicio`* | `0.00` si no aplica descuento. | Depende de D-06. |
| `TotalConIGV` | `DetalleServicio.Total` ([`Taller.cs#L174`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Domain/Entities/Taller.cs#L174)) | Monto total de la línea incluyendo IGV con 2 decimales. | No. |

---

## 4. Análisis Detallado de Riesgos de Regresión (Enfoque No Destructivo)

### 4.1 El Prefijo `OS-` vs `OT-` y las Órdenes Históricas
- **Situación actual en el código:**
  - En [`OrdenServicioService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Taller/OrdenServicioService.cs#L33-L39), `GenerarNumeroOrdenAsync()` utiliza la secuencia PostgreSQL `OrdenServicioNumeroSeq` ([`ApplicationDbContext.cs#L168-L170`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Data/ApplicationDbContext.cs#L168-L170)) y formatea `$"OS-{seqVal:D6}"` (ej. `OS-000042`).
  - Este `NumeroOrden` se muestra en el frontend web (`src/web`), en la app móvil (`src/movil`), en las notificaciones push ([`NotificacionesInterceptor.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Notificaciones/NotificacionesInterceptor.cs)), en los motivos de Kardex (`MovimientoInventario.Motivo`) y en las referencias de auditoría y aprobaciones (`SolicitudAprobacion.EntidadId` / `DetalleCambio`).
- **Riesgo de regresión si se modifica la base de datos o el generador interno:**
  - Renombrar `OrdenServicio.NumeroOrden` en la base de datos de `OS-` a `OT-` rompería las referencias históricas en texto dentro de `MovimientosInventario`, `EventosAuditoria`, `Notificaciones`, búsquedas de usuarios acostumbrados a `OS-`, e impresiones previas entregadas a clientes.
- **Solución no destructiva recomendada:**
  - **Conservar intacto `OS-000001` dentro del CRM** y realizar una **traducción biunívoca y determinista en la capa de proyección hacia Yamaha**:
    $$\text{Yamaha.NumeroOrdenTrabajo} = \text{Regex.Replace}(\text{NumeroOrden}, \text{\texttt{"\^{}OS-"}}, \text{\texttt{"OT-"}})$$
  - De esta forma, `OS-000042` se envía siempre como `OT-000042` tanto para las órdenes históricas de los últimos 3 meses como para las nuevas, cumpliendo el formato obligatorio `OT-...` de la Sección 6.2 con **cero impacto** en la base de datos, frontend, app móvil, Kardex o auditoría.

### 4.2 Órdenes Históricas (Ventana Inicial de 3 Meses)
- **Situación actual:**
  - En el primer envío de Servicio Técnico (Sección 3.1), se deben incluir las órdenes facturadas de los últimos 3 meses.
  - Es posible que existan órdenes históricas en la base de datos que:
    1. Pertenecen a vehículos de otras marcas (ej. `"Honda"`, `"Bajaj"`).
    2. Pertenecen a vehículos Yamaha pero fueron registrados en su momento con `Placa == null`, placa sin guión (`"ABC123"`), `NumeroSerieVIN == null` o `KilometrajeIngreso == null`.
- **Riesgo de regresión:**
  - Si un único registro histórico inválido se incluye sin filtrar en el lote JSON, **Yamaha puede rechazar el lote completo** o fallar la carga inicial.
  - Por otro lado, si hacemos `Placa` o `NumeroSerieVIN` `NOT NULL` a nivel de esquema PostgreSQL (`Vehiculos`), fallarían las migraciones sobre datos existentes o se impediría atender en el taller motocicletas de otras marcas o unidades nuevas en trámite de placa.
- **Solución no destructiva recomendada:**
  - **No alterar la nulabilidad de columnas en PostgreSQL.**
  - Aplicar la validación a nivel de negocio únicamente cuando se intente facturar una orden de un vehículo marca `YAMAHA` (o a través del endpoint de pre-validación dry-run), reportando en una bandeja/resumen de pre-envío qué órdenes históricas tienen datos incompletos (`Placa` o `Chasis` faltante) para que el administrador pueda completarlas desde la pantalla actual de Vehículos antes de ejecutar el primer envío histórico.

### 4.3 Anulaciones y Notas de Crédito (Sección 4.2)
- **Situación actual en el código:**
  - [`VentaService.AnularAsync`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1111-L1249) cambia `venta.Estado = EstadoVenta.Anulada`, anula sus pagos, revierte el stock en `MovimientoInventario` y marca `comprobante.Estado = "Anulado"`.
  - [`VentaService.AnularComprobanteAsync`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L1417-L1456) cambia únicamente `comprobante.Estado = "Anulado"` manteniendo la venta confirmada para poder emitir otro comprobante.
- **Riesgo de regresión:**
  - Alterar `AnularAsync` o `AnularComprobanteAsync` para forzar la creación de nuevas entidades contables sin definición previa podría romper el cuadre de Caja Chica, Reportes de Ventas y Kardex que hoy dependen de `venta.Estado == EstadoVenta.Confirmada`.
- **Escenarios y manejo seguro sin romper la contabilidad actual:**
  - **Caso 1 (Anulación ANTES de que el comprobante haya sido enviado a Yamaha):** Si una venta se emite a las 10:15 y se anula a las 10:30 (antes del corte de las 12:00), ni la venta original ni su anulación deben enviarse a Yamaha, pues para efectos de reporte externo la transacción fue cancelada antes de informarse.
  - **Caso 2 (Anulación DESPUÉS de que el comprobante ya fue reportado a Yamaha):** Como Yamaha ya recibió el comprobante original en un lote anterior, la única forma admitida por la Sección 4.2 de revertir esa venta en el sistema de Yamaha es enviar un registro con `TipoDocumentoVenta = "NOTA DE CREDITO"` y signo negativo en `MontoSinIGV`, `MontoConIGV`, `PrecioFinal` y `CantidadVendida`. *(Ver Decisión Pendiente D-04 en la Sección 6 para confirmar el número de comprobante a reportar en este caso).*

### 4.4 Pagos (`TipoPago` y `EntidadFinanciera` — Sección 4.1)
- **Situación actual:**
  - [`PagoService.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/PagoService.cs) permite registrar múltiples pagos parciales o totales asociados a una `Venta` o `OrdenServicio` usando `MetodoPago` (`EFECTIVO`, `TARJETA`, `TRANSFERENCIA`, `YAPE_PLIN`).
  - Una venta puede confirmarse y facturarse teniendo pagos completos (`TotalPagado >= Total`) o saldo pendiente (`TotalPagado < Total`).
- **Riesgo de regresión:**
  - No se debe modificar el funcionamiento de `PagoService` ni de `CajaChica`.
  - Si el negocio opera exclusivamente al contado (o los medios de pago actuales representan pagos al contado), se puede mapear de forma predeterminada `TipoPago = "CONTADO"` y `EntidadFinanciera = "CONTADO"`, habilitando opcionalmente un campo no obligatorio para especificar entidad financiera solo si existen ventas a crédito (Decisión D-05).

### 4.5 Riesgo de Duplicidad entre Servicio 1 (Ventas) y Servicio 3 (Servicio Técnico)
- **Situación actual en el código:**
  - Cuando una `OrdenServicio` pasa por facturación ([`VentaService.CreateAsync`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Services/Ventas/VentaService.cs#L230-L255)), se crea una entidad `Venta` con `Venta.OrdenServicioId = orden.Id` y se copian todos los `DetalleServicio` hacia `DetalleVenta` (tanto `TipoItemServicio.Repuesto` como `Servicio`, `ManoDeObra` y `Terceros`).
- **Análisis del riesgo:**
  1. **Mano de Obra en Ventas:** Los `DetalleVenta` con `TipoItem != TipoItemServicio.Repuesto` (`Servicio`, `ManoDeObra`, `Terceros`) **jamás deben incluirse en el Servicio 1 (Ventas)**, porque la Sección 4 solo admite `LineaNegocio` en `{"MC", "ATV", "OM", "SP"}`.
  2. **Repuestos de Taller (`Venta.OrdenServicioId != null`):** Esos repuestos ya se informan detalladamente en el **Servicio 3 (Servicio Técnico)** bajo `SubLineaNegocio = "SP"`. Sin embargo, la Sección 4 incluye el campo `Canal` con valores permitidos `MOSTRADOR / TALLER`. Por tanto, antes de implementar el filtro de `Ventas`, se debe confirmar (Decisión D-02) si Yamaha requiere que los repuestos de una OT se envíen **solo** en Servicio Técnico, o **también** en el Servicio de Ventas con `Canal = "TALLER"`.

---

## 5. Diseño de la Arquitectura de Integración

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                        CRM Team Benavides (.NET 10)                          │
│                                                                              │
│  ┌─────────────────────────┐       ┌──────────────────────────────────────┐  │
│  │ YamahaSyncBackgroundSvc │──────▶│      YamahaSyncOrchestrator          │  │
│  │ (Cada 4h: Ventas y STE) │       │ 1. Lee Watermark (último corte OK)   │  │
│  │ (Diario cierre: Stock)  │       │ 2. Extrae registros (3m o Deltas)    │  │
│  └─────────────────────────┘       │ 3. Transforma a DTOs exactos v3      │  │
│               ▲                    │ 4. Ejecuta YamahaPayloadValidator    │  │
│               │                    │ 5. Envía vía IYamahaApiClient        │  │
│  ┌─────────────────────────┐       │ 6. Registra lote en Auditoría        │  │
│  │ Endpoints Admin Yamaha  │──────▶│ 7. Avanza Watermark SOLO si HTTP 2xx │  │
│  │ - POST /dry-run         │       └──────────────────┬───────────────────┘  │
│  │ - POST /ejecutar        │                          │                      │
│  └─────────────────────────┘                          ▼                      │
│                                    ┌──────────────────────────────────────┐  │
│                                    │           YamahaApiClient            │  │
│                                    │ - POST Content-Type: application/json│  │
│                                    │ - Query ?code={Function_Key}         │  │
│                                    │ - Sanitiza URI en ILogger (?code=***)│  │
│                                    └──────────────────┬───────────────────┘  │
└───────────────────────────────────────────────────────┼──────────────────────┘
                                                        │ HTTPS POST
                                                        ▼
             ┌────────────────────────────────────────────────────┐
             │       Azure Functions — Yamaha Motor del Perú      │
             │ - /api/ReceiveRetailVentas?code=...                │
             │ - /api/ReceiveRetailStock?code=...                 │
             │ - /api/ReceiveRetailServicioTecnico?code=...       │
             └────────────────────────────────────────────────────┘
```

### 5.1 Autenticación de Solicitudes y Cero Secretos en Logs
- **Esquema oficial (Sección 7):** Cada endpoint es un Azure Function HTTP Trigger autenticado mediante el parámetro de consulta `?code={Function_Key}` y requiere la cabecera `Content-Type: application/json`.
- **Almacenamiento de credenciales:**
  - En `appsettings.json` los campos de `Function_Key` estarán vacíos (`""`).
  - En QA y Producción se inyectarán mediante variables de entorno (`Yamaha__VentasFunctionKey`, `Yamaha__StockFunctionKey`, `Yamaha__ServicioTecnicoFunctionKey`, o una clave común `Yamaha__FunctionKey` si YMDP entrega una única clave para la Function App).
- **Redacción de secretos en logs:**
  - En [`YamahaApiClient.cs`](file:///d:/Projects/CRMTeamBenavides/src/CRMTeamBenavides.Api/Features/Yamaha/YamahaApiClient.cs), antes de escribir cualquier mensaje en `ILogger`, se reemplazará el valor de `code=` por `code=***REDACTED***`.
  - En la configuración de logging de `Program.cs` / `appsettings.json`, se establecerá `"System.Net.Http.HttpClient.IYamahaApiClient": "Warning"` para impedir que el logger interno de .NET registre las URLs de salida en nivel `Information`.

### 5.2 Control de Carga Inicial (3 Meses) vs Lotes Incrementales (Deltas)
- **Determinación automática del alcance (Sección 3.1):**
  - Para **Ventas** y **Servicio Técnico**:
    1. El orquestador consulta si existe un corte exitoso previo (`WatermarkUtc`) para ese servicio.
    2. **Si NO existe corte previo (Primer Envío):** Calcula `fechaInicioUtc = HoraPeru.AHoraUtc(HoraPeru.Ahora().Date.AddMonths(-3))` y selecciona únicamente los comprobantes emitidos en `[fechaInicioUtc, fechaCorteActualUtc]`.
    3. **Si SÍ existe corte previo (Envíos Posteriores cada 4h):** Selecciona únicamente los registros cuya fecha de emisión de comprobante o fecha de última modificación (`Comprobante.FechaCreacion`, `Venta.FechaModificacion`, `OrdenServicio.FechaModificacion`) sea mayor a `WatermarkUtc` y menor o igual a `fechaCorteActualUtc`.
    4. **Si no hay registros nuevos ni modificados en la ventana de 4 horas:** Se registra el ciclo como `"SIN_CAMBIOS"` sin realizar llamadas innecesarias o enviando el arreglo vacío `[]` según confirme Yamaha.
  - Para **Stock**:
    - Ignora marcas de agua históricas: siempre construye la foto completa del inventario activo al momento del cierre diario.

### 5.3 Manejo de Errores HTTP, Reintentos, Duplicados y Recuperación ante Fallos
- **Transaccionalidad del Cursor (`WatermarkUtc`):**
  - Antes de iniciar la consulta a la base de datos, el orquestador captura `fechaCorteActualUtc = DateTime.UtcNow`.
  - Si Yamaha responde **HTTP `2xx`**, se persiste `WatermarkUtc = fechaCorteActualUtc` en el registro de control/auditoría.
  - Si ocurre un error de validación local, un timeout (`TaskCanceledException`), un fallo de red (`HttpRequestException`) o una respuesta **HTTP `4xx` / `5xx`** de Yamaha:
    - **El `WatermarkUtc` NO se actualiza.**
    - Se registra en auditoría el código de estado HTTP y el cuerpo de error devuelto por Yamaha (donde Yamaha detalla el campo o registro rechazado según la Sección 8).
- **Política de Reintentos:**
  - **Errores `400 Bad Request` / `401 Unauthorized` / `403 Forbidden`:** **No se reintentan inmediatamente** en bucle, ya que indican un error de contrato/datos o una `Function_Key` inválida. Se registra el detalle para corrección y se reintenta en el siguiente ciclo o mediante disparo manual tras corregir el dato.
  - **Errores transitorios (`408 Request Timeout`, `429 Too Many Requests`, `500`, `502`, `503`, `504` o fallo de red):** Se aplica hasta 2 reintentos con espera exponencial corta (ej. 5 s y 15 s) dentro de la misma ejecución; si persiste la caída de Azure, queda pendiente para el siguiente turno de 4 horas sin pérdida de datos.
- **Prevención de duplicados y concurrencia:**
  - Un `SemaphoreSlim(1, 1)` por servicio dentro del proceso garantiza que un disparo manual y el `BackgroundService` nunca ejecuten dos envíos simultáneos del mismo servicio.

---

## 6. Datos Faltantes y Decisiones Pendientes de Aprobación

Para poder iniciar la codificación sin suposiciones, se requiere tu decisión o confirmación sobre los siguientes **9 puntos**, clasificados según su naturaleza:

### A. Decisiones Comerciales y Contables Internas (Negocio Team Benavides)

- **[D-01] Alcance de Líneas de Negocio (`SP` vs `MC`/`ATV`/`OM`):**
  - *Pregunta:* ¿En el CRM Team Benavides se gestionarán y reportarán únicamente **Repuestos/Accesorios (`SP`)** y **Servicio Técnico (`STE`)**, o el concesionario también vende vehículos nuevos (**Motocicletas `MC`, Cuatrimotos `ATV`, Motores Fuera de Borda `OM`**) desde este sistema?
  - *Impacto:* Si solo se reportan `SP` y `STE`, no se requiere crear tablas ni campos de inventario de vehículos del dealer (`VIN`, `NumeroMotor`, `AnioModelo` en `Producto`).

- **[D-05] Ventas al Contado vs Crédito y Entidades Financieras (Sección 4.1):**
  - *Pregunta:* ¿Todas las ventas registradas en el CRM se reportarán como `TipoPago = "CONTADO"` y `EntidadFinanciera = "CONTADO"`, o se realizan ventas a crédito financiadas por entidades de la tabla 4.1 (`EFECTIVA`, `INTEGRA`, `MERCADO PAGO`, `GLOBAL GO`, `CREDITO DIRECTO`, `FONBIENES`, `CALIDDA`, `BCP`, `BBVA`) que requieran un selector en la pantalla de Venta/Comprobante?

- **[D-06] Manejo de Descuentos (`DescuentoPorcentaje`, `DescuentoSoles`, `Descuento`):**
  - *Pregunta:* Actualmente, cuando se modifica un precio unitario en una venta u orden de servicio, se guarda directamente el `PrecioUnitario` pactado. ¿Deseas que a efectos de Yamaha ese `PrecioUnitario` se reporte como precio final sin descuento (`DescuentoPorcentaje = 0.00`, `DescuentoSoles = 0.00`, `Descuento = 0.00`), o deseas que calculemos/persistamos la diferencia cuando el `PrecioUnitario` sea menor al `Producto.PrecioVenta` de catálogo?

- **[D-07] Filtro de Productos Yamaha en Stock y Ventas:**
  - *Pregunta:* ¿Todos los registros activos de la tabla `Productos` pertenecen al catálogo Yamaha y deben enviarse en el cierre de Stock, o en el CRM conviven repuestos de otras marcas que deben filtrarse (por ejemplo, enviando solo aquellos donde `Producto.Marca` sea `"YAMAHA"` o pertenezcan a determinadas categorías)?

- **[D-09] Clasificación de `TipoServicio` en Órdenes de Taller (Sección 6.3):**
  - *Pregunta:* El enum actual `TipoAtencion` tiene 4 valores: `MantenimientoPreventivo` (`MP`), `MantenimientoCorrectivo` (`R`), `ReclamoGarantia` (`G`) y `Gratuito` (`SG`). ¿Es suficiente con esos 4 códigos para la operación del taller de Team Benavides, o necesitas que agreguemos al enum los otros 5 tipos de servicio de Yamaha (`D` Diagnóstico, `MP+R` Mant. Prev. + Rep., `RC` Recall, `I` Inspección, `L` Lavado)? Asimismo, para los repuestos (`SubLineaNegocio = "SP"`) dentro de una orden de trabajo, ¿deben heredar el `TipoServicio` de la orden (ej. `MP`, `R`) o enviarse con `"-"`?

### B. Decisiones Técnicas sobre Reglas del CRM

- **[D-08] Prefijo `OT-` en Servicio Técnico (Sección 6.2):**
  - *Propuesta recomendada:* Mantener el correlativo interno `OS-000001` en la base de datos y pantallas del CRM para no afectar historial ni usuarios, y transformarlo automáticamente a `OT-000001` al generar el JSON para Yamaha. ¿Confirmas este enfoque?

- **[D-04] Manejo de Anulaciones y Notas de Crédito (Sección 4.2):**
  - *Opciones:*
    - **Opción 4A (Sin cambios de esquema):** Si un comprobante aún no ha sido enviado a Yamaha y se anula, simplemente se omite. Si ya fue enviado a Yamaha y posteriormente se anula en el CRM, el integrador genera automáticamente el registro de reversión con `TipoDocumentoVenta = "NOTA DE CREDITO"` y montos/cantidades negativos.
    - **Opción 4B (Con soporte explícito de Nota de Crédito):** Agregar `NotaCredito` al flujo de `Comprobante` con su propia serie y correlativo para que el usuario emita formalmente la Nota de Crédito cuando anule una factura/boleta ya emitida.

### C. Confirmaciones con Yamaha Motor del Perú (YMDP)

- **[D-02] Repuestos facturados dentro de una Orden de Trabajo (`Canal = TALLER` vs `Servicio Técnico`):**
  - *Pregunta para YMDP:* Los repuestos utilizados y facturados en una Orden de Servicio ya se reportan en `ReceiveRetailServicioTecnico` con `SubLineaNegocio = "SP"`. ¿Deben esos mismos repuestos incluirse **también** en `ReceiveRetailVentas` (con `Canal = "TALLER"`), o `ReceiveRetailVentas` es únicamente para ventas de mostrador (`Canal = "MOSTRADOR"`)?
- **[D-03] Definición de `CostoVentaSinIGV` y `CostoVentaConIGV` en Stock (Sección 5.3):**
  - *Pregunta para YMDP:* ¿Estos dos campos del servicio de Stock corresponden al **costo de compra del concesionario** (`Producto.Costo`) o al **precio de venta al público** (`Producto.PrecioVenta`) sin y con IGV?

---

## 7. Estrategia de Pruebas y Procedimiento de Despliegue (QA -> Producción)

### 7.1 Niveles de Pruebas

1. **Pruebas Unitarias (sin base de datos ni red):**
   - **Serialización de contratos:** Verificación de que el JSON generado para `Ventas` tenga exactamente los 39 nombres de campo en PascalCase, `Stock` tenga los 12 campos (sin `SitioDealer` ni `FechaVenta`) y `Servicio Técnico` tenga los 30 campos (sin `FechaApertura`, `FechaCierre`, `MOSinIGV`, `RepuestosSinIGV`, `IGV`).
   - **Formateo regional:** Ejecución forzando `Thread.CurrentThread.CurrentCulture = new CultureInfo("es-PE")` y `"es-ES"` para comprobar que los decimales siempre usen punto (`.`) y las fechas `dd/MM/yyyy`.
   - **Reglas condicionales y de negocio:** Signos negativos en `NOTA DE CREDITO`, regla de stock `0` por línea de negocio, consistencia de los 6 campos de cabecera en líneas de una misma OT, transformación `OS-` -> `OT-`, y sanitización de `?code=***` en logs.

2. **Pruebas de Integración (contra `crm_teambenavides_qa` con `FakeYamahaApiClient`):**
   - Carga histórica inicial (3 meses) vs ejecución incremental posterior (verificando que al crear una nueva venta o facturar una nueva OT, el siguiente lote contenga únicamente el delta).
   - Simulación de fallo HTTP `500` y `400` verificando que el cursor (`WatermarkUtc`) no avance y los registros se reprocesen tras resolver el fallo.

3. **Pruebas de Regresión del CRM:**
   - Verificar que el flujo actual de creación de Ventas de mostrador, Órdenes de Servicio, Aprobaciones, Pagos, Compras, Kardex e Importador Excel siga funcionando sin alteraciones.

4. **Pruebas de Aceptación (Dry-Run con datos reales):**
   - Ejecución del endpoint `POST /api/yamaha/sync/dry-run` sobre una copia de datos reales para inspeccionar el archivo JSON resultante y validar que 0 registros tengan errores de formato antes de activar las credenciales de producción.

### 7.2 Procedimiento Paso a Paso para Activar Producción sin Perder ni Duplicar Reportes

1. **Paso 1 — Despliegue en modo pasivo (`Enabled = false`, `UseFakeClient = true`):**
   - Desplegar los nuevos binarios en producción con el scheduler automático deshabilitado. Ningún dato se envía aún a Yamaha.
2. **Paso 2 — Auditoría en vivo con Dry-Run:**
   - Invocar el endpoint de **dry-run** para `Stock`, `Ventas` (últimos 3 meses) y `Servicio Técnico` (últimos 3 meses).
   - Si el validador pre-envío detecta órdenes o clientes con datos faltantes (por ejemplo, una moto Yamaha sin placa con guión o sin VIN de 15–20 caracteres), corregir esos registros puntuales desde la interfaz del CRM hasta que el dry-run arroje `0 errores`.
3. **Paso 3 — Configuración de Credenciales (`Function_Key`) y `CodigoPuntoVenta`:**
   - Configurar en las variables de entorno del servidor de producción las URLs oficiales de la Sección 7, el `CodigoPuntoVenta` y la `Function_Key` entregada por YMDP.
4. **Paso 4 — Ejecución controlada del Primer Envío Histórico (Sección 3.1):**
   - Disparar manualmente el primer envío de `Ventas` (histórico de 3 meses), `Servicio Técnico` (histórico de 3 meses) y `Stock` (cierre del día).
   - Verificar en el registro de auditoría la respuesta `HTTP 200 OK` de Azure Functions y la creación automática del `WatermarkUtc` inicial.
5. **Paso 5 — Activación del Scheduler Incremental (`Enabled = true`, `UseFakeClient = false`):**
   - Con el `WatermarkUtc` ya establecido por el Paso 4, activar el `BackgroundService` para que cada 4 horas envíe **únicamente los registros nuevos o modificados** desde ese instante, y al cierre diario envíe el snapshot de `Stock`.

---

## 8. Orden de Implementación Propuesto (Entregas Pequeñas y Verificables)

Para minimizar el riesgo y facilitar la revisión de código, se dividió la implementación en entregas incrementales verificadas mediante pruebas automatizadas:

| Entrega | Alcance Técnico Acotado | Archivos Afectados / Nuevos | Estado Técnico |
| :---: | :--- | :--- | :---: |
| **Entrega 1** | **Seguridad, Configuración y Contratos JSON v3:** DTOs v3 (39, 12 y 30 campos), serializador genérico con formato regional, `YamahaV3Validator`, sanitización de secretos en URI/logs, y reintentos HTTP en `YamahaApiClient`. | `Features/Yamaha/YamahaSettings.cs`, `Features/Yamaha/YamahaV3Contracts.cs`, `Features/Yamaha/YamahaV3Validator.cs`, `Features/Yamaha/YamahaApiClient.cs`, `Features/Yamaha/FakeYamahaApiClient.cs` | **IMPLEMENTADO Y VERIFICADO** |
| **Entrega 2** | **Persistencia de Auditoría:** Entidad `YamahaSyncLog`, migración EF Core aditiva, índices y repositorio/DbContext. | `Domain/Entities/YamahaSyncLog.cs`, `Data/Migrations/*_AgregarYamahaSyncLog.cs`, `Data/ApplicationDbContext.cs` | **IMPLEMENTADO Y VERIFICADO** |
| **Entrega 3** | **Extractores PostgreSQL (AsNoTracking):** Extractores para Stock (`soloMarcaYamaha`), Ventas (`soloVentasMostrador`) y Servicio Técnico (transformación no destructiva `OS-` -> `OT-` y traducción determinística `TipoAtencion`). | `Services/Yamaha/Exportadores/IYamahaStockExporter.cs`, `YamahaStockExporter.cs`, `IYamahaVentasExporter.cs`, `YamahaVentasExporter.cs`, `IYamahaServicioTecnicoExporter.cs`, `YamahaServicioTecnicoExporter.cs` | **IMPLEMENTADO Y VERIFICADO** |
| **Entrega 4** | **Servicio de Watermarks y Orquestador Central:** Derivación de cursor temporal desde `YamahaSyncLogs` sin tablas adicionales, orquestación de extracción, cálculo de SHA-256, validación y auditoría. | `Services/Yamaha/IYamahaWatermarkService.cs`, `YamahaWatermarkService.cs`, `IYamahaSyncOrchestrator.cs`, `YamahaSyncOrchestrator.cs` | **IMPLEMENTADO Y VERIFICADO** |
| **Entrega 5** | **Scheduler, Endpoints Administrativos y Suite de Pruebas:** BackgroundService protegido por semáforo (inactivo si `Enabled = false`), endpoints de sincronización/diagnóstico/historial y suite inicial de 161 pruebas pasando. | `Services/Yamaha/YamahaSyncBackgroundService.cs`, `Features/Yamaha/YamahaEndpoints.cs`, `Program.cs`, `tests/CRMTeamBenavides.Api.Tests/*` | **IMPLEMENTADO Y VERIFICADO** |
| **Entrega 6** | **Robustez Técnica, Paginación y Resiliencia de Scheduler:** Programación diaria de Stock a hora de cierre local (`HoraCierreStockPeru`), paginación determinista por bloques en PostgreSQL (`TamanioLoteExportacion`), recuperación segura de ejecuciones `EnProceso` abandonadas (`TiempoExpiracionEnProcesoMinutos`), endpoint administrativo y 167 pruebas pasando. | `Features/Yamaha/YamahaSettings.cs`, `Services/Yamaha/Exportadores/*`, `Services/Yamaha/YamahaSyncOrchestrator.cs`, `Services/Yamaha/YamahaSyncBackgroundService.cs`, `Features/Yamaha/YamahaEndpoints.cs` | **IMPLEMENTADO Y VERIFICADO** |
| **Entrega 7** | **Corrección Final de Robustez (H-01, H-02, H-04):** Reutilización estricta de la colección validada en el envío (eliminando doble consulta a BD y garantizando integridad de `PayloadHash`), reintentos resilientes de Stock con enfriamiento configurable e idempotencia persistida sin bloqueo por simulaciones, y exclusión mutua concurrente a nivel de servicio en `YamahaSyncOrchestrator` (`SemaphoreSlim` in-process con liberación garantizada en `finally`). 172 pruebas pasando. | `Features/Yamaha/YamahaSettings.cs`, `Services/Yamaha/YamahaSyncOrchestrator.cs`, `Services/Yamaha/YamahaSyncBackgroundService.cs`, `tests/CRMTeamBenavides.Api.Tests/*` | **IMPLEMENTADO Y VERIFICADO** |

---

## 9. Estado Actual del Sistema y Próximos Pasos de Negocio

1. **Estado de Producción:** La integración permanece en **modo inactivo seguro** (`YamahaSettings.Enabled = false`). Ningún dato se envía a endpoints reales de Yamaha y ninguna conexión saliente es ejecutada.
2. **Desacoplamiento de Decisiones Pendientes:**
   - **D-01 (Unidades MC/ATV/OM):** El sistema exporta `"SP"` y no asume inventario de motos comerciales sin definición.
   - **D-02 (Repuestos Taller en Ventas):** El exportador de ventas soporta el flag `soloVentasMostrador` para conmutar fácilmente entre ambas alternativas sin rehacer código.
   - **D-03 (Costo vs Precio en Stock):** Mapeado a costo de adquisición configurable.
   - **D-04 (Notas de Crédito):** Excluidas de la proyección hasta implementar el tipo de comprobante formal.
   - **D-07 (Filtro Marca Yamaha):** El exportador de stock incluye el flag `soloMarcaYamaha`.
3. **Próximo Paso Requerido:** Resolver en la reunión de negocio los puntos D-01 a D-07 con Team Benavides y Yamaha, y cargar las `Function_Key` reales en User Secrets o variables de entorno del servidor.

---

## 10. Actualización de Robustez Técnica y Cierre de Implementación

En la fase de cierre técnico se resolvieron los tres hallazgos de robustez identificados sin afectar la arquitectura desacoplada ni resolver decisiones de negocio pendientes:

1. **Programación Automática Diaria de Stock (`YamahaSyncBackgroundService`):**
   - El worker en segundo plano evalúa en cada ciclo el horario local de Perú (`America/Lima`) mediante `HoraPeru`.
   - Se configuró la propiedad `HoraCierreStockPeru` (por defecto `"22:00"`).
   - Para evitar duplicaciones tras reinicios o desviaciones horarias, `YaSeEjecutoStockCierreHoyAsync` consulta `YamahaSyncLogs` para verificar si ya existe un cierre de Stock exitoso no simulado ejecutado a partir de la hora de cierre en el día local. Si ya existe, se omite la ejecución.
2. **Paginación Determinista en PostgreSQL y Control de Memoria:**
   - Los exportadores (`YamahaVentasExporter`, `YamahaStockExporter`, `YamahaServicioTecnicoExporter`) leen la base de datos en bloques de tamaño configurable (`TamanioLoteExportacion`, predeterminado en 500 registros) con ordenamiento determinista de doble clave:
     - Ventas: `OrderBy(v => v.Fecha).ThenBy(v => v.Id)`
     - Stock: `OrderBy(p => p.Codigo).ThenBy(p => p.Id)`
     - Servicio Técnico: `OrderBy(os => os.FechaIngreso).ThenBy(os => os.Id)`
   - Cada bloque materializa entidades EF Core con `AsNoTracking()`, las proyecta a DTOs v3 inmutables y libera los grafos para recolección de basura.
   - La lista completa de DTOs se consolida en un único payload JSON final para cumplir con el contrato de Azure Functions de Yamaha (que no admite payloads fraccionados).
3. **Recuperación Segura de Ejecuciones Abandonadas:**
   - Se implementó `RecuperarEjecucionesAbandonadasAsync` en el orquestador (`IYamahaSyncOrchestrator`), ejecutado al iniciar el servicio en segundo plano y expuesto mediante `POST /api/yamaha/recuperar-abandonadas`.
   - Recupera únicamente registros con `Estado == YamahaSyncEstado.EnProceso` cuya `FechaInicio` supere el umbral de expiración (`TiempoExpiracionEnProcesoMinutos`, por defecto 30 minutos).
   - Los registros recientes (< 30 minutos) permanecen intactos para proteger ejecuciones concurrentes o en vuelo.
   - Las ejecuciones expiradas se marcan como fallidas (`Estado = Fallido`, `Exitoso = false`), liberando el estado y garantizando que el cursor de `YamahaWatermarkService` no avance y el lote se reintente sin pérdida de datos.
4. **Verificación Automatizada:**
   - Se incrementó la suite de pruebas a **167 pruebas unitarias y de integración pasando exitosamente** (0 errores, 0 omitidas).
5. **Limitaciones Técnicas y Riesgos Externos Declarados:**
   - **Entorno de pruebas:** Las pruebas se ejecutan sobre `Microsoft.EntityFrameworkCore.InMemory`. No se ejecutaron contra un motor PostgreSQL real ni se aplicaron migraciones a bases activas.
   - **Decisiones pendientes:** Las definiciones D-01 a D-07 y la ambigüedad AMB-3.4-8 continúan pendientes de validación con gerencia y YMDP.
   - **Riesgo de idempotencia remota:** Ante fallos de red TCP o timeouts donde Yamaha haya procesado el payload pero la respuesta HTTP no llegue al CRM, el reintento podría generar duplicados si los endpoints de Yamaha operan mediante `INSERT` en lugar de `UPSERT`. Este comportamiento debe ser aclarado con YMDP.

---

## 11. Cierre de Hallazgos de Robustez H-01, H-02 y H-04

En la última revisión técnica previa a la aprobación funcional se subsanaron tres hallazgos específicos de robustez y resiliencia:

1. **H-01 — Reutilización Estricta de la Colección Validada en el Envío (`YamahaSyncOrchestrator`):**
   - **Problema previo:** En el Paso 6 del orquestador, el camino de envío real volvía a consultar la base de datos a través de los exportadores en lugar de enviar la colección ya extraída y validada en el Paso 2. Si durante ese lapso se insertaban o modificaban transacciones, el payload enviado a Yamaha difería del evaluado por el validador y del contenido del `PayloadHash` (SHA-256) auditado en `YamahaSyncLogs`.
   - **Corrección aplicada:** Se eliminó por completo la segunda llamada a `Exportar...Async` en el camino de envío. Las listas validadas (`loteVentasValidos`, `loteStockValidos`, `loteServicioTecnicoValidos`) obtenidas en el Paso 2 se transmiten directamente al cliente HTTP (`_apiClient`). Se garantiza correspondencia unívoca del 100 % entre los datos validados, el hash SHA-256 persistido en base de datos y los bytes transmitidos por la red.
2. **H-02 — Resiliencia en Reintentos de Stock e Idempotencia Diaria (`YamahaSyncBackgroundService`):**
   - **Problema previo:** Si el envío diario de Stock fallaba (por ejemplo por timeout de Azure Functions o indisponibilidad temporal de Yamaha), la variable en memoria `_ultimoDiaStockEjecutadoPeru` bloqueaba todo intento posterior hasta el día siguiente.
   - **Corrección aplicada:** Se sustituyó el flag genérico por `_ultimoDiaStockExitosoPeru` y `_ultimoIntentoStockFallidoUtc`. Si un intento falla, el scheduler programa reintentos en la misma jornada respetando un periodo de enfriamiento configurable (`IntervaloReintentoStockMinutos`, por defecto 15 minutos). Si la sincronización culmina con éxito, queda registrada tanto en memoria como en la tabla `YamahaSyncLogs`, impidiendo duplicados incluso tras reinicios del proceso. Asimismo, `YaSeEjecutoStockCierreHoyAsync` descarta ejecuciones simuladas (`EsSimulacion == true`), garantizando que pruebas en modo dry-run no impidan el cierre real posterior.
   - **Anotación de negocio:** La hora de cierre configurada (`HoraCierreStockPeru = "22:00"`) se mantiene como valor por defecto configurable sujeto a ratificación formal en la reunión de negocio.
3. **H-04 — Exclusión Mutua Concurrente a Nivel de Servicio (`YamahaSyncOrchestrator`):**
   - **Problema previo:** No existía protección ante ejecuciones simultáneas del mismo servicio disparadas concurrentemente (por ejemplo, un operador invocando manualmente `POST /api/yamaha/sincronizar/Ventas` mientras el worker en segundo plano iniciaba el ciclo programado).
   - **Corrección aplicada:** Se implementó un diccionario estático y concurrente de semáforos (`ConcurrentDictionary<YamahaServicioSincronizacion, SemaphoreSlim>`) en `YamahaSyncOrchestrator`. Cada solicitud adquiere el semáforo de su servicio específico con un tiempo de espera configurable (`ConcurrenciaEsperaSegundos`, por defecto 5 segundos). Si el semáforo no se obtiene dentro de la ventana de espera, la solicitud se rechaza limpiamente con mensaje descriptivo y no genera ejecuciones solapadas. La liberación del semáforo se realiza de forma estricta en un bloque `finally`.
   - **Alcance y limitación técnica:** Esta sincronización opera en proceso (*in-process*) y protege concurrentemente todas las instancias con ámbito (*Scoped*) dentro de un único proceso de API. Si en el futuro el CRM se despliega en arquitectura horizontal multirréplica (ej. múltiples contenedores o pods de Kubernetes), este mecanismo deberá ser complementado con un cerrojo distribuido (ej. cerrojos consultivos de PostgreSQL `pg_advisory_lock` o Redis distribuido).
4. **Verificación Automatizada:**
   - La suite de pruebas unitarias y de integración se amplió a **172 pruebas automatizadas pasando al 100 %** (0 fallos, 0 omitidas), incluyendo pruebas de reintento de stock, idempotencia ante reinicios y simulaciones, reutilización de lote sin reconsulta y rechazo por concurrencia.
