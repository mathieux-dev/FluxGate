namespace FluxPay.Core.Services;

public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
    string Hash(string input);
    bool VerifyHash(string input, string hash);
}
