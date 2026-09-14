[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$MosaikArgument
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$cliPath = Join-Path $repositoryRoot "tools\mosaik_cli.py"
$venvPython = Join-Path $repositoryRoot ".venv\Scripts\python.exe"

if (Test-Path -LiteralPath $venvPython -PathType Leaf) {
    $pythonCommand = $venvPython
} else {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) {
        throw "No se encontro Python. Ejecuta tools\Bootstrap-MOSAIK.ps1 o instala Python 3.11+."
    }
    $pythonCommand = $python.Source
}

if (-not (Test-Path -LiteralPath $cliPath -PathType Leaf)) {
    throw "No se encontro la CLI de MOSAIK: $cliPath"
}

& $pythonCommand $cliPath @MosaikArgument
exit $LASTEXITCODE
