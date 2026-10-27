using UnityEngine;

namespace FEB.Archive
{
    [CreateAssetMenu(menuName = "FEB/Evidência histórica")]
    public sealed class HistoricalEvidence : ScriptableObject
    {
        public string id;
        public string title;
        public string dateLabel;
        [TextArea(5, 16)] public string description;
        [TextArea(2, 5)] public string keyFinding;
        public Texture2D image;
        [TextArea(2, 5)] public string imageCaption;
        [TextArea(2, 5)] public string citation;
        public string sourceUrl;
        public string chronologyLabel;
        public int chronologyOrder;
    }
}
