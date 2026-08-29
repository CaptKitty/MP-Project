using UnityEngine;
using UnityEngine.UI;

public class UIProvinceHost : MonoBehaviour
{
    public static UIProvinceHost Instance { get; private set; }

    public Province LoadedProvince { get; private set; }
    private Text provinceName;
    private Text provinceOwnerName;
    private UIBuildingMenu buildingMenu;
    private UIRegionalCultureChart cultureChart;
    private UIRegionalLoyaltyDisplay loyaltyDisplay;
    private UIRegionSummary regionSummary;
    private Text holdingsSummary;
    private Transform holdingsSummaryRoot;
    private Text holdingsCountText;
    private Text urbanizationText;
    private Text provincialTotalIncome;
    private string holdingsCountTemplate;
    private string urbanizationTemplate;
    private readonly System.Collections.Generic.List<Transform> holdingSlots = new System.Collections.Generic.List<Transform>();
    private string lastOwnerName;
    private string lastAdministrationSummary;
    private int lastBuildingSignature;
    private int lastCultureSignature;
    private int lastRegionalLoyalty = int.MinValue;
    private int lastRegionalFoodState = int.MinValue;
    private int lastRegionalLevyState = int.MinValue;
    private int lastNationGold = int.MinValue;
    private int lastUrbanization = int.MinValue;
    [SerializeField] private Button raiseArmyButton;
    private Button recruitUnitsButton;
    private Text recruitUnitsLabel;
    private FieldArmyHolder lastSelectedArmy;
    private float nextRecruitButtonRefresh;

    private void Awake()
    {
        if (Instance == null || gameObject.activeInHierarchy) Instance = this;
        ResolveReferences();
    }

    private void Start()
    {
        Province selected = Mapshower.Instance != null ? Mapshower.Instance.SelectedProvince : null;
        if (selected != null) LoadProvince(selected);
        else RefreshHeader();
    }

    private void Update()
    {
        Province selected = Mapshower.Instance != null ? Mapshower.Instance.SelectedProvince : null;
        if (selected != LoadedProvince) LoadProvince(selected);
        else if (LoadedProvince != null)
        {
            string owner = LoadedProvince.nation != null ? LoadedProvince.nation.name : "Unowned";
            int gold = LoadedProvince.nation != null ? LoadedProvince.nation.Gold : 0;
            string administration = AdministrationSummary(LoadedProvince);
            if (owner != lastOwnerName || gold != lastNationGold || administration != lastAdministrationSummary) RefreshHeader();
            if (LoadedProvince.urbanization != lastUrbanization)
            {
                lastUrbanization = LoadedProvince.urbanization;
                RefreshHoldingsSummary();
            }
            int signature = RegionBuildingSignature(LoadedProvince);
            if (signature != lastBuildingSignature)
            {
                lastBuildingSignature = signature;
                if (buildingMenu != null) buildingMenu.LoadProvince(LoadedProvince, this);
                RefreshHoldingsSummary();
                RepositionRecruitUnitsButton();
            }
            int cultureSignature = RegionCultureSignature(LoadedProvince);
            if (cultureSignature != lastCultureSignature)
            {
                lastCultureSignature = cultureSignature;
                RefreshCultureChart();
            }
            CampaignRegion loadedRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(LoadedProvince.region) : null;
            int regionalLoyalty = loadedRegion != null ? Mathf.RoundToInt(loadedRegion.GetLoyalty(LoadedProvince.nation) * 10f) : 0;
            if (regionalLoyalty != lastRegionalLoyalty) RefreshLoyaltyDisplay();
            RegionalLoyaltyShare foodShare = loadedRegion != null ? loadedRegion.GetLoyaltyShare(LoadedProvince.nation, true) : null;
            int regionalFoodState = foodShare != null ? foodShare.foodStorage * 10000 +
                foodShare.foodStorageCapacity * 100 + foodShare.lastFoodShortage : 0;
            if (regionalFoodState != lastRegionalFoodState)
            {
                lastRegionalFoodState = regionalFoodState;
                // Food storage changes every economy tick but does not alter holding
                // groups or their tooltips. Rebuilding the complete holdings UI here
                // caused a visible WebGL layout/GC spike.
                if (regionSummary != null) regionSummary.RefreshFor(LoadedProvince);
                UIProvincePanelSummary panelSummary = buildingMenu != null
                    ? buildingMenu.GetComponent<UIProvincePanelSummary>() : null;
                if (panelSummary != null) panelSummary.RefreshFor(LoadedProvince);
                else RefreshProvincialTotalIncome();
            }
            int regionalLevyState = RegionalLevyState(LoadedProvince);
            if (regionalLevyState != lastRegionalLevyState)
            {
                lastRegionalLevyState = regionalLevyState;
                if (regionSummary != null) regionSummary.RefreshFor(LoadedProvince);
            }
        }

        FieldArmyHolder selectedArmy = FieldArmyHolder.SelectedPlayerArmy;
        if (selectedArmy != lastSelectedArmy || Time.unscaledTime >= nextRecruitButtonRefresh)
        {
            lastSelectedArmy = selectedArmy;
            nextRecruitButtonRefresh = Time.unscaledTime + .5f;
            RefreshRecruitUnitsButton();
        }
    }

