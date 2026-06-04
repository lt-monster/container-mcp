param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64-aot", "linux-x64-aot")]
    [string] $Runtime,

    [string] $RootPath = (Join-Path $PSScriptRoot "..")
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RootPath).Path
$publishDirectory = Join-Path $root "artifacts/publish/$Runtime"

if (-not (Test-Path -LiteralPath $publishDirectory -PathType Container)) {
    throw "Publish directory does not exist: $publishDirectory"
}

$expectedFileName = switch ($Runtime) {
    "win-x64-aot" { "container-mcp.exe" }
    "linux-x64-aot" { "container-mcp" }
}

$files = @(Get-ChildItem -LiteralPath $publishDirectory -File)
if ($files.Count -ne 1) {
    $actual = if ($files.Count -eq 0) { "<none>" } else { ($files.Name -join ", ") }
    throw "Expected exactly one file in $publishDirectory ($expectedFileName), found $($files.Count): $actual"
}

$file = $files[0]
if ($file.Name -ne $expectedFileName) {
    throw "Expected publish artifact '$expectedFileName', found '$($file.Name)'."
}

if ($file.Length -le 0) {
    throw "Publish artifact is empty: $($file.FullName)"
}

Write-Host "Verified $Runtime artifact: $($file.FullName) ($($file.Length) bytes)"
