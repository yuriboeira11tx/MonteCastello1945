using FEB.Cinematics;
using UnityEngine;
using UnityEngine.Audio;

namespace FEB.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class CampBackgroundMusic : MonoBehaviour
    {
        [SerializeField] AudioSource radio;
        [SerializeField] AudioListener ouvinte;
        [SerializeField] CampArrival chegada;
        [SerializeField] AudioClip musicaDaChegada;
        [SerializeField, Range(-12, 6), Tooltip("Compensa a diferença de loudness entre a música e as gravações das falas.")]
        float ajusteDaFaixaDb = -3f;
        [SerializeField, Min(0), Tooltip("Até esta distância, apenas o rádio toca a música.")] float distanciaSoRadio = 6f;
        [SerializeField, Min(0), Tooltip("A partir desta distância a trilha recupera seu volume normal.")] float distanciaTrilhaPlena = 24f;

        AudioSource trilha;
        AudioResource programacaoNormal;
        float volumeNormal;
        bool loopNormal, iniciar, tocandoChegada;
        float ganhoDaFaixa;

        void Awake()
        {
            trilha = GetComponent<AudioSource>();
            volumeNormal = trilha.volume;
            programacaoNormal = trilha.resource;
            loopNormal = trilha.loop;
            ganhoDaFaixa = Mathf.Pow(10f, ajusteDaFaixaDb / 20f);
        }

        void OnEnable() => iniciar = true;

        void Update()
        {
            if (AudioListener.pause) return;
            bool naChegada = chegada && chegada.EmAndamento && musicaDaChegada;
            if (iniciar || naChegada != tocandoChegada)
            {
                if (!iniciar && !naChegada)
                {
                    trilha.volume = Mathf.MoveTowards(trilha.volume, 0f, Time.unscaledDeltaTime);
                    if (trilha.volume > 0f) return;
                }
                trilha.Stop();
                trilha.resource = naChegada ? musicaDaChegada : programacaoNormal;
                trilha.loop = naChegada || loopNormal;
                trilha.volume = iniciar ? VolumeAlvo(naChegada) : 0f;
                if (trilha.resource) trilha.Play();
                tocandoChegada = naChegada;
                iniciar = false;
            }
            float alvo = VolumeAlvo(naChegada);
            float velocidade = Mathf.Max(volumeNormal, Mathf.Max(trilha.volume, alvo));
            trilha.volume = Mathf.MoveTowards(trilha.volume, alvo, velocidade * Time.unscaledDeltaTime);
        }

        float VolumeAlvo(bool naChegada)
        {
            if (naChegada) return Mathf.Clamp01(chegada.VolumeDaVoz * ganhoDaFaixa);
            if (ouvinte && radio && radio.isPlaying && !radio.mute && radio.volume > 0f)
            {
                float distancia = Vector3.Distance(ouvinte.transform.position, radio.transform.position);
                float mistura = Mathf.InverseLerp(distanciaSoRadio, distanciaTrilhaPlena, distancia);
                return volumeNormal * Mathf.SmoothStep(0f, 1f, mistura);
            }
            return volumeNormal;
        }

        void OnValidate() => distanciaTrilhaPlena = Mathf.Max(distanciaTrilhaPlena, distanciaSoRadio + .1f);

        void OnDisable()
        {
            if (!trilha) return;
            trilha.Stop();
            trilha.resource = programacaoNormal;
            trilha.loop = loopNormal;
            trilha.volume = volumeNormal;
        }
    }
}
