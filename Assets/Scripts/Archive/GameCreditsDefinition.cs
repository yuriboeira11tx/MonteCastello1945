using System;
using UnityEngine;

namespace FEB.Archive
{
    [Serializable]
    public sealed class GameCreditSection
    {
        public string heading;
        [TextArea(2, 12)] public string text;
    }

    [CreateAssetMenu(menuName = "FEB/Créditos finais")]
    public sealed class GameCreditsDefinition : ScriptableObject
    {
        [TextArea(2, 5)] public string academicTitle = "TÍTULO DO TRABALHO ACADÊMICO";
        public GameCreditSection[] sections =
        {
            new GameCreditSection
            {
                heading = "Direção, desenvolvimento e realização",
                text = "Yuri Juliano do Nascimento Boeira"
            },
            new GameCreditSection
            {
                heading = "Professor Orientador",
                text = "Sidnei Renato Silveira"
            },
            new GameCreditSection
            {
                heading = "Dublagem",
                text = "Brandon Lima — Motorista do acampamento\nFábio Mamede — Sargento do acampamento\nFernando Oliveira — Soldado do acampamento\nLeonardo Vidal — Soldado do acampamento"
            }
        };

        public AudioClip music;
        [Range(0, 1)] public float volume = 0.28f;
        [Tooltip("Unidades do painel por segundo. Um valor menor prolonga a leitura.")]
        [Min(1)] public float scrollSpeed = 38f;
        [Min(0)] public float musicFadeSeconds = 2.5f;
    }
}
