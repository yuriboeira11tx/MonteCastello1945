using System;
using FEB.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    [RequireComponent(typeof(UIDocument), typeof(EvidenceInspection))]
    public sealed class FieldEvidenceView : MonoBehaviour
    {
        VisualElement panel, tools, sources, navigation;
        Image preview, photo;
        Label prompt, caption, title, date, heading, body, bibliography, pageNumber, registrationError;
        Button reset, close, previous, next, register;
        ScrollView reading;
        EvidenceInspection inspection;

        HistoricalEvidence document;
        Func<bool> registerEvidence;
        Action closed;
        int page;
        bool reachedLastPage, registered;
        public bool IsOpen => document != null;
        int PageCount => Mathf.Max(1, document.pages?.Length ?? 0);

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            prompt = root.Q<Label>("fieldPrompt");
            panel = root.Q("fieldInspection");
            tools = panel.Q("objectTools");
            preview = panel.Q<Image>("objectPreview");
            photo = panel.Q<Image>("objectPhoto");
            photo.scaleMode = ScaleMode.ScaleToFit;
            caption = panel.Q<Label>("objectCaption");
            title = panel.Q<Label>("fileTitle");
            date = panel.Q<Label>("fileDate");
            heading = panel.Q<Label>("pageHeading");
            body = panel.Q<Label>("pageText");
            sources = panel.Q("sources");
            bibliography = panel.Q<Label>("bibliography");
            reading = panel.Q<ScrollView>("readingScroll");
            navigation = panel.Q("pageNavigation");
            pageNumber = panel.Q<Label>("pageNumber");
            registrationError = panel.Q<Label>("registrationError");
            reset = panel.Q<Button>("resetObject");
            close = panel.Q<Button>("closeInspection");
            previous = panel.Q<Button>("previousPage");
            next = panel.Q<Button>("nextPage");
            register = panel.Q<Button>("registerEvidence");

            inspection = GetComponent<EvidenceInspection>();
            inspection.Initialize(preview);
            reset.clicked += inspection.ResetView;
            close.clicked += Close;
            previous.clicked += PreviousPage;
            next.clicked += NextPage;
            register.clicked += Register;
            panel.RegisterCallback<GeometryChangedEvent>(OnPanelGeometry);
            ScreenUI.Show(panel, false);
            SetPrompt(null);
        }

        public void SetPrompt(string text)
        {
            if (prompt == null) return;
            bool visible = !IsOpen && !string.IsNullOrEmpty(text);
            if (visible && prompt.text != text) prompt.text = text;
            ScreenUI.Show(prompt, visible);
        }

        public void Open(HistoricalEvidence evidence, bool registered, Func<bool> onRegistered, Action onClosed)
        {
            if (evidence == null || IsOpen) return;
            document = evidence;
            this.registered = registered;
            registerEvidence = onRegistered;
            closed = onClosed;
            page = 0;
            reachedLastPage = false;
            SetPrompt(null);
            ScreenUI.Show(panel, true);
            ScreenUI.Show(registrationError, false);
            title.text = evidence.title;
            date.text = evidence.dateLabel;
            ScreenUI.Show(date, !string.IsNullOrWhiteSpace(evidence.dateLabel));
            caption.text = evidence.imageCaption;
            ScreenUI.Show(caption, !string.IsNullOrWhiteSpace(evidence.imageCaption));
            bibliography.text = evidence.citation;
            bool hasModel = inspection.Show(evidence);
            photo.image = hasModel ? null : evidence.image;
            ScreenUI.Show(photo, photo.image != null);
            ScreenUI.Show(tools, hasModel);
            ShowPage();
            close.Focus();
        }

        void OnPanelGeometry(GeometryChangedEvent evt)
        {
            var bounds = evt.newRect;
            panel.EnableInClassList("inspection-split--compact", bounds.width < 1180 || bounds.height < 650);
        }

        void PreviousPage()
        {
            if (!IsOpen || page == 0) return;
            page--;
            ShowPage();
        }

        void NextPage()
        {
            if (!IsOpen || page + 1 >= PageCount) return;
            page++;
            ShowPage();
        }

        void ShowPage()
        {
            int total = PageCount;
            bool last = page + 1 == total;
            reachedLastPage |= last;
            var sheet = document.pages != null && page < document.pages.Length ? document.pages[page] : null;
            heading.text = sheet?.title;
            ScreenUI.Show(heading, !string.IsNullOrWhiteSpace(heading.text) && heading.text != title.text);
            body.text = sheet?.text ?? document.description;
            ScreenUI.Show(sources, last && !string.IsNullOrWhiteSpace(document.citation));
            pageNumber.text = $"Folha {page + 1} de {total}";
            ScreenUI.Show(navigation, total > 1);
            previous.SetEnabled(page > 0);
            next.SetEnabled(!last);
            reading.scrollOffset = Vector2.zero;
            UpdateRegistration();
        }

        void UpdateRegistration()
        {
            register.text = registered ? "Registrada na caderneta" : "Registrar evidência";
            register.SetEnabled(!registered && reachedLastPage && registerEvidence != null);
        }

        void Register()
        {
            if (!IsOpen || registered || !reachedLastPage || registerEvidence == null) return;
            registered = registerEvidence();
            ScreenUI.Show(registrationError, !registered);
            if (!registered) registrationError.text = "Não foi possível salvar. Tente registrar novamente.";
            UpdateRegistration();
        }

        void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Close()
        {
            if (!IsOpen) return;
            document = null;
            inspection.Hide();
            photo.image = null;
            ScreenUI.Show(panel, false);
            registerEvidence = null;
            var notify = closed;
            closed = null;
            notify?.Invoke();
        }

        void OnDisable()
        {
            Close();
            reset.clicked -= inspection.ResetView;
            close.clicked -= Close;
            previous.clicked -= PreviousPage;
            next.clicked -= NextPage;
            register.clicked -= Register;
            panel.UnregisterCallback<GeometryChangedEvent>(OnPanelGeometry);
            inspection.Detach();
            SetPrompt(null);
        }
    }
}
