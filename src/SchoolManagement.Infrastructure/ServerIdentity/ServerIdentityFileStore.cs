using System.Security.Cryptography;
using System.Text.Json;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Application.ServerIdentity;

namespace SchoolManagement.Infrastructure.ServerIdentity;

internal sealed class ServerIdentityFileStore
{
    public const string FileName = "ServerIdentity.json";
    public const string BackupFileName = "ServerIdentity.json.bak";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly string _backupPath;
    private readonly IEncryptionService _encryption;

    public ServerIdentityFileStore(string applicationBaseDirectory, IEncryptionService encryption)
    {
        _path = Path.Combine(applicationBaseDirectory, FileName);
        _backupPath = Path.Combine(applicationBaseDirectory, BackupFileName);
        _encryption = encryption;
    }

    /// <summary>
    /// Charge l'identité existante ou crée le fichier uniquement s'il est absent.
    /// </summary>
    public ServerIdentityFileModel LoadOrCreateIfMissing()
    {
        if (File.Exists(_path))
        {
            return LoadAndValidateExisting();
        }

        return CreateNewInitial();
    }

    private ServerIdentityFileModel LoadAndValidateExisting()
    {
        string json;
        try
        {
            json = File.ReadAllText(_path);
        }
        catch (Exception ex)
        {
            throw new ServerIdentityCorruptedException(
                $"Impossible de lire {FileName} : {ex.Message}. " +
                $"Restaurez une copie saine ou {BackupFileName} avant de redémarrer l'API.",
                ex);
        }

        ServerIdentityFileModel? model;
        try
        {
            model = JsonSerializer.Deserialize<ServerIdentityFileModel>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ServerIdentityCorruptedException(
                $"{FileName} n'est pas un JSON valide. " +
                $"Restaurez {BackupFileName} ou une sauvegarde d'exploitation. Aucune identité n'a été régénérée.",
                ex);
        }

        if (model is null
            || model.ServerInstanceId == Guid.Empty
            || model.KeyVersion < 1
            || string.IsNullOrWhiteSpace(model.PublicKeyBase64)
            || string.IsNullOrWhiteSpace(model.PublicKeyFingerprint)
            || string.IsNullOrWhiteSpace(model.PrivateKeyProtected))
        {
            throw new ServerIdentityCorruptedException(
                $"{FileName} est incomplet ou invalide (champs obligatoires manquants). " +
                "Restaurez le fichier depuis une sauvegarde ; le serveur ne régénère pas l'identité automatiquement.");
        }

        byte[] publicKeySpki;
        try
        {
            publicKeySpki = Convert.FromBase64String(model.PublicKeyBase64);
        }
        catch (FormatException ex)
        {
            throw new ServerIdentityCorruptedException(
                $"{FileName} : clé publique Base64 invalide.", ex);
        }

        var expectedFingerprint = ServerIdentityKeyMaterial.ComputeFingerprint(publicKeySpki);
        if (!string.Equals(model.PublicKeyFingerprint, expectedFingerprint, StringComparison.Ordinal))
        {
            throw new ServerIdentityCorruptedException(
                $"{FileName} : l'empreinte publique ne correspond pas à la clé enregistrée (intégrité compromise).");
        }

        try
        {
            var privatePlain = _encryption.Decrypt(model.PrivateKeyProtected);
            var privatePkcs8 = ServerIdentityKeyMaterial.DecodePrivateKeyFromStorage(privatePlain);
            using var rsaPrivate = RSA.Create();
            rsaPrivate.ImportPkcs8PrivateKey(privatePkcs8, out _);
            var derivedPublic = rsaPrivate.ExportSubjectPublicKeyInfo();
            var derivedFingerprint = ServerIdentityKeyMaterial.ComputeFingerprint(derivedPublic);
            if (!string.Equals(derivedFingerprint, expectedFingerprint, StringComparison.Ordinal))
            {
                throw new ServerIdentityCorruptedException(
                    $"{FileName} : la clé privée ne correspond pas à la clé publique.");
            }
        }
        catch (ServerIdentityCorruptedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ServerIdentityCorruptedException(
                $"{FileName} : impossible de déchiffrer ou valider la clé privée " +
                "(mot de passe/clé AES incorrect ou fichier endommagé). " +
                "Vérifiez ERP_CONFIG_ENCRYPTION_KEY en Docker ou restaurez ServerIdentity.json.bak.",
                ex);
        }

        ServerIdentityFilePermissions.ApplyRestrictive(_path);

        return model;
    }

    private ServerIdentityFileModel CreateNewInitial()
    {
        var (publicKey, privateKey, fingerprint) = ServerIdentityKeyMaterial.GenerateRsa2048();
        var model = new ServerIdentityFileModel
        {
            ServerInstanceId = Guid.NewGuid(),
            KeyVersion = 1,
            PublicKeyBase64 = Convert.ToBase64String(publicKey),
            PublicKeyFingerprint = fingerprint,
            PrivateKeyProtected = _encryption.Encrypt(
                ServerIdentityKeyMaterial.EncodePrivateKeyForStorage(privateKey)),
            InstalledAtUtc = DateTime.UtcNow
        };
        Save(model);
        return model;
    }

    public void Save(ServerIdentityFileModel model)
    {
        if (File.Exists(_path))
        {
            File.Copy(_path, _backupPath, overwrite: true);
        }

        var json = JsonSerializer.Serialize(model, JsonOptions);
        File.WriteAllText(_path, json);
        ServerIdentityFilePermissions.ApplyRestrictive(_path);
    }
}
