using UnityEngine;

namespace FEB.UI
{
    public static class SubtitlePreferences
    {
        const string EnabledKey = "FEB.Subtitles.Enabled";
        const string SizeKey = "FEB.Subtitles.Size";
        static readonly float[] Scales = { 1f, 1.25f, 1.5f };

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set => SaveChanged(EnabledKey, value ? 1 : 0, 1);
        }

        internal static int SizeIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SizeKey, 0), 0, Scales.Length - 1);
            set => SaveChanged(SizeKey, Mathf.Clamp(value, 0, Scales.Length - 1), 0);
        }

        public static float Scale => Scales[SizeIndex];

        static void SaveChanged(string key, int value, int defaultValue)
        {
            if (PlayerPrefs.GetInt(key, defaultValue) == value) return;
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
