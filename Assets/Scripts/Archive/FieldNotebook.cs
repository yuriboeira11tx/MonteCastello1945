using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    [Serializable]
    public sealed class NotebookChapter
    {
        public string scene;
        public HistoricalEvidence[] readings;
        public HistoricalEvidence[] fieldEvidence;
    }

    [RequireComponent(typeof(AudioSource))]
    public sealed class FieldNotebook : MonoBehaviour
    {
        const int Width = 1440, Height = 840;
        [SerializeField] GameObject book;
        [SerializeField] Transform leftHinge, rightHinge;
        [SerializeField] MeshRenderer leftPage, rightPage;
        [SerializeField] UIDocument document;
        [SerializeField] PanelSettings panelTemplate;
        [SerializeField] ArchiveCaseDefinition firstCase;
        [SerializeField] HistoricalEvidence[] commonReadings;
        [SerializeField] NotebookChapter[] chapters;
        [SerializeField] Texture2D fieldPhoto, emblem;
        [SerializeField] AudioClip openSound, pageTurnSound, closeSound;

        Camera view;
        AudioSource paperAudio;
        ArchivePlayerInteraction interaction;
        RenderTexture texture;
        PanelSettings panel;
        FieldNotebookPages pages;
        bool raised, holding;
        float opening;
        int openedFrame;

        void Awake()
        {
            paperAudio = GetComponent<AudioSource>();
            view = GetComponentInParent<Camera>();
            interaction = GetComponentInParent<ArchivePlayerInteraction>();
            book.SetActive(false);
        }

        void PreparePanel()
        {
            if (panel != null) return;
            texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Páginas da caderneta", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            panel = Instantiate(panelTemplate);
            panel.targetTexture = texture;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = 1;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            panel.sortingOrder = 30;
            document.panelSettings = panel;
            BindSurface(leftPage, 0);
            BindSurface(rightPage, .5f);
        }

        void BindSurface(Renderer surface, float start)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_BaseMap", texture);
            properties.SetVector("_BaseMap_ST", new Vector4(.5f, 1, start, 0));
            surface.SetPropertyBlock(properties);
        }

        void RaiseBook()
        {
            if (raised) return;
            if (holding) PutAway();
            PreparePanel();
            raised = holding = true;
            opening = 0;
            openedFrame = Time.frameCount;
            interaction.SetReading(true);
            book.SetActive(true);
            panel.SetScreenToPanelSpaceFunction(PickPage);
            pages = new FieldNotebookPages(document.rootVisualElement, firstCase, commonReadings, chapters,
                gameObject.scene.name, fieldPhoto, emblem, interaction.Experience, CloseBook, ExamineFromNotebook, PlayPageTurn);
            PositionBook();
            PlaySound(openSound);
        }

        public void Open() => ShowIndex();

        public void ShowIndex()
        {
            RaiseBook();
            pages.ShowIndex();
        }

        public void ShowEvidenceIndex(HistoricalEvidence selected = null)
        {
            bool wasOpen = raised;
            RaiseBook();
            pages.ShowEvidenceIndex(selected, wasOpen);
        }

        public void ShowActivity(string kicker, string title, string body, bool isPause = false)
        {
            bool wasOpen = raised;
            RaiseBook();
            pages.ShowActivity(kicker, title, body, isPause, wasOpen);
        }

        public void AddChoice(string text, Action action, bool enabled = true) =>
            pages?.AddChoice(text, action, enabled);

        void ExamineFromNotebook(HistoricalEvidence evidence)
        {
            if (!BeginClose()) return;
            PutAway();
            interaction.Experience.OpenEvidence(evidence, fromNotebook: true);
        }

        public void PlayPageTurn() => PlaySound(pageTurnSound);
        internal void PlayCloseSound() => PlaySound(closeSound);

        void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            paperAudio.clip = clip;
            paperAudio.Play();
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (!raised || Time.frameCount == openedFrame || keys == null) return;
            if (keys.tabKey.wasPressedThisFrame) CloseBook();
            else if (keys.escapeKey.wasPressedThisFrame)
            {
                if (interaction.Experience != null && !pages.IsPausePage) interaction.Experience.OpenPause();
                else CloseBook();
            }
        }

        void LateUpdate()
        {
            if (!holding) return;
            opening = Mathf.MoveTowards(opening, raised ? 1 : 0, Time.unscaledDeltaTime * 3.8f);
            PositionBook();
            if (!raised && opening <= 0) PutAway();
        }

        void PositionBook()
        {
            float t = Mathf.SmoothStep(0, 1, opening);
            float distance = Mathf.Max(.24f, view.nearClipPlane + .17f);
            float frustum = 2 * distance * Mathf.Tan(.5f * view.fieldOfView * Mathf.Deg2Rad);
            float width = Mathf.Min(frustum * view.aspect * .91f, frustum * .83f * Width / Height);
            transform.localScale = Vector3.one * width;
            transform.localPosition = new Vector3(0, frustum * Mathf.Lerp(-1.0f, -.045f, t), distance);
            transform.localRotation = Quaternion.Euler(Mathf.Lerp(33, 7, t), 0, Mathf.Lerp(-9, -.6f, t));
            float angle = Mathf.Lerp(65, 2.6f, t);
            leftHinge.localRotation = Quaternion.Euler(0, -angle, 0);
            rightHinge.localRotation = Quaternion.Euler(0, angle, 0);
        }

        Vector2 PickPage(Vector2 screen)
        {
            if (!raised || opening < .9f) return new Vector2(float.NaN, float.NaN);
            var ray = view.ScreenPointToRay(new Vector2(screen.x, Screen.height - screen.y));
            if (PickHalf(ray, leftPage.transform, 0, out var pixel) ||
                PickHalf(ray, rightPage.transform, 1, out pixel)) return pixel;
            return new Vector2(float.NaN, float.NaN);
        }

        static bool PickHalf(Ray ray, Transform surface, int side, out Vector2 pixel)
        {
            pixel = default;
            var plane = new Plane(surface.forward, surface.position);
            if (!plane.Raycast(ray, out float hit)) return false;
            Vector3 p = surface.InverseTransformPoint(ray.GetPoint(hit));
            if (Mathf.Abs(p.x) > .5f || Mathf.Abs(p.y) > .5f) return false;
            pixel = new Vector2((side + p.x + .5f) * Width / 2f, (.5f - p.y) * Height);
            return true;
        }

        bool BeginClose()
        {
            if (!raised || !pages.FlushNote()) return false;
            raised = false;
            PlayCloseSound();
            return true;
        }

        public void CloseBook() => BeginClose();

        internal void StoreForTransition()
        {
            if (BeginClose()) PutAway();
        }

        void PutAway()
        {
            raised = holding = false;
            pages?.Unbind();
            pages = null;
            book.SetActive(false);
            interaction.SetReading(false);
        }

        void OnApplicationPause(bool paused) { if (paused) pages?.FlushNote(); }
        void OnApplicationQuit() => pages?.FlushNote();

        void OnDisable()
        {
            if (!holding) return;
            pages?.FlushNote();
            PutAway();
        }

        void OnDestroy()
        {
            if (panel != null) { panel.SetScreenToPanelSpaceFunction(null); Destroy(panel); }
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}

