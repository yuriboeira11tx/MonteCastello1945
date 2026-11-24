using FEB.Persistence;
using FEB.Cinematics;
using UnityEngine;
using UnityEngine.InputSystem;
using FEB.UI;

namespace FEB.Archive
{
    public sealed class ArchiveCutsceneReturn : MonoBehaviour
    {
        [SerializeField] NavalArchiveSession session;
        [SerializeField] string archiveScene = "ArquivoHistorico";
        float exitHeld;
        bool returning;
        float nextAutosave;
        bool retryReturn;
        float nextReturnAttempt;

        void Start() => session.ReturnRequested += ReturnToArchive;

        void Update()
        {
            if (returning) return;
            if (retryReturn)
            {
                if (Time.unscaledTime >= nextReturnAttempt) ReturnToArchive();
                return;
            }
            if (session.IsEnding) return;
            var progress = ResearchSaveStore.EnsureSession();
            if (Application.isFocused && Time.timeScale > 0 && session.VideoReady && progress.ActiveDossier != null &&
                !progress.ActiveDossier.reportCompleted && progress.finalReport == null)
                progress.ActiveDossier.reconstructionSeconds += Time.unscaledDeltaTime;
            if (Time.unscaledTime >= nextAutosave)
            {
                nextAutosave = Time.unscaledTime + 30;
                ResearchSaveStore.Save(out _);
            }
            exitHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed ? exitHeld + Time.unscaledDeltaTime : 0;
            if (exitHeld >= 2) ReturnToArchive();
        }

        public void ReturnToArchive()
        {
            if (returning) return;
            var progress = ResearchSaveStore.EnsureSession();
            string destination = string.IsNullOrWhiteSpace(progress.archiveScene) ? archiveScene : progress.archiveScene;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            { Debug.LogError("Cena de arquivo indisponível: " + destination, this); return; }
            bool wasViewed = progress.ActiveDossier?.reconstructionViewed ?? false;
            int eventCount = progress.events.Count;
            if (progress.ActiveDossier != null) progress.ActiveDossier.reconstructionViewed |= session.Completed;
            string previousScene = progress.scene;
            progress.scene = destination;
            progress.Record(session.Completed ? "reconstruction_completed" : session.Defeated ? "naval_exercise_failed" : "reconstruction_left", progress.activeCaseId);
            if (!ResearchSaveStore.Save(out var error))
            {
                progress.scene = previousScene;
                if (progress.ActiveDossier != null) progress.ActiveDossier.reconstructionViewed = wasViewed;
                progress.events.RemoveRange(eventCount, progress.events.Count - eventCount);
                if (!retryReturn) Debug.LogWarning("Retorno ao acervo pendente: " + error + " Uma nova tentativa será feita em 5 segundos.", this);
                retryReturn = true;
                nextReturnAttempt = Time.unscaledTime + 5;
                return;
            }
            returning = true;
            SceneLoadingScreen.Load(destination);
        }

        void OnDestroy() { if (session != null) session.ReturnRequested -= ReturnToArchive; }
    }
}
