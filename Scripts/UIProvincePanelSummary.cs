using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UIProvincePanelSummary : MonoBehaviour
{
    private Province province;
    private Transform summaryRoot;
    private Text holdingsText;
    private Text urbanizationText;
    private Text totalIncomeText;
    private string holdingsTemplate;
    private string urbanizationTemplate;
    private readonly List<Transform> slots = new List<Transform>();
    private GameObject tooltipRoot;
    private Text tooltipText;
    private RectTransform tooltipRows;
    private GameObject allegianceTooltipRoot;
    private Text allegianceTooltipText;
    private Coroutine hideRoutine;
    private bool productionTooltipPinned;

    public void RefreshFor(Province target)
    {
        if (province != target)
        {
            productionTooltipPinned = false;
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
        }
        province = target;
        ResolveReferences();
        if (province == null)
        {
            ApplyTemplates(0, 0);
            foreach (Transform slot in slots)
            {
                Text label = EnsureCountLabel(slot);
                label.text = "0";
            }
            if (totalIncomeText != null) totalIncomeText.text = "Income\nNo province";
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
            return;
        }
        int count = province.holdings != null ? province.holdings.FindAll(item => item != null).Count : 0;
        ApplyTemplates(count, Mathf.RoundToInt(province.urbanization));
        RefreshSlots();
        RefreshTotalProduction();
    }

    private void ResolveReferences()
    {
        // Runtime-created province panels are clones. Never retain a reference that belongs
        // to another panel, even if Unity copied the component's managed fields.
        if (summaryRoot != null && !summaryRoot.IsChildOf(transform)) summaryRoot = null;
        if (holdingsText != null && !holdingsText.transform.IsChildOf(transform))
        { holdingsText = null; holdingsTemplate = null; }
        if (urbanizationText != null && !urbanizationText.transform.IsChildOf(transform))
        { urbanizationText = null; urbanizationTemplate = null; }
        if (totalIncomeText != null && !totalIncomeText.transform.IsChildOf(transform)) totalIncomeText = null;
        if (summaryRoot == null) summaryRoot = Find("HoldingsSummary");
        if (totalIncomeText == null)
        {
            Transform income = Find("Provincial Total Income");
            if (income != null) totalIncomeText = income.GetComponent<Text>();
        }
        if (totalIncomeText != null)
        {
            totalIncomeText.raycastTarget = true;
            UIProvinceOutputHover hover = totalIncomeText.GetComponent<UIProvinceOutputHover>();
            if (hover == null) hover = totalIncomeText.gameObject.AddComponent<UIProvinceOutputHover>();
            hover.Configure(this);
            Tooltip oldTooltip = totalIncomeText.GetComponent<Tooltip>();
            if (oldTooltip != null) oldTooltip.enabled = false;
        }
        slots.Clear();
        if (summaryRoot != null)
        {
            for (int i = 0; i < summaryRoot.childCount; i++)
            {
                Transform child = summaryRoot.GetChild(i);
                if (child.name.StartsWith("HoldingSlot", System.StringComparison.OrdinalIgnoreCase)) slots.Add(child);
            }
            slots.Sort((left, right) => SlotNumber(left.name).CompareTo(SlotNumber(right.name)));
        }
        foreach (Text text in GetComponentsInChildren<Text>(true))
        {
            if (text == null || IsSlotLabel(text.transform)) continue;
            if (holdingsText == null && (ContainsToken(text.text, 'x') || text.name.IndexOf("Holding", System.StringComparison.OrdinalIgnoreCase) >= 0))
            { holdingsText = text; holdingsTemplate = text.text; }
            if (urbanizationText == null && (ContainsToken(text.text, 'y') || text.name.IndexOf("Urban", System.StringComparison.OrdinalIgnoreCase) >= 0))
            { urbanizationText = text; urbanizationTemplate = text.text; }
        }
    }

    private bool IsSlotLabel(Transform candidate)
    {
        foreach (Transform slot in slots) if (candidate.IsChildOf(slot)) return true;
        return false;
    }

    private Transform Find(string objectName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true)) if (child.name == objectName) return child;
        return null;
    }

    private static int SlotNumber(string value)
    {
        int start = value != null ? value.Length : 0;
        while (start > 0 && char.IsDigit(value[start - 1])) start--;
        return value != null && int.TryParse(value.Substring(start), out int result) ? result : int.MaxValue;
    }

    private void ApplyTemplates(int count, int urbanization)
    {
        if (holdingsText != null)
            holdingsText.text = ApplyTemplate(holdingsTemplate, holdingsText, count, urbanization,
                holdingsText == urbanizationText || holdingsText.name.IndexOf("Urban", System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (urbanizationText != null && urbanizationText != holdingsText)
            urbanizationText.text = ApplyTemplate(urbanizationTemplate, urbanizationText, count, urbanization, false);
    }

    private static bool ContainsToken(string value, char token)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.IndexOf("<" + char.ToLowerInvariant(token) + ">", System.StringComparison.Ordinal) >= 0 ||
            value.IndexOf("<" + char.ToUpperInvariant(token) + ">", System.StringComparison.Ordinal) >= 0;
    }

    private static string ApplyTemplate(string template, Text target, int count, int urbanization, bool combined)
    {
        string value = !string.IsNullOrEmpty(template) ? template : target.text;
        bool hasX = ContainsToken(value, 'x');
        bool hasY = ContainsToken(value, 'y');
        if (!hasX && !hasY)
        {
            // A clone may be made after its source panel has already replaced its tokens.
            // Reconstruct the intended label from its semantic object name.
            if (combined) return "Holdings: " + count + " || Urbanization: " + urbanization;
            return target.name.IndexOf("Urban", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? "Urbanization: " + urbanization : "Holdings: " + count;
        }
        return value.Replace("<X>", count.ToString()).Replace("<x>", count.ToString())
            .Replace("<Y>", urbanization.ToString()).Replace("<y>", urbanization.ToString());
    }

    private void RefreshSlots()
    {
        Dictionary<string, List<ProvinceHolding>> groups = new Dictionary<string, List<ProvinceHolding>>(System.StringComparer.OrdinalIgnoreCase);
        if (province.holdings != null) foreach (ProvinceHolding holding in province.holdings)
        {
            if (holding == null) continue;
            string category = HoldingCategoryRules.GroupName(holding);
            if (!groups.TryGetValue(category, out List<ProvinceHolding> group))
            { group = new List<ProvinceHolding>(); groups.Add(category, group); }
            group.Add(holding);
        }
        List<KeyValuePair<string, List<ProvinceHolding>>> sorted =
            new List<KeyValuePair<string, List<ProvinceHolding>>>(groups);
        sorted.Sort((left, right) =>
        {
            int byCount = right.Value.Count.CompareTo(left.Value.Count);
            return byCount != 0 ? byCount : string.Compare(left.Key, right.Key, System.StringComparison.OrdinalIgnoreCase);
        });
        for (int i = 0; i < slots.Count; i++)
        {
            Transform slot = slots[i];
            Text label = EnsureCountLabel(slot);
            UIHoldingClassSlotHover hover = slot.GetComponent<UIHoldingClassSlotHover>();
            if (hover == null) hover = slot.gameObject.AddComponent<UIHoldingClassSlotHover>();
            Tooltip oldTooltip = slot.GetComponent<Tooltip>(); if (oldTooltip != null) oldTooltip.enabled = false;
            if (i >= sorted.Count)
            {
                label.text = "0"; UIHoldingSlotIcon.Set(slot, null);
                HoldingTooltipData empty = new HoldingTooltipData { title = "No holding category in this slot." };
                hover.Configure(this, empty); continue;
            }
            label.text = sorted[i].Value.Count.ToString();
            UIHoldingSlotIcon.Set(slot, HoldingCategoryRules.RepresentativeIcon(sorted[i].Value));
            hover.Configure(this, BuildTypeTooltip(sorted[i].Key, sorted[i].Value));
        }
    }

    private HoldingTooltipData BuildTypeTooltip(string category, List<ProvinceHolding> holdings)
    {
        HoldingTooltipData result = new HoldingTooltipData { title = category + " (" + holdings.Count + ")" };
        Dictionary<string, List<ProvinceHolding>> identical = new Dictionary<string, List<ProvinceHolding>>();
        foreach (ProvinceHolding holding in holdings)
        {
            string culture = !string.IsNullOrWhiteSpace(holding.cultureName) ? holding.cultureName : "Unassigned";
            string key = holding.HoldingId + "|" + culture;
            if (!identical.TryGetValue(key, out List<ProvinceHolding> group)) { group = new List<ProvinceHolding>(); identical.Add(key, group); }
            group.Add(holding);
        }
        List<List<ProvinceHolding>> groups = new List<List<ProvinceHolding>>(identical.Values);
        groups.Sort((left, right) => string.CompareOrdinal(left[0].DisplayName, right[0].DisplayName));
        foreach (List<ProvinceHolding> group in groups)
        {
            StringBuilder text = new StringBuilder();
            AppendHoldingGroup(text, group);
            result.entries.Add(new HoldingTooltipEntry { text = text.ToString(), allegianceDetails = BuildAllegianceDetails(group) });
        }
        return result;
    }

    private static string BuildAllegianceDetails(List<ProvinceHolding> holdings)
    {
        Dictionary<string, int> totals = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        foreach (ProvinceHolding holding in holdings)
        {
            string allegiance = !string.IsNullOrWhiteSpace(holding.allegiance) ? holding.allegiance.Trim() : "Unaligned";
            totals[allegiance] = totals.TryGetValue(allegiance, out int count) ? count + 1 : 1;
        }
        List<KeyValuePair<string, int>> sorted = new List<KeyValuePair<string, int>>(totals);
        sorted.Sort((left, right) => right.Value != left.Value ? right.Value.CompareTo(left.Value) :
            string.Compare(left.Key, right.Key, System.StringComparison.OrdinalIgnoreCase));
        StringBuilder result = new StringBuilder("Allegiance breakdown");
        foreach (KeyValuePair<string, int> entry in sorted)
            result.Append("\n").Append(entry.Value).Append("x ").Append(entry.Key);
        return result.ToString();
    }

    private void AppendHoldingGroup(StringBuilder result, List<ProvinceHolding> group)
    {
        ProvinceHolding example = group[0];
        string culture = !string.IsNullOrWhiteSpace(example.cultureName) ? example.cultureName : "Unassigned";
        result.Append(group.Count).Append("x ").Append(example.DisplayName).Append(" - ").Append(culture);
        List<string> outputs = new List<string>();
        bool produced = false;
        foreach (HoldingOutputType type in System.Enum.GetValues(typeof(HoldingOutputType)))
        {
            if (type == HoldingOutputType.PoliticalInfluence) continue;
            int total = 0;
            foreach (ProvinceHolding holding in group)
                total += province.GetHoldingOutput(holding, type) +
                    (type == HoldingOutputType.Food ? holding.FoodConsumption : 0);
            if (total == 0) continue;
            outputs.Add(OutputLabel(type) + " " + (total > 0 ? "+" : string.Empty) + total);
            produced = true;
        }
        int garrisonCapacity = 0;
        foreach (ProvinceHolding holding in group)
            if (holding != null && holding.definition != null)
                garrisonCapacity += Mathf.Max(0, holding.definition.garrisonCapacity);
        if (garrisonCapacity > 0)
        {
            outputs.Add("Garrison +" + garrisonCapacity);
            produced = true;
        }
        int foodUpkeep = 0;
        foreach (ProvinceHolding holding in group)
            if (holding != null) foodUpkeep += holding.FoodUpkeep;
        if (foodUpkeep > 0)
        {
            outputs.Add("Upkeep: " + foodUpkeep + " food");
            produced = true;
        }
        if (example.definition != null && example.definition.category == HoldingCategory.EliteAgriculture)
        {
            int servileEfficiency = Mathf.Clamp(example.definition.categoryTier, 1, 3) * 5 * group.Count;
            outputs.Add("Servile output +" + servileEfficiency + "%");
            produced = true;
        }
        result.Append("\n").Append(produced ? string.Join(" | ", outputs) : "No output");
        float levyContribution = 0f;
        HashSet<string> levyEntitlementIds = new HashSet<string>(System.StringComparer.Ordinal);
        int availableLevies = 0;
        Dictionary<string, float> resolvedLevies = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
        foreach (ProvinceHolding holding in group)
        {
            float contribution = holding.EffectiveLevyContribution(province.nation);
            if (contribution <= 0f) continue;
            levyContribution += contribution;
            UnitSaveData levy = HoldingEvolutionSystem.ResolveLevyUnit(province, holding);
            string levyName = levy != null && !string.IsNullOrWhiteSpace(levy.unitname)
                ? levy.unitname : levy != null ? levy.name : "No matching levy";
            if (!resolvedLevies.ContainsKey(levyName)) resolvedLevies.Add(levyName, 0f);
            resolvedLevies[levyName] += contribution;
            foreach (ProvinceLevyEntitlement entitlement in province.GetRegionalLevyEntitlementsForHolding(holding.instanceId))
            {
                if (entitlement == null || !levyEntitlementIds.Add(entitlement.id)) continue;
                if (entitlement.state == LevyEntitlementState.Available) availableLevies++;
            }
        }
        if (levyContribution > 0f)
        {
            result.Append("\nLevies: ").Append(availableLevies).Append("/").Append(levyEntitlementIds.Count).Append(" -> ");
            if (resolvedLevies.Count == 1)
            {
                foreach (KeyValuePair<string, float> levy in resolvedLevies) result.Append(levy.Key);
            }
            else
            {
                List<string> levyTypes = new List<string>();
                foreach (KeyValuePair<string, float> levy in resolvedLevies)
                    levyTypes.Add(levy.Value.ToString("0.###") + " " + levy.Key);
                result.Append(levyTypes.Count > 0 ? string.Join(", ", levyTypes) : "No matching levy");
            }
        }
    }

    private void RefreshTotalProduction()
    {
        if (totalIncomeText == null) return;
        totalIncomeText.text = UIProvinceEconomySummary.Build(province); totalIncomeText.resizeTextForBestFit = true;
        totalIncomeText.resizeTextMinSize = 8; totalIncomeText.resizeTextMaxSize = 16;
    }

    public void ShowProductionBreakdownTooltip()
    {
        HoldingTooltipData data = new HoldingTooltipData { title = "Provincial holding efficiencies" };
        if (province == null || province.holdings == null || province.holdings.Count == 0)
        {
            data.entries.Add(new HoldingTooltipEntry { text = "No holdings." });
            ShowHoldingTooltip(data, true);
            return;
        }

        Dictionary<HoldingDefinition, List<ProvinceHolding>> groups =
            new Dictionary<HoldingDefinition, List<ProvinceHolding>>();
        foreach (ProvinceHolding holding in province.holdings)
        {
            if (holding == null || holding.definition == null) continue;
            if (!groups.TryGetValue(holding.definition, out List<ProvinceHolding> group))
            {
                group = new List<ProvinceHolding>();
                groups.Add(holding.definition, group);
            }
            group.Add(holding);
        }

        List<List<ProvinceHolding>> sorted = new List<List<ProvinceHolding>>(groups.Values);
        sorted.Sort((left, right) => string.Compare(left[0].DisplayName, right[0].DisplayName,
            System.StringComparison.OrdinalIgnoreCase));
        foreach (List<ProvinceHolding> group in sorted)
            data.entries.Add(new HoldingTooltipEntry
            {
                text = BuildProductionEfficiencySummary(group),
                allegianceDetails = BuildProductionBreakdown(group)
            });
        ShowHoldingTooltip(data, true);
    }

    private string BuildProductionEfficiencySummary(List<ProvinceHolding> group)
    {
        ProvinceHolding example = group[0];
        HoldingDefinition definition = example.definition;
        float provincialEfficiency = HoldingEvolutionSystem.OutputEfficiencyPercent(province, definition);
        StringBuilder result = new StringBuilder();
        result.Append(group.Count).Append("x ").Append(example.DisplayName);

        List<string> efficiencies = new List<string>();
        if (definition != null && definition.outputs != null)
            foreach (HoldingOutputDefinition output in definition.outputs)
            {
                if (output == null || output.type == HoldingOutputType.PoliticalInfluence || output.baseValue == 0) continue;
                float urbanization = UrbanizationPercent(output.EffectiveUrbanizationResponse, province.urbanization);
                float combined = ((1f + urbanization / 100f) * (1f + provincialEfficiency / 100f) - 1f) * 100f;
                efficiencies.Add(OutputLabel(output.type) + " " + SignedPercent(combined));
            }
        if (efficiencies.Count == 0)
            efficiencies.Add("Net efficiency " + SignedPercent(provincialEfficiency));
        result.Append("\nNet efficiency: ").Append(string.Join(" | ", efficiencies));
        result.Append("\nHover for breakdown");
        return result.ToString();
    }

    public void ToggleProductionBreakdownTooltip()
    {
        if (productionTooltipPinned)
        {
            productionTooltipPinned = false;
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
            HideAllegianceTooltip();
            return;
        }
        productionTooltipPinned = true;
        ShowProductionBreakdownTooltip();
    }

    public void ReleasePinnedProductionTooltip()
    {
        productionTooltipPinned = false;
    }

    private string BuildProductionBreakdown(List<ProvinceHolding> group)
    {
        ProvinceHolding example = group[0];
        HoldingDefinition definition = example.definition;
        float efficiency = HoldingEvolutionSystem.OutputEfficiencyPercent(province, definition);
        string tags = HoldingEvolutionSystem.TagList(definition);
        StringBuilder result = new StringBuilder();
        if (group.Count > 1) result.Append(group.Count).Append("x ");
        result.Append(example.DisplayName);

        bool any = false;
        if (definition.outputs != null) foreach (HoldingOutputDefinition output in definition.outputs)
        {
            if (output == null || output.type == HoldingOutputType.PoliticalInfluence || output.baseValue == 0) continue;
            any = true;
            int response = output.EffectiveUrbanizationResponse;
            float urbanized = UrbanizationOutputScaling.ApplyUnrounded(output.baseValue, response, province.urbanization);
            float final = urbanized * (1f + efficiency / 100f);
            result.Append("\n").Append(OutputLabel(output.type)).Append(": ")
                .Append(output.baseValue).Append(" base")
                .Append(" x urbanization ").Append(SignedPercent(UrbanizationPercent(response, province.urbanization)))
                .Append(" x provincial ").Append(tags).Append(" ").Append(SignedPercent(efficiency))
                .Append(" = ").Append(final.ToString("0.###")).Append(" unrounded output added to region");
        }

        if (!any && example.GoldIncome != 0)
        {
            int urbanized = example.GoldIncomeAt(Mathf.RoundToInt(province.urbanization));
            int final = Mathf.RoundToInt(urbanized * (1f + efficiency / 100f));
            result.Append("\nGold: ").Append(example.GoldIncome).Append(" base")
                .Append(" x provincial ").Append(tags).Append(" ").Append(SignedPercent(efficiency))
                .Append(" = ").Append(final).Append(" (after rounding)");
        }

        List<string> buildingEffects = MatchingBuildingEfficiencyEffects(definition);
        if (buildingEffects.Count > 0)
            result.Append("\nBuildings: ").Append(string.Join(", ", buildingEffects));
        string holdingEffects = HoldingEfficiencyEffects(definition);
        if (!string.IsNullOrEmpty(holdingEffects))
            result.Append("\nHoldings: ").Append(holdingEffects);
        if (example.FoodConsumption > 0)
            result.Append("\nFood consumed: ").Append(example.FoodConsumption).Append(" each");
        if (group.Count > 1) result.Append("\nValues above are per holding.");
        return result.ToString();
    }

    private List<string> MatchingBuildingEfficiencyEffects(HoldingDefinition definition)
    {
        List<string> result = new List<string>();
        if (province == null || province.buildings == null || definition == null) return result;
        HoldingTag tags = HoldingEvolutionSystem.EffectiveTags(definition);
        foreach (ProvinceBuilding building in province.buildings)
        {
            if (building == null || building.definition == null || building.definition.levels == null) continue;
            float amount = 0f;
            foreach (BuildingLevelDefinition level in building.definition.levels)
            {
                if (level == null || level.level > building.level || level.holdingEconomyModifiers == null) continue;
                foreach (HoldingTagModifier modifier in level.holdingEconomyModifiers)
                    if (modifier != null && (modifier.tag & tags) != 0 &&
                        (string.IsNullOrWhiteSpace(modifier.requiredNationFlag) ||
                         NationContentResolver.HasFlag(province.nation, modifier.requiredNationFlag)))
                        amount += modifier.outputEfficiencyPercent;
            }
            if (!Mathf.Approximately(amount, 0f))
                result.Add(building.DisplayName + " " + SignedPercent(amount));
        }
        return result;
    }

    private string HoldingEfficiencyEffects(HoldingDefinition receivingDefinition)
    {
        if (province == null || province.holdings == null || receivingDefinition == null ||
            (HoldingEvolutionSystem.EffectiveTags(receivingDefinition) & HoldingTag.Servile) == 0)
            return string.Empty;
        Dictionary<string, int> sources = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        float total = 0f;
        foreach (ProvinceHolding holding in province.holdings)
        {
            if (holding == null || holding.definition == null ||
                holding.definition.category != HoldingCategory.EliteAgriculture) continue;
            float amount = Mathf.Clamp(holding.definition.categoryTier, 1, 3) * 5f;
            string label = holding.DisplayName + " " + SignedPercent(amount);
            sources[label] = sources.TryGetValue(label, out int count) ? count + 1 : 1;
            total += amount;
        }
        if (sources.Count == 0) return string.Empty;
        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> source in sources)
            parts.Add((source.Value > 1 ? source.Value + "x " : string.Empty) + source.Key);
        return string.Join(", ", parts) + " = " + SignedPercent(total) + " Servile output";
    }

    private static float UrbanizationPercent(int response, float urbanization) =>
        Mathf.Clamp(response, -100, 100) * Mathf.Clamp(urbanization, -100f, 100f) / 100f;

    private static string SignedPercent(float value) =>
        (value >= 0f ? "+" : string.Empty) + value.ToString("0.#") + "%";

    private void AppendHoldingComposition(StringBuilder text)
    {
        if (province == null || province.holdings == null || province.holdings.Count == 0) return;
        Dictionary<HoldingTag, float> desired = HoldingEvolutionSystem.DesiredWeights(province);
        List<string> parts = new List<string>();
        foreach (HoldingTag tag in HoldingEvolutionSystem.Tags)
        {
            int present = province.holdings.FindAll(holding => holding != null && holding.definition != null &&
                (HoldingEvolutionSystem.EffectiveTags(holding.definition) & tag) != 0).Count;
            float current = present * 100f / Mathf.Max(1, province.holdings.Count);
            float target = Mathf.Clamp(desired[tag], 0f, 100f);
            if (present > 0 || target >= 5f) parts.Add(tag + " " + current.ToString("0") + "%/" + target.ToString("0") + "%");
        }
        text.Append("\nHolding composition (current/desired): ").Append(parts.Count > 0 ? string.Join(" | ", parts) : "None");
    }

    private Text EnsureCountLabel(Transform slot)
    {
        Text text = slot.GetComponentInChildren<Text>(true);
        if (text != null) return text;
        GameObject child = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.layer = slot.gameObject.layer; child.transform.SetParent(slot, false); text = child.GetComponent<Text>();
        Text reference = GetComponentInChildren<Text>(true);
        text.font = reference != null && reference.font != null ? reference.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        RectTransform rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero; return text;
    }

    private static string OutputLabel(HoldingOutputType type)
    {
        switch (type)
        {
            case HoldingOutputType.Income: return "Gold";
            case HoldingOutputType.Food: return "Food produced";
            case HoldingOutputType.CulturalInfluence: return "Cultural influence";
            case HoldingOutputType.ReligiousInfluence: return "Religious influence";
            default: return type.ToString();
        }
    }

    public void ShowHoldingTooltip(HoldingTooltipData data, bool productionPanel = false)
    {
        EnsureTooltip(); KeepHoldingTooltipOpen(); HideAllegianceTooltip();
        tooltipText.text = data != null ? data.title : string.Empty;
        for (int i = tooltipRows.childCount - 1; i >= 0; i--) Destroy(tooltipRows.GetChild(i).gameObject);
        float y = 0f;
        if (data != null) foreach (HoldingTooltipEntry entry in data.entries)
        {
            int lines = 1; foreach (char character in entry.text) if (character == '\n') lines++;
            float height = Mathf.Max(30f, lines * 16f + 8f);
            GameObject row = new GameObject("HoldingRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIHoldingDetailRowHover));
            row.layer = gameObject.layer; row.transform.SetParent(tooltipRows, false);
            RectTransform rowRect = (RectTransform)row.transform; rowRect.anchorMin = new Vector2(0f, 1f); rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(.5f, 1f); rowRect.anchoredPosition = new Vector2(0f, -y); rowRect.sizeDelta = new Vector2(0f, height);
            row.GetComponent<Image>().color = new Color(1f, 1f, 1f, .035f);
            UIHoldingDetailRowHover hover = row.GetComponent<UIHoldingDetailRowHover>(); hover.Configure(this, entry.allegianceDetails);
            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.layer = gameObject.layer; labelObject.transform.SetParent(row.transform, false);
            Text label = labelObject.GetComponent<Text>(); label.font = tooltipText.font; label.fontSize = 12; label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false; label.text = entry.text;
            RectTransform labelRect = label.rectTransform; labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4f, 3f); labelRect.offsetMax = new Vector2(-4f, -3f);
            y += height + 4f;
        }
        RectTransform rootRect = (RectTransform)tooltipRoot.transform;
        // Keep the panel attached just above Provincial Total Income (its top is
        // approximately y=-10 in the BuildingMenu). A bottom pivot makes additional
        // holding rows expand upward instead of opening a growing gap below it.
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(.5f, .5f);
        rootRect.pivot = new Vector2(.5f, 0f);
        rootRect.anchoredPosition = productionPanel ? new Vector2(0f, -5f) : new Vector2(200f, 60f);
        rootRect.sizeDelta = new Vector2(600f, Mathf.Clamp(y + 54f, 180f, 700f));
        tooltipRoot.transform.SetAsLastSibling();
        tooltipRoot.SetActive(true);
    }

    public void ShowAllegianceTooltip(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) { HideAllegianceTooltip(); return; }
        EnsureTooltip(); KeepHoldingTooltipOpen(); allegianceTooltipText.text = message;
        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)allegianceTooltipRoot.transform;
        rect.sizeDelta = new Vector2(460f, Mathf.Clamp(allegianceTooltipText.preferredHeight + 24f, 170f, 620f));
        allegianceTooltipRoot.SetActive(true); allegianceTooltipRoot.transform.SetAsLastSibling();
    }

    public void HideAllegianceTooltip() { if (allegianceTooltipRoot != null) allegianceTooltipRoot.SetActive(false); }

    public void RequestHideHoldingTooltip()
    {
        if (productionTooltipPinned) return;
        KeepHoldingTooltipOpen(); hideRoutine = StartCoroutine(HideAfterGrace());
    }

    public void KeepHoldingTooltipOpen()
    {
        if (hideRoutine != null) StopCoroutine(hideRoutine); hideRoutine = null;
    }

    private IEnumerator HideAfterGrace()
    {
        yield return new WaitForSecondsRealtime(.25f); hideRoutine = null;
        if (tooltipRoot != null) tooltipRoot.SetActive(false); HideAllegianceTooltip();
    }

    private void EnsureTooltip()
    {
        if (tooltipRoot != null) return;
        Transform existing = transform.Find("HoldingDetailsTooltip");
        if (existing != null)
        {
            tooltipRoot = existing.gameObject;
            tooltipText = existing.GetComponentInChildren<Text>(true);
            UIHoldingTooltipHoverArea hover = existing.GetComponent<UIHoldingTooltipHoverArea>();
            if (hover == null) hover = existing.gameObject.AddComponent<UIHoldingTooltipHoverArea>();
            hover.Owner = this;
            foreach (Transform child in existing) if (child.name == "Rows") tooltipRows = child as RectTransform;
            if (tooltipRows == null) tooltipRows = CreateRowsRoot(existing);
            EnsureAllegianceTooltip(); return;
        }
        tooltipRoot = new GameObject("HoldingDetailsTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIHoldingTooltipHoverArea));
        tooltipRoot.layer = gameObject.layer; tooltipRoot.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)tooltipRoot.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, 0f); rect.anchoredPosition = new Vector2(0f, -5f);
        rect.sizeDelta = new Vector2(600f, 460f);
        tooltipRoot.GetComponent<Image>().color = new Color(.06f, .06f, .06f, .97f);
        tooltipRoot.GetComponent<UIHoldingTooltipHoverArea>().Owner = this;
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = gameObject.layer; textObject.transform.SetParent(tooltipRoot.transform, false);
        tooltipText = textObject.GetComponent<Text>(); Text reference = GetComponentInChildren<Text>(true);
        tooltipText.font = reference != null && reference.font != null ? reference.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tooltipText.fontSize = 12; tooltipText.color = Color.white; tooltipText.alignment = TextAnchor.UpperLeft;
        tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap; tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform textRect = tooltipText.rectTransform; textRect.anchorMin = new Vector2(0f, 1f); textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(.5f, 1f); textRect.anchoredPosition = new Vector2(0f, -7f); textRect.sizeDelta = new Vector2(-16f, 24f);
        tooltipRows = CreateRowsRoot(tooltipRoot.transform);
        EnsureAllegianceTooltip();
        tooltipRoot.SetActive(false);
    }

    private RectTransform CreateRowsRoot(Transform parent)
    {
        GameObject rows = new GameObject("Rows", typeof(RectTransform)); rows.layer = gameObject.layer; rows.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)rows.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 8f); rect.offsetMax = new Vector2(-8f, -34f); return rect;
    }

    private void EnsureAllegianceTooltip()
    {
        if (allegianceTooltipRoot != null) return;
        allegianceTooltipRoot = new GameObject("HoldingAllegianceTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        allegianceTooltipRoot.layer = gameObject.layer; allegianceTooltipRoot.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)allegianceTooltipRoot.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(540f, 225f); rect.sizeDelta = new Vector2(460f, 260f);
        allegianceTooltipRoot.GetComponent<Image>().color = new Color(.04f, .04f, .04f, .98f);
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = gameObject.layer; textObject.transform.SetParent(allegianceTooltipRoot.transform, false);
        allegianceTooltipText = textObject.GetComponent<Text>(); allegianceTooltipText.font = tooltipText.font;
        allegianceTooltipText.fontSize = 12; allegianceTooltipText.color = Color.white; allegianceTooltipText.alignment = TextAnchor.UpperLeft;
        allegianceTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap; allegianceTooltipText.verticalOverflow = VerticalWrapMode.Overflow;
        allegianceTooltipText.raycastTarget = false; RectTransform textRect = allegianceTooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(8f, 8f); textRect.offsetMax = new Vector2(-8f, -8f);
        allegianceTooltipRoot.SetActive(false);
    }
}

