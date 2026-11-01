using FEB.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FEB.Archive
{
    public enum ArchiveActivityStage { Reconstruction, FollowUp }

    public sealed class ArchiveActivityExit : MonoBehaviour
    {
        [SerializeField] ArchiveActivityStage stage = ArchiveActivityStage.FollowUp;
        [SerializeField] bool completeOnPlayerTrigger;
        bool returning;

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
            string destination = string.IsNullOrWhiteSpace(progress.archiveScene) ? "ArquivoHistorico" : progress.archiveScene;
            if (!Application.CanStreamedLevelBeLoaded(destination)) { Debug.LogError("Cena de arquivo indisponível: " + destination, this); return; }
            if (complete && progress.ActiveDossier != null)
            {
                if (stage == ArchiveActivityStage.Reconstruction) progress.ActiveDossier.reconstructionViewed = true;
                else progress.ActiveDossier.followUpCompleted = true;
            }
            string previousScene = progress.scene;
            progress.scene = destination;
            progress.Record(complete ? "activity_completed" : "activity_left", progress.activeCaseId);
            if (!ResearchSaveStore.Save(out _)) { progress.scene = previousScene; return; }
            returning = true;
            SceneManager.LoadSceneAsync(destination);
        }
    }
}
