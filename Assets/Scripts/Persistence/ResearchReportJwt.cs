using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FEB.Persistence
{
    // Assinatura local para detectar edição do relatório. A chave distribuída no jogo
    // pode ser extraída; isto não substitui um emissor confiável fora do cliente.
    public static class ResearchReportJwt
    {
        public const string Issuer = "MonteCastello1945";
        public const string Audience = "professor-relatorios";
        public const string KeyId = "feb-report-1113d5bbac4e";
        const string Modulus = "o6F-HuHR9-W6830bCYDN7t4R8UnFp2jvo37H9vxzj6Kq40dIncfoHXmd7JtzdCpe5qhNnruaTLM8YniJWF8A6lfiHYkgMMiGiccgUs-ZYpPWcCUpMPIpURW5vjKmGBvE_Cp28SbPYZr-7nipzHeUblSoZ-cix55S4sQ9KfF4bPhd6PEzhhnM87JqqpOnZNuhGda7T-hEqXBnSNzJhg3yf7WivWxOfx4Y5k9bf5LsQD99aOG2-9suGfXssnQXv9v8WQ5JrceBPSIXI-pLPuFeI-8swfzKjGobtmdoNeDN8Flmg_s10jt22ID9S9fjfoCAZzFp0QfNEzQOeNJTczhXCQ";
        const string Exponent = "AQAB";
        const int MaximumLength = 524288;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static string Header => "{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"" + KeyId + "\"}";

        [Serializable]
        sealed class Claims
        {
            public int v = 1;
            public string iss, aud, jti;
            public long iat;
            public ResearchFinalReport report;
        }

        [Serializable]
        sealed class SigningKey
        {
            public string kid, n, e, d, p, q, dp, dq, qi;
        }

        public static string Issue(ResearchFinalReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var asset = Resources.Load<TextAsset>("ResearchReportSigningKey");
            if (asset == null) throw new InvalidOperationException("Chave de assinatura do relatório indisponível.");
            var key = JsonUtility.FromJson<SigningKey>(asset.text);
            if (key == null || key.kid != KeyId || key.n != Modulus || key.e != Exponent)
                throw new InvalidDataException("A chave do jogo não corresponde ao verificador do professor.");
            var claims = new Claims
            {
                iss = Issuer, aud = Audience, jti = report.reportId,
                iat = CompletedSeconds(report), report = report
            };
            string input = Encode(Utf8.GetBytes(Header)) + "." + Encode(Utf8.GetBytes(JsonUtility.ToJson(claims, false)));
            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = Decode(key.n), Exponent = Decode(key.e), D = Decode(key.d),
                    P = Decode(key.p), Q = Decode(key.q), DP = Decode(key.dp), DQ = Decode(key.dq), InverseQ = Decode(key.qi)
                });
                string token = input + "." + Encode(rsa.SignData(Utf8.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
                VerifyMatches(token, report);
                return token;
            }
        }

        public static ResearchFinalReport Verify(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length > MaximumLength)
                throw new InvalidDataException("JWT ausente ou maior que o limite aceito.");
            var parts = token.Split('.');
            // Perfil fechado: não escolhemos algoritmo nem buscamos chaves a partir do token.
            if (parts.Length != 3 || parts[0] != Encode(Utf8.GetBytes(Header)))
                throw new InvalidDataException("Cabeçalho JWT não reconhecido. Este jogo utiliza RS256.");
            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(new RSAParameters { Modulus = Decode(Modulus), Exponent = Decode(Exponent) });
                if (!rsa.VerifyData(Utf8.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    throw new InvalidDataException("Assinatura inválida: o relatório foi alterado ou está incompleto.");
            }
            var claims = JsonUtility.FromJson<Claims>(Utf8.GetString(Decode(parts[1])));
            if (claims == null || claims.v != 1 || claims.iss != Issuer || claims.aud != Audience ||
                claims.report == null || claims.jti != claims.report.reportId || claims.iat != CompletedSeconds(claims.report) ||
                !ResearchReportStore.ValidateCode(claims.report.code))
                throw new InvalidDataException("O JWT não contém um relatório válido deste jogo.");
            return claims.report;
        }

        public static void VerifyMatches(string token, ResearchFinalReport report)
        {
            var signed = Verify(token);
            if (report == null || JsonUtility.ToJson(signed, false) != JsonUtility.ToJson(report, false))
                throw new InvalidDataException("As estatísticas salvas não correspondem ao JWT emitido na conclusão.");
        }

        static long CompletedSeconds(ResearchFinalReport report)
        {
            if (!DateTimeOffset.TryParse(report.completedUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date))
                throw new InvalidDataException("Data de conclusão inválida.");
            return date.ToUnixTimeSeconds();
        }

        static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static byte[] Decode(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new InvalidDataException("Parte do JWT ausente.");
            foreach (char c in value)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_'))
                    throw new InvalidDataException("Codificação JWT inválida.");
            string base64 = value.Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
            if (Encode(bytes) != value) throw new InvalidDataException("Codificação JWT não canônica.");
            return bytes;
        }
    }
}
