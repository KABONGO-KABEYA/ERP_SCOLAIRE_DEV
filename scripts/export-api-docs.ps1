# Exporte la documentation API (CSV + OpenAPI + Markdown).
# Usage: .\scripts\export-api-docs.ps1
# Option: .\scripts\export-api-docs.ps1 -SwaggerUrl "http://localhost:5041/swagger/v1/swagger.json"
param(
    [string]$SwaggerUrl = ""
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$controllersDir = Join-Path $root "src\SchoolManagement.API\Controllers"
$outDir = Join-Path $root "docs\api"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$routes = @{
    Base              = "api/v1"
    Auth              = "api/v1/auth"
    Schools           = "api/v1/schools"
    Students          = "api/v1/students"
    Grades            = "api/v1/grades"
    Payments          = "api/v1/payments"
    RevenueAllocation = "api/v1/revenue-allocation"
    Withholdings      = "api/v1/withholdings"
    Currencies        = "api/v1/currencies"
    ExchangeRates     = "api/v1/exchange-rates"
    ExchangeRateTypes = "api/v1/exchange-rate-types"
    SchoolCurrencies  = "api/v1/school-currencies"
    StudentCards      = "api/v1/cards"
    CardTemplates     = "api/v1/card-templates"
    Accounting        = "api/v1/accounting"
    Finance           = "api/v1/finance"
    Reports           = "api/v1/reports"
    Dashboard         = "api/v1/dashboard"
    Academic          = "api/v1/academic"
    Teacher           = "api/v1/teacher"
    Parent            = "api/v1/parent"
    Documents         = "api/v1/documents"
    DocumentBranding  = "api/v1/document-branding"
    Admin             = "api/v1/admin"
    Personnel         = "api/v1/personnel"
    CloudSync         = "api/v1/cloud-sync"
}

function Resolve-RouteExpr([string]$routeExpr) {
    $routeExpr = $routeExpr.Trim()
    if ($routeExpr -match '\$"\{ApiRoutes\.(\w+)\}(?:/([^"]*))?"') {
        $base = $routes[$Matches[1]]
        if ($Matches[2]) {
            $sub = $Matches[2] -replace '\[controller\]', 'health'
            return "$base/$sub"
        }
        return $base
    }
    if ($routeExpr -match 'ApiRoutes\.(\w+)') { return $routes[$Matches[1]] }
    if ($routeExpr -match '"([^"]+)"') {
        return ($Matches[1] -replace '\[controller\]', 'health')
    }
    return $null
}

$endpoints = New-Object System.Collections.Generic.List[object]

Get-ChildItem "$controllersDir\*.cs" | ForEach-Object {
    $fileContent = Get-Content $_.FullName -Raw
    $lines = Get-Content $_.FullName
    $controllerBases = @{}

    $classMatches = [regex]::Matches($fileContent, '(?s)\[Route\([^\]]+\)\]\s*(?:\[[^\]]+\]\s*)*public\s+(?:sealed\s+)?class\s+(\w+)')
    foreach ($cm in $classMatches) {
        $className = $cm.Groups[1].Value
        $prefix = $fileContent.Substring(0, $cm.Index + $cm.Length)
        $routeMatches = [regex]::Matches($prefix, '\[Route\(([^\]]+)\)\]')
        if ($routeMatches.Count -eq 0) { continue }
        $routeExpr = $routeMatches[$routeMatches.Count - 1].Groups[1].Value
        $controllerBases[$className] = Resolve-RouteExpr $routeExpr
    }

    $activeClass = $null
    $activeBase = $null
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match 'public\s+(?:sealed\s+)?class\s+(\w+)') {
            $activeClass = $Matches[1]
            $activeBase = $controllerBases[$activeClass]
        }
        if ($line -match '\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?\]') {
            $http = $Matches[1].ToUpper()
            $sub = $Matches[2]
            $auth = "JWT"
            $perm = ""
            for ($j = [Math]::Max(0, $i - 10); $j -le $i; $j++) {
                if ($lines[$j] -match '\[AllowAnonymous\]') { $auth = "Anonymous" }
                if ($lines[$j] -match 'Authorize\(Policy\s*=\s*"([^"]+)"\)') {
                    $perm = $Matches[1]
                    $auth = "JWT+Policy"
                }
                elseif ($lines[$j] -match '\[Authorize\]') { $auth = "JWT" }
            }
            $action = $null
            for ($k = $i; $k -lt [Math]::Min($i + 15, $lines.Count); $k++) {
                if ($lines[$k] -match '(?:public|private|protected)\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|IActionResult|ActionResult(?:<[^>]+>)?)\s+(\w+)\s*\(') {
                    $action = $Matches[1]
                    break
                }
            }
            if (-not $action -or -not $activeBase) { continue }
            $path = if ([string]::IsNullOrEmpty($sub)) { "/$activeBase" } else { "/$activeBase/$sub" }
            $path = ($path -replace '/{2,}', '/')
            $tag = ($activeClass -replace 'Controller$', '' -replace 'Controllers$', '')
            $endpoints.Add([pscustomobject]@{
                    Tag        = $tag
                    Controller = $activeClass
                    Method     = $http
                    Path       = $path
                    Action     = $action
                    Auth       = $auth
                    Permission = $perm
                })
        }
    }
}

