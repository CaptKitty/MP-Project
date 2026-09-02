using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIBuildingMenuSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ProvinceBuilding Building { get; private set; }
    public ProvinceBuilding TooltipBuilding { get; private set; }
    public Province Province { get; private set; }
    public int SlotIndex { get; private set; }
    public bool IsHovered { get; private set; }

    private UIBuildingMenu menu;
    private Image background;
    private Image buildingIcon;
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
        EnsureIcon();
    }

    public void Configure(UIBuildingMenu owner, Province province, ProvinceBuilding building, int slotIndex, bool showProvinceName)
    {
        menu = owner;
        Province = province;
        Building = building;
        if (Building != null && Building.definition == null)
            Building.definition = BuildingDefinition.Find(Building.id);
        SlotIndex = slotIndex;
        if (background == null) background = GetComponent<Image>();
        EnsureLabel();

        ProvinceConstructionOrder construction = province != null && province.constructionOrders != null
            ? province.constructionOrders.Find(order => order != null && order.slotIndex == slotIndex)
            : null;
        BuildingDefinition displayedDefinition = building != null ? building.definition :
            construction != null ? BuildingDefinition.Find(construction.buildingId) : null;
        TooltipBuilding = building;
        if (TooltipBuilding == null && construction != null)
            TooltipBuilding = new ProvinceBuilding { definition = displayedDefinition, id = construction.buildingId,
                level = Mathf.Max(1, construction.targetLevel), slotIndex = slotIndex };
        Sprite displayedIcon = displayedDefinition != null ? displayedDefinition.icon : null;
        SetIcon(displayedIcon);
        string prefix = showProvinceName && province != null ? province.name + "\n" : string.Empty;
        if (construction != null)
        {
            string constructionName = displayedDefinition != null ? displayedDefinition.DisplayName : construction.buildingId;
            label.text = prefix + constructionName + "\n" + construction.remainingTicks + " ticks";
            if (background != null) background.color = new Color(.45f, .32f, .12f, .95f);
            if (button != null) button.interactable = false;
            return;
        }
        if (button != null) button.interactable = true;

        if (building == null)
        {
            label.text = "Empty";
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

    private void EnsureIcon()
    {
        Transform existing = transform.Find("BuildingIcon");
        if (existing != null) buildingIcon = existing.GetComponent<Image>();
        if (buildingIcon == null)
        {
            GameObject child = new GameObject("BuildingIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            buildingIcon = child.GetComponent<Image>();
        }
        buildingIcon.raycastTarget = false;
        buildingIcon.preserveAspect = true;
        RectTransform rect = buildingIcon.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 5f); rect.offsetMax = new Vector2(-5f, -5f);
        // Keep the icon above the colored slot background but behind its descriptive text.
        buildingIcon.transform.SetAsFirstSibling();
    }

    private void SetIcon(Sprite icon)
    {
        EnsureIcon();
        buildingIcon.sprite = icon;
        buildingIcon.color = Color.white;
        buildingIcon.enabled = icon != null;
        RectTransform iconRect = buildingIcon.rectTransform;
        RectTransform labelRect = label != null ? label.rectTransform : null;
        if (icon != null)
        {
            iconRect.anchorMin = new Vector2(0f, .30f); iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(5f, 2f); iconRect.offsetMax = new Vector2(-5f, -5f);
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = new Vector2(1f, .30f);
                labelRect.offsetMin = new Vector2(2f, 1f); labelRect.offsetMax = new Vector2(-2f, -1f);
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 7; label.resizeTextMaxSize = 11;
            }
        }
        else if (labelRect != null)
        {
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(3f, 3f); labelRect.offsetMax = new Vector2(-3f, -3f);
            label.resizeTextForBestFit = false; label.fontSize = 12;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { IsHovered = true; if (menu != null) menu.PointerEntered(this); }
    public void OnPointerExit(PointerEventData eventData) { IsHovered = false; if (menu != null) menu.PointerExited(this); }
    private void OnDisable() { IsHovered = false; }
    private void HandleClick() { if (menu != null) menu.SlotClicked(this); }
}
