using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    public enum ArchiveBoardMode { Chronology, Sources, Camp }

    [RequireComponent(typeof(UIDocument))]
    public sealed class ArchiveEvidenceBoardView : MonoBehaviour
    {
        const float LayoutWidth = 1440, LayoutHeight = 648;
        static readonly Color ThreadColor = new Color32(125, 43, 34, 255);
        static readonly Color ThreadShadow = new Color32(41, 24, 12, 105);

        VisualElement root, frame, surface, cards, labels, threads;
        Label title, feedback;
        Button check;
        ArchiveCaseDefinition chapter;
        ArchiveBoardMode mode;
        Rect viewport = new Rect(.045f, .06f, .91f, .88f);
        Action onSolved, onClosed, onIncorrect;
        Button[] cardButtons;
        VisualElement[] cardPins, slotPins, columns;
        int[] assigned, displayed, chronologicalOrder;
        bool[] linked;
        int selected = -1;
        bool verified;
        public bool IsOpen { get; private set; }

        void Start()
        {
            var tree = GetComponent<UIDocument>().rootVisualElement;
            if (tree != null) tree.pickingMode = PickingMode.Ignore;
        }

        public void Show(ArchiveBoardMode mode, ArchiveCaseDefinition caseDefinition,
            Action onSolved, Action onClosed, Action onIncorrect = null)
        {
            if (!BindDocument())
            {
                Debug.LogError("Atribua QuadroDeEvidencias.uxml ao UIDocument do quadro.", this);
                onClosed?.Invoke();
                return;
            }
            this.mode = mode;
            chapter = caseDefinition;
            this.onSolved = onSolved;
            this.onClosed = onClosed;
            this.onIncorrect = onIncorrect;
            selected = -1;
            verified = false;
            IsOpen = true;
            cards.Clear();
            labels.Clear();
            cardPins = slotPins = null;
            root.RemoveFromClassList("hidden");
            SetViewport(viewport);
            title.text = chapter != null ? chapter.title : "Quadro de evidências";
            check.text = "Conferir";
            check.SetEnabled(IsConfigured());
            if (!check.enabledSelf)
            {
                SetFeedback("As fontes desta montagem ainda não estão disponíveis.");
                return;
            }
            BuildLayout();
            SetFeedback(Instructions());
            cardButtons[displayed[0]].schedule.Execute(cardButtons[displayed[0]].Focus);
        }

        bool BindDocument()
        {
            var tree = GetComponent<UIDocument>().rootVisualElement;
            var boardRoot = tree?.Q("boardRoot");
            if (boardRoot == null) return false;
            if (root == boardRoot) return true;
            tree.pickingMode = PickingMode.Ignore;
            root = boardRoot;
            frame = root.Q("boardFrame");
            surface = frame.Q("boardSurface");
            cards = surface.Q("boardCards");
            labels = surface.Q("boardLabels");
            threads = surface.Q("boardThreads");
            title = surface.Q<Label>("boardTitle");
            feedback = surface.Q<Label>("boardFeedback");
            check = surface.Q<Button>("checkBoard");
            check.clicked += Check;
            root.Q<Button>("closeBoard").clicked += () => Finish(onClosed);
            threads.generateVisualContent += DrawThreads;
            frame.RegisterCallback<GeometryChangedEvent>(evt => FitSurface(evt.newRect.size));
            return true;
        }

        bool IsConfigured()
        {
            var items = chapter != null ? chapter.evidence : null;
            if (items == null || items.Length == 0 || items.Any(item => item == null)) return false;
            if (mode == ArchiveBoardMode.Camp)
                return chapter.boardThemes?.Length == items.Length && !chapter.boardThemes.Any(string.IsNullOrWhiteSpace);
            if (mode == ArchiveBoardMode.Sources)
                return !string.IsNullOrWhiteSpace(chapter.boardConclusion) && chapter.boardSources?.Length > 0 &&
                       chapter.boardSources.All(item => Array.IndexOf(items, item) >= 0);
            return true;
        }

        public void SetViewport(Rect viewport)
        {
            this.viewport = viewport;
            if (frame == null) return;
            frame.style.left = Length.Percent(viewport.x * 100);
            frame.style.top = Length.Percent((1f - viewport.yMax) * 100);
            frame.style.width = Length.Percent(viewport.width * 100);
            frame.style.height = Length.Percent(viewport.height * 100);
        }

        void FitSurface(Vector2 size)
        {
            float scale = Mathf.Min(size.x / LayoutWidth, size.y / LayoutHeight);
            if (scale <= 0) return;
            surface.style.scale = new Scale(new Vector3(scale, scale, 1));
            surface.style.left = (size.x - LayoutWidth * scale) / 2;
            surface.style.top = (size.y - LayoutHeight * scale) / 2;
            threads.MarkDirtyRepaint();
        }

        void BuildLayout()
        {
            int count = chapter.evidence.Length;
            assigned = Enumerable.Repeat(-1, count).ToArray();
            linked = new bool[count];
            cardButtons = new Button[count];
            cardPins = new VisualElement[count];
            columns = new VisualElement[count];
            chronologicalOrder = Enumerable.Range(0, count).OrderBy(i => chapter.evidence[i].chronologyOrder).ToArray();
            displayed = Enumerable.Range(0, count).OrderBy(i => chapter.evidence[i].title, StringComparer.Ordinal).ToArray();
            // A abertura deve apresentar uma montagem por fazer, inclusive nos casos já cadastrados por ordem.
            if (count > 1 && displayed.SequenceEqual(ExpectedSlots()))
                (displayed[0], displayed[count - 1]) = (displayed[count - 1], displayed[0]);
            for (int slot = 0; slot < count; slot++)
            {
                int id = displayed[slot];
                columns[slot] = Column(cards);
                cardButtons[id] = MakeCard(id);
                columns[slot].Add(cardButtons[id]);
            }
            if (mode == ArchiveBoardMode.Sources)
            {
                var caption = new VisualElement { name = "boardConclusion", pickingMode = PickingMode.Ignore };
                caption.AddToClassList("board-caption");
                labels.Add(caption);
                slotPins = new[] { AddPin(caption) };
                AddLabel(caption, chapter.boardConclusion, "board-tag-text");
                return;
            }
            slotPins = new VisualElement[count];
            for (int slot = 0; slot < count; slot++)
            {
                int index = slot;
                var tag = new Button(() => Assign(index)) { name = "slot_" + slot };
                tag.AddToClassList("board-tag");
                Column(labels).Add(tag);
                slotPins[slot] = AddPin(tag);
                var text = mode == ArchiveBoardMode.Camp ? chapter.boardThemes[slot] : (slot + 1) + "º acontecimento";
                AddLabel(tag, text, "board-tag-text").AddToClassList("typewriter");
            }
        }

        Button MakeCard(int id)
        {
            var source = chapter.evidence[id];
            var card = new Button(() => SelectCard(id)) { name = "card_" + source.id };
            card.AddToClassList("board-card");
            var photo = new Image { image = source.image, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            photo.AddToClassList("board-photo");
            card.Add(photo);
            AddLabel(card, source.title, "board-card-title");
            AddLabel(card, source.dateLabel, "board-card-date").AddToClassList("typewriter");
            cardPins[id] = AddPin(card);
            return card;
        }

        static VisualElement Column(VisualElement row)
        {
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.AddToClassList("board-column");
            row.Add(column);
            return column;
        }

        static Label AddLabel(VisualElement parent, string text, string styleClass)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(styleClass);
            parent.Add(label);
            return label;
        }

        static VisualElement AddPin(VisualElement parent)
        {
            var pin = new VisualElement { pickingMode = PickingMode.Ignore };
            var glint = new VisualElement { pickingMode = PickingMode.Ignore };
            pin.AddToClassList("board-pin");
            glint.AddToClassList("board-glint");
            pin.Add(glint);
            parent.Add(pin);
            return pin;
        }

        string Instructions()
        {
            if (mode == ArchiveBoardMode.Sources) return "Ligue à legenda os alfinetes das fontes que a sustentam. Clique outra vez para soltar o fio.";
            return mode == ArchiveBoardMode.Camp
                ? "Escolha uma fotografia e depois a etiqueta que identifica o seu tema."
                : "Escolha um documento e depois a sua posição, do mais antigo ao mais recente.";
        }

        System.Collections.Generic.IEnumerable<int> ExpectedSlots() => mode == ArchiveBoardMode.Chronology
            ? chronologicalOrder : Enumerable.Range(0, chapter.evidence.Length);

        void SelectCard(int id)
        {
            if (verified) return;
            if (mode == ArchiveBoardMode.Sources)
            {
                linked[id] = !linked[id];
                SetFeedback(linked[id] ? "Fio preso à legenda. Você pode ligar outra fonte ou conferir." : "Fio solto. Escolha as fontes que sustentam a legenda.");
                threads.MarkDirtyRepaint();
                return;
            }
            if (selected >= 0) cardButtons[selected].RemoveFromClassList("board-selected");
            selected = selected == id ? -1 : id;
            if (selected >= 0) cardButtons[id].AddToClassList("board-selected");
            SetFeedback(selected < 0 ? Instructions() : mode == ArchiveBoardMode.Camp
                ? "Agora escolha a etiqueta desta fotografia."
                : "Agora escolha a posição deste documento na sequência.");
        }

        void Assign(int slot)
        {
            if (verified) return;
            if (selected < 0)
            {
                SetFeedback("Escolha primeiro o documento que deseja prender.");
                return;
            }
            int previous = Array.IndexOf(displayed, selected);
            if (previous != slot)
            {
                int displaced = displayed[slot];
                columns[previous].Add(cardButtons[displaced]);
                columns[slot].Add(cardButtons[selected]);
                displayed[previous] = displaced;
                displayed[slot] = selected;
                assigned[previous] = -1;
            }
            assigned[slot] = selected;
            cardButtons[selected].RemoveFromClassList("board-selected");
            selected = -1;
            SetFeedback(assigned.All(index => index >= 0) ? "Todas as fichas estão presas. Confira a montagem." : "Ficha presa. Escolha o próximo documento.");
            threads.MarkDirtyRepaint();
        }

        void Check()
        {
            if (!IsOpen) return;
            if (verified)
            {
                Finish(onSolved);
                return;
            }
            bool sources = mode == ArchiveBoardMode.Sources;
            if (sources ? !linked.Any(value => value) : assigned.Any(value => value < 0))
            {
                SetFeedback(sources ? "Prenda ao menos um fio antes de conferir." : "Ainda há fichas soltas. Prenda um documento a cada etiqueta.", true);
                return;
            }
            bool correct = sources
                ? chapter.evidence.Where((item, i) => linked[i]).ToHashSet().SetEquals(chapter.boardSources)
                : assigned.SequenceEqual(ExpectedSlots());
            if (!correct)
            {
                onIncorrect?.Invoke();
                string hint = mode == ArchiveBoardMode.Sources ? "Releia a legenda. Que fonte estabelece a decisão e qual documenta um ataque posterior?"
                    : mode == ArchiveBoardMode.Camp ? "Observe o que as pessoas fazem em cada fotografia e reveja as etiquetas."
                    : "Compare as datas das fontes. Um recorte de jornal leva a data da publicação.";
                SetFeedback(hint, true);
                return;
            }
            verified = true;
            if (selected >= 0) cardButtons[selected].RemoveFromClassList("board-selected");
            check.text = "Concluir";
            SetFeedback(sources ? "A legenda está apoiada pelas fontes. Conclua a montagem." : "A montagem está correta. Conclua para prosseguir.");
            check.Focus();
        }

        void SetFeedback(string message, bool error = false)
        {
            feedback.text = message;
            feedback.EnableInClassList("board-error", error);
        }

        void DrawThreads(MeshGenerationContext context)
        {
            if (!IsOpen || cardPins == null || slotPins == null) return;
            if (mode == ArchiveBoardMode.Sources)
            {
                for (int i = 0; i < linked.Length; i++)
                    if (linked[i]) DrawThread(context.painter2D, cardPins[i], slotPins[0]);
                return;
            }
            for (int i = 0; i < assigned.Length; i++)
                if (assigned[i] >= 0) DrawThread(context.painter2D, cardPins[assigned[i]], slotPins[i]);
        }

        void DrawThread(Painter2D pen, VisualElement from, VisualElement to)
        {
            Vector2 start = threads.WorldToLocal(from.worldBound.center);
            Vector2 end = threads.WorldToLocal(to.worldBound.center);
            Vector2 middle = (start + end) * .5f + new Vector2(0f, 8f);
            Stroke(pen, start + Vector2.one * 2, middle + Vector2.one * 2, end + Vector2.one * 2, ThreadShadow, 4);
            Stroke(pen, start, middle, end, ThreadColor, 3);
        }

        static void Stroke(Painter2D pen, Vector2 start, Vector2 middle, Vector2 end, Color color, float width)
        {
            pen.strokeColor = color;
            pen.lineWidth = width;
            pen.BeginPath();
            pen.MoveTo(start);
            pen.QuadraticCurveTo(middle, end);
            pen.Stroke();
        }

        void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Finish(onClosed);
        }

        void Finish(Action callback)
        {
            if (!IsOpen) return;
            Hide();
            callback?.Invoke();
        }

        public void Hide()
        {
            IsOpen = false;
            root?.AddToClassList("hidden");
            onSolved = onClosed = onIncorrect = null;
        }

        void OnDisable()
        {
            if (IsOpen) Finish(onClosed);
        }
    }
}
