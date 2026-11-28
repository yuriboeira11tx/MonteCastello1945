using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace FEB.Persistence
{
    // O jogo envia o JWT assinado; apenas a função no servidor pode gravar na tabela.
    public static class ResearchReportSubmission
    {
        [Serializable] sealed class Endpoint { public string url, publishableKey; }
        [Serializable] public sealed class Receipt
        {
            public bool ok;
            public string reportId, code, receivedUtc, message;
            public string tokenHash, endpoint;
        }

        static Endpoint Settings()
        {
            var asset = Resources.Load<TextAsset>("ResearchReportEndpoint");
            var settings = asset == null ? null : JsonUtility.FromJson<Endpoint>(asset.text);
            if (settings == null || !Uri.TryCreate(settings.url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(settings.publishableKey))
                throw new InvalidOperationException("O envio online ainda não está configurado.");
            return settings;
        }

        static string ReceiptPath(ResearchFinalReport report)
        {
            if (report == null || !Guid.TryParseExact(report.reportId, "N", out _))
                throw new InvalidDataException("Identificação do relatório inválida.");
            return Path.Combine(ResearchReportStore.DirectoryPath, report.reportId + "-recibo.json");
        }

        public static bool HasReceipt(ResearchFinalReport report, string jwt)
        {
            try
            {
                string path = ReceiptPath(report);
                if (!File.Exists(path) || string.IsNullOrEmpty(jwt)) return false;
                var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(path));
                return receipt != null && receipt.ok && receipt.reportId == report.reportId &&
                    receipt.code == report.code && receipt.tokenHash == ResearchReportStore.Hash(jwt) &&
                    receipt.endpoint == Settings().url;
            }
            catch (Exception) { return false; }
        }

        public static async Task<Receipt> SendAsync(ResearchFinalReport report, string jwt, CancellationToken cancellation)
        {
            ResearchReportJwt.VerifyMatches(jwt, report);
            var settings = Settings();
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ResearchReportEnvelope { jwt = jwt }));
            using (var request = new UnityWebRequest(settings.url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 30;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("apikey", settings.publishableKey);
                cancellation.ThrowIfCancellationRequested();
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellation.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }
                cancellation.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (request.responseCode == 400 || request.responseCode == 401 || request.responseCode == 409 || request.responseCode == 413)
                        throw new InvalidOperationException("O servidor não aceitou este relatório. Exporte o JSON e envie ao professor.");
                    throw new InvalidOperationException("Não foi possível enviar agora. O relatório continua salvo. Tente novamente quando houver conexão.");
                }
                Receipt receipt;
                try { receipt = JsonUtility.FromJson<Receipt>(request.downloadHandler.text); }
                catch (Exception) { throw new InvalidDataException("O servidor não confirmou o recebimento. Tente enviar novamente."); }
                if (receipt == null || !receipt.ok || receipt.reportId != report.reportId || receipt.code != report.code ||
                    !DateTimeOffset.TryParse(receipt.receivedUtc, out _))
                    throw new InvalidDataException("O servidor não confirmou o recebimento. Tente enviar novamente.");
                receipt.tokenHash = ResearchReportStore.Hash(jwt);
                receipt.endpoint = settings.url;
                try
                {
                    Directory.CreateDirectory(ResearchReportStore.DirectoryPath);
                    File.WriteAllText(ReceiptPath(report), JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
                }
                catch (IOException) { receipt.message = "Recebido pelo professor. Não foi possível guardar o comprovante neste computador."; }
                catch (UnauthorizedAccessException) { receipt.message = "Recebido pelo professor. Não foi possível guardar o comprovante neste computador."; }
                return receipt;
            }
        }
    }
}
