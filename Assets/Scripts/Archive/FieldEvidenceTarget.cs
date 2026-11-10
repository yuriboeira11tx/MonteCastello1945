using UnityEngine;

namespace FEB.Archive
{
    [DisallowMultipleComponent]
    public sealed class FieldEvidenceTarget : MonoBehaviour
    {
        [SerializeField] HistoricalEvidence evidence;
        public HistoricalEvidence Evidence => evidence;
    }
}
