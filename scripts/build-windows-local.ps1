# SPDX-License-Identifier: AGPL-3.0-only
# CouchSubs'ı Windows'ta kendi bilgisayarınızda derler (GitHub Actions gerekmez). Çıktı: publish\CouchSubs\CouchSubs.exe
#
# Hafif derleme: OBS kaynağı, capture card penceresi, Remote Play penceresi, ücretsiz OPUS-MT çevirisi, DeepL, yerel
# model (Ollama, LM Studio), seçici ve OBS katmanı çalışır. PS5'e doğrudan bağlanma (ikinci PSN hesabıyla) için
# gereken yerel kütüphane (couchsubs_native.dll; libchiaki + FFmpeg, MSYS2 ile derlenir) ve Türkçe dublaj ses motoru
# bu betikte yoktur. windows\native\bin\couchsubs_native.dll varsa uygulamaya kendiliğinden eklenir.
#
# Gerekenler (bir kez, PowerShell'de):
#   winget install Git.Git Microsoft.DotNet.SDK.10 Python.Python.3.12 Microsoft.VCRedist.2015+.x64
# Kullanım (depo klasöründe):
#   powershell -ExecutionPolicy Bypass -File scripts\build-windows-local.ps1          # derle
#   powershell -ExecutionPolicy Bypass -File scripts\build-windows-local.ps1 -Test    # önce testleri de koş
param(
    [string]$Output = "publish\CouchSubs",
    [switch]$Test
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

function Step($text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host ""; Write-Host "HATA: $text" -ForegroundColor Red; exit 1 }

Step "Araçlar denetleniyor"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail ".NET SDK yok. Kurun: winget install Microsoft.DotNet.SDK.10" }
if (-not (& dotnet --list-sdks | Where-Object { $_ -match '^10\.' })) { Fail ".NET 10 SDK yok. Kurun: winget install Microsoft.DotNet.SDK.10" }
$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python -or $python.Source -like "*WindowsApps*") { Fail "Python yok (Microsoft Store kısayolu sayılmaz). Kurun: winget install Python.Python.3.12, sonra yeni bir PowerShell penceresi açın." }
Write-Host ("dotnet " + (& dotnet --version) + ", " + (& python --version))

# Paketler nuget.org'dan iner. Bazı kurulumlarda kullanıcının NuGet ayarında nuget.org yoktur ya da kapalıdır; o zaman
# her paket "NU1100: çözümlenemiyor" verir. Kaynak eklenir ya da açılır (yalnız bu kullanıcının NuGet ayarı değişir).
$nugetOrg = "https://api.nuget.org/v3/index.json"
$sources = @(& dotnet nuget list source --format Short)
if (-not ($sources | Where-Object { $_ -match "^E\s+$([regex]::Escape($nugetOrg))" })) {
    if ($sources | Where-Object { $_ -match [regex]::Escape($nugetOrg) }) {
        Write-Host "nuget.org paket kaynağı kapalı; açılıyor."
        & dotnet nuget enable source nuget.org
        if ($LASTEXITCODE -ne 0) { & dotnet nuget add source $nugetOrg -n nuget.org-couchsubs }
    } else {
        Write-Host "nuget.org paket kaynağı tanımlı değil; ekleniyor."
        & dotnet nuget add source $nugetOrg -n nuget.org
        if ($LASTEXITCODE -ne 0) { & dotnet nuget add source $nugetOrg -n nuget.org-couchsubs }
    }
    if ($LASTEXITCODE -ne 0) { Fail "nuget.org paket kaynağı eklenemedi. Elle deneyin: dotnet nuget add source $nugetOrg -n nuget.org" }
}

# Xbox görüntü bileşeni (Node.js ile paketlenir) bu derlemede yok; proje dosyaları istediği için boş yer tutucular konur.
$bridge = "windows\xbox-bridge\dist"
if (-not (Test-Path "$bridge\bridge.cjs")) {
    New-Item -ItemType Directory -Force $bridge | Out-Null
    foreach ($file in "bridge.cjs", "player.html", "player.js") { Set-Content -LiteralPath "$bridge\$file" -Value "" }
}

