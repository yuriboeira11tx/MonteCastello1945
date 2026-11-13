using System;
using System.Collections;
using FEB.UI;
using UnityEngine;

namespace FEB.Audio
{
    [Serializable]
    public sealed class CampArrivalDialogue
    {
        public enum Cue { Greeting, Introduction, CredentialsRequest, CredentialsHandover, Orders }

        [Serializable]
        sealed class Caption
        {
            [Range(0, 1), Tooltip("Posição na gravação. 0,5 corresponde à metade do áudio.")]
            public float inicio;
            [TextArea(1, 3)] public string texto;
        }

        [Serializable]
        sealed class Turn
        {
            [Tooltip("A gravação completa deste turno de fala.")]
            public AudioClip gravacao;
            [Min(1)] public float segundosSemGravacao;
            [Tooltip("Trechos em ordem de leitura. Ajuste o início de cada trecho ao gravar a voz.")]
            public Caption[] legendas;

            public Turn(params string[] frases)
            {
                legendas = new Caption[frases.Length];
                for (int i = 0; i < frases.Length; i++)
                {
                    legendas[i] = new Caption { inicio = segundosSemGravacao, texto = frases[i] };
                    segundosSemGravacao += Mathf.Max(1.5f, frases[i].Length / 17f);
                }
                foreach (var legenda in legendas) legenda.inicio /= segundosSemGravacao;
            }
        }

        [SerializeField, InspectorName("Capitão — Chegada do jipe")] Turn chegada = new(
            "Ô, Costa! Peraí!", "Quem é esse aí?");
        [SerializeField, InspectorName("Motorista — Apresentação")] Turn apresentacao = new(
            "Meu capitão! Trouxe o pesquisador que o comando anunciou.",
            "Veio ouvir o pessoal e escrever sobre a vida aqui no acampamento.",
            "É pra publicar lá no Brasil.");
        [SerializeField, InspectorName("Capitão — Credenciais")] Turn suspeita = new(
            "Publicar, é? E como é que eu sei que o senhor não é espião da Rádio Auriverde?",
            "Aquela conversa dos alemães, em português, com uma voz de compadre…",
            "“Ô, pracinha… que saudade de casa, hein?”",
            "Em duas frases o sujeito já quer que eu largue o fuzil.",
            "E ainda me chama de amigo! Soldado, a credencial dele.",
            "Deixe eu conferir com a ordem que recebi.");
        [SerializeField, InspectorName("Motorista — Entrega da credencial")] Turn credencial = new(
            "Aqui, meu capitão.",
            "E se ele escrever tudo que ouviu no jipe, peço licença pra riscar umas duas palavras.");
        [SerializeField, InspectorName("Capitão — Missão na enfermaria")] Turn missao = new(
            "Nome certo… assinatura certa. Muito bem. Seja bem-vindo.",
            "Tem uma caderneta aí? Então comece pela enfermaria.",
            "Pergunte às enfermeiras como foi a noite.",
            "Quando a notícia chega ao Brasil, quase nunca vem com o nome de quem cuidou dos feridos.");

        SceneDialogue ambient;
        SubtitleView subtitles;
        bool resumeAmbient;
        AudioSource currentVoice;
        public int CaptionIndex { get; private set; } = -1;

        public void SetBlackout(float opacity)
        {
            if (subtitles != null) subtitles.SetBlackout(opacity);
        }

        public void Begin(SceneDialogue sceneDialogue)
        {
            ambient = sceneDialogue;
            resumeAmbient = ambient.enabled;
            ambient.enabled = false;
            subtitles = ambient.GetComponent<SubtitleView>();
        }

        public IEnumerator Play(Cue cue, AudioSource voice)
        {
            var turn = cue switch
            {
                Cue.Greeting => chegada,
                Cue.Introduction => apresentacao,
                Cue.CredentialsRequest => suspeita,
                Cue.CredentialsHandover => credencial,
                Cue.Orders => missao,
                _ => throw new ArgumentOutOfRangeException(nameof(cue))
            };
            var clip = turn.gravacao;
            if (clip != null)
            {
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                float timeout = Time.realtimeSinceStartup + 5f;
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < timeout)
                    yield return null;
            }

            bool hasVoice = clip != null && clip.loadState == AudioDataLoadState.Loaded && clip.samples > 0;
            if (hasVoice)
            {
                currentVoice = voice;
                voice.clip = clip;
                voice.Play();
                hasVoice = voice.isPlaying;
            }
            if (!hasVoice && clip != null)
                Debug.LogWarning($"A gravação '{clip.name}' não iniciou. A chegada usará as legendas deste trecho.", ambient);

            int displayed = -1;
            float elapsed = 0;
            float duration = Mathf.Max(1f, turn.segundosSemGravacao);
            while (ambient != null)
            {
                if (!AudioListener.pause)
                {
                    if (hasVoice ? !voice.isPlaying : elapsed >= duration) break;
                    float progress = hasVoice ? (float)voice.timeSamples / clip.samples : elapsed / duration;
                    int last = (turn.legendas?.Length ?? 0) - 1;
                    int current = displayed;
                    while (current < last && progress >= turn.legendas[current + 1].inicio) current++;
                    if (current != displayed)
                    {
                        CaptionIndex = displayed = current;
                        if (!hasVoice || SubtitlePreferences.Enabled)
                            subtitles.Show(turn.legendas[current].texto, SubtitlePreferences.Scale);
                    }
                    elapsed += Time.deltaTime;
                }
                yield return null;
            }
            if (currentVoice) currentVoice.Stop();
            currentVoice = null;
            if (subtitles != null) subtitles.Hide();
            CaptionIndex = -1;
        }

        public void End()
        {
            SetBlackout(0);
            if (subtitles != null) subtitles.Hide();
            if (currentVoice) currentVoice.Stop();
            currentVoice = null;
            if (ambient != null) ambient.enabled = resumeAmbient;
            ambient = null;
            subtitles = null;
        }
    }
}
