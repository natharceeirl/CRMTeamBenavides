# Módulo de compras

Registro de las compras a proveedores: el comprobante, los repuestos que entran al stock, el costo de cada repuesto y lo que se le paga al proveedor. Reemplaza la entrada manual de stock con motivo «Compra a proveedor».

> **Cambio de alcance.** «Compras a proveedores» estaba en la lista de fuera de alcance. Paolo autorizó construirlo el 10/10; falta registrarlo con el cliente.

## Qué se toma del sistema que usan hoy

| Su formulario | En el nuestro |
|---|---|
| Tipo de comprobante, serie y número | Igual. Una factura no se registra dos veces: mismo proveedor, tipo, serie y número. |
| Fecha de emisión y de vencimiento | Igual. Con saldo y vencimiento pasado, la compra figura como vencida. |
| Proveedor y «+ Nuevo» | Tabla de proveedores con alta desde la misma compra. |
| Moneda y tipo de cambio | Soles o dólares. Se propone el tipo de cambio vigente y se puede corregir con el de la factura. |
| Porcentaje de IGV | Se propone el de Configuración. |
| Información adicional | Guía de remisión y observaciones. |
| Agregar cliente | Se reemplaza por el pedido a Lima al que corresponde la compra (ver más abajo). |
| Agregar pagos | Pagos al registrar la compra o después. |
| Producto o servicio y «+ Nuevo» | Repuesto del catálogo, con alta rápida, u otro concepto libre como flete o servicio externo. |
| Afectación de IGV | Gravado, exonerado o inafecto. |
| Cantidad, precio unitario y total | Igual. Una casilla dice si los precios incluyen IGV. |
| Código de barras | El lector escribe el código en el buscador del repuesto y selecciona el que coincide. |
| Almacén de destino | No va todavía: depende de almacenes. |
| Atributos UBL 2.1 | No va: es de la factura electrónica, que está fuera de alcance. |

## Reglas

### Registrar una compra

Todo en una sola transacción: si algo falla, no queda nada a medias.

1. **Montos de cada línea**, en la moneda de la compra:
   - Precios sin IGV: subtotal = cantidad × precio; IGV = subtotal × % IGV.
   - Precios con IGV: total = cantidad × precio; subtotal = total ÷ (1 + % IGV); IGV = total − subtotal.
   - Exonerado o inafecto: sin IGV.
   - Los totales de la compra son la suma de las líneas, redondeadas a dos decimales.
2. **Stock:** cada repuesto suma su cantidad con el bloqueo `FOR UPDATE` y deja un movimiento de entrada ligado a la compra. Los conceptos libres no mueven stock.
3. **Costo unitario en soles** de cada repuesto:
   - Factura: el subtotal sin IGV, porque el IGV es crédito fiscal.
   - Boleta, ticket, nota de venta, recibo u otro: el total con IGV, porque ese IGV no se recupera.
   - En dólares se multiplica por el tipo de cambio de la compra.
4. **Costo del repuesto**, según el método de Configuración:
   - Promedio ponderado (por defecto): (stock × costo actual + cantidad × costo de compra) ÷ (stock + cantidad).
   - Último costo: el de la compra.
   - En los dos métodos, si el repuesto no tenía stock o tenía costo 0, toma el costo de la compra.
   - Una línea con precio 0, como una bonificación, nunca deja el costo en 0. En el promedio solo baja el costo de lo que ya había en stock.
   - La línea guarda el costo anterior y el que dejó, para poder deshacerlo al anular.
5. **Pagos iniciales** (opcional): siguen las reglas de «Pagar».

### Compra de un pedido a Lima

El pedido a Lima ya suma su stock cuando se marca «Recibido». Una compra ligada a un pedido registra el comprobante y la deuda con el proveedor, pero **no mueve stock ni costo**; si lo hiciera, el repuesto entraría dos veces. No se liga a un pedido cancelado.

### Pagar al proveedor

- El monto va en la moneda de la compra y no puede pasar el saldo.
- **Efectivo:** sale de la caja abierta como egreso, en soles con el tipo de cambio de la compra. Exige caja abierta y saldo suficiente.
- **Otro método:** queda en el pago, sin movimiento de caja. La caja solo cuenta lo que hay en el cajón.
- Cada pago bloquea la compra y la caja: dos pagos a la vez no se pasan del saldo.
- Estado de pago: pendiente, parcial o pagada. Vencida si tiene saldo y la fecha de vencimiento ya pasó.

### Corregir

- **Datos del comprobante:** serie, número, fechas, proveedor, guía y observaciones se pueden editar mientras la compra esté registrada.
- **Tipo de comprobante, líneas, moneda, tipo de cambio o IGV:** no se editan, porque ya movieron stock, costo y pagos; el tipo define si el IGV es costo. Se anula la compra y se registra de nuevo.
- **Anular un pago:** exige motivo. Si fue en efectivo, el dinero vuelve como ingreso a la caja abierta; nunca se toca una caja cerrada.
- **Anular la compra:** exige motivo. Saca del stock lo que entró, anula sus pagos y devuelve el costo anterior. El costo no se toca si después hubo otra compra vigente del mismo repuesto o alguien lo editó a mano. No se puede anular si alguno de esos repuestos ya no tiene stock suficiente.

### Permisos

| Permiso | Qué permite |
|---|---|
| `compras.ver` | Ver compras, pagos y proveedores. Muestra costos. |
| `compras.registrar` | Registrar y corregir compras, pagar y administrar proveedores. |
| `compras.anular` | Anular compras y pagos. |

Por defecto solo los tiene Gerencia. Se asignan a otro rol desde Usuarios; para elegir repuestos ese rol también necesita `inventario.ver`, y para registrar uno nuevo desde la compra, `inventario.crear`.

## Dónde está

- API: `Features/Compras` (endpoints y contratos), `Services/CompraService.cs`, `Services/ProveedorService.cs` y `Services/CalculoCompra.cs`, con los cálculos sin base de datos.
- Base de datos: migración `AgregarComprasYProveedores`. Tablas `Proveedores`, `Compras`, `DetallesCompra` y `PagosCompra`; columnas `CompraId` en `MovimientosInventario`, `PagoCompraId` en `MovimientosCajaChica` y `MetodoCosteo` en `ConfiguracionesEmpresa`.
- Web: `/compras` (lista, deuda y proveedores) y `/compras/nueva`. El método de costeo se elige en Configuración.

## Preguntas abiertas para el cliente

Lo construido funciona con los valores por defecto; las respuestas solo cambian configuración o permisos.

1. ¿Quién registra las compras? Por defecto, solo Gerencia.
2. ¿Costo promedio o último costo? Por defecto, promedio; se cambia en Configuración.
3. ¿Pagan a proveedores con la caja chica o desde el banco? Las dos formas funcionan.
4. ¿Para qué usan hoy «Agregar cliente»?
5. ¿Sus repuestos traen código de barras distinto del código interno?
6. ¿Hay devoluciones o notas de crédito de proveedores? Hoy se resuelve anulando la compra.
