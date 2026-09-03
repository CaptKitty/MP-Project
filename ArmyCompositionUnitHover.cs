using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class ArmyCompositionUnitHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UIElement Owner;
    public UnitSaveData Unit;
    public Material ArtworkMaterial;
    private Coroutine pendingShow;
    private Image background;

    private void Awake()
    {
        background = GetComponent<Image>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner == null || Unit == null) return;
        Owner.KeepCompositionUnitDetailsOpen();
        if (pendingShow != null) StopCoroutine(pendingShow);
        pendingShow = StartCoroutine(ShowAfterDelay());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (pendingShow != null) StopCoroutine(pendingShow);
        pendingShow = null;
        SetTint(false);
        if (Owner != null) Owner.RequestHideCompositionUnitDetails();
    }

    private void OnDisable()
    {
        if (pendingShow != null) StopCoroutine(pendingShow);
        pendingShow = null;
        SetTint(false);
        if (Owner != null) Owner.HideCompositionUnitDetails();
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1f);
        pendingShow = null;
        SetTint(true);
        if (Owner != null && Unit != null) Owner.ShowCompositionUnitDetails(Unit, ArtworkMaterial);
    }

    private void SetTint(bool active)
    {
        if (background != null) background.color = active ? new Color(1f, 1f, 1f, .12f) : Color.clear;
    }
}
