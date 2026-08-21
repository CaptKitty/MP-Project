using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UIBuildingMenu : MonoBehaviour
{
    public Province LoadedProvince { get; private set; }

    private readonly List<UIBuildingMenuSlot> slots = new List<UIBuildingMenuSlot>();
    private readonly List<UIBuildingMenuSlot> generatedSlots = new List<UIBuildingMenuSlot>();
    private readonly List<Vector2> sceneSlotPositions = new List<Vector2>();
    private readonly List<float> sceneSlotTopOffsets = new List<float>();
    private int sceneSlotCount;
    private float sceneMenuHeight;
    private Vector2 sceneMenuPosition;
    private UIProvinceHost host;
    private UIBuildingMenuSlot selectedSlot;
    private GameObject tooltipRoot;
    private Text tooltipText;
    private GameObject buildGridRoot;
    private bool buildGridOpen;
    public int TestConstructionTicks = -1;

    private void Awake()
    {
        ResolveSlots();
        sceneSlotCount = slots.Count;
        RectTransform menuRect = GetComponent<RectTransform>();
        sceneMenuHeight = menuRect.sizeDelta.y;
        sceneMenuPosition = menuRect.anchoredPosition;
        float menuTop = sceneMenuHeight * (1f - menuRect.pivot.y);
        for (int i = 0; i < sceneSlotCount; i++)
        {
            RectTransform slotRect = slots[i].GetComponent<RectTransform>();
            sceneSlotPositions.Add(slotRect.anchoredPosition);
            float slotPivotY = menuRect.InverseTransformPoint(slotRect.position).y;
            sceneSlotTopOffsets.Add(menuTop - slotPivotY);
        }
    }

    public void LoadProvince(Province province, UIProvinceHost owner)
    {
        host = owner;
        LoadedProvince = province;
        selectedSlot = null;
        buildGridOpen = false;
        ResolveSlots();
        EnsureTooltip();
        HideTooltip();
        HideBuildGrid();

        List<Province> provinces = GetDisplayedProvinces(province);
        int slotsPerProvince = Mathf.Max(4, sceneSlotCount);
        EnsureSlotCount(provinces.Count * slotsPerProvince);
        LayoutRegionSlots(provinces.Count, slotsPerProvince);
        int displayIndex = 0;
        foreach (Province displayedProvince in provinces)
        {
            for (int slotIndex = 0; slotIndex < slotsPerProvince; slotIndex++)
            {
                ProvinceBuilding building = displayedProvince != null ? displayedProvince.GetBuildingInSlot(slotIndex) : null;
                slots[displayIndex].gameObject.SetActive(true);
                slots[displayIndex].Configure(this, displayedProvince, building, slotIndex, provinces.Count > 1);
                displayIndex++;
            }
        }
        for (int i = displayIndex; i < slots.Count; i++) slots[i].gameObject.SetActive(false);
    }

    private void LayoutRegionSlots(int provinceCount, int slotsPerProvince)
    {
        if (sceneSlotCount == 0 || sceneSlotPositions.Count == 0) return;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < sceneSlotCount; i++)
        {
            RectTransform rect = slots[i].GetComponent<RectTransform>();
            float halfHeight = rect.rect.height * .5f;
            minY = Mathf.Min(minY, sceneSlotPositions[i].y - halfHeight);
            maxY = Mathf.Max(maxY, sceneSlotPositions[i].y + halfHeight);
        }
        float groupHeight = Mathf.Max(1f, maxY - minY + 8f);
        int visibleCount = provinceCount * slotsPerProvince;
        for (int i = 0; i < visibleCount && i < slots.Count; i++)
        {
            int templateIndex = i % slotsPerProvince;
            int provinceIndex = i / slotsPerProvince;
            RectTransform rect = slots[i].GetComponent<RectTransform>();
            RectTransform templateRect = slots[Mathf.Min(templateIndex, sceneSlotCount - 1)].GetComponent<RectTransform>();
            rect.anchorMin = templateRect.anchorMin;
            rect.anchorMax = templateRect.anchorMax;
            rect.anchorMin = new Vector2(rect.anchorMin.x, 1f);
            rect.anchorMax = new Vector2(rect.anchorMax.x, 1f);
            rect.pivot = templateRect.pivot;
            rect.sizeDelta = templateRect.sizeDelta;
            int sourceIndex = Mathf.Min(templateIndex, sceneSlotPositions.Count - 1);
            rect.anchoredPosition = new Vector2(sceneSlotPositions[sourceIndex].x,
                -sceneSlotTopOffsets[sourceIndex] - provinceIndex * groupHeight);
        }
        RectTransform menuRect = GetComponent<RectTransform>();
        float expandedHeight = sceneMenuHeight + Mathf.Max(0, provinceCount - 1) * groupHeight;
        float addedHeight = expandedHeight - sceneMenuHeight;
        menuRect.sizeDelta = new Vector2(menuRect.sizeDelta.x, expandedHeight);
        // Keep the original top edge fixed and grow the region building list downward.
        menuRect.anchoredPosition = sceneMenuPosition +
            Vector2.down * (addedHeight * (1f - menuRect.pivot.y));
    }

    private static List<Province> GetDisplayedProvinces(Province selectedProvince)
    {
        List<Province> result = new List<Province>();
        if (selectedProvince == null) return result;
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(selectedProvince.region) : null;
        if (region != null && region.provincelist != null)
            foreach (Province province in region.provincelist) if (province != null) result.Add(province);
        if (result.Count == 0) result.Add(selectedProvince);
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

    public void PointerEntered(UIBuildingMenuSlot slot)
    {
        if (slot == null || buildGridOpen) return;
        ShowTooltip(BuildBuildingDescription(slot.Province, slot.Building, slot.SlotIndex));
    }

    public void PointerExited(UIBuildingMenuSlot slot)
    {
        if (buildGridOpen) return;
        if (selectedSlot != null) ShowTooltip(BuildUpgradeDescription(selectedSlot.Province, selectedSlot.Building, selectedSlot.SlotIndex));
        else HideTooltip();
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
                AddBuildOption(slot, buildingId, 1, buildingId + "\nLevel 1\n" +
                    CampaignEconomy.BuildingGoldCost(buildingId, 1) + " gold\n" +
                    BuildingDefinition.ConstructionTicks(buildingId, 1) + " ticks");
        }
        else if (building.level < building.EffectiveMaximumLevel)
        {
            string buildingId = building.BuildingId;
            if (!NationContentResolver.HasBuilding(targetProvince.nation, buildingId))
            {
                AddGridMessage(building.DisplayName + " is not available to this nation.");
                AddCancelOption();
                return;
            }
            int nextLevel = building.level + 1;
            string caption = building.DisplayName + "\nLevel " + nextLevel;
            caption += "\n" + CampaignEconomy.BuildingGoldCost(buildingId, nextLevel) + " gold";
            caption += "\n" + BuildingDefinition.ConstructionTicks(buildingId, nextLevel) + " ticks";
            if (targetProvince.nation != null && NationContentResolver.ResolveUnits(targetProvince.nation)
                .Exists(entry => entry != null && entry.RequiredBuildingId.Equals(buildingId,
                    System.StringComparison.OrdinalIgnoreCase) && entry.minimumBuildingLevel == nextLevel))
                caption += "\nUnlocks unit tier " + nextLevel;
            AddBuildOption(slot, buildingId, nextLevel, caption);
        }
        else AddGridMessage(building.DisplayName + " is already at maximum level.");
        AddCancelOption();
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
    }

    private void AddBuildOption(UIBuildingMenuSlot slot, string buildingId, int targetLevel, string caption)
    {
        GameObject option = new GameObject(buildingId + " Option", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        option.layer = gameObject.layer;
        option.transform.SetParent(buildGridRoot.transform, false);
        option.GetComponent<Image>().color = new Color(.2f, .34f, .22f, .98f);
        LayoutElement layout = option.GetComponent<LayoutElement>(); layout.preferredWidth = 82f; layout.preferredHeight = 82f;
        Text text = CreateGridText(option.transform, caption, 11, TextAnchor.MiddleCenter);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(3f, 3f); text.rectTransform.offsetMax = new Vector2(-3f, -3f);
        option.GetComponent<Button>().onClick.AddListener(() => BeginConstruction(slot, buildingId, targetLevel));
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
    }

    private void ClearBuildGrid()
    {
        if (buildGridRoot == null) return;
        for (int i = buildGridRoot.transform.childCount - 1; i >= 0; i--) Destroy(buildGridRoot.transform.GetChild(i).gameObject);
    }

    private void HideBuildGrid()
    {
        if (buildGridRoot != null) buildGridRoot.SetActive(false);
    }

    private string BuildBuildingDescription(Province province, ProvinceBuilding building, int slotIndex)
    {
        string provinceName = province != null ? province.name + "\n" : string.Empty;
        if (building == null) return provinceName + "Building slot " + (slotIndex + 1) + "\n\nEmpty";
        StringBuilder text = new StringBuilder();
        text.Append(provinceName).Append(building.id).Append("\nLevel ").Append(building.level).Append(" / ").Append(building.maxLevel);
        AppendUnlocks(text, province, building);
        return text.ToString();
    }

    private string BuildUpgradeDescription(Province province, ProvinceBuilding building, int slotIndex)
    {
        if (building == null)
            return (province != null ? province.name + "\n" : string.Empty) + "Building slot " + (slotIndex + 1) + "\n\nNo building is present, so this slot has no upgrades.";

        StringBuilder text = new StringBuilder();
        string buildingId = string.IsNullOrEmpty(building.id) ? "Building" : building.id;
        if (province != null) text.Append(province.name).Append("\n");
        text.Append(buildingId).Append(" upgrades\n\n");
        if (building.level >= building.maxLevel)
        {
            text.Append("Maximum level reached (Level ").Append(building.maxLevel).Append(").");
            return text.ToString();
        }

        int nextLevel = building.level + 1;
        text.Append("Level ").Append(building.level).Append(" -> Level ").Append(nextLevel);
        text.Append("\nCost: ").Append(CampaignEconomy.BuildingGoldCost(buildingId, nextLevel)).Append(" gold");
        if (buildingId.Equals("Barracks", System.StringComparison.OrdinalIgnoreCase))
        {
            Faction localFaction = province != null && province.nation != null ? province.nation.faction : null;
            if (localFaction != null && localFaction.BarracksDataList != null && building.level < localFaction.BarracksDataList.Count)
            {
                UnitSaveData unlocked = localFaction.BarracksDataList[building.level];
                if (unlocked != null) text.Append("\nUnlocks recruitment: ").Append(unlocked.unitname);
            }
        }
        else if (buildingId.Equals("Fort", System.StringComparison.OrdinalIgnoreCase))
        {
            text.Append("\nGarrison capacity: ")
                .Append(6 + building.level * 3).Append(" -> ").Append(6 + nextLevel * 3);
            text.Append("\nAdds 3 garrison troops.");
        }
        text.Append("\n\nNo construction cost is configured in ProvinceBuilding yet.");
        return text.ToString();
    }

    private void AppendUnlocks(StringBuilder text, Province province, ProvinceBuilding building)
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
        if (names.Count > 0) text.Append("\n\nRecruitment unlocked:\n- ").Append(string.Join("\n- ", names));
        else if (building.DefinitionGarrisonCapacity > 0 ||
            building.BuildingId.Equals("Fort", System.StringComparison.OrdinalIgnoreCase))
            text.Append("\n\nAdditional garrison capacity: ").Append(building.definition != null
                ? building.DefinitionGarrisonCapacity : building.level * 3);
        else text.Append("\n\nNo configured recruitment unlocks.");
    }

    private void EnsureTooltip()
    {
        if (tooltipRoot != null)
        {
            PositionTooltip();
            return;
        }
        Transform parent = host != null ? host.transform : transform.parent;
        tooltipRoot = new GameObject("BuildingTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipRoot.layer = gameObject.layer;
        tooltipRoot.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)tooltipRoot.transform;
        rect.sizeDelta = new Vector2(190f, 245f);
        tooltipRoot.GetComponent<Image>().color = new Color(.08f, .08f, .08f, .96f);

        GameObject textObject = new GameObject("TooltipText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(tooltipRoot.transform, false);
        tooltipText = textObject.GetComponent<Text>();
        Text existing = host != null ? host.GetComponentInChildren<Text>(true) : null;
        tooltipText.font = existing != null ? existing.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tooltipText.fontSize = 12;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAnchor.UpperLeft;
        tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        tooltipText.verticalOverflow = VerticalWrapMode.Truncate;
        tooltipText.raycastTarget = false;
        RectTransform textRect = tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 8f); textRect.offsetMax = new Vector2(-8f, -8f);
        PositionTooltip();
    }

    private void PositionTooltip()
    {
        if (tooltipRoot == null) return;
        RectTransform tooltipRect = (RectTransform)tooltipRoot.transform;
        RectTransform menuRect = GetComponent<RectTransform>();
        tooltipRect.anchorMin = tooltipRect.anchorMax = menuRect.anchorMin;
        float menuTop = menuRect.anchoredPosition.y + menuRect.sizeDelta.y * (1f - menuRect.pivot.y);
        float tooltipY = menuTop - tooltipRect.sizeDelta.y * (1f - tooltipRect.pivot.y);
        float menuRight = menuRect.anchoredPosition.x + menuRect.sizeDelta.x * (1f - menuRect.pivot.x);
        float tooltipX = menuRight + 10f + tooltipRect.sizeDelta.x * tooltipRect.pivot.x;
        tooltipRect.anchoredPosition = new Vector2(tooltipX, tooltipY);
    }

    private void ShowTooltip(string contents)
    {
        EnsureTooltip();
        PositionTooltip();
        tooltipText.text = contents;
        tooltipRoot.SetActive(true);
        tooltipRoot.transform.SetAsLastSibling();
    }

    private void HideTooltip()
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }
}
