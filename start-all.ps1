#!/usr/bin/env pwsh
# Starts all three ProjectManagementApp services, each in its own PowerShell window.
#
#   API : http://localhost:5180  (owns BoardCollection.json; start first)
#   MCP : http://localhost:5190
#   Web : http://localhost:5148  (launches a browser)
#
# Usage:  .\start-all.ps1

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$services = @(
    @{ Name = 'API'; Path = 'ProjectManagementApp.Api' },
    @{ Name = 'MCP'; Path = 'ProjectManagementApp.Mcp' },
    @{ Name = 'Web'; Path = '.' }
)

foreach ($svc in $services) {
    $projectDir = Join-Path $root $svc.Path
    Write-Host "Starting $($svc.Name) ($($svc.Path))..." -ForegroundColor Cyan
    Start-Process pwsh -ArgumentList @(
        '-NoExit',
        '-Command',
        "Set-Location -LiteralPath '$projectDir'; `$Host.UI.RawUI.WindowTitle = 'PMApp $($svc.Name)'; dotnet run --launch-profile http"
    )
}

Write-Host 'All three services launching in separate windows.' -ForegroundColor Green
