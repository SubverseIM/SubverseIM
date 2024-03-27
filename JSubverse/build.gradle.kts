plugins {
    id("java-library")
    alias(libs.plugins.jetbrainsKotlinJvm)
}

dependencies {
    implementation(libs.kwik)
    implementation(libs.pgpainless)
}

java {
    sourceCompatibility = JavaVersion.VERSION_17
    targetCompatibility = JavaVersion.VERSION_17
}