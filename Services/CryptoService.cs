using System.Security.Cryptography;

namespace ChastiMaid.Services;

/// <summary>
/// Crittografia file AES-256-GCM.
/// Formato file: [12 byte nonce][16 byte tag][ciphertext]
/// </summary>
public static class CryptoService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>Deriva la chiave AES-256 (32 byte) dalla stessa sorgente usata per i DB.</summary>
    private static byte[] DeriveKey()
    {
        var hex = DatabaseService.DeriveEncryptionKey();
        return Convert.FromHexString(hex);
    }

    public static void EncryptFile(string inputPath, string outputPath)
    {
        var plaintext = File.ReadAllBytes(inputPath);
        var key = DeriveKey();
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        using var fs = File.Create(outputPath);
        fs.Write(nonce);
        fs.Write(tag);
        fs.Write(ciphertext);
    }

    public static byte[] DecryptFile(string encryptedPath)
    {
        var data = File.ReadAllBytes(encryptedPath);
        var key = DeriveKey();

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(NonceSize, TagSize);
        var ciphertext = data.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
