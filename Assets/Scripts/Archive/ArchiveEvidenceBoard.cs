using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace FEB.Archive
{
    [DisallowMultipleComponent]
    public sealed class ArchiveEvidenceBoard : MonoBehaviour
    {
        [SerializeField] Transform surface;
        [SerializeField] Vector2 size = new Vector2(3.4f, 1.53f);
        [SerializeField] Camera playerCamera;
        [SerializeField] ArchiveEvidenceBoardView view;
        [SerializeField] UIDocument exhibitTitle, exhibitCards;

        Camera boardCamera;
        Action closeInteraction;
        float lastAspect;

        public void SetExhibit(ArchiveCaseDefinition chapter, IEnumerable<string> registered, bool organized, bool linked)
        {
            var heading = exhibitTitle.rootVisualElement.Q<Label>("text");
            heading.text = chapter.title.ToUpperInvariant();
            var root = exhibitCards.rootVisualElement;
            var photos = root.Q("photos");
            photos.Clear();
            root.Q("savedThreads")?.RemoveFromHierarchy();
            var pins = new List<VisualElement>();
            var items = registered.Select(id => Array.Find(chapter.evidence, item => item != null && item.id == id))
                .Where(item => item != null);
            if (organized) items = items.OrderBy(item => item.chronologyOrder);
            foreach (var item in items)
            {
                int i = Array.IndexOf(chapter.evidence, item);
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                card.AddToClassList("wall-card");
                var image = new Image { image = item.image, scaleMode = ScaleMode.ScaleToFit };
                card.Add(image);
                var caption = chapter.fieldStudy && organized && i < chapter.boardThemes.Length
                    ? chapter.boardThemes[i] : item.title;
                card.Add(new Label(caption));
                var pin = new VisualElement();
                pin.AddToClassList("wall-pin");
                card.Add(pin);
                photos.Add(card);
                if (linked && chapter.boardSources.Contains(item)) pins.Add(pin);
            }
            var note = root.Q<Label>("wallNote");
            bool showConclusion = linked && !string.IsNullOrEmpty(chapter.boardConclusion);
            note.text = showConclusion ? chapter.boardConclusion : "As reproduções são afixadas ao registrar cada fonte.";
            note.style.display = showConclusion || photos.childCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!showConclusion || pins.Count == 0) return;
            var wires = new VisualElement { name = "savedThreads", pickingMode = PickingMode.Ignore };
            wires.AddToClassList("wall-threads");
            root.Add(wires);
            wires.generateVisualContent += context =>
            {
                var pen = context.painter2D;
                var end = note.ChangeCoordinatesTo(wires, new Vector2(note.contentRect.center.x, 0));
                pen.strokeColor = new Color32(122, 48, 37, 255);
                pen.lineWidth = 2.4f;
                foreach (var pin in pins)
                {
                    pen.BeginPath();
                    pen.MoveTo(pin.ChangeCoordinatesTo(wires, pin.contentRect.center));
                    pen.LineTo(end);
                    pen.Stroke();
                }
            };
        }

        public void Show(ArchiveBoardMode mode, ArchiveCaseDefinition chapter, Action onSolved, Action onClosed, Action onIncorrect = null)
        {
            if (closeInteraction != null) return;
            closeInteraction = onClosed;
            if (boardCamera == null)
            {
                var cameraObject = new GameObject("Câmera do quadro");
                cameraObject.transform.SetParent(surface, false);
                boardCamera = cameraObject.AddComponent<Camera>();
            }
            boardCamera.CopyFrom(playerCamera);
            var playerSettings = playerCamera.GetUniversalAdditionalCameraData();
            var settings = boardCamera.GetUniversalAdditionalCameraData();
            settings.renderPostProcessing = playerSettings.renderPostProcessing;
            settings.volumeLayerMask = playerSettings.volumeLayerMask;
            boardCamera.transform.SetPositionAndRotation(surface.position - surface.forward * 0.8f, surface.rotation);
            boardCamera.orthographic = true;
            boardCamera.nearClipPlane = 0.035f;
            boardCamera.ResetProjectionMatrix();
            playerCamera.enabled = false;
            boardCamera.enabled = true;
            SetWallVisible(false);
            view.Show(mode, chapter, () => { Close(); onSolved(); }, Close, onIncorrect);
            FrameBoard();
        }

        void LateUpdate()
        {
            if (boardCamera != null && boardCamera.enabled && !Mathf.Approximately(lastAspect, boardCamera.aspect))
                FrameBoard();
        }

        void FrameBoard()
        {
            lastAspect = boardCamera.aspect;
            float height = Mathf.Max(size.y / 0.90f, size.x / (lastAspect * 0.93f));
            boardCamera.orthographicSize = height * 0.5f;
            float w = size.x / (height * lastAspect);
            float h = size.y / height;
            view.SetViewport(new Rect((1f - w) * 0.5f, (1f - h) * 0.5f, w, h));
        }

        void SetWallVisible(bool show)
        {
            var visibility = show ? Visibility.Visible : Visibility.Hidden;
            if (exhibitTitle != null && exhibitTitle.rootVisualElement != null)
                exhibitTitle.rootVisualElement.style.visibility = visibility;
            if (exhibitCards != null && exhibitCards.rootVisualElement != null)
                exhibitCards.rootVisualElement.style.visibility = visibility;
        }

        void Close()
        {
            if (closeInteraction == null) return;
            var closed = closeInteraction;
            closeInteraction = null;
            if (view != null) view.Hide();
            if (boardCamera != null) boardCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            SetWallVisible(true);
            closed();
        }

        void OnDisable()
        {
            if (closeInteraction != null && view != null && playerCamera != null) Close();
        }
    }
}
