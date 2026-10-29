using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class MenuOptionHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] RectTransform indicator;
    [SerializeField] TMP_Text label;
    [SerializeField, Min(0)] float gap = 4f;
    [SerializeField, Min(1)] float thickness = 5f;

    UnityEngine.UI.Button button;
    bool pointerInside;

    void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        ShowIndicator();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        HideIndicator();
    }

    public void OnSelect(BaseEventData eventData) => ShowIndicator();

    public void OnDeselect(BaseEventData eventData)
    {
        if (!pointerInside) HideIndicator();
    }

    void OnDisable()
    {
        pointerInside = false;
        HideIndicator();
    }

    void ShowIndicator()
    {
        if (indicator == null || label == null || button == null || !button.IsActive() || !button.IsInteractable()) return;

        label.ForceMeshUpdate();
        var bounds = label.textBounds;
        indicator.SetParent(label.rectTransform, false);
        indicator.anchorMin = indicator.anchorMax = label.rectTransform.pivot;
        indicator.pivot = new Vector2(0, 0.5f);
        indicator.localRotation = Quaternion.identity;
        indicator.localScale = Vector3.one;
        indicator.sizeDelta = new Vector2(bounds.size.x, thickness);
        indicator.anchoredPosition = new Vector2(bounds.min.x, bounds.min.y - gap - thickness * 0.5f);
        indicator.gameObject.SetActive(true);
    }

    void HideIndicator()
    {
        if (indicator != null && label != null && indicator.parent == label.transform)
            indicator.gameObject.SetActive(false);
    }
}
