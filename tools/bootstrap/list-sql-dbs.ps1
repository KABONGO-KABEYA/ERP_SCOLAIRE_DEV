#Requires -Version 5.1
param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path)

$ErrorActionPreference = "Stop"
function Get-DotEnvValue([string]$path, [string]$key) {
    foreach ($line in Get-Content $path) {
        if ($line -match '^\s*#' -or $line -match '^\s*$') { continue }
        $idx = $line.IndexOf('=')
        if ($idx -lt 1) { continue }
        if ($line.Substring(0, $idx).Trim() -eq $key) {
            return $line.Substring($idx + 1).Trim().Trim('"').Trim("'")
        }
    }
    return $null
}

$sqlCs = Get-DotEnvValue (Join-Path $RepoRoot ".env") "SQL_CONNECTION_STRING"
if (-not $sqlCs) { throw "SQL_CONNECTION_STRING missing" }

# Mask password in diagnostic of CS shape only
$shape = [regex]::Replace($sqlCs, "(?i)Password=[^;]*", "Password=***")
Write-Host ("CS_SHAPE=" + $shape)

Add-Type -AssemblyName System.Data
function Q([string]$cs, [string]$q) {
    $c = New-Object System.Data.SqlClient.SqlConnection($cs)
    $c.Open()
    try {
        $cmd = $c.CreateCommand(); $cmd.CommandText = $q
        $a = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $t = New-Object System.Data.DataTable
        [void]$a.Fill($t)
        return $t
    } finally { $c.Close() }
}

$masterCs = [regex]::Replace($sqlCs, "Database=[^;]+", "Database=master", "IgnoreCase")
Write-Host "Listing databases..."
$rows = Q $masterCs "SELECT name FROM sys.databases ORDER BY name"
foreach ($r in $rows.Rows) { Write-Host ("DB=" + $r.name) }
Write-Host ("COUNT=" + $rows.Rows.Count)

$boot = Q $masterCs "SELECT CASE WHEN DB_ID(N'SchoolManagementBootstrap') IS NULL THEN 0 ELSE 1 END AS e"
Write-Host ("BOOTSTRAP_DB=" + $boot.Rows[0].e)
