param(
    [string] $OutputPath = "artifacts/publish/win-x64-aot"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "ContainerMcp.Server/ContainerMcp.Server.csproj"
$output = Join-Path $root $OutputPath

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishAot=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    -o $output

Get-ChildItem -Path $output -File |
    Where-Object { $_.Name -ne "container-mcp.exe" } |
    Remove-Item -Force

Write-Host "Published $output"
Get-ChildItem -Path $output
