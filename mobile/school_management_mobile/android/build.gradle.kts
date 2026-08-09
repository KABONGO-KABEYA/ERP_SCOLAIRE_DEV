import com.android.build.gradle.LibraryExtension

allprojects {
    repositories {
        // Cache local des artefacts Flutter engine (évite storage.googleapis.com hors ligne).
        maven {
            url = uri("${rootProject.projectDir}/../build/flutter_maven")
        }
        google()
        mavenCentral()
        maven {
            url = uri("https://storage.googleapis.com/download.flutter.io")
        }
    }
}

val newBuildDir: Directory =
    rootProject.layout.buildDirectory
        .dir("../../build")
        .get()
rootProject.layout.buildDirectory.value(newBuildDir)

subprojects {
    val newSubprojectBuildDir: Directory = newBuildDir.dir(project.name)
    project.layout.buildDirectory.value(newSubprojectBuildDir)
}

subprojects {
    afterEvaluate {
        extensions.findByType(LibraryExtension::class.java)?.compileSdk = 36
    }
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
