# App móvil · CRM Team Benavides

App en Flutter para Android e iOS. Consume la misma API que la web
(`src/CRMTeamBenavides.Api`), sin reglas de negocio propias.

## Qué hay hoy

- Login contra `POST /api/auth/login`, con renovación automática del token y
  cierre de sesión cuando el refresh ya no sirve.
- La sesión se guarda en el almacenamiento seguro del dispositivo, así que la app
  no vuelve a pedir la contraseña al reabrirse.
- Navegación con go_router y guardia de sesión: sin sesión, solo el login.
- Clientes: lista con búsqueda y ficha con sus unidades.
- Unidades: lista con búsqueda.

Todo es de solo lectura por ahora. Crear y editar se hace desde la web.

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
| `lib/pantallas` | Pantallas: login, inicio, clientes, ficha de cliente y unidades |
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
- Agregar repuestos y cambiar el estado de la orden se hacen desde la web.
- Android permite tráfico HTTP sin cifrar solo en la compilación de depuración
  (`android/app/src/debug/AndroidManifest.xml`). En producción la API va por
  HTTPS.
