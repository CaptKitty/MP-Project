using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIRegionSummary : MonoBehaviour
{
    private Transform regionalTotalMenu;
    private Transform classMenu;
    private Text regionalOutputText;
    private Transform levyMenu;
    private Transform levyContent;
    private Transform levyRowTemplate;
    private readonly List<Transform> levyRows = new List<Transform>();
    private UIRegionalClassChart classChart;

    public void RefreshFor(Province selectedProvince)
    {
        ResolveReferences();
        List<Province> provinces = OccupiedRegionProvinces(selectedProvince);
        RefreshRegionalOutput(provinces);
        RefreshRegionalLevies(provinces, selectedProvince);
        RefreshClassComposition(provinces);
    }

    private void ResolveReferences()
    {
        if (regionalTotalMenu == null) regionalTotalMenu = FindNamedTransform("RegionalTotalMenu");
        if (classMenu == null) classMenu = FindNamedTransform("ClassMenu");
        if (regionalTotalMenu != null && regionalOutputText == null)
            regionalOutputText = regionalTotalMenu.GetComponentInChildren<Text>(true);
        if (levyMenu == null)
        {
            levyMenu = FindNamedTransform("LevyMenu");
            if (levyMenu != null)
            {
                levyContent = FindDescendant(levyMenu, "LevyContent") ?? levyMenu;
                foreach (Transform child in levyContent)
                    if (child.name.Equals("LevyHolder", System.StringComparison.OrdinalIgnoreCase))
                    { levyRowTemplate = child; break; }
                if (levyRowTemplate != null) levyRows.Add(levyRowTemplate);
            }
        }
        if (classMenu != null && classChart == null)
        {
            Image oldBackground = classMenu.GetComponent<Image>();
            if (oldBackground != null) oldBackground.enabled = false;
            classChart = classMenu.GetComponentInChildren<UIRegionalClassChart>(true);
            if (classChart == null)
            {
                GameObject chartObject = new GameObject("RegionalClassChart", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(UIRegionalClassChart));
                chartObject.layer = classMenu.gameObject.layer;
                chartObject.transform.SetParent(classMenu, false);
                RectTransform chartRect = chartObject.GetComponent<RectTransform>();
                chartRect.anchorMin = Vector2.zero; chartRect.anchorMax = Vector2.one;
                chartRect.offsetMin = chartRect.offsetMax = Vector2.zero;
                classChart = chartObject.GetComponent<UIRegionalClassChart>();
            }
        }
    }

    private void RefreshRegionalLevies(List<Province> provinces, Province selectedProvince)
    {
        if (levyMenu == null || levyRowTemplate == null) return;
        Nation owner = selectedProvince != null ? selectedProvince.nation : null;
        CampaignRegion region = selectedProvince != null && Owners.Instance != null
            ? Owners.Instance.CallRegionByString(selectedProvince.region) : null;
        bool callupsAllowed = owner != null && (region == null || region.AllowsLevyCallups(owner));
        Dictionary<UnitSaveData, Vector2Int> unitCounts = new Dictionary<UnitSaveData, Vector2Int>();
        foreach (Province province in provinces)
        {
            if (province == null || province.nation != owner || province.levyEntitlements == null) continue;
            foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
            {
                if (entitlement == null || entitlement.unit == null) continue;
                // Reconciliation leaves obsolete records marked ineligible. They are not part of
                // the current levy capacity and must not inflate the displayed maximum.
                if (callupsAllowed && !entitlement.eligible) continue;
                UnitSaveData unit = entitlement.unit;
                unitCounts.TryGetValue(unit, out Vector2Int counts);
                counts.y++;
                if (callupsAllowed && entitlement.eligible && entitlement.state == LevyEntitlementState.Available)
                    counts.x++;
                unitCounts[unit] = counts;
            }
        }
        List<UnitSaveData> units = new List<UnitSaveData>(unitCounts.Keys);
        units.Sort((left, right) =>
        {
            Vector2Int leftCounts = unitCounts[left];
            Vector2Int rightCounts = unitCounts[right];
            int byMaximum = rightCounts.y.CompareTo(leftCounts.y);
            if (byMaximum != 0) return byMaximum;
            int byAvailable = rightCounts.x.CompareTo(leftCounts.x);
            return byAvailable != 0 ? byAvailable : string.Compare(UnitName(left), UnitName(right),
                System.StringComparison.OrdinalIgnoreCase);
        });
        EnsureLevyRows(Mathf.Max(1, units.Count));
        for (int index = 0; index < levyRows.Count; index++)
        {
            bool populated = index < units.Count;
            levyRows[index].gameObject.SetActive(populated);
            if (!populated) continue;
            UnitSaveData unit = units[index];
            Vector2Int counts = unitCounts[unit];
            PopulateLevyRow(levyRows[index], unit, counts.x, counts.y, index);
        }
    }

    private void EnsureLevyRows(int required)
    {
        levyRows.RemoveAll(row => row == null);
        while (levyRows.Count < required)
        {
            Transform row = Instantiate(levyRowTemplate, levyContent);
            row.name = "LevyHolder";
            levyRows.Add(row);
        }
    }

    private void PopulateLevyRow(Transform row, UnitSaveData unit, int available, int maximum, int index)
    {
        RectTransform menuRect = levyContent as RectTransform;
        RectTransform rowRect = row as RectTransform;
        if (rowRect != null)
        {
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(.5f, 1f);
            rowRect.pivot = new Vector2(.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * Mathf.Max(30f, rowRect.sizeDelta.y));
            if (menuRect != null && menuRect.sizeDelta.y < (index + 1) * rowRect.sizeDelta.y)
                menuRect.sizeDelta = new Vector2(menuRect.sizeDelta.x, (index + 1) * rowRect.sizeDelta.y);
        }
        Transform visualizer = FindDescendant(row, "LevyVisualizer");
        RectTransform visualizerRect = visualizer as RectTransform;
        if (visualizerRect != null)
            visualizerRect.anchoredPosition = new Vector2(index % 2 == 0 ? -80f : -50f,
                visualizerRect.anchoredPosition.y);
        Transform dataRoot = FindDescendant(row, "LevyData");
        Text data = dataRoot != null ? dataRoot.GetComponent<Text>() : row.GetComponentInChildren<Text>(true);
        if (data != null) data.text = available + "/" + maximum + " " + UnitName(unit);
        PopulateLevyModel(visualizer, unit);
    }

    private void PopulateLevyModel(Transform visualizer, UnitSaveData unit)
    {
        if (visualizer == null) return;
        UILevyUnitHover hover = visualizer.GetComponent<UILevyUnitHover>();
        if (hover == null) hover = visualizer.gameObject.AddComponent<UILevyUnitHover>();
        UIElement owner = GetComponent<UIElement>();
        if (owner == null) owner = visualizer.GetComponentInParent<UIElement>();
        bool needsArtwork = hover.Unit != unit || visualizer.childCount == 0;
        hover.Configure(owner, unit);
        if (!needsArtwork) return;
        for (int i = visualizer.childCount - 1; i >= 0; i--) Destroy(visualizer.GetChild(i).gameObject);
        if (unit == null || unit.bodyparts == null) return;
        Vector2[] offsets = { Vector2.zero, new Vector2(-2.2f, -6.5f), new Vector2(4.4f, -2.5f) };
        Material material = null;
        foreach (Material candidate in Resources.FindObjectsOfTypeAll<Material>())
            if (candidate != null && candidate.name.StartsWith("New Material 1", System.StringComparison.Ordinal))
            { material = candidate; break; }
        for (int i = 0; i < Mathf.Min(3, unit.bodyparts.Count); i++)
        {
            if (unit.bodyparts[i] == null) continue;
            GameObject layer = new GameObject("Bodypart " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            layer.layer = visualizer.gameObject.layer; layer.transform.SetParent(visualizer, false);
            Image image = layer.GetComponent<Image>(); image.sprite = unit.bodyparts[i]; image.material = material;
            image.type = Image.Type.Sliced; image.fillCenter = true; image.preserveAspect = true; image.raycastTarget = false;
            RectTransform rect = (RectTransform)layer.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(80f, 80f);
            rect.anchoredPosition = offsets[i] * (80f / 30f);
        }
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase)) return child;
        return null;
    }

    private static string UnitName(UnitSaveData unit) => unit != null && !string.IsNullOrWhiteSpace(unit.unitname)
        ? unit.unitname : unit != null ? unit.name : "Unknown levy";

    private void RefreshRegionalOutput(List<Province> provinces)
    {
        if (regionalOutputText == null) return;
        float rawGold = 0f;
        float rawFood = 0f;
        float rawFoodProduction = 0f;
        int foodConsumption = 0;
        int upkeep = 0;
        Dictionary<HoldingOutputType, float> outputs = new Dictionary<HoldingOutputType, float>();
        foreach (Province province in provinces)
        {
            rawGold += province.GetGoldIncomeUnrounded();
            upkeep += province.GetBuildingUpkeep();
            rawFood += province.GetFoodOutputUnrounded();
            rawFoodProduction += province.GetFoodProductionUnrounded();
            foodConsumption += province.GetFoodConsumption();
            foreach (HoldingOutputType type in System.Enum.GetValues(typeof(HoldingOutputType)))
            {
                if (type == HoldingOutputType.Income || type == HoldingOutputType.Food ||
                    type == HoldingOutputType.PoliticalInfluence) continue;
                float amount = province.GetHoldingOutputUnrounded(type);
                outputs[type] = outputs.TryGetValue(type, out float current) ? current + amount : amount;
            }
        }
        int netGold = Mathf.RoundToInt(rawGold) - upkeep;
        int netFood = Mathf.RoundToInt(rawFood);
        int foodProduction = Mathf.RoundToInt(rawFoodProduction);

        StringBuilder text = new StringBuilder("Regional Net Output");
        text.Append("\nGold: ").Append(netGold >= 0 ? "+" : string.Empty).Append(netGold);
        text.Append("\nFood production: +").Append(foodProduction);
        text.Append("\nFood consumption: -").Append(foodConsumption);
        text.Append("\nNet food: ").Append(netFood >= 0 ? "+" : string.Empty).Append(netFood);
        Province referenceProvince = provinces.Count > 0 ? provinces[0] : null;
        CampaignRegion referenceRegion = referenceProvince != null && Owners.Instance != null
            ? Owners.Instance.CallRegionByString(referenceProvince.region) : null;
        RegionalLoyaltyShare foodShare = referenceRegion != null
            ? referenceRegion.GetLoyaltyShare(referenceProvince.nation, true) : null;
        if (foodShare != null)
            text.Append("\nFood storage: ").Append(foodShare.foodStorage).Append(" / ").Append(foodShare.foodStorageCapacity);
        foreach (HoldingOutputType type in System.Enum.GetValues(typeof(HoldingOutputType)))
        {
            if (type == HoldingOutputType.Income || type == HoldingOutputType.Food ||
                type == HoldingOutputType.PoliticalInfluence) continue;
            int amount = outputs.TryGetValue(type, out float value) ? Mathf.RoundToInt(value) : 0;
            text.Append("\n").Append(OutputLabel(type)).Append(": ")
                .Append(amount >= 0 ? "+" : string.Empty).Append(amount);
        }
        regionalOutputText.text = text.ToString();
        ConfigureText(regionalOutputText, 8, 18);
    }

    private void RefreshClassComposition(List<Province> provinces)
    {
        if (classChart == null) return;
        Province fallback = provinces.Count > 0 ? provinces[0] : null;
        CampaignRegion region = fallback != null && Owners.Instance != null
            ? Owners.Instance.CallRegionByString(fallback.region) : null;
        classChart.LoadRegion(region, fallback);
    }

    private static List<Province> OccupiedRegionProvinces(Province selectedProvince)
    {
        List<Province> result = new List<Province>();
        if (selectedProvince == null) return result;
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(selectedProvince.region) : null;
        if (region != null && region.provincelist != null)
            foreach (Province province in region.provincelist)
                if (province != null && selectedProvince.nation != null && province.nation == selectedProvince.nation)
                    result.Add(province);
        if (result.Count == 0) result.Add(selectedProvince);
        return result;
    }

    private Transform FindNamedTransform(string objectName)
    {
        Transform parent = transform.parent;
        Transform searchRoot = parent != null && parent.name.Equals("AdministrationMenu", System.StringComparison.OrdinalIgnoreCase)
            ? parent : transform;
        foreach (Transform child in searchRoot.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child;
        return null;
    }

    private static void ConfigureText(Text text, int minimum, int maximum)
    {
        text.alignment = TextAnchor.UpperLeft;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minimum;
        text.resizeTextMaxSize = maximum;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static string OutputLabel(HoldingOutputType type)
    {
        switch (type)
        {
            case HoldingOutputType.CulturalInfluence: return "Cultural influence";
            case HoldingOutputType.ReligiousInfluence: return "Religious influence";
            default: return type.ToString();
        }
    }
}
