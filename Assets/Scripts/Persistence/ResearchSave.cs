using System;
using System.Collections.Generic;
using UnityEngine;

namespace FEB.Persistence
{
    [Serializable]
    public sealed class ResearchSave
    {
        public int version = 1;
        public int slot = -1;
        public string id = Guid.NewGuid().ToString("N");
        public string updatedUtc;
        public string scene = "ArquivoHistorico";
        public bool briefingRead;
        public bool chronologySolved;
        public bool reconstructionViewed;
        public bool reconstructionSkipped;
        public bool reportCompleted;
        public bool formationUnlocked;
        public bool campArrivalCompleted;
        public bool hasPlayerPosition;
        public int archiveRoomRevision;
        public Vector3 playerPosition;
        public float playerYaw;
        public double archiveSeconds;
        public int chronologyAttempts;
        public int reportAttempts;
        public List<string> evidence = new List<string>();
        public List<string> discoveredEvidence = new List<string>();
        public List<string> fieldEvidence = new List<string>();
        public List<EvidenceNote> fieldNotes = new List<EvidenceNote>();
        public List<ResearchEvent> events = new List<ResearchEvent>();
        public string activeCaseId;
        public string archiveScene = "ArquivoHistorico";
        public List<DossierProgress> dossiers = new List<DossierProgress>();
        public ResearchFinalReport finalReport;
        public string finalReportJwt;

        public DossierProgress ActiveDossier => dossiers?.Find(d => d.caseId == activeCaseId);
        public string StageLabel => ActiveDossier?.StageLabel ?? (reportCompleted ? "Dossiê concluído" : reconstructionViewed ? "Relatório pendente" : chronologySolved ? "Reconstrução disponível" : "Investigação no arquivo");

        public DossierProgress SelectCase(string caseId)
        {
            dossiers ??= new List<DossierProgress>();
            var dossier = dossiers.Find(d => d.caseId == caseId);
            if (dossier == null)
            {
                dossier = new DossierProgress { caseId = caseId };
                if (dossiers.Count == 0)
                {
                    dossier.briefingRead = briefingRead;
                    dossier.chronologySolved = chronologySolved;
                    dossier.reconstructionViewed = reconstructionViewed || reportCompleted;
                    dossier.reportCompleted = reportCompleted;
                    dossier.archiveSeconds = archiveSeconds;
                    dossier.chronologyAttempts = chronologyAttempts;
                    dossier.reportAttempts = reportAttempts;
                    dossier.evidence = new List<string>(evidence ?? new List<string>());
                    dossier.discoveredEvidence = new List<string>(discoveredEvidence ?? new List<string>());
                    dossier.readEvidence = new List<string>(dossier.evidence);
                }
                dossiers.Add(dossier);
            }
            dossier.Normalize();
            activeCaseId = caseId;
            return dossier;
        }

        public void Record(string action, string subject = "", string detail = "")
        {
            events.Add(new ResearchEvent { utc = DateTime.UtcNow.ToString("O"), action = action, subject = subject, detail = detail });
        }
    }

    [Serializable]
    public sealed class DossierProgress
    {
        public string caseId;
        public string fieldStudyTitle;
        public bool briefingRead, chronologyOrdered, chronologySolved, reconstructionViewed, reportCompleted, followUpCompleted;
        public bool boardSourcesLinked;
        public List<string> answeredQuestions = new List<string>();
        public double archiveSeconds;
        public double fieldSeconds, reconstructionSeconds;
        public bool fieldTimingStarted, fieldTimeIncomplete;
        public int chronologyAttempts, reportAttempts;
        public List<string> evidence = new List<string>();
        public List<string> discoveredEvidence = new List<string>();
        public List<string> readEvidence = new List<string>();
        public List<EvidenceNote> notes = new List<EvidenceNote>();
        public string StageLabel => reportCompleted ? "Dossiê concluído" :
            !string.IsNullOrWhiteSpace(fieldStudyTitle) ? (reconstructionViewed ? "Relatório do acampamento pendente" : fieldStudyTitle) :
            reconstructionViewed ? "Relatório pendente" : chronologySolved ? "Reconstrução disponível" :
            chronologyOrdered ? "Perguntas sobre as fontes" : "Investigação no arquivo";

        public void Normalize()
        {
            if (chronologySolved) chronologyOrdered = true;
            answeredQuestions ??= new List<string>();
            evidence ??= new List<string>();
            discoveredEvidence ??= new List<string>();
            readEvidence ??= new List<string>();
            notes ??= new List<EvidenceNote>();
            foreach (var id in evidence)
            {
                if (!discoveredEvidence.Contains(id)) discoveredEvidence.Add(id);
                if (!readEvidence.Contains(id)) readEvidence.Add(id);
            }
        }
    }

    [Serializable]
    public sealed class EvidenceNote
    {
        public string evidenceId;
        public string text;
    }

    [Serializable]
    public sealed class ResearchEvent
    {
        public string utc;
        public string action;
        public string subject;
        public string detail;
    }
}
