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
        float elapsed;
        bool active;

        void Awake()
        {
            foreach (var root in contactEffects)
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var ps in fireAndSmoke) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            impactLight.enabled = false;
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
            StartCoroutine(Ignite());
        }

        IEnumerator Ignite()
        {
            yield return new WaitForSeconds(.3f);
            foreach (var ps in fireAndSmoke) ps.Play();
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