$sorted = @($endpoints | Sort-Object Tag, Path, Method)
$sorted | Export-Csv -Path (Join-Path $outDir "endpoints.csv") -NoTypeInformation -Encoding UTF8

$nl = "`n"
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("# Reference API - v1$nl$nl")
[void]$sb.Append("Documentation generee depuis les controleurs (`scripts/export-api-docs.ps1`).$nl$nl")
[void]$sb.Append("| | |$nl")
[void]$sb.Append("|---|---|$nl")
[void]$sb.Append("| **Base URL locale** | ``http://localhost:5041`` |$nl")
[void]$sb.Append("| **Base URL cloud** | ``http://169.58.93.203:1804`` |$nl")
[void]$sb.Append("| **Swagger UI** | ``{base}/swagger`` |$nl")
[void]$sb.Append("| **OpenAPI JSON** | ``{base}/swagger/v1/swagger.json`` |$nl")
[void]$sb.Append("| **Auth** | ``Authorization: Bearer {token}`` |$nl")
[void]$sb.Append("| **Endpoints** | $($sorted.Count) |$nl$nl")
[void]$sb.Append("## Authentification$nl$nl")
[void]$sb.Append("1. ``POST /api/v1/auth/login`` avec body JSON userName/password$nl")
[void]$sb.Append("2. Utiliser ``data.accessToken`` dans l'en-tete Bearer$nl")
[void]$sb.Append("3. Endpoints publics : login, refresh, health$nl$nl")
[void]$sb.Append("## Mode Cloud (ReadOnly)$nl$nl")
[void]$sb.Append("Sur l'API Cloud, les ecritures (POST/PUT/PATCH/DELETE) sont refusees (403), sauf :$nl")
[void]$sb.Append("- ``/api/v1/auth/*``$nl")
[void]$sb.Append("- ``/api/v1/health``$nl")
[void]$sb.Append("- ``/api/v1/grades/entries``$nl$nl")
[void]$sb.Append("## Catalogue des endpoints$nl$nl")

foreach ($group in ($sorted | Group-Object Tag)) {
    [void]$sb.Append("### $($group.Name)$nl$nl")
    [void]$sb.Append("| Methode | Route | Action | Auth | Permission |$nl")
    [void]$sb.Append("|---------|-------|--------|------|------------|$nl")
    foreach ($ep in $group.Group) {
        $permCell = if ($ep.Permission) { '`' + $ep.Permission + '`' } else { "-" }
        $pathCell = '`' + $ep.Path + '`'
        [void]$sb.Append("| $($ep.Method) | $pathCell | $($ep.Action) | $($ep.Auth) | $permCell |$nl")
    }
    [void]$sb.Append($nl)
}

$mdPath = Join-Path $root "docs\api-reference.md"
[IO.File]::WriteAllText($mdPath, $sb.ToString(), [Text.UTF8Encoding]::new($false))

$paths = [ordered]@{}
foreach ($ep in $sorted) {
    $p = $ep.Path
    if (-not $paths.Contains($p)) { $paths[$p] = [ordered]@{} }
    $op = [ordered]@{
        tags        = @($ep.Tag)
        operationId = "$($ep.Tag)_$($ep.Action)"
        summary     = $ep.Action
        responses   = @{ "200" = @{ description = "OK" } }
    }
    if ($ep.Auth -ne "Anonymous") {
        $op["security"] = @(@{ Bearer = @() })
    }
    $paths[$p][$ep.Method.ToLowerInvariant()] = $op
}

$openapi = [ordered]@{
    openapi = "3.0.1"
    info    = [ordered]@{
        title       = "ERP Administration Scolaire RDC"
        version     = "v1"
        description = "Catalogue des endpoints. Pour les schemas DTO complets: swagger.json runtime."
    }
    servers = @(
        @{ url = "http://localhost:5041"; description = "Local" },
        @{ url = "http://169.58.93.203:1804"; description = "Cloud Coolify" }
    )
    paths      = $paths
    components = [ordered]@{
        securitySchemes = [ordered]@{
            Bearer = [ordered]@{
                type         = "http"
                scheme       = "bearer"
                bearerFormat = "JWT"
            }
        }
    }
}

$openapiPath = Join-Path $outDir "openapi.v1.json"
[IO.File]::WriteAllText($openapiPath, ($openapi | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))

Write-Host "Endpoints : $($sorted.Count)"
Write-Host "Markdown  : $mdPath"
Write-Host "OpenAPI   : $openapiPath"

if ($SwaggerUrl) {
    try {
        $r = Invoke-WebRequest -Uri $SwaggerUrl -TimeoutSec 60 -UseBasicParsing
        $fullPath = Join-Path $outDir "swagger.v1.json"
        [IO.File]::WriteAllText($fullPath, $r.Content, [Text.UTF8Encoding]::new($false))
        Write-Host "Swagger runtime : $fullPath ($($r.Content.Length) bytes)"
    }
    catch {
        Write-Host "Swagger download failed: $($_.Exception.Message)"
    }
}