    public void LoadProvince(Province province)
    {
        ResolveReferences();
        LoadedProvince = province;
        lastBuildingSignature = RegionBuildingSignature(province);
        lastCultureSignature = RegionCultureSignature(province);
        lastRegionalLoyalty = int.MinValue;
        lastRegionalFoodState = int.MinValue;
        lastRegionalLevyState = int.MinValue;
        lastUrbanization = province != null ? province.urbanization : int.MinValue;
        RefreshHeader();
        if (buildingMenu != null) buildingMenu.LoadProvince(province, this);
        RefreshHoldingsSummary();
        RepositionRecruitUnitsButton();
    }

    private void ResolveReferences()
    {
        provinceName = provinceName != null ? provinceName : FindText("ProvinceName");
        provinceOwnerName = provinceOwnerName != null ? provinceOwnerName : FindText("ProvinceOwnerName");
        if (buildingMenu == null)
        {
            Transform namedBuildingMenu = FindTransform("BuildingMenu");
            buildingMenu = namedBuildingMenu != null ? namedBuildingMenu.GetComponent<UIBuildingMenu>() : null;
        }
        if (regionSummary == null)
        {
            regionSummary = GetComponent<UIRegionSummary>();
            if (regionSummary == null) regionSummary = gameObject.AddComponent<UIRegionSummary>();
        }
        provincialTotalIncome = provincialTotalIncome != null ? provincialTotalIncome : FindText("Provincial Total Income");
        if (cultureChart == null)
        {
            Transform cultureMenu = FindTransform("CultureMenu");
            if (cultureMenu != null)
            {
                Image oldBackground = cultureMenu.GetComponent<Image>();
                if (oldBackground != null) oldBackground.enabled = false;
                cultureChart = cultureMenu.GetComponentInChildren<UIRegionalCultureChart>(true);
                if (cultureChart == null)
                {
                    GameObject chartObject = new GameObject("RegionalCultureChart", typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(UIRegionalCultureChart));
                    chartObject.layer = gameObject.layer;
                    chartObject.transform.SetParent(cultureMenu, false);
                    RectTransform chartRect = chartObject.GetComponent<RectTransform>();
                    chartRect.anchorMin = Vector2.zero;
                    chartRect.anchorMax = Vector2.one;
                    chartRect.offsetMin = chartRect.offsetMax = Vector2.zero;
                    cultureChart = chartObject.GetComponent<UIRegionalCultureChart>();
                }
            }
        }
        if (loyaltyDisplay == null)
        {
            Transform loyaltyMenu = FindTransform("LoyaltyMenu");
            if (loyaltyMenu != null)
            {
                loyaltyDisplay = loyaltyMenu.GetComponent<UIRegionalLoyaltyDisplay>();
                if (loyaltyDisplay == null) loyaltyDisplay = loyaltyMenu.gameObject.AddComponent<UIRegionalLoyaltyDisplay>();
            }
        }
        if (raiseArmyButton == null)
        {
            Transform raiseArmy = FindTransform("Raise Army");
            raiseArmyButton = raiseArmy != null ? raiseArmy.GetComponent<Button>() : null;
        }
        if (raiseArmyButton != null)
        {
            raiseArmyButton.onClick.RemoveListener(RaiseArmy);
            raiseArmyButton.onClick.AddListener(RaiseArmy);
            Text label = raiseArmyButton.GetComponentInChildren<Text>(true);
            if (label != null) label.text = "Raise Army (" + CampaignEconomy.ArmyCreationCost + " gold)";
        }
        ResolveHoldingsSummary();
    }

