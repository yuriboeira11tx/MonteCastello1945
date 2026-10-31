using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using FEB.UI;

[RequireComponent(typeof(UIDocument))]
public sealed class MenuController : MonoBehaviour
{
    [SerializeField] MenuGameFlow gameFlow;
    [SerializeField] Sprite[] backgrounds;
    [SerializeField] float visibleSeconds = 4f;
    [SerializeField] float fadeSeconds = 2f;
    VisualElement intro;
    Label introPrompt;
    float promptElapsed;
    VisualElement options;
    Image slideshow;
    Button newGame;
    Button continueGame;
    Button credits;
    Button quit;
    Coroutine slideshowRoutine;
    bool introVisible;
    public Button NewGameButton => newGame;
    public Button ContinueButton => continueGame;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        intro = root.Q("intro");
        introPrompt = root.Q<Label>("introPrompt");
        options = root.Q("options");
        slideshow = root.Q<Image>("slideshow");
        newGame = root.Q<Button>("newGame");
        continueGame = root.Q<Button>("continueGame");
        credits = root.Q<Button>("credits");
        quit = root.Q<Button>("quit");
        newGame.clicked += OpenNewGame;
        continueGame.clicked += ContinueLatest;
        credits.clicked += OpenCredits;
        quit.clicked += Quit;
        foreach (var button in new[] { newGame, continueGame, credits, quit })
            button.RegisterCallback<PointerEnterEvent>(FocusOption);
        intro.RegisterCallback<PointerDownEvent>(OnIntroPointerDown);
        introVisible = true;
        promptElapsed = 0f;
        introPrompt.style.opacity = 0f;
        ScreenUI.Show(intro, true);
        ScreenUI.Show(options, false);
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        if (Application.isPlaying) slideshowRoutine = StartCoroutine(Slideshow());
    }

    void Start() => continueGame.SetEnabled(FEB.Persistence.ResearchSaveStore.Latest() != null);

    void Update()
    {
        if (!introVisible) return;

        promptElapsed = Mathf.Repeat(promptElapsed + Time.unscaledDeltaTime, 2f);
        introPrompt.style.opacity = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(promptElapsed, 1f));

        if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) ShowOptions();
    }

    public void ShowOptions()
    {
        introVisible = false;
        ScreenUI.Show(intro, false);
        ScreenUI.Show(options, true);
        newGame.Focus();
        if (slideshowRoutine != null) { StopCoroutine(slideshowRoutine); slideshowRoutine = null; }
    }

    void OnIntroPointerDown(PointerDownEvent evt) => ShowOptions();
    void FocusOption(PointerEnterEvent evt) => ((Button)evt.currentTarget).Focus();
    public void SetMenuEnabled(bool enabled)
    {
        options.SetEnabled(enabled);
        if (enabled) newGame.Focus();
    }
    void OpenNewGame() => gameFlow.OpenNewGame();
    void ContinueLatest() => gameFlow.ContinueLatest();
    void OpenCredits() => gameFlow.OpenCredits();
    void Quit() => Application.Quit();

    IEnumerator Slideshow()
    {
        if (backgrounds == null || backgrounds.Length == 0) yield break;
        int index = 0;
        slideshow.sprite = backgrounds[index];
        slideshow.scaleMode = ScaleMode.StretchToFill;
        while (true)
        {
            yield return new WaitForSecondsRealtime(visibleSeconds);
            yield return Fade(1, 0);
            index = (index + 1) % backgrounds.Length;
            slideshow.sprite = backgrounds[index];
            yield return Fade(0, 1);
        }
    }

    IEnumerator Fade(float from, float to)
    {
        for (float time = 0; time < fadeSeconds; time += Time.unscaledDeltaTime)
        {
            slideshow.style.opacity = Mathf.Lerp(from, to, time / fadeSeconds);
            yield return null;
        }
        slideshow.style.opacity = to;
    }

    void OnDisable()
    {
        if (slideshowRoutine != null) StopCoroutine(slideshowRoutine);
        slideshowRoutine = null;
        if (newGame == null) return;
        newGame.clicked -= OpenNewGame;
        continueGame.clicked -= ContinueLatest;
        credits.clicked -= OpenCredits;
        quit.clicked -= Quit;
        foreach (var button in new[] { newGame, continueGame, credits, quit })
            button.UnregisterCallback<PointerEnterEvent>(FocusOption);
        intro.UnregisterCallback<PointerDownEvent>(OnIntroPointerDown);
    }
}
