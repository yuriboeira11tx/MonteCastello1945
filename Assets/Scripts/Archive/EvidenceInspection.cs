using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace FEB.Archive
{
    [RequireComponent(typeof(UnityEngine.UI.RawImage))]
    public sealed class EvidenceInspection : MonoBehaviour, IDragHandler, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Range(256, 2048)] int textureSize = 1024;
        [SerializeField] float rotationSensitivity = .25f;
        [SerializeField] float defaultDistance = 3f;
        const int PreviewLayer = 31;
        UnityEngine.UI.RawImage viewport;
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
            viewport = GetComponent<UnityEngine.UI.RawImage>();
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
            float aspect = viewport.rectTransform.rect.width / Mathf.Max(1, viewport.rectTransform.rect.height);
            renderTexture = new RenderTexture(textureSize, Mathf.RoundToInt(textureSize / aspect), 24, RenderTextureFormat.ARGB32) { name = "Inspecao_Evidencia", useMipMap = false };
            renderTexture.Create();
            previewCamera.targetTexture = renderTexture;
            viewport.texture = renderTexture;
            AddLight("Luz_Principal", new Vector3(-1.5f, 1.5f, -2), new Color(1, .91f, .77f), 8f);
            AddLight("Preenchimento", new Vector3(1.4f, .3f, -1.5f), new Color(.8f, .9f, 1), 4f);
            AddLight("Luz_Verso", new Vector3(0, 1.5f, 1.8f), new Color(1, .94f, .8f), 3f);
        }

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
            light.cullingMask = 1 << PreviewLayer;
        }

        public bool Show(HistoricalEvidence evidence)
        {
            Hide();
            if (evidence.inspectionPrefab == null) return false;
            EnsureStudio();
            studio.SetActive(true);
            gameObject.SetActive(true);
            pivot.localRotation = Quaternion.identity;
            instance = Instantiate(evidence.inspectionPrefab, pivot, false);
            foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Hide(); return false; }
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
        public void OnDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Rotate(data.delta); }
        public void OnScroll(PointerEventData data) => Zoom(Mathf.Clamp(data.scrollDelta.y, -1, 1));
        public void OnPointerEnter(PointerEventData data) => pointerInside = true;
        public void OnPointerExit(PointerEventData data) => pointerInside = false;

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
            pointerInside = false;
            if (instance != null) { instance.SetActive(false); Destroy(instance); instance = null; }
            if (studio != null) studio.SetActive(false);
            gameObject.SetActive(false);
        }

        void OnDisable() { if (studio != null) studio.SetActive(false); }
        void OnDestroy()
        {
            if (studio != null) Destroy(studio);
            if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); }
        }
    }
}
