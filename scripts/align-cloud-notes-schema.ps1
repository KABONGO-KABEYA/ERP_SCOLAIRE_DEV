<#
.SYNOPSIS
  Aligne le schéma SQL Cloud sur le local pour le module Notes / Cotation uniquement.

.DESCRIPTION
  Tables concernées : EvaluationTypes, Evaluations, GradeEntries, MaximaParPeriode, CourseAssignments.
  N'ajoute que tables/colonnes manquantes sur le cloud (pas de DROP, pas d'autres tables).

.EXAMPLE
  .\scripts\align-cloud-notes-schema.ps1
  .\scripts\align-cloud-notes-schema.ps1 -WhatIf
#>
param(
    [string]$ApiDir = "",
    [string]$CloudConfigPath = "",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($ApiDir)) {
    $ApiDir = Join-Path $root "src\SchoolManagement.API\bin\Debug\net8.0"
}
if (-not (Test-Path $ApiDir)) {
    throw "Répertoire API introuvable : $ApiDir (compilez d'abord l'API)."
}
$ApiDir = (Resolve-Path $ApiDir).Path

$localFile = Join-Path $ApiDir "ServeurDonnees.txt"
if (-not (Test-Path $localFile)) { throw "Fichier local introuvable : $localFile" }

if ([string]::IsNullOrWhiteSpace($CloudConfigPath)) {
    $CloudConfigPath = Join-Path $ApiDir "ServeurDonneesCloud.txt"
}
if (-not (Test-Path $CloudConfigPath)) {
    throw @"
ServeurDonneesCloud.txt introuvable : $CloudConfigPath
Configurez la sync cloud d'abord, par exemple :
  .\scripts\configure-cloud-sync.ps1 -Password 'VOTRE_MDP' -ApiDir '$ApiDir'
"@
}

$notesTables = @(
    "EvaluationTypes",
    "Evaluations",
    "GradeEntries",
    "MaximaParPeriode",
    "CourseAssignments"
)