    private Text FindText(string objectName)
    {
        Transform[] children = AdministrationRoot().GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName) return children[i].GetComponent<Text>();
        return null;
    }

    private Transform FindTransform(string objectName)
    {
        Transform[] children = AdministrationRoot().GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++) if (children[i].name == objectName) return children[i];
        return null;
    }

    private Transform AdministrationRoot()
    {
        Transform parent = transform.parent;
        return parent != null && parent.name.Equals("AdministrationMenu", System.StringComparison.OrdinalIgnoreCase)
            ? parent : transform;
    }

    private void RefreshHeader()
    {
        if (LoadedProvince == null)
        {
            if (provinceName != null) provinceName.text = "No province selected";
            if (provinceOwnerName != null) provinceOwnerName.text = string.Empty;
            lastOwnerName = null;
            lastAdministrationSummary = null;
            return;
        }

        if (provinceName != null) provinceName.text = !string.IsNullOrWhiteSpace(LoadedProvince.region)
            ? LoadedProvince.region
            : LoadedProvince.name;
        lastOwnerName = LoadedProvince.nation != null ? LoadedProvince.nation.name : "Unowned";
        lastNationGold = LoadedProvince.nation != null ? LoadedProvince.nation.Gold : 0;
        lastAdministrationSummary = AdministrationSummary(LoadedProvince);
        if (provinceOwnerName != null) provinceOwnerName.text = lastOwnerName + " | " + lastAdministrationSummary;
        RefreshCultureChart();
        RefreshLoyaltyDisplay();
        RefreshRaiseArmyButton();
        RefreshRecruitUnitsButton();
        RefreshHoldingsSummary();
    }

    private void CreateHoldingsSummary()
    {
        GameObject root = new GameObject("HoldingsSummary", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        holdingsSummaryRoot = root.transform;
        root.layer = gameObject.layer; root.transform.SetParent(transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.03f, .11f); rect.anchorMax = new Vector2(.38f, .27f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(.08f, .08f, .08f, .9f);
        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        label.layer = gameObject.layer; label.transform.SetParent(root.transform, false);
        holdingsSummary = label.GetComponent<Text>();
        Text existing = GetComponentInChildren<Text>(true);
        holdingsSummary.font = existing != null && existing.font != null ? existing.font :
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        holdingsSummary.fontSize = 12; holdingsSummary.color = Color.white;
        holdingsSummary.alignment = TextAnchor.UpperLeft; holdingsSummary.horizontalOverflow = HorizontalWrapMode.Wrap;
        holdingsSummary.verticalOverflow = VerticalWrapMode.Truncate; holdingsSummary.raycastTarget = false;
        RectTransform labelRect = holdingsSummary.rectTransform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(7f, 5f); labelRect.offsetMax = new Vector2(-7f, -5f);
    }

    private static int RegionalLevyState(Province province)
    {
        if (province == null || province.nation == null) return 0;
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
        bool allowed = region == null || region.AllowsLevyCallups(province.nation);
        int current = 0, maximum = 0;
        System.Collections.Generic.IEnumerable<Province> provinces = region != null && region.provincelist != null
            ? region.provincelist : new[] { province };
        foreach (Province candidate in provinces)
        {
            if (candidate == null || candidate.nation != province.nation || candidate.levyEntitlements == null) continue;
            foreach (ProvinceLevyEntitlement entitlement in candidate.levyEntitlements)
            {
                if (entitlement == null || entitlement.unit == null) continue;
                maximum++;
                if (allowed && entitlement.eligible && entitlement.state == LevyEntitlementState.Available) current++;
            }
        }
        unchecked { return (current * 397) ^ maximum ^ (allowed ? 1 << 30 : 0); }
    }

    private void ResolveHoldingsSummary()
    {
        if (holdingsSummaryRoot == null) holdingsSummaryRoot = FindTransform("HoldingsSummary");
        if (holdingsSummaryRoot == null)
        {
            CreateHoldingsSummary();
            return;
        }

        holdingSlots.Clear();
        for (int i = 0; i < holdingsSummaryRoot.childCount; i++)
        {
            Transform child = holdingsSummaryRoot.GetChild(i);
            if (child != null && child.name.StartsWith("HoldingSlot", System.StringComparison.OrdinalIgnoreCase))
                holdingSlots.Add(child);
        }
        holdingSlots.Sort((left, right) => NaturalSlotNumber(left.name).CompareTo(NaturalSlotNumber(right.name)));

        Text[] texts = buildingMenu != null
            ? buildingMenu.GetComponentsInChildren<Text>(true)
            : holdingsSummaryRoot.GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            if (text == null) continue;
            if (holdingsCountText == null && (text.name.IndexOf("Holding", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.text.Contains("<X>")))
            { holdingsCountText = text; holdingsCountTemplate = text.text; }
            if (urbanizationText == null && (text.name.IndexOf("Urban", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.text.Contains("<Y>")))
            { urbanizationText = text; urbanizationTemplate = text.text; }
        }
        // One combined template may contain both placeholders.
        if (holdingsCountText != null && holdingsCountText == urbanizationText)
            urbanizationTemplate = holdingsCountTemplate;
    }

    private static int NaturalSlotNumber(string name)
    {
        if (string.IsNullOrEmpty(name)) return int.MaxValue;
        int start = name.Length;
        while (start > 0 && char.IsDigit(name[start - 1])) start--;
        return int.TryParse(name.Substring(start), out int number) ? number : int.MaxValue;
    }

    private void RefreshHoldingsSummary()
    {
        if (regionSummary != null) regionSummary.RefreshFor(LoadedProvince);
        UIProvincePanelSummary panelSummary = buildingMenu != null
            ? buildingMenu.GetComponent<UIProvincePanelSummary>() : null;
        if (panelSummary != null)
        {
            panelSummary.RefreshFor(LoadedProvince);
            return;
        }
        ResolveHoldingsSummary();
        if (holdingsSummaryRoot == null) return;
        if (LoadedProvince == null) { holdingsSummaryRoot.gameObject.SetActive(false); return; }
        holdingsSummaryRoot.gameObject.SetActive(true);

        int holdingCount = LoadedProvince.holdings != null
            ? LoadedProvince.holdings.FindAll(holding => holding != null).Count : 0;
        ApplyHoldingsTemplates(holdingCount, Mathf.RoundToInt(LoadedProvince.urbanization));
        RefreshHoldingClassSlots();
        RefreshProvincialTotalIncome();

        // Preserve the generated legacy summary when loading an older scene without the new named slot layout.
        if (holdingSlots.Count > 0 || holdingsSummary == null) return;
        System.Text.StringBuilder text = new System.Text.StringBuilder("Holdings");
        if (LoadedProvince.holdings == null || LoadedProvince.holdings.Count == 0) text.Append("\nNone");
        else foreach (ProvinceHolding holding in LoadedProvince.holdings)
        {
            if (holding == null) continue;
            text.Append("\n").Append(holding.DisplayName).Append(" Lv ").Append(holding.level);
            if (!string.IsNullOrWhiteSpace(holding.cultureName)) text.Append(" - ").Append(holding.cultureName);
            text.Append(" - ").Append(SocioEconomicClassRules.DisplayName(holding.socioEconomicClass));
            bool mobilized = LoadedProvince.IsHoldingMobilized(holding.instanceId);
            int recoveryTicks = LoadedProvince.GetHoldingLevyRecoveryTicks(holding.instanceId);
            int income = LoadedProvince.GetHoldingOutput(holding, HoldingOutputType.Income);
            if (income != 0) text.Append(" - +").Append(income).Append(" gold");
            int food = LoadedProvince.GetHoldingOutput(holding, HoldingOutputType.Food);
            int manpower = LoadedProvince.GetHoldingOutput(holding, HoldingOutputType.Manpower);
            if (food != 0) text.Append(" - ").Append(food).Append(" food");
            if (manpower != 0) text.Append(" - ").Append(manpower).Append(" manpower");
            if (recoveryTicks > 0) text.Append(" - LEVY LOSSES: NO PRODUCTION (").Append(recoveryTicks).Append(" ticks)");
            else if (mobilized) text.Append(" - MOBILIZED");
            if (holding.CanRaiseLevies) text.Append(" - ")
                .Append(holding.EffectiveLevyContribution(LoadedProvince.nation).ToString("0.###")).Append(" levy capacity");
        }
        if (LoadedProvince.holdingConstructionOrders != null)
            foreach (HoldingConstructionOrder order in LoadedProvince.holdingConstructionOrders)
                if (order != null) text.Append("\nTransforming to ").Append(order.holdingId)
                    .Append(" (").Append(order.remainingTicks).Append(" ticks)");
        holdingsSummary.text = text.ToString();
    }

    private void ApplyHoldingsTemplates(int count, int urbanization)
    {
        if (holdingsCountText != null)
        {
            string template = !string.IsNullOrEmpty(holdingsCountTemplate) ? holdingsCountTemplate : holdingsCountText.text;
            holdingsCountText.text = template.Replace("<X>", count.ToString()).Replace("<x>", count.ToString())
                .Replace("<Y>", urbanization.ToString()).Replace("<y>", urbanization.ToString());
        }
        if (urbanizationText != null && urbanizationText != holdingsCountText)
        {
            string template = !string.IsNullOrEmpty(urbanizationTemplate) ? urbanizationTemplate : urbanizationText.text;
            urbanizationText.text = template.Replace("<X>", count.ToString()).Replace("<x>", count.ToString())
                .Replace("<Y>", urbanization.ToString()).Replace("<y>", urbanization.ToString());
        }
    }

    private void RefreshHoldingClassSlots()
    {
        if (holdingSlots.Count == 0) return;
        System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ProvinceHolding>> groups =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ProvinceHolding>>(System.StringComparer.OrdinalIgnoreCase);
        if (LoadedProvince.holdings != null) foreach (ProvinceHolding holding in LoadedProvince.holdings)
        {
            if (holding == null) continue;
            string category = HoldingCategoryRules.GroupName(holding);
            if (!groups.TryGetValue(category, out System.Collections.Generic.List<ProvinceHolding> list))
            { list = new System.Collections.Generic.List<ProvinceHolding>(); groups.Add(category, list); }
            list.Add(holding);
        }
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.List<ProvinceHolding>>> sorted =
            new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.List<ProvinceHolding>>>(groups);
        sorted.Sort((left, right) =>
        {
            int count = right.Value.Count.CompareTo(left.Value.Count);
            return count != 0 ? count : string.Compare(left.Key, right.Key, System.StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < holdingSlots.Count; i++)
        {
            Transform slot = holdingSlots[i];
            Text label = EnsureHoldingSlotLabel(slot);
            Tooltip tooltip = slot.GetComponent<Tooltip>();
            if (tooltip == null) tooltip = slot.gameObject.AddComponent<Tooltip>();
            tooltip.positions = new Vector3(120f, 30f, 0f);
            if (i >= sorted.Count)
            {
                label.text = "0";
                UIHoldingSlotIcon.Set(slot, null);
                tooltip.message = "No holding category in this slot.";
                continue;
            }
            System.Collections.Generic.List<ProvinceHolding> holdings = sorted[i].Value;
            label.text = holdings.Count.ToString();
            UIHoldingSlotIcon.Set(slot, HoldingCategoryRules.RepresentativeIcon(holdings));
            System.Text.StringBuilder message = new System.Text.StringBuilder(sorted[i].Key).Append(" (" + holdings.Count + ")");
            holdings.Sort((left, right) => string.CompareOrdinal(left.DisplayName, right.DisplayName));
            foreach (ProvinceHolding holding in holdings)
                AppendHoldingTooltip(message, holding);
            tooltip.message = message.ToString();
        }
    }

    private void AppendHoldingTooltip(System.Text.StringBuilder message, ProvinceHolding holding)
    {
        bool mobilized = LoadedProvince.IsHoldingMobilized(holding.instanceId);
        int recoveryTicks = LoadedProvince.GetHoldingLevyRecoveryTicks(holding.instanceId);
        message.Append("\n- ").Append(holding.DisplayName).Append(" Lv ").Append(holding.level)
            .Append(" - ").Append(!string.IsNullOrWhiteSpace(holding.cultureName) ? holding.cultureName : "Unassigned");
        message.Append("\n    Class: ").Append(SocioEconomicClassRules.DisplayName(holding.socioEconomicClass));
        if (holding.definition != null && holding.definition.outputs != null)
            foreach (HoldingOutputDefinition output in holding.definition.outputs)
                if (output != null && output.EffectiveUrbanizationResponse != 0)
                    message.Append("\n    ").Append(HoldingOutputLabel(output.type)).Append(" urbanization response: ")
                        .Append(output.EffectiveUrbanizationResponse > 0 ? "+" : string.Empty)
                        .Append(output.EffectiveUrbanizationResponse);
        if (recoveryTicks > 0)
            message.Append("\n    LEVY CASUALTIES: no production for ").Append(recoveryTicks).Append(" more ticks");
        int netFood = LoadedProvince.GetHoldingOutput(holding, HoldingOutputType.Food);
        int grossFood = netFood + holding.FoodConsumption;
        message.Append("\n    Food production: +").Append(grossFood);
        if (holding.FoodUpkeep > 0)
            message.Append("\n    Upkeep: ").Append(holding.FoodUpkeep).Append(" food");
        bool anyOutput = false;
        foreach (HoldingOutputType type in System.Enum.GetValues(typeof(HoldingOutputType)))
        {
            if (type == HoldingOutputType.Food || type == HoldingOutputType.PoliticalInfluence) continue;
            int amount = LoadedProvince.GetHoldingOutput(holding, type);
            if (amount == 0) continue;
            message.Append("\n    ").Append(HoldingOutputLabel(type)).Append(": ").Append(amount > 0 ? "+" : string.Empty).Append(amount);
            anyOutput = true;
        }
        System.Collections.Generic.List<ProvinceLevyEntitlement> holdingLevies =
            LoadedProvince.GetRegionalLevyEntitlementsForHolding(holding.instanceId);
        int levyTotal = holdingLevies.Count;
        float levyContribution = holding.EffectiveLevyContribution(LoadedProvince.nation);
        if (levyContribution > 0f)
        {
            int available = 0;
            foreach (ProvinceLevyEntitlement entitlement in holdingLevies)
                if (entitlement != null && entitlement.state == LevyEntitlementState.Available) available++;
            message.Append("\n    Levy contribution: ").Append(levyContribution.ToString("0.###"));
            message.Append("\n    Levies: ").Append(available).Append("/").Append(levyTotal).Append(" available");
            anyOutput = true;
        }
        if (holding.definition != null && holding.definition.levels != null)
        foreach (HoldingLevelDefinition level in holding.definition.levels)
        {
            if (level == null || level.level > holding.level) continue;
            if (!string.IsNullOrWhiteSpace(level.displayedEffect))
                message.Append("\n    Effect: ").Append(level.displayedEffect.Trim());
            if (level.localModifiers != null && level.localModifiers.maxDevelopment != 0)
                message.Append("\n    ").Append(ProvinceLocalModifiers.FormatMaxDevelopment(level.localModifiers.maxDevelopment));
        }
        if (mobilized) message.Append("\n    Mobilized (affected outputs already reduced)");
        else if (!anyOutput) message.Append("\n    No configured production");
    }

    private void RefreshProvincialTotalIncome()
    {
        if (provincialTotalIncome == null || LoadedProvince == null) return;
        provincialTotalIncome.text = UIProvinceEconomySummary.Build(LoadedProvince);
        provincialTotalIncome.resizeTextForBestFit = true;
        provincialTotalIncome.resizeTextMinSize = 8;
        provincialTotalIncome.resizeTextMaxSize = 16;
    }

    private static string HoldingOutputLabel(HoldingOutputType type)
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

    private Text EnsureHoldingSlotLabel(Transform slot)
    {
        Text label = slot.GetComponentInChildren<Text>(true);
        if (label != null) return label;
        GameObject labelObject = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.layer = slot.gameObject.layer; labelObject.transform.SetParent(slot, false);
        label = labelObject.GetComponent<Text>();
        Text reference = provinceName != null ? provinceName : GetComponentInChildren<Text>(true);
        label.font = reference != null && reference.font != null ? reference.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 20; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return label;
    }

    private static string AdministrationSummary(Province province)
    {
        if (province == null) return string.Empty;
        string culture = "Unassigned 100%";
        if (province.cultures != null && province.cultures.Count > 0)
        {
            System.Collections.Generic.List<string> shares = new System.Collections.Generic.List<string>();
            foreach (Culture entry in province.cultures)
                if (entry != null && !string.IsNullOrEmpty(entry.name))
                    shares.Add(entry.name + " " + province.GetCulturePercentage(entry.name).ToString("0.#") + "%");
            if (shares.Count > 0) culture = string.Join(", ", shares);
        }
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
        System.Collections.Generic.List<string> classes = new System.Collections.Generic.List<string>();
        foreach (System.Collections.Generic.KeyValuePair<SocioEconomicClass, int> entry in province.GetSocioEconomicComposition())
            classes.Add(entry.Key + " " + entry.Value);
        int developmentModifier = province.MaxDevelopmentModifier;
        string developmentEffect = ProvinceLocalModifiers.FormatMaxDevelopment(developmentModifier);
        int development = Mathf.Clamp(province.urbanization, -100, province.MaximumDevelopment);
        string developmentLabel = development < 0 ? "Ruralization: " + -development : "Urbanization: " + development;
        return "Population: " + province.population + " Holdings | " + developmentLabel +
            "/" + province.MaximumDevelopment + (!string.IsNullOrEmpty(developmentEffect) ? " (" + developmentEffect + ")" : string.Empty) +
            "% | Classes: " + (classes.Count > 0 ? string.Join(", ", classes) : "None") + " | Cultures: " + culture +
            " | Region loyalty: " + (region != null ? region.GetLoyalty(province.nation).ToString("0.#") : "0") + "%";
    }

    private void RefreshCultureChart()
    {
        if (cultureChart == null || LoadedProvince == null) return;
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(LoadedProvince.region) : null;
        cultureChart.LoadRegion(region, LoadedProvince);
    }

    private void RefreshLoyaltyDisplay()
    {
        if (loyaltyDisplay == null || LoadedProvince == null) return;
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(LoadedProvince.region) : null;
        lastRegionalLoyalty = region != null ? Mathf.RoundToInt(region.GetLoyalty(LoadedProvince.nation) * 10f) : 0;
        loyaltyDisplay.LoadRegion(region, LoadedProvince.nation);
        UIProvincePanelSummary panelSummary = buildingMenu != null
            ? buildingMenu.GetComponent<UIProvincePanelSummary>() : null;
        if (panelSummary != null) panelSummary.RefreshFor(LoadedProvince);
        else RefreshProvincialTotalIncome();
    }

    private static int RegionCultureSignature(Province province)
    {
        unchecked
        {
            int hash = 17;
            if (province == null) return hash;
            CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
            System.Collections.Generic.IEnumerable<Province> provinces = region != null
                ? region.provincelist : new[] { province };
            foreach (Province member in provinces)
                if (member != null && member.holdings != null)
                    foreach (ProvinceHolding holding in member.holdings)
                    {
                        if (holding == null) continue;
                        hash = hash * 31 + (holding.instanceId != null ? holding.instanceId.GetHashCode() : 0);
                        hash = hash * 31 + (holding.cultureName != null ? holding.cultureName.GetHashCode() : 0);
                        hash = hash * 31 + (holding.allegiance != null ? holding.allegiance.GetHashCode() : 0);
                    }
            return hash;
        }
    }

    private void CreateRecruitUnitsButton()
    {
        // Recruitment belongs to the selected army panel; retained as a no-op so older scenes remain compatible.
        if (transform != null) return;
        GameObject root = new GameObject("Recruit Units", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        RectTransform buildingRect = buildingMenu != null ? buildingMenu.GetComponent<RectTransform>() : null;
        rect.anchorMin = rect.anchorMax = buildingRect != null ? buildingRect.anchorMin : new Vector2(.5f, .5f);
        float buildingHeight = buildingRect != null ? Mathf.Max(0f, buildingRect.sizeDelta.y) : 190f;
        Vector2 buildingPosition = buildingRect != null ? buildingRect.anchoredPosition : new Vector2(0f, 150f);
        rect.anchoredPosition = buildingPosition + Vector2.down * (buildingHeight * .5f + 28f);
        rect.sizeDelta = new Vector2(buildingRect != null ? Mathf.Max(150f, buildingRect.sizeDelta.x) : 190f, 46f);
        root.GetComponent<Image>().color = new Color(.18f, .30f, .16f, .95f);
        recruitUnitsButton = root.GetComponent<Button>();
        recruitUnitsButton.onClick.AddListener(OpenRecruitment);

        GameObject labelRoot = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelRoot.layer = gameObject.layer;
        labelRoot.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelRoot.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 2f); labelRect.offsetMax = new Vector2(-4f, -2f);
        recruitUnitsLabel = labelRoot.GetComponent<Text>();
        Text existingText = GetComponentInChildren<Text>(true);
        recruitUnitsLabel.font = existingText != null && existingText.font != null
            ? existingText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        recruitUnitsLabel.alignment = TextAnchor.MiddleCenter;
        recruitUnitsLabel.color = Color.white;
        recruitUnitsLabel.resizeTextForBestFit = true;
        RefreshRecruitUnitsButton();
    }

    private void RepositionRecruitUnitsButton()
    {
        if (recruitUnitsButton == null || buildingMenu == null) return;
        RectTransform rect = recruitUnitsButton.GetComponent<RectTransform>();
        RectTransform buildingRect = buildingMenu.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = buildingRect.anchorMin;
        rect.anchoredPosition = buildingRect.anchoredPosition +
            Vector2.down * (Mathf.Max(0f, buildingRect.sizeDelta.y) * .5f + 28f);
        rect.sizeDelta = new Vector2(Mathf.Max(150f, buildingRect.sizeDelta.x), 46f);
    }

    private void RefreshRecruitUnitsButton()
    {
        if (recruitUnitsButton == null) return;
        FieldArmyHolder army = FieldArmyHolder.SelectedPlayerArmy;
        Province localProvince = army != null ? army.GrabNearestProvince() : null;
        bool canRecruit = army != null && army.IsFriendlyToLocalPlayer() && localProvince != null;
        recruitUnitsButton.interactable = canRecruit;
        if (recruitUnitsLabel != null)
            recruitUnitsLabel.text = canRecruit
                ? "Recruit Units - " + localProvince.name
                : "Recruit Units - select an army";
    }

    private void OpenRecruitment()
    {
        FieldArmyHolder army = FieldArmyHolder.SelectedPlayerArmy;
        if (army == null || !army.IsFriendlyToLocalPlayer()) return;
        Province localProvince = army.GrabNearestProvince();
        if (localProvince == null) return;
        RecruitmentMenu.EnsureExists();
        if (RecruitmentMenu.Instance != null) RecruitmentMenu.Instance.Open(army, localProvince);
    }

    private void RefreshRaiseArmyButton()
    {
        if (raiseArmyButton == null) return;
        string localNation = CampaignNetworkPlayer.Local != null ? CampaignNetworkPlayer.Local.AssignedNation :
            SessionManager.Instance != null && SessionManager.Instance.HostFaction != null ? SessionManager.Instance.HostFaction.name : string.Empty;
        raiseArmyButton.gameObject.SetActive(LoadedProvince != null && LoadedProvince.nation != null && LoadedProvince.nation.name == localNation);
        raiseArmyButton.interactable = LoadedProvince != null && LoadedProvince.nation != null &&
            LoadedProvince.nation.Gold >= CampaignEconomy.ArmyCreationCost;
    }

    private void RaiseArmy()
    {
        if (LoadedProvince == null) return;
        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsSpawned)
            CampaignNetworkPlayer.Local.RequestRaiseArmy(LoadedProvince.name);
        else if (LoadedProvince.nation != null && LoadedProvince.nation.Gold >= CampaignEconomy.ArmyCreationCost)
        {
            Nation nation = LoadedProvince.nation; nation.Gold -= CampaignEconomy.ArmyCreationCost; nation.ArmyNumber++;
            FieldArmyHolder army = Mapshower.Instance.SpawnArmy(LoadedProvince, nation.ArmyNumber + " Army of " + nation.name);
            if (army == null) { nation.Gold += CampaignEconomy.ArmyCreationCost; return; }
            army.PreserveConfiguredRoster = true;
            army.ConfigureNetworkIdentity(nation.name + "_local_" + nation.ArmyNumber, ulong.MaxValue, true, nation);
            FieldArmyHolder.SelectedPlayerArmy = army;
            FieldArmyHolder.InspectedArmy = army;
            RefreshRaiseArmyButton();
        }
    }

    private static int RegionBuildingSignature(Province province)
    {
        unchecked
        {
            int hash = 17;
            if (province == null) return hash;
            CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
            if (region == null) return BuildingSignature(province);
            foreach (Province regionProvince in region.provincelist)
                if (province.nation != null && regionProvince != null && regionProvince.nation == province.nation)
                    hash = hash * 31 + BuildingSignature(regionProvince);
            return hash;
        }
    }

    private static int BuildingSignature(Province province)
    {
        unchecked
        {
            int hash = 17;
            if (province == null) return hash;
            hash = hash * 31 + Mathf.RoundToInt(province.urbanization * 100f);
            if (province.buildings == null) return hash;
            hash = hash * 31 + province.buildings.Count;
            for (int i = 0; i < province.buildings.Count; i++)
            {
                ProvinceBuilding building = province.buildings[i];
                if (building == null) { hash *= 31; continue; }
                hash = hash * 31 + building.BuildingId.GetHashCode();
                hash = hash * 31 + building.level;
                hash = hash * 31 + building.maxLevel;
            }
            if (province.constructionOrders != null)
                foreach (ProvinceConstructionOrder order in province.constructionOrders)
                {
                    if (order == null) { hash *= 31; continue; }
                    hash = hash * 31 + order.slotIndex;
                    hash = hash * 31 + order.remainingTicks;
                    hash = hash * 31 + (order.buildingId != null ? order.buildingId.GetHashCode() : 0);
                }
            if (province.holdings != null)
                foreach (ProvinceHolding holding in province.holdings)
                {
                    if (holding == null) { hash *= 31; continue; }
                    hash = hash * 31 + (holding.HoldingId != null ? holding.HoldingId.GetHashCode() : 0);
                    hash = hash * 31 + holding.level; hash = hash * 31 + holding.slotIndex;
                    hash = hash * 31 + (holding.cultureName != null ? holding.cultureName.GetHashCode() : 0);
                    hash = hash * 31 + (int)holding.socioEconomicClass;
                    hash = hash * 31 + (holding.allegiance != null ? holding.allegiance.GetHashCode() : 0);
                    hash = hash * 31 + holding.levyEnabled.GetHashCode();
                }
            if (province.holdingConstructionOrders != null)
                foreach (HoldingConstructionOrder order in province.holdingConstructionOrders)
                {
                    if (order == null) { hash *= 31; continue; }
                    hash = hash * 31 + order.slotIndex; hash = hash * 31 + order.targetLevel;
                    hash = hash * 31 + order.remainingTicks;
                    hash = hash * 31 + (order.holdingId != null ? order.holdingId.GetHashCode() : 0);
                }
            if (province.levyEntitlements != null)
                foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
                {
                    if (entitlement == null) { hash *= 31; continue; }
                    hash = hash * 31 + (entitlement.id != null ? entitlement.id.GetHashCode() : 0);
                    hash = hash * 31 + (int)entitlement.state;
                    hash = hash * 31 + entitlement.remainingTicks;
                    hash = hash * 31 + entitlement.eligible.GetHashCode();
                }
            return hash;
        }
    }
}
