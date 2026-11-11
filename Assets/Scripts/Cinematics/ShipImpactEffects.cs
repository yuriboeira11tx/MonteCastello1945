using System.Collections;
using UnityEngine;

namespace FEB.Cinematics
{
    public sealed class ShipImpactEffects : MonoBehaviour
    {
        [SerializeField] Transform[] contactEffects;
        [SerializeField] ParticleSystem[] fireAndSmoke;
        [SerializeField] Light impactLight;
        [SerializeField] float seaLevel = 3.82f;
        [SerializeField] AudioSource explosionAudio;
        [SerializeField] AudioSource fireAudio;
        float explosionVolume, fireVolume;
        float elapsed;
        bool active;

        void Awake()
        {
            foreach (var root in contactEffects)
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var ps in fireAndSmoke) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            impactLight.enabled = false;
            if (explosionAudio) explosionVolume = explosionAudio.volume;
            if (fireAudio) fireVolume = fireAudio.volume;
        }

        public void Play(Vector3 contact)
        {
            if (active) return;
            active = true;
            elapsed = 0;
            foreach (var root in contactEffects)
            {
                root.position = new Vector3(contact.x, seaLevel, contact.z);
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            }
            impactLight.transform.position = contact + Vector3.up * 7;
            impactLight.enabled = true;
            impactLight.range = 65;
            if (explosionAudio) explosionAudio.Play();
            StartCoroutine(Ignite());
        }

        IEnumerator Ignite()
        {
            yield return new WaitForSeconds(.3f);
            foreach (var ps in fireAndSmoke) ps.Play();
            if (fireAudio) fireAudio.Play();
        }

        public void FadeAudio(float duration) => StartCoroutine(FadeImpactAudio(duration));

        IEnumerator FadeImpactAudio(float duration)
        {
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float gain = 1f - elapsed / duration;
                if (explosionAudio) explosionAudio.volume = explosionVolume * gain;
                if (fireAudio) fireAudio.volume = fireVolume * gain;
                yield return null;
            }
            StopAudio();
        }

        void StopAudio()
        {
            if (explosionAudio) { explosionAudio.Stop(); explosionAudio.volume = explosionVolume; }
            if (fireAudio) { fireAudio.Stop(); fireAudio.volume = fireVolume; }
        }

        void OnDisable()
        {
            StopAllCoroutines();
            StopAudio();
        }

        void Update()
        {
            if (!active) return;
            elapsed += Time.deltaTime;
            float flash = Mathf.Clamp01(1 - elapsed / .7f);
            impactLight.intensity = flash * 12000 + 750 + Mathf.Sin(elapsed * 17) * 120;
            impactLight.color = Color.Lerp(new Color(1, .32f, .08f), new Color(1, .84f, .55f), flash);
        }
    }
}
