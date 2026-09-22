# App móvil · CRM Team Benavides

App en Flutter para Android e iOS. Consume la misma API que la web
(`src/CRMTeamBenavides.Api`), sin reglas de negocio propias.

## Qué hay hoy

- Login contra `POST /api/auth/login`, con renovación automática del token y
  cierre de sesión cuando el refresh ya no sirve.
- La sesión se guarda en el almacenamiento seguro del dispositivo, así que la app
  no vuelve a pedir la contraseña al reabrirse.
- Navegación con go_router y guardia de sesión: sin sesión, solo el login.
- Tablero con el resumen de `/api/dashboard/resumen`.
- Órdenes: lista con búsqueda, filtro «solo en taller» y ficha de la orden, donde
  el técnico puede registrar el diagnóstico.
- Tienda, que agrupa las dos caras del mostrador:
  - Repuestos: búsqueda por código, nombre o categoría, filtro de bajo stock,
    filtro por categoría y ficha con los últimos movimientos.
  - Ventas: búsqueda por cliente o referencia, filtro por estado, cuánto suman
    las vigentes y ficha con el detalle y el comprobante.
- Clientes: lista con búsqueda y ficha con sus unidades.
- Unidades: lista con búsqueda.
- Chatbot: consulta de FAQs y derivación a un asesor.

Salvo el diagnóstico de la orden, todo es de solo lectura. Crear y editar se hace
desde la web, que es donde vive la operación completa.

## Cómo correrla

La API tiene que estar levantada (ver la raíz del repositorio). Después:

```
flutter run
```

En el emulador de Android la app apunta sola a `http://10.0.2.2:5021`, que es
como el emulador ve el `localhost` del PC. En web y escritorio apunta a
`http://localhost:5021`.

Con un teléfono físico hay que indicar la IP del PC en la red:

```
flutter run --dart-define=API_URL=http://192.168.1.50:5021
```

El teléfono y el PC tienen que estar en la misma red, y el firewall de Windows
debe dejar pasar el puerto 5021.

## Estructura

| Carpeta | Qué contiene |
|---|---|
| `lib/api` | Cliente HTTP con Dio, modelos de los contratos del backend y almacenamiento de la sesión |
| `lib/auth` | Estado de sesión con Riverpod y lectura de los claims del token |
| `lib/pantallas` | Pantallas: login, inicio, tablero, órdenes, tienda (repuestos y ventas), clientes, unidades y chatbot |
| `lib/rutas.dart` | Rutas y guardia de sesión |
| `lib/tema.dart` | Colores y tipografías de marca, en un solo archivo, como en la web |
| `assets/fonts` | Space Grotesk para títulos e IBM Plex Sans para texto |

## Pendientes

- **Ícono de la app:** sigue el de Flutter. Falta el logo en PNG del Ingeniero.
- **Firma de release:** `android/app/build.gradle.kts` firma con la clave de
  depuración. Para publicar hace falta un keystore propio, con sus credenciales
  fuera del repositorio.
- **Cuenta de Google Play** (pago único) y **cuenta de Apple Developer**, que
  además exige un Mac para compilar iOS.
- **Identificador de la app:** Android usa `pe.natharce.crm_team_benavides` y
  iOS `pe.natharce.crmTeamBenavides`. Conviene unificarlos antes de publicar,
  porque después no se pueden cambiar.
- **Menús por rol:** la app ya recibe los roles desde `/api/auth/me`, pero no
  esconde nada todavía; falta la matriz de permisos del cliente.
- Agregar repuestos y cambiar el estado de la orden se hacen desde la web. La
  tienda en la app es consulta: registrar entradas, salidas y ajustes de stock,
  y crear, confirmar o anular una venta y su comprobante, también.
- **Catálogos grandes:** repuestos y ventas traen la lista completa y filtran en
  el dispositivo, como el resto de la app. `GET /api/productos` acepta
  `categoriaId`, `busqueda` y `bajoStock`, y `GET /api/ventas` acepta `estado`,
  `clienteId` y rango de fechas; si los datos reales crecen, conviene pasar el
  filtrado al servidor.
- Android permite tráfico HTTP sin cifrar solo en la compilación de depuración
  (`android/app/src/debug/AndroidManifest.xml`). En producción la API va por
  HTTPS.
