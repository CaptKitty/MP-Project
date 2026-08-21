using UnityEngine;
using UnityEngine.UI;

public class UIProvinceHost : MonoBehaviour
{
    public static UIProvinceHost Instance { get; private set; }

    public Province LoadedProvince { get; private set; }
    private Text provinceName;
    private Text provinceOwnerName;
    private UIBuildingMenu buildingMenu;
    private string lastOwnerName;
    private int lastBuildingSignature;
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
            if (owner != lastOwnerName || gold != lastNationGold) RefreshHeader();
            int signature = RegionBuildingSignature(LoadedProvince);
            if (signature != lastBuildingSignature)
            {
                lastBuildingSignature = signature;
                if (buildingMenu != null) buildingMenu.LoadProvince(LoadedProvince, this);
                RepositionRecruitUnitsButton();
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
        RefreshHeader();
        if (buildingMenu != null) buildingMenu.LoadProvince(province, this);
        RepositionRecruitUnitsButton();
    }

    private void ResolveReferences()
    {
        provinceName = provinceName != null ? provinceName : FindText("ProvinceName");
        provinceOwnerName = provinceOwnerName != null ? provinceOwnerName : FindText("ProvinceOwnerName");
        buildingMenu = buildingMenu != null ? buildingMenu : GetComponentInChildren<UIBuildingMenu>(true);
        if (raiseArmyButton == null) CreateRaiseArmyButton();
        if (recruitUnitsButton == null) CreateRecruitUnitsButton();
    }

    private Text FindText(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName) return children[i].GetComponent<Text>();
        return null;
    }

    private void RefreshHeader()
    {
        if (LoadedProvince == null)
        {
            if (provinceName != null) provinceName.text = "No province selected";
            if (provinceOwnerName != null) provinceOwnerName.text = string.Empty;
            lastOwnerName = null;
            return;
        }

        if (provinceName != null) provinceName.text = LoadedProvince.name;
        lastOwnerName = LoadedProvince.nation != null ? LoadedProvince.nation.name : "Unowned";
        lastNationGold = LoadedProvince.nation != null ? LoadedProvince.nation.Gold : 0;
        if (provinceOwnerName != null) provinceOwnerName.text = lastOwnerName;
        RefreshRaiseArmyButton();
        RefreshRecruitUnitsButton();
    }

    private void CreateRecruitUnitsButton()
    {
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
            return hash;
        }
    }
}
