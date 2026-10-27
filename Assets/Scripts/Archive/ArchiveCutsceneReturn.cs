using FEB.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace FEB.Archive
{
    public sealed class ArchiveCutsceneReturn : MonoBehaviour
    {
        [SerializeField] PlayableDirector director;
        [SerializeField] string archiveScene = "ArquivoHistorico";
        float skipHeld;
        bool returning;

        void Update()
        {
            var progress = ResearchSaveStore.Current;
            if (returning || progress == null || progress.scene != gameObject.scene.name || !progress.chronologySolved) return;
            skipHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed ? skipHeld + Time.unscaledDeltaTime : 0;
            bool complete = director.time >= director.duration - .02;
            if (!complete && skipHeld < 2) return;
            progress.reconstructionViewed |= complete;
            progress.reconstructionSkipped |= !complete;
            progress.scene = archiveScene;
            progress.Record(complete ? "reconstruction_completed" : "reconstruction_skipped", "itapage");
            if (!ResearchSaveStore.Save(out _)) { progress.scene = gameObject.scene.name; return; }
            returning = true;
            director.Stop();
            SceneManager.LoadSceneAsync(archiveScene);
        }
    }
}
