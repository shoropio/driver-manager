param(
    [string]$Thumbprint = $env:DM_THUMBPRINT,
    [string]$CertPath = $env:DM_CERTIFICATE,
    [string]$CertPassword = $env:DM_CERTIFICATE_PASSWORD
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "DriverManager.App"
$out = Join-Path $proj "bin\Release\publish"

Write-Host "== Publicando Release (win-x64, autocontenido) =="
dotnet publish $proj -c Release -r win-x64 --self-contained true -o $out
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$signScript = Join-Path $PSScriptRoot "sign.ps1"
$appExe = Join-Path $out "DriverManager.App.exe"
$hasCert = [bool]($Thumbprint -or $CertPath)

if ($hasCert) {
    Write-Host "== Firmando binario de la aplicacion =="
    & $signScript -FilePath $appExe -Thumbprint $Thumbprint -CertPath $CertPath -CertPassword $CertPassword
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
} else {
    Write-Host "== Sin certificado: la firma se omitio =="
}

Write-Host "== Generando instalador (Inno Setup) =="
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path -LiteralPath $iscc) {
    $isccArgs = @((Join-Path $PSScriptRoot "driver-manager.iss"))
    if ($hasCert) {
        $signTool = $env:DM_SIGNTOOL
        if (-not $signTool) {
            $candidates = @(
                "signtool.exe",
                "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe",
                "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
            )
            foreach ($candidate in $candidates) {
                if (Get-Command $candidate -ErrorAction SilentlyContinue) {
                    $signTool = $candidate
                    break
                }
                if (Test-Path -LiteralPath $candidate) {
                    $signTool = $candidate
                    break
                }
            }
        }
        if (-not $signTool) { $signTool = "signtool.exe" }
        $ts = "http://timestamp.digicert.com"
        if ($Thumbprint) {
            $sigArgs = "sign /fd SHA256 /tr $ts /td SHA256 /sha1 $Thumbprint"
        } else {
            $sigArgs = "sign /fd SHA256 /tr $ts /td SHA256 /f `"$CertPath`" /p $CertPassword"
        }
        $wrapper = Join-Path $env:TEMP "opencode\signtool_wrapper.cmd"
        "@echo off`r`n`"$signTool`" $sigArgs `"%1`"" | Set-Content -LiteralPath $wrapper -Encoding ASCII
        $isccArgs += "/DMySign=1"
        $isccArgs += "/Ssigntool=$wrapper `$f"
    }
    & $iscc @isccArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "ISCC fallo con codigo $LASTEXITCODE."
    }
} else {
    Write-Warning "Inno Setup 6 no esta instalado. Compila el instalador manualmente:"
    Write-Warning "  ISCC.exe packaging\driver-manager.iss"
}

Write-Host "== Listo. Publicacion: $out | Instalador: $root\output =="
