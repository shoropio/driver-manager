param(
    [string]$CertPath = $env:DM_CERTIFICATE,
    [string]$CertPassword = $env:DM_CERTIFICATE_PASSWORD,
    [string]$Thumbprint = $env:DM_THUMBPRINT,
    [Parameter(Mandatory = $true)]
    [string]$FilePath
)

$ErrorActionPreference = "Stop"

$candidates = @(
    $env:DM_SIGNTOOL,
    "signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
)

$signTool = $null
foreach ($candidate in $candidates) {
    if ($candidate -and (Get-Command $candidate -ErrorAction SilentlyContinue)) {
        $signTool = $candidate
        break
    }
    if ($candidate -and (Test-Path -LiteralPath $candidate)) {
        $signTool = $candidate
        break
    }
}

if (-not $signTool) {
    Write-Warning "signtool.exe no encontrado. Instala el Windows SDK o define DM_SIGNTOOL."
    exit 2
}

if (-not $CertPath -and -not $Thumbprint) {
    Write-Warning "No se especifico certificado (por -CertPath/-Thumbprint o env DM_CERTIFICATE/DM_THUMBPRINT). La firma se omitio."
    exit 0
}

$timestampUrl = "http://timestamp.digicert.com"

if ($Thumbprint) {
    Write-Host "Firmando $FilePath con certificado del almacen (thumbprint $Thumbprint) ..."
    & $signTool sign /fd SHA256 /tr $timestampUrl /td SHA256 /sha1 $Thumbprint $FilePath
} else {
    Write-Host "Firmando $FilePath con certificado PFX ..."
    & $signTool sign /fd SHA256 /tr $timestampUrl /td SHA256 /f $CertPath /p $CertPassword $FilePath
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "La firma fallo con codigo $LASTEXITCODE."
}
