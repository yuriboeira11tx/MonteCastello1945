using UnityEngine.UIElements;

namespace FEB.UI
{
    public static class ScreenUI
    {
        public static void FitText(TextElement element, int minimumSize, int maximumSize)
        {
            element.schedule.Execute(() =>
            {
                var area = element.contentRect;
                if (area.width <= 0 || area.height <= 0) return;
                int size = maximumSize;
                for (; size > minimumSize; size--)
                {
                    var measured = element.MeasureTextSize($"<size={size}px>{element.text}</size>",
                        area.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                    if (measured.y <= area.height) break;
                }
                element.style.fontSize = size;
            });
        }

        public static void Show(VisualElement element, bool visible) => element.EnableInClassList("hidden", !visible);
        public static bool IsVisible(VisualElement element) => element != null && !element.ClassListContains("hidden");
    }
}

