using System;
using System.Collections.Generic;
using System.Linq;
using FEB.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FEB.Archive
{
    public sealed class ArchiveExperience : MonoBehaviour
    {
        [SerializeField] ArchiveCaseDefinition caseDefinition;
        [SerializeField] ArchivePlayerInteraction interaction;
        [SerializeField] Transform player;
        readonly List<HistoricalEvidence> orderedEvidence = new List<HistoricalEvidence>();
        ResearchSave progress;
        DossierProgress dossier;
        readonly List<ArchiveCaseDefinition> cases = new List<ArchiveCaseDefinition>();
        float nextAutosave;
        bool leaving;
        bool HasReconstruction => dossier.reconstructionViewed;
        FieldNotebook Notebook => interaction.Notebook;
        public ResearchSave Progress => progress;
        public string CurrentObjective
        {
            get
            {
                if (dossier == null) return string.Empty;
                if (!dossier.briefingRead) return "Examine a pasta de pesquisa sobre a mesa.";
                if (!caseDefinition.evidence.All(item => dossier.evidence.Contains(item.id))) return "Leia e registre os documentos deste dossiê.";
                if (!dossier.chronologySolved) return "Organize os acontecimentos na linha do tempo.";
                if (!HasReconstruction) return "Conclua a atividade na mesa de reconstrução.";
                if (!dossier.reportCompleted) return "Conclua o relatório na caderneta.";
                return "Dossiê concluído. Consulte as experiências e dossiês na caderneta.";
            }
        }
        public string EvidenceCounts
        {
            get
            {
                if (dossier == null) return string.Empty;
                var evidence = caseDefinition.evidence;
                int found = evidence.Count(item => dossier.discoveredEvidence.Contains(item.id));
                int registered = evidence.Count(item => dossier.evidence.Contains(item.id));
                return $"Encontradas {found}/{evidence.Length}   ·   Registradas {registered}/{evidence.Length}";
            }
        }

        void Start()
        {
            progress = ResearchSaveStore.EnsureSession();
            for (var item = caseDefinition; item != null && !cases.Contains(item); item = item.nextCase) cases.Add(item);
            if (string.IsNullOrWhiteSpace(progress.activeCaseId)) progress.SelectCase(caseDefinition.Id);
            var savedCase = cases.Find(c => c.Id == progress.activeCaseId);
            if (savedCase != null) caseDefinition = savedCase;
            dossier = progress.SelectCase(caseDefinition.Id);
            progress.archiveScene = caseDefinition.archiveScene;
            dossier.discoveredEvidence ??= new List<string>();
            foreach (var id in dossier.evidence)
                if (!dossier.discoveredEvidence.Contains(id)) dossier.discoveredEvidence.Add(id);
            progress.scene = caseDefinition.archiveScene;
            if (progress.hasPlayerPosition)
            {
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.SetPositionAndRotation(progress.playerPosition, Quaternion.Euler(0, progress.playerYaw, 0));
                controller.enabled = true;
            }
            interaction.SetBlocked(false);
            nextAutosave = Time.unscaledTime + 30;
            if (HasReconstruction && !dossier.reportCompleted) OpenReport();
        }

        void Update()
        {
            if (progress == null || leaving) return;
            if (Application.isFocused) dossier.archiveSeconds += Time.unscaledDeltaTime;
            if (Time.unscaledTime >= nextAutosave) { SaveProgress(); nextAutosave = Time.unscaledTime + 30; }
        }

        public void Interact(ArchiveInteractable target)
        {
            progress.Record("interaction", target.Action.ToString());
            switch (target.Action)
            {
                case ArchiveAction.Briefing: OpenBriefing(); break;
                case ArchiveAction.Evidence: OpenEvidence(target.Evidence); break;
                case ArchiveAction.Chronology: OpenChronology(); break;
                case ArchiveAction.Reconstruction: OpenReconstruction(); break;
                case ArchiveAction.Report: OpenReport(); break;
            }
        }

        public void OpenBriefing()
        {
            Notebook.ShowActivity("Pasta de pesquisa", caseDefinition.title, caseDefinition.mission);
            Notebook.AddChoice("Assumir a pesquisa", () =>
            {
                dossier.briefingRead = true;
                progress.Record("briefing_read");
                if (SaveProgress()) Notebook.CloseBook();
            });
        }

        public void OpenEvidence(HistoricalEvidence document, bool fromNotebook = false)
        {
            if (document == null) return;
            if (!caseDefinition.evidence.Contains(document))
            {
                Notebook.ShowActivity("Documento de outro dossiê", document.title, "Selecione o dossiê correspondente na caderneta para registrar esta fonte.");
                Notebook.AddChoice("Selecionar dossiê", OpenCaseSelection);
                return;
            }
            progress.Record("document_opened", document.id);
            if (!SaveEvidenceStep(dossier.discoveredEvidence, document.id, "evidence_discovered", showError: true)) return;
            interaction.OpenInspection(document, dossier.evidence.Contains(document.id),
                () => SaveEvidenceStep(dossier.evidence, document.id, "evidence_recorded"),
                dossier.readEvidence.Contains(document.id),
                () => SaveEvidenceStep(dossier.readEvidence, document.id, "document_read"),
                fromNotebook ? () => Notebook.ShowEvidenceIndex(document) : (Action)null);
        }

        bool SaveEvidenceStep(List<string> ids, string id, string action, bool showError = false)
        {
            if (ids.Contains(id)) return true;
            ids.Add(id);
            progress.Record(action, id);
            if (SaveProgress(showError)) return true;
            ids.Remove(id);
            progress.events.RemoveAt(progress.events.Count - 1);
            return false;
        }

        public ArchiveCaseDefinition ActiveCase => caseDefinition;
        public bool HasMultipleCases => cases.Count > 1;
        public void OpenNotebook() => Notebook.ShowIndex();

        public void OpenCaseSelection()
        {
            Notebook.ShowActivity("Dossiês do arquivo", "Investigações e experiências", "Conclua uma investigação para liberar a seguinte. As evidências e conclusões de cada dossiê ficam preservadas.");
            for (int i = 0; i < cases.Count; i++)
            {
                var item = cases[i];
                var previous = i > 0 ? cases[i - 1] : null;
                var state = previous == null ? null : progress.dossiers.Find(d => d.caseId == previous.Id);
                bool available = previous == null || (state != null && state.reportCompleted &&
                    (string.IsNullOrWhiteSpace(previous.followUpScene) || state.followUpCompleted));
                Notebook.AddChoice(item.title + (available ? "" : " · Bloqueado"), () =>
                {
                    caseDefinition = item;
                    dossier = progress.SelectCase(item.Id);
                    progress.archiveScene = item.archiveScene;
                    orderedEvidence.Clear();
                    if (SaveProgress()) OpenBriefing();
                }, available);
            }
        }

        public void OpenChronology()
        {
            if (!caseDefinition.evidence.All(d => dossier.evidence.Contains(d.id)))
            {
                Notebook.ShowActivity("Linha do tempo", "Evidências pendentes", "Consulte e registre todos os documentos deste dossiê. Procure as datas e o que cada fonte permite concluir.");
                Notebook.AddChoice("Continuar a pesquisa", Notebook.CloseBook);
                return;
            }
            if (dossier.chronologySolved)
            {
                Notebook.ShowActivity("Cronologia estabelecida", caseDefinition.title, caseDefinition.chronologyExplanation);
                Notebook.AddChoice("Examinar a reconstrução", OpenReconstruction);
                return;
            }
            orderedEvidence.Clear();
            ShowChronologyChoices();
        }

        void ShowChronologyChoices()
        {
            var selected = string.Join("\n", orderedEvidence.Select((d, i) => $"{i + 1}. {d.chronologyLabel}"));
            Notebook.ShowActivity("Organize os acontecimentos", "Do mais antigo ao mais recente", caseDefinition.chronologyIntroduction + "\n\n" + selected);
            foreach (var document in caseDefinition.evidence.OrderByDescending(d => d.chronologyOrder))
            {
                var item = document;
                Notebook.AddChoice(item.chronologyLabel, () =>
                {
                    orderedEvidence.Add(item);
                    if (orderedEvidence.Count == caseDefinition.evidence.Length) CheckChronology();
                    else ShowChronologyChoices();
                }, !orderedEvidence.Contains(item));
            }
            Notebook.AddChoice("Recomeçar a ordenação", OpenChronology);
        }

        void CheckChronology()
        {
            dossier.chronologyAttempts++;
            bool correct = orderedEvidence.SequenceEqual(orderedEvidence.OrderBy(d => d.chronologyOrder));
            progress.Record("chronology_answer", caseDefinition.Id, string.Join(",", orderedEvidence.Select(d => d.id)) + ";correct=" + correct);
            dossier.chronologySolved = correct;
            if (!SaveProgress()) return;
            Notebook.ShowActivity(correct ? "Cronologia estabelecida" : "Reveja as datas", correct ? "Reconstrução disponível" : "Você pode consultar a caderneta", correct ? caseDefinition.chronologyExplanation : "Compare as datas e diferencie os acontecimentos apresentados nas fontes. Consulte os documentos e tente novamente.");
            Notebook.AddChoice(correct ? "Examinar a reconstrução" : "Tentar novamente", correct ? (Action)OpenReconstruction : OpenChronology);
            Notebook.AddChoice("Voltar ao arquivo", Notebook.CloseBook);
        }

        public void OpenReconstruction()
        {
            Notebook.ShowActivity("Reconstrução interativa", caseDefinition.title, dossier.chronologySolved ? caseDefinition.reconstructionIntroduction + "\n\nSegure Esc por 2 segundos para voltar ao arquivo. Sair antes de concluir mantém a atividade pendente." : "Registre as fontes e organize a linha do tempo antes de examinar a reconstrução deste caso.");
            Notebook.AddChoice("Examinar reconstrução", () => LeaveFor(caseDefinition.reconstructionScene), dossier.chronologySolved);
        }

        public void OpenReport()
        {
            if (!HasReconstruction)
            {
                Notebook.ShowActivity("Relatório de pesquisa", "Investigação em andamento", "Registre as evidências, organize a cronologia e examine a reconstrução para concluir o dossiê.");
                Notebook.AddChoice("Voltar ao arquivo", Notebook.CloseBook);
                return;
            }
            if (dossier.reportCompleted) { ShowCompletedReport(); return; }
            Notebook.ShowActivity("Sua conclusão", "Relacione os documentos ao caso investigado", caseDefinition.reportQuestion);
            for (int i = 0; i < caseDefinition.reportAnswers.Length; i++)
            {
                int answer = i;
                Notebook.AddChoice(caseDefinition.reportAnswers[i], () => SubmitReport(answer));
            }
        }

        void SubmitReport(int answer)
        {
            dossier.reportAttempts++;
            bool correct = answer == caseDefinition.correctReportAnswer;
            progress.Record("report_answer", caseDefinition.Id, answer + ";correct=" + correct);
            if (correct) dossier.reportCompleted = true;
            if (!SaveProgress()) return;
            if (correct) ShowCompletedReport();
            else
            {
                Notebook.ShowActivity("Retome as evidências", "A conclusão precisa respeitar a cronologia", caseDefinition.reportExplanation);
                Notebook.AddChoice("Revisar minha conclusão", OpenReport);
                Notebook.AddChoice("Consultar a caderneta", OpenNotebook);
            }
        }

        void ShowCompletedReport()
        {
            Notebook.ShowActivity("Dossiê concluído", caseDefinition.title, caseDefinition.reportExplanation + $"\n\n{EvidenceCounts}\nCronologia e interpretação concluídas.\nTempo no arquivo {TimeSpan.FromSeconds(dossier.archiveSeconds):mm\\:ss}\nTentativas de cronologia {dossier.chronologyAttempts}   ·   Tentativas de interpretação {dossier.reportAttempts}");
            if (!string.IsNullOrWhiteSpace(caseDefinition.followUpScene))
                Notebook.AddChoice("Entrar na experiência deste dossiê", () => LeaveFor(caseDefinition.followUpScene));
            if (caseDefinition.nextCase != null) Notebook.AddChoice("Próximo dossiê", OpenCaseSelection);
            Notebook.AddChoice("Continuar explorando o arquivo", Notebook.CloseBook);
            Notebook.AddChoice("Salvar e voltar ao menu", () => LeaveFor(caseDefinition.menuScene));
        }

        public void OpenPause()
        {
            Notebook.ShowActivity("Opções", "Pesquisa em andamento", "O progresso é salvo ao registrar evidências e concluir atividades.", true);
            Notebook.AddChoice("Retomar", Notebook.CloseBook);
            Notebook.AddChoice("Índice da caderneta", OpenNotebook);
            Notebook.AddChoice("Salvar e voltar ao menu", () => LeaveFor(caseDefinition.menuScene));
        }

        bool SaveProgress(bool showError = true)
        {
            if (progress == null) return true;
            progress.hasPlayerPosition = true;
            progress.playerPosition = player.position;
            progress.playerYaw = player.eulerAngles.y;
            if (ResearchSaveStore.Save(out var error)) return true;
            if (showError) Notebook.ShowActivity("Progresso não salvo", "Verifique o espaço disponível no computador", error);
            return false;
        }

        void LeaveFor(string scene)
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Notebook.ShowActivity("Cena indisponível", scene, "Esta cena precisa estar incluída no perfil de compilação.");
                return;
            }
            var previousScene = progress.scene;
            progress.scene = scene == caseDefinition.menuScene ? caseDefinition.archiveScene : scene;
            progress.Record("scene_requested", scene);
            if (!SaveProgress()) { progress.scene = previousScene; return; }
            leaving = true;
            interaction.SetBlocked(true);
            SceneManager.LoadSceneAsync(scene);
        }

        void OnApplicationPause(bool paused) { if (paused && !leaving) SaveProgress(); }
        void OnApplicationQuit() { if (!leaving) SaveProgress(false); }
    }
}
