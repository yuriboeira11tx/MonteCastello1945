using System;
using FEB.Archive;
using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    public sealed class HistoricalDocumentReader : IDisposable
    {
        readonly VisualElement panel, imageColumn;
        readonly Label heading, pageLabel, text, citation;
        readonly Image image;
        readonly ScrollView scroll;
        readonly Button previous, next, done, close, fit, smaller, larger;
        readonly DocumentImageManipulator imageManipulator;
        readonly Action closed;
        DocumentPage[] pages;
        HistoricalEvidence evidence;
        Action completed;
        int page, furthestPage, textSize;
        public bool IsOpen => ScreenUI.IsVisible(panel);

        public HistoricalDocumentReader(VisualElement root, Action closed)
        {
            this.closed = closed;
            panel = root.Q("readerPanel");
            heading = root.Q<Label>("readerTitle");
            pageLabel = root.Q<Label>("readerPage");
            text = root.Q<Label>("readerText");
            citation = root.Q<Label>("readerCitation");
            image = root.Q<Image>("readerImage");
            imageColumn = root.Q("readerImageColumn");
            scroll = root.Q<ScrollView>("readerScroll");
            previous = root.Q<Button>("readerPrevious");
            next = root.Q<Button>("readerNext");
            done = root.Q<Button>("readerDone");
            close = root.Q<Button>("readerClose");
            fit = root.Q<Button>("readerFit");
            smaller = root.Q<Button>("readerSmaller");
            larger = root.Q<Button>("readerLarger");
            image.scaleMode = ScaleMode.ScaleToFit;
            imageManipulator = new DocumentImageManipulator(image);
            root.Q("readerImageViewport").AddManipulator(imageManipulator);
            previous.clicked += Previous;
            next.clicked += Next;
            done.clicked += Complete;
            close.clicked += Close;
            fit.clicked += imageManipulator.Reset;
            smaller.clicked += Smaller;
            larger.clicked += Larger;
            text.RegisterCallback<GeometryChangedEvent>(OnGeometry);
        }

        public void Open(HistoricalEvidence document, Action onCompleted)
        {
            evidence = document;
            completed = onCompleted;
            pages = document.pages != null && document.pages.Length > 0 ? document.pages : new[] {
                new DocumentPage { title = document.title, text = document.description,
                    image = document.image, caption = document.imageCaption }
            };
            page = furthestPage = 0;
            ScreenUI.Show(panel, true);
            ShowPage();
            next.Focus();
        }

        void ShowPage()
        {
            var current = pages[page];
            heading.text = evidence.title;
            pageLabel.text = $"{page + 1} / {pages.Length}  •  {current.title}";
            text.text = current.text;
            citation.text = current.caption + "\n" + evidence.citation;
            image.image = current.image;
            ScreenUI.Show(imageColumn, current.image != null);
            imageManipulator.Reset();
            scroll.scrollOffset = Vector2.zero;
            previous.SetEnabled(page > 0);
            next.SetEnabled(page < pages.Length - 1);
            done.SetEnabled(furthestPage == pages.Length - 1);
            text.schedule.Execute(ResizeText);
        }

        void Previous() { if (page > 0) { page--; ShowPage(); } }
        void Next() { if (page + 1 < pages.Length) { page++; furthestPage = Math.Max(furthestPage, page); ShowPage(); } }
        void Complete() { if (furthestPage < pages.Length - 1) return; var action = completed; Close(); action?.Invoke(); }
        void Smaller() { textSize = Mathf.Max(-1, textSize - 1); ApplyTextSize(); }
        void Larger() { textSize = Mathf.Min(1, textSize + 1); ApplyTextSize(); }
        void ApplyTextSize()
        {
            text.EnableInClassList("reader-text-large", textSize == 1);
            text.EnableInClassList("reader-text-small", textSize == -1);
            text.schedule.Execute(ResizeText);
        }
        void OnGeometry(GeometryChangedEvent evt) { if (evt.newRect.width != evt.oldRect.width) ResizeText(); }
        void ResizeText()
        {
            if (text.contentRect.width <= 0) return;
            text.style.height = Mathf.Ceil(text.MeasureTextSize(text.text, text.contentRect.width,
                VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined).y) + 2;
        }
        public void Close()
        {
            imageManipulator.Reset();
            ScreenUI.Show(panel, false);
            completed = null;
            closed?.Invoke();
        }
        public void Dispose()
        {
            previous.clicked -= Previous;
            next.clicked -= Next;
            done.clicked -= Complete;
            close.clicked -= Close;
            fit.clicked -= imageManipulator.Reset;
            smaller.clicked -= Smaller;
            larger.clicked -= Larger;
            text.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            image.parent.RemoveManipulator(imageManipulator);
        }

        sealed class DocumentImageManipulator : PointerManipulator
        {
            readonly Image image;
            float zoom = 1;
            Vector2 offset;
            int pointer = -1;
            public DocumentImageManipulator(Image image) => this.image = image;
            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(Down);
                target.RegisterCallback<PointerMoveEvent>(Move);
                target.RegisterCallback<PointerUpEvent>(Up);
                target.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
                target.RegisterCallback<WheelEvent>(Wheel);
            }
            protected override void UnregisterCallbacksFromTarget()
            {
                Reset();
                target.UnregisterCallback<PointerDownEvent>(Down);
                target.UnregisterCallback<PointerMoveEvent>(Move);
                target.UnregisterCallback<PointerUpEvent>(Up);
                target.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);
                target.UnregisterCallback<WheelEvent>(Wheel);
            }
            void Down(PointerDownEvent evt) { if (evt.button != 0) return; pointer = evt.pointerId; target.CapturePointer(pointer); evt.StopPropagation(); }
            void Move(PointerMoveEvent evt) { if (pointer != evt.pointerId) return; offset += new Vector2(evt.deltaPosition.x, evt.deltaPosition.y); Apply(); }
            void Up(PointerUpEvent evt) { if (evt.pointerId == pointer) Release(); }
            void CaptureOut(PointerCaptureOutEvent evt) => pointer = -1;
            void Wheel(WheelEvent evt) { zoom = Mathf.Clamp(zoom * Mathf.Exp(-evt.delta.y * .12f), 1, 6); Apply(); evt.StopPropagation(); }
            void Release() { int id = pointer; pointer = -1; if (id >= 0 && target.HasPointerCapture(id)) target.ReleasePointer(id); }
            public void Reset() { Release(); zoom = 1; offset = Vector2.zero; Apply(); }
            void Apply()
            {
                if (target == null) return;
                var bounds = target.contentRect.size * ((zoom - 1) * .5f);
                offset.x = Mathf.Clamp(offset.x, -Mathf.Max(0, bounds.x), Mathf.Max(0, bounds.x));
                offset.y = Mathf.Clamp(offset.y, -Mathf.Max(0, bounds.y), Mathf.Max(0, bounds.y));
                image.style.scale = new Scale(new Vector3(zoom, zoom, 1));
                image.style.translate = new Translate(offset.x, offset.y);
            }
        }
    }
}
