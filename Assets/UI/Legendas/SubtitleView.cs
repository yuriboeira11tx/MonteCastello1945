using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class SubtitleView : MonoBehaviour
    {
        PanelRenderer subtitlePanel;
        VisualElement screen;
        Label lineLabel;
        string currentText;
        float scale = 1f;
        float blackout;

        void OnEnable()
        {
            subtitlePanel = GetComponent<PanelRenderer>();
            subtitlePanel.RegisterUIReloadCallback(Bind);
        }

        void OnDisable()
        {
            subtitlePanel.UnregisterUIReloadCallback(Bind);
            Hide();
            SetBlackout(0);
            screen = null;
            lineLabel = null;
        }

        void Bind(PanelRenderer panel, VisualElement root, int version)
        {
            root.style.position = Position.Absolute;
            root.StretchToParentSize();
            root.pickingMode = PickingMode.Ignore;
            root.focusable = false;
            screen = root.Q("subtitleScreen");
            SetBlackout(blackout);
            lineLabel = root.Q<Label>("subtitleLine");
            if (lineLabel == null)
            {
                if (panel.visualTreeAsset != null)
                    Debug.LogError("O painel de legendas precisa do documento Legendas.uxml.", this);
                return;
            }
            ApplyLine();
        }

        public void Show(string text, float textScale)
        {
            currentText = text;
            scale = float.IsNaN(textScale) ? 1f : Mathf.Clamp(textScale, 0.75f, 2f);
            ApplyLine();
        }

        public void Hide() => Show(null, scale);

        public void SetBlackout(float opacity)
        {
            blackout = Mathf.Clamp01(opacity);
            if (screen != null) screen.style.backgroundColor = new Color(0, 0, 0, blackout);
        }

        void ApplyLine()
        {
            if (lineLabel == null) return;
            lineLabel.style.fontSize = 32f * scale;
            lineLabel.text = currentText ?? string.Empty;
            lineLabel.EnableInClassList("hidden", string.IsNullOrWhiteSpace(currentText));
        }
    }
}
