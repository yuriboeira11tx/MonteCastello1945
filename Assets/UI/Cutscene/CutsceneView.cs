using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace FEB.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CutsceneView : MonoBehaviour
    {
        [SerializeField] RenderTexture videoTexture;
        [Serializable]
        public struct TributePhotograph
        {
            public Texture2D image;
            public string caption;
        }
        [Header("Homenagem ao Itapagé")]
        [SerializeField] TributePhotograph[] tributePhotographs = Array.Empty<TributePhotograph>();
        [Tooltip("22 mortos: Introdução à História Marítima Brasileira (Marinha, 2006), tabela de perdas; corroborado por Sérgio Lima Conceição (UFAL, 2012).")]
        [SerializeField, Min(0)] int fatalities = 22;
        [SerializeField, TextArea(3, 5)] string historicalMessage = "Você conseguiu evitar os torpedos durante o filme.\nNa realidade, o Itapagé foi torpedeado e afundou.\nLitoral de Alagoas, 26 de setembro de 1943.";
        [SerializeField, Min(3)] float tributeTextSeconds = 10;
        [SerializeField, Min(1)] float tributePhotoSeconds = 2.5f;
        [SerializeField, Min(3)] float tributeClosingSeconds = 5;
        [SerializeField, Min(.1f)] float tributeFadeSeconds = .65f;
        Image video, navigation, stamp;
        NavalRadar radar;
        VisualElement outcome, navigationPanel, videoPanel, finalFade;
        Label message;
        VisualElement tribute, tributeContent;
        Image tributePhoto;
        Label tributeHeading, tributeMessage, tributeCaption, tributeCount;

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
            tribute = root.Q("tribute");
            tributeContent = root.Q("tributeContent");
            tributePhoto = root.Q<Image>("tributePhoto");
            tributeHeading = root.Q<Label>("tributeHeading");
            tributeMessage = root.Q<Label>("tributeMessage");
            tributeCaption = root.Q<Label>("tributeCaption");
            tributeCount = root.Q<Label>("tributeCount");
            tributePhoto.scaleMode = ScaleMode.ScaleToFit;
            ResetSession();
        }

        public void ResetSession()
        {
            ShowFinale(false);
            SetFinalFade(0);
            ShowVideo(false);
            ShowRadar(false);
            ScreenUI.Show(outcome, false);
            ScreenUI.Show(tribute, false);
            tributePhoto.image = null;
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

        public IEnumerator PlayTribute()
        {
            ShowRadar(false);
            ScreenUI.Show(outcome, false);
            ScreenUI.Show(tribute, true);
            tributeContent.style.opacity = 0;
            SetTributeText("A história não pôde ser mudada", historicalMessage, $"{fatalities} pessoas perderam a vida.");
            yield return FadeTribute(0, 1);
            yield return new WaitForSecondsRealtime(tributeTextSeconds);
            yield return FadeTribute(1, 0);

            foreach (var photograph in tributePhotographs)
            {
                if (photograph.image == null) continue;
                SetTributeText("Sobreviventes do Itapagé", "", "");
                tributePhoto.image = photograph.image;
                tributeCaption.text = photograph.caption;
                ScreenUI.Show(tributePhoto, true);
                ScreenUI.Show(tributeCaption, true);
                yield return FadeTribute(0, 1);
                yield return new WaitForSecondsRealtime(tributePhotoSeconds);
                yield return FadeTribute(1, 0);
            }

            SetTributeText("Em memória", "Às vítimas do Itapagé.\nAos sobreviventes e às famílias marcadas por essa perda.", $"{fatalities} vidas perdidas\n26 de setembro de 1943");
            yield return FadeTribute(0, 1);
            yield return new WaitForSecondsRealtime(tributeClosingSeconds);
            yield return FadeTribute(1, 0);
            tributePhoto.image = null;
            ScreenUI.Show(tribute, false);
        }

        void SetTributeText(string heading, string text, string count)
        {
            tributeHeading.text = heading;
            tributeMessage.text = text;
            tributeCount.text = count;
            ScreenUI.Show(tributePhoto, false);
            ScreenUI.Show(tributeCaption, false);
            ScreenUI.Show(tributeMessage, !string.IsNullOrEmpty(text));
            ScreenUI.Show(tributeCount, !string.IsNullOrEmpty(count));
        }

        IEnumerator FadeTribute(float from, float to)
        {
            float duration = Mathf.Max(.1f, tributeFadeSeconds);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                tributeContent.style.opacity = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / duration));
                yield return null;
            }
            tributeContent.style.opacity = to;
        }
    }
}