public sealed class HoldingTooltipData
{
    public string title;
    public readonly List<HoldingTooltipEntry> entries = new List<HoldingTooltipEntry>();
}

public sealed class HoldingTooltipEntry { public string text; public string allegianceDetails; }

public sealed class UIHoldingClassSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private UIProvincePanelSummary owner; private HoldingTooltipData message;
    public void Configure(UIProvincePanelSummary target, HoldingTooltipData contents) { owner = target; message = contents; }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null) return;
        owner.ReleasePinnedProductionTooltip();
        owner.ShowHoldingTooltip(message);
    }
    public void OnPointerExit(PointerEventData eventData) { if (owner != null) owner.RequestHideHoldingTooltip(); }
}

public sealed class UIProvinceOutputHover : MonoBehaviour, IPointerClickHandler
{
    private UIProvincePanelSummary owner;

    public void Configure(UIProvincePanelSummary target) { owner = target; }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null) owner.ToggleProductionBreakdownTooltip();
    }
}

public sealed class UIHoldingDetailRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private UIProvincePanelSummary owner; private string message;
    public void Configure(UIProvincePanelSummary target, string contents) { owner = target; message = contents; }
    public void OnPointerEnter(PointerEventData eventData) { if (owner != null) owner.ShowAllegianceTooltip(message); }
    public void OnPointerExit(PointerEventData eventData) { if (owner != null) owner.HideAllegianceTooltip(); }
}

public sealed class UIHoldingTooltipHoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UIProvincePanelSummary Owner;
    public void OnPointerEnter(PointerEventData eventData) { if (Owner != null) Owner.KeepHoldingTooltipOpen(); }
    public void OnPointerExit(PointerEventData eventData) { if (Owner != null) Owner.RequestHideHoldingTooltip(); }
}
