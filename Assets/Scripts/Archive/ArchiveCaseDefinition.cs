using System;
using System.Collections.Generic;
using UnityEngine;

namespace FEB.Archive
{
    [Serializable]
    public sealed class ArchiveQuestion
    {
        [Tooltip("Identificador único no dossiê. Mantenha-o ao editar ou reordenar a pergunta para preservar os saves.")]
        public string id;
        [TextArea(2, 6)] public string prompt;
        public string[] answers;
        [Tooltip("Posição da alternativa correta, começando em 0.")]
        public int correctAnswer;
        [TextArea(2, 6)] public string explanation;
    }

    [CreateAssetMenu(menuName = "FEB/Caso de pesquisa")]
    public sealed class ArchiveCaseDefinition : ScriptableObject
    {
        public string caseId;
        public string Id => string.IsNullOrWhiteSpace(caseId) ? name : caseId;
        public string title;
        [Tooltip("A mesa abre uma visita de campo depois das atividades de preparação deste dossiê.")]
        public bool fieldStudy;
        [TextArea(5, 12)] public string mission;
        public HistoricalEvidence[] evidence;
        [Header("Conclusão da visita de campo")]
        [Tooltip("Evidências encontradas na reconstrução que devem ser registradas antes do relatório final.")]
        public HistoricalEvidence[] requiredFieldEvidence = Array.Empty<HistoricalEvidence>();
        public bool HasFieldReport => fieldStudy && reportQuestions != null && reportQuestions.Length > 0;

        public bool FieldEvidenceComplete(FEB.Persistence.ResearchSave save) => requiredFieldEvidence != null &&
            requiredFieldEvidence.Length > 0 && Array.TrueForAll(requiredFieldEvidence,
                item => item != null && save.fieldEvidence.Contains(item.id));
        [Header("Quadro de evidências")]
        [TextArea(2, 5)] public string boardConclusion;
        [Tooltip("Fontes que sustentam a legenda após a reconstrução.")]
        public HistoricalEvidence[] boardSources = Array.Empty<HistoricalEvidence>();
        [Tooltip("Nas visitas de campo, informe o tema de cada fotografia na mesma ordem da lista Evidence.")]
        public string[] boardThemes = Array.Empty<string>();
        [TextArea(3, 8)] public string chronologyIntroduction;
        [TextArea(3, 8)] public string chronologyExplanation;
        [Tooltip("Perguntas sobre as fontes já registradas, depois da ordenação.")]
        public ArchiveQuestion[] chronologyQuestions = Array.Empty<ArchiveQuestion>();
        [TextArea(3, 8)] public string reconstructionIntroduction;
        [Tooltip("Perguntas do relatório, depois de concluir a reconstrução.")]
        public ArchiveQuestion[] reportQuestions = Array.Empty<ArchiveQuestion>();
        [TextArea(3, 8)] public string reportExplanation;
        public string archiveScene = "ArquivoHistorico";
        public string reconstructionScene = "CutsceneInicial";
        public string menuScene = "Menu";
        public string followUpScene;
        public ArchiveCaseDefinition nextCase;

        internal bool QuestionsAreValid()
        {
            var ids = new HashSet<string>();
            bool Valid(ArchiveQuestion q) => q != null && !string.IsNullOrWhiteSpace(q.id) && ids.Add(q.id)
                && !string.IsNullOrWhiteSpace(q.prompt) && !string.IsNullOrWhiteSpace(q.explanation)
                && q.answers != null && q.answers.Length >= 2 && !Array.Exists(q.answers, string.IsNullOrWhiteSpace)
                && q.correctAnswer >= 0 && q.correctAnswer < q.answers.Length;
            return chronologyQuestions != null && Array.TrueForAll(chronologyQuestions, Valid)
                && reportQuestions != null && (fieldStudy || reportQuestions.Length > 0) && Array.TrueForAll(reportQuestions, Valid);
        }
    }
}
