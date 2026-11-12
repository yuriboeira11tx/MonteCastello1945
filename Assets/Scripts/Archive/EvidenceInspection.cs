using UnityEngine;
using UnityEngine.UIElements;
using FEB.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FEB.Archive
{
    public sealed class EvidenceInspection : MonoBehaviour
    {
        [SerializeField, Range(256, 2048)] int textureSize = 1024;
        [SerializeField] float rotationSensitivity = .25f;
        [SerializeField] float defaultDistance = 3f;
        const int PreviewLayer = 31;
        const uint StudioLightMask = 1u << 7;
        Image viewport;
        InspectionManipulator manipulator;
        GameObject studio;
        Transform pivot;
        GameObject instance;
        Camera previewCamera;
        RenderTexture renderTexture;
        Quaternion targetRotation;
        Quaternion initialRotation;
        float distance;
        float targetDistance;
        bool pointerInside;

        void EnsureStudio()
        {
            if (studio != null) return;
            studio = new GameObject("Estudio_Inspecao");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(studio, gameObject.scene);
            studio.transform.position = new Vector3(0, -1000, 0);
            pivot = new GameObject("Objeto").transform;
            pivot.SetParent(studio.transform, false);
            var cameraObject = new GameObject("Camera_Inspecao");
            cameraObject.transform.SetParent(studio.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.fieldOfView = 32;
            previewCamera.nearClipPlane = .05f;
            previewCamera.farClipPlane = 10;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.volumeLayerMask = 0;
            ResizePreview(viewport.contentRect.size);
            AddLight("Luz_Principal", new Vector3(-1.5f, 1.5f, -2), new Color(1, .91f, .77f), 2.6f);
            AddLight("Preenchimento", new Vector3(1.4f, .3f, -1.5f), new Color(.8f, .9f, 1), 1.15f);
            AddLight("Luz_Verso", new Vector3(0, 1.5f, 1.8f), new Color(1, .94f, .8f), 2f);
        }

        void ResizePreview(Vector2 size)
        {
            if (previewCamera == null || !float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x < 1 || size.y < 1) return;
            float aspect = size.x / size.y;
            int width = Mathf.Max(1, Mathf.RoundToInt(textureSize * Mathf.Min(aspect, 1)));
            int height = Mathf.Max(1, Mathf.RoundToInt(textureSize * Mathf.Min(1 / aspect, 1)));
            if (renderTexture != null && renderTexture.width == width && renderTexture.height == height) return;
            if (renderTexture != null)
            {
                previewCamera.targetTexture = null;
                renderTexture.Release();
                Destroy(renderTexture);
            }
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Inspecao_Evidencia", useMipMap = false };
            renderTexture.Create();
            previewCamera.targetTexture = renderTexture;
            previewCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(32, Mathf.Min(aspect, 1));
            viewport.image = renderTexture;
            previewCamera.enabled = true;
        }

        void OnViewportGeometry(GeometryChangedEvent evt) => ResizePreview(evt.newRect.size);

        void AddLight(string label, Vector3 position, Color color, float intensity)
        {
            var lightObject = new GameObject(label);
            lightObject.transform.SetParent(studio.transform, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 8;
            light.shadows = LightShadows.None;
            SetStudioLightMask(light);
        }

        static void SetStudioLightMask(Light light)
        {
            light.cullingMask = 1 << PreviewLayer;
            light.GetUniversalAdditionalLightData().renderingLayers = StudioLightMask;
        }

        static void LightPreviewRenderers(Renderer[] renderers)
        {
            // A camada separa as luzes diretas. A sonda neutra substitui a luz difusa do céu.
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.45f, .45f, .45f));
            var probe = new[] { ambient };
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                renderer.renderingLayerMask = StudioLightMask;
                renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
                renderer.GetPropertyBlock(properties);
                properties.CopySHCoefficientArraysFrom(probe);
                renderer.SetPropertyBlock(properties);
            }
        }

        public bool Show(HistoricalEvidence evidence)
        {
            Hide();
            if (evidence.inspectionPrefab == null) return false;
            EnsureStudio();
            studio.SetActive(true);
            previewCamera.enabled = renderTexture != null;
            ScreenUI.Show(viewport, true);
            pivot.localRotation = Quaternion.identity;
            instance = Instantiate(evidence.inspectionPrefab, pivot, false);
            foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Hide(); return false; }
            LightPreviewRenderers(renderers);
            foreach (var light in instance.GetComponentsInChildren<Light>(true)) SetStudioLightMask(light);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = 1.35f / Mathf.Max(.001f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            Vector3 center = pivot.InverseTransformPoint(bounds.center);
            instance.transform.localScale *= scale;
            instance.transform.localPosition -= center * scale;
            initialRotation = Quaternion.Euler(evidence.inspectionRotation);
            ResetView();
            return true;
        }

        public void ResetView()
        {
            if (pivot == null) return;
            targetRotation = pivot.localRotation = initialRotation;
            targetDistance = distance = defaultDistance;
            previewCamera.transform.localPosition = new Vector3(0, 0, -distance);
        }

        public void Rotate(Vector2 delta)
        {
            targetRotation = Quaternion.AngleAxis(-delta.x * rotationSensitivity, Vector3.up)
                * Quaternion.AngleAxis(delta.y * rotationSensitivity, Vector3.right) * targetRotation;
        }

        public void Zoom(float amount) => targetDistance = Mathf.Clamp(targetDistance - amount * .2f, 1.65f, 4.8f);
        public void Initialize(Image image)
        {
            Detach();
            viewport = image;
            viewport.scaleMode = ScaleMode.ScaleToFit;
            viewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometry);
            if (renderTexture != null) viewport.image = renderTexture;
            manipulator = new InspectionManipulator(this);
            viewport.AddManipulator(manipulator);
            Hide();
        }

        public void Detach()
        {
            if (viewport != null)
            {
                viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometry);
                if (manipulator != null) viewport.RemoveManipulator(manipulator);
            }
            manipulator = null;
            Hide();
            if (viewport != null) viewport.image = null;
            viewport = null;
        }

        sealed class InspectionManipulator : PointerManipulator
        {
            readonly EvidenceInspection owner;
            int pointer = -1;
            public InspectionManipulator(EvidenceInspection owner) => this.owner = owner;
            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(Down);
                target.RegisterCallback<PointerMoveEvent>(Move);
                target.RegisterCallback<PointerUpEvent>(Up);
                target.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
                target.RegisterCallback<WheelEvent>(Wheel);
                target.RegisterCallback<PointerEnterEvent>(Enter);
                target.RegisterCallback<PointerLeaveEvent>(Leave);
            }
            protected override void UnregisterCallbacksFromTarget()
            {
                if (pointer >= 0 && target.HasPointerCapture(pointer)) target.ReleasePointer(pointer);
                target.UnregisterCallback<PointerDownEvent>(Down);
                target.UnregisterCallback<PointerMoveEvent>(Move);
                target.UnregisterCallback<PointerUpEvent>(Up);
                target.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);
                target.UnregisterCallback<WheelEvent>(Wheel);
                target.UnregisterCallback<PointerEnterEvent>(Enter);
                target.UnregisterCallback<PointerLeaveEvent>(Leave);
            }
            void Down(PointerDownEvent evt) { if (evt.button != 0) return; pointer = evt.pointerId; target.CapturePointer(pointer); evt.StopPropagation(); }
            void Move(PointerMoveEvent evt) { if (pointer != evt.pointerId || !target.HasPointerCapture(pointer)) return; owner.Rotate(new Vector2(evt.deltaPosition.x, -evt.deltaPosition.y)); evt.StopPropagation(); }
            void Up(PointerUpEvent evt) { if (evt.pointerId != pointer) return; target.ReleasePointer(pointer); pointer = -1; evt.StopPropagation(); }
            void CaptureOut(PointerCaptureOutEvent evt) => pointer = -1;
            void Wheel(WheelEvent evt) { owner.Zoom(-Mathf.Clamp(evt.delta.y, -1, 1)); evt.StopPropagation(); }
            void Enter(PointerEnterEvent evt) => owner.pointerInside = true;
            void Leave(PointerLeaveEvent evt) => owner.pointerInside = false;
            public void Cancel()
            {
                if (pointer >= 0 && target.HasPointerCapture(pointer)) target.ReleasePointer(pointer);
                pointer = -1;
            }
        }

        void Update()
        {
            if (instance == null) return;
            if (pointerInside && Keyboard.current != null)
            {
                if (Keyboard.current.rKey.wasPressedThisFrame) ResetView();
                float x = (Keyboard.current.rightArrowKey.isPressed ? 1 : 0) - (Keyboard.current.leftArrowKey.isPressed ? 1 : 0);
                float y = (Keyboard.current.downArrowKey.isPressed ? 1 : 0) - (Keyboard.current.upArrowKey.isPressed ? 1 : 0);
                Rotate(new Vector2(x, y) * (220 * Time.unscaledDeltaTime));
            }
            float blend = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
            pivot.localRotation = Quaternion.Slerp(pivot.localRotation, targetRotation, blend);
            distance = Mathf.Lerp(distance, targetDistance, blend);
            previewCamera.transform.localPosition = new Vector3(0, 0, -distance);
        }

        public void Hide()
        {
            manipulator?.Cancel();
            pointerInside = false;
            if (instance != null) { instance.SetActive(false); Destroy(instance); instance = null; }
            if (studio != null) studio.SetActive(false);
            if (viewport != null) ScreenUI.Show(viewport, false);
        }

        void OnDisable() { if (studio != null) studio.SetActive(false); }
        void OnDestroy()
        {
            if (studio != null) Destroy(studio);
            if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); }
        }
    }
}
