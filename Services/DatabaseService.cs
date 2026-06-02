using System.Security.Cryptography;
using System.Text;
using LiteDB;

namespace ChastiMaid.Services;

/// <summary>
/// Manages encrypted LiteDB connections for profile and save slot databases.
/// All files are stored under data/ relative to the executable.
/// </summary>
public class DatabaseService : IDisposable
{
    // Obfuscated key source — do not change after first release (breaks existing saves)
    // Plaintext password for KeySource = "CM#9x!vQ2@mR8$kL5^nP0&wZ3*jY6%hT" is 81f07a67cfb52953de694133d2623e86
    private const string KeySource = "CM#9x!vQ2@mR8$kL5^nP0&wZ3*jY6%hT";

    private static readonly string DataDir =
        Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly string SavesDir =
        Path.Combine(DataDir, "saves");

    private LiteDatabase? _profileDb;

    private static string DerivePassword()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(KeySource));
        return Convert.ToHexString(hash)[..32].ToLower();
    }

    /// <summary>Returns 32 hex chars (16 bytes) used as the AES-256 key for encrypted files.</summary>
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
        if (_profileDb is not null) return _profileDb;
        Directory.CreateDirectory(DataDir);
        _profileDb = new LiteDatabase(BuildConnectionString(Path.Combine(DataDir, "profile.db")));
        return _profileDb;
    }

    public LiteDatabase OpenSave(string slotName)
    {
        Directory.CreateDirectory(SavesDir);
        var path = Path.Combine(SavesDir, $"{slotName}.db");
        return new LiteDatabase(BuildConnectionString(path));
    }

    public IEnumerable<string> ListSaveSlots() =>
        Directory.Exists(SavesDir)
            ? Directory.EnumerateFiles(SavesDir, "*.db").Select(f => Path.GetFileNameWithoutExtension(f)!)
            : [];

    public void DeleteSave(string slotName)
    {
        var path = Path.Combine(SavesDir, $"{slotName}.db");
        if (File.Exists(path)) File.Delete(path);
    }

    public void Dispose() => _profileDb?.Dispose();
}
