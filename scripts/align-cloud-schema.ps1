<#
.SYNOPSIS
  Aligne le schema SQL Cloud sur le schema SQL Local (tables + colonnes dbo).

.DESCRIPTION
  1. Lit ServeurDonnees.txt + ServeurDonneesCloud.txt (mot de passe DPAPI)
  2. Compare INFORMATION_SCHEMA (tables / colonnes)
  3. Cree les tables manquantes et ajoute les colonnes manquantes sur le Cloud
  4. N'efface rien sur le Cloud (pas de DROP)

.EXAMPLE
  .\scripts\align-cloud-schema.ps1
  .\scripts\align-cloud-schema.ps1 -WhatIf
#>
param(
    [string]$ApiDir = "",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($ApiDir)) {
    $ApiDir = Join-Path $root "src\SchoolManagement.API\bin\Debug\net8.0"
}
if (-not (Test-Path $ApiDir)) {
    throw "API directory not found: $ApiDir"
}
$ApiDir = (Resolve-Path $ApiDir).Path

$localFile = Join-Path $ApiDir "ServeurDonnees.txt"
$cloudFile = Join-Path $ApiDir "ServeurDonneesCloud.txt"
if (-not (Test-Path $localFile)) { throw "Missing $localFile" }
if (-not (Test-Path $cloudFile)) { throw "Missing $cloudFile" }

$tempCs = Join-Path $env:TEMP ("erp-align-schema-" + [guid]::NewGuid().ToString("N") + ".cs")
$tempExe = Join-Path $env:TEMP ("erp-align-schema-" + [guid]::NewGuid().ToString("N") + ".exe")

$cs = @'
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

class Program {
  static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SchoolManagement.ERP.Scolaire.RDC.v1");
  const string Prefix = "ENC:";

