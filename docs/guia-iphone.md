# Probar la app en iPhone desde una Mac

Cómo compilar la app de Flutter (`src/movil`) para iOS, probarla en el simulador y en un iPhone, y enviarla a TestFlight. Desde Windows no se puede compilar para iOS: hace falta una Mac.

## Resumen

| Para | Hace falta | Costo |
|---|---|---|
| Probar en el simulador | Mac con Xcode | Gratis |
| Probar en un iPhone propio, por cable o wifi | Mac, Xcode y un Apple ID | Gratis. La app instalada vence a los 7 días |
| Repartirla a otros iPhone (TestFlight) | Cuenta de Apple Developer | US$99 al año |
| Publicarla en el App Store | La misma cuenta y la revisión de Apple | Incluido en la cuenta |

La cuenta de Apple Developer debe estar a nombre de Team Benavides. Apple pide que la empresa sea persona jurídica, tenga número D-U-N-S, un correo con el dominio de la empresa y un sitio web público. Quién abre esa cuenta sigue entre las decisiones pendientes del CLAUDE.md.

## 1. Preparar la Mac (una sola vez)

1. **Xcode:** instalarlo desde el App Store y abrirlo una vez. En Xcode → Settings → Components, descargar la plataforma iOS y un simulador.
2. **Herramientas de línea de comandos:**

   ```bash
   xcode-select --install
   sudo xcodebuild -license accept
   xcodebuild -runFirstLaunch
   ```

3. **CocoaPods**, que instala los plugins nativos de iOS:

   ```bash
   brew install cocoapods
   ```

4. **Flutter 3.47.4.** Es la versión del equipo; no usar otra para no cambiar el `pubspec.lock`:

   ```bash
   git clone https://github.com/flutter/flutter.git -b 3.47.4 ~/flutter
   echo 'export PATH="$HOME/flutter/bin:$PATH"' >> ~/.zshrc
   source ~/.zshrc
   flutter doctor
   ```

   `flutter doctor` debe salir sin errores en Xcode y CocoaPods. Si pide otra versión de Xcode, instalar la que indique.

5. **El proyecto:**

   ```bash
   git clone https://github.com/natharceeirl/CRMTeamBenavides.git
   cd CRMTeamBenavides
   git checkout develop
   cd src/movil
   flutter pub get
   ```

   La carpeta `ios/Pods` y el `Podfile.lock` se generan solos la primera vez que se compila.

## 2. A qué API se conecta

La app recibe la dirección de la API al compilar, con `--dart-define=API_URL=...`. Sin ese valor, en iOS apunta a `http://localhost:5021`, que solo sirve si la API corre en la misma Mac y se prueba en el simulador.

Hay dos formas de que el iPhone llegue a la API que corre en la PC:

**A. Túnel HTTPS (recomendada).** Es la misma idea del túnel que usamos para la demo en Vercel. Funciona desde cualquier red, sirve también para los compilados de release y para TestFlight, y no choca con la seguridad de iOS, que exige HTTPS:

```bash
# En la PC donde corre la API
cloudflared tunnel --url http://localhost:5021
# Devuelve una dirección como https://algo-al-azar.trycloudflare.com
```

La dirección cambia cada vez que se reinicia el túnel; hay que volver a compilar con la nueva.

**B. Red local, misma wifi.** Sirve solo para compilados de depuración:

1. Levantar la API escuchando en la red, no solo en `localhost`:

   ```powershell
   cd src\CRMTeamBenavides.Api
   dotnet run --urls "http://0.0.0.0:5021"
   ```

2. Abrir el puerto en el firewall de Windows (una vez, como administrador):

   ```powershell
   netsh advfirewall firewall add rule name="API CRM 5021" dir=in action=allow protocol=TCP localport=5021
   ```

3. Usar la IP de la PC (`ipconfig`, «Dirección IPv4»): `API_URL=http://192.168.1.50:5021`.

El `Info.plist` ya permite HTTP en la red local (`NSAllowsLocalNetworking`). La primera vez, el iPhone pregunta si la app puede acceder a la red local: hay que aceptar.

## 3. Simulador

```bash
open -a Simulator
flutter devices                       # muestra el nombre del simulador
flutter run -d "iPhone 16" --dart-define=API_URL=https://algo-al-azar.trycloudflare.com
```

- El simulador no tiene cámara. Para las fotos de la orden se usa «De la galería»; para cargar imágenes a la galería del simulador, se arrastran los archivos a su ventana.
- `r` recarga en caliente y `R` reinicia la app.

## 4. iPhone propio (gratis, con Apple ID)

1. **Agregar el Apple ID a Xcode:** Settings → Accounts → «+».
2. **Firmar la app:** abrir `src/movil/ios/Runner.xcworkspace` (el `.xcworkspace`, no el `.xcodeproj`). En el target Runner → Signing & Capabilities:
   - marcar «Automatically manage signing»;
   - en Team, elegir el equipo personal del Apple ID.

   Si Xcode dice que el bundle identifier `pe.natharce.crmTeamBenavides` no está disponible, cambiarle el final solo en esa Mac (por ejemplo `pe.natharce.crmTeamBenavides.santiago`). **Ese cambio no se sube al repositorio.**
