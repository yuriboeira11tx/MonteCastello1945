using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CutsceneView : MonoBehaviour
    {
        [SerializeField] RenderTexture videoTexture;
        VisualElement videoGroup;
        VisualElement titleGroup;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            videoGroup = root.Q("videoGroup");
            titleGroup = root.Q("titleGroup");
            var video = root.Q<Image>("archiveVideo");
            video.image = videoTexture;
            video.scaleMode = ScaleMode.ScaleAndCrop;
            SetVideoOpacity(0);
            SetTitleOpacity(0);
        }

        public void SetVideoOpacity(float value)
        {
            if (videoGroup != null) videoGroup.style.opacity = Mathf.Clamp01(value);
        }

        public void SetTitleOpacity(float value)
        {
            if (titleGroup != null) titleGroup.style.opacity = Mathf.Clamp01(value);
        }
    }
}

