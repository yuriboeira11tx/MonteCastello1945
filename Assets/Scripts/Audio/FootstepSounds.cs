using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Footstep Sounds")]
public sealed class FootstepSounds : ScriptableObject
{
    [System.Serializable]
    private struct Variation
    {
        public AudioClip clip;
        [Range(-24f, 0f)] public float gainDb;
    }

    public struct PlaybackHistory
    {
        internal AudioClip lastLeft;
        internal AudioClip lastRight;
    }

    [SerializeField] private Variation[] leftFoot;
    [SerializeField] private Variation[] rightFoot;

    public void Play(AudioSource source, bool isLeftFoot, ref PlaybackHistory history, float intensity = 1f)
    {
        Variation[] variations = isLeftFoot ? leftFoot : rightFoot;
        if (variations == null) return;

        AudioClip previous = isLeftFoot ? history.lastLeft : history.lastRight;
        Variation selected = default;
        int alternatives = 0;
        foreach (Variation variation in variations)
        {
            if (variation.clip == null) continue;
            if (variation.clip == previous)
            {
                // Uma lista com um único som também pode ser usada.
                if (alternatives == 0) selected = variation;
                continue;
            }

            alternatives++;
            if (Random.Range(0, alternatives) == 0) selected = variation;
        }

        if (selected.clip == null) return;
        if (isLeftFoot) history.lastLeft = selected.clip;
        else history.lastRight = selected.clip;

        float gain = Mathf.Pow(10f, selected.gainDb / 20f);
        source.PlayOneShot(selected.clip, gain * intensity * Random.Range(0.94f, 1f));
    }
}
