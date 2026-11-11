using System;
using System.Collections.Generic;
using System.Linq;
using FEB.Persistence;
using FEB.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    internal sealed class FieldNotebookPages
    {
        enum Section { Index, Reading, Notes, Activity, Subtitles }
        readonly VisualElement root;
        readonly ScrollView left, right;
        readonly Button[] tabs;
        readonly List<(Button button, Action action)> bindings = new List<(Button, Action)>();
        readonly List<(HistoricalEvidence doc, List<EvidenceNote> notes)> records = new List<(HistoricalEvidence, List<EvidenceNote>)>();
        readonly List<HistoricalEvidence> readings = new List<HistoricalEvidence>();
        readonly Texture2D photo, emblem;
        readonly ArchiveExperience archive;
        readonly Action pageChanged;
        readonly ResearchSave save;
        Section section;
        int record, reading, page;
        TextField editor;
        Label status;
        bool dirty;
        public bool IsPausePage { get; private set; }

        public FieldNotebookPages(VisualElement root, ArchiveCaseDefinition firstCase, HistoricalEvidence[] common,
            NotebookChapter[] chapters, string scene, Texture2D photo, Texture2D emblem, ArchiveExperience archive,
            Action close, Action pageChanged)
        {
            this.root = root; this.photo = photo; this.emblem = emblem;
            this.archive = archive;
            this.pageChanged = pageChanged;
            save = ResearchSaveStore.EnsureSession();
            if (save.dossiers.Count == 0 && firstCase != null) save.SelectCase(firstCase.Id);
            var physicalIds = CollectEvidence(firstCase, chapters);
            void AddReadings(HistoricalEvidence[] items)
            {
                if (items == null) return;
                foreach (var item in items)
                    if (item != null && item.inspectionPrefab == null && !physicalIds.Contains(item.id) && !readings.Contains(item))
                        readings.Add(item);
            }
            if (chapters != null)
                foreach (var chapter in chapters)
                {
                    if (chapter == null) continue;
                    if (chapter.scene == scene) AddReadings(chapter.readings);
                }
            AddReadings(common);
            left = root.Q<ScrollView>("leftContent"); right = root.Q<ScrollView>("rightContent");
            tabs = new[] { Bind("indexTab", () => Switch(Section.Index)), Bind("readingTab", () => Switch(Section.Reading)),
                Bind("notesTab", () => Switch(Section.Notes)) };
            Bind("previousPage", () => Turn(-1)); Bind("nextPage", () => Turn(1));
            Bind("closeBook", close).text = archive != null ? "Guardar · Tab" : "Guardar · Esc";
            Render();
        }

        HashSet<string> CollectEvidence(ArchiveCaseDefinition firstCase, NotebookChapter[] chapters)
        {
            var physicalIds = new HashSet<string>();
            var visited = new HashSet<ArchiveCaseDefinition>();
            for (var c = firstCase; c != null && visited.Add(c); c = c.nextCase)
            {
                if (c.evidence == null) continue;
                var dossier = save.dossiers.Find(d => d.caseId == c.Id);
                dossier?.Normalize();
                foreach (var doc in c.evidence)
                {
                    if (doc == null) continue;
                    physicalIds.Add(doc.id);
                    if (dossier != null && dossier.evidence.Contains(doc.id)) records.Add((doc, dossier.notes));
                }
            }
            if (chapters != null)
                foreach (var chapter in chapters)
                {
                    if (chapter?.fieldEvidence == null) continue;
                    foreach (var doc in chapter.fieldEvidence)
                    {
                        if (doc == null) continue;
                        physicalIds.Add(doc.id);
                        if (save.fieldEvidence.Contains(doc.id) && !records.Any(r => r.doc.id == doc.id))
                            records.Add((doc, save.fieldNotes));
                    }
                }
            return physicalIds;
        }

        Button Bind(string name, Action action)
        {
            var button = root.Q<Button>(name);
            button.clicked += action; bindings.Add((button, action)); return button;
        }

        public void Unbind()
        {
            foreach (var binding in bindings) binding.button.clicked -= binding.action;
            editor = null; status = null;
            left.Clear(); right.Clear();
        }

        void Switch(Section next)
        {
            if (!FlushNote() || (section == next && page == 0)) return;
            IsPausePage = false;
            section = next; page = 0; ShowChangedPage();
        }

        public void ShowIndex() => Switch(Section.Index);

        public void ShowActivity(string kicker, string title, string body, bool isPause, bool sound)
        {
            if (!FlushNote()) return;
            IsPausePage = isPause;
            section = Section.Activity;
            ClearPages();
            Titles(kicker, title, isPause ? "OPÇÕES" : "ARQUIVO HISTÓRICO");
            Text(right, body);
            if (isPause) ActionButton(left, "Legendas", () => Switch(Section.Subtitles));
            Footer(isPause ? "Opções da pesquisa" : "Dossiê em andamento");
            if (sound) pageChanged();
        }

        public void AddChoice(string text, Action action, bool enabled)
        {
            if (section == Section.Activity) ActionButton(left, text, action).SetEnabled(enabled);
        }

        void Select(int index)
        {
            int current = section == Section.Reading ? reading : record;
            if (!FlushNote() || (index == current && page == 0)) return;
            if (section == Section.Reading) reading = index; else record = index;
            page = 0; ShowChangedPage();
        }

        void Turn(int delta) { page += delta; ShowChangedPage(); }

        void ShowChangedPage()
        {
            Render();
            pageChanged();
        }

        static Label Text(VisualElement parent, string value, string style = "copy")
        {
            var label = new Label(value) { enableRichText = false };
            label.AddToClassList(style); parent.Add(label); return label;
        }

        static Button ActionButton(VisualElement parent, string name, Action action, string style = "action")
        {
            var b = new Button(action) { text = name };
            b.AddToClassList(style); parent.Add(b); return b;
        }

        static void Photo(VisualElement parent, Texture texture, string caption, string style = "photo", bool magnify = false)
        {
            if (texture == null) return;
            var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore };
            if (magnify)
            {
                var frame = new VisualElement();
                frame.AddToClassList(style);
                image.AddToClassList("plate-image");
                frame.Add(image);
                frame.AddManipulator(new FEB.UI.DocumentImageManipulator(image));
                parent.Add(frame);
            }
            else { image.AddToClassList(style); parent.Add(image); }
            if (!string.IsNullOrEmpty(caption)) Text(parent, caption, "fine");
            if (magnify) Text(parent, "Role sobre a imagem para ampliar. Arraste para examinar.", "fine");
        }

        void Titles(string l, string r, string category)
        {
            root.Q<Label>("leftTitle").text = l;
            root.Q<Label>("rightTitle").text = r;
            root.Q<Label>("section").text = category;
        }

        void Footer(string leftText, int number = 0, int total = 0)
        {
            root.Q<Label>("leftFolio").text = leftText;
            root.Q<Label>("rightFolio").text = total == 0 ? "" : $"Folha {number} de {total}";
            foreach (var name in new[] { "previousPage", "nextPage" })
                root.Q<Button>(name).style.display = total > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Button>("previousPage").SetEnabled(number > 1);
            root.Q<Button>("nextPage").SetEnabled(number < total);
        }

        void ClearPages()
        {
            editor = null; status = null;
            left.Clear(); right.Clear(); left.scrollOffset = right.scrollOffset = Vector2.zero;
            for (int i = 0; i < tabs.Length; i++) tabs[i].EnableInClassList("current", (int)section == i);
        }

        void Render()
        {
            ClearPages();
            if (section == Section.Index)
            {
                Titles("Caderneta de campo", archive != null ? archive.ActiveCase.title : "Campanha da Itália", "ÁLBUM DE CAMPO");
                Photo(left, emblem, null, "seal");
                ActionButton(left, $"Leituras da fase  ·  {readings.Count}", () => Switch(Section.Reading));
                Text(left, $"Evidências registradas  ·  {records.Count}", "fine");
                ActionButton(left, "Minhas anotações", () => Switch(Section.Notes));
                ActionButton(left, "Legendas", () => Switch(Section.Subtitles));
                if (archive != null)
                {
                    ActionButton(left, "Pasta de pesquisa", archive.OpenBriefing);
                    if (archive.ActiveCase.fieldStudy) ActionButton(left, "Visita de campo", archive.OpenReconstruction);
                    else
                    {
                        ActionButton(left, "Organizar a linha do tempo", archive.OpenChronology);
                        if (save.ActiveDossier?.reconstructionViewed != true)
                            ActionButton(left, "Examinar a reconstrução", archive.OpenReconstruction);
                        ActionButton(left, "Relatório da investigação", archive.OpenReport);
                    }
                    ActionButton(left, "Opções", archive.OpenPause);

                    Text(right, "Objetivo atual", "entry-date");
                    Text(right, archive.CurrentObjective);
                    Text(right, archive.EvidenceCounts, "fine");
                }
                Photo(right, photo, "Expedicionários brasileiros na Itália. Fotografia do acervo do projeto.");
                if (archive == null) Text(right, "Anote aqui suas observações. Para consultar uma evidência, volte ao objeto no local onde o encontrou.");
                Footer("Índice geral"); return;
            }
            if (section == Section.Subtitles)
            {
                ShowSubtitleOptions();
                return;
            }

            bool library = section == Section.Reading;
            int count = library ? readings.Count : records.Count;
            for (int i = 0; i < count; i++)
            {
                int item = i;
                var doc = library ? readings[i] : records[i].doc;
                var b = ActionButton(left, doc.title, () => Select(item), "entry");
                b.EnableInClassList("chosen", i == (library ? reading : record));
            }
            string heading = library ? "Leituras da fase" : "Anotações";
            if (count == 0)
            {
                Titles(heading, library ? "Biblioteca de campo" : "Folha em branco", library ? "BIBLIOTECA DE CAMPO" : "NOTAS DO PESQUISADOR");
                Text(right, library ? "Ainda não há leituras nesta fase." : "Examine uma fonte no arquivo ou no campo. Depois de registrá-la, você pode escrever suas observações aqui.");
                Footer(heading); return;
            }
            if (!library)
            {
                var item = records[record];
                Titles(heading, item.doc.title, "NOTAS DO PESQUISADOR");
                Text(right, "Minha anotação", "entry-date");
                var note = item.notes.Find(n => n.evidenceId == item.doc.id);
                editor = new TextField { name = "evidenceNote", multiline = true, maxLength = 1800 };
                editor.AddToClassList("note"); editor.SetValueWithoutNotify(note?.text ?? "");
                editor.RegisterValueChangedCallback(_ => { dirty = true; if (status != null) status.text = "Alterações por guardar"; });
                right.Add(editor); ActionButton(right, "Guardar anotação", () => FlushNote()); status = Text(right, "", "status");
                Footer("Notas por evidência"); return;
            }
            var current = readings[reading];
            var pages = current.pages ?? Array.Empty<DocumentPage>();
            int total = Math.Max(1, pages.Length); page = Mathf.Clamp(page, 0, total - 1);
            var sheet = pages.Length > 0 ? pages[page] : null;
            Titles(heading, sheet?.title ?? current.title, "BIBLIOTECA DE CAMPO");
            Photo(left, sheet != null ? sheet.image : current.image, sheet != null ? sheet.caption : current.imageCaption, magnify: true);
            Text(right, current.dateLabel, "entry-date");
            Text(right, sheet?.text ?? current.description);
            if (page == total - 1 && !string.IsNullOrWhiteSpace(current.citation)) Text(right, current.citation, "fine");
            Footer("Biblioteca da fase", page + 1, total);
        }

        void ShowSubtitleOptions()
        {
            Titles("Legendas", "Tamanho do texto", "OPÇÕES");
            AddSubtitleSwitch("Exibir legendas", () => SubtitlePreferences.Enabled, value => SubtitlePreferences.Enabled = value);

            string[] sizes = { "Normal", "Grande", "Muito grande" };
            var sizeButtons = new Button[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                int index = i;
                sizeButtons[i] = ActionButton(right, sizes[i], () =>
                {
                    SubtitlePreferences.SizeIndex = index;
                    for (int j = 0; j < sizeButtons.Length; j++)
                        sizeButtons[j].EnableInClassList("chosen", j == index);
                }, "entry");
                sizeButtons[i].EnableInClassList("chosen", i == SubtitlePreferences.SizeIndex);
            }
            Footer("Preferências de leitura");
        }

        void AddSubtitleSwitch(string title, Func<bool> read, Action<bool> write)
        {
            var button = new Button();
            button.AddToClassList("entry");
            void Refresh()
            {
                bool enabled = read();
                button.text = title + (enabled ? " · Sim" : " · Não");
                button.EnableInClassList("chosen", enabled);
            }
            button.clicked += () =>
            {
                write(!read());
                Refresh();
            };
            left.Add(button);
            Refresh();
        }

        public bool FlushNote()
        {
            if (!dirty || editor == null) return true;
            var item = records[record];
            var entry = item.notes.Find(n => n.evidenceId == item.doc.id);
            if (string.IsNullOrWhiteSpace(editor.value)) { if (entry != null) item.notes.Remove(entry); }
            else
            {
                if (entry == null) { entry = new EvidenceNote { evidenceId = item.doc.id }; item.notes.Add(entry); }
                entry.text = editor.value;
            }
            bool ok = ResearchSaveStore.Save(out var error);
            if (ok) dirty = false;
            if (status != null) status.text = ok ? (save.slot < 0 ? "Guardada nesta sessão." : "Anotação guardada.") : error;
            return ok;
        }
    }
}

