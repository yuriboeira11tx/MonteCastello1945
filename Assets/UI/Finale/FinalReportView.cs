using System;
using System.Collections.Generic;
using System.Linq;
using FEB.Archive;
using FEB.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class FinalReportView : MonoBehaviour
    {
        [SerializeField] GameCreditsDefinition credits;
        [SerializeField] GameCreditsView creditsView;
        [Tooltip("Músicas do acervo suspensas enquanto os créditos tocam.")]
        [SerializeField] AudioSource[] backgroundMusic = Array.Empty<AudioSource>();
        readonly List<AudioSource> pausedMusic = new List<AudioSource>();
        VisualElement root;
        ResearchFinalReport report;
        string reportToken;
        string encryptedReportToken;
        Action<string> confirmName;
        bool confirmingName;
        Action closed;

        void OnEnable() { Bind(); if (root != null) root.style.display = DisplayStyle.None; }

        void Bind()
        {
            var documentRoot = GetComponent<UIDocument>().rootVisualElement;
            var screen = documentRoot?.Q("reportRoot");
            if (screen == null || root == screen) return;
            documentRoot.StretchToParentSize();
            documentRoot.pickingMode = PickingMode.Ignore;
            root = screen;
            root.Q<Button>("copyJwt").clicked += () =>
            {
                if (string.IsNullOrEmpty(encryptedReportToken)) return;
                GUIUtility.systemCopyBuffer = encryptedReportToken;
                Status("Código copiado. Cole o conteúdo no formulário de avaliação.");
            };
            root.Q<Button>("exportReport").clicked += () => Export(true);
            root.Q<Button>("showCredits").clicked += ShowCredits;
            root.Q<Button>("closeReport").clicked += Close;
            root.Q<Button>("confirmStudentName").clicked += ConfirmStudentName;
            root.RegisterCallback<GeometryChangedEvent>(evt =>
                root.EnableInClassList("report-compact", evt.newRect.width < 1000 || evt.newRect.height < 700));
        }

        public void Show(ResearchFinalReport snapshot, string jwt, string encryptedJwt, Action onClosed)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!string.IsNullOrEmpty(jwt)) ResearchReportJwt.VerifyMatches(jwt, snapshot);
            if (!string.IsNullOrEmpty(jwt)) ResearchReportJwe.ValidateEnvelope(encryptedJwt);
            Bind();
            if (root == null) throw new InvalidOperationException("Associe PranchetaFinal.uxml ao UIDocument do relatório.");
            report = snapshot;
            reportToken = jwt;
            encryptedReportToken = string.IsNullOrEmpty(jwt) ? null : encryptedJwt;
            confirmName = null;
            closed = onClosed;
            root.style.display = DisplayStyle.Flex;
            root.AddToClassList("report-issued");
            Text("reportCode", report.code?.Replace("-", " "));
            Text("reportDate", DateTime.TryParse(report.completedUtc, out var date)
                ? date.ToLocalTime().ToString("dd/MM/yyyy  HH:mm") : report.completedUtc);
            Text("testNotice", report.testSession ? "Demonstração com dados de teste" : "Pesquisa concluída");
            string score = report.firstAttemptKnown > 0
                ? $"{report.firstAttemptCorrect} de {report.firstAttemptKnown}" : "Sem histórico";
            Text("firstScore", score);
            Text("firstPercent", report.firstAttemptKnown > 0
                ? $"Aproveitamento de {100.0 * report.firstAttemptCorrect / report.firstAttemptKnown:0}%" : "Primeira tentativa não registrada");
            Text("totalTime", Duration(report.archiveSeconds + report.fieldSeconds + report.reconstructionSeconds));
            Text("timeDetail", $"Acervo: {Duration(report.archiveSeconds)}\nAcampamento: {Duration(report.fieldSeconds)}\nReconstrução: {Duration(report.reconstructionSeconds)}");
            Text("attempts", $"{report.attempts} respostas enviadas. {report.errors} incorretas.");
            Text("completion", $"Perguntas concluídas: {report.answered} de {report.questionCount}.");
            Text("evidenceCount", $"Evidências: {report.registeredArchiveEvidence} no acervo e {report.registeredFieldEvidence} no acampamento.");
            Text("historyNotice", (report.fieldTimeIncomplete ? "O tempo anterior à atualização deste save não está disponível. " : "") +
                (report.answerHistoryIncomplete ? "Há respostas antigas sem histórico completo ou com gabarito alterado. " : "") +
                "Pausas e créditos não entram no tempo de pesquisa.");
            Text("integrity", string.IsNullOrEmpty(reportToken)
                ? "Este relatório antigo está disponível apenas para leitura."
                : "Use Copiar JWT para enviar seu resultado no formulário de avaliação.");
            root.Q<Foldout>("questionDetails").value = false;
            var list = root.Q<VisualElement>("questionResults");
            list.Clear();
            foreach (var group in report.questions.GroupBy(q => q.caseId))
            {
                var heading = new Label(group.Key == "acampamentos_da_feb" ? "ACAMPAMENTO DA FEB" : "O CASO ITAPAGÉ");
                heading.AddToClassList("result-group");
                list.Add(heading);
                foreach (var question in group)
                {
                    var entry = new VisualElement();
                    entry.AddToClassList("question-result");
                    var prompt = new Label((question.prompt ?? string.Empty).Replace("nº", "número").Replace("n°", "número").Replace(" — ", ". ").Replace(" – ", ". ")) { enableRichText = false };
                    prompt.AddToClassList("result-prompt");
                    string state = !question.firstAttemptKnown ? "Primeira tentativa sem registro"
                        : question.firstCorrect ? "Correta na primeira tentativa" : "Corrigida após revisão";
                    string attempts = question.attempts == 1 ? "1 tentativa" : $"{question.attempts} tentativas";
                    var detail = new Label($"{state}. {attempts}.");
                    detail.AddToClassList("result-detail");
                    entry.Add(prompt); entry.Add(detail); list.Add(entry);
                }
            }
            root.Q<Button>("showCredits").SetEnabled(credits != null && creditsView != null);
            root.Q<Button>("copyJwt").SetEnabled(!string.IsNullOrEmpty(reportToken));
            root.Q<Button>("exportReport").SetEnabled(!string.IsNullOrEmpty(reportToken));
            var name = root.Q<TextField>("studentName");
            name.SetValueWithoutNotify(report.studentName ?? string.Empty);
            name.isReadOnly = true;
            root.Q<Button>("confirmStudentName").style.display = DisplayStyle.None;
            Text("studentNameHelp", string.IsNullOrEmpty(report.studentName)
                ? "Este relatório antigo não contém o nome do aluno."
                : "Nome registrado no relatório enviado ao professor.");
            root.Q<ScrollView>("reportScroll").scrollOffset = Vector2.zero;
            if (!string.IsNullOrEmpty(reportToken)) Export(false);
            else Status("A cópia do código está disponível nos relatórios de novas partidas.");
        }

        public void ShowDraft(ResearchFinalReport snapshot, Action<string> onConfirm, Action onClosed)
        {
            Show(snapshot, null, null, onClosed);
            root.RemoveFromClassList("report-issued");
            confirmName = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
            var name = root.Q<TextField>("studentName");
            name.isReadOnly = false;
            root.Q<Button>("confirmStudentName").style.display = DisplayStyle.Flex;
            root.Q<Button>("showCredits").SetEnabled(false);
            Text("testNotice", snapshot.testSession ? "Demonstração com dados de teste" : "Confira seus dados antes de concluir");
            Text("reportCode", "Aguardando confirmação");
            Text("integrity", "A identificação será gerada após a confirmação do nome.");
            Text("studentNameHelp", "Use o mesmo nome informado nos formulários.");
            Status("Confirme seu nome para liberar a cópia do JWT.");
            name.Focus();
        }

        void ConfirmStudentName()
        {
            if (confirmName == null || confirmingName) return;
            confirmingName = true;
            try
            {
                string name = ResearchReportStore.NormalizeStudentName(root.Q<TextField>("studentName").value);
                confirmName(name);
            }
            catch (Exception error) { Status(error.Message); }
            finally { confirmingName = false; }
        }

        static string Duration(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        void Text(string name, string value) => root.Q<Label>(name).text = value;
        void Status(string value) => Text("exportStatus", value);

        void Export(bool showPath)
        {
            try
            {
                string path = ResearchReportStore.Export(report, reportToken, encryptedReportToken);
                Status(showPath ? "Arquivo salvo em: " + path : "Relatório salvo.");
            }
            catch (Exception error) { Status("Não foi possível exportar. O resultado permanece no save. " + error.Message); }
        }

        void ShowCredits()
        {
            if (creditsView == null || credits == null) return;
            foreach (var source in backgroundMusic)
                if (source != null && source.isPlaying) { source.Pause(); pausedMusic.Add(source); }
            root.style.display = DisplayStyle.None;
            try
            {
                creditsView.Show(credits, () =>
                {
                    ResumeMusic();
                    root.style.display = DisplayStyle.Flex;
                    Status("Pesquisa concluída. Seu relatório permanece disponível neste save.");
                });
            }
            catch (Exception error)
            {
                ResumeMusic(); root.style.display = DisplayStyle.Flex;
                Status("Não foi possível abrir os créditos: " + error.Message);
            }
        }

        void ResumeMusic()
        {
            foreach (var source in pausedMusic) if (source != null) source.UnPause();
            pausedMusic.Clear();
        }

        void Close()
        {
            confirmName = null;
            root.style.display = DisplayStyle.None;
            var notify = closed; closed = null;
            notify?.Invoke();
        }

        void OnDisable()
        {
            creditsView?.Hide();
            ResumeMusic();
            if (root != null) root.style.display = DisplayStyle.None;
            root = null;
            closed = null;
            confirmName = null;
        }
    }
}
