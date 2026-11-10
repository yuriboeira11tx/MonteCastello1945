using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    public sealed class ArchiveChapterTransition : MonoBehaviour
    {
        [SerializeField] UIDocument screen;
        VisualElement eyelids;
        float closure;

        public void Play(string title, Action onCovered, Action onFinished)
        {
            if (eyelids != null) return;
            StartCoroutine(ShowChapter(title, onCovered, onFinished));
        }

        IEnumerator ShowChapter(string title, Action onCovered, Action onFinished)
        {
            eyelids = new VisualElement { name = "chapterTransition" };
            eyelids.StretchToParentSize();
            eyelids.style.alignItems = Align.Center;
            eyelids.style.justifyContent = Justify.Center;
            eyelids.generateVisualContent += PaintEyelids;
            screen.rootVisualElement.Add(eyelids);
            var label = new Label(title) { name = "chapterName", pickingMode = PickingMode.Ignore };
            label.AddToClassList("stencil");
            label.style.color = new Color(.94f, .91f, .81f);
            label.style.fontSize = 68;
            label.style.opacity = 0;
            eyelids.Add(label);
            yield return Animate(.65f, t => SetClosure(t));
            onCovered?.Invoke();
            yield return Animate(.3f, t => label.style.opacity = t);
            yield return new WaitForSecondsRealtime(1.35f);
            yield return Animate(.3f, t => label.style.opacity = 1 - t);
            yield return Animate(.85f, t => SetClosure(1 - t));
            Clear();
            onFinished?.Invoke();
        }

        static IEnumerator Animate(float duration, Action<float> apply)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < duration)
            {
                apply(Mathf.SmoothStep(0, 1, (Time.unscaledTime - start) / duration));
                yield return null;
            }
            apply(1);
        }

        void SetClosure(float value)
        {
            closure = value;
            eyelids.MarkDirtyRepaint();
        }

        void PaintEyelids(MeshGenerationContext context)
        {
            var rect = eyelids.contentRect;
            if (closure <= 0 || rect.width <= 0 || rect.height <= 0) return;
            var pen = context.painter2D;
            pen.fillColor = Color.black;
            float middle = rect.height * .52f;
            float bow = rect.height * .20f * Mathf.Sin(Mathf.PI * closure);
            float top = Mathf.Lerp(-2, middle + 2, closure);
            float bottom = Mathf.Lerp(rect.height + 2, middle - 2, closure);
            DrawLid(pen, rect.width, 0, top, top - bow);
            DrawLid(pen, rect.width, rect.height, bottom, bottom + bow);
        }

        static void DrawLid(Painter2D pen, float width, float edge, float end, float curve)
        {
            pen.BeginPath();
            pen.MoveTo(new Vector2(0, edge));
            pen.LineTo(new Vector2(0, end));
            pen.BezierCurveTo(new Vector2(width * .27f, curve), new Vector2(width * .73f, curve),
                new Vector2(width, end));
            pen.LineTo(new Vector2(width, edge));
            pen.ClosePath();
            pen.Fill();
        }

        void Clear()
        {
            if (eyelids == null) return;
            eyelids.generateVisualContent -= PaintEyelids;
            eyelids.RemoveFromHierarchy();
            eyelids = null;
            closure = 0;
        }

        void OnDisable()
        {
            StopAllCoroutines();
            Clear();
        }
    }
}
