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

        void Start() => session.ReturnRequested += ReturnToArchive;

        void Update()
        {
            if (returning || session.IsEnding) return;
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
            if (progress.ActiveDossier != null) progress.ActiveDossier.reconstructionViewed |= session.Completed;
            string previousScene = progress.scene;
            progress.scene = destination;
            progress.Record(session.Completed ? "reconstruction_completed" : session.Defeated ? "naval_exercise_failed" : "reconstruction_left", progress.activeCaseId);
            if (!ResearchSaveStore.Save(out _)) { progress.scene = previousScene; return; }
            returning = true;
            SceneLoadingScreen.Load(destination);
        }

        void OnDestroy() { if (session != null) session.ReturnRequested -= ReturnToArchive; }
    }
}
