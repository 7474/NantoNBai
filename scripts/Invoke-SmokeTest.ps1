<#
.SYNOPSIS
    NantoNBai Function App のエンドポイント疎通を検証する。

.DESCRIPTION
    CI ではローカルで起動した Functions ホストに対して、CD ではデプロイ先に対して
    同じ検証を実行する。分離ワーカー移行で壊れやすい以下を対象にしている。

      - pptx テンプレートの解決 (AppContext.BaseDirectory)
      - ワーカープロセス内での Spire.Presentation による画像変換
      - OpenAPI 拡張が提供する swagger エンドポイント
      - ルートプレフィックス (api) と Content-Type

.PARAMETER BaseUrl
    検証対象のベース URL。例: http://localhost:7071

.PARAMETER ReadyTimeoutSeconds
    最初の応答が返るまで待つ秒数。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BaseUrl,

    [int] $ReadyTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = $BaseUrl.TrimEnd('/')
# name=ポート番号&from=80&to=443 (README と E2E が使っているサンプル)
$sampleQuery = 'name=%E3%83%9D%E3%83%BC%E3%83%88%E7%95%AA%E5%8F%B7&from=80&to=443'
$failures = [System.Collections.Generic.List[string]]::new()

function Get-Bytes {
    param($Response)
    return $Response.RawContentStream.ToArray()
}

function Get-Text {
    param($Response)
    return [System.Text.Encoding]::UTF8.GetString((Get-Bytes $Response))
}

function Get-ContentType {
    param($Response)
    foreach ($key in $Response.Headers.Keys) {
        if ($key -ieq 'Content-Type') {
            return (@($Response.Headers[$key]) -join '; ')
        }
    }
    return ''
}

function Test-Endpoint {
    param(
        [string] $Name,
        [string] $Path,
        [string] $ExpectedContentType,
        [scriptblock] $Assert
    )

    Write-Host "==> $Name : GET $Path"
    try {
        $response = Invoke-WebRequest -Uri "$root$Path" -UseBasicParsing -TimeoutSec 120
        if ($response.StatusCode -ne 200) {
            throw "期待 200 に対して $($response.StatusCode)"
        }

        $contentType = Get-ContentType $response
        if ($ExpectedContentType -and $contentType -notlike "*$ExpectedContentType*") {
            throw "Content-Type が '$ExpectedContentType' を含まない: '$contentType'"
        }

        if ($Assert) {
            & $Assert $response
        }

        Write-Host "    OK  content-type=$contentType, $((Get-Bytes $response).Length) bytes"
    }
    catch {
        $message = $_.Exception.Message
        Write-Host "    NG  $message"
        $failures.Add("${Name}: $message")
    }
}

function Assert-Contains {
    param([string] $Haystack, [string] $Needle, [string] $What)
    if ($Haystack -notlike "*$Needle*") {
        throw "$What に '$Needle' が含まれていない"
    }
}

Write-Host "Smoke test target: $root"

# --- ホストが応答するまで待つ ---------------------------------------------
$deadline = (Get-Date).AddSeconds($ReadyTimeoutSeconds)
$ready = $false
while (-not $ready -and (Get-Date) -lt $deadline) {
    try {
        $probe = Invoke-WebRequest -Uri "$root/api/Index" -UseBasicParsing -TimeoutSec 15
        if ($probe.StatusCode -eq 200) { $ready = $true; break }
    }
    catch {
        Start-Sleep -Seconds 3
    }
}
if (-not $ready) {
    Write-Error "$ReadyTimeoutSeconds 秒以内に $root/api/Index が応答しなかった"
    exit 1
}
Write-Host "Host is ready."
Write-Host ''

# --- HTML エンドポイント ---------------------------------------------------
Test-Endpoint -Name 'Index' -Path '/api/Index' -ExpectedContentType 'text/html' -Assert {
    param($response)
    $text = Get-Text $response
    Assert-Contains $text '<h1>NantoNBai</h1>' 'Index'
    Assert-Contains $text '/api/swagger/ui' 'Index'
}

Test-Endpoint -Name 'Viewer' -Path "/api/Viewer?$sampleQuery" -ExpectedContentType 'text/html' -Assert {
    param($response)
    $text = Get-Text $response
    # 日本語リテラルはスクリプトのエンコーディング差異で誤検知しうるので ASCII だけで検証する
    Assert-Contains $text 'og:title' 'Viewer'
    Assert-Contains $text 'twitter:card' 'Viewer'
    Assert-Contains $text '/api/Generate.png' 'Viewer'
}

# --- グラフ生成 (Windows 依存のオフィスドキュメント処理) --------------------
Test-Endpoint -Name 'Generate.pptx' -Path "/api/Generate.pptx?$sampleQuery" -ExpectedContentType 'application/octet-stream' -Assert {
    param($response)
    $bytes = Get-Bytes $response
    if ($bytes.Length -lt 1024) { throw "pptx が小さすぎる: $($bytes.Length) bytes" }
    # OOXML は zip なので 'PK'
    if ($bytes[0] -ne 0x50 -or $bytes[1] -ne 0x4B) { throw 'pptx が zip (PK) で始まっていない' }
}

Test-Endpoint -Name 'Generate.png' -Path "/api/Generate.png?$sampleQuery" -ExpectedContentType 'image/png' -Assert {
    param($response)
    $bytes = Get-Bytes $response
    if ($bytes.Length -lt 4096) { throw "png が小さすぎる: $($bytes.Length) bytes" }
    $magic = @(0x89, 0x50, 0x4E, 0x47)
    for ($i = 0; $i -lt $magic.Length; $i++) {
        if ($bytes[$i] -ne $magic[$i]) { throw 'PNG シグネチャが不正' }
    }
}

Test-Endpoint -Name 'Generate.svg' -Path "/api/Generate.svg?$sampleQuery" -ExpectedContentType 'image/svg+xml' -Assert {
    param($response)
    Assert-Contains (Get-Text $response) '<svg' 'svg'
}

# --- OpenAPI (ASP.NET Core 統合と非互換なため最重要) ------------------------
Test-Endpoint -Name 'swagger.json' -Path '/api/swagger.json' -ExpectedContentType 'application/json' -Assert {
    param($response)
    $document = (Get-Text $response) | ConvertFrom-Json
    if ($null -eq $document.paths) { throw 'swagger.json に paths がない' }
    $paths = @($document.paths.PSObject.Properties.Name)
    foreach ($expected in @('/Generate.{format}', '/Viewer')) {
        if ($paths -notcontains $expected) {
            throw "swagger.json に $expected がない (paths: $($paths -join ', '))"
        }
    }
}

Test-Endpoint -Name 'swagger/ui' -Path '/api/swagger/ui' -ExpectedContentType 'text/html' -Assert {
    param($response)
    Assert-Contains (Get-Text $response) 'swagger' 'swagger UI'
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "FAILED ($($failures.Count))"
    foreach ($failure in $failures) { Write-Host "  - $failure" }
    exit 1
}

Write-Host 'All smoke tests passed.'
