using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CutsceneView : MonoBehaviour
    {
        [SerializeField] RenderTexture videoTexture;
        Image video, navigation, stamp;
        NavalRadar radar;
        VisualElement outcome, navigationPanel, videoPanel, finalFade;
        Label message;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            video = root.Q<Image>("archiveVideo");
            video.image = videoTexture;
            video.scaleMode = ScaleMode.ScaleToFit;
            navigation = root.Q<Image>("navigationView");
            navigationPanel = navigation.parent;
            videoPanel = video.parent;
            finalFade = root.Q("finalFade");
            navigation.scaleMode = ScaleMode.StretchToFill;
            radar = root.Q<NavalRadar>("navalRadar");
            outcome = root.Q("outcome");
            stamp = root.Q<Image>("archiveStamp");
            stamp.scaleMode = ScaleMode.ScaleToFit;
            message = root.Q<Label>("outcomeMessage");
            ResetSession();
        }

        public void ResetSession()
        {
            ShowFinale(false);
            SetFinalFade(0);
            ShowVideo(false);
            ShowRadar(false);
            ScreenUI.Show(outcome, false);
        }

        public void ShowVideo(bool show) => ScreenUI.Show(video, show);
        public void ShowRadar(bool show) => ScreenUI.Show(radar, show);
        public void SetNavigationTexture(RenderTexture texture) => navigation.image = texture;
        public void SetRadarContacts(Vector2[] positions, Vector2[] directions, int count) => radar.SetContacts(positions, directions, count);

        public void ShowFinale(bool show)
        {
            navigationPanel.EnableInClassList("navigation-full", show);
            ScreenUI.Show(videoPanel, !show);
        }

        public void SetFinalFade(float opacity)
        {
            finalFade.style.opacity = Mathf.Clamp01(opacity);
            ScreenUI.Show(finalFade, opacity > 0);
        }

        public void ShowFailure()
        {
            ShowRadar(false);
            ScreenUI.Show(stamp, true);
            message.text = "Recruta, você pode fazer melhor.\nRetorne ao acervo e prepare uma nova tentativa.";
            ScreenUI.Show(outcome, true);
        }

        public void ShowUnavailable()
        {
            ShowRadar(false);
            ScreenUI.Show(stamp, false);
            message.text = "Não foi possível abrir o filme.\nRetornando ao acervo.";
            ScreenUI.Show(outcome, true);
        }
    }
}
