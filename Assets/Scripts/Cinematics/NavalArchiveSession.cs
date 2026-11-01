using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using FEB.UI;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class NavalArchiveSession : MonoBehaviour
    {
        [SerializeField] VideoPlayer archiveVideo;
        [SerializeField] AudioSource archiveAudio;
        [SerializeField] AudioSource oceanAudio;
        [SerializeField] CutsceneView view;
        [SerializeField] ShipEvasionController navigation;
        [SerializeField] ShipImpactEffects impactEffects;
        [SerializeField, Range(0, 1)] float archiveVolume = .8f;
        [SerializeField, Min(3)] float failureDisplaySeconds = 6;
        [SerializeField, Min(2)] float impactPresentationSeconds = 4.5f;
        [SerializeField, Min(2)] float finalFireSeconds = 5;
        bool ready, ending;
        public bool Completed { get; private set; }
        public bool Defeated { get; private set; }
        public bool VideoReady => ready;
        public bool IsEnding => ending;
        public VideoPlayer Video => archiveVideo;
        public CutsceneView View => view;
        public event Action ReturnRequested;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            archiveVideo.errorReceived += VideoError;
            archiveVideo.loopPointReached += VideoEnded;
            navigation.HitReceived += VesselHit;
            StartCoroutine(PrepareVideo());
        }

        IEnumerator PrepareVideo()
        {
            ready = ending = Completed = Defeated = false;
            navigation.SetRunning(false);
            view.ResetSession();
            archiveVideo.Stop();
            archiveVideo.isLooping = false;
            archiveVideo.playbackSpeed = 1;
            archiveVideo.timeUpdateMode = VideoTimeUpdateMode.GameTime;
            archiveVideo.waitForFirstFrame = true;
            if (archiveVideo.canSetSkipOnDrop) archiveVideo.skipOnDrop = false;
            if (archiveVideo.clip == null) { VideoError(archiveVideo, "Nenhum filme foi atribuído."); yield break; }
            archiveVideo.Prepare();
            float deadline = Time.realtimeSinceStartup + 20;
            while (!archiveVideo.isPrepared && !ending && Time.realtimeSinceStartup < deadline) yield return null;
            if (ending) yield break;
            if (!archiveVideo.isPrepared) { VideoError(archiveVideo, "Tempo limite de preparação."); yield break; }
            archiveAudio.volume = archiveVolume;
            archiveVideo.Play();
            deadline = Time.realtimeSinceStartup + 8;
            while (archiveVideo.frame < 0 && !ending && Time.realtimeSinceStartup < deadline) yield return null;
            if (ending) yield break;
            if (archiveVideo.frame < 0) { VideoError(archiveVideo, "O primeiro quadro não ficou disponível."); yield break; }
            ready = true;
            view.ShowVideo(true);
            view.ShowRadar(true);
            navigation.BeginSession();
            navigation.SetRunning(true);
            if (oceanAudio != null) { oceanAudio.loop = true; oceanAudio.Play(); }
        }

        void VesselHit()
        {
            if (ending) return;
            ending = Defeated = true;
            navigation.SetRunning(false);
            archiveVideo.Pause();
            view.ShowRadar(false);
            impactEffects.Play(navigation.LastImpactPoint);
            StartCoroutine(PresentDefeat());
        }

        IEnumerator PresentDefeat()
        {
            yield return new WaitForSecondsRealtime(impactPresentationSeconds);
            view.ShowFailure();
            if (oceanAudio != null) oceanAudio.Stop();
            yield return ReturnAfter(failureDisplaySeconds);
        }

        void VideoEnded(VideoPlayer player)
        {
            if (ending || !ready) return;
            if (navigation.Damaged) { VesselHit(); return; }
            ending = true;
            navigation.SetRunning(false);
            view.ShowRadar(false);
            StartCoroutine(PresentCompletion());
        }

        IEnumerator PresentCompletion()
        {
            view.ShowFinale(true);
            yield return navigation.PlayFinalAttack();
            if (!navigation.Damaged)
            {
                view.ShowUnavailable();
                yield return ReturnAfter(failureDisplaySeconds);
                yield break;
            }
            impactEffects.Play(navigation.LastImpactPoint);
            yield return new WaitForSeconds(finalFireSeconds);
            float oceanVolume = oceanAudio != null ? oceanAudio.volume : 0;
            for (float elapsed = 0; elapsed < 1.2f; elapsed += Time.unscaledDeltaTime)
            {
                float fade = Mathf.Clamp01(elapsed / 1.2f);
                view.SetFinalFade(fade);
                if (oceanAudio != null) oceanAudio.volume = Mathf.Lerp(oceanVolume, 0, fade);
                yield return null;
            }
            view.SetFinalFade(1);
            Completed = true;
            ReturnRequested?.Invoke();
        }

        void VideoError(VideoPlayer player, string message)
        {
            if (ending) return;
            ending = true;
            navigation.SetRunning(false);
            player.Stop();
            view.ShowUnavailable();
            Debug.LogWarning("Arquivo histórico indisponível: " + message, this);
            StartCoroutine(ReturnAfter(failureDisplaySeconds));
        }

        IEnumerator ReturnAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            ReturnRequested?.Invoke();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if (archiveVideo != null) archiveVideo.Stop();
            if (oceanAudio != null) oceanAudio.Stop();
            if (navigation != null) navigation.SetRunning(false);
        }

        void OnDestroy()
        {
            if (archiveVideo != null) { archiveVideo.errorReceived -= VideoError; archiveVideo.loopPointReached -= VideoEnded; }
            if (navigation != null) navigation.HitReceived -= VesselHit;
        }
    }
}
