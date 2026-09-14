using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundSlider : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    
    [Header("Imagens do slider")]
    [SerializeField] private Sprite[] imagens;

    [Header("Tempos")]
    [SerializeField] private float tempoVisivel = 5f;
    [SerializeField] private float tempoFade = 2f;

    private Image backgroundImage;
    private CanvasGroup canvasGroup;

    private int imagemAtual = 0;

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (imagens == null || imagens.Length == 0)
            return;

        StartCoroutine(StartSlider());
    }

    private IEnumerator StartSlider()
    {
        backgroundImage.sprite = imagens[imagemAtual];
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(tempoVisivel);

        while (true)
        {
            yield return Fade(1f, 0f);
            
            imagemAtual++;
            if (imagemAtual >= imagens.Length)
                imagemAtual = 0;
            backgroundImage.sprite = imagens[imagemAtual];

            yield return Fade(0f, 1f);
            yield return new WaitForSeconds(tempoVisivel);
        }
    }

    private IEnumerator Fade(float inicio, float fim)
    {
        float tempo = 0f;

        while (tempo < tempoFade)
        {
            tempo += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(inicio, fim, tempo / tempoFade);
            yield return null;
        }

        canvasGroup.alpha = fim;
    }
}