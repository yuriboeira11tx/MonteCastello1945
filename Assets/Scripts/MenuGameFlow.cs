using System;
using FEB.Persistence;
using FEB.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class MenuGameFlow : MonoBehaviour
{
    [SerializeField] MenuController menu;
    [SerializeField] string firstScene = "ArquivoHistorico";
    VisualElement panel;
    VisualElement slots;
    Label panelTitle;
    Label panelMessage;
    Button[] slotButtons;
    Button cancelButton;
    Button confirmOverwriteButton;
    Button continueSlotButton;
    readonly Action[] selectSlotActions = new Action[ResearchSaveStore.SlotCount];
    int pendingSlot = -1;
    bool loading;
    bool panelOpen;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        panel = root.Q("slotsPanel");
        slots = root.Q("slots");
        panelTitle = root.Q<Label>("panelTitle");
        panelMessage = root.Q<Label>("panelMessage");
        cancelButton = root.Q<Button>("cancel");
        confirmOverwriteButton = root.Q<Button>("confirmOverwrite");
        continueSlotButton = root.Q<Button>("continueSlot");
        cancelButton.clicked += Close;
        confirmOverwriteButton.clicked += ConfirmOverwrite;
        continueSlotButton.clicked += ContinueSelected;
        slotButtons = new Button[ResearchSaveStore.SlotCount];
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i;
            slotButtons[i] = root.Q<Button>("slot" + (i + 1));
            selectSlotActions[i] = () => SelectSlot(slot);
            slotButtons[i].clicked += selectSlotActions[i];
        }
        ScreenUI.Show(panel, false);
        panelOpen = loading = false;
    }

    void Update()
    {
        if (panelOpen && !loading && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    public void OpenNewGame()
    {
        if (loading) return;
        pendingSlot = -1;
        panelTitle.text = "NOVA PESQUISA";
        panel.RemoveFromClassList("credits-open");
        panelMessage.text = "Escolha um dos quatro espaços para salvar sua investigação.";
        ScreenUI.Show(confirmOverwriteButton, false);
        ScreenUI.Show(continueSlotButton, false);
        ScreenUI.Show(slots, true);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var save = ResearchSaveStore.Read(i);
            var date = save != null && DateTime.TryParse(save.updatedUtc, out var updated) ? updated.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";
            slotButtons[i].text = save == null ? $"{i + 1:00}   Espaço livre" : $"{i + 1:00}   {save.StageLabel}\n       {date}";
            slotButtons[i].SetEnabled(true);
        }
        OpenPanel();
    }

    public void OpenCredits()
    {
        if (loading) return;
        panelTitle.text = "CRÉDITOS";
        panel.AddToClassList("credits-open");
        panelMessage.text = "Monte Castello: 1945\n\nYuri Boeira\n\nv1.0";
        ScreenUI.Show(slots, false);
        ScreenUI.Show(confirmOverwriteButton, false);
        ScreenUI.Show(continueSlotButton, false);
        OpenPanel();
    }

    void OpenPanel()
    {
        panelOpen = true;
        ScreenUI.Show(panel, true);
        menu.SetMenuEnabled(false);
        cancelButton.Focus();
    }
    void Close()
    {
        if (loading) return;
        panelOpen = false;
        ScreenUI.Show(panel, false);
        menu.SetMenuEnabled(true);
    }
    void ConfirmOverwrite() => StartSlot(pendingSlot);
    void ContinueSelected() => ResumeSlot(ResearchSaveStore.Read(pendingSlot));

    void SelectSlot(int slot)
    {
        if (loading) return;
        if (ResearchSaveStore.Read(slot) == null) { StartSlot(slot); return; }
        pendingSlot = slot;
        panelMessage.text = $"O espaço {slot + 1} já contém uma pesquisa. Continue de onde parou ou confirme a substituição por um novo jogo.";
        ScreenUI.Show(confirmOverwriteButton, true);
        ScreenUI.Show(continueSlotButton, true);
    }

    void StartSlot(int slot)
    {
        if (loading || slot < 0 || slot >= ResearchSaveStore.SlotCount) return;
        if (!Application.CanStreamedLevelBeLoaded(firstScene)) { ShowError("A cena do arquivo não está disponível."); return; }
        try { ResearchSaveStore.Begin(slot); Load(firstScene); }
        catch (Exception e) { ShowError("Não foi possível criar o save. " + e.Message); }
    }

    public void ContinueLatest() { if (!loading) ResumeSlot(ResearchSaveStore.Latest()); }

    void ResumeSlot(ResearchSave save)
    {
        if (loading) return;
        if (save == null) { OpenNewGame(); return; }
        var scene = Application.CanStreamedLevelBeLoaded(save.scene) ? save.scene : firstScene;
        ResearchSaveStore.Resume(save);
        Load(scene);
    }

    void Load(string scene)
    {
        loading = true;
        menu.NewGameButton.SetEnabled(false);
        menu.ContinueButton.SetEnabled(false);
        panelTitle.text = "CARREGANDO";
        panelMessage.text = "Preparando sua pesquisa…";
        foreach (var button in slotButtons) button.SetEnabled(false);
        cancelButton.SetEnabled(false);
        ScreenUI.Show(confirmOverwriteButton, false);
        ScreenUI.Show(continueSlotButton, false);
        OpenPanel();
        SceneManager.LoadSceneAsync(scene);
    }

    void ShowError(string message)
    {
        loading = false;
        panelMessage.text = message;
        cancelButton.SetEnabled(true);
        OpenPanel();
    }

    void OnDisable()
    {
        if (cancelButton == null) return;
        cancelButton.clicked -= Close;
        confirmOverwriteButton.clicked -= ConfirmOverwrite;
        continueSlotButton.clicked -= ContinueSelected;
        for (int i = 0; i < slotButtons.Length; i++) slotButtons[i].clicked -= selectSlotActions[i];
    }
}
