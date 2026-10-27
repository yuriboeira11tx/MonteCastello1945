using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FEB.Archive
{
    public sealed class ArchiveView : MonoBehaviour
    {
        [SerializeField] GameObject modal;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text citation;
        [SerializeField] RawImage illustration;
        [SerializeField] AspectRatioFitter illustrationAspect;
        [SerializeField] TMP_Text imageCaption;
        [SerializeField] GameObject imagePlaceholder;
        [SerializeField] Button[] choices;
        [SerializeField] Button closeButton;
        [SerializeField] Button zoomButton;
        [SerializeField] GameObject zoomPanel;
        [SerializeField] RawImage zoomImage;
        [SerializeField] AspectRatioFitter zoomAspect;
        [SerializeField] Button zoomCloseButton;
        [SerializeField] TMP_Text objective;
        [SerializeField] TMP_Text progress;
        [SerializeField] CanvasGroup transition;
        public event Action<bool> VisibilityChanged;
        public bool IsOpen => modal.activeSelf;
        int openedFrame;
        int choiceIndex;
        bool transitioning;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            zoomButton.onClick.AddListener(() => zoomPanel.SetActive(true));
            zoomCloseButton.onClick.AddListener(() => zoomPanel.SetActive(false));
            modal.SetActive(false);
            zoomPanel.SetActive(false);
            if (transition != null) { transition.alpha = 0; transition.blocksRaycasts = false; }
        }

        void Update()
        {
            if (!transitioning && IsOpen && Time.frameCount > openedFrame && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (zoomPanel.activeSelf) zoomPanel.SetActive(false);
                else Close();
            }
        }

        public void Show(string heading, string context, string text, Texture texture = null, string credit = "", string caption = "")
        {
            title.text = heading;
            subtitle.text = context;
            body.text = text;
            citation.text = credit;
            imageCaption.text = caption;
            illustration.texture = zoomImage.texture = texture;
            illustration.gameObject.SetActive(texture != null);
            imagePlaceholder.SetActive(texture == null);
            zoomButton.gameObject.SetActive(texture != null);
            if (texture != null) illustrationAspect.aspectRatio = zoomAspect.aspectRatio = (float)texture.width / texture.height;
            choiceIndex = 0;
            foreach (var button in choices) { button.onClick.RemoveAllListeners(); button.gameObject.SetActive(false); }
            zoomPanel.SetActive(false);
            modal.SetActive(true);
            openedFrame = Time.frameCount;
            VisibilityChanged?.Invoke(true);
        }

        public void AddChoice(string label, Action action, bool enabled = true)
        {
            if (choiceIndex >= choices.Length) throw new InvalidOperationException("O painel não possui espaço para todas as opções.");
            var button = choices[choiceIndex++];
            button.gameObject.SetActive(true);
            button.interactable = enabled;
            button.GetComponentInChildren<TMP_Text>().text = label;
            button.onClick.AddListener(() => action());
        }

        public void SetHud(string nextObjective, string completion)
        {
            objective.text = nextObjective;
            progress.text = completion;
        }

        public void Close()
        {
            if (transitioning) return;
            zoomPanel.SetActive(false);
            modal.SetActive(false);
            VisibilityChanged?.Invoke(false);
        }

        public IEnumerator FadeToBlack(float duration)
        {
            transitioning = true;
            closeButton.interactable = false;
            if (transition == null) yield break;
            transition.blocksRaycasts = true;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                transition.alpha = Mathf.SmoothStep(0, 1, elapsed / duration);
                yield return null;
            }
            transition.alpha = 1;
        }
    }
}
