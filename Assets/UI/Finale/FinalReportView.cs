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
            root.Q<Button>("copyCode").clicked += () =>
            {
                GUIUtility.systemCopyBuffer = report.code;
                Status("Identificação copiada.");
            };
            root.Q<Button>("exportReport").clicked += Export;
            root.Q<Button>("showCredits").clicked += ShowCredits;
            root.Q<Button>("closeReport").clicked += Close;
            root.RegisterCallback<GeometryChangedEvent>(evt =>
                root.EnableInClassList("report-compact", evt.newRect.width < 1000 || evt.newRect.height < 700));
        }

        public void Show(ResearchFinalReport snapshot, Action onClosed)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Bind();
            if (root == null) throw new InvalidOperationException("Associe PranchetaFinal.uxml ao UIDocument do relatório.");
            report = snapshot;
            closed = onClosed;
            root.style.display = DisplayStyle.Flex;
            Text("reportCode", report.code);
            Text("reportDate", DateTime.TryParse(report.completedUtc, out var date)
                ? date.ToLocalTime().ToString("dd.MM.yyyy · HH:mm") : report.completedUtc);
            Text("testNotice", report.testSession ? "SESSÃO DE TESTE DO EDITOR · SEM RESULTADO OFICIAL" : "REGISTRO FINAL DA PESQUISA");
            string score = report.firstAttemptKnown > 0
                ? $"{report.firstAttemptCorrect} / {report.firstAttemptKnown}" : "Sem histórico";
            Text("firstScore", score);
            Text("firstPercent", report.firstAttemptKnown > 0
                ? $"{100.0 * report.firstAttemptCorrect / report.firstAttemptKnown:0}% na primeira tentativa" : "Primeira tentativa não registrada");
            Text("totalTime", Duration(report.archiveSeconds + report.fieldSeconds + report.reconstructionSeconds));
            Text("timeDetail", $"Acervo {Duration(report.archiveSeconds)} · Campo {Duration(report.fieldSeconds)}\nReconstrução {Duration(report.reconstructionSeconds)}");
            Text("attempts", $"{report.attempts} tentativas · {report.errors} incorretas");
            Text("completion", $"{report.answered}/{report.questionCount} perguntas concluídas");
            Text("evidenceCount", $"{report.registeredArchiveEvidence} no acervo · {report.registeredFieldEvidence} no campo");
            Text("historyNotice", (report.fieldTimeIncomplete ? "O tempo anterior à atualização deste save não está disponível. " : "") +
                (report.answerHistoryIncomplete ? "Há respostas antigas sem histórico completo ou com gabarito alterado. " : "") +
                "Tempo registrado durante a pesquisa, sem os menus de pausa e sem os créditos.");
            Text("integrity", "Identificação para conferência com o arquivo original. A hash verifica o conteúdo; não comprova, sozinha, a origem dos resultados.");
            root.Q<TextField>("reportHash").value = ResearchReportStore.Hash(JsonUtility.ToJson(report, false));
            var list = root.Q<VisualElement>("questionResults");
            list.Clear();
            foreach (var group in report.questions.GroupBy(q => q.caseId))
            {
                var heading = new Label(group.Key == "acampamentos_da_feb" ? "ACAMPAMENTO DA FEB" : "O CASO ITAPAGÉ");
                heading.AddToClassList("result-group");
                list.Add(heading);
                int index = 0;
                foreach (var question in group)
                {
                    var entry = new VisualElement();
                    entry.AddToClassList("question-result");
                    var prompt = new Label($"{++index:00}. {question.prompt}") { enableRichText = false };
                    prompt.AddToClassList("result-prompt");
                    string state = !question.firstAttemptKnown ? "Primeira tentativa sem registro"
                        : question.firstCorrect ? "Correta na primeira tentativa" : "Corrigida após revisão";
                    var detail = new Label($"{state} · {question.attempts} tentativa(s)");
                    detail.AddToClassList("result-detail");
                    entry.Add(prompt); entry.Add(detail); list.Add(entry);
                }
            }
            root.Q<Button>("showCredits").SetEnabled(credits != null && creditsView != null);
            root.Q<ScrollView>("reportScroll").scrollOffset = Vector2.zero;
            Export();
        }

        static string Duration(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        void Text(string name, string value) => root.Q<Label>(name).text = value;
        void Status(string value) => Text("exportStatus", value);

        void Export()
        {
            try { Status("Arquivo exportado: " + ResearchReportStore.Export(report)); }
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
        }
    }
}
