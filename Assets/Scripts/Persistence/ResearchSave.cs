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
        public bool hasPlayerPosition;
        public Vector3 playerPosition;
        public float playerYaw;
        public double archiveSeconds;
        public int chronologyAttempts;
        public int reportAttempts;
        public List<string> evidence = new List<string>();
        public List<ResearchEvent> events = new List<ResearchEvent>();

        public string StageLabel => reportCompleted ? "Dossiê concluído" : reconstructionViewed || reconstructionSkipped ? "Relatório pendente" : chronologySolved ? "Reconstrução disponível" : "Investigação no arquivo";

        public void Record(string action, string subject = "", string detail = "")
        {
            events.Add(new ResearchEvent { utc = DateTime.UtcNow.ToString("O"), action = action, subject = subject, detail = detail });
        }
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
