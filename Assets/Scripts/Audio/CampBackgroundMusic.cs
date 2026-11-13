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

        AudioSource trilha;
        float volumeNormal;

        void Awake()
        {
            trilha = GetComponent<AudioSource>();
            volumeNormal = trilha.volume;
        }

        void Start() => trilha.volume = VolumeAlvo();

        void Update()
        {
            if (AudioListener.pause) return;
            trilha.volume = Mathf.MoveTowards(trilha.volume, VolumeAlvo(), volumeNormal * Time.unscaledDeltaTime);
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
            if (trilha) trilha.volume = volumeNormal;
        }
    }
}
