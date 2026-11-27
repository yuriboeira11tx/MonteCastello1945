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
        [Tooltip("Título curto do objeto ou da imagem, exibido durante a leitura.")]
        [TextArea(2, 5)] public string imageCaption;
        [Tooltip("Referências para revisão histórica no Editor. Não aparecem nas interfaces de leitura.")]
        [TextArea(2, 5)] public string citation;
        [Tooltip("Endereço da fonte para consulta no Editor. Não aparece para o jogador.")]
        public string sourceUrl;
        public DocumentPage[] pages = System.Array.Empty<DocumentPage>();
        public string chronologyLabel;
        public int chronologyOrder;
        public GameObject inspectionPrefab;
        public Vector3 inspectionRotation = new Vector3(6, -14, -3);
    }

    [System.Serializable]
    public sealed class DocumentPage
    {
        public string title;
        [TextArea(8, 30)] public string text;
        public Texture2D image;
        [Tooltip("Título curto da imagem desta página.")]
        [TextArea(2, 5)] public string caption;
    }
}
