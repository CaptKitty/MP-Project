using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIBuildingMenuSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ProvinceBuilding Building { get; private set; }
    public Province Province { get; private set; }
    public int SlotIndex { get; private set; }
    public bool IsHovered { get; private set; }

    private UIBuildingMenu menu;
    private Image background;
    private Text label;
    private Button button;

    private void Awake()
    {
        background = GetComponent<Image>();
        button = GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();
        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
        EnsureLabel();
    }

    public void Configure(UIBuildingMenu owner, Province province, ProvinceBuilding building, int slotIndex, bool showProvinceName)
    {
        menu = owner;
        Province = province;
        Building = building;
        SlotIndex = slotIndex;
        if (background == null) background = GetComponent<Image>();
        EnsureLabel();

        ProvinceConstructionOrder construction = province != null && province.constructionOrders != null
            ? province.constructionOrders.Find(order => order != null && order.slotIndex == slotIndex)
            : null;
        string prefix = showProvinceName && province != null ? province.name + "\n" : string.Empty;
        if (construction != null)
        {
            label.text = prefix + "Constructing " + construction.buildingId + "\n" + construction.remainingTicks + " ticks";
            if (background != null) background.color = new Color(.45f, .32f, .12f, .95f);
            if (button != null) button.interactable = false;
            return;
        }
        if (button != null) button.interactable = true;

        if (building == null)
        {
            label.text = prefix + "Empty";
            if (background != null) background.color = new Color(.25f, .25f, .25f, .9f);
        }
        else
        {
            label.text = prefix + building.DisplayName + "\nLv " + building.level;
            if (background != null) background.color = new Color(.22f, .38f, .24f, .95f);
        }
        if (background != null) background.raycastTarget = true;
    }

    private void EnsureLabel()
    {
        Transform existing = transform.Find("BuildingLabel");
        if (existing != null) label = existing.GetComponent<Text>();
        if (label != null) return;

        GameObject child = new GameObject("BuildingLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        label = child.GetComponent<Text>();
        Text sceneText = GetComponentInParent<UIProvinceHost>() != null
            ? GetComponentInParent<UIProvinceHost>().GetComponentInChildren<Text>(true) : null;
        label.font = sceneText != null ? sceneText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 12;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(3f, 3f); rect.offsetMax = new Vector2(-3f, -3f);
    }

    public void OnPointerEnter(PointerEventData eventData) { IsHovered = true; if (menu != null) menu.PointerEntered(this); }
    public void OnPointerExit(PointerEventData eventData) { IsHovered = false; if (menu != null) menu.PointerExited(this); }
    private void OnDisable() { IsHovered = false; }
    private void HandleClick() { if (menu != null) menu.SlotClicked(this); }
}
