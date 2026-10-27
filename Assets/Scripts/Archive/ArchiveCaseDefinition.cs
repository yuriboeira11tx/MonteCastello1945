using UnityEngine;

namespace FEB.Archive
{
    [CreateAssetMenu(menuName = "FEB/Caso de pesquisa")]
    public sealed class ArchiveCaseDefinition : ScriptableObject
    {
        public string title;
        [TextArea(5, 12)] public string mission;
        public HistoricalEvidence[] evidence;
        [TextArea(3, 8)] public string chronologyIntroduction;
        [TextArea(3, 8)] public string chronologyExplanation;
        [TextArea(3, 8)] public string reconstructionIntroduction;
        [TextArea(2, 6)] public string reportQuestion;
        public string[] reportAnswers;
        public int correctReportAnswer;
        [TextArea(3, 8)] public string reportExplanation;
        public string archiveScene = "ArquivoHistorico";
        public string reconstructionScene = "CutsceneInicial";
        public string menuScene = "Menu";
    }
}
