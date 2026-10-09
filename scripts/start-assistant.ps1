param(
    [int]$Port = 5188,
    [string]$Model = 'qwen2.5:3b'
)
$ErrorActionPreference = 'Stop'
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'La porta deve essere compresa tra 1024 e 65535.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Installa .NET SDK 10 prima di avviare il servizio.' }
if (-not $env:Assistant__AccessKey) {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $env:Assistant__AccessKey = ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
}
$previousModel = $env:Assistant__Model
$env:Assistant__Model = $Model
Write-Host ''
Write-Host "Inserisci questa chiave nella pagina Assistente dell'app:"
Write-Host $env:Assistant__AccessKey
Write-Host ''
Write-Host "Indirizzo per test sul PC: http://localhost:$Port"
if (Get-Command Get-NetIPAddress -ErrorAction SilentlyContinue) {
    Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -match '^(192\.168\.|10\.|172\.(1[6-9]|2[0-9]|3[01])\.)' } |
        ForEach-Object { Write-Host "Indirizzo possibile per iPhone: http://$($_.IPAddress):$Port ($($_.InterfaceAlias))" }
}
Write-Host 'Usa la rete Wi-Fi privata di casa. Lascia questo terminale aperto; Ctrl+C arresta il servizio.'
Write-Host "Se riavvii con un nuovo processo, aggiorna la chiave nell'app."
$projectPath = Join-Path $PSScriptRoot '../src/ExpenseTracker.AssistantServer/ExpenseTracker.AssistantServer.csproj'
try {
    & dotnet run --project $projectPath --no-launch-profile -- --urls "http://0.0.0.0:$Port"
    if ($LASTEXITCODE -ne 0) { throw "Il servizio e' terminato con codice $LASTEXITCODE." }
} finally { $env:Assistant__Model = $previousModel }
