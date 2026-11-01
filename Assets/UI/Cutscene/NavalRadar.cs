using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    [UxmlElement]
    public partial class NavalRadar : VisualElement
    {
        readonly Vector2[] positions = new Vector2[4];
        readonly Vector2[] directions = new Vector2[4];
        int contactCount;

        public NavalRadar() => generateVisualContent += Draw;

        public void SetContacts(Vector2[] points, Vector2[] headings, int count)
        {
            contactCount = Mathf.Min(count, positions.Length);
            for (int i = 0; i < contactCount; i++)
            {
                positions[i] = points[i];
                directions[i] = headings[i];
            }
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            if (contentRect.width < 1 || contentRect.height < 1) return;
            var p = context.painter2D;
            Vector2 center = contentRect.size * .5f;
            float radius = Mathf.Min(center.x, center.y) - 6;
            p.fillColor = new Color(.015f, .045f, .023f, .94f);
            Circle(p, center, radius);
            p.Fill();
            p.lineWidth = 1;
            p.strokeColor = new Color(.28f, .57f, .34f, .55f);
            for (int i = 1; i <= 3; i++) { Circle(p, center, radius * i / 3); p.Stroke(); }
            Line(p, center - Vector2.right * radius, center + Vector2.right * radius);
            Line(p, center - Vector2.up * radius, center + Vector2.up * radius);

            float sweep = Time.unscaledTime * 65;
            for (int i = 12; i >= 0; i--)
            {
                float angle = (sweep - i * 2) * Mathf.Deg2Rad;
                p.strokeColor = new Color(.35f, 1, .48f, (1 - i / 13f) * .32f);
                Line(p, center, center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * radius);
            }

            p.fillColor = new Color(.72f, 1, .75f);
            p.BeginPath();
            p.MoveTo(center + new Vector2(0, -10));
            p.LineTo(center + new Vector2(5, 7));
            p.LineTo(center + new Vector2(-5, 7));
            p.ClosePath();
            p.Fill();

            for (int i = 0; i < contactCount; i++)
            {
                if (positions[i].sqrMagnitude > 1) continue;
                var point = center + positions[i] * radius;
                float proximity = 1 - positions[i].magnitude;
                p.strokeColor = new Color(1, .52f, .28f, .85f);
                p.lineWidth = 2;
                Line(p, point - directions[i] * 14, point);
                p.fillColor = new Color(1, .55f, .28f, .8f + .2f * Mathf.Sin(Time.unscaledTime * 5));
                Circle(p, point, 3.5f + proximity * 1.5f);
                p.Fill();
            }
        }

        static void Circle(Painter2D p, Vector2 center, float radius)
        {
            p.BeginPath();
            p.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
        }

        static void Line(Painter2D p, Vector2 from, Vector2 to)
        {
            p.BeginPath();
            p.MoveTo(from);
            p.LineTo(to);
            p.Stroke();
        }
    }
}