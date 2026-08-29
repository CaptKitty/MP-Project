using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UILevyUnitHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UnitSaveData Unit { get; private set; }
    private UIElement owner;
    private Coroutine pendingShow;
    private Image hitArea;

    private void Awake()
    {
        hitArea = GetComponent<Image>();
    }

    public void Configure(UIElement target, UnitSaveData unit)
    {
        owner = target;
        Unit = unit;
        if (hitArea == null) hitArea = GetComponent<Image>();
        if (hitArea != null) hitArea.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null || Unit == null) return;
        owner.KeepCompositionUnitDetailsOpen();
        if (pendingShow != null) StopCoroutine(pendingShow);
        pendingShow = StartCoroutine(ShowAfterDelay());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CancelPending();
        if (owner != null) owner.RequestHideCompositionUnitDetails();
    }

    private void OnDisable()
    {
        CancelPending();
        if (owner != null) owner.HideCompositionUnitDetails();
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(2f);
        pendingShow = null;
        if (owner != null && Unit != null) owner.ShowCompositionUnitDetails(Unit);
    }

    private void CancelPending()
    {
        if (pendingShow != null) StopCoroutine(pendingShow);
        pendingShow = null;
    }
}