  static int Main(string[] args) {
    var localPath = args[0];
    var cloudPath = args[1];
    var whatIf = args.Length > 2 && string.Equals(args[2], "whatif", StringComparison.OrdinalIgnoreCase);

    string localCs, cloudCs;
    try {
      localCs = BuildCs(ReadConfig(localPath));
      cloudCs = BuildCs(ReadConfig(cloudPath));
    } catch (Exception ex) {
      Console.Error.WriteLine("CONFIG_ERROR: " + ex.Message);
      return 2;
    }

    Console.WriteLine("Comparing schemas (dbo)...");
    var localCols = LoadColumns(localCs);
    var cloudCols = LoadColumns(cloudCs);
    var localTables = localCols.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    var cloudTables = new HashSet<string>(cloudCols.Keys, StringComparer.OrdinalIgnoreCase);

    var missingTables = localTables.Where(t => !cloudTables.Contains(t)).ToList();
    var missingColumns = new List<Tuple<string, ColDef>>();
    foreach (var table in localTables) {
      if (!cloudTables.Contains(table)) continue;
      var remote = cloudCols[table].ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
      foreach (var col in localCols[table]) {
        if (!remote.ContainsKey(col.Name)) missingColumns.Add(Tuple.Create(table, col));
      }
    }

    var typeMismatches = new List<string>();
    foreach (var table in localTables) {
      if (!cloudTables.Contains(table)) continue;
      var remote = cloudCols[table].ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
      foreach (var col in localCols[table]) {
        ColDef remoteCol;
        if (!remote.TryGetValue(col.Name, out remoteCol)) continue;
        var localSig = col.SqlType + "|" + (col.IsNullable ? "NULL" : "NOT NULL") + (col.IsIdentity ? "|ID" : "");
        var cloudSig = remoteCol.SqlType + "|" + (remoteCol.IsNullable ? "NULL" : "NOT NULL") + (remoteCol.IsIdentity ? "|ID" : "");
        if (!string.Equals(localSig, cloudSig, StringComparison.OrdinalIgnoreCase)) {
          typeMismatches.Add(table + "." + col.Name + " LOCAL=[" + localSig + "] CLOUD=[" + cloudSig + "]");
        }
      }
    }

    Console.WriteLine("LOCAL_TABLES=" + localTables.Count);
    Console.WriteLine("CLOUD_TABLES=" + cloudTables.Count);
    Console.WriteLine("MISSING_TABLES=" + missingTables.Count);
    Console.WriteLine("MISSING_COLUMNS=" + missingColumns.Count);
    Console.WriteLine("TYPE_MISMATCHES=" + typeMismatches.Count);

    foreach (var t in missingTables) Console.WriteLine("MISSING_TABLE=" + t);
    foreach (var mc in missingColumns) Console.WriteLine("MISSING_COLUMN=" + mc.Item1 + "." + mc.Item2.Name + " (" + mc.Item2.SqlType + ")");
    foreach (var diff in typeMismatches) Console.WriteLine("TYPE_DIFF=" + diff);

    if (missingTables.Count == 0 && missingColumns.Count == 0 && typeMismatches.Count == 0) {
      Console.WriteLine("RESULT=ALREADY_ALIGNED");
      return 0;
    }
    if (missingTables.Count == 0 && missingColumns.Count == 0 && typeMismatches.Count > 0) {
      Console.WriteLine("RESULT=TYPE_DIFFS_ONLY (manual review required)");
      return 4;
    }

    if (whatIf) {
      Console.WriteLine("RESULT=WHATIF_ONLY");
      return 0;
    }

    using (var cloud = new SqlConnection(cloudCs)) {
      cloud.Open();
      var created = 0;
      var altered = 0;

      foreach (var table in missingTables) {
        var cols = localCols[table];
        var pk = LoadPrimaryKey(localCs, table);
        var sql = BuildCreateTable(table, cols, pk);
        Console.WriteLine("CREATE_TABLE=" + table);
        using (var cmd = cloud.CreateCommand()) {
          cmd.CommandTimeout = 120;
          cmd.CommandText = sql;
          cmd.ExecuteNonQuery();
        }
        created++;
      }

      // Reload cloud columns after creates
      cloudCols = LoadColumns(cloudCs);
      foreach (var table in localTables) {
        if (!cloudCols.ContainsKey(table)) continue;
        var remote = cloudCols[table].ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
        foreach (var col in localCols[table]) {
          if (remote.ContainsKey(col.Name)) continue;
          var sql = BuildAddColumn(table, col);
          Console.WriteLine("ADD_COLUMN=" + table + "." + col.Name);
          using (var cmd = cloud.CreateCommand()) {
            cmd.CommandTimeout = 120;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
          }
          altered++;
        }
      }

      Console.WriteLine("CREATED_TABLES=" + created);
      Console.WriteLine("ADDED_COLUMNS=" + altered);
    }

    // Final verify
    cloudCols = LoadColumns(cloudCs);
    var stillMissingTables = localTables.Where(t => !cloudCols.ContainsKey(t)).ToList();
    var stillMissingCols = 0;
    foreach (var table in localTables) {
      if (!cloudCols.ContainsKey(table)) continue;
      var remote = new HashSet<string>(cloudCols[table].Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
      stillMissingCols += localCols[table].Count(c => !remote.Contains(c.Name));
    }

    Console.WriteLine("VERIFY_MISSING_TABLES=" + stillMissingTables.Count);
    Console.WriteLine("VERIFY_MISSING_COLUMNS=" + stillMissingCols);
    if (stillMissingTables.Count == 0 && stillMissingCols == 0) {
      Console.WriteLine("RESULT=ALIGNED");
      return 0;
    }
    Console.WriteLine("RESULT=PARTIAL");
    return 3;
  }

  class ColDef {
    public string Name;
    public string DataType;
    public int? CharLen;
    public byte? NumPrecision;
    public int? NumScale;
    public short? DatePrecision;
    public bool IsNullable;
    public bool IsIdentity;
    public string SqlType;
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
    if (!cfg.TryGetValue("UTILISATEUR", out user) || string.IsNullOrWhiteSpace(user)) throw new Exception("UTILISATEUR missing");
    if (!cfg.TryGetValue("MOTDEPASSE", out encPwd) || string.IsNullOrWhiteSpace(encPwd)) throw new Exception("MOTDEPASSE missing");
    var port = 1433;
    string portRaw;
    if (cfg.TryGetValue("PORT", out portRaw)) int.TryParse(portRaw, out port);

    if (!encPwd.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
      throw new Exception("Password is not DPAPI-encrypted (ENC:) in config file.");
    var protectedBytes = Convert.FromBase64String(encPwd.Substring(Prefix.Length));
    var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
    var pwd = Encoding.UTF8.GetString(plainBytes);

    // Named instance (local) : keep as-is. Remote host : host,port
    var dataSource = server;
    if (server.IndexOf('\\') < 0 && port > 0 && port != 1433) dataSource = server + "," + port;
    else if (server.IndexOf('\\') < 0 && Regex.IsMatch(server, @"^\d+\.\d+\.\d+\.\d+$")) dataSource = server + "," + port;

    var b = new SqlConnectionStringBuilder();
    b.DataSource = dataSource;
    b.InitialCatalog = database;
    b.UserID = user;
    b.Password = pwd;
    b.TrustServerCertificate = true;
    b.Encrypt = false;
    b.ConnectTimeout = 30;
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
       COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS c
INNER JOIN INFORMATION_SCHEMA.TABLES t
  ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
WHERE c.TABLE_SCHEMA = 'dbo' AND t.TABLE_TYPE = 'BASE TABLE'
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION";
        using (var r = cmd.ExecuteReader()) {
          while (r.Read()) {
            var table = r.GetString(0);
            var col = new ColDef {
              Name = r.GetString(1),
              DataType = r.GetString(2),
              CharLen = r.IsDBNull(3) ? (int?)null : Convert.ToInt32(r.GetValue(3)),
              NumPrecision = r.IsDBNull(4) ? (byte?)null : Convert.ToByte(r.GetValue(4)),
              NumScale = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5)),
              DatePrecision = r.IsDBNull(6) ? (short?)null : Convert.ToInt16(r.GetValue(6)),
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
SELECT kcu.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
  ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
WHERE tc.TABLE_SCHEMA = 'dbo' AND tc.TABLE_NAME = @t AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY kcu.ORDINAL_POSITION";
        cmd.Parameters.AddWithValue("@t", table);
        using (var r = cmd.ExecuteReader()) {
          while (r.Read()) cols.Add(r.GetString(0));
        }
      }
    }
    return cols;
  }

  static string FormatSqlType(ColDef c) {
    var dt = c.DataType.ToLowerInvariant();
    switch (dt) {
      case "nvarchar":
      case "varchar":
      case "nchar":
      case "char":
      case "varbinary":
      case "binary":
        if (c.CharLen.HasValue && c.CharLen.Value < 0) return dt.ToUpperInvariant() + "(MAX)";
        return dt.ToUpperInvariant() + "(" + (c.CharLen ?? 1) + ")";
      case "decimal":
      case "numeric":
        return dt.ToUpperInvariant() + "(" + (c.NumPrecision ?? 18) + "," + (c.NumScale ?? 2) + ")";
      case "datetime2":
      case "time":
      case "datetimeoffset":
        if (c.DatePrecision.HasValue) return dt.ToUpperInvariant() + "(" + c.DatePrecision.Value + ")";
        return dt.ToUpperInvariant();
      case "float":
        if (c.NumPrecision.HasValue) return "FLOAT(" + c.NumPrecision.Value + ")";
        return "FLOAT";
      default:
        return dt.ToUpperInvariant();
    }
  }

  static string Quote(string name) { return "[" + name.Replace("]", "]]") + "]"; }

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
    // Pour tables deja remplies : ajouter nullable puis, si besoin, NOT NULL avec default.
    if (col.IsNullable || col.IsIdentity) {
      return "ALTER TABLE " + Quote(table) + " ADD " + Quote(col.Name) + " " + col.SqlType +
             (col.IsIdentity ? " IDENTITY(1,1)" : "") +
             (col.IsNullable ? " NULL" : " NOT NULL") + ";";
    }

    var def = DefaultLiteral(col);
    return "ALTER TABLE " + Quote(table) + " ADD " + Quote(col.Name) + " " + col.SqlType +
           " NOT NULL CONSTRAINT " + Quote("DF_tmp_" + table + "_" + col.Name) + " DEFAULT " + def + ";" +
           " ALTER TABLE " + Quote(table) + " DROP CONSTRAINT " + Quote("DF_tmp_" + table + "_" + col.Name) + ";";
  }

