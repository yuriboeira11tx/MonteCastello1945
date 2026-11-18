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
            [Tooltip("Para falas de soldado, use a fonte de voz 3D da cabeça do personagem.")]
            public AudioSource vozDoSoldado;
            public HistoricalEvidence evidencia;
            [TextArea(2, 6)] public string texto;
            public AudioClip audio;
            [Min(0), Tooltip("Segundos de leitura sem gravação. Zero calcula pelo comprimento do texto.")]
            public float duracaoSemAudio;
        }

        [SerializeField] ArchivePlayerInteraction pesquisador;
        [SerializeField] AudioSource voz;
        [SerializeField, Min(0.5f)] float alcance = 5f;
        [SerializeField, Min(0), Tooltip("Intervalo entre falas da mesma fonte de voz. Soldados diferentes podem falar juntos.")]
        float intervalo = 7f;
        [SerializeField, Min(0)] float esperaInicial = 1.5f;
        [SerializeField] Line[] falas = Array.Empty<Line>();

        readonly HashSet<Line> delivered = new HashSet<Line>();
        readonly HashSet<HistoricalEvidence> registrations = new HashSet<HistoricalEvidence>();
        sealed class Playback
        {
            public Line line;
            public AudioSource source;
            public float silentEnd;
            public bool timedReading;
        }

        readonly List<Playback> playing = new List<Playback>();
        readonly Dictionary<AudioSource, float> nextVoiceTime = new Dictionary<AudioSource, float>();
        SubtitleView subtitles;
        Playback caption;
        float nextCheck, captionScale;

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
                StopAll();
                return;
            }
            if (AudioListener.pause || Time.timeScale <= 0) return;
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                var playback = playing[i];
                var line = playback.line;
                if (line.quando == Trigger.Soldier)
                {
                    if (line.origem == null || !line.origem.activeInHierarchy)
                    {
                        Finish(i, false);
                        continue;
                    }
                    Vector3 position = playback.source ? playback.source.transform.position : line.origem.transform.position;
                    float distance = playback.source ? playback.source.maxDistance : voz.maxDistance;
                    if ((pesquisador.transform.position - position).sqrMagnitude > distance * distance)
                    {
                        Finish(i, false);
                        continue;
                    }
                }
                if (!playback.timedReading && !playback.source)
                {
                    Finish(i, false);
                    continue;
                }
                if (!playback.timedReading && line.audio.loadState == AudioDataLoadState.Failed)
                    playback.timedReading = true;
                if (playback.timedReading ? Time.time >= playback.silentEnd :
                    line.audio.loadState == AudioDataLoadState.Loaded && !playback.source.isPlaying)
                    Finish(i, true);
            }

            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + .25f;
                var line = ChooseLine();
                if (line != null) Speak(line);
            }
            UpdateCaption();
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
                var source = line.quando == Trigger.Soldier ? line.vozDoSoldado : voz;
                if (!VoiceAvailable(line, source)) continue;
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
            var source = line.quando == Trigger.Soldier ? line.vozDoSoldado : voz;
            if (!VoiceAvailable(line, source)) return;
            var playback = new Playback
            {
                line = line,
                source = source,
                silentEnd = Time.time + (line.duracaoSemAudio > 0 ? line.duracaoSemAudio :
                    Mathf.Clamp(1f + line.texto.Length / 15f, 3f, 18f)),
                timedReading = line.audio == null || !source
            };
            if (line.audio && !source)
                Debug.LogWarning($"Atribua a fonte de voz do soldado em '{line.identificador}'.", this);
            if (!playback.timedReading)
            {
                source.clip = line.audio;
                source.Play();
            }
            playing.Add(playback);
        }

        bool VoiceAvailable(Line line, AudioSource source)
        {
            foreach (var playback in playing)
                if (playback.line == line || (source && playback.source == source)) return false;
            return !source || (!source.isPlaying &&
                (!nextVoiceTime.TryGetValue(source, out float next) || Time.time >= next));
        }

        float CaptionDistance(Playback playback) => playback.line.quando != Trigger.Soldier ? 0f :
            Vector3.Distance(pesquisador.transform.position,
                playback.source ? playback.source.transform.position : playback.line.origem.transform.position);

        void UpdateCaption()
        {
            Playback nearest = caption != null && playing.Contains(caption) ? caption : null;
            float distance = nearest != null ? CaptionDistance(nearest) : float.PositiveInfinity;
            foreach (var playback in playing)
            {
                float candidate = CaptionDistance(playback);
                // Uma pequena margem evita trocar a legenda a cada passo entre dois soldados.
                if (nearest == null || candidate + 1f < distance)
                {
                    nearest = playback;
                    distance = candidate;
                }
            }
            if (!SubtitlePreferences.Enabled) nearest = null;
            if (nearest == caption && captionScale == SubtitlePreferences.Scale) return;
            caption = nearest;
            captionScale = SubtitlePreferences.Scale;
            if (caption != null) subtitles.Show(caption.line.texto, captionScale);
            else subtitles.Hide();
        }

        void Finish(int index, bool completed)
        {
            var playback = playing[index];
            if (completed) delivered.Add(playback.line);
            if (playback.source)
            {
                if (playback.source.clip == playback.line.audio) playback.source.Stop();
                nextVoiceTime[playback.source] = Time.time + intervalo;
            }
            playing.RemoveAt(index);
        }

        void StopAll()
        {
            for (int i = playing.Count - 1; i >= 0; i--) Finish(i, false);
            if (caption != null && subtitles) subtitles.Hide();
            caption = null;
        }

        void OnDisable()
        {
            if (pesquisador != null) pesquisador.FieldEvidenceRegistered -= OnEvidenceRegistered;
            StopAll();
        }
    }
}
