using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Keeps the army-composition detail panel open while its interactive contents are hovered.</summary>
public sealed class CompositionStatDisplayHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UIElement Owner;
    private RectTransform panelRect;
    private Canvas rootCanvas;

    private void Awake()
    {
        panelRect = transform as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
    }

    private void Update()
    {
        if (Owner == null || panelRect == null) return;
        Camera eventCamera = rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : rootCanvas.worldCamera;
        if (RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, eventCamera))
            Owner.KeepCompositionUnitDetailsOpen();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner != null) Owner.KeepCompositionUnitDetailsOpen();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Child controls can generate exit events while the pointer is still within the panel.
        // Update() cancels this request whenever the pointer remains inside the complete rectangle.
        if (Owner != null) Owner.RequestHideCompositionUnitDetails();
    }
}
