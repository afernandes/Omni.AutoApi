<#
.SYNOPSIS
    Monta o feed NuGet local e compila o consumidor deste exemplo.

.DESCRIPTION
    O projeto Consumer consome MyApi.Contracts como PACOTE (é o ponto do exemplo: distribuir o
    cliente gerado para outra solution). Por isso ele NÃO compila num clone novo sem antes gerar
    o feed — que é .gitignore'd de propósito, já que é artefato de build.

    Rode este script antes de abrir samples/Distribution no editor.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot          # .../samples
$repo = Split-Path -Parent $raiz                  # raiz do repositório
$feed = Join-Path $PSScriptRoot '_feed'

Write-Host "==> Gerando feed local em $feed" -ForegroundColor Cyan
if (Test-Path $feed) { Remove-Item -Recurse -Force $feed }
New-Item -ItemType Directory -Path $feed | Out-Null

# O Abstractions precisa estar no feed: é dependência declarada do MyApi.Contracts.
dotnet pack (Join-Path $repo 'src\Omni.AutoApi.Abstractions') -c $Configuration -o $feed --nologo
dotnet pack (Join-Path $PSScriptRoot 'MyApi.Contracts')       -c $Configuration -o $feed --nologo

Write-Host "==> Compilando o Consumer" -ForegroundColor Cyan
dotnet build (Join-Path $PSScriptRoot 'Consumer') -c $Configuration --nologo

Write-Host ""
Write-Host "OK. Para executar contra um servidor:" -ForegroundColor Green
Write-Host "  dotnet run --project samples/Distribution/Consumer -- http://localhost:5097"
