using System;
using System.IO;
using UnityEngine;

namespace FEB.Persistence
{
    public static class ResearchSaveStore
    {
        public const int SlotCount = 4;
        public static ResearchSave Current { get; private set; }
        public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "ResearchSaves");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => Current = null;

        static string SlotPath(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(DirectoryPath, $"save-{slot + 1}.json");
        }

        public static ResearchSave Read(int slot)
        {
            var path = SlotPath(slot);
            return ReadFile(path, slot) ?? ReadFile(path + ".bak", slot);
        }

        static ResearchSave ReadFile(string path, int slot)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<ResearchSave>(json);
                if (data == null || data.version != 1 || data.slot != slot || string.IsNullOrEmpty(data.id)) return null;
                // JsonUtility recria referências serializadas inline como objetos vazios, mesmo quando eram null.
                // Só normaliza o relatório padrão sem assinatura; preserva relatórios preenchidos ou assinados.
                if (data.finalReport != null && string.IsNullOrEmpty(data.finalReportJwt) &&
                    string.IsNullOrEmpty(data.finalReportEncryptedJwt) &&
                    JsonUtility.ToJson(data.finalReport) == JsonUtility.ToJson(new ResearchFinalReport()))
                    data.finalReport = null;
                data.evidence ??= new System.Collections.Generic.List<string>();
                data.discoveredEvidence ??= new System.Collections.Generic.List<string>();
                data.fieldEvidence ??= new System.Collections.Generic.List<string>();
                data.fieldNotes ??= new System.Collections.Generic.List<EvidenceNote>();
                foreach (var id in data.evidence)
                    if (!data.discoveredEvidence.Contains(id)) data.discoveredEvidence.Add(id);
                data.events ??= new System.Collections.Generic.List<ResearchEvent>();
                data.dossiers ??= new System.Collections.Generic.List<DossierProgress>();
                foreach (var dossier in data.dossiers) dossier.Normalize();
                if (!json.Contains("\"fieldTimingStarted\"") && (data.campArrivalCompleted || data.fieldEvidence.Count > 0 ||
                    data.events.Exists(e => e.action == "scene_requested" && e.subject == "Acampamento")))
                {
                    var camp = data.dossiers.Find(d => d.caseId == "acampamentos_da_feb");
                    if (camp != null) camp.fieldTimeIncomplete = true;
                }
                return data;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Não foi possível ler {Path.GetFileName(path)}: {e.Message}");
                return null;
            }
        }

        public static ResearchSave Latest()
        {
            ResearchSave latest = null;
            for (var i = 0; i < SlotCount; i++)
            {
                var candidate = Read(i);
                if (candidate != null && (latest == null || string.CompareOrdinal(candidate.updatedUtc, latest.updatedUtc) > 0)) latest = candidate;
            }
            return latest;
        }

        public static void Begin(int slot)
        {
            var next = new ResearchSave { slot = slot };
            next.Record("session_started");
            Write(next);
            Current = next;
        }

        public static void Resume(ResearchSave save)
        {
            Current = save ?? throw new ArgumentNullException(nameof(save));
            Current.Record("session_resumed");
        }

        // Opening the scene directly in the Editor never overwrites a player's save.
        public static ResearchSave EnsureSession() => Current ??= new ResearchSave();

        public static bool CompleteCampArrival()
        {
            var save = EnsureSession();
            if (save.campArrivalCompleted) return true;
            save.campArrivalCompleted = true;
            save.Record("camp_arrival_completed");
            if (Save(out _)) return true;
            save.campArrivalCompleted = false;
            save.events.RemoveAt(save.events.Count - 1);
            return false;
        }

        public static bool RegisterFieldEvidence(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            var save = EnsureSession();
            if (save.fieldEvidence.Contains(id)) return true;
            save.fieldEvidence.Add(id);
            save.Record("field_evidence_registered", id);
            if (Save(out _)) return true;
            save.fieldEvidence.Remove(id);
            save.events.RemoveAt(save.events.Count - 1);
            return false;
        }

        public static bool Save(out string error)
        {
            error = null;
            if (Current == null || Current.slot < 0) return true;
            try { Write(Current); return true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = "Não foi possível salvar o progresso. " + e.Message;
                Debug.LogError(error);
                return false;
            }
        }

        static void Write(ResearchSave save)
        {
            var path = SlotPath(save.slot);
            Directory.CreateDirectory(DirectoryPath);
            save.updatedUtc = DateTime.UtcNow.ToString("O");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
