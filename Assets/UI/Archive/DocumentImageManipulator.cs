using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    internal sealed class DocumentImageManipulator : PointerManipulator
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
            Release();
            zoom = 1;
            offset = Vector2.zero;
            Apply();

            target.UnregisterCallback<PointerDownEvent>(Down);
            target.UnregisterCallback<PointerMoveEvent>(Move);
            target.UnregisterCallback<PointerUpEvent>(Up);
            target.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);
            target.UnregisterCallback<WheelEvent>(Wheel);
        }

        void Down(PointerDownEvent evt)
        {
            if (evt.button != 0 || pointer != -1) return;
            pointer = evt.pointerId;
            target.CapturePointer(pointer);
            evt.StopPropagation();
        }

        void Move(PointerMoveEvent evt)
        {
            if (pointer != evt.pointerId) return;
            offset += new Vector2(evt.deltaPosition.x, evt.deltaPosition.y);
            Apply();
        }

        void Up(PointerUpEvent evt)
        {
            if (evt.pointerId == pointer) Release();
        }

        void CaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == pointer) pointer = -1;
        }

        void Wheel(WheelEvent evt)
        {
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-evt.delta.y * .12f), 1, 6);
            Apply();
            evt.StopPropagation();
        }

        void Release()
        {
            int id = pointer;
            pointer = -1;
            if (id >= 0 && target.HasPointerCapture(id)) target.ReleasePointer(id);
        }

        void Apply()
        {
            var bounds = target.contentRect.size * ((zoom - 1) * .5f);
            offset.x = Mathf.Clamp(offset.x, -Mathf.Max(0, bounds.x), Mathf.Max(0, bounds.x));
            offset.y = Mathf.Clamp(offset.y, -Mathf.Max(0, bounds.y), Mathf.Max(0, bounds.y));
            image.style.scale = new Scale(new Vector3(zoom, zoom, 1));
            image.style.translate = new Translate(offset.x, offset.y);
        }
    }
}
