using System;
using System.Collections;
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
        [SerializeField] ArchiveView view;
        [SerializeField] ArchivePlayerInteraction interaction;
        [SerializeField] Transform player;
        readonly List<HistoricalEvidence> orderedEvidence = new List<HistoricalEvidence>();
        ResearchSave progress;
        DossierProgress dossier;
        readonly List<ArchiveCaseDefinition> cases = new List<ArchiveCaseDefinition>();
        float nextAutosave;
        bool leaving;
        bool HasReconstruction => dossier.reconstructionViewed;
        public ResearchSave Progress => progress;

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
            view.VisibilityChanged += interaction.SetBlocked;
            interaction.SetBlocked(false);
            nextAutosave = Time.unscaledTime + 30;
            RefreshHud();
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
            view.Show("Pasta de pesquisa", caseDefinition.title, caseDefinition.mission);
            view.AddChoice("Assumir a pesquisa", () => { dossier.briefingRead = true; progress.Record("briefing_read"); SaveProgress(); view.Close(); RefreshHud(); });
        }

        public void OpenEvidence(HistoricalEvidence document)
        {
            if (document == null) return;
            if (!caseDefinition.evidence.Contains(document))
            {
                view.Show("Documento de outro dossiê", document.title, "Selecione o dossiê correspondente na caderneta para registrar esta fonte.");
                view.AddChoice("Selecionar dossiê", OpenCaseSelection);
                return;
            }
            progress.Record("document_opened", document.id);
            if (!dossier.discoveredEvidence.Contains(document.id))
            {
                dossier.discoveredEvidence.Add(document.id);
                progress.Record("evidence_discovered", document.id);
                if (!SaveProgress()) return;
            }
            RefreshHud();
            view.ShowEvidence(document);
            bool collected = dossier.evidence.Contains(document.id);
            view.AddChoice("Ler documento no arquivo", () => view.ReadDocument(document, () =>
            {
                if (!dossier.readEvidence.Contains(document.id)) dossier.readEvidence.Add(document.id);
                progress.Record("document_read", document.id);
                SaveProgress();
                OpenEvidence(document);
            }));
            view.AddChoice(collected ? "Evidência registrada — voltar" : "Registrar evidência no caderno", () =>
            {
                if (!dossier.evidence.Contains(document.id)) { dossier.evidence.Add(document.id); progress.Record("evidence_recorded", document.id); }
                SaveProgress(); RefreshHud();
                view.Show("Anotação de pesquisa", document.title, document.keyFinding);
                view.AddChoice("Voltar à investigação", view.Close);
            }, dossier.readEvidence.Contains(document.id));
        }

        public void OpenNotebook()
        {
            view.Show("Caderneta de pesquisa", caseDefinition.title, "Reexamine os objetos encontrados, leia as fontes e registre as evidências que sustentam a investigação.\n\nCada dossiê reúne documentos, cronologia, reconstrução e uma conclusão.");
            foreach (var evidence in caseDefinition.evidence)
            {
                var item = evidence;
                string state = dossier.evidence.Contains(item.id) ? "Registrada" : dossier.discoveredEvidence.Contains(item.id) ? "Encontrada" : "Não encontrada";
                view.AddChoice(item.title + " · " + state, () => OpenEvidence(item), dossier.discoveredEvidence.Contains(item.id));
            }
            view.AddChoice("Organizar a linha do tempo", OpenChronology);
            view.AddChoice("Examinar a reconstrução", OpenReconstruction);
            view.AddChoice("Relatório da investigação", OpenReport);
            if (cases.Count > 1) view.AddChoice("Selecionar dossiê", OpenCaseSelection);
        }

        public void OpenCaseSelection()
        {
            view.Show("Dossiês do arquivo", "Investigações e experiências", "Conclua uma investigação para liberar a seguinte. As evidências e conclusões de cada dossiê ficam preservadas.");
            for (int i = 0; i < cases.Count; i++)
            {
                var item = cases[i];
                var previous = i > 0 ? cases[i - 1] : null;
                var state = previous == null ? null : progress.dossiers.Find(d => d.caseId == previous.Id);
                bool available = previous == null || (state != null && state.reportCompleted &&
                    (string.IsNullOrWhiteSpace(previous.followUpScene) || state.followUpCompleted));
                view.AddChoice(item.title + (available ? "" : " · Bloqueado"), () =>
                {
                    caseDefinition = item;
                    dossier = progress.SelectCase(item.Id);
                    progress.archiveScene = item.archiveScene;
                    orderedEvidence.Clear();
                    SaveProgress(); RefreshHud(); OpenBriefing();
                }, available);
            }
        }

        public void OpenChronology()
        {
            if (!caseDefinition.evidence.All(d => dossier.evidence.Contains(d.id)))
            {
                view.Show("Linha do tempo", "Evidências pendentes", "Consulte e registre todos os documentos deste dossiê. Procure as datas e o que cada fonte permite concluir.");
                view.AddChoice("Continuar a pesquisa", view.Close); return;
            }
            if (dossier.chronologySolved)
            {
                view.Show("Cronologia estabelecida", caseDefinition.title, caseDefinition.chronologyExplanation);
                view.AddChoice("Examinar a reconstrução", OpenReconstruction); return;
            }
            orderedEvidence.Clear();
            ShowChronologyChoices();
        }

        void ShowChronologyChoices()
        {
            var selected = string.Join("\n", orderedEvidence.Select((d, i) => $"{i + 1}. {d.chronologyLabel}"));
            view.Show("Organize os acontecimentos", "Do mais antigo ao mais recente", caseDefinition.chronologyIntroduction + "\n\n" + selected);
            foreach (var document in caseDefinition.evidence.OrderByDescending(d => d.chronologyOrder))
            {
                var item = document;
                view.AddChoice(item.chronologyLabel, () => { orderedEvidence.Add(item); if (orderedEvidence.Count == caseDefinition.evidence.Length) CheckChronology(); else ShowChronologyChoices(); }, !orderedEvidence.Contains(item));
            }
            view.AddChoice("Recomeçar a ordenação", () => { orderedEvidence.Clear(); ShowChronologyChoices(); });
        }

        void CheckChronology()
        {
            dossier.chronologyAttempts++;
            bool correct = orderedEvidence.SequenceEqual(orderedEvidence.OrderBy(d => d.chronologyOrder));
            progress.Record("chronology_answer", caseDefinition.Id, string.Join(",", orderedEvidence.Select(d => d.id)) + ";correct=" + correct);
            dossier.chronologySolved = correct;
            SaveProgress(); RefreshHud();
            view.Show(correct ? "Cronologia estabelecida" : "Reveja as datas", correct ? "Reconstrução disponível" : "Você pode consultar o caderno", correct ? caseDefinition.chronologyExplanation : "Compare as datas e diferencie os acontecimentos apresentados nas fontes. Consulte os documentos e tente novamente.");
            view.AddChoice(correct ? "Examinar a reconstrução" : "Tentar novamente", correct ? (Action)OpenReconstruction : OpenChronology);
            view.AddChoice("Voltar ao arquivo", view.Close);
        }

        public void OpenReconstruction()
        {
            view.Show("Reconstrução interativa", caseDefinition.title, dossier.chronologySolved ? caseDefinition.reconstructionIntroduction + "\n\nSegure Esc por 2 segundos para voltar ao arquivo. Sair antes de concluir mantém a atividade pendente." : "Registre as fontes e organize a linha do tempo antes de examinar a reconstrução deste caso.");
            view.AddChoice("Examinar reconstrução", () => LeaveFor(caseDefinition.reconstructionScene), dossier.chronologySolved);
        }

        public void OpenReport()
        {
            if (!HasReconstruction)
            {
                view.Show("Relatório de pesquisa", "Investigação em andamento", "Registre as evidências, organize a cronologia e examine a reconstrução para concluir o dossiê.");
                view.AddChoice("Voltar ao arquivo", view.Close); return;
            }
            if (dossier.reportCompleted) { ShowCompletedReport(); return; }
            view.Show("Sua conclusão", "Relacione os documentos ao caso investigado", caseDefinition.reportQuestion);
            for (int i = 0; i < caseDefinition.reportAnswers.Length; i++)
            {
                int answer = i;
                view.AddChoice(caseDefinition.reportAnswers[i], () => SubmitReport(answer));
            }
        }

        void SubmitReport(int answer)
        {
            dossier.reportAttempts++;
            bool correct = answer == caseDefinition.correctReportAnswer;
            progress.Record("report_answer", caseDefinition.Id, answer + ";correct=" + correct);
            if (correct) { dossier.reportCompleted = true; SaveProgress(); RefreshHud(); ShowCompletedReport(); }
            else
            {
                SaveProgress();
                view.Show("Retome as evidências", "A conclusão precisa respeitar a cronologia", caseDefinition.reportExplanation);
                view.AddChoice("Revisar minha conclusão", OpenReport);
                view.AddChoice("Consultar o caderno", OpenNotebook);
            }
        }

        void ShowCompletedReport()
        {
            view.Show("Dossiê concluído", caseDefinition.title, caseDefinition.reportExplanation + $"\n\nEvidências: {dossier.evidence.Count}/{caseDefinition.evidence.Length}   •   Cronologia: concluída   •   Interpretação: concluída\nTempo no arquivo: {TimeSpan.FromSeconds(dossier.archiveSeconds):mm\\:ss}\nTentativas de cronologia: {dossier.chronologyAttempts}   •   Tentativas de interpretação: {dossier.reportAttempts}");
            if (!string.IsNullOrWhiteSpace(caseDefinition.followUpScene))
                view.AddChoice("Entrar na experiência deste dossiê", () => LeaveFor(caseDefinition.followUpScene));
            if (caseDefinition.nextCase != null) view.AddChoice("Próximo dossiê", OpenCaseSelection);
            view.AddChoice("Continuar explorando o arquivo", view.Close);
            view.AddChoice("Salvar e voltar ao menu", () => LeaveFor(caseDefinition.menuScene));
        }

        public void OpenPause()
        {
            view.Show("Pesquisa em andamento", caseDefinition.title, "O progresso é salvo ao registrar evidências e concluir atividades.");
            view.AddChoice("Retomar", view.Close);
            view.AddChoice("Caderno de pesquisa", OpenNotebook);
            view.AddChoice("Salvar e voltar ao menu", () => LeaveFor(caseDefinition.menuScene));
        }

        void RefreshHud()
        {
            string goal = !dossier.briefingRead ? "Examine a pasta de pesquisa sobre a mesa." : dossier.evidence.Count < caseDefinition.evidence.Length ? "Leia e registre os documentos deste dossiê." : !dossier.chronologySolved ? "Organize os acontecimentos no painel da linha do tempo." : !HasReconstruction ? "Conclua a atividade na mesa de reconstrução." : !dossier.reportCompleted ? "Conclua o relatório no caderno de pesquisa." : "Dossiê concluído. Consulte as experiências e dossiês na caderneta.";
            int found = caseDefinition.evidence.Count(d => dossier.discoveredEvidence.Contains(d.id));
            int registered = caseDefinition.evidence.Count(d => dossier.evidence.Contains(d.id));
            view.SetHud("OBJETIVO ATUAL\n" + goal, $"Encontradas: {found}/{caseDefinition.evidence.Length}     Registradas: {registered}/{caseDefinition.evidence.Length}");
        }

        bool SaveProgress()
        {
            if (progress == null) return true;
            progress.hasPlayerPosition = true;
            progress.playerPosition = player.position;
            progress.playerYaw = player.eulerAngles.y;
            if (ResearchSaveStore.Save(out var error)) return true;
            view.Show("Progresso não salvo", "Verifique o espaço disponível no computador", error);
            return false;
        }

        void LeaveFor(string scene)
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(scene)) { view.Show("Cena indisponível", scene, "Esta cena precisa estar incluída no perfil de compilação."); return; }
            var previousScene = progress.scene;
            progress.scene = scene == caseDefinition.menuScene ? caseDefinition.archiveScene : scene;
            progress.Record("scene_requested", scene);
            if (!SaveProgress()) { progress.scene = previousScene; return; }
            leaving = true;
            view.Close();
            interaction.SetBlocked(true);
            StartCoroutine(LoadScene(scene));
        }

        IEnumerator LoadScene(string scene)
        {
            yield return view.FadeToBlack(.75f);
            yield return SceneManager.LoadSceneAsync(scene);
        }

        void OnApplicationPause(bool paused) { if (paused && !leaving) SaveProgress(); }
        void OnApplicationQuit() { if (!leaving) SaveProgress(); }
        void OnDestroy() { if (view != null) view.VisibilityChanged -= interaction.SetBlocked; }
    }
}
