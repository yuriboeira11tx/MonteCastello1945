using System;
using FEB.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MenuGameFlow : MonoBehaviour
{
    [SerializeField] Button newGameButton;
    [SerializeField] Button continueButton;
    [SerializeField] GameObject slotsPanel;
    [SerializeField] TMP_Text panelTitle;
    [SerializeField] TMP_Text panelMessage;
    [SerializeField] Button[] slotButtons;
    [SerializeField] Button cancelButton;
    [SerializeField] Button confirmOverwriteButton;
    [SerializeField] Button continueSlotButton;
    [SerializeField] string firstScene = "ArquivoHistorico";
    int pendingSlot = -1;
    bool loading;

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        newGameButton.onClick.AddListener(OpenNewGame);
        continueButton.onClick.AddListener(ContinueLatest);
        cancelButton.onClick.AddListener(() => slotsPanel.SetActive(false));
        confirmOverwriteButton.onClick.AddListener(() => StartSlot(pendingSlot));
        continueSlotButton.onClick.AddListener(() => ResumeSlot(ResearchSaveStore.Read(pendingSlot)));
        for (int i = 0; i < slotButtons.Length; i++) { int slot = i; slotButtons[i].onClick.AddListener(() => SelectSlot(slot)); }
        slotsPanel.SetActive(false);
        continueButton.interactable = ResearchSaveStore.Latest() != null;
    }

    public void OpenNewGame()
    {
        if (loading) return;
        pendingSlot = -1;
        panelTitle.text = "NOVA PESQUISA";
        panelMessage.text = "Escolha um dos quatro espaços para salvar sua investigação.";
        confirmOverwriteButton.gameObject.SetActive(false);
        continueSlotButton.gameObject.SetActive(false);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var save = ResearchSaveStore.Read(i);
            var date = save != null && DateTime.TryParse(save.updatedUtc, out var updated) ? updated.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";
            slotButtons[i].GetComponentInChildren<TMP_Text>().text = save == null ? $"{i + 1:00}   Espaço livre" : $"{i + 1:00}   {save.StageLabel}\n       {date}";
            slotButtons[i].interactable = true;
        }
        slotsPanel.SetActive(true);
    }

    void SelectSlot(int slot)
    {
        if (ResearchSaveStore.Read(slot) == null) { StartSlot(slot); return; }
        pendingSlot = slot;
        panelMessage.text = $"O espaço {slot + 1} já contém uma pesquisa. Continue de onde parou ou confirme a substituição por um novo jogo.";
        confirmOverwriteButton.gameObject.SetActive(true);
        continueSlotButton.gameObject.SetActive(true);
    }

    void StartSlot(int slot)
    {
        if (loading || slot < 0 || slot >= ResearchSaveStore.SlotCount) return;
        if (!Application.CanStreamedLevelBeLoaded(firstScene)) { ShowError("A cena do arquivo não está disponível."); return; }
        try { ResearchSaveStore.Begin(slot); Load(firstScene); }
        catch (Exception e) { ShowError("Não foi possível criar o save. " + e.Message); }
    }

    public void ContinueLatest()
    {
        if (loading) return;
        var save = ResearchSaveStore.Latest();
        ResumeSlot(save);
    }

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
        newGameButton.interactable = continueButton.interactable = false;
        panelTitle.text = "CARREGANDO";
        panelMessage.text = "Preparando sua pesquisa…";
        foreach (var button in slotButtons) button.interactable = false;
        confirmOverwriteButton.gameObject.SetActive(false);
        continueSlotButton.gameObject.SetActive(false);
        slotsPanel.SetActive(true);
        SceneManager.LoadSceneAsync(scene);
    }

    void ShowError(string message)
    {
        loading = false;
        panelMessage.text = message;
        slotsPanel.SetActive(true);
    }
}