  static string DefaultLiteral(ColDef col) {
    var dt = col.DataType.ToLowerInvariant();
    switch (dt) {
      case "uniqueidentifier": return "NEWID()";
      case "bit": return "0";
      case "int": case "bigint": case "smallint": case "tinyint": return "0";
      case "decimal": case "numeric": case "money": case "smallmoney": case "float": case "real": return "0";
      case "date": return "'19000101'";
      case "datetime": case "datetime2": case "smalldatetime": case "datetimeoffset": return "SYSUTCDATETIME()";
      case "time": return "'00:00:00'";
      case "nvarchar": case "varchar": case "nchar": case "char": case "text": case "ntext": return "N''";
      case "varbinary": case "binary": case "image": return "0x";
      default: return "NULL"; // should not happen for NOT NULL path without default
    }
  }
}
'@

Set-Content -Path $tempCs -Value $cs -Encoding ASCII

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found" }

& $csc /nologo /r:System.Data.dll /out:$tempExe $tempCs | Out-Null
if (-not (Test-Path $tempExe)) { throw "Failed to compile align-cloud-schema helper" }

$mode = if ($WhatIf) { "whatif" } else { "apply" }
Write-Host "Running schema align ($mode)..."
$prev = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$output = & $tempExe $localFile $cloudFile $mode 2>&1
$exit = $LASTEXITCODE
$ErrorActionPreference = $prev

Remove-Item $tempCs, $tempExe -Force -ErrorAction SilentlyContinue

$output | ForEach-Object { Write-Host $_ }

if ($exit -ne 0 -and $exit -ne 3) {
    throw "align-cloud-schema failed (exit $exit)"
}

if ($exit -eq 0) {
    Write-Host ""
    Write-Host "Cloud schema aligned on local (tables + columns)."
} else {
    Write-Host ""
    Write-Host "Partial alignment - review MISSING_* lines above."
}
exit $exit
