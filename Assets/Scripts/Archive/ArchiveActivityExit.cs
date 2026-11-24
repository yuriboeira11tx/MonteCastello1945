using System.Linq;
using FEB.Persistence;
using UnityEngine;
using FEB.UI;

namespace FEB.Archive
{
    public enum ArchiveActivityStage { Reconstruction, FollowUp }

    public sealed class ArchiveActivityExit : MonoBehaviour
    {
        [SerializeField] ArchiveActivityStage stage = ArchiveActivityStage.FollowUp;
        [SerializeField] bool completeOnPlayerTrigger;
        [SerializeField] ArchiveCaseDefinition fieldCase;
        [SerializeField] ArchivePlayerInteraction interaction;
        bool returning;
        float nextAutosave;

        public bool IsFieldVisit => fieldCase != null && fieldCase.fieldStudy;
        public int RequiredCount => fieldCase?.requiredFieldEvidence?.Length ?? 0;
        public int RegisteredCount => fieldCase?.requiredFieldEvidence?.Count(e => e != null &&
            ResearchSaveStore.EnsureSession().fieldEvidence.Contains(e.id)) ?? 0;
        public bool CanComplete => IsFieldVisit && fieldCase.HasFieldReport &&
            fieldCase.FieldEvidenceComplete(ResearchSaveStore.EnsureSession());
        public string FieldObjective => CanComplete ? "As fontes de campo estão registradas. Conclua a visita para redigir o relatório no acervo."
            : $"Registre as evidências do campo: {RegisteredCount}/{RequiredCount}. Examine os documentos e objetos do acampamento.";

        void Start()
        {
            if (!IsFieldVisit) return;
            if (interaction == null) interaction = GetComponent<ArchivePlayerInteraction>();
            var progress = ResearchSaveStore.EnsureSession();
            var dossier = progress.SelectCase(fieldCase.Id);
            dossier.fieldStudyTitle = fieldCase.title;
            dossier.fieldTimingStarted = true;
            progress.scene = fieldCase.reconstructionScene;
            progress.archiveScene = fieldCase.archiveScene;
            nextAutosave = Time.unscaledTime + 30;
        }

        void Update()
        {
            if (!IsFieldVisit || returning) return;
            var progress = ResearchSaveStore.EnsureSession();
            var dossier = progress.ActiveDossier;
            if (dossier == null || dossier.reportCompleted || progress.finalReport != null) return;
            if (Application.isFocused && Time.timeScale > 0 && !(interaction?.Notebook?.IsPauseMenu ?? false))
                dossier.fieldSeconds += Time.unscaledDeltaTime;
            if (Time.unscaledTime < nextAutosave) return;
            ResearchSaveStore.Save(out _);
            nextAutosave = Time.unscaledTime + 30;
        }

        public void ShowReturnOptions()
        {
            if (interaction == null || !IsFieldVisit || returning) return;
            var notebook = interaction.Notebook;
            string pending = string.Join("\n", fieldCase.requiredFieldEvidence.Where(e => e != null &&
                !ResearchSaveStore.EnsureSession().fieldEvidence.Contains(e.id)).Select(e => "• " + e.title));
            notebook.ShowActivity("Visita de campo", "Relatório do acampamento", FieldObjective +
                (string.IsNullOrEmpty(pending) ? "" : "\n\nFontes por registrar:\n" + pending));
            if (CanComplete) notebook.AddChoice("Concluir visita e voltar ao acervo", CompleteAndReturn);
            else notebook.AddChoice("Voltar ao acervo sem concluir", ReturnWithoutCompleting);
            notebook.AddChoice("Continuar no acampamento", notebook.CloseBook);
        }

        public void CompleteAndReturn() => Return(true);
        public void ReturnWithoutCompleting() => Return(false);

        void OnTriggerEnter(Collider other)
        {
            if (completeOnPlayerTrigger && other.CompareTag("Player")) CompleteAndReturn();
        }

        void Return(bool complete)
        {
            if (returning) return;
            var progress = ResearchSaveStore.EnsureSession();
            if (complete && IsFieldVisit && !CanComplete) { ShowReturnOptions(); return; }
            string destination = string.IsNullOrWhiteSpace(progress.archiveScene) ? "ArquivoHistorico" : progress.archiveScene;
            if (!Application.CanStreamedLevelBeLoaded(destination)) { Debug.LogError("Cena de arquivo indisponível: " + destination, this); return; }
            var dossier = progress.ActiveDossier;
            bool previousViewed = dossier?.reconstructionViewed ?? false;
            bool previousFollowUp = dossier?.followUpCompleted ?? false;
            int eventCount = progress.events.Count;
            if (complete && dossier != null)
            {
                if (IsFieldVisit || stage == ArchiveActivityStage.Reconstruction) dossier.reconstructionViewed = true;
                else dossier.followUpCompleted = true;
            }
            string previousScene = progress.scene;
            progress.scene = destination;
            progress.Record(complete ? "activity_completed" : "activity_left", progress.activeCaseId);
            if (!ResearchSaveStore.Save(out var error))
            {
                progress.scene = previousScene;
                if (dossier != null) { dossier.reconstructionViewed = previousViewed; dossier.followUpCompleted = previousFollowUp; }
                progress.events.RemoveRange(eventCount, progress.events.Count - eventCount);
                if (interaction != null)
                {
                    interaction.Notebook.ShowActivity("Visita de campo", "Não foi possível salvar", error);
                    interaction.Notebook.AddChoice("Tentar novamente", ShowReturnOptions);
                }
                return;
            }
            returning = true;
            interaction?.Notebook?.StoreForTransition();
            SceneLoadingScreen.Load(destination);
        }

        void OnApplicationPause(bool paused) { if (paused && IsFieldVisit && !returning) ResearchSaveStore.Save(out _); }
        void OnApplicationQuit() { if (IsFieldVisit && !returning) ResearchSaveStore.Save(out _); }
    }
}
