using FEB.Cinematics;
using UnityEngine;

namespace FEB.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class CampBackgroundMusic : MonoBehaviour
    {
        [SerializeField] AudioSource radio;
        [SerializeField] AudioListener ouvinte;
        [SerializeField] CampArrival chegada;
        [SerializeField, Min(0), Tooltip("Até esta distância, apenas o rádio toca a música.")] float distanciaSoRadio = 6f;
        [SerializeField, Min(0), Tooltip("A partir desta distância a trilha recupera seu volume normal.")] float distanciaTrilhaPlena = 24f;
        [SerializeField, Range(0, 1), Tooltip("Volume relativo da trilha durante a conversa no jipe.")] float volumeNaChegada = .25f;
        [SerializeField, Min(1)] float alcanceDoRadioNaChegada = 400f;
        [SerializeField, Range(0, 1)] float volumeDoRadioNaChegada = .55f;

        AudioSource trilha;
        float volumeNormal;
        bool radioNaChegada;
        float alcanceOriginal, volumeOriginal;
        AudioRolloffMode atenuacaoOriginal;
        AnimationCurve curvaOriginal;

        void Awake()
        {
            trilha = GetComponent<AudioSource>();
            volumeNormal = trilha.volume;
        }

        void Start()
        {
            AtualizarRadio();
            trilha.volume = VolumeAlvo();
        }

        void Update()
        {
            if (AudioListener.pause) return;
            AtualizarRadio();
            trilha.volume = Mathf.MoveTowards(trilha.volume, VolumeAlvo(), volumeNormal * Time.unscaledDeltaTime);
        }

        void AtualizarRadio()
        {
            bool naChegada = chegada && chegada.EmAndamento;
            if (!radio || naChegada == radioNaChegada) return;
            if (!naChegada)
            {
                RestaurarRadio();
                return;
            }
            alcanceOriginal = radio.maxDistance;
            volumeOriginal = radio.volume;
            atenuacaoOriginal = radio.rolloffMode;
            curvaOriginal = radio.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
            radio.maxDistance = alcanceDoRadioNaChegada;
            radio.volume = volumeDoRadioNaChegada;
            // O rádio se destaca junto da mesa e chega baixo até o jipe e o limite da cena.
            radio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(.018f, .78f, -9f, -9f),
                new Keyframe(.10f, .24f, -2f, -2f), new Keyframe(.30f, .075f, -.30f, -.30f),
                new Keyframe(1f, 0f)));
            radio.rolloffMode = AudioRolloffMode.Custom;
            radioNaChegada = true;
        }

        void RestaurarRadio()
        {
            if (radioNaChegada && radio)
            {
                radio.maxDistance = alcanceOriginal;
                radio.volume = volumeOriginal;
                if (curvaOriginal != null) radio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curvaOriginal);
                radio.rolloffMode = atenuacaoOriginal;
            }
            radioNaChegada = false;
        }

        float VolumeAlvo()
        {
            float ganho = chegada && chegada.EmAndamento ? volumeNaChegada : 1f;
            if (ouvinte && radio && radio.isPlaying && !radio.mute && radio.volume > 0f)
            {
                float distancia = Vector3.Distance(ouvinte.transform.position, radio.transform.position);
                float mistura = Mathf.InverseLerp(distanciaSoRadio, distanciaTrilhaPlena, distancia);
                ganho *= Mathf.SmoothStep(0f, 1f, mistura);
            }
            return volumeNormal * ganho;
        }

        void OnValidate() => distanciaTrilhaPlena = Mathf.Max(distanciaTrilhaPlena, distanciaSoRadio + .1f);

        void OnDisable()
        {
            RestaurarRadio();
            if (trilha) trilha.volume = volumeNormal;
        }
    }
}
