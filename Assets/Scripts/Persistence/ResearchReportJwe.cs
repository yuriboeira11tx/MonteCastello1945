using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FEB.Persistence
{
    // JWE compacto (RFC 7516): o JWT assinado fica dentro do conteúdo cifrado.
    // Apenas a chave pública do destinatário acompanha o jogo.
    public static class ResearchReportJwe
    {
        public const string KeyId = "feb-professor-56b9b08ae089";
        const string Modulus = "5u3lE7ejvfl3zvyWHa1Pf7S291jqMFWtCXg4Z2Q3jJnSA0-Yfd4IkgMcI5CS2z4bIguEnH7R7jpQkUPSkDXtOEM7hPthpOF4DoyniRxu_-ZIQ10e_BuRyuPSfEehO3RG220fK1KuRUQlpciNsM44uU8FLFluaC-AJh-E7qKCGlS8NtnXdHfEgTwg-lOgvPkqUct616-cmiC-hz3EUoiwtOUAs0xuAkLZYuSG4HA4lvUbDCP1IWq-02al5d9068XK5Jkz2UXQv8PkZd582MlNiRWe9fBwBeKRxLH0A4F8jl962NpvOxM-vsUj0IuWUJCiLibIkIxeWaKGpWats91lKWNmNz5hTUepGdC4EW6hElJkzC55EM7xhXQWzn-Vg82Rk_eEvanuAQgqMteZpjESBibVQ1OqeH1jgp07ykmTCJ3QJLA_FspZpY_L3aj-5t9RglyFIB9E6e2OW5QEwabFiUftfFqkBMMqp64gHgGdviOwRhL8hGjHfQEWxRpE43yP";
        const string Exponent = "AQAB";
        const int MaximumLength = 800000;
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        static string Header => "{\"alg\":\"RSA-OAEP\",\"enc\":\"A256CBC-HS512\",\"cty\":\"JWT\",\"kid\":\"" + KeyId + "\"}";

        public static string Encrypt(string signedJwt)
        {
            ResearchReportJwt.Verify(signedJwt);
            var cek = new byte[64];
            var iv = new byte[16];
            var macKey = new byte[32];
            var encryptionKey = new byte[32];
            var plaintext = Utf8.GetBytes(signedJwt);
            try
            {
                using (var random = RandomNumberGenerator.Create())
                {
                    random.GetBytes(cek);
                    random.GetBytes(iv);
                }
                Buffer.BlockCopy(cek, 0, macKey, 0, 32);
                Buffer.BlockCopy(cek, 32, encryptionKey, 0, 32);
                byte[] encryptedKey, ciphertext;
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters { Modulus = Decode(Modulus), Exponent = Decode(Exponent) });
                    encryptedKey = rsa.Encrypt(cek, RSAEncryptionPadding.OaepSHA1);
                }
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.IV = iv;
                    using (var encryptor = aes.CreateEncryptor())
                        ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
                }
                string protectedHeader = Encode(Utf8.GetBytes(Header));
                var aad = Encoding.ASCII.GetBytes(protectedHeader);
                var authenticated = new byte[aad.Length + iv.Length + ciphertext.Length + 8];
                Buffer.BlockCopy(aad, 0, authenticated, 0, aad.Length);
                Buffer.BlockCopy(iv, 0, authenticated, aad.Length, iv.Length);
                Buffer.BlockCopy(ciphertext, 0, authenticated, aad.Length + iv.Length, ciphertext.Length);
                ulong aadBits = (ulong)aad.Length * 8;
                for (int i = 0; i < 8; i++) authenticated[authenticated.Length - 1 - i] = (byte)(aadBits >> (8 * i));
                var tag = new byte[32];
                using (var hmac = new HMACSHA512(macKey))
                    Buffer.BlockCopy(hmac.ComputeHash(authenticated), 0, tag, 0, tag.Length);
                string token = string.Join(".", protectedHeader, Encode(encryptedKey), Encode(iv), Encode(ciphertext), Encode(tag));
                ValidateEnvelope(token);
                return token;
            }
            finally
            {
                Array.Clear(cek, 0, cek.Length);
                Array.Clear(macKey, 0, macKey.Length);
                Array.Clear(encryptionKey, 0, encryptionKey.Length);
                Array.Clear(plaintext, 0, plaintext.Length);
            }
        }

        // Confere a estrutura para exportação. Somente o destinatário pode abrir
        // e autenticar o conteúdo cifrado; sua chave privada não está no cliente.
        public static void ValidateEnvelope(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length > MaximumLength)
                throw new InvalidDataException("Relatório cifrado ausente ou maior que o limite.");
            var parts = token.Split('.');
            if (parts.Length != 5 || parts[0] != Encode(Utf8.GetBytes(Header)))
                throw new InvalidDataException("Formato ou destinatário do relatório cifrado não reconhecido.");
            var ciphertext = Decode(parts[3]);
            if (Decode(parts[1]).Length != 384 || Decode(parts[2]).Length != 16 ||
                ciphertext.Length == 0 || ciphertext.Length % 16 != 0 || Decode(parts[4]).Length != 32)
                throw new InvalidDataException("Relatório cifrado incompleto.");
        }

        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static byte[] Decode(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new InvalidDataException("Parte do JWE ausente.");
            foreach (char c in value)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_'))
                    throw new InvalidDataException("Codificação JWE inválida.");
            string base64 = value.Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
            if (Encode(bytes) != value) throw new InvalidDataException("Codificação JWE não canônica.");
            return bytes;
        }
    }
}
