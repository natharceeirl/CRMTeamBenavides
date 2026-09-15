# Prompt para Claude Design · Mockup Team Benavides

## Cómo usarlo

1. Crea un proyecto en Claude Design y adjunta el `logo.svg` oficial y las capturas del sitio web.
2. Pega el prompt de la sección siguiente.
3. Valida el sistema de diseño y las tres pantallas clave antes de pedir el resto.

Pantallas para una segunda ronda: ajustes de inventario, reportes, bandeja de WhatsApp, configuración, auditoría e importación de datos (web); buscar unidad y consulta de stock (técnico); historial, cotizaciones y comprobantes, contacto y perfil (cliente).

## Prompt

```text
Diseña el mockup en alta fidelidad de la plataforma de gestión de Team Benavides, concesionario autorizado Yamaha en Arequipa (Perú). La plataforma administra el taller de servicio post-venta y la tienda de repuestos. Todo el texto de la interfaz va en español de Perú.

CANALES
1. Web administrativa (escritorio, 1440 px). La usa el personal del taller y la tienda durante toda la jornada: navegación lateral fija, buscador global y tablas densas.
2. App móvil (390 × 844, Android e iOS). Es una sola app con dos perfiles según quién inicia sesión:
   - Técnico: trabaja sus órdenes dentro del taller, a veces con guantes. Botones grandes, fotos con un toque y pocos pasos.
   - Cliente: sigue el estado de su unidad y aprueba presupuestos. Simple y claro.

IDENTIDAD VISUAL
- Logo adjunto: «TEAM» en rojo #FF0000 y «Benavides» en negro. No lo redibujes ni lo deformes. Como aún no hay versión negativa, colócalo solo sobre fondos claros.
- Colores: rojo de marca #FF0000 para barras, encabezados y superficies de marca; rojo #D70000 para botones, enlaces y texto rojo; #B30000 para hover y presionado; negro #000000; blanco; gris de fondo #EEEEEE.
- El rojo de marca no se usa para estados. Los errores llevan ícono y texto, no solo color.
- Tipografía: Barlow Condensed para títulos, menús y etiquetas en mayúsculas, como en el sitio web; Barlow para textos, formularios y tablas. Números tabulares en montos y cantidades.
- Carácter: contundente como el sitio de Team Benavides (rojo, negro y tipografía condensada), pero sobrio en las pantallas de trabajo. El rojo se reserva para la marca y las acciones principales.
- Ícono de la app: usa un marcador provisional que diga «Ícono pendiente».

SISTEMA DE DISEÑO (primero)
- Componentes: botones (primario, secundario, fantasma, peligro), campos, selects, buscador, carga de fotos, tablas con filtros y paginación, tarjetas de unidad, línea de tiempo, pestañas, modales, avisos, navegación lateral (web), barra inferior (app) y estados vacío, cargando y error.
- Chips de estado de la orden, cada uno con un color propio distinto del rojo de marca: Recepción, Diagnóstico, Esperando aprobación, En reparación, Esperando repuesto, Control de calidad, Lista para entrega, Entregada y Anulada.
- Indicador de stock: disponible, bajo o agotado.
- Medidor de la unidad: muestra «km» u «h» según el tipo de unidad.

PANTALLAS
Web administrativa:
1. Inicio de sesión.
2. Dashboard: órdenes por estado, ventas del día, stock crítico y conversaciones de WhatsApp pendientes.
3. Clientes: lista con búsqueda por nombre, DNI o RUC, teléfono o placa.
4. Ficha de cliente: datos, unidades, historial de órdenes y ventas, y botón «Invitar a la app».
5. Ficha de unidad: tipo, marca, modelo, año, VIN o número de serie, número de motor, placa opcional, medidor, historial de servicios y panel «Garantía Yamaha».
6. Órdenes: lista filtrable por estado y técnico.
7. Nueva orden (recepción): cliente, unidad, motivo, lectura del medidor, accesorios entregados y fotos de ingreso.
8. Detalle de orden: diagnóstico con fotos, presupuesto de servicios y repuestos, técnico asignado, línea de tiempo de estados y cierre.
9. Cotización: detalle, descuentos, vigencia y botón «Convertir en venta».
10. Venta de mostrador: productos, pago y registro del comprobante.
11. Productos: lista con stock y alertas.
12. Ficha de producto: código interno, código de parte Yamaha, precios y kardex.
13. Entrada de mercadería.
14. Registro de comprobante: boleta o factura, serie y número, cliente, operación gravada, IGV 18 %, total y estado.
15. Usuarios y roles: matriz de permisos por módulo.

App, perfil Técnico:
16. Inicio de sesión.
17. Mis órdenes, agrupadas por estado.
18. Detalle de orden.
19. Diagnóstico: hallazgos, fotos, servicios y repuestos propuestos.
20. Avance: tareas, repuestos usados y cambio de estado.

App, perfil Cliente:
21. Activar cuenta e iniciar sesión.
22. Mis unidades, con la orden en curso destacada.
23. Orden en curso: línea de tiempo y fecha estimada.
24. Aprobar presupuesto: detalle, total con IGV y botones «Aprobar» y «Rechazar».

PROTOTIPO
Conecta las pantallas para recorrer este flujo: Nueva orden (web) → Diagnóstico (técnico) → Detalle de orden con presupuesto (web) → Aprobar presupuesto (cliente) → Avance (técnico) → Detalle de orden en «Lista para entrega» (web) → Orden en curso (cliente).

DATOS DE EJEMPLO
- Clientes: Luis Quispe Mamani (DNI 45872310), Carla Zegarra Paredes (DNI 70125564), Agroindustrias del Sur S.A.C. (RUC 20601234567).
- Unidades: Yamaha MT-03 2023, motocicleta, 12 480 km; Yamaha Grizzly 700 2022, cuatrimoto, 3 210 km; Yamaha WaveRunner VX Cruiser 2021, moto acuática, 146 h; Yamaha EF2000iS, generador, 820 h.
- Personal: asesora Milagros Torres; técnicos Jorge Huamán y Ricardo Ccori.
- Orden OT-000482: MT-03 de Luis Quispe Mamani, mantenimiento de 12 000 km y ruido en frenos, técnico Jorge Huamán, estado «Esperando aprobación».
- Presupuesto de la OT-000482 (precios con IGV):
  - Aceite Yamalube 10W-40 1 L × 3: S/ 126.00
  - Filtro de aceite × 1: S/ 38.00
  - Pastillas de freno delanteras × 1: S/ 165.00
  - Mano de obra, mantenimiento 12 000 km: S/ 120.00
  - Mano de obra, revisión de frenos: S/ 37.00
  - Operación gravada S/ 411.86 · IGV 18 % S/ 74.14 · Total S/ 486.00

NO INCLUIR
Agenda de citas, pagos en línea, venta de motos nuevas, compras a proveedores, emisión electrónica ante SUNAT ni respuestas con IA. Por ahora tampoco diseñes reportes, configuración, auditoría ni la bandeja de WhatsApp.

ORDEN DE ENTREGA
1. Sistema de diseño.
2. Dashboard (web), Detalle de orden (web) y Orden en curso (cliente), para validar el estilo.
3. El resto de pantallas y el prototipo.
```