if ($Test) {
    Step "Testler (birkaç dakika sürer)"
    & dotnet test windows\CouchSubs.Windows.slnx -c Release -m:1
    if ($LASTEXITCODE -ne 0) { Fail "Testler başarısız." }
}

Step "Uygulama derleniyor"
if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }
& dotnet publish windows\src\CouchSubs.App -c Release -r win-x64 --self-contained -o $Output -p:Version=0.1.0-dev `
    -p:CouchSubsPacksUrl=https://couchsubs-packs.enmera.workers.dev
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish başarısız." }
$app = (Resolve-Path $Output).Path

# Ücretsiz çeviri (OPUS-MT) CTranslate2 ile çalışır: Python uygulamanın yanında gelir, kullanıcının Python'u kullanılmaz.
Step "Ücretsiz çeviri motoru (CTranslate2)"
$runtime = Join-Path $app "ct2"
New-Item -ItemType Directory -Force $runtime | Out-Null
$zip = Join-Path $env:TEMP "couchsubs-python-embed.zip"
Invoke-WebRequest "https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip" -OutFile $zip
$sha = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
if ($sha -ne "4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3") { Fail "İndirilen Python paketinin özeti tutmuyor: $sha" }
Expand-Archive -LiteralPath $zip -DestinationPath $runtime -Force
Set-Content -LiteralPath (Join-Path $runtime "python312._pth") -Value @("python312.zip", ".", "Lib\site-packages", "import site")
# Paketler PyPI'den sabit sürüm ve SHA-256 özetiyle (windows\ct2\requirements.txt).
& python -m pip install --disable-pip-version-check --no-deps --require-hashes --only-binary=:all: `
    --python-version 3.12 --platform win_amd64 --implementation cp `
    --target (Join-Path $runtime "Lib\site-packages") -r windows\ct2\requirements.txt
if ($LASTEXITCODE -ne 0) { Fail "CTranslate2 paketleri indirilemedi." }
Copy-Item windows\ct2\worker.py (Join-Path $runtime "worker.py")

# CTranslate2 Visual C++ çalışma zamanını ister; sistemdeki güncel kopyası uygulamanın yanına konur.
Step "Visual C++ çalışma zamanı"
$crt = "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "msvcp140_1.dll", "msvcp140_2.dll", "concrt140.dll"
$system = Join-Path $env:WINDIR "System32"
$msvcp = Join-Path $system "msvcp140.dll"
if (-not (Test-Path $msvcp)) { Fail "Visual C++ çalışma zamanı yok. Kurun: winget install Microsoft.VCRedist.2015+.x64" }
$v = (Get-Item $msvcp).VersionInfo
if ([version]::new($v.FileMajorPart, $v.FileMinorPart) -lt [version]"14.40") {
    Fail "Visual C++ çalışma zamanı eski ($($v.FileVersion)). Güncelleyin: winget install Microsoft.VCRedist.2015+.x64"
}
foreach ($dll in $crt) {
    $source = Join-Path $system $dll
    if (Test-Path $source) { Copy-Item $source $app; Copy-Item $source $runtime }
}

Step "Ücretsiz çeviri motoru deneniyor"
& (Join-Path $runtime "python.exe") -c "import ctranslate2; print('CTranslate2', ctranslate2.__version__)"
if ($LASTEXITCODE -ne 0) { Fail "CTranslate2 açılamadı (Visual C++ çalışma zamanını kurup yeniden deneyin)." }

Copy-Item LICENSE, THIRD-PARTY-NOTICES.md $app
$native = Test-Path (Join-Path $app "couchsubs_native.dll")

Step "Bitti"
Write-Host "Uygulama: $app\CouchSubs.exe" -ForegroundColor Green
Write-Host "Klasörü istediğiniz yere taşıyabilirsiniz; kurulum gerekmez. Çeviri modeli (yaklaşık 475 MB) ilk açılışta iner."
if (-not $native) { Write-Host "PS5'e doğrudan bağlanma bu derlemede yok (couchsubs_native.dll yok); OBS ve pencere kaynakları çalışır." -ForegroundColor Yellow }
