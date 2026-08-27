using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIRegionSummary : MonoBehaviour
{
    private Transform regionalTotalMenu;
    private Transform classMenu;
    private Text regionalOutputText;
    private UIRegionalClassChart classChart;

    public void RefreshFor(Province selectedProvince)
    {
        ResolveReferences();
        List<Province> provinces = OccupiedRegionProvinces(selectedProvince);
        RefreshRegionalOutput(provinces);
        RefreshClassComposition(provinces);
    }

    private void ResolveReferences()
    {
        if (regionalTotalMenu == null) regionalTotalMenu = FindNamedTransform("RegionalTotalMenu");
        if (classMenu == null) classMenu = FindNamedTransform("ClassMenu");
        if (regionalTotalMenu != null && regionalOutputText == null)
            regionalOutputText = regionalTotalMenu.GetComponentInChildren<Text>(true);
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

    private void RefreshRegionalOutput(List<Province> provinces)
    {
        if (regionalOutputText == null) return;
        int netGold = 0;
        int netFood = 0;
        int foodProduction = 0;
        int foodConsumption = 0;
        Dictionary<HoldingOutputType, int> outputs = new Dictionary<HoldingOutputType, int>();
        foreach (Province province in provinces)
        {
            netGold += province.GetGoldIncome() - province.GetTempleUpkeep();
            netFood += province.GetFoodOutput();
            foodProduction += province.GetFoodProduction();
            foodConsumption += province.GetFoodConsumption();
            foreach (HoldingOutputType type in System.Enum.GetValues(typeof(HoldingOutputType)))
            {
                if (type == HoldingOutputType.Income || type == HoldingOutputType.Food ||
                    type == HoldingOutputType.PoliticalInfluence) continue;
                int amount = province.GetHoldingOutput(type);
                outputs[type] = outputs.TryGetValue(type, out int current) ? current + amount : amount;
            }
        }

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
            int amount = outputs.TryGetValue(type, out int value) ? value : 0;
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
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
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
