# Resumen de cambios y nuevas funcionalidades

Anotaciones de la reunión con Team Benavides del 25/09/2026, ordenadas por módulo. Son la base del plan de implementación ([plan-implementacion.md](plan-implementacion.md)).

## 1. Roles y permisos — se cambia

Se definen definitivamente los roles:

- **Gerencia/Admin:** administra usuarios, roles y permisos; tiene acceso total y aprueba los cambios importantes de las OS.
- **Recepción:** gestión operativa de clientes, unidades y OS según permisos.
- **Técnico:** solo sus OS asignadas; registra diagnóstico, servicios, repuestos, mano de obra y avances. **No modifica precios ni aprueba finalmente.**
- **Vendedor:** gestiona ventas y comprobantes de sus ventas. **No modifica precios ni descuentos.**
- **Cliente:** solo puede ver sus unidades, OS, estados, documentos y comprobantes; puede aprobar o rechazar presupuestos cuando corresponda.

**Se agrega autorización real mediante policies y permisos en el backend**, no solamente ocultando botones.

## 2. Portal del cliente — se agrega

- Rol Cliente real.
- Relación Usuario → Cliente.
- Activación mediante DNI + código generado por personal autorizado.
- Mis unidades.
- Mis OS.
- Seguimiento y timeline de la OS.
- Fecha estimada de entrega.
- Cotizaciones y presupuestos.
- Aprobar o rechazar el presupuesto.
- Comprobantes.
- Historial de servicios.
- Chatbot y ayuda.
- El cliente solo puede consultar sus propios datos.

Las pantallas de la propuesta anterior **se mantienen como referencia visual** ([referencias/app-cliente.html](referencias/app-cliente.html)).

## 3. Órdenes de servicio — se cambia y amplía

- Mantener los estados actuales.
- Agregar aprobación de Gerencia.
- Agregar historial o timeline de estados con usuario y fecha y hora.
- Asignación de técnico.
- El técnico marca avance y trabajo realizado, pero no la aprobación final.
- Agregar fecha y hora de ingreso y de salida.
- Agregar fecha estimada de entrega.
- Mejorar la búsqueda por OS, placa, serie, modelo, cliente, fecha, km u horas, etc.
- Conectar la OS con cliente, unidad, servicios, repuestos, mano de obra, cotización y comprobante.
- Separar **servicios + repuestos + mano de obra**.

## 4. Servicios — se agrega

En «Agregar producto o servicio» permitir:

- Buscar y seleccionar un repuesto.
- Agregar un servicio manual.
- Descripción del servicio.
- Cantidad cuando corresponda.
- Precio.
- Mano de obra.
- IGV.
- Total automático.
- Almacén o destino para repuestos.

**El servicio no requiere código obligatorio.**

## 5. Unidades / vehículos — se cambia

La unidad ya no depende únicamente de la placa.

Agregar:

- Tipo de unidad.
- Placa opcional.
- VIN o serie.
- Número de motor.
- Tipo de medidor: km u horas.
- Valor del medidor.
- Historial de placa o unidad.

Debe soportar motos, motos acuáticas, generadores y otros tipos.

## 6. Inventario / repuestos — se cambia

- Usar el concepto **Repuestos** donde corresponda.
- Alertas de stock configurables.
- El valor inicial solicitado es 4 unidades, pero debe poder cambiarse por repuesto o categoría.
- Mantener movimientos, kardex y control de stock.
- Permitir modificar el stock según permisos.
- Corregir un posible **doble descuento de stock** cuando un repuesto participa en una OS y en una venta.

## 7. Ventas — se amplía

Agregar:

- Métodos de pago.
- Servicios y mano de obra cuando corresponda.
- IGV.
- Totales automáticos.
- Relación Venta ↔ OS.
- Adelantos y saldo.
- Pedidos de Lima.
- Tiempo y estado de entrega.
- Comprobante relacionado.

