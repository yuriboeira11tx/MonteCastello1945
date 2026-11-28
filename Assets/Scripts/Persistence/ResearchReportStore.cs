using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FEB.Archive;
using UnityEngine;

namespace FEB.Persistence
{
    public static class ResearchReportStore
    {
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "ResearchReports");

        public static string NormalizeStudentName(string value)
        {
            string name = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
            if (name.Length == 0 || name.Length > 120 || name.Any(char.IsControl))
                throw new ArgumentException("Preencha o nome do aluno, com até 120 caracteres e sem quebras de linha.");
            return name;
        }

        [Serializable]
        sealed class Questionnaire
        {
            public QuestionDefinition[] questions;
        }

        [Serializable]
        sealed class QuestionDefinition
        {
            public string caseId, round, id, prompt;
            public string[] answers;
            public int correctAnswer;
            public string explanation;
        }

        public static ResearchFinalReport Create(ResearchSave save, IReadOnlyList<ArchiveCaseDefinition> cases, string buildVersion)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (cases == null) throw new ArgumentNullException(nameof(cases));
            var report = new ResearchFinalReport
            {
                reportId = Guid.NewGuid().ToString("N"),
                sessionId = save.id,
                completedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                buildVersion = string.IsNullOrWhiteSpace(buildVersion) ? Application.version : buildVersion,
                testSession = save.slot < 0
            };
            report.code = CreateCode(report.reportId);
            var results = new List<ResearchQuestionResult>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var caseIds = new HashSet<string>(StringComparer.Ordinal);
            var fieldIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var chapter in cases)
            {
                if (chapter == null) continue;
                if (string.IsNullOrWhiteSpace(chapter.Id))
                    throw new InvalidOperationException("O questionário contém um dossiê sem identificação.");
                if (!caseIds.Add(chapter.Id)) continue;
                var dossier = save.dossiers?.Find(d => d.caseId == chapter.Id);
                if (dossier != null)
                {
                    report.archiveSeconds += MeasuredSeconds(dossier.archiveSeconds);
                    report.fieldSeconds += MeasuredSeconds(dossier.fieldSeconds);
                    report.reconstructionSeconds += MeasuredSeconds(dossier.reconstructionSeconds);
                    report.fieldTimeIncomplete |= dossier.fieldTimeIncomplete ||
                        (chapter.fieldStudy && !dossier.fieldTimingStarted &&
                         (dossier.reconstructionViewed || save.campArrivalCompleted));
                    if (chapter.evidence != null)
                        report.registeredArchiveEvidence += chapter.evidence.Where(e => e != null)
                            .Select(e => e.id).Distinct(StringComparer.Ordinal)
                            .Count(id => dossier.evidence != null && dossier.evidence.Contains(id));
                }
                if (chapter.requiredFieldEvidence != null)
                    foreach (var evidence in chapter.requiredFieldEvidence)
                        if (evidence != null && save.fieldEvidence != null && save.fieldEvidence.Contains(evidence.id))
                            fieldIds.Add(evidence.id);
                AddQuestions(chapter.chronologyQuestions, "sources");
                AddQuestions(chapter.reportQuestions, "report");

                void AddQuestions(ArchiveQuestion[] questions, string round)
                {
                    if (questions == null) return;
                    foreach (var question in questions)
                    {
                        if (question == null || string.IsNullOrWhiteSpace(question.id) ||
                            string.IsNullOrWhiteSpace(question.prompt) ||
                            question.answers == null || question.answers.Length < 2 ||
                            question.answers.Any(string.IsNullOrWhiteSpace) ||
                            question.correctAnswer < 0 || question.correctAnswer >= question.answers.Length)
                            throw new InvalidOperationException("O questionário contém uma pergunta incompleta.");
                        if (!identities.Add(chapter.Id + "\u001f" + question.id))
                            throw new InvalidOperationException("Há identificadores de pergunta repetidos no dossiê " + chapter.Id + ".");
                        results.Add(new ResearchQuestionResult
                        {
                            caseId = chapter.Id, round = round, id = question.id, prompt = question.prompt,
                            answers = (string[])question.answers.Clone(), correctAnswer = question.correctAnswer,
                            explanation = question.explanation ?? string.Empty,
                            answered = dossier?.answeredQuestions?.Contains(question.id) == true
                        });
                    }
                }
            }
            report.registeredFieldEvidence = fieldIds.Count;
            var lookup = results.ToDictionary(q => Key(q.caseId, q.round, q.id), StringComparer.Ordinal);
            if (save.events != null)
                foreach (var entry in save.events)
                {
                    if (entry == null || (entry.action != "source_answer" && entry.action != "report_answer")) continue;
                    if (!caseIds.Contains(entry.subject ?? string.Empty)) continue;
                    string round = entry.action == "source_answer" ? "sources" : "report";
                    if (!ReadAttempt(entry.detail, out var questionId, out var answer, out var correct))
                    {
                        report.answerHistoryIncomplete = true;
                        continue;
                    }
                    if (!lookup.TryGetValue(Key(entry.subject, round, questionId), out var result)) continue;
                    if (answer < 0 || answer >= result.answers.Length)
                    {
                        report.answerHistoryIncomplete = result.historicalAnswerMismatch = true;
                        continue;
                    }
                    if (result.attempts == 0)
                    {
                        result.firstAttemptKnown = true;
                        result.firstAnswer = answer;
                        result.firstCorrect = correct;
                    }
                    result.attempts++;
                    if (correct) { result.correctResponses++; result.answered = true; }
                    if (correct != (answer == result.correctAnswer))
                        report.answerHistoryIncomplete = result.historicalAnswerMismatch = true;
                }
            foreach (var result in results)
            {
                if (result.answered && result.correctResponses == 0)
                {
                    // Saves antigos podem conter o acerto sem conservar as tentativas originais.
                    report.answerHistoryIncomplete = true;
                    result.firstAttemptKnown = false;
                    result.firstAnswer = -1;
                    result.firstCorrect = false;
                }
                if (result.answered) report.answered++;
                if (result.firstAttemptKnown)
                {
                    report.firstAttemptKnown++;
                    if (result.firstCorrect) report.firstAttemptCorrect++;
                }
                report.attempts += result.attempts;
                report.errors += result.attempts - result.correctResponses;
            }
            report.questions = results.ToArray();
            report.questionCount = results.Count;
            var definition = new Questionnaire
            {
                questions = results.Select(q => new QuestionDefinition
                {
                    caseId = q.caseId, round = q.round, id = q.id, prompt = q.prompt,
                    answers = q.answers, correctAnswer = q.correctAnswer, explanation = q.explanation
                }).ToArray()
            };
            report.questionnaireDigest = Hash(JsonUtility.ToJson(definition, false));
            return report;
        }

        static string Key(string caseId, string round, string id) => caseId + "\u001f" + round + "\u001f" + id;
        static double MeasuredSeconds(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, value);

        static bool ReadAttempt(string detail, out string id, out int answer, out bool correct)
        {
            id = null; answer = -1; correct = false;
            if (string.IsNullOrEmpty(detail)) return false;
            var parts = detail.Split(';');
            if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0]) ||
                !parts[1].StartsWith("answer=", StringComparison.Ordinal) ||
                !parts[2].StartsWith("correct=", StringComparison.Ordinal)) return false;
            id = parts[0];
            return int.TryParse(parts[1].Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out answer) &&
                bool.TryParse(parts[2].Substring(8), out correct);
        }

        public static string Hash(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(Utf8.GetBytes(value));
                var hex = new StringBuilder(bytes.Length * 2);
                foreach (var item in bytes) hex.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        static string CreateCode(string reportId)
        {
            byte[] bytes;
            using (var algorithm = SHA256.Create()) bytes = algorithm.ComputeHash(Utf8.GetBytes(reportId));
            var body = new StringBuilder(14);
            int checksum = 0;
            for (int i = 0; i < 12; i++)
            {
                int bit = i * 5, index = bit / 8, offset = bit % 8;
                int value = ((bytes[index] << 8 | bytes[index + 1]) >> (11 - offset)) & 31;
                body.Append(Alphabet[value]);
                checksum = (checksum * 32 + value) % 1021;
            }
            body.Append(Alphabet[checksum >> 5]);
            body.Append(Alphabet[checksum & 31]);
            return FormatCode(body.ToString());
        }

        static string FormatCode(string body) => "FEB-" + body.Substring(0, 4) + "-" + body.Substring(4, 4) + "-" + body.Substring(8, 4) + "-" + body.Substring(12, 2);

        public static string NormalizeCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var raw = new StringBuilder();
            foreach (char c in value)
                if (!char.IsWhiteSpace(c) && c != '-') raw.Append(char.ToUpperInvariant(c));
            string body = raw.ToString();
            if (body.Length == 17 && body.StartsWith("FEB", StringComparison.Ordinal)) body = body.Substring(3);
            if (body.Length != 14) return string.Empty;
            body = body.Replace('O', '0').Replace('I', '1').Replace('L', '1');
            foreach (char c in body) if (Alphabet.IndexOf(c) < 0) return string.Empty;
            return FormatCode(body);
        }

        public static bool ValidateCode(string value)
        {
            string normalized = NormalizeCode(value);
            if (normalized.Length == 0) return false;
            string body = normalized.Substring(4).Replace("-", string.Empty);
            int checksum = 0;
            for (int i = 0; i < 12; i++) checksum = (checksum * 32 + Alphabet.IndexOf(body[i])) % 1021;
            return checksum == Alphabet.IndexOf(body[12]) * 32 + Alphabet.IndexOf(body[13]);
        }

    }
}
