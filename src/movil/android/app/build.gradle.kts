import java.util.Base64
import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Firma de release: android/key.properties, fuera del repositorio, apunta al keystore
// de subida y trae sus claves (ver src/movil/README.md, «Publicar»).
val propiedadesFirma = Properties().apply {
    val archivo = rootProject.file("key.properties")
    if (archivo.exists()) archivo.inputStream().use { load(it) }
}

// Los --dart-define llegan a Gradle en base64, separados por comas.
val dartDefines: Map<String, String> = (findProperty("dart-defines") as String?)
    ?.split(",")
    ?.filter { it.isNotBlank() }
    ?.map { String(Base64.getDecoder().decode(it)) }
    ?.associate { it.substringBefore("=") to it.substringAfter("=", "") }
    ?: emptyMap()

// Una versión de release mal armada se frena antes de compilar, no en el teléfono:
// sin firma propia no se puede actualizar después, y sin API_URL con HTTPS no conecta.
gradle.taskGraph.whenReady {
    if (allTasks.none { it.name.contains("Release") }) return@whenReady

    check(!propiedadesFirma.isEmpty) {
        "Falta android/key.properties con la firma de release. Ver src/movil/README.md, «Publicar»."
    }
    val urlApi = dartDefines["API_URL"].orEmpty()
    check(urlApi.startsWith("https://")) {
        if (urlApi.isEmpty()) {
            "Falta --dart-define=API_URL=https://… al compilar la versión de release."
        } else {
            "API_URL tiene que empezar con https:// en la versión de release (llegó «$urlApi»)."
        }
    }
}

// google-services.json trae el proyecto de Firebase y no se sube al repositorio:
// cada quien pone el suyo (ver docs/notificaciones.md). Sin el archivo la app
// compila igual y funciona sin notificaciones al teléfono.
if (file("google-services.json").exists()) {
    apply(plugin = "com.google.gms.google-services")
}

android {
    namespace = "pe.natharce.crm_team_benavides"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        // flutter_local_notifications usa APIs de fecha de Java 8 en Android antiguos.
        isCoreLibraryDesugaringEnabled = true
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "pe.natharce.crm_team_benavides"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        // Uses the version code from pubspec.yaml. When using split APKs, 1000 * ABI_VERSION
        // is added automatically by Flutter. (https://developer.android.com/studio/build/configure-apk-splits#configure-APK-versions)
        // You can force using the value of versionCode by specifying the `-P force-version-code-ignoring-abi=true`
        // flag during build.
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        create("release") {
            if (!propiedadesFirma.isEmpty) {
                storeFile = file(propiedadesFirma.getProperty("storeFile"))
                storePassword = propiedadesFirma.getProperty("storePassword")
                keyAlias = propiedadesFirma.getProperty("keyAlias")
                keyPassword = propiedadesFirma.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            // Nunca con la clave de depuración: un APK firmado así no se puede actualizar
            // después con la firma de verdad. Sin key.properties la compilación se frena (arriba).
            signingConfig = signingConfigs.getByName("release")
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}

dependencies {
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.5")
}
