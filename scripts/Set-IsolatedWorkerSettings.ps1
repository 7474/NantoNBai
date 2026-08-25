<#
.SYNOPSIS
    Function App (または そのデプロイスロット) を分離ワーカー向けの設定にする。

.DESCRIPTION
    - FUNCTIONS_WORKER_RUNTIME を dotnet-isolated にする
    - .NET 8 の in-process モデルを有効化する FUNCTIONS_INPROC_NET8_ENABLED を削除する
      (分離ワーカーでは不要で、残したままにすると挙動が未定義になる)

    必要な権限は Microsoft.Web/sites/config/read と write だけで、
    App Service プランへの権限は要らない。

.PARAMETER AppName
    Function App 名。

.PARAMETER ResourceGroup
    リソースグループ名。

.PARAMETER Slot
    デプロイスロット名。省略すると本番 (production) に適用する。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AppName,

    [Parameter(Mandatory = $true)]
    [string] $ResourceGroup,

    [string] $Slot
)

$ErrorActionPreference = 'Stop'

$slotArgs = @()
$label = 'production'
if ($Slot) {
    $slotArgs = @('--slot', $Slot)
    $label = "slot '$Slot'"
}

Write-Host "分離ワーカー向けの設定を $AppName の $label に適用する"

az functionapp config appsettings set `
    --name $AppName --resource-group $ResourceGroup $slotArgs `
    --settings FUNCTIONS_WORKER_RUNTIME=dotnet-isolated --output none
if ($LASTEXITCODE -ne 0) { throw 'FUNCTIONS_WORKER_RUNTIME の設定に失敗した' }
Write-Host 'FUNCTIONS_WORKER_RUNTIME=dotnet-isolated を設定した'

$names = az functionapp config appsettings list `
    --name $AppName --resource-group $ResourceGroup $slotArgs `
    --query '[].name' --output tsv
if ($LASTEXITCODE -ne 0) { throw 'アプリケーション設定の一覧取得に失敗した' }

$existing = @($names -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($existing -contains 'FUNCTIONS_INPROC_NET8_ENABLED') {
    az functionapp config appsettings delete `
        --name $AppName --resource-group $ResourceGroup $slotArgs `
        --setting-names FUNCTIONS_INPROC_NET8_ENABLED --output none
    if ($LASTEXITCODE -ne 0) { throw 'FUNCTIONS_INPROC_NET8_ENABLED の削除に失敗した' }
    Write-Host 'FUNCTIONS_INPROC_NET8_ENABLED を削除した'
} else {
    Write-Host 'FUNCTIONS_INPROC_NET8_ENABLED は設定されていない'
}

Write-Host '設定の適用が完了した。アプリは再起動する。'
