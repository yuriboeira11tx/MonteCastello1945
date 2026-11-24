using System;
using FEB.Archive;
using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    [RequireComponent(typeof(UIDocument), typeof(AudioSource))]
    public sealed class GameCreditsView : MonoBehaviour
    {
        const float EndPadding = 55f;
        VisualElement root, viewport, roll;
        AudioSource musicSource;
        GameCreditsDefinition definition;
        Action completed;
        float offset, elapsed, endingElapsed;
        bool initialized, ending;

        public bool IsShowing { get; private set; }

        void OnEnable()
        {
            musicSource = GetComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0;
            musicSource.dopplerLevel = 0;
            musicSource.Stop();
            if (BindDocument()) root.style.display = DisplayStyle.None;
        }

        bool BindDocument()
        {
            var document = GetComponent<UIDocument>();
            var documentRoot = document.rootVisualElement;
            var screen = documentRoot?.Q("creditsRoot");
            if (screen == null) return false;
            if (root == screen) return viewport != null && roll != null;
            if (viewport != null) viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometry);
            documentRoot.StretchToParentSize();
            documentRoot.pickingMode = PickingMode.Ignore;
            root = screen;
            viewport = root.Q("creditsViewport");
            roll = root.Q("creditsRoll");
            if (viewport == null || roll == null) return false;
            viewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometry);
            return true;
        }

        public void Show(GameCreditsDefinition credits, Action onCompleted)
        {
            if (credits == null) throw new ArgumentNullException(nameof(credits));
            Hide();
            if (!BindDocument())
                throw new InvalidOperationException("Associe CreditosFinais.uxml ao UIDocument dos créditos.");

            musicSource = GetComponent<AudioSource>();
            definition = credits;
            completed = onCompleted;
            elapsed = endingElapsed = 0;
            initialized = ending = false;
            roll.Clear();
            AddText(credits.academicTitle, "credits-title");
            if (credits.sections != null)
                foreach (var section in credits.sections)
                {
                    if (section == null) continue;
                    var group = new VisualElement { pickingMode = PickingMode.Ignore };
                    group.AddToClassList("credits-section");
                    if (!string.IsNullOrWhiteSpace(section.heading))
                    {
                        var heading = MakeLabel(section.heading, "credits-heading");
                        group.Add(heading);
                    }
                    if (!string.IsNullOrWhiteSpace(section.text))
                        foreach (var line in section.text.Split('\n'))
                            if (!string.IsNullOrWhiteSpace(line))
                                group.Add(MakeLabel(line.TrimEnd('\r'), "credits-names"));
                    if (group.childCount > 0) roll.Add(group);
                }

            root.style.display = DisplayStyle.Flex;
            root.style.opacity = 1;
            roll.style.visibility = Visibility.Hidden;
            roll.style.top = 0;
            IsShowing = true;
            SetLayout(viewport.contentRect.size);

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0;
            musicSource.dopplerLevel = 0;
            musicSource.clip = credits.music;
            musicSource.volume = credits.musicFadeSeconds > 0 ? 0 : Mathf.Clamp01(credits.volume);
            if (credits.music != null) musicSource.Play();
        }

        static Label MakeLabel(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false };
            label.AddToClassList(className);
            return label;
        }

        void AddText(string text, string className)
        {
            if (!string.IsNullOrWhiteSpace(text)) roll.Add(MakeLabel(text, className));
        }

        void OnViewportGeometry(GeometryChangedEvent evt)
        {
            SetLayout(evt.newRect.size);
            if (initialized && evt.oldRect.height > 0)
                offset += (evt.newRect.height - evt.oldRect.height) * 0.88f;
        }

        void SetLayout(Vector2 size)
        {
            if (root == null || size.x <= 0 || size.y <= 0) return;
            root.EnableInClassList("credits-compact", size.x < 1000 || size.y < 650);
        }

        void Update()
        {
            if (!IsShowing || definition == null || roll == null || viewport == null) return;
            float height = viewport.contentRect.height;
            float contentHeight = roll.layout.height;
            if (float.IsNaN(height) || float.IsInfinity(height) || float.IsNaN(contentHeight)
                || float.IsInfinity(contentHeight) || height <= 0 || contentHeight <= 0) return;
            if (!initialized)
            {
                offset = height * 0.88f;
                roll.style.top = offset;
                roll.style.visibility = Visibility.Visible;
                initialized = true;
            }

            float delta = Time.unscaledDeltaTime;
            float fade = Mathf.Max(0, definition.musicFadeSeconds);
            elapsed += delta;
            if (!ending)
            {
                offset -= Mathf.Max(1, definition.scrollSpeed) * delta;
                roll.style.top = offset;
                musicSource.volume = Mathf.Clamp01(definition.volume) * (fade > 0 ? Mathf.Clamp01(elapsed / fade) : 1);
                if (offset + contentHeight < -EndPadding) ending = true;
            }
            else
            {
                endingElapsed += delta;
                float tail = Mathf.Max(0.5f, fade);
                musicSource.volume = Mathf.Clamp01(definition.volume) * (1 - Mathf.Clamp01(endingElapsed / tail));
                if (endingElapsed >= tail) Complete();
            }
        }

        void Complete()
        {
            if (!IsShowing) return;
            var notify = completed;
            Hide();
            notify?.Invoke();
        }

        public void Hide()
        {
            IsShowing = false;
            completed = null;
            definition = null;
            initialized = ending = false;
            if (root != null) root.style.display = DisplayStyle.None;
            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }
        }

        void OnDisable()
        {
            Hide();
            if (viewport != null) viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometry);
            root = viewport = roll = null;
        }
    }
}
