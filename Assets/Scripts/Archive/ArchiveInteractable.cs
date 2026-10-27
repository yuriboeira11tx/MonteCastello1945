using UnityEngine;

namespace FEB.Archive
{
    public enum ArchiveAction { Briefing, Evidence, Chronology, Reconstruction, Report }

    [DisallowMultipleComponent]
    public sealed class ArchiveInteractable : MonoBehaviour
    {
        [SerializeField] string prompt = "Examinar";
        [SerializeField] ArchiveAction action;
        [SerializeField] HistoricalEvidence evidence;
        public string Prompt => prompt;
        public ArchiveAction Action => action;
        public HistoricalEvidence Evidence => evidence;
    }
}
