using System;
using System.Collections.Generic;
using FEB.Archive;
using FEB.UI;
using UnityEngine;

namespace FEB.Audio
{
    [RequireComponent(typeof(SubtitleView))]
    [DisallowMultipleComponent]
    public sealed class SceneDialogue : MonoBehaviour
    {
        public enum Trigger
        {
            [InspectorName("Ao chegar à cena")] Arrival,
            [InspectorName("Perto do soldado")] Soldier,
            [InspectorName("Perto do local")] Landmark,
            [InspectorName("Ao registrar evidência de campo")] Evidence
        }

        [Serializable]
        public sealed class Line
        {
            [Tooltip("Nome de trabalho para localizar a fala no roteiro de gravação.")]
            public string identificador;
            public string locutor;
            public Trigger quando;
            [Tooltip("Soldado ou ponto de interesse que o pesquisador deve alcançar.")]
            public GameObject origem;
            public HistoricalEvidence evidencia;
            [TextArea(2, 6)] public string texto;
            public AudioClip audio;
            [Min(0), Tooltip("Segundos de leitura sem gravação. Zero calcula pelo comprimento do texto.")]
            public float duracaoSemAudio;
        }

        [SerializeField] ArchivePlayerInteraction pesquisador;
        [SerializeField] AudioSource voz;
        [SerializeField, Min(0.5f)] float alcance = 5f;
        [SerializeField, Min(0)] float intervalo = 7f;
        [SerializeField, Min(0)] float esperaInicial = 1.5f;
        [SerializeField] Line[] falas = Array.Empty<Line>();

        readonly HashSet<Line> delivered = new HashSet<Line>();
        readonly HashSet<HistoricalEvidence> registrations = new HashSet<HistoricalEvidence>();
        SubtitleView subtitles;
        Line current;
        float nextCheck, silentEnd;
        bool timedReading;

        public AudioSource VoiceSource => voz;

        void Awake() => subtitles = GetComponent<SubtitleView>();

        void OnEnable()
        {
            if (pesquisador == null || voz == null)
            {
                Debug.LogError("Atribua o pesquisador e a fonte de voz em Scene Dialogue.", this);
                enabled = false;
                return;
            }
            pesquisador.FieldEvidenceRegistered += OnEvidenceRegistered;
            nextCheck = Time.time + esperaInicial;
        }

        void OnEvidenceRegistered(HistoricalEvidence evidence) => registrations.Add(evidence);

        void Update()
        {
            if (pesquisador.IsReading)
            {
                if (current != null) Finish(false);
                return;
            }
            if (AudioListener.pause || Time.timeScale <= 0) return;
            if (current != null)
            {
                if (current.quando == Trigger.Soldier)
                {
                    if (current.origem == null || !current.origem.activeInHierarchy)
                    {
                        Finish(false);
                        return;
                    }
                    voz.transform.position = current.origem.transform.position + Vector3.up * 1.5f;
                    if ((pesquisador.transform.position - voz.transform.position).sqrMagnitude > voz.maxDistance * voz.maxDistance)
                    {
                        Finish(false);
                        return;
                    }
                }
                if (!timedReading && current.audio.loadState == AudioDataLoadState.Failed) timedReading = true;
                if (timedReading ? Time.time >= silentEnd :
                    current.audio.loadState == AudioDataLoadState.Loaded && !voz.isPlaying) Finish(true);
                return;
            }
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + .25f;
            var line = ChooseLine();
            if (line != null) Speak(line);
        }

        Line ChooseLine()
        {
            Line nearby = null;
            float bestDistance = alcance * alcance;
            bool showSubtitles = SubtitlePreferences.Enabled;
            foreach (var line in falas)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.texto) || delivered.Contains(line)) continue;
                if (line.audio == null && !showSubtitles) continue;
                if (line.quando == Trigger.Evidence)
                {
                    if (line.evidencia != null && registrations.Contains(line.evidencia)) return line;
                    continue;
                }
                if (line.quando == Trigger.Arrival) return line;
                if (line.origem == null || !line.origem.activeInHierarchy) continue;
                float distance = (pesquisador.transform.position - line.origem.transform.position).sqrMagnitude;
                if (line.quando == Trigger.Landmark && distance <= alcance * alcance) return line;
                if (distance > bestDistance) continue;
                bestDistance = distance;
                nearby = line;
            }
            return nearby;
        }

        void Speak(Line line)
        {
            current = line;
            silentEnd = Time.time + (line.duracaoSemAudio > 0 ? line.duracaoSemAudio :
                Mathf.Clamp(1f + line.texto.Length / 15f, 3f, 18f));
            timedReading = line.audio == null;
            if (!timedReading)
            {
                bool spatial = line.quando == Trigger.Soldier;
                voz.spatialBlend = spatial ? 1f : 0f;
                if (spatial) voz.transform.position = line.origem.transform.position + Vector3.up * 1.5f;
                voz.clip = line.audio;
                voz.Play();
            }
            if (SubtitlePreferences.Enabled)
                subtitles.Show(line.texto, SubtitlePreferences.Scale);
        }

        void Finish(bool completed)
        {
            if (completed) delivered.Add(current);
            current = null;
            voz.Stop();
            subtitles.Hide();
            nextCheck = Time.time + intervalo;
        }

        void OnDisable()
        {
            if (pesquisador != null) pesquisador.FieldEvidenceRegistered -= OnEvidenceRegistered;
            if (current != null) Finish(false);
        }
    }
}
