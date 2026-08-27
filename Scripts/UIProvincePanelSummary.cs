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
    private Coroutine hideRoutine;

    public void RefreshFor(Province target)
    {
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
            string holdingType = !string.IsNullOrWhiteSpace(holding.HoldingId) ? holding.HoldingId : "Unassigned";
            if (!groups.TryGetValue(holdingType, out List<ProvinceHolding> group))
            { group = new List<ProvinceHolding>(); groups.Add(holdingType, group); }
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
                label.text = "0"; hover.Configure(this, "No holding type in this slot."); continue;
            }
            label.text = sorted[i].Value.Count.ToString();
            hover.Configure(this, BuildTypeTooltip(sorted[i].Value));
        }
    }

    private string BuildTypeTooltip(List<ProvinceHolding> holdings)
    {
        StringBuilder result = new StringBuilder();
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
            if (result.Length > 0) result.Append("\n\n");
            AppendHoldingGroup(result, group);
        }
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
                total += province.GetHoldingOutput(holding, type);
            if (total == 0) continue;
            outputs.Add(OutputLabel(type) + " " + (total > 0 ? "+" : string.Empty) + total);
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
        StringBuilder text = new StringBuilder("Income\nGold: ").Append(province.GetGoldIncome());
        foreach (HoldingOutputType type in System.Enum.GetValues(typeof(HoldingOutputType)))
            if (type != HoldingOutputType.Income && type != HoldingOutputType.PoliticalInfluence)
                text.Append(" | ").Append(OutputLabel(type)).Append(": ").Append(province.GetHoldingOutput(type));
        int total = 0, available = 0, mobilized = 0, recovering = 0;
        if (province.levyEntitlements != null) foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
        {
            if (entitlement == null || !entitlement.eligible) continue; total++;
            if (entitlement.state == LevyEntitlementState.Available) available++;
            else if (entitlement.state == LevyEntitlementState.Recovering) recovering++;
            else mobilized++;
        }
        text.Append("\nLevies: ").Append(available).Append("/").Append(total);
        if (province.nation != null)
            text.Append(" | National levy law: ").Append((province.nation.LevyLawPermille / 10f).ToString("0.#")).Append("%");
        if (mobilized > 0) text.Append(" | ").Append(mobilized).Append(" mobilized");
        if (recovering > 0) text.Append(" | ").Append(recovering).Append(" recovering");
        List<string> modifiers = new List<string>();
        string development = ProvinceLocalModifiers.FormatMaxDevelopment(province.MaxDevelopmentModifier);
        if (!string.IsNullOrEmpty(development)) modifiers.Add(development);
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
        float loyalty = region != null ? region.GetLoyalty(province.nation) : 100f;
        if (!Mathf.Approximately(loyalty, 100f)) modifiers.Add("Loyalty " + loyalty.ToString("0.#") + "%");
        if (!Mathf.Approximately(CampaignEconomy.GoldIncomeRate, 1f)) modifiers.Add("Income rate " + (CampaignEconomy.GoldIncomeRate * 100f).ToString("0.#") + "%");
        text.Append("\nModifiers: ").Append(modifiers.Count > 0 ? string.Join(" | ", modifiers) : "None");
        AppendHoldingComposition(text);
        totalIncomeText.text = text.ToString(); totalIncomeText.resizeTextForBestFit = true;
        totalIncomeText.resizeTextMinSize = 8; totalIncomeText.resizeTextMaxSize = 16;
    }

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
            case HoldingOutputType.Food: return "Net food";
            case HoldingOutputType.CulturalInfluence: return "Cultural influence";
            case HoldingOutputType.ReligiousInfluence: return "Religious influence";
            default: return type.ToString();
        }
    }

    public void ShowHoldingTooltip(string message)
    {
        EnsureTooltip(); KeepHoldingTooltipOpen(); tooltipText.text = message; tooltipRoot.SetActive(true);
        tooltipRoot.transform.SetAsLastSibling();
    }

    public void RequestHideHoldingTooltip()
    {
        KeepHoldingTooltipOpen(); hideRoutine = StartCoroutine(HideAfterGrace());
    }

    public void KeepHoldingTooltipOpen()
    {
        if (hideRoutine != null) StopCoroutine(hideRoutine); hideRoutine = null;
    }

    private IEnumerator HideAfterGrace()
    {
        yield return new WaitForSecondsRealtime(.25f); hideRoutine = null;
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
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
            return;
        }
        tooltipRoot = new GameObject("HoldingDetailsTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIHoldingTooltipHoverArea));
        tooltipRoot.layer = gameObject.layer; tooltipRoot.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)tooltipRoot.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, 225f); rect.sizeDelta = new Vector2(285f, 260f);
        tooltipRoot.GetComponent<Image>().color = new Color(.06f, .06f, .06f, .97f);
        tooltipRoot.GetComponent<UIHoldingTooltipHoverArea>().Owner = this;
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = gameObject.layer; textObject.transform.SetParent(tooltipRoot.transform, false);
        tooltipText = textObject.GetComponent<Text>(); Text reference = GetComponentInChildren<Text>(true);
        tooltipText.font = reference != null && reference.font != null ? reference.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tooltipText.fontSize = 12; tooltipText.color = Color.white; tooltipText.alignment = TextAnchor.UpperLeft;
        tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap; tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform textRect = tooltipText.rectTransform; textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 8f); textRect.offsetMax = new Vector2(-8f, -8f);
        tooltipRoot.SetActive(false);
    }
}

public sealed class UIHoldingClassSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private UIProvincePanelSummary owner; private string message;
    public void Configure(UIProvincePanelSummary target, string contents) { owner = target; message = contents; }
    public void OnPointerEnter(PointerEventData eventData) { if (owner != null) owner.ShowHoldingTooltip(message); }
    public void OnPointerExit(PointerEventData eventData) { if (owner != null) owner.RequestHideHoldingTooltip(); }
}

public sealed class UIHoldingTooltipHoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UIProvincePanelSummary Owner;
    public void OnPointerEnter(PointerEventData eventData) { if (Owner != null) Owner.KeepHoldingTooltipOpen(); }
    public void OnPointerExit(PointerEventData eventData) { if (Owner != null) Owner.RequestHideHoldingTooltip(); }
}
