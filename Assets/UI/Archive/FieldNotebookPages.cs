using System;
using System.Collections.Generic;
using System.Linq;
using FEB.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    internal sealed class FieldNotebookPages
    {
        enum Section { Index, Reading, Evidence, Notes, Activity }
        readonly VisualElement root;
        readonly ScrollView left, right;
        readonly Button[] tabs;
        readonly List<(Button button, Action action)> bindings = new List<(Button, Action)>();
        readonly List<(HistoricalEvidence doc, List<EvidenceNote> notes)> records = new List<(HistoricalEvidence, List<EvidenceNote>)>();
        readonly List<HistoricalEvidence> readings = new List<HistoricalEvidence>();
        readonly Texture2D photo, emblem;
        readonly ArchiveExperience archive;
        readonly Action<HistoricalEvidence> examine;
        readonly Action pageChanged;
        readonly ResearchSave save;
        readonly ArchiveCaseDefinition firstCase;
        readonly NotebookChapter[] chapters;
        Section section;
        int record, reading, page;
        TextField editor;
        Label status;
        bool dirty;
        public bool IsPausePage { get; private set; }

        public FieldNotebookPages(VisualElement root, ArchiveCaseDefinition firstCase, HistoricalEvidence[] common,
            NotebookChapter[] chapters, string scene, Texture2D photo, Texture2D emblem, ArchiveExperience archive,
            Action close, Action<HistoricalEvidence> examine, Action pageChanged)
        {
            this.root = root; this.photo = photo; this.emblem = emblem;
            this.archive = archive;
            this.examine = examine;
            this.firstCase = firstCase;
            this.chapters = chapters;
            this.pageChanged = pageChanged;
            save = ResearchSaveStore.EnsureSession();
            if (save.dossiers.Count == 0 && firstCase != null) save.SelectCase(firstCase.Id);
            void AddReadings(HistoricalEvidence[] items)
            {
                if (items == null) return;
                foreach (var item in items)
                    if (item != null && !readings.Contains(item)) readings.Add(item);
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
                Bind("evidenceTab", () => Switch(Section.Evidence)), Bind("notesTab", () => Switch(Section.Notes)) };
            Bind("previousPage", () => Turn(-1)); Bind("nextPage", () => Turn(1));
            Bind("closeBook", close).text = archive != null ? "Guardar · Tab" : "Guardar · Esc";
            RefreshRecords();
            Render();
        }

        void RefreshRecords()
        {
            records.Clear();
            var visited = new HashSet<ArchiveCaseDefinition>();
            for (var c = firstCase; c != null && visited.Add(c); c = c.nextCase)
            {
                var dossier = save.dossiers.Find(d => d.caseId == c.Id);
                if (dossier == null) continue;
                dossier.Normalize();
                foreach (var doc in c.evidence)
                    if (doc != null && dossier.evidence.Contains(doc.id)) records.Add((doc, dossier.notes));
            }
            if (chapters != null)
                foreach (var chapter in chapters)
                {
                    if (chapter?.fieldEvidence == null) continue;
                    foreach (var doc in chapter.fieldEvidence)
                        if (doc != null && save.fieldEvidence.Contains(doc.id) && !records.Any(r => r.doc.id == doc.id))
                            records.Add((doc, save.fieldNotes));
                }
            record = Mathf.Clamp(record, 0, Math.Max(0, records.Count - 1));
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

        public void ShowEvidenceIndex(HistoricalEvidence selected, bool sound)
        {
            if (!FlushNote()) return;
            IsPausePage = false;
            RefreshRecords();
            if (selected != null)
            {
                int index = records.FindIndex(item => item.doc.id == selected.id);
                if (index >= 0) record = index;
            }
            section = Section.Evidence; page = 0;
            Render();
            if (sound) pageChanged();
        }

        public void ShowActivity(string kicker, string title, string body, bool isPause, bool sound)
        {
            if (!FlushNote()) return;
            IsPausePage = isPause;
            section = Section.Activity;
            ClearPages();
            Titles(kicker, title, isPause ? "OPÇÕES" : "ARQUIVO HISTÓRICO");
            Text(right, body);
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
                ActionButton(left, $"Evidências registradas  ·  {records.Count}", () => Switch(Section.Evidence));
                ActionButton(left, "Minhas anotações", () => Switch(Section.Notes));
                if (archive != null)
                {
                    ActionButton(left, "Pasta de pesquisa", archive.OpenBriefing);
                    ActionButton(left, "Organizar a linha do tempo", archive.OpenChronology);
                    ActionButton(left, "Examinar a reconstrução", archive.OpenReconstruction);
                    ActionButton(left, "Relatório da investigação", archive.OpenReport);
                    if (archive.HasMultipleCases) ActionButton(left, "Selecionar dossiê", archive.OpenCaseSelection);
                    ActionButton(left, "Opções", archive.OpenPause);

                    Text(right, "Objetivo atual", "entry-date");
                    Text(right, archive.CurrentObjective);
                    Text(right, archive.EvidenceCounts, "fine");
                }
                Photo(right, photo, "Expedicionários brasileiros na Itália. Fotografia do acervo do projeto.");
                if (archive == null) Text(right, "O caderno acompanha a pesquisa do arquivo ao campo. Reúna aqui as fontes consultadas e as observações que elas despertam.");
                Footer("Índice geral"); return;
            }

            bool library = section == Section.Reading, notes = section == Section.Notes;
            int count = library ? readings.Count : records.Count;
            for (int i = 0; i < count; i++)
            {
                int item = i;
                var doc = library ? readings[i] : records[i].doc;
                var b = ActionButton(left, doc.title, () => Select(item), "entry");
                b.EnableInClassList("chosen", i == (library ? reading : record));
            }
            if (archive != null && section == Section.Evidence)
            {
                var dossier = save.ActiveDossier;
                var pending = archive.ActiveCase.evidence.Where(e => e != null && dossier != null &&
                    !dossier.evidence.Contains(e.id)).ToArray();
                if (pending.Length > 0) Text(left, "Por registrar neste dossiê", "entry-date");
                foreach (var item in pending)
                {
                    bool found = dossier.discoveredEvidence.Contains(item.id);
                    ActionButton(left, item.title + (found ? " · Examinar" : " · Não encontrada"),
                        () => examine(item)).SetEnabled(found);
                }
            }
            string heading = library ? "Leituras da fase" : notes ? "Anotações" : "Evidências";
            if (count == 0)
            {
                Titles(heading, library ? "Biblioteca de campo" : "Folha em branco", notes ? "NOTAS DO PESQUISADOR" : "REGISTRO DE CAMPO");
                Text(right, library ? "Ainda não há leituras nesta fase." : "Examine uma fonte no arquivo ou no campo e registre a evidência para consultar sua ficha e escrever sobre ela aqui.");
                if (!library) ActionButton(right, "Abrir leituras da fase", () => Switch(Section.Reading));
                Footer(heading); return;
            }
            var current = library ? readings[reading] : records[record].doc;
            var pages = current.pages ?? Array.Empty<DocumentPage>();
            int total = Math.Max(1, pages.Length); page = Mathf.Clamp(page, 0, total - 1);
            var sheet = pages.Length > 0 ? pages[page] : null;
            Titles(heading, notes ? current.title : sheet?.title ?? current.title,
                library ? "BIBLIOTECA DE CAMPO" : notes ? "NOTAS DO PESQUISADOR" : "FICHA DE EVIDÊNCIA");
            if (notes)
            {
                Text(right, current.keyFinding, "fine");
                Text(right, "Minha anotação", "entry-date");
                var note = records[record].notes.Find(n => n.evidenceId == current.id);
                editor = new TextField { name = "evidenceNote", multiline = true, maxLength = 1800 };
                editor.AddToClassList("note"); editor.SetValueWithoutNotify(note?.text ?? "");
                editor.RegisterValueChangedCallback(_ => { dirty = true; if (status != null) status.text = "Alterações por guardar"; });
                right.Add(editor); ActionButton(right, "Guardar anotação", () => FlushNote()); status = Text(right, "", "status");
                Footer("Notas por evidência"); return;
            }
            Photo(left, sheet != null ? sheet.image : current.image, sheet != null ? sheet.caption : current.imageCaption, magnify: true);
            Text(right, current.dateLabel, "entry-date");
            Text(right, sheet?.text ?? current.description);
            if (page == total - 1 && !string.IsNullOrWhiteSpace(current.citation)) Text(right, current.citation, "fine");
            if (!library)
            {
                ActionButton(right, "Anotar sobre esta evidência", () => Switch(Section.Notes));
                if (archive != null && archive.ActiveCase.evidence.Contains(current))
                    ActionButton(right, "Reexaminar no acervo", () => examine(current));
            }
            Footer(library ? "Biblioteca da fase" : "Arquivo de evidências", page + 1, total);
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

