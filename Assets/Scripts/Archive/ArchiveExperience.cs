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
        float nextAutosave;
        bool leaving;
        bool HasReconstruction => progress.reconstructionViewed || progress.reconstructionSkipped;
        public ResearchSave Progress => progress;

        void Start()
        {
            progress = ResearchSaveStore.EnsureSession();
            progress.discoveredEvidence ??= new List<string>();
            foreach (var id in progress.evidence)
                if (!progress.discoveredEvidence.Contains(id)) progress.discoveredEvidence.Add(id);
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
            if (HasReconstruction && !progress.reportCompleted) OpenReport();
        }

        void Update()
        {
            if (progress == null || leaving) return;
            if (Application.isFocused) progress.archiveSeconds += Time.unscaledDeltaTime;
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
            view.Show("Sua primeira investigação", caseDefinition.title, caseDefinition.mission);
            view.AddChoice("Assumir a pesquisa", () => { progress.briefingRead = true; progress.Record("briefing_read"); SaveProgress(); view.Close(); RefreshHud(); });
        }

        public void OpenEvidence(HistoricalEvidence document)
        {
            if (document == null) return;
            progress.Record("document_opened", document.id);
            if (!progress.discoveredEvidence.Contains(document.id))
            {
                progress.discoveredEvidence.Add(document.id);
                progress.Record("evidence_discovered", document.id);
                if (!SaveProgress()) return;
            }
            RefreshHud();
            view.ShowEvidence(document);
            bool collected = progress.evidence.Contains(document.id);
            view.AddChoice(collected ? "Evidência registrada — voltar" : "Registrar evidência no caderno", () =>
            {
                if (!progress.evidence.Contains(document.id)) { progress.evidence.Add(document.id); progress.Record("evidence_recorded", document.id); }
                SaveProgress(); RefreshHud();
                view.Show("Anotação de pesquisa", document.title, document.keyFinding);
                view.AddChoice("Voltar à investigação", view.Close);
            });
            if (!string.IsNullOrWhiteSpace(document.sourceUrl)) view.AddChoice("Consultar fonte original", () => Application.OpenURL(document.sourceUrl));
        }

        public void OpenNotebook()
        {
            view.Show("Caderneta de pesquisa", caseDefinition.title, "Reexamine os objetos encontrados e registre as evidências que sustentam a investigação.\n\n" + (progress.formationUnlocked ? "Próximo capítulo liberado: Formação e envio da FEB." : "A exposição será construída a partir das suas descobertas."));
            foreach (var evidence in caseDefinition.evidence)
            {
                var item = evidence;
                string state = progress.evidence.Contains(item.id) ? "Registrada" : progress.discoveredEvidence.Contains(item.id) ? "Encontrada" : "Não encontrada";
                view.AddChoice(item.title + " · " + state, () => OpenEvidence(item), progress.discoveredEvidence.Contains(item.id));
            }
            view.AddChoice("Organizar a linha do tempo", OpenChronology);
            view.AddChoice("Examinar a reconstrução", OpenReconstruction);
            view.AddChoice("Relatório da investigação", OpenReport);
        }

        public void OpenChronology()
        {
            if (!caseDefinition.evidence.All(d => progress.evidence.Contains(d.id)))
            {
                view.Show("Linha do tempo", "Evidências pendentes", "Consulte e registre os três documentos na sala. Procure as datas e o que cada fonte permite concluir.");
                view.AddChoice("Continuar a pesquisa", view.Close); return;
            }
            if (progress.chronologySolved)
            {
                view.Show("Cronologia estabelecida", "1942 — 1943", caseDefinition.chronologyExplanation);
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
            progress.chronologyAttempts++;
            bool correct = orderedEvidence.SequenceEqual(orderedEvidence.OrderBy(d => d.chronologyOrder));
            progress.Record("chronology_answer", "atlantic", string.Join(",", orderedEvidence.Select(d => d.id)) + ";correct=" + correct);
            progress.chronologySolved = correct;
            SaveProgress(); RefreshHud();
            view.Show(correct ? "Cronologia estabelecida" : "Reveja as datas", correct ? "Reconstrução disponível" : "Você pode consultar o caderno", correct ? caseDefinition.chronologyExplanation : "Compare a data do jornal, a declaração do estado de guerra e o registro do Itapagé. Uma notícia sobre ataques não é o mesmo acontecimento que a decisão política de entrar na guerra.");
            view.AddChoice(correct ? "Examinar a reconstrução" : "Tentar novamente", correct ? (Action)OpenReconstruction : OpenChronology);
            view.AddChoice("Voltar ao arquivo", view.Close);
        }

        public void OpenReconstruction()
        {
            view.Show("O caso Itapagé", "Litoral de Alagoas • 26 de setembro de 1943", progress.chronologySolved ? caseDefinition.reconstructionIntroduction + "\n\nDuração: 3 minutos. Segure Esc por 2 segundos para voltar ao arquivo." : "Organize a linha do tempo antes de examinar este caso. Isso permite situar o naufrágio depois da entrada do Brasil na guerra.");
            view.AddChoice("Examinar reconstrução", () => LeaveFor(caseDefinition.reconstructionScene), progress.chronologySolved);
        }

        public void OpenReport()
        {
            if (!HasReconstruction)
            {
                view.Show("Relatório de pesquisa", "Investigação em andamento", "Registre as evidências, organize a cronologia e examine a reconstrução para concluir o dossiê.");
                view.AddChoice("Voltar ao arquivo", view.Close); return;
            }
            if (progress.reportCompleted) { ShowCompletedReport(); return; }
            view.Show("Sua conclusão", "Relacione os documentos ao caso investigado", caseDefinition.reportQuestion);
            for (int i = 0; i < caseDefinition.reportAnswers.Length; i++)
            {
                int answer = i;
                view.AddChoice(caseDefinition.reportAnswers[i], () => SubmitReport(answer));
            }
        }

        void SubmitReport(int answer)
        {
            progress.reportAttempts++;
            bool correct = answer == caseDefinition.correctReportAnswer;
            progress.Record("report_answer", "atlantic", answer + ";correct=" + correct);
            if (correct) { progress.reportCompleted = progress.formationUnlocked = true; SaveProgress(); RefreshHud(); ShowCompletedReport(); }
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
            view.Show("Dossiê concluído", "Próximo capítulo: Formação e envio da FEB", caseDefinition.reportExplanation + $"\n\nEvidências: {progress.evidence.Count}/{caseDefinition.evidence.Length}   •   Cronologia: concluída   •   Interpretação: concluída\nTempo no arquivo: {TimeSpan.FromSeconds(progress.archiveSeconds):mm\\:ss}\nTentativas de cronologia: {progress.chronologyAttempts}   •   Tentativas de interpretação: {progress.reportAttempts}");
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
            string goal = !progress.briefingRead ? "Examine a pasta de pesquisa sobre a mesa." : progress.evidence.Count < caseDefinition.evidence.Length ? "Examine e registre os três documentos da sala." : !progress.chronologySolved ? "Organize os acontecimentos no painel da linha do tempo." : !HasReconstruction ? "Examine o caso Itapagé na mesa de reconstrução." : !progress.reportCompleted ? "Conclua o relatório no caderno de pesquisa." : "Dossiê concluído. Explore o arquivo ou volte ao menu.";
            int found = caseDefinition.evidence.Count(d => progress.discoveredEvidence.Contains(d.id));
            int registered = caseDefinition.evidence.Count(d => progress.evidence.Contains(d.id));
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
