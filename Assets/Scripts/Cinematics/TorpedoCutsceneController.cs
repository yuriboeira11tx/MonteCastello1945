using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
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

        void Awake()
        {
            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            savedFogMode = RenderSettings.fogMode;
            savedClearFlags = outputCamera.clearFlags;
            savedBackground = outputCamera.backgroundColor;
            initialized = true;
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
        }

        [ContextMenu("Reiniciar cutscene (180 segundos)")]
        public void RestartCutscene()
        {
            if (!Application.isPlaying || director == null) return;
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
            director.Play();
        }

        void OnDisable()
        {
            if (!initialized) return;
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.fogMode = savedFogMode;
            if (outputCamera != null) { outputCamera.clearFlags = savedClearFlags; outputCamera.backgroundColor = savedBackground; }
            if (impactLight != null) impactLight.enabled = false;
        }
    }
}
