using System.Security.Cryptography;
using System.Text;
using LiteDB;

namespace ChastiMaid.Services;

/// <summary>
///     Manages encrypted LiteDB connections for profile and save slot databases.
///     All files are stored under data/ in AppPaths.Root (the launcher's folder in a release).
/// </summary>
public class DatabaseService : IDisposable
{
    // Obfuscated key source — do not change after first release (breaks existing saves)
    // Plaintext password for KeySource = "CM#9x!vQ2@mR8$kL5^nP0&wZ3*jY6%hT" is 81f07a67cfb52953de694133d2623e86
    private const string KeySource = "CM#9x!vQ2@mR8$kL5^nP0&wZ3*jY6%hT";

    // LiteDB's default DateTime deserialization returns Kind=Local with the value shifted by the
    // machine's local UTC offset instead of preserving the original UTC instant (confirmed via a
    // round-trip probe). Every DateTime written in this app is DateTime.UtcNow-based, so recovering
    // the true instant on read is a plain ToUniversalTime() call — this only affects how bytes
    // already on disk are interpreted, not how they're written, so it's compatible with existing saves.
    static DatabaseService()
    {
        BsonMapper.Global.RegisterType<DateTime>(
            serialize: value => new BsonValue(value),
            deserialize: bson => bson.AsDateTime.ToUniversalTime());
    }

    private readonly string _dataDir;
    private readonly string _savesDir;
    private LiteDatabase? _profileDb;

    /// <param name="dataDir">Defaults to <c>data/</c> under <see cref="AppPaths.Root" />; tests pass their own.</param>
    public DatabaseService(string? dataDir = null)
    {
        _dataDir = dataDir ?? Path.Combine(AppPaths.Root, "data");
        _savesDir = Path.Combine(_dataDir, "saves");
    }

    public void Dispose()
    {
        _profileDb?.Dispose();
    }

    private static string DerivePassword()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(KeySource));
        return Convert.ToHexString(hash)[..32].ToLower();
    }

    /// <summary>Returns 32 hex chars (16 bytes) used as the AES-128 key for encrypted files.</summary>
    internal static string DeriveEncryptionKey()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(KeySource));
        // Uses the second half of the hash to keep the file key separate from the DB password
        return Convert.ToHexString(hash)[32..64].ToLower();
    }

    private static string BuildConnectionString(string path)
    {
        var password = DerivePassword();
        return $"Filename={path};Password={password};";
    }

    public LiteDatabase OpenProfile()
    {
        if (_profileDb is not null)
        {
            return _profileDb;
        }

        Directory.CreateDirectory(_dataDir);
        _profileDb = new LiteDatabase(BuildConnectionString(Path.Combine(_dataDir, "profile.db")));
        return _profileDb;
    }

    public LiteDatabase OpenSave(string slotName)
    {
        Directory.CreateDirectory(_savesDir);
        var path = Path.Combine(_savesDir, $"{slotName}.db");
        return new LiteDatabase(BuildConnectionString(path));
    }

    public IEnumerable<string> ListSaveSlots()
    {
        return Directory.Exists(_savesDir)
            ? Directory.EnumerateFiles(_savesDir, "*.db").Select(f => Path.GetFileNameWithoutExtension(f)!)
            : [];
    }

    public void DeleteSave(string slotName)
    {
        var path = Path.Combine(_savesDir, $"{slotName}.db");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
