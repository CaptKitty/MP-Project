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
    private Text holdingsSummary;
    private string lastOwnerName;
    private string lastAdministrationSummary;
    private int lastBuildingSignature;
    private int lastCultureSignature;
    private int lastRegionalLoyalty = int.MinValue;
    private int lastNationGold = int.MinValue;
    private Button raiseArmyButton;
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
            int regionalLoyalty = loadedRegion != null ? Mathf.RoundToInt(loadedRegion.loyalty * 10f) : 0;
            if (regionalLoyalty != lastRegionalLoyalty) RefreshLoyaltyDisplay();
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
        RefreshHeader();
        if (buildingMenu != null) buildingMenu.LoadProvince(province, this);
        RefreshHoldingsSummary();
        RepositionRecruitUnitsButton();
    }

    private void ResolveReferences()
    {
        provinceName = provinceName != null ? provinceName : FindText("ProvinceName");
        provinceOwnerName = provinceOwnerName != null ? provinceOwnerName : FindText("ProvinceOwnerName");
        buildingMenu = buildingMenu != null ? buildingMenu : GetComponentInChildren<UIBuildingMenu>(true);
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
        if (raiseArmyButton == null) CreateRaiseArmyButton();
        if (holdingsSummary == null) CreateHoldingsSummary();
    }

    private Text FindText(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName) return children[i].GetComponent<Text>();
        return null;
    }

    private Transform FindTransform(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++) if (children[i].name == objectName) return children[i];
        return null;
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

    private void RefreshHoldingsSummary()
    {
        if (holdingsSummary == null) return;
        if (LoadedProvince == null) { holdingsSummary.transform.parent.gameObject.SetActive(false); return; }
        holdingsSummary.transform.parent.gameObject.SetActive(true);
        System.Text.StringBuilder text = new System.Text.StringBuilder("Holdings");
        if (LoadedProvince.holdings == null || LoadedProvince.holdings.Count == 0) text.Append("\nNone");
        else foreach (ProvinceHolding holding in LoadedProvince.holdings)
        {
            if (holding == null) continue;
            text.Append("\n").Append(holding.DisplayName).Append(" Lv ").Append(holding.level);
            if (!string.IsNullOrWhiteSpace(holding.cultureName)) text.Append(" - ").Append(holding.cultureName);
            text.Append(" - ").Append(holding.socioEconomicClass);
            bool mobilized = LoadedProvince.IsHoldingMobilized(holding.instanceId);
            int income = holding.GetOutput(HoldingOutputType.Income, LoadedProvince.urbanization, mobilized);
            if (income != 0) text.Append(" - +").Append(income).Append(" gold");
            int food = holding.GetOutput(HoldingOutputType.Food, LoadedProvince.urbanization,
                mobilized);
            int influence = holding.GetOutput(HoldingOutputType.PoliticalInfluence, LoadedProvince.urbanization,
                mobilized);
            int manpower = holding.GetOutput(HoldingOutputType.Manpower, LoadedProvince.urbanization,
                mobilized);
            if (food != 0) text.Append(" - ").Append(food).Append(" food");
            if (influence != 0) text.Append(" - ").Append(influence).Append(" influence");
            if (manpower != 0) text.Append(" - ").Append(manpower).Append(" manpower");
            if (mobilized) text.Append(" - MOBILIZED");
            if (holding.CanRaiseLevies) text.Append(" - ").Append(holding.LevyFormationCount).Append(" levies");
        }
        if (LoadedProvince.holdingConstructionOrders != null)
            foreach (HoldingConstructionOrder order in LoadedProvince.holdingConstructionOrders)
                if (order != null) text.Append("\nTransforming to ").Append(order.holdingId)
                    .Append(" (").Append(order.remainingTicks).Append(" ticks)");
        holdingsSummary.text = text.ToString();
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
        return "Population: " + province.population + " Holdings | Urbanization: " + Mathf.Clamp(province.urbanization, 0, 100) +
            "% | Classes: " + (classes.Count > 0 ? string.Join(", ", classes) : "None") + " | Cultures: " + culture +
            " | Region loyalty: " + (region != null ? region.loyalty.ToString("0.#") : "0") + "%";
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
        lastRegionalLoyalty = region != null ? Mathf.RoundToInt(region.loyalty * 10f) : 0;
        loyaltyDisplay.LoadRegion(region);
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

    private void CreateRaiseArmyButton()
    {
        GameObject root = new GameObject("Raise Army", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.transform.SetParent(transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.62f, .02f); rect.anchorMax = new Vector2(.97f, .10f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(.35f, .08f, .04f, .95f);
        raiseArmyButton = root.GetComponent<Button>(); raiseArmyButton.onClick.AddListener(RaiseArmy);
        GameObject labelRoot = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelRoot.transform.SetParent(root.transform, false); RectTransform labelRect = labelRoot.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text label = labelRoot.GetComponent<Text>();
        Text existingText = GetComponentInChildren<Text>(true);
        label.font = existingText != null && existingText.font != null
            ? existingText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.resizeTextForBestFit = true;
        label.text = "Raise Army (" + CampaignEconomy.ArmyCreationCost + " gold)";
        RefreshRaiseArmyButton();
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
            if (province == null || province.buildings == null) return hash;
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
                    hash = hash * 31 + (int)holding.socioEconomicClass; hash = hash * 31 + holding.levyEnabled.GetHashCode();
                }
            if (province.holdingConstructionOrders != null)
                foreach (HoldingConstructionOrder order in province.holdingConstructionOrders)
                {
                    if (order == null) { hash *= 31; continue; }
                    hash = hash * 31 + order.slotIndex; hash = hash * 31 + order.targetLevel;
                    hash = hash * 31 + order.remainingTicks;
                    hash = hash * 31 + (order.holdingId != null ? order.holdingId.GetHashCode() : 0);
                }
            return hash;
        }
    }
}
