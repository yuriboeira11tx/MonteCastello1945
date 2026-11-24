using System;

namespace FEB.Persistence
{
    [Serializable]
    public sealed class ResearchFinalReport
    {
        public int schemaVersion = 1;
        public string reportId;
        public string code;
        public string sessionId;
        public string studentName;
        public string completedUtc;
        public string buildVersion;
        public string questionnaireDigest;
        public bool testSession;
        public bool fieldTimeIncomplete;
        public bool answerHistoryIncomplete;
        public double archiveSeconds;
        public double fieldSeconds;
        public double reconstructionSeconds;
        public int firstAttemptCorrect;
        public int firstAttemptKnown;
        public int answered;
        public int attempts;
        public int errors;
        public int registeredArchiveEvidence;
        public int registeredFieldEvidence;
        public int questionCount;
        public ResearchQuestionResult[] questions = Array.Empty<ResearchQuestionResult>();
    }

    [Serializable]
    public sealed class ResearchQuestionResult
    {
        public string caseId;
        public string round;
        public string id;
        public string prompt;
        public string[] answers = Array.Empty<string>();
        public int correctAnswer;
        public string explanation;
        public bool answered;
        public int attempts;
        public int correctResponses;
        public bool firstAttemptKnown;
        public int firstAnswer = -1;
        public bool firstCorrect;
        public bool historicalAnswerMismatch;
    }

    [Serializable]
    public sealed class ResearchReportEnvelope
    {
        public string jwt;
    }
}
