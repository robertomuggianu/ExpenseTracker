param(
    [string]$Avd = 'MAUI_Emulator_API_36',
    [string]$AndroidSdk = $env:ANDROID_HOME,
    [ValidateSet('software', 'auto', 'host', 'swiftshader', 'lavapipe', 'swangle')]
    [string]$Gpu = 'software'
)

$ErrorActionPreference = 'Stop'
if (-not $AndroidSdk) { $AndroidSdk = $env:ANDROID_SDK_ROOT }
if (-not $AndroidSdk) { $AndroidSdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
$emulatorPath = Join-Path $AndroidSdk 'emulator\emulator.exe'
if (-not (Test-Path -LiteralPath $emulatorPath)) {
    throw "Emulatore non trovato in $AndroidSdk. Specifica -AndroidSdk con il percorso del tuo SDK Android."
}
$availableAvds = @(& $emulatorPath -list-avds)
if ($LASTEXITCODE -ne 0) { throw 'Impossibile leggere gli emulatori disponibili.' }
if ($Avd -notin $availableAvds) {
    throw "Emulatore $Avd non trovato. Disponibili: $($availableAvds -join ', ')."
}

Write-Host "Avvio $Avd con grafica $Gpu e senza caricare lo snapshot. App e dati vengono conservati."
& $emulatorPath -avd $Avd -gpu $Gpu -no-snapshot-load
if ($LASTEXITCODE -ne 0) { throw "Emulatore terminato con codice $LASTEXITCODE." }
