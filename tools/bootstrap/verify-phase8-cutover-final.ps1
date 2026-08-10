#Requires -Version 5.1
<#
.SYNOPSIS
  Phase 8 cutover final — verify /health (no legacy Schools) + establishment start/complete.
  Reuses existing local credential (does NOT regenerate).
#>
param(
    [string]$BootstrapUrl = "https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io",
    [string]$CredentialMetaPath = "artifacts\ecole-test-credential.local.json"
)

$ErrorActionPreference = "Stop"
$BootstrapUrl = $BootstrapUrl.TrimEnd("/")
$expectedSchoolId = [guid]"71635f62-b975-479d-9e6e-fbacd05e4996"

Write-Host "=== /health ==="
$h = Invoke-RestMethod -Uri "$BootstrapUrl/health" -TimeoutSec 30
$h | ConvertTo-Json -Depth 6

$checks = @(
    @{ Name = "registry"; Expected = "sql"; Actual = [string]$h.registry },
    @{ Name = "schoolsRegistered"; Expected = "1"; Actual = [string]$h.schoolsRegistered },
    @{ Name = "ecoleTestPresent"; Expected = "True"; Actual = [string]$h.ecoleTestPresent },
    @{ Name = "activeCredentials"; Expected = "1"; Actual = [string]$h.activeCredentials },
    @{ Name = "legacyEnvSchoolsConfigured"; Expected = "0"; Actual = [string]$h.legacyEnvSchoolsConfigured },
    @{ Name = "allowLegacyEnvSchoolRegistry"; Expected = "False"; Actual = [string]$h.allowLegacyEnvSchoolRegistry }
)
$healthOk = $true
foreach ($c in $checks) {
    $ok = $c.Actual -eq $c.Expected
    if (-not $ok) { $healthOk = $false }
    Write-Host ("{0}: actual={1} expected={2} -> {3}" -f $c.Name, $c.Actual, $c.Expected, $(if ($ok) { "OK" } else { "FAIL" }))
}
if (-not $healthOk) { throw "Health cutover checks failed (legacy still present or SQL registry incomplete)." }

if (-not (Test-Path $CredentialMetaPath)) {
    throw "Credential meta missing: $CredentialMetaPath (do not regenerate — restore from prior seed)."
}
$meta = Get-Content $CredentialMetaPath -Raw | ConvertFrom-Json
$deviceId = "phase8-cutover-" + [guid]::NewGuid().ToString("N").Substring(0, 8)

$tmpDir = Join-Path (Get-Location) "artifacts\est-cutover-tmp"
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
@'
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

var schoolId = Guid.Parse(args[0]);
var credId = Guid.Parse(args[1]);
var version = int.Parse(args[2]);
var secretHash = args[3];
var deviceId = args[4];
var bootstrap = args[5].TrimEnd('/');

var derived = SHA256.HashData(Encoding.UTF8.GetBytes(secretHash.Trim()));
var jwt = new JwtSecurityToken(
    issuer: $"school:{schoolId:D}",
    audience: "erp-scolaire-mobile-establish",
    claims: new List<Claim>
    {
        new("token_type", "school_establishment"),
        new("school_id", schoolId.ToString("D")),
        new(JwtRegisteredClaimNames.Jti, credId.ToString("D")),
        new("ver", version.ToString()),
    },
    notBefore: DateTime.UtcNow.AddMinutes(-1),
    expires: DateTime.UtcNow.AddDays(3650),
    signingCredentials: new SigningCredentials(new SymmetricSecurityKey(derived), SecurityAlgorithms.HmacSha256));
var token = new JwtSecurityTokenHandler().WriteToken(jwt);

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
var startResp = await http.PostAsync($"{bootstrap}/establishment/start",
    new StringContent(JsonSerializer.Serialize(new { token, deviceId }), Encoding.UTF8, "application/json"));
var startText = await startResp.Content.ReadAsStringAsync();
Console.WriteLine("START_STATUS=" + (int)startResp.StatusCode);
Console.WriteLine("START_BODY=" + startText);
startResp.EnsureSuccessStatusCode();
var sessionId = JsonDocument.Parse(startText).RootElement.GetProperty("establishmentSessionId").GetGuid();

var completeResp = await http.PostAsync($"{bootstrap}/establishment/complete",
    new StringContent(JsonSerializer.Serialize(new { establishmentSessionId = sessionId, deviceId }), Encoding.UTF8, "application/json"));
var completeText = await completeResp.Content.ReadAsStringAsync();
Console.WriteLine("COMPLETE_STATUS=" + (int)completeResp.StatusCode);
Console.WriteLine("COMPLETE_BODY=" + completeText);
completeResp.EnsureSuccessStatusCode();
using var doc = JsonDocument.Parse(completeText);
var root = doc.RootElement;
var bindingSchoolId = root.GetProperty("schoolId").GetGuid();
var bindingKind = root.GetProperty("extensions").GetProperty("bindingKind").GetString();
var credVer = root.GetProperty("extensions").GetProperty("establishmentCredentialVersion").GetInt32();
Console.WriteLine("BINDING_SCHOOL_ID=" + bindingSchoolId.ToString("D"));
Console.WriteLine("BINDING_KIND=" + bindingKind);
Console.WriteLine("CRED_VERSION=" + credVer);
Console.WriteLine(bindingSchoolId == schoolId && bindingKind == "school_establishment" && credVer == version
    ? "CUTOVER_TEST=PASS"
    : "CUTOVER_TEST=FAIL");
'@ | Set-Content "$tmpDir\Program.cs" -Encoding UTF8

@"
<Project Sdk=`"Microsoft.NET.Sdk`">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=`"System.IdentityModel.Tokens.Jwt`" Version=`"8.0.2`" />
  </ItemGroup>
</Project>
"@ | Set-Content "$tmpDir\EstCutover.csproj" -Encoding UTF8

Write-Host "=== establishment start/complete (existing credential) ==="
Push-Location $tmpDir
dotnet run --quiet -- $expectedSchoolId $meta.credentialId $meta.credentialVersion $meta.secretHash $deviceId $BootstrapUrl
$exit = $LASTEXITCODE
Pop-Location
if ($exit -ne 0) { throw "establishment test failed" }
Write-Host "PHASE8_CUTOVER_VERIFY=OK"
