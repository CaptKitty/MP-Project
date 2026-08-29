using UnityEngine;
using UnityEngine.UI;

public static class UIHoldingSlotIcon
{
    public static void Set(Transform slot, Sprite sprite)
    {
        if (slot == null) return;
        Transform existing = slot.Find("HoldingIcon");
        Image image;
        if (existing == null)
        {
            GameObject iconObject = new GameObject("HoldingIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = slot.gameObject.layer;
            iconObject.transform.SetParent(slot, false);
            existing = iconObject.transform;
            image = iconObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f); rect.offsetMax = new Vector2(-2f, -2f);
        }
        else image = existing.GetComponent<Image>();
        if (image == null) return;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.sprite = sprite;
        image.enabled = sprite != null;
        existing.SetAsFirstSibling();
    }
}
