# App móvil · CRM Team Benavides

App en Flutter para Android e iOS. Consume la misma API que la web
(`src/CRMTeamBenavides.Api`), sin reglas de negocio propias.

## Qué hay hoy

- Login con correo o DNI contra `POST /api/auth/login`, con renovación
  automática del token y cierre de sesión cuando el refresh ya no sirve.
- La sesión se guarda en el almacenamiento seguro del dispositivo, así que la app
  no vuelve a pedir la contraseña al reabrirse.
- Navegación con go_router y guardia de sesión: sin sesión, solo el login.
- Tablero con el resumen de `/api/dashboard/resumen`.
- Órdenes: lista con búsqueda, filtro «solo en taller» y ficha de la orden, donde
  el técnico registra el diagnóstico, cambia el estado, agrega repuestos y
  servicios y sube fotos con la cámara o la galería. El precio de un repuesto o
  servicio se puede cambiar; si no es el de lista, la orden queda pendiente de
  aprobación de Gerencia.
- Portal del cliente: sus órdenes con el avance, el presupuesto para aprobar o
  rechazar, sus comprobantes, sus citas y sus pedidos a Lima.
- Tienda, que agrupa las dos caras del mostrador:
  - Repuestos: búsqueda por código, nombre o categoría, filtro de bajo stock,
    filtro por categoría y ficha con los últimos movimientos.
  - Ventas: búsqueda por cliente o referencia, filtro por estado, cuánto suman
    las vigentes y ficha con el detalle y el comprobante.
- Clientes: lista con búsqueda y ficha con sus unidades.
- Unidades: lista con búsqueda.
- Chatbot: consulta de FAQs y derivación a un asesor.
- Cuenta: cambiar la contraseña y abrir la política de privacidad de la web, que
  también se abre desde el login. El cliente además puede eliminar su cuenta, como
  lo exigen Google Play y Apple; al personal lo da de baja Gerencia.

Fuera de la orden y las citas, la app es de consulta. Crear y editar el resto se
hace desde la web, que es donde vive la operación completa.

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

Para compilar y probar en iPhone (simulador, iPhone propio y TestFlight) ver
[docs/guia-iphone.md](../../docs/guia-iphone.md): hace falta una Mac.

## Publicar

### Compilar la versión de release

```
flutter build appbundle --release --dart-define=API_URL=https://app.teambenavides.com
```

`build.gradle.kts` frena la compilación antes de empezar si falta la firma (abajo) o si `API_URL` no está o no empieza con `https://`. Una versión de release sin `API_URL` apuntaría en silencio a `10.0.2.2`, que en un teléfono no lleva a ningún lado. iOS se compila en un Mac, fuera de Gradle: ahí, al arrancar, la app muestra qué falta en vez de abrir el login.

### Firma

La versión de release nunca se firma con la clave de depuración: un APK firmado así no se puede actualizar después con otra clave.

1. Quien guarda las credenciales del proyecto genera la clave de subida una sola vez, fuera del repositorio:

   ```
   keytool -genkeypair -keystore team-benavides-subida.jks -storetype PKCS12 -keyalg RSA -keysize 2048 -validity 10000 -alias subida
   ```

   El archivo y sus contraseñas van a un lugar seguro con copia, como un gestor de contraseñas. En Play Console se activa Play App Signing: Google guarda la clave con la que firma la app que se instala, y esta es solo la de subida. Si se pierde, Google permite cambiarla.
2. Cada PC que publica crea `android/key.properties`, que git ignora:

   ```
   storeFile=C:/ruta/fuera/del/repositorio/team-benavides-subida.jks
   storePassword=…
   keyAlias=subida
   keyPassword=…
   ```

Se probó el 20/10 con una clave desechable:
- sin `key.properties`, sin `API_URL` o con `http://`, la compilación se detuvo con su mensaje;
- con todo en orden, el APK salió firmado con esa clave y no con la de depuración.

### Identificador de la app

Todavía no se cambia: lo decide el Ingeniero. Una vez publicada la app, no se puede cambiar. Hoy Android e iOS ni siquiera coinciden:

| Dónde | Valor actual |
|---|---|
| Android: `applicationId` y `namespace` en `android/app/build.gradle.kts`, y la carpeta de `MainActivity.kt` | `pe.natharce.crm_team_benavides` |
| iOS: `PRODUCT_BUNDLE_IDENTIFIER` en `ios/Runner.xcodeproj/project.pbxproj`, en las tres configuraciones | `pe.natharce.crmTeamBenavides` |
| Firebase: la app registrada en el proyecto, de la que salen `google-services.json` y `GoogleService-Info.plist` | El de Android |

Al cambiarlo hay que tocar todos esos lugares a la vez y registrar la app de nuevo en el proyecto de Firebase de la empresa.

### Lo que piden las tiendas

- **Eliminar la cuenta desde la app:** está en el menú del cliente. El enlace web que pide Google es `https://<dominio>/privacidad#eliminar-cuenta`.
- **Política de privacidad:** `https://<dominio>/privacidad`. Es un borrador; faltan los datos de la empresa y la revisión legal.

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
- **Clave de subida:** la genera quien guarda las credenciales (ver «Publicar»).
- **Cuenta de Google Play** (pago único) y **cuenta de Apple Developer**, que
  además exige un Mac para compilar iOS.
- **Identificador de la app:** lo decide el Ingeniero antes de publicar (ver «Publicar»).
- **Menús por rol:** la app ya recibe los roles desde `/api/auth/me`, pero no
  esconde nada todavía; falta la matriz de permisos del cliente.
- La tienda en la app es consulta: registrar entradas, salidas y ajustes de
  stock, y crear, confirmar o anular una venta y su comprobante, se hacen desde
  la web.
- **Catálogos grandes:** repuestos y ventas traen la lista completa y filtran en
  el dispositivo, como el resto de la app. `GET /api/productos` acepta
  `categoriaId`, `busqueda` y `bajoStock`, y `GET /api/ventas` acepta `estado`,
  `clienteId` y rango de fechas; si los datos reales crecen, conviene pasar el
  filtrado al servidor.
- Android permite tráfico HTTP sin cifrar solo en la compilación de depuración
  (`android/app/src/debug/AndroidManifest.xml`). En producción la API va por
  HTTPS.
