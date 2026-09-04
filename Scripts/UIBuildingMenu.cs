using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIBuildingMenu : MonoBehaviour
{
    public Province LoadedProvince { get; private set; }

    private readonly List<UIBuildingMenuSlot> slots = new List<UIBuildingMenuSlot>();
    private readonly List<UIBuildingMenuSlot> generatedSlots = new List<UIBuildingMenuSlot>();
    private readonly List<Vector2> sceneSlotPositions = new List<Vector2>();
    private readonly List<float> sceneSlotTopOffsets = new List<float>();
    private readonly List<Vector2> sceneSlotAnchorMins = new List<Vector2>();
    private readonly List<Vector2> sceneSlotAnchorMaxs = new List<Vector2>();
    private int sceneSlotCount;
    private float sceneMenuHeight;
    private float sceneMenuWidth;
    private Vector2 sceneMenuPosition;
    private UIProvinceHost host;
    private UIBuildingMenuSlot selectedSlot;
    private GameObject tooltipRoot;
    private Text tooltipText;
    private string displayedTooltipContents;
    private GameObject buildGridRoot;
    private Text panelProvinceName;
    private bool buildGridOpen;
    private bool isRegionClone;
    private readonly List<UIBuildingMenu> provincePanelCopies = new List<UIBuildingMenu>();
    private const float ProvincePanelSpacing = 12f;
    public int TestConstructionTicks = -1;

    private void Awake()
    {
        if (GetComponent<UIProvincePanelSummary>() == null)
            gameObject.AddComponent<UIProvincePanelSummary>();
        ResolveSlots();
        ResolvePanelProvinceName();
        sceneSlotCount = slots.Count;
        RectTransform menuRect = GetComponent<RectTransform>();
        sceneMenuHeight = menuRect.sizeDelta.y;
        sceneMenuWidth = menuRect.sizeDelta.x;
        sceneMenuPosition = menuRect.anchoredPosition;
        float menuTop = sceneMenuHeight * (1f - menuRect.pivot.y);
        for (int i = 0; i < sceneSlotCount; i++)
        {
            RectTransform slotRect = slots[i].GetComponent<RectTransform>();
            sceneSlotPositions.Add(slotRect.anchoredPosition);
            sceneSlotAnchorMins.Add(slotRect.anchorMin);
            sceneSlotAnchorMaxs.Add(slotRect.anchorMax);
            float slotPivotY = menuRect.InverseTransformPoint(slotRect.position).y;
            sceneSlotTopOffsets.Add(menuTop - slotPivotY);
        }
    }

    public void LoadProvince(Province province, UIProvinceHost owner)
    {
        if (isRegionClone)
        {
            LoadSingleProvince(province, owner, true);
            return;
        }
        List<Province> provinces = GetDisplayedProvinces(province);
        if (province != null)
        {
            provinces.Remove(province);
            provinces.Insert(0, province);
        }
        EnsureProvincePanelCopies(Mathf.Max(0, provinces.Count - 1));
        LoadSingleProvince(provinces.Count > 0 ? provinces[0] : province, owner, provinces.Count > 1);
        RectTransform masterRect = GetComponent<RectTransform>();
        masterRect.sizeDelta = new Vector2(sceneMenuWidth, sceneMenuHeight);
        masterRect.anchoredPosition = sceneMenuPosition;
        for (int i = 0; i < provincePanelCopies.Count; i++)
        {
            bool active = i + 1 < provinces.Count;
            UIBuildingMenu copy = provincePanelCopies[i];
            copy.gameObject.SetActive(active);
            if (!active) continue;
            RectTransform copyRect = copy.GetComponent<RectTransform>();
            copyRect.sizeDelta = new Vector2(sceneMenuWidth, sceneMenuHeight);
            copyRect.anchoredPosition = sceneMenuPosition + Vector2.right *
                ((i + 1) * (sceneMenuWidth + ProvincePanelSpacing));
            copy.LoadSingleProvince(provinces[i + 1], owner, true);
        }
    }

    private void LoadSingleProvince(Province province, UIProvinceHost owner, bool showProvinceName)
    {
        bool preserveInteraction = province != null && province == LoadedProvince;
        host = owner;
        LoadedProvince = province;
        ResolvePanelProvinceName();
        if (panelProvinceName != null)
            panelProvinceName.text = province != null ? province.name : "No province";
        if (!preserveInteraction)
        {
            selectedSlot = null;
            buildGridOpen = false;
        }
        ResolveSlots();
        EnsureTooltip();
        if (!preserveInteraction)
        {
            HideTooltip();
            HideBuildGrid();
        }

        int slotsPerProvince = Mathf.Max(4, sceneSlotCount);
        EnsureSlotCount(slotsPerProvince);
        int displayIndex = 0;
        for (int slotIndex = 0; slotIndex < slotsPerProvince; slotIndex++)
        {
            ProvinceBuilding building = province != null ? province.GetBuildingInSlot(slotIndex) : null;
            slots[displayIndex].gameObject.SetActive(true);
            slots[displayIndex].Configure(this, province, building, slotIndex, showProvinceName);
            displayIndex++;
        }
        for (int i = displayIndex; i < slots.Count; i++) slots[i].gameObject.SetActive(false);
        if (preserveInteraction && !buildGridOpen)
        {
            UIBuildingMenuSlot hovered = slots.Find(slot => slot != null && slot.gameObject.activeInHierarchy && slot.IsHovered);
            if (hovered != null) ShowTooltip(BuildBuildingDescription(hovered.Province, hovered.TooltipBuilding, hovered.SlotIndex));
            else HideTooltip();
        }
        UIProvincePanelSummary summary = GetComponent<UIProvincePanelSummary>();
        if (summary == null) summary = gameObject.AddComponent<UIProvincePanelSummary>();
        summary.RefreshFor(province);
    }

    private void EnsureProvincePanelCopies(int required)
    {
        while (provincePanelCopies.Count < required)
        {
            GameObject cloneObject = Instantiate(gameObject, transform.parent);
            cloneObject.name = gameObject.name + " Province Copy " + (provincePanelCopies.Count + 2);
            UIBuildingMenu clone = cloneObject.GetComponent<UIBuildingMenu>();
            clone.isRegionClone = true;
            clone.provincePanelCopies.Clear();
            provincePanelCopies.Add(clone);
        }
    }

    private void LayoutRegionSlots(int provinceCount, int slotsPerProvince)
    {
        if (sceneSlotCount == 0 || sceneSlotPositions.Count == 0) return;
        float groupWidth = Mathf.Max(1f, sceneMenuWidth);
        float expandedWidth = sceneMenuWidth + Mathf.Max(0, provinceCount - 1) * groupWidth;
        float addedWidth = expandedWidth - sceneMenuWidth;
        int visibleCount = provinceCount * slotsPerProvince;
        for (int i = 0; i < visibleCount && i < slots.Count; i++)
        {
            int templateIndex = i % slotsPerProvince;
            int provinceIndex = i / slotsPerProvince;
            RectTransform rect = slots[i].GetComponent<RectTransform>();
            RectTransform templateRect = slots[Mathf.Min(templateIndex, sceneSlotCount - 1)].GetComponent<RectTransform>();
            int sourceIndex = Mathf.Min(templateIndex, sceneSlotPositions.Count - 1);
            Vector2 originalAnchorMin = sceneSlotAnchorMins[sourceIndex];
            Vector2 originalAnchorMax = sceneSlotAnchorMaxs[sourceIndex];
            float leftRelativeX = sceneMenuWidth * originalAnchorMin.x + sceneSlotPositions[sourceIndex].x;
            rect.anchorMin = new Vector2(0f, originalAnchorMin.y);
            rect.anchorMax = new Vector2(0f, originalAnchorMax.y);
            rect.pivot = templateRect.pivot;
            rect.sizeDelta = templateRect.sizeDelta;
            rect.anchoredPosition = new Vector2(leftRelativeX + provinceIndex * groupWidth, sceneSlotPositions[sourceIndex].y);
        }
        RectTransform menuRect = GetComponent<RectTransform>();
        menuRect.sizeDelta = new Vector2(expandedWidth, sceneMenuHeight);
        // Keep the original left edge fixed and grow the region building list to the right.
        menuRect.anchoredPosition = sceneMenuPosition + Vector2.right * (addedWidth * menuRect.pivot.x);
    }

    private static List<Province> GetDisplayedProvinces(Province selectedProvince)
    {
        List<Province> result = new List<Province>();
        if (selectedProvince == null) return result;
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(selectedProvince.region) : null;
        if (region != null && region.provincelist != null)
            foreach (Province province in region.provincelist)
                if (selectedProvince.nation != null && province != null && province.nation == selectedProvince.nation)
                    result.Add(province);
        if (result.Count == 0 && selectedProvince.nation != null) result.Add(selectedProvince);
        return result;
    }

    private void EnsureSlotCount(int required)
    {
        ResolveSlots();
        if (required <= slots.Count || slots.Count == 0) return;
        UIBuildingMenuSlot template = slots[0];
        Transform parent = template.transform.parent;
        while (slots.Count < required)
        {
            UIBuildingMenuSlot clone = Instantiate(template, parent);
            clone.gameObject.name = "RegionBuildingSlot_" + slots.Count.ToString("D2");
            generatedSlots.Add(clone);
            slots.Add(clone);
        }
    }

    private void ResolveSlots()
    {
        slots.Clear();
        slots.AddRange(GetComponentsInChildren<UIBuildingMenuSlot>(true));
        slots.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
    }

    private void ResolvePanelProvinceName()
    {
        if (panelProvinceName != null && panelProvinceName.transform.IsChildOf(transform)) return;
        panelProvinceName = null;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform || !child.name.Equals("ProvinceName", System.StringComparison.OrdinalIgnoreCase)) continue;
            panelProvinceName = child.GetComponent<Text>();
            if (panelProvinceName != null) return;
        }
    }

    public void PointerEntered(UIBuildingMenuSlot slot)
    {
        if (slot == null || buildGridOpen) return;
        ShowTooltip(BuildBuildingDescription(slot.Province, slot.TooltipBuilding, slot.SlotIndex));
    }

    public void PointerExited(UIBuildingMenuSlot slot)
    {
        if (buildGridOpen) return;
        HideTooltip();
    }

    public void SlotClicked(UIBuildingMenuSlot slot)
    {
        selectedSlot = slot;
        HideTooltip();
        ShowBuildGrid(slot);
    }

    private void ShowBuildGrid(UIBuildingMenuSlot slot)
    {
        EnsureBuildGrid();
        ClearBuildGrid();
        buildGridOpen = true;
        buildGridRoot.SetActive(true);
        buildGridRoot.transform.SetAsLastSibling();
        Province targetProvince = slot != null ? slot.Province : null;
        if (slot == null || targetProvince == null) { AddGridMessage("No province slot selected."); return; }

        ProvinceBuilding building = slot.Building;
        if (building == null)
        {
            Nation nation = targetProvince.nation;
            foreach (string buildingId in NationContentResolver.ResolveBuildings(nation))
            {
                if (!NationContentResolver.CanConstructBuildingLevel(nation, buildingId, 1)) continue;
                BuildingDefinition definition = BuildingDefinition.Find(buildingId);
                if (definition != null && definition.isSpecialization) continue;
                AddBuildOption(slot, buildingId, 1);
            }
        }
        else if (building.definition != null && building.definition.upgradeOptions != null && building.definition.upgradeOptions.Count > 0)
        {
            foreach (BuildingDefinition upgrade in building.definition.upgradeOptions)
                if (upgrade != null) AddBuildOption(slot, upgrade.StableId, 1);
        }
        else if (building.level < NationContentResolver.UsefulBuildingMaximumLevel(targetProvince.nation, building.BuildingId))
        {
            string buildingId = building.BuildingId;
            if (!NationContentResolver.HasBuilding(targetProvince.nation, buildingId))
            {
                AddGridMessage(building.DisplayName + " is not available to this nation.");
                AddDestroyOption(slot);
                AddCancelOption();
                return;
            }
            int nextLevel = building.level + 1;
            AddBuildOption(slot, buildingId, nextLevel);
        }
        else AddGridMessage(building.DisplayName + " is already at maximum level.");
        if (building != null) AddDestroyOption(slot);
        AddCancelOption();
    }

    private void AddDestroyOption(UIBuildingMenuSlot slot)
    {
        GameObject option = new GameObject("Destroy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        option.layer = gameObject.layer; option.transform.SetParent(buildGridRoot.transform, false);
        option.GetComponent<Image>().color = new Color(.48f, .12f, .1f, .98f);
        LayoutElement layout = option.GetComponent<LayoutElement>(); layout.preferredWidth = 82f; layout.preferredHeight = 82f;
        Text text = CreateGridText(option.transform, "Destroy", 11, TextAnchor.MiddleCenter);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        option.GetComponent<Button>().onClick.AddListener(() => DestroyBuilding(slot));
        LayoutBuildGrid();
    }

    private void DestroyBuilding(UIBuildingMenuSlot slot)
    {
        Province targetProvince = slot != null ? slot.Province : null;
        if (targetProvince == null || slot.Building == null) return;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            if (CampaignNetworkPlayer.Local != null)
                CampaignNetworkPlayer.Local.RequestDestroyProvinceBuilding(targetProvince.name, slot.SlotIndex);
        }
        else targetProvince.DestroyBuildingInSlot(slot.SlotIndex);
        buildGridOpen = false;
        HideBuildGrid();
        LoadProvince(LoadedProvince, host);
    }

    private void AddCancelOption()
    {
        GameObject option = new GameObject("Cancel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        option.layer = gameObject.layer; option.transform.SetParent(buildGridRoot.transform, false);
        option.GetComponent<Image>().color = new Color(.32f, .18f, .18f, .98f);
        LayoutElement layout = option.GetComponent<LayoutElement>(); layout.preferredWidth = 82f; layout.preferredHeight = 82f;
        Text text = CreateGridText(option.transform, "Cancel", 11, TextAnchor.MiddleCenter);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        option.GetComponent<Button>().onClick.AddListener(() => { buildGridOpen = false; HideBuildGrid(); });
        LayoutBuildGrid();
    }

    private void AddBuildOption(UIBuildingMenuSlot slot, string buildingId, int targetLevel)
    {
        GameObject option = new GameObject(buildingId + " Option", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(UIBuildingOptionHover));
        option.layer = gameObject.layer;
        option.transform.SetParent(buildGridRoot.transform, false);
        option.GetComponent<Image>().color = new Color(.2f, .34f, .22f, .98f);
        LayoutElement layout = option.GetComponent<LayoutElement>(); layout.preferredWidth = 82f; layout.preferredHeight = 82f;
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        Sprite icon = definition != null ? definition.icon : null;
        if (icon != null)
        {
            GameObject iconObject = new GameObject("BuildingIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = option.layer;
            iconObject.transform.SetParent(option.transform, false);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0f, .3f); iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(5f, 3f); iconRect.offsetMax = new Vector2(-5f, -5f);
        }

        string displayName = definition != null ? definition.DisplayName : buildingId;
        Text text = CreateGridText(option.transform, displayName + "\nLv " + targetLevel, 10, TextAnchor.MiddleCenter);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 7;
        text.resizeTextMaxSize = 10;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = icon != null ? new Vector2(1f, .32f) : Vector2.one;
        text.rectTransform.offsetMin = new Vector2(3f, 2f);
        text.rectTransform.offsetMax = new Vector2(-3f, -1f);
        option.GetComponent<Button>().onClick.AddListener(() => BeginConstruction(slot, buildingId, targetLevel));
        if (definition != null && !BuildingPlacementSystem.CanPlace(slot != null ? slot.Province : null,
            definition, slot != null ? slot.SlotIndex : -1, out _)) option.GetComponent<Button>().interactable = false;
        option.GetComponent<UIBuildingOptionHover>().Configure(this, slot != null ? slot.Province : null, buildingId, targetLevel);
        LayoutBuildGrid();
    }

    public void ProspectiveBuildingEntered(Province province, string buildingId, int targetLevel)
    {
        ShowTooltip(BuildProspectiveDescription(province, buildingId, targetLevel));
    }

    public void ProspectiveBuildingExited()
    {
        if (buildGridOpen) HideTooltip();
    }

    private string BuildProspectiveDescription(Province province, string buildingId, int targetLevel)
    {
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        ProvinceBuilding prospective = new ProvinceBuilding
        {
            definition = definition,
            id = buildingId,
            level = Mathf.Max(1, targetLevel),
            maxLevel = definition != null ? definition.maximumLevel : ProvinceBuilding.MaximumLevelFor(buildingId)
        };
        StringBuilder text = new StringBuilder(BuildBuildingDescription(province, prospective, -1));
        text.Append("\n\nConstruction:");
        text.Append("\n- Cost: ").Append(CampaignEconomy.BuildingGoldCost(buildingId, targetLevel)).Append(" gold");
        text.Append("\n- Time: ").Append(BuildingDefinition.ConstructionTicks(buildingId, targetLevel)).Append(" ticks");
        if (definition != null)
        {
            text.Append("\n- Placement: ").Append(definition.EffectivePlacementLimit == BuildingPlacementLimit.Unlimited
                ? "Unlimited" : definition.EffectivePlacementLimit == BuildingPlacementLimit.ProvinceUnique ? "Province Unique" : "Region Unique");
            if (!BuildingPlacementSystem.CanPlace(province, definition, -1, out string reason)) text.Append("\n- Cannot build: ").Append(reason);
        }
        return text.ToString();
    }

    private void BeginConstruction(UIBuildingMenuSlot slot, string buildingId, int targetLevel)
    {
        Province targetProvince = slot != null ? slot.Province : null;
        if (targetProvince == null || slot == null) return;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            if (CampaignNetworkPlayer.Local != null)
                CampaignNetworkPlayer.Local.RequestProvinceBuilding(targetProvince.name, slot.SlotIndex, buildingId, targetLevel);
            buildGridOpen = false;
            HideBuildGrid();
            return;
        }
        Nation owner = targetProvince.nation; int goldCost = CampaignEconomy.BuildingGoldCost(buildingId, targetLevel);
        int constructionTicks = TestConstructionTicks >= 0 ? TestConstructionTicks
            : BuildingDefinition.ConstructionTicks(buildingId, targetLevel);
        if (owner == null || owner.Gold < goldCost ||
            !targetProvince.BeginBuildingConstruction(slot.SlotIndex, buildingId, targetLevel, constructionTicks)) return;
        owner.Gold -= goldCost;
        buildGridOpen = false;
        HideBuildGrid();
        LoadProvince(LoadedProvince, host);
    }

    private void EnsureBuildGrid()
    {
        if (buildGridRoot != null) return;
        Transform parent = host != null ? host.transform : transform.parent;
        buildGridRoot = new GameObject("BuildingConstructionGrid", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(GridLayoutGroup));
        buildGridRoot.layer = gameObject.layer;
        buildGridRoot.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)buildGridRoot.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, -130f);
        rect.sizeDelta = new Vector2(190f, 245f);
        buildGridRoot.GetComponent<Image>().color = new Color(.07f, .07f, .07f, .97f);
        GridLayoutGroup grid = buildGridRoot.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(8, 8, 8, 8); grid.spacing = new Vector2(6f, 6f);
        grid.cellSize = new Vector2(82f, 82f); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
    }

    private Text CreateGridText(Transform parent, string contents, int size, TextAnchor alignment)
    {
        GameObject child = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.layer = gameObject.layer; child.transform.SetParent(parent, false);
        Text text = child.GetComponent<Text>();
        Text existing = host != null ? host.GetComponentInChildren<Text>(true) : null;
        text.font = existing != null ? existing.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size; text.color = Color.white; text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false;
        text.text = contents; return text;
    }

    private void AddGridMessage(string message)
    {
        Text text = CreateGridText(buildGridRoot.transform, message, 12, TextAnchor.MiddleCenter);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>(); layout.preferredWidth = 170f; layout.preferredHeight = 82f;
        LayoutBuildGrid();
    }

    private void LayoutBuildGrid()
    {
        if (buildGridRoot == null) return;
        GridLayoutGroup grid = buildGridRoot.GetComponent<GridLayoutGroup>();
        RectTransform rect = buildGridRoot.GetComponent<RectTransform>();
        int columns = Mathf.Max(1, grid.constraintCount);
        int rows = Mathf.Max(1, Mathf.CeilToInt(buildGridRoot.transform.childCount / (float)columns));
        float height = grid.padding.top + grid.padding.bottom + rows * grid.cellSize.y +
            Mathf.Max(0, rows - 1) * grid.spacing.y;
        rect.sizeDelta = new Vector2(190f, height);
        Canvas.ForceUpdateCanvases();
        ClampRectToCanvas(rect);
    }

    private void ClearBuildGrid()
    {
        if (buildGridRoot == null) return;
        for (int i = buildGridRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = buildGridRoot.transform.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void HideBuildGrid()
    {
        if (buildGridRoot != null) buildGridRoot.SetActive(false);
        HideTooltip();
    }

    private string BuildBuildingDescription(Province province, ProvinceBuilding building, int slotIndex)
    {
        if (building == null) return "Building slot " + (slotIndex + 1) + "\n\nEmpty";
        if (building.definition == null) building.definition = BuildingDefinition.Find(building.id);
        StringBuilder text = new StringBuilder();
        text.Append(building.DisplayName);
        if (building.definition != null && !string.IsNullOrWhiteSpace(building.definition.description))
            text.Append("\n\n").Append(building.definition.description);
        AppendEffects(text, province, building);
        return text.ToString();
    }

    private string BuildUpgradeDescription(Province province, ProvinceBuilding building, int slotIndex)
    {
        if (building == null)
            return "Building slot " + (slotIndex + 1) + "\n\nNo building is present, so this slot has no upgrades.";

        StringBuilder text = new StringBuilder();
        string buildingId = string.IsNullOrEmpty(building.id) ? "Building" : building.id;
        text.Append(buildingId).Append(" upgrades\n\n");
        int usefulMaximum = province != null
            ? NationContentResolver.UsefulBuildingMaximumLevel(province.nation, buildingId)
            : building.EffectiveMaximumLevel;
        if (building.level >= usefulMaximum)
        {
            text.Append("Maximum useful level reached (Level ").Append(usefulMaximum).Append(").");
            return text.ToString();
        }

        int nextLevel = building.level + 1;
        text.Append("Level ").Append(building.level).Append(" -> Level ").Append(nextLevel);
        text.Append("\nCost: ").Append(CampaignEconomy.BuildingGoldCost(buildingId, nextLevel)).Append(" gold");
        if (province != null && province.nation != null)
        {
            List<string> unlocks = NationContentResolver.ResolveUnits(province.nation)
                .FindAll(entry => entry != null && entry.unit != null &&
                    entry.RequiredBuildingId.Equals(buildingId, System.StringComparison.OrdinalIgnoreCase) &&
                    entry.minimumBuildingLevel == nextLevel)
                .ConvertAll(entry => entry.unit.unitname);
            if (unlocks.Count > 0) text.Append("\nUnlocks recruitment: ").Append(string.Join(", ", unlocks));
        }
        if (buildingId.Equals("Fort", System.StringComparison.OrdinalIgnoreCase))
        {
            text.Append("\nGarrison capacity: ")
                .Append(6 + building.level * 3).Append(" -> ").Append(6 + nextLevel * 3);
            text.Append("\nAdds 3 garrison troops.");
        }
        text.Append("\n\nNo construction cost is configured in ProvinceBuilding yet.");
        return text.ToString();
    }

    private void AppendEffects(StringBuilder text, Province province, ProvinceBuilding building)
    {
        List<string> names = new List<string>();
        if (building.explicitUnitUnlocks != null)
            for (int i = 0; i < building.explicitUnitUnlocks.Count; i++)
                if (building.explicitUnitUnlocks[i] != null) names.Add(building.explicitUnitUnlocks[i].unitname);

        if (building.definition != null && building.definition.levels != null)
            foreach (BuildingLevelDefinition level in building.definition.levels)
                if (level != null && level.level <= building.level && level.unitUnlocks != null)
                    foreach (UnitSaveData unit in level.unitUnlocks)
                        if (unit != null && !names.Contains(unit.unitname)) names.Add(unit.unitname);

        if (province != null)
        {
            if (province.nation != null)
                foreach (NationUnitEntry entry in NationContentResolver.ResolveUnits(province.nation))
                    if (entry != null && entry.unit != null && entry.RequiredBuildingId.Equals(building.BuildingId,
                        System.StringComparison.OrdinalIgnoreCase) && entry.minimumBuildingLevel <= building.level &&
                        !names.Contains(entry.unit.unitname)) names.Add(entry.unit.unitname);
        }
        text.Append("\n\nProvides:");
        bool any = false;
        int urbanizationTarget = 0;
        Dictionary<HoldingTag, float> holdingEfficiency = new Dictionary<HoldingTag, float>();
        Dictionary<HoldingTag, float> holdingPressure = new Dictionary<HoldingTag, float>();
        if (building.definition != null && building.definition.levels != null)
            foreach (BuildingLevelDefinition level in building.definition.levels)
            {
                if (level == null || level.level > building.level) continue;
                urbanizationTarget += level.urbanizationTargetModifier;
                if (level.holdingEconomyModifiers == null) continue;
                foreach (HoldingTagModifier modifier in level.holdingEconomyModifiers)
                {
                    if (modifier == null || modifier.tag == HoldingTag.None ||
                        !string.IsNullOrWhiteSpace(modifier.requiredNationFlag) &&
                        (province == null || !NationContentResolver.HasFlag(province.nation, modifier.requiredNationFlag))) continue;
                    holdingEfficiency[modifier.tag] = holdingEfficiency.TryGetValue(modifier.tag, out float efficiency)
                        ? efficiency + modifier.outputEfficiencyPercent : modifier.outputEfficiencyPercent;
                    holdingPressure[modifier.tag] = holdingPressure.TryGetValue(modifier.tag, out float pressure)
                        ? pressure + modifier.desiredWeight : modifier.desiredWeight;
                }
            }
        if (building.definition != null && building.definition.economicEffects != null)
            foreach (BuildingEconomicEffect effect in building.definition.economicEffects)
            {
                if (effect == null || Mathf.Approximately(effect.amount, 0f)) continue;
                string prefix = effect.scope == BuildingEffectScope.Region ? "Region: " : string.Empty;
                switch (effect.type)
                {
                    case BuildingEconomicEffectType.HoldingTypePressure:
                        text.Append("\n- ").Append(prefix).Append(Signed(effect.amount)).Append(" ").Append(effect.holdingType).Append(" Pressure"); break;
                    case BuildingEconomicEffectType.ClassPressure:
                        text.Append("\n- ").Append(prefix).Append(Signed(effect.amount)).Append(" ")
                            .Append(SocioEconomicClassRules.DisplayName(effect.socialClass)).Append(" Class Pressure"); break;
                    case BuildingEconomicEffectType.LevyTypePressure:
                        text.Append("\n- ").Append(prefix).Append(Signed(effect.amount)).Append(" ").Append(effect.levyType).Append(" Pressure"); break;
                    case BuildingEconomicEffectType.LocalLevyCapacity:
                        text.Append("\n- ").Append(prefix).Append(Signed(effect.amount)).Append(" Local Levy Capacity"); break;
                    case BuildingEconomicEffectType.EconomicValuePercent:
                        text.Append("\n- ").Append(prefix).Append(Signed(effect.amount)).Append("% ")
                            .Append(effect.outputType == HoldingOutputType.Income ? "Economic" : effect.outputType.ToString().Replace("Value", ""))
                            .Append(" Value"); break;
                    case BuildingEconomicEffectType.FoodOutputPercent:
                        text.Append("\n- ").Append(prefix).Append(Signed(effect.amount)).Append("% Food Output"); break;
                }
                any = true;
            }
        if (building.definition != null && building.definition.valueConversions != null)
            for (int conversionIndex = 0; conversionIndex < building.definition.valueConversions.Count; conversionIndex++)
            {
                BuildingValueConversion conversion = building.definition.valueConversions[conversionIndex];
                if (conversion == null || building.level < Mathf.Max(1, conversion.minimumLevel)) continue;
                text.Append("\n- Diverts up to ").Append(conversion.inputAmount.ToString("0.##")).Append(" ")
                    .Append(conversion.input).Append(" -> ").Append(conversion.outputAmount.ToString("0.##")).Append(" ")
                    .Append(conversion.output);
                if (province != null && building.slotIndex >= 0)
                    text.Append(" (operating at ").Append((ValueTradeSystem.OperatingFraction(province, building, conversionIndex) * 100f).ToString("0.#")).Append("%)");
                any = true;
            }
        int rawIncome = building.definition != null ? building.DefinitionGoldIncomeAt(province != null ? province.urbanization : 0) :
            building.BuildingId.Equals("Farm", System.StringComparison.OrdinalIgnoreCase) ? building.level * CampaignEconomy.FarmIncomePerLevel : 0;
        int gold = CampaignEconomy.ApplyGoldIncomeRate(rawIncome);
        if (gold != 0)
        {
            text.Append("\n- Gold: ").Append(Signed(gold));
            any = true;
        }
        int food = building.definition != null ? building.DefinitionFoodOutputAt(province != null ? province.urbanization : 0) : 0;
        if (food > 0)
        {
            text.Append("\n- Food: +").Append(food).Append(" produced");
            any = true;
        }
        int foodConsumption = building.definition != null ? building.DefinitionFoodConsumption : 0;
        if (foodConsumption > 0)
        {
            text.Append("\n- Food: -").Append(foodConsumption).Append(" consumed");
            any = true;
        }
        int goldUpkeep = building.definition != null ? building.DefinitionGoldUpkeep : 0;
        if (goldUpkeep > 0)
        {
            text.Append("\n- Upkeep: -").Append(goldUpkeep).Append(" gold");
            any = true;
        }
        int garrison = building.definition != null ? building.DefinitionGarrisonCapacity :
            building.BuildingId.Equals("Fort", System.StringComparison.OrdinalIgnoreCase) ? building.level * 3 : 0;
        if (garrison > 0) { text.Append("\n- Garrison capacity: +").Append(garrison); any = true; }
        float manpowerRecovery = building.definition != null ? building.DefinitionManpowerRecovery : 0f;
        if (manpowerRecovery > 0f)
        { text.Append("\n- Manpower recovery: +").Append(manpowerRecovery.ToString("0.###")); any = true; }
        if (building.BuildingId.Equals("Fort", System.StringComparison.OrdinalIgnoreCase))
        { text.Append("\n- Regional loyalty: +").Append((building.level * .1f).ToString("0.#")); any = true; }
        if (building.BuildingId.Equals("Temple", System.StringComparison.OrdinalIgnoreCase))
        {
            text.Append("\n- Regional loyalty: +").Append((building.level * .1f).ToString("0.#"));
            text.Append("\n- Culture conversion: +").Append((building.level * .1f).ToString("0.#"))
                .Append("% national primary culture");
            any = true;
        }
        if (urbanizationTarget != 0)
        {
            text.Append("\n- Urbanization target: ").Append(Signed(urbanizationTarget));
            any = true;
        }
        foreach (KeyValuePair<HoldingTag, float> entry in holdingEfficiency)
            if (!Mathf.Approximately(entry.Value, 0f))
            { text.Append("\n- ").Append(entry.Key).Append(" holding efficiency: ").Append(Signed(entry.Value)).Append("%"); any = true; }
        foreach (KeyValuePair<HoldingTag, float> entry in holdingPressure)
            if (!Mathf.Approximately(entry.Value, 0f))
            { text.Append("\n- ").Append(entry.Key).Append(" holding pressure: ").Append(Signed(entry.Value)); any = true; }
        if (names.Count > 0)
        { text.Append("\n- Recruitment unlocks: ").Append(string.Join(", ", names)); any = true; }
        if (!any) text.Append("\n- No configured effects.");
    }

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
    private static string Signed(float value) => (value > 0f ? "+" : string.Empty) + value.ToString("0.#");

    private void EnsureTooltip()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform desiredParent = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform
            : host != null ? host.transform : transform.parent;
        if (tooltipRoot != null)
        {
            if (tooltipRoot.transform.parent != desiredParent)
                tooltipRoot.transform.SetParent(desiredParent, false);
            if (tooltipText == null) tooltipText = tooltipRoot.GetComponentInChildren<Text>(true);
            EnsureTooltipTextStyle();
            PositionTooltip();
            return;
        }
        tooltipRoot = new GameObject("BuildingTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipRoot.layer = gameObject.layer;
        tooltipRoot.transform.SetParent(desiredParent, false);
        RectTransform rect = (RectTransform)tooltipRoot.transform;
        rect.sizeDelta = new Vector2(190f, 245f);
        Image tooltipBackground = tooltipRoot.GetComponent<Image>();
        tooltipBackground.color = new Color(.08f, .08f, .08f, .96f);
        // The tooltip may be clamped back over its source slot near a screen edge.
        // It is informational, so it must never steal the pointer and generate an
        // exit/enter loop on the slot beneath it.
        tooltipBackground.raycastTarget = false;

        GameObject textObject = new GameObject("TooltipText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(tooltipRoot.transform, false);
        tooltipText = textObject.GetComponent<Text>();
        EnsureTooltipTextStyle();
        RectTransform textRect = tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 8f); textRect.offsetMax = new Vector2(-8f, -8f);
        PositionTooltip();
        tooltipRoot.SetActive(false);
    }

    private void PositionTooltip()
    {
        if (tooltipRoot == null) return;
        RectTransform tooltipRect = (RectTransform)tooltipRoot.transform;
        RectTransform menuRect = GetComponent<RectTransform>();
        RectTransform parentRect = tooltipRect.parent as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (menuRect == null || parentRect == null || canvas == null) return;
        Canvas rootCanvas = canvas.rootCanvas;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector3[] corners = new Vector3[4];
        menuRect.GetWorldCorners(corners);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]) + new Vector2(10f, 0f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, eventCamera, out Vector2 local)) return;
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(.5f, .5f);
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.localPosition = new Vector3(local.x, local.y, 0f);
        ClampRectToCanvas(tooltipRect);
    }

    private void EnsureTooltipTextStyle()
    {
        if (tooltipText == null) return;
        Text existing = host != null ? host.GetComponentInChildren<Text>(true) : null;
        Font font = existing != null ? existing.font : null;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tooltipText.font = font;
        tooltipText.fontSize = 12;
        tooltipText.color = new Color(1f, 1f, 1f, 1f);
        tooltipText.alignment = TextAnchor.UpperLeft;
        tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
        tooltipText.raycastTarget = false;
        tooltipText.canvasRenderer.SetAlpha(1f);
        tooltipText.rectTransform.localScale = Vector3.one;
    }

    private void ClampRectToCanvas(RectTransform tooltipRect)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform parentRect = tooltipRect != null ? tooltipRect.parent as RectTransform : null;
        if (canvas == null || tooltipRect == null || parentRect == null) return;
        Canvas rootCanvas = canvas.rootCanvas;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Rect bounds = rootCanvas.pixelRect;
        Vector3[] corners = new Vector3[4]; tooltipRect.GetWorldCorners(corners);
        Vector2 minimum = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
        Vector2 maximum = minimum;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
            minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
        }
        const float margin = 8f;
        Vector2 shift = Vector2.zero;
        if (minimum.x < bounds.xMin + margin) shift.x += bounds.xMin + margin - minimum.x;
        if (maximum.x > bounds.xMax - margin) shift.x -= maximum.x - (bounds.xMax - margin);
        if (minimum.y < bounds.yMin + margin) shift.y += bounds.yMin + margin - minimum.y;
        if (maximum.y > bounds.yMax - margin) shift.y -= maximum.y - (bounds.yMax - margin);
        if (shift.sqrMagnitude <= .01f) return;
        Vector2 pivotScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, tooltipRect.position) + shift;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, pivotScreen, eventCamera, out Vector2 local))
            tooltipRect.localPosition = new Vector3(local.x, local.y, tooltipRect.localPosition.z);
    }

    private void ShowTooltip(string contents)
    {
        EnsureTooltip();
        if (tooltipRoot == null || tooltipText == null) return;
        RectTransform tooltipRect = (RectTransform)tooltipRoot.transform;
        displayedTooltipContents = string.IsNullOrWhiteSpace(contents)
            ? "No building information is available."
            : contents;
        tooltipText.text = displayedTooltipContents;
        tooltipText.enabled = true;
        tooltipText.gameObject.SetActive(true);
        if (!tooltipRoot.activeSelf) tooltipRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        tooltipRect.sizeDelta = new Vector2(190f, Mathf.Clamp(tooltipText.preferredHeight + 20f, 245f, 440f));
        PositionTooltip();
        tooltipRoot.transform.SetAsLastSibling();
        tooltipText.transform.SetAsLastSibling();
    }

    private void HideTooltip()
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }

    private void OnDisable()
    {
        // Building tooltips are parented to the root canvas so they can escape the
        // administration panel's bounds. Explicitly close them with their owner.
        HideTooltip();
        if (buildGridRoot != null) buildGridRoot.SetActive(false);
        buildGridOpen = false;
    }
}

public sealed class UIBuildingOptionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private UIBuildingMenu menu;
    private Province province;
    private string buildingId;
    private int targetLevel;

    public void Configure(UIBuildingMenu owner, Province targetProvince, string id, int level)
    {
        menu = owner; province = targetProvince; buildingId = id; targetLevel = level;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menu != null) menu.ProspectiveBuildingEntered(province, buildingId, targetLevel);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (menu != null) menu.ProspectiveBuildingExited();
    }
}