3. **Conectar el iPhone por cable** y aceptar «Confiar en esta computadora».
4. **Activar el modo de desarrollador en el iPhone:** Ajustes → Privacidad y seguridad → Modo de desarrollador. La opción aparece después de conectarlo a la Mac con Xcode abierto. El iPhone se reinicia.
5. **Instalar y correr:**

   ```bash
   flutter devices                    # el iPhone debe aparecer
   flutter run -d <id-del-iphone> --dart-define=API_URL=https://algo-al-azar.trycloudflare.com
   ```

6. **La primera vez**, en el iPhone: Ajustes → General → VPN y gestión de dispositivos → el Apple ID → Confiar.

Lo que hay que saber de la firma gratuita:

- La app deja de abrir a los 7 días y hay que volver a instalarla.
- Un compilado de depuración (`flutter run`) solo se abre desde la Mac. Para usarla sin cable, instalar en modo release:

  ```bash
  flutter run --release -d <id-del-iphone> --dart-define=API_URL=https://...
  ```

  En release la API tiene que ser HTTPS, es decir, el túnel.
- Para no usar cable: Xcode → Window → Devices and Simulators → marcar «Connect via network» con el iPhone conectado una vez. Después aparece en `flutter devices` por wifi.

## 5. TestFlight: repartir la app a otros iPhone

Requiere la cuenta de Apple Developer (US$99 al año) con el bundle identifier registrado a nombre de la empresa.

1. **En App Store Connect** (appstoreconnect.apple.com) → Apps → «+» → Nueva app: plataforma iOS, nombre «Team Benavides», idioma español y el bundle identifier de la cuenta.
2. **En Xcode**, Signing & Capabilities → Team: el de la empresa.
3. **Subir el número de compilación** en `src/movil/pubspec.yaml`. Cada envío a TestFlight necesita uno mayor:

   ```yaml
   version: 1.0.0+2   # el número después de + sube en cada envío
   ```

4. **Compilar el paquete:**

   ```bash
   flutter build ipa --release --dart-define=API_URL=https://<api-de-pruebas>
   ```

   Deja el archivo en `build/ios/ipa/` y el archivo de Xcode en `build/ios/archive/Runner.xcarchive`.
5. **Subirlo** de una de estas formas:
   - abrir `build/ios/archive/Runner.xcarchive` con Xcode → Organizer → Distribute App → App Store Connect;
   - o arrastrar el `.ipa` a la app Transporter de Apple.
6. **En App Store Connect**, la compilación tarda unos minutos en procesarse. Apple pregunta por el cifrado: la app solo usa HTTPS, que está exento. Para no responderlo en cada envío, se puede agregar `ITSAppUsesNonExemptEncryption = NO` al `Info.plist`.
7. **Probadores en la pestaña TestFlight:**
   - **Internos:** hasta 100 personas del equipo de App Store Connect. No pasan revisión.
   - **Externos:** hasta 10 000 personas por correo o enlace público. La primera compilación pasa una revisión corta de Apple.

   Cada probador instala la app TestFlight en su iPhone y acepta la invitación. Cada compilación dura 90 días.

## 6. Antes de publicar en el App Store

- **Ícono:** hoy la app tiene el ícono de Flutter. Falta el ícono cuadrado de Team Benavides (1024 × 1024, sin transparencia); está entre lo pedido al cliente.
- **API de producción con HTTPS y dominio propio**, por ejemplo `https://app.teambenavides.com`, compilada con `--dart-define=API_URL=...`.
- **Ficha del App Store:** capturas de pantalla, descripción, política de privacidad (una página pública) y datos de contacto.
- **Usuario de prueba para la revisión de Apple:** la app pide inicio de sesión, así que Apple necesita una cuenta de prueba que funcione.

## Problemas comunes

| Síntoma | Solución |
|---|---|
| `CocoaPods not installed` o `pod install` falla | `brew install cocoapods`, luego `cd ios && pod install --repo-update` |
| «No profiles for … were found» | Elegir el Team en Signing & Capabilities y dejar marcado «Automatically manage signing» |
| «Untrusted Developer» en el iPhone | Ajustes → General → VPN y gestión de dispositivos → Confiar |
| La app abre pero no carga datos | Revisar `API_URL`: desde el iPhone `localhost` es el propio iPhone. Usar el túnel o la IP de la PC |
| Error de conexión en release con `http://` | iOS exige HTTPS fuera de la red local: usar el túnel |
| La app se cierra al tocar «Tomar foto» | Falta el permiso en el `Info.plist`; ya está agregado, revisar que el compilado sea reciente |
| La app instalada gratis dejó de abrir | Vencieron los 7 días: volver a correr `flutter run` |