**El vendedor no puede modificar precios ni aplicar descuentos.**

## 8. Comprobantes — se cambia

Mejorar la estructura para incluir:

- Relación con OS y venta.
- Productos y repuestos.
- Servicios.
- IGV.
- Método de pago.
- Total.
- Observaciones manuales.

Validar después el alcance de SUNAT, OSE o PSE, series y correlativos.

## 9. Ganancias — se agrega

Nuevo módulo o reporte de rentabilidad:

**Ganancia = precio de venta − costo**

Permitir el análisis por venta, repuesto y período.

Pendiente confirmar si los servicios y la mano de obra entran en el cálculo de la utilidad.

## 10. Caja — se agrega

- Ingresos.
- Adelantos.
- Egresos.
- Gastos y motivo.
- Movimientos.
- Saldo.
- Reportes de caja.

## 11. Citas / agenda — se agrega

- Solicitud de citas.
- Fecha, hora o rango.
- Cliente y unidad.
- Motivo.
- Agenda del personal.
- Bloqueo de horarios no disponibles.
- Flujo de aprobación según permisos.

## 12. Configuración — se amplía

Agregar configuración de negocio para:

- Datos de la empresa.
- Catálogos.
- IGV.
- Umbrales de stock.
- Series y correlativos, si corresponden.
- Parámetros generales.
- Tipo de cambio.

## 13. Tipo de cambio — se agrega

Registro manual diario PEN/USD:

- Fecha.
- Valor.
- Usuario.
- Fecha y hora.

**No se requiere integración automática externa.**

## 14. Formato de atención — se adapta

La OS debe incorporar la información necesaria del formato entregado por el cliente:

- Datos del cliente.
- Datos de la unidad.
- Fechas y horarios.
- Tipo de servicio.
- Falla.
- Diagnóstico y solución.
- Repuestos.
- Servicios.
- Servicios de terceros.
- Observaciones y recomendaciones.
- Firmas cuando corresponda.

Definir qué campos serán estructurados y cuáles quedarán solo en el documento.

## 15. Reportes / dashboard — se amplía

Agregar reportes de:

- Ganancias.
- Caja.
- Stock bajo.
- OS.
- Ventas.

Respetando los permisos por rol.

## 16. Auditoría — se amplía

Registrar quién y cuándo realiza acciones importantes:

- Aprobaciones.
- Cambios de estado de la OS.
- Cambios de stock.
- Cambios de precios.
- Anulaciones.
- Configuración.

## 17. Seguridad — se cambia

Implementar control real en el backend para impedir:

- Que un cliente vea a otro cliente.
- Que un técnico vea OS no asignadas.
- Que un técnico modifique precios.
- Que un vendedor modifique precios o descuentos.
- Que otros usuarios ejecuten acciones de Gerencia.

## 18. Pendientes técnicos a completar

- Recuperación de contraseña en el backend.
- Fotos de la OS.
- Integración real con Yamaha cuando existan credenciales y documentación.
- Hangfire y trabajos de Yamaha, si corresponde.
- Migración desde Excel.
- Backup y script de migración.
- Validación final de producción y UAT.

## Resumen para aprobación

**Se agrega:** portal del cliente, rol Cliente, aprobaciones de Gerencia, permisos reales, historial de la OS, servicios manuales, mano de obra, nuevos datos de unidades, stock configurable, ganancias, caja, citas, tipo de cambio, adelantos, mejoras de ventas y comprobantes y nuevas reglas de seguridad.

**Se cambia:** modelo de unidades, flujo de la OS, permisos por rol, cálculo y estructura de servicios y ventas, comprobantes, alertas de stock, relación OS–venta–comprobante y control de stock para evitar el doble descuento.

**Se mantiene:** pantallas del portal del cliente como referencia visual, estados actuales de la OS y funcionalidades ya operativas, salvo las ampliaciones indicadas arriba.