Write-Host "Build des projets (Application + Infrastructure)..."
dotnet build (Join-Path $root "src\SchoolManagement.Infrastructure\SchoolManagement.Infrastructure.csproj") `
    -c Debug --nologo -v q | Out-Null

$infraDll = Join-Path $root "src\SchoolManagement.Infrastructure\bin\Debug\net8.0\SchoolManagement.Infrastructure.dll"
$appDll = Join-Path $root "src\SchoolManagement.Application\bin\Debug\net8.0\SchoolManagement.Application.dll"
if (-not (Test-Path $infraDll)) { throw "DLL Infrastructure introuvable." }
if (-not (Test-Path $appDll)) { throw "DLL Application introuvable." }

$tempCs = Join-Path $env:TEMP ("erp-align-notes-" + [guid]::NewGuid().ToString("N") + ".cs")
$tempExe = Join-Path $env:TEMP ("erp-align-notes-" + [guid]::NewGuid().ToString("N") + ".exe")

$tablesArg = [string]::Join(",", $notesTables)

$cs = @'
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

class Program {
  static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SchoolManagement.ERP.Scolaire.RDC.v1");
  const string Prefix = "ENC:";

  static int Main(string[] args) {
    var localPath = args[0];
    var cloudPath = args[1];
    var infraDll = args[2];
    var appDll = args[3];
    var tables = new HashSet<string>(args[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
    var whatIf = args.Length > 5 && string.Equals(args[5], "whatif", StringComparison.OrdinalIgnoreCase);

    string localCs, cloudCs;
    try {
      localCs = BuildCs(ReadConfig(localPath));
      cloudCs = BuildCs(ReadConfig(cloudPath));
    } catch (Exception ex) {
      Console.Error.WriteLine("CONFIG_ERROR: " + ex.Message);
      return 2;
    }

    Console.WriteLine("Comparaison schéma module Notes (référence = local, cible = cloud)...");
    var localCols = LoadColumns(localCs);
    var cloudCols = LoadColumns(cloudCs);
    var localTables = localCols.Keys.Where(t => tables.Contains(t)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    var cloudTableSet = new HashSet<string>(cloudCols.Keys, StringComparer.OrdinalIgnoreCase);

    var missingTables = localTables.Where(t => !cloudTableSet.Contains(t)).ToList();
    var missingColumns = new List<Tuple<string, ColDef>>();
    foreach (var table in localTables) {
      if (!cloudTableSet.Contains(table)) continue;
      var remote = cloudCols[table].ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
      foreach (var col in localCols[table]) {
        if (!remote.ContainsKey(col.Name)) missingColumns.Add(Tuple.Create(table, col));
      }
    }

    Console.WriteLine("NOTES_TABLES=" + localTables.Count);
    Console.WriteLine("MISSING_TABLES=" + missingTables.Count);
    Console.WriteLine("MISSING_COLUMNS=" + missingColumns.Count);
    foreach (var t in missingTables) Console.WriteLine("MISSING_TABLE=" + t);
    foreach (var mc in missingColumns) Console.WriteLine("MISSING_COLUMN=" + mc.Item1 + "." + mc.Item2.Name + " (" + mc.Item2.SqlType + ")");

    if (missingTables.Count == 0 && missingColumns.Count == 0) {
      Console.WriteLine("RESULT=ALREADY_ALIGNED");
      return RunInitializers(cloudCs, infraDll, appDll);
    }

    if (whatIf) {
      Console.WriteLine("RESULT=WHATIF_ONLY");
      return 0;
    }

    using (var cloud = new SqlConnection(cloudCs)) {
      cloud.Open();
      foreach (var table in missingTables) {
        var cols = localCols[table];
        var pk = LoadPrimaryKey(localCs, table);
        var sql = BuildCreateTable(table, cols, pk);
        Console.WriteLine("CREATE_TABLE=" + table);
        Exec(cloud, sql);
      }

      cloudCols = LoadColumns(cloudCs);
      foreach (var table in localTables) {
        if (!cloudCols.ContainsKey(table)) continue;
        var remote = cloudCols[table].ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
        foreach (var col in localCols[table]) {
          if (remote.ContainsKey(col.Name)) continue;
          Console.WriteLine("ADD_COLUMN=" + table + "." + col.Name);
          Exec(cloud, BuildAddColumn(table, col));
        }
      }
    }

    return RunInitializers(cloudCs, infraDll, appDll);
  }

  static int RunInitializers(string cloudCs, string infraDll, string appDll) {
    Console.WriteLine("Running notes schema initializers on cloud...");
    try {
      var infra = Assembly.LoadFrom(infraDll);
      RunInitializer(infra, "SchoolManagement.Infrastructure.Persistence.CourseAssignmentSchemaInitializer", cloudCs, "EnsureUpdatedAsync");
      RunInitializer(infra, "SchoolManagement.Infrastructure.Persistence.EvaluationSchemaInitializer", cloudCs, "EnsureUpdatedAsync");
      RunInitializer(infra, "SchoolManagement.Infrastructure.Persistence.MaximaParPeriodeSchemaInitializer", cloudCs, "EnsureCreatedAsync");

      Console.WriteLine("RESULT=INITIALIZERS_OK");
      return 0;
    } catch (Exception ex) {
      Console.Error.WriteLine("INITIALIZER_ERROR: " + ex.Message);
      Console.WriteLine("RESULT=INITIALIZERS_FAILED");
      return 5;
    }
  }

  static void RunInitializer(Assembly infra, string typeName, string cs, string method) {
    if (string.IsNullOrEmpty(method)) method = "EnsureUpdatedAsync";
    var type = infra.GetType(typeName);
    if (type == null) throw new Exception("Type not found: " + typeName);
    var loggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(type);
    var logger = Activator.CreateInstance(loggerType);
    var instance = Activator.CreateInstance(type, cs, logger);
    var mi = type.GetMethod(method);
    if (mi == null) throw new Exception("Method not found: " + method);
    var task = (Task)mi.Invoke(instance, new object[] { CancellationToken.None });
    task.GetAwaiter().GetResult();
    Console.WriteLine("INITIALIZER=" + typeName.Substring(typeName.LastIndexOf('.') + 1));
  }

  static void Exec(SqlConnection cn, string sql) {
    using (var cmd = cn.CreateCommand()) {
      cmd.CommandTimeout = 120;
      cmd.CommandText = sql;
      cmd.ExecuteNonQuery();
    }
  }

  class ColDef {
    public string Name = "";
    public string DataType = "";
    public int? CharLen;
    public byte? NumPrecision;
    public int? NumScale;
    public short? DatePrecision;
    public bool IsNullable;
    public bool IsIdentity;
    public string SqlType = "";
  }

  static Dictionary<string, string> ReadConfig(string path) {
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var raw in File.ReadAllLines(path)) {
      var line = raw.Trim();
      if (line.Length == 0 || line.StartsWith("#")) continue;
      var i = line.IndexOf('=');
      if (i <= 0) continue;
      map[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
    }
    return map;
  }

  static string BuildCs(Dictionary<string, string> cfg) {
    string server, database, user, encPwd;
    if (!cfg.TryGetValue("SERVEUR", out server) || string.IsNullOrWhiteSpace(server)) throw new Exception("SERVEUR missing");
    if (!cfg.TryGetValue("BASE", out database) || string.IsNullOrWhiteSpace(database)) throw new Exception("BASE missing");
    if (!cfg.TryGetValue("UTILISATEUR", out user)) user = "";
    if (!cfg.TryGetValue("MOTDEPASSE", out encPwd)) encPwd = "";
    var port = 1433;
    string portRaw;
    if (cfg.TryGetValue("PORT", out portRaw)) {
      int p;
      if (int.TryParse(portRaw, out p)) port = p;
    }

    string authRaw;
    var auth = cfg.TryGetValue("AUTHENTIFICATION", out authRaw) ? authRaw.Trim() : "SQL";
    var b = new SqlConnectionStringBuilder();
    var dataSource = server;
    if (server.IndexOf('\\') < 0 && Regex.IsMatch(server, @"^\d+\.\d+\.\d+\.\d+$")) dataSource = server + "," + port;
    else if (server.IndexOf('\\') < 0 && port > 0 && port != 1433) dataSource = server + "," + port;
    b.DataSource = dataSource;
    b.InitialCatalog = database;
    b.TrustServerCertificate = true;
    b.Encrypt = false;
    b.ConnectTimeout = 30;

    if (auth.Equals("WINDOWS", StringComparison.OrdinalIgnoreCase)) {
      b.IntegratedSecurity = true;
    } else {
      if (string.IsNullOrWhiteSpace(user)) throw new Exception("UTILISATEUR missing");
      if (string.IsNullOrWhiteSpace(encPwd)) throw new Exception("MOTDEPASSE missing");
      if (!encPwd.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Password is not DPAPI-encrypted (ENC:).");
      var protectedBytes = Convert.FromBase64String(encPwd.Substring(Prefix.Length));
      var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
      b.UserID = user;
      b.Password = Encoding.UTF8.GetString(plainBytes);
    }
    return b.ConnectionString;
  }

  static Dictionary<string, List<ColDef>> LoadColumns(string cs) {
    var result = new Dictionary<string, List<ColDef>>(StringComparer.OrdinalIgnoreCase);
    using (var cn = new SqlConnection(cs)) {
      cn.Open();
      using (var cmd = cn.CreateCommand()) {
        cmd.CommandText = @"
SELECT c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH,
       c.NUMERIC_PRECISION, c.NUMERIC_SCALE, c.DATETIME_PRECISION, c.IS_NULLABLE,
       COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA)+'.'+QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS c
INNER JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_SCHEMA=c.TABLE_SCHEMA AND t.TABLE_NAME=c.TABLE_NAME
WHERE c.TABLE_SCHEMA='dbo' AND t.TABLE_TYPE='BASE TABLE'
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION";
        using (var r = cmd.ExecuteReader()) {
          while (r.Read()) {
            var table = r.GetString(0);
            var col = new ColDef {
              Name = r.GetString(1),
              DataType = r.GetString(2),
              CharLen = r.IsDBNull(3) ? null : Convert.ToInt32(r.GetValue(3)),
              NumPrecision = r.IsDBNull(4) ? null : Convert.ToByte(r.GetValue(4)),
              NumScale = r.IsDBNull(5) ? null : Convert.ToInt32(r.GetValue(5)),
              DatePrecision = r.IsDBNull(6) ? null : Convert.ToInt16(r.GetValue(6)),
              IsNullable = string.Equals(r.GetString(7), "YES", StringComparison.OrdinalIgnoreCase),
              IsIdentity = !r.IsDBNull(8) && Convert.ToInt32(r.GetValue(8)) == 1
            };
            col.SqlType = FormatSqlType(col);
            if (!result.ContainsKey(table)) result[table] = new List<ColDef>();
            result[table].Add(col);
          }
        }
      }
    }
    return result;
  }

  static List<string> LoadPrimaryKey(string cs, string table) {
    var cols = new List<string>();
    using (var cn = new SqlConnection(cs)) {
      cn.Open();
      using (var cmd = cn.CreateCommand()) {
        cmd.CommandText = @"
SELECT kcu.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON tc.CONSTRAINT_NAME=kcu.CONSTRAINT_NAME AND tc.TABLE_SCHEMA=kcu.TABLE_SCHEMA
WHERE tc.TABLE_SCHEMA='dbo' AND tc.TABLE_NAME=@t AND tc.CONSTRAINT_TYPE='PRIMARY KEY'
ORDER BY kcu.ORDINAL_POSITION";
        cmd.Parameters.AddWithValue("@t", table);
        using (var r = cmd.ExecuteReader()) while (r.Read()) cols.Add(r.GetString(0));
      }
    }
    return cols;
  }

  static string FormatSqlType(ColDef c) {
    var dt = c.DataType.ToLowerInvariant();
    switch (dt) {
      case "nvarchar": case "varchar": case "nchar": case "char": case "varbinary": case "binary":
        if (c.CharLen.HasValue && c.CharLen.Value < 0) return dt.ToUpperInvariant() + "(MAX)";
        return dt.ToUpperInvariant() + "(" + (c.CharLen ?? 1) + ")";
      case "decimal": case "numeric":
        return dt.ToUpperInvariant() + "(" + (c.NumPrecision ?? 18) + "," + (c.NumScale ?? 2) + ")";
      case "datetime2": case "time": case "datetimeoffset":
        if (c.DatePrecision.HasValue) return dt.ToUpperInvariant() + "(" + c.DatePrecision.Value + ")";
        return dt.ToUpperInvariant();
      case "float":
        if (c.NumPrecision.HasValue) return "FLOAT(" + c.NumPrecision.Value + ")";
        return "FLOAT";
      default: return dt.ToUpperInvariant();
    }
  }

  static string Quote(string name) {
    return "[" + name.Replace("]", "]]") + "]";
  }

  static string BuildCreateTable(string table, List<ColDef> cols, List<string> pk) {
    var sb = new StringBuilder();
    sb.AppendLine("CREATE TABLE " + Quote(table) + " (");
    for (var i = 0; i < cols.Count; i++) {
      var c = cols[i];
      sb.Append("  " + Quote(c.Name) + " " + c.SqlType);
      if (c.IsIdentity) sb.Append(" IDENTITY(1,1)");
      sb.Append(c.IsNullable ? " NULL" : " NOT NULL");
      if (i < cols.Count - 1 || (pk != null && pk.Count > 0)) sb.Append(",");
      sb.AppendLine();
    }
    if (pk != null && pk.Count > 0) {
      sb.Append("  CONSTRAINT " + Quote("PK_" + table) + " PRIMARY KEY (");
      sb.Append(string.Join(", ", pk.Select(Quote)));
      sb.AppendLine(")");
    }
    sb.AppendLine(");");
    return sb.ToString();
  }

  static string BuildAddColumn(string table, ColDef col) {
    if (col.IsNullable || col.IsIdentity)
      return "ALTER TABLE " + Quote(table) + " ADD " + Quote(col.Name) + " " + col.SqlType +
             (col.IsIdentity ? " IDENTITY(1,1)" : "") + (col.IsNullable ? " NULL" : " NOT NULL") + ";";
    var def = DefaultLiteral(col);
    return "ALTER TABLE " + Quote(table) + " ADD " + Quote(col.Name) + " " + col.SqlType +
           " NOT NULL CONSTRAINT " + Quote("DF_tmp_" + table + "_" + col.Name) + " DEFAULT " + def + ";" +
           " ALTER TABLE " + Quote(table) + " DROP CONSTRAINT " + Quote("DF_tmp_" + table + "_" + col.Name) + ";";
  }

  static string DefaultLiteral(ColDef col) {
    switch (col.DataType.ToLowerInvariant()) {
      case "uniqueidentifier": return "NEWID()";
      case "bit": return "0";
      case "int": case "bigint": case "smallint": case "tinyint": return "0";
      case "decimal": case "numeric": case "money": case "smallmoney": case "float": case "real": return "0";
      case "date": return "'19000101'";
      case "datetime": case "datetime2": case "smalldatetime": case "datetimeoffset": return "SYSUTCDATETIME()";
      case "nvarchar": case "varchar": case "nchar": case "char": return "N''";
      default: return "NULL";
    }
  }
}
'@

Set-Content -Path $tempCs -Value $cs -Encoding ASCII
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$nullLoggerDll = (Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.extensions.logging.abstractions" -Recurse -Filter "Microsoft.Extensions.Logging.Abstractions.dll" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1).FullName

$refs = @("/r:System.Data.dll")
if ($nullLoggerDll) { $refs += "/r:$nullLoggerDll" }

& $csc /nologo @refs /out:$tempExe $tempCs
if (-not (Test-Path $tempExe)) { throw "Échec compilation align-cloud-notes-schema" }

$mode = if ($WhatIf) { "whatif" } else { "apply" }
Write-Host "Alignement module Notes sur le cloud ($mode)..."
$output = & $tempExe $localFile $CloudConfigPath $infraDll $appDll $tablesArg $mode 2>&1
Remove-Item $tempCs, $tempExe -Force -ErrorAction SilentlyContinue
$output | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 5) {
    throw "align-cloud-notes-schema failed (exit $LASTEXITCODE)"
}

Write-Host ""
Write-Host "Schéma cloud module Notes traité."
