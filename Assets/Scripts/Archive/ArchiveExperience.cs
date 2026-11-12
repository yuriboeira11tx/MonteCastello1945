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
        enum QuestionRound { Sources, Report }
        const int RoomRevision = 1;

        [SerializeField] ArchiveCaseDefinition caseDefinition;
        [SerializeField] ArchivePlayerInteraction interaction;
        [SerializeField] Transform player;
        [Header("Maquetes da mesa de reconstrução")]
        [SerializeField] GameObject shipExhibit;
        [SerializeField] GameObject campExhibit;
        [SerializeField] GameObject campEvidenceGroup;
        [SerializeField] ArchiveChapterTransition chapterTransition;
        [Header("Quadro da exposição")]
        [SerializeField] ArchiveEvidenceBoard board;
        ResearchSave progress;
        DossierProgress dossier;
        readonly List<ArchiveCaseDefinition> cases = new List<ArchiveCaseDefinition>();
        float nextAutosave;
        bool leaving;
        bool HasReconstruction => dossier.reconstructionViewed;
        bool NeedsSourceLink => caseDefinition.boardSources.Length > 0 && !dossier.boardSourcesLinked && !dossier.reportCompleted;
        bool FieldVisitReady => dossier.chronologySolved || caseDefinition.boardThemes.Length == 0 ||
            progress.events.Exists(e => e.action == "scene_requested" && e.subject == caseDefinition.reconstructionScene) ||
            (caseDefinition.reconstructionScene == "Acampamento" && progress.campArrivalCompleted);
        bool AllRegistered => caseDefinition.evidence.Length > 0 &&
            caseDefinition.evidence.All(item => dossier.evidence.Contains(item.id));
        FieldNotebook Notebook => interaction.Notebook;
        public ResearchSave Progress => progress;
        public string CurrentObjective
        {
            get
            {
                if (dossier == null) return string.Empty;
                if (caseDefinition.fieldStudy)
                {
                    if (!AllRegistered) return $"Localize e registre as {caseDefinition.evidence.Length} fotografias do acampamento no escritório.";
                    if (FieldVisitReady) return "Vá à mesa da tenda e da M1 Garand para visitar o acampamento.";
                    return dossier.chronologyOrdered ? "Responda às perguntas sobre as fotografias na caderneta."
                        : "No quadro da exposição, associe cada fotografia ao seu tema.";
                }
                if (!dossier.briefingRead && dossier.evidence.Count == 0) return "Examine a pasta de pesquisa sobre a mesa.";
                if (!AllRegistered) return "Leia e registre os documentos deste dossiê.";
                if (!dossier.chronologySolved) return dossier.chronologyOrdered
                    ? "Responda às perguntas sobre as fontes na caderneta."
                    : "Fixe os acontecimentos em ordem no quadro da exposição.";
                if (!HasReconstruction) return "Conclua a atividade na mesa de reconstrução.";
                if (NeedsSourceLink) return "Volte ao quadro da exposição e ligue as fontes que sustentam a legenda.";
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
            string savedCaseId = progress.activeCaseId;
            foreach (var item in cases)
            {
                SelectChapter(item);
                if (!dossier.reconstructionViewed || !dossier.reportCompleted) break;
            }
            RefreshExhibits();
            if (progress.hasPlayerPosition && progress.archiveRoomRevision == RoomRevision)
            {
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.SetPositionAndRotation(progress.playerPosition, Quaternion.Euler(0, progress.playerYaw, 0));
                controller.enabled = true;
            }
            interaction.SetBlocked(false);
            nextAutosave = Time.unscaledTime + 30;
            if (caseDefinition != cases[0] && savedCaseId != caseDefinition.Id) SaveProgress();
            if (!caseDefinition.fieldStudy && HasReconstruction && !NeedsSourceLink && !dossier.reportCompleted) OpenReport();
        }

        void SelectChapter(ArchiveCaseDefinition chapter)
        {
            caseDefinition = chapter;
            dossier = progress.SelectCase(chapter.Id);
            dossier.fieldStudyTitle = chapter.fieldStudy ? chapter.title : null;
            progress.archiveScene = progress.scene = chapter.archiveScene;
        }

        void RefreshExhibits()
        {
            bool showCamp = caseDefinition != cases[0];
            if (shipExhibit != null) shipExhibit.SetActive(!showCamp);
            if (campExhibit != null) campExhibit.SetActive(showCamp);
            if (campEvidenceGroup != null) campEvidenceGroup.SetActive(caseDefinition.fieldStudy);
            RefreshBoard();
        }

        void RefreshBoard() => board?.SetExhibit(caseDefinition, dossier.evidence,
            dossier.chronologyOrdered, dossier.boardSourcesLinked);

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
                case ArchiveAction.Chronology: ExamineBoard(); break;
                case ArchiveAction.Reconstruction: OpenReconstruction(atTable: true); break;
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

        void OpenEvidence(HistoricalEvidence document)
        {
            if (document == null) return;
            if (!caseDefinition.evidence.Contains(document))
            {
                if (CanReexamine(document))
                {
                    interaction.OpenInspection(document, true, null, true);
                    return;
                }
                Notebook.ShowActivity("Documento de outro dossiê", document.title, "Esta fonte pertence a outra investigação.");
                Notebook.AddChoice("Voltar ao índice", OpenNotebook);
                return;
            }
            progress.Record("document_opened", document.id);
            if (!SaveEvidenceStep(dossier.discoveredEvidence, document.id, "evidence_discovered", showError: true)) return;
            interaction.OpenInspection(document, dossier.evidence.Contains(document.id),
                () =>
                {
                    if (!SaveEvidenceStep(dossier.evidence, document.id, "evidence_recorded")) return false;
                    RefreshBoard();
                    return true;
                },
                dossier.readEvidence.Contains(document.id),
                () => SaveEvidenceStep(dossier.readEvidence, document.id, "document_read"));
        }

        bool CanReexamine(HistoricalEvidence document) => cases.Any(item =>
            item.evidence.Contains(document) && progress.dossiers.Exists(d => d.caseId == item.Id && d.evidence.Contains(document.id)));

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
        public void OpenNotebook() => Notebook.ShowIndex();

        void ExamineBoard()
        {
            if (!AllRegistered)
            {
                Notebook.ShowActivity("Quadro da exposição", "Fontes por registrar", CurrentObjective + "\n\n" + EvidenceCounts);
                Notebook.AddChoice("Continuar a pesquisa", Notebook.CloseBook);
                return;
            }
            if (!dossier.chronologyOrdered)
            {
                BeginBoardTask(caseDefinition.fieldStudy ? ArchiveBoardMode.Camp : ArchiveBoardMode.Chronology);
                return;
            }
            if (!dossier.chronologySolved)
            {
                OpenSourceQuestions();
                return;
            }
            if (!caseDefinition.fieldStudy && HasReconstruction && NeedsSourceLink)
                BeginBoardTask(ArchiveBoardMode.Sources);
            else if (!caseDefinition.fieldStudy && HasReconstruction)
                OpenReport();
            else
                OpenReconstruction();
        }

        void BeginBoardTask(ArchiveBoardMode mode)
        {
            if (board == null)
            {
                Debug.LogError("Associe o quadro da exposição ao ArchiveExperience na cena.", this);
                return;
            }
            interaction.SetBlocked(true);
            board.Show(mode, caseDefinition, () => FinishBoardTask(mode),
                () => interaction.SetBlocked(false), () => SaveBoardAttempt(mode, false));
        }

        bool SaveBoardAttempt(ArchiveBoardMode mode, bool correct)
        {
            bool chronology = mode == ArchiveBoardMode.Chronology;
            string action = chronology ? "chronology_answer" : mode == ArchiveBoardMode.Camp ? "board_themes" : "board_sources";
            if (chronology) dossier.chronologyAttempts++;
            progress.Record(action, caseDefinition.Id, "correct=" + correct);
            if (SaveProgress(showError: correct)) return true;
            if (chronology) dossier.chronologyAttempts--;
            progress.events.RemoveAt(progress.events.Count - 1);
            return false;
        }

        void FinishBoardTask(ArchiveBoardMode mode)
        {
            if (mode == ArchiveBoardMode.Sources)
            {
                dossier.boardSourcesLinked = true;
                if (!SaveBoardAttempt(mode, true)) { dossier.boardSourcesLinked = false; return; }
                RefreshBoard();
                OpenReport();
                return;
            }
            dossier.chronologyOrdered = true;
            dossier.chronologySolved = caseDefinition.chronologyQuestions.Length == 0;
            if (!SaveBoardAttempt(mode, true)) { dossier.chronologyOrdered = dossier.chronologySolved = false; return; }
            RefreshBoard();
            if (dossier.chronologySolved) OpenReconstruction();
            else OpenSourceQuestions();
        }

        public void OpenSourceQuestions()
        {
            if (dossier.chronologyOrdered && !dossier.chronologySolved) ShowQuestions(QuestionRound.Sources);
        }

        public void OpenReconstruction() => OpenReconstruction(atTable: false);

        void OpenReconstruction(bool atTable)
        {
            if (caseDefinition.fieldStudy)
            {
                bool canEnter = atTable && AllRegistered && FieldVisitReady;
                Notebook.ShowActivity("Visita de campo", caseDefinition.title,
                    canEnter ? caseDefinition.reconstructionIntroduction : CurrentObjective + "\n\n" + EvidenceCounts);
                if (canEnter)
                    Notebook.AddChoice("Entrar no acampamento", () => LeaveFor(caseDefinition.reconstructionScene));
                else Notebook.AddChoice("Voltar ao arquivo", Notebook.CloseBook);
                return;
            }
            if (HasReconstruction) { OpenReport(); return; }
            Notebook.ShowActivity("Reconstrução interativa", caseDefinition.title, dossier.chronologySolved ? caseDefinition.reconstructionIntroduction + "\n\nSegure Esc por 2 segundos para voltar ao arquivo. Sair antes de concluir mantém a atividade pendente." : CurrentObjective);
            Notebook.AddChoice("Examinar reconstrução", () => LeaveFor(caseDefinition.reconstructionScene), dossier.chronologySolved);
            if (dossier.chronologyOrdered && !dossier.chronologySolved) Notebook.AddChoice("Perguntas sobre as fontes", OpenSourceQuestions);
        }

        public void OpenReport()
        {
            if (caseDefinition.fieldStudy) { OpenReconstruction(); return; }
            if (!HasReconstruction || NeedsSourceLink)
            {
                Notebook.ShowActivity("Relatório de pesquisa", "Investigação em andamento", CurrentObjective);
                Notebook.AddChoice("Voltar ao arquivo", Notebook.CloseBook);
                return;
            }
            if (dossier.reportCompleted) { ShowCompletedReport(); return; }
            ShowQuestions(QuestionRound.Report);
        }

        void ShowQuestions(QuestionRound round)
        {
            if (!caseDefinition.QuestionsAreValid())
            {
                Debug.LogError($"Confira os identificadores, enunciados, alternativas, gabaritos e explicações em {caseDefinition.name}.", caseDefinition);
                Notebook.ShowActivity("Caderneta de campo", "Perguntas indisponíveis", "O questionário deste dossiê está incompleto.");
                Notebook.AddChoice("Voltar ao acervo", Notebook.CloseBook);
                return;
            }
            var questions = round == QuestionRound.Report ? caseDefinition.reportQuestions : caseDefinition.chronologyQuestions;
            int answered = questions.Count(q => dossier.answeredQuestions.Contains(q.id));
            string title = round == QuestionRound.Report ? "Relatório da reconstrução" : "Perguntas sobre as fontes";
            if (answered == questions.Length)
            {
                if (round == QuestionRound.Report)
                {
                    Notebook.ShowActivity(title, "Interpretação concluída", caseDefinition.reportExplanation);
                    Notebook.AddChoice("Concluir dossiê", CompleteReport);
                }
                else
                {
                    dossier.chronologySolved = true;
                    if (SaveProgress()) OpenReconstruction();
                    else dossier.chronologySolved = false;
                }
                return;
            }
            var question = questions.First(q => !dossier.answeredQuestions.Contains(q.id));
            Notebook.ShowActivity(title, $"Pergunta {answered + 1} de {questions.Length}", question.prompt);
            for (int i = 0; i < question.answers.Length; i++)
            {
                int answer = i;
                Notebook.AddChoice(question.answers[i], () => SubmitAnswer(round, question, answer));
            }
        }

        void SubmitAnswer(QuestionRound round, ArchiveQuestion question, int answer)
        {
            bool report = round == QuestionRound.Report;
            if (leaving || dossier.reportCompleted || dossier.answeredQuestions.Contains(question.id)) return;
            if (report ? caseDefinition.fieldStudy || !HasReconstruction || NeedsSourceLink : !dossier.chronologyOrdered || dossier.chronologySolved) return;
            bool correct = answer == question.correctAnswer;
            if (report) dossier.reportAttempts++;
            progress.Record(report ? "report_answer" : "source_answer", caseDefinition.Id, $"{question.id};answer={answer};correct={correct}");
            if (correct) dossier.answeredQuestions.Add(question.id);
            var questions = report ? caseDefinition.reportQuestions : caseDefinition.chronologyQuestions;
            bool finished = correct && questions.All(q => dossier.answeredQuestions.Contains(q.id));
            if (finished && !report) dossier.chronologySolved = true;
            if (!SaveProgress())
            {
                if (correct) dossier.answeredQuestions.Remove(question.id);
                if (finished && !report) dossier.chronologySolved = false;
                if (report) dossier.reportAttempts--;
                progress.events.RemoveAt(progress.events.Count - 1);
                return;
            }
            Notebook.ShowActivity(report ? "Relatório da reconstrução" : "Perguntas sobre as fontes",
                correct ? "Resposta correta" : "Reveja as evidências", question.explanation);
            if (finished && report) Notebook.AddChoice("Concluir dossiê", CompleteReport);
            else if (finished) Notebook.AddChoice(caseDefinition.fieldStudy ? "Preparar visita de campo" : "Examinar a reconstrução", OpenReconstruction);
            else Notebook.AddChoice(correct ? "Próxima pergunta" : "Tentar novamente", () => ShowQuestions(round));
            Notebook.AddChoice("Voltar aos documentos no acervo", Notebook.CloseBook);
        }

        void CompleteReport()
        {
            if (leaving || !HasReconstruction || NeedsSourceLink || dossier.reportCompleted || caseDefinition.fieldStudy ||
                !caseDefinition.QuestionsAreValid() || !caseDefinition.reportQuestions.All(q => dossier.answeredQuestions.Contains(q.id))) return;
            if (caseDefinition.nextCase != null) { AdvanceChapter(); return; }
            dossier.reportCompleted = true;
            progress.Record("report_completed", caseDefinition.Id);
            if (SaveProgress()) ShowCompletedReport();
            else
            {
                dossier.reportCompleted = false;
                progress.events.RemoveAt(progress.events.Count - 1);
            }
        }

        void AdvanceChapter()
        {
            var previous = caseDefinition;
            var previousDossier = dossier;
            var next = previous.nextCase;
            bool advanced = false;
            leaving = true;
            Notebook.StoreForTransition();
            interaction.SetBlocked(true);

            void Covered()
            {
                int eventCount = progress.events.Count;
                bool newDossier = !progress.dossiers.Exists(item => item.caseId == next.Id);
                previousDossier.reportCompleted = true;
                progress.Record("report_completed", previous.Id);
                SelectChapter(next);
                progress.Record("chapter_started", next.Id);
                advanced = SaveProgress(false);
                if (!advanced)
                {
                    if (newDossier) progress.dossiers.Remove(dossier);
                    previousDossier.reportCompleted = false;
                    progress.events.RemoveRange(eventCount, progress.events.Count - eventCount);
                    SelectChapter(previous);
                }
                RefreshExhibits();
            }

            void Finished()
            {
                leaving = false;
                interaction.SetBlocked(false);
                nextAutosave = Time.unscaledTime + 30;
                if (advanced) return;
                Notebook.ShowActivity("Progresso não salvo", "A mudança de fase ficou pendente.",
                    "Verifique o espaço disponível no computador e tente concluir o relatório novamente.");
                Notebook.AddChoice("Abrir relatório", OpenReport);
            }

            if (chapterTransition != null) chapterTransition.Play(next.title, Covered, Finished);
            else { Covered(); Finished(); }
        }

        void ShowCompletedReport()
        {
            Notebook.ShowActivity("Dossiê concluído", caseDefinition.title, caseDefinition.reportExplanation + $"\n\n{EvidenceCounts}\nCronologia e interpretação concluídas.\nTempo no arquivo {TimeSpan.FromSeconds(dossier.archiveSeconds):mm\\:ss}\nTentativas de cronologia {dossier.chronologyAttempts}   ·   Tentativas de interpretação {dossier.reportAttempts}");
            if (!string.IsNullOrWhiteSpace(caseDefinition.followUpScene))
                Notebook.AddChoice("Entrar na experiência deste dossiê", () => LeaveFor(caseDefinition.followUpScene));
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
            progress.archiveRoomRevision = RoomRevision;
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
