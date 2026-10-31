using System;
using System.Collections;
using FEB.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    [RequireComponent(typeof(UIDocument), typeof(EvidenceInspection))]
    public sealed class ArchiveView : MonoBehaviour
    {
        VisualElement modal;
        Label title;
        Label subtitle;
        Label body;
        Label citation;
        Image illustration;
        Label imageCaption;
        VisualElement imagePlaceholder;
        Button[] choices;
        readonly Action[] choiceActions = new Action[6];
        Button closeButton;
        Button zoomButton;
        VisualElement zoomPanel;
        Image zoomImage;
        Button zoomCloseButton;
        Label objective;
        Label progress;
        Label interactionLabel;
        VisualElement transition;
        VisualElement hud;
        EvidenceInspection inspection;
        VisualElement inspectionControls;
        Button resetInspectionButton;
        public event Action<bool> VisibilityChanged;
        public bool IsOpen => ScreenUI.IsVisible(modal);
        int openedFrame;
        int choiceIndex;
        bool transitioning;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            modal = root.Q("documentPanel");
            title = root.Q<Label>("title");
            subtitle = root.Q<Label>("subtitle");
            body = root.Q<Label>("body");
            body.RegisterCallback<GeometryChangedEvent>(OnBodyGeometryChanged);
            citation = root.Q<Label>("citation");
            illustration = root.Q<Image>("illustration");
            imageCaption = root.Q<Label>("imageCaption");
            imagePlaceholder = root.Q("imagePlaceholder");
            closeButton = root.Q<Button>("close");
            zoomButton = root.Q<Button>("zoom");
            zoomPanel = root.Q("zoomPanel");
            zoomImage = root.Q<Image>("zoomImage");
            zoomCloseButton = root.Q<Button>("zoomClose");
            objective = root.Q<Label>("objective");
            progress = root.Q<Label>("progress");
            interactionLabel = root.Q<Label>("interaction");
            transition = root.Q("transition");
            hud = root.Q("hud");
            inspectionControls = root.Q("inspectionControls");
            resetInspectionButton = root.Q<Button>("resetInspection");
            inspection = GetComponent<EvidenceInspection>();
            inspection.Initialize(root.Q<Image>("inspection"));
            choices = new Button[6];
            for (int i = 0; i < choices.Length; i++) choices[i] = root.Q<Button>("choice" + i);
            closeButton.clicked += Close;
            zoomButton.clicked += OpenZoom;
            zoomCloseButton.clicked += CloseZoom;
            resetInspectionButton.clicked += inspection.ResetView;
            illustration.scaleMode = zoomImage.scaleMode = ScaleMode.ScaleToFit;
            ResetChoices();
            ScreenUI.Show(modal, false);
            ScreenUI.Show(zoomPanel, false);
            ScreenUI.Show(inspectionControls, false);
            ScreenUI.Show(transition, false);
            transitioning = false;
        }

        void Update()
        {
            if (!transitioning && IsOpen && Time.frameCount > openedFrame && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (ScreenUI.IsVisible(zoomPanel)) CloseZoom();
                else Close();
            }
        }

        void OpenZoom() { modal.SetEnabled(false); ScreenUI.Show(zoomPanel, true); zoomCloseButton.Focus(); }
        void CloseZoom() { ScreenUI.Show(zoomPanel, false); modal.SetEnabled(true); zoomButton.Focus(); }

        void ResetChoices()
        {
            choiceIndex = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                if (choiceActions[i] != null) choices[i].clicked -= choiceActions[i];
                choiceActions[i] = null;
                ScreenUI.Show(choices[i], false);
            }
        }

        public void Show(string heading, string context, string text, Texture texture = null, string credit = "", string caption = "")
        {
            inspection.Hide();
            ScreenUI.Show(inspectionControls, false);
            ScreenUI.Show(hud, false);
            modal.SetEnabled(true);
            title.text = heading;
            ScreenUI.FitText(title, 31, 41);
            subtitle.text = context;
            body.text = text;
            body.schedule.Execute(ResizeBody);
            ScrollToTop();
            citation.text = credit;
            imageCaption.text = caption;
            illustration.image = zoomImage.image = texture;
            ScreenUI.Show(illustration, texture != null);
            ScreenUI.Show(imagePlaceholder, texture == null);
            ScreenUI.Show(zoomButton, texture != null);
            ResetChoices();
            ScreenUI.Show(zoomPanel, false);
            ScreenUI.Show(modal, true);
            openedFrame = Time.frameCount;
            closeButton.Focus();
            VisibilityChanged?.Invoke(true);
        }

        void OnBodyGeometryChanged(GeometryChangedEvent evt)
        {
            if (!Mathf.Approximately(evt.newRect.width, evt.oldRect.width)) ResizeBody();
        }

        void ResizeBody()
        {
            if (body.contentRect.width <= 0) return;
            var measured = body.MeasureTextSize(body.text, body.contentRect.width,
                VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
            body.style.height = Mathf.Ceil(measured.y) + 2;
        }

        void ScrollToTop()
        {
            var scroll = body.GetFirstAncestorOfType<ScrollView>();
            if (scroll != null) scroll.scrollOffset = Vector2.zero;
        }

        public void ShowEvidence(HistoricalEvidence document)
        {
            Show(document.title, document.dateLabel, document.description, document.image, document.citation, document.imageCaption);
            if (inspection.Show(document))
            {
                ScreenUI.Show(illustration, false);
                ScreenUI.Show(imagePlaceholder, false);
                ScreenUI.Show(inspectionControls, true);
            }
        }

        public void AddChoice(string label, Action action, bool enabled = true)
        {
            if (choiceIndex >= choices.Length) throw new InvalidOperationException("O painel não possui espaço para todas as opções.");
            int index = choiceIndex++;
            var button = choices[index];
            button.text = label;
            button.SetEnabled(enabled);
            ScreenUI.Show(button, true);
            ScreenUI.FitText(button, 19, 23);
            choiceActions[index] = action;
            button.clicked += action;
        }

        public void SetHud(string nextObjective, string completion)
        {
            objective.text = nextObjective;
            progress.text = completion;
        }

        public void SetInteraction(string message)
        {
            if (interactionLabel != null) interactionLabel.text = message;
        }

        public void Close()
        {
            if (transitioning) return;
            inspection.Hide();
            ScreenUI.Show(inspectionControls, false);
            ScreenUI.Show(zoomPanel, false);
            ScreenUI.Show(modal, false);
            ScreenUI.Show(hud, true);
            VisibilityChanged?.Invoke(false);
        }

        public IEnumerator FadeToBlack(float duration)
        {
            transitioning = true;
            closeButton.SetEnabled(false);
            ScreenUI.Show(transition, true);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                transition.style.opacity = Mathf.SmoothStep(0, 1, elapsed / duration);
                yield return null;
            }
            transition.style.opacity = 1;
        }

        void OnDisable()
        {
            if (closeButton == null) return;
            closeButton.clicked -= Close;
            zoomButton.clicked -= OpenZoom;
            zoomCloseButton.clicked -= CloseZoom;
            resetInspectionButton.clicked -= inspection.ResetView;
            body.UnregisterCallback<GeometryChangedEvent>(OnBodyGeometryChanged);
            ResetChoices();
            inspection.Detach();
        }
    }
}
