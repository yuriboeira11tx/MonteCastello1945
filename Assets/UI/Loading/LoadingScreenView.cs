using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    public sealed class LoadingScreenView : IDisposable
    {
        readonly VisualElement screen, smoke;
        readonly Image portrait;
        readonly Label heading, copy;
        readonly IVisualElementScheduledItem smokeAnimation;
        double openedAt;

        public LoadingScreenView(VisualElement root)
        {
            screen = root.Q("loadingRoot");
            portrait = screen.Q<Image>("loadingObject");
            portrait.scaleMode = ScaleMode.ScaleToFit;
            heading = screen.Q<Label>("objectTitle");
            copy = screen.Q<Label>("historicalFact");
            smoke = screen.Q("snakeSmoke");
            screen.RegisterCallback<GeometryChangedEvent>(OnResize);
            smoke.generateVisualContent += DrawSmoke;
            smokeAnimation = smoke.schedule.Execute(smoke.MarkDirtyRepaint).Every(40);
            smokeAnimation.Pause();
        }

        public void Show(Texture preview, string title, string fact)
        {
            portrait.image = preview;
            heading.text = title;
            copy.text = fact;
            SetLayout(screen.contentRect);
            openedAt = Time.realtimeSinceStartupAsDouble;
            smokeAnimation.Resume();
            smoke.MarkDirtyRepaint();
        }

        void OnResize(GeometryChangedEvent evt) => SetLayout(evt.newRect);

        void SetLayout(Rect size)
        {
            if (size.width <= 0 || size.height <= 0) return;
            screen.EnableInClassList("loading-narrow", size.width < size.height);
            screen.EnableInClassList("loading-short", size.height < 530 || size.width < 840);
        }

        void DrawSmoke(MeshGenerationContext context)
        {
            var rect = smoke.contentRect;
            if (rect.width < 1 || rect.height < 1) return;
            float time = (float)(Time.realtimeSinceStartupAsDouble - openedAt);
            DrawWisp(context, rect.size, time, 0, 0.54f);
            DrawWisp(context, rect.size, time, 1.8f, 0.33f);
        }

        // Each ribbon fades across its width and breaks up as it rises from the pipe.
        static void DrawWisp(MeshGenerationContext context, Vector2 size, float time, float phase, float opacity)
        {
            const int steps = 26;
            const int columns = 3;
            var mesh = context.Allocate((steps + 1) * columns, steps * (columns - 1) * 6);
            for (int row = 0; row <= steps; row++)
            {
                float rise = row / (float)steps;
                float wave = Mathf.Sin(rise * 10.8f - time * 1.75f + phase);
                float curl = Mathf.Sin(rise * 19f - time * 2.55f + phase) * 0.29f;
                // The pipe bowl is at 84% of the width of the existing FEB badge.
                float center = size.x * (0.36f + rise * (0.16f + (wave + curl) * 0.19f));
                float halfWidth = size.x * Mathf.Lerp(0.026f, 0.20f, rise);
                float density = opacity * (1 - rise * rise) * (0.72f + 0.28f * wave);
                density *= Mathf.SmoothStep(0, 1, Mathf.Min(1, rise * 12));
                for (int col = 0; col < columns; col++)
                {
                    float x = center + (col - 1) * halfWidth;
                    mesh.SetNextVertex(new Vertex
                    {
                        position = new Vector3(x, size.y * (1 - rise), Vertex.nearZ),
                        tint = new Color(0.85f, 0.84f, 0.79f, col == 1 ? density : 0)
                    });
                }
                if (row == 0) continue;
                for (int col = 0; col < columns - 1; col++)
                {
                    ushort bottom = (ushort)((row - 1) * columns + col);
                    ushort top = (ushort)(bottom + columns);
                    mesh.SetNextIndex(bottom);
                    mesh.SetNextIndex(top);
                    mesh.SetNextIndex((ushort)(bottom + 1));
                    mesh.SetNextIndex((ushort)(bottom + 1));
                    mesh.SetNextIndex(top);
                    mesh.SetNextIndex((ushort)(top + 1));
                }
            }
        }

        public void Dispose()
        {
            smokeAnimation.Pause();
            smoke.generateVisualContent -= DrawSmoke;
            screen.UnregisterCallback<GeometryChangedEvent>(OnResize);
            portrait.image = null;
        }
    }
}
