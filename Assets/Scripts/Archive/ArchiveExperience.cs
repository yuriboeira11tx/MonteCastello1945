using System;
using System.Collections.Generic;
using System.Linq;
using FEB.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    public sealed class ArchiveExperience : MonoBehaviour
    {
        [SerializeField] ArchiveCaseDefinition caseDefinition;
        [SerializeField] ArchivePlayerInteraction interaction;
        [SerializeField] Transform player;
        [Header("Maquetes da mesa de reconstrução")]
        [SerializeField] GameObject shipExhibit;
        [SerializeField] GameObject campExhibit;
        [SerializeField] GameObject campEvidenceGroup;
        [SerializeField] ArchiveChapterTransition chapterTransition;
        [SerializeField] UIDocument boardTitle, boardInstructions;
        readonly List<HistoricalEvidence> orderedEvidence = new List<HistoricalEvidence>();
        ResearchSave progress;
        DossierProgress dossier;
        readonly List<ArchiveCaseDefinition> cases = new List<ArchiveCaseDefinition>();
        float nextAutosave;
        bool leaving;
        bool HasReconstruction => dossier.reconstructionViewed;
        bool AllRegistered => caseDefinition.evidence.Length > 0 &&
            caseDefinition.evidence.All(item => dossier.evidence.Contains(item.id));
        FieldNotebook Notebook => interaction.Notebook;
        public ResearchSave Progress => progress;
        public string CurrentObjective
        {
            get
            {
                if (dossier == null) return string.Empty;
                if (caseDefinition.fieldStudy) return AllRegistered
                    ? "Vá à mesa da maquete com a tenda e a M1 Garand para entrar no acampamento."
                    : $"Examine os {caseDefinition.evidence.Length} documentos na mesa junto ao decreto. Leia cada ficha e registre a evidência na caderneta.";
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
            string savedCaseId = progress.activeCaseId;
            foreach (var item in cases)
            {
                SelectChapter(item);
                if (!dossier.reconstructionViewed || !dossier.reportCompleted) break;
            }
            RefreshExhibits();
            if (progress.hasPlayerPosition)
            {
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.SetPositionAndRotation(progress.playerPosition, Quaternion.Euler(0, progress.playerYaw, 0));
                controller.enabled = true;
            }
            interaction.SetBlocked(false);
            nextAutosave = Time.unscaledTime + 30;
            if (caseDefinition != cases[0] && savedCaseId != caseDefinition.Id) SaveProgress();
            if (!caseDefinition.fieldStudy && HasReconstruction && !dossier.reportCompleted) OpenReport();
        }

        void SelectChapter(ArchiveCaseDefinition chapter)
        {
            caseDefinition = chapter;
            dossier = progress.SelectCase(chapter.Id);
            dossier.fieldStudyTitle = chapter.fieldStudy ? chapter.title : null;
            progress.archiveScene = progress.scene = chapter.archiveScene;
            orderedEvidence.Clear();
        }

        void RefreshExhibits()
        {
            bool showCamp = progress.dossiers.Find(item => item.caseId == cases[0].Id)?.reconstructionViewed == true;
            if (shipExhibit != null) shipExhibit.SetActive(!showCamp);
            if (campExhibit != null) campExhibit.SetActive(showCamp);
            if (campEvidenceGroup != null) campEvidenceGroup.SetActive(caseDefinition.fieldStudy);
            RefreshFieldBoard();
        }

        void RefreshFieldBoard()
        {
            if (!caseDefinition.fieldStudy) return;
            var title = boardTitle != null ? boardTitle.rootVisualElement?.Q<Label>("text") : null;
            var instructions = boardInstructions != null ? boardInstructions.rootVisualElement?.Q<Label>("text") : null;
            if (title != null) title.text = "ACAMPAMENTO DA FEB";
            if (instructions != null) instructions.text = AllRegistered
                ? "Fontes registradas.\nExamine a mesa da tenda e da M1 Garand.\nTAB — Abrir caderneta"
                : "Leia as três fontes na mesa do decreto.\nAbrigo · Rancho · Cotidiano\nTAB — Abrir caderneta";
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

        public void OpenEvidence(HistoricalEvidence document, bool fromNotebook = false)
        {
            if (document == null) return;
            Action onClosed = fromNotebook ? () => Notebook.ShowEvidenceIndex(document) : (Action)null;
            if (!caseDefinition.evidence.Contains(document))
            {
                if (CanReexamine(document))
                {
                    interaction.OpenInspection(document, true, null, true, onClosed: onClosed);
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
                    RefreshFieldBoard();
                    return true;
                },
                dossier.readEvidence.Contains(document.id),
                () => SaveEvidenceStep(dossier.readEvidence, document.id, "document_read"),
                onClosed);
        }

        public bool CanReexamine(HistoricalEvidence document) => cases.Any(item =>
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

        public void OpenChronology()
        {
            if (caseDefinition.fieldStudy) { OpenReconstruction(); return; }
            if (!caseDefinition.evidence.All(d => dossier.evidence.Contains(d.id)))
            {
                Notebook.ShowActivity("Linha do tempo", "Evidências pendentes", "Consulte e registre todos os documentos deste dossiê. Procure as datas e o que cada fonte permite concluir.");
                Notebook.AddChoice("Continuar a pesquisa", Notebook.CloseBook);
                return;
            }
            if (dossier.chronologySolved)
            {
                Notebook.ShowActivity("Cronologia estabelecida", caseDefinition.title, caseDefinition.chronologyExplanation);
                Notebook.AddChoice(HasReconstruction ? "Abrir relatório" : "Examinar a reconstrução", OpenReconstruction);
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

        public void OpenReconstruction() => OpenReconstruction(atTable: false);

        void OpenReconstruction(bool atTable)
        {
            if (caseDefinition.fieldStudy)
            {
                Notebook.ShowActivity("Visita de campo", caseDefinition.title,
                    atTable && AllRegistered ? caseDefinition.reconstructionIntroduction : CurrentObjective + "\n\n" + EvidenceCounts);
                if (atTable && AllRegistered)
                    Notebook.AddChoice("Entrar no acampamento", () => LeaveFor(caseDefinition.reconstructionScene));
                else Notebook.AddChoice("Voltar ao arquivo", Notebook.CloseBook);
                return;
            }
            if (HasReconstruction) { OpenReport(); return; }
            Notebook.ShowActivity("Reconstrução interativa", caseDefinition.title, dossier.chronologySolved ? caseDefinition.reconstructionIntroduction + "\n\nSegure Esc por 2 segundos para voltar ao arquivo. Sair antes de concluir mantém a atividade pendente." : "Registre as fontes e organize a linha do tempo antes de examinar a reconstrução deste caso.");
            Notebook.AddChoice("Examinar reconstrução", () => LeaveFor(caseDefinition.reconstructionScene), dossier.chronologySolved);
        }

        public void OpenReport()
        {
            if (caseDefinition.fieldStudy) { OpenReconstruction(); return; }
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
            if (leaving || !HasReconstruction || dossier.reportCompleted || caseDefinition.fieldStudy) return;
            bool correct = answer == caseDefinition.correctReportAnswer;
            if (correct && caseDefinition.nextCase != null)
            {
                AdvanceChapter(answer);
                return;
            }
            dossier.reportAttempts++;
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

        void AdvanceChapter(int answer)
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
                previousDossier.reportAttempts++;
                previousDossier.reportCompleted = true;
                progress.Record("report_answer", previous.Id, answer + ";correct=True");
                SelectChapter(next);
                progress.Record("chapter_started", next.Id);
                advanced = SaveProgress(false);
                if (!advanced)
                {
                    if (newDossier) progress.dossiers.Remove(dossier);
                    previousDossier.reportCompleted = false;
                    previousDossier.reportAttempts--;
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
