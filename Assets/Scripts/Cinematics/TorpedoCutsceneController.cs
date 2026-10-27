using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class TorpedoCutsceneController : MonoBehaviour
    {
        public PlayableDirector director;
        public Transform ship;
        public Transform torpedo;
        public Camera outputCamera;
        public Volume underwaterVolume;
        public Volume fadeVolume;
        public Light impactLight;
        public AudioLowPassFilter[] audioFilters;
        public FireSite[] fireSites;
        public float seaLevel = 3.82f;
        [Range(0, 1)] public float underwaterBlend;
        [Range(0, 1)] public float fade;
        [Range(0, 1)] public float impactFlash;
        public Vector3 initialShipPosition;
        public Quaternion initialShipRotation = Quaternion.identity;
        public Vector3 initialTorpedoPosition;
        public Quaternion initialTorpedoRotation = Quaternion.identity;
        public Renderer[] torpedoRenderers;
        public ParticleSystem[] controlledParticles;
        public AudioSource[] audioSources;
        public bool autoPlay = true;

        [Header("Video de arquivo")]
        public VideoPlayer archiveVideo;
        public AudioSource archiveAudio;
        public CanvasGroup archiveGroup;
        [Range(0, 1)] public float archiveVolume = .7f;
        public Volume surfaceVolume;
        public Vector3 shipFocusOffset = new Vector3(0, 5, 0);

        [Serializable]
        public struct FireSite
        {
            public Transform anchor;
            public ParticleSystem flame;
            public ParticleSystem smoke;
            public float flameRate;
            public float smokeRate;
        }

        Color savedFogColor;
        float savedFogDensity;
        bool savedFog;
        FogMode savedFogMode;
        CameraClearFlags savedClearFlags;
        Color savedBackground;
        bool initialized;
        bool videoReady;
        bool videoFailed;
        bool videoSeeking;
        double lastVideoTime;
        float lastVideoSeek;
        Coroutine startingSequence;
        DepthOfField surfaceFocus;

        void Awake()
        {
            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            savedFogMode = RenderSettings.fogMode;
            savedClearFlags = outputCamera.clearFlags;
            savedBackground = outputCamera.backgroundColor;
            initialized = true;
            if (archiveVideo != null)
            {
                archiveVideo.errorReceived += OnVideoError;
                archiveVideo.seekCompleted += OnVideoSeekCompleted;
            }
            if (archiveGroup != null) archiveGroup.alpha = 0;
            if (surfaceVolume != null) surfaceVolume.profile.TryGet(out surfaceFocus);
        }

        void Start()
        {
            if (autoPlay) RestartCutscene();
        }

        void LateUpdate()
        {
            if (!initialized || director == null) return;
            float water = Mathf.Clamp01(underwaterBlend);
            underwaterVolume.weight = water;
            fadeVolume.weight = Mathf.Clamp01(fade);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(savedFogColor, new Color(.08f, .30f, .34f), water);
            float underwaterDensity = Mathf.Lerp(.018f, .006f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(78, 94, (float)director.time)));
            RenderSettings.fogDensity = Mathf.Lerp(savedFogDensity, underwaterDensity, water);
            outputCamera.clearFlags = water > .75f ? CameraClearFlags.SolidColor : savedClearFlags;
            outputCamera.backgroundColor = Color.Lerp(savedBackground, new Color(.08f, .30f, .34f), water);
            impactLight.intensity = impactFlash * 35f;
            impactLight.enabled = impactFlash > .001f;
            foreach (var filter in audioFilters)
                if (filter != null) filter.cutoffFrequency = Mathf.Lerp(22000f, 850f, water);
            foreach (var site in fireSites)
            {
                if (site.anchor == null) continue;
                float aboveWater = Mathf.InverseLerp(seaLevel - .3f, seaLevel + 2f, site.anchor.position.y);
                if (site.flame != null) { var em = site.flame.emission; em.rateOverTimeMultiplier = site.flameRate * aboveWater; }
                if (site.smoke != null) { var em = site.smoke.emission; em.rateOverTimeMultiplier = site.smokeRate * aboveWater; }
            }
            bool showTorpedo = director.time < 100.12;
            foreach (var renderer in torpedoRenderers) if (renderer != null) renderer.enabled = showTorpedo;
            if (surfaceFocus != null && ship != null)
            {
                Vector3 subject = ship.TransformPoint(shipFocusOffset);
                subject.y = Mathf.Max(seaLevel + 1f, subject.y);
                surfaceFocus.focusDistance.value = Mathf.Max(1f,
                    Vector3.Dot(subject - outputCamera.transform.position, outputCamera.transform.forward));
            }
            UpdateArchiveVideo();
        }

        [ContextMenu("Reiniciar cutscene (180 segundos)")]
        public void RestartCutscene()
        {
            if (!Application.isPlaying || director == null) return;
            if (startingSequence != null) StopCoroutine(startingSequence);
            if (archiveVideo != null) archiveVideo.Stop();
            videoReady = videoFailed = videoSeeking = false;
            lastVideoSeek = -10;
            if (archiveGroup != null) archiveGroup.alpha = 0;
            director.Stop();
            foreach (var ps in controlledParticles)
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var source in audioSources) if (source != null) source.Stop();
            ship.SetPositionAndRotation(initialShipPosition, initialShipRotation);
            torpedo.SetPositionAndRotation(initialTorpedoPosition, initialTorpedoRotation);
            foreach (var renderer in torpedoRenderers) if (renderer != null) renderer.enabled = true;
            underwaterBlend = fade = impactFlash = 0;
            director.time = 0;
            director.Evaluate();
            startingSequence = StartCoroutine(PrepareAndPlay());
        }

        IEnumerator PrepareAndPlay()
        {
            if (archiveVideo != null && archiveVideo.clip != null)
            {
                yield return null;
                archiveVideo.timeReference = VideoTimeReference.Freerun;
                archiveVideo.timeUpdateMode = VideoTimeUpdateMode.GameTime;
                archiveVideo.Prepare();
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!archiveVideo.isPrepared && !videoFailed && Time.realtimeSinceStartup < deadline)
                    yield return null;

                if (archiveVideo.isPrepared && !videoFailed)
                {
                    if (archiveAudio != null) archiveAudio.volume = 0;
                    archiveVideo.Play();
                    deadline = Time.realtimeSinceStartup + 5f;
                    while (archiveVideo.frame < 0 && !videoFailed && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    videoReady = archiveVideo.frame >= 0 && !videoFailed;
                }
                if (!videoReady && !videoFailed)
                    OnVideoError(archiveVideo, "Tempo limite ao preparar o video.");
            }
            lastVideoTime = 0;
            director.Play();
            startingSequence = null;
        }

        void UpdateArchiveVideo()
        {
            if (archiveVideo == null || !videoReady || videoFailed) return;
            double time = director.time;
            double lastFrameTime = Math.Max(0, archiveVideo.length - 1.0 / Math.Max(1, archiveVideo.frameRate));
            double target = Math.Min(Math.Max(0, time), lastFrameTime);
            // Both clocks use game time. Only explicit Timeline jumps require a decoder seek.
            double step = time - lastVideoTime;
            bool jumped = step < -.05 || Math.Abs(step) > Math.Max(.3, Time.unscaledDeltaTime * 2);
            if (videoSeeking && Time.realtimeSinceStartup - lastVideoSeek > 3) videoSeeking = false;
            if (jumped && !videoSeeking && archiveVideo.canSetTime &&
                Time.realtimeSinceStartup - lastVideoSeek > .25f)
            {
                archiveVideo.Pause();
                videoSeeking = true;
                lastVideoSeek = Time.realtimeSinceStartup;
                archiveVideo.time = target;
            }
            bool playing = director.state == PlayState.Playing && Time.timeScale > 0 && time < lastFrameTime;
            if (playing && !videoSeeking && !archiveVideo.isPlaying) archiveVideo.Play();
            else if (!playing && archiveVideo.isPlaying) archiveVideo.Pause();
            lastVideoTime = time;

            float opacity = Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)time / 1.5f)) * (1 - Mathf.Clamp01(fade));
            if (archiveGroup != null) archiveGroup.alpha = opacity;
            if (archiveAudio != null)
            {
                float end = Mathf.Clamp01((float)(archiveVideo.length - time) / 1.25f);
                float start = Mathf.Clamp01((float)time / .5f);
                archiveAudio.volume = archiveVolume * Mathf.Min(start, end) * (1 - Mathf.Clamp01(fade));
            }
        }

        void OnVideoSeekCompleted(VideoPlayer player)
        {
            videoSeeking = false;
        }

        void OnVideoError(VideoPlayer player, string message)
        {
            videoFailed = true;
            videoReady = false;
            if (archiveGroup != null) archiveGroup.alpha = 0;
            if (archiveAudio != null) archiveAudio.Stop();
            player.Stop();
            Debug.LogWarning("Video de arquivo indisponivel. A cutscene 3D continua. " + message, this);
        }

        void OnDisable()
        {
            if (startingSequence != null) { StopCoroutine(startingSequence); startingSequence = null; }
            if (archiveVideo != null) archiveVideo.Stop();
            if (archiveGroup != null) archiveGroup.alpha = 0;
            videoReady = false;
            if (!initialized) return;
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.fogMode = savedFogMode;
            if (outputCamera != null) { outputCamera.clearFlags = savedClearFlags; outputCamera.backgroundColor = savedBackground; }
            if (impactLight != null) impactLight.enabled = false;
        }

        void OnDestroy()
        {
            if (archiveVideo == null) return;
            archiveVideo.errorReceived -= OnVideoError;
            archiveVideo.seekCompleted -= OnVideoSeekCompleted;
        }
    }
}
