using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UIElement : MonoBehaviour
{
    public static UIElement NationHost;
    public static UIElement ProvinceHost;
    public static UIElement ArmyHost;
    private Text currencyText;
    private Text armyNameText;
    private Text armyCompositionText;
    private Text armyCommanderText;
    private RectTransform armyCompositionSprites;
    private Material armyCompositionMaterial;
    private UnitStatDisplayMenu compositionStatDisplay;
    private Coroutine compositionHideRoutine;
    private string currencyTemplate;
    private string armyNameTemplate;
    private string armyCompositionTemplate;
    private string armyCommanderTemplate;
    private int lastGold = int.MinValue;
    private int lastUpkeep = int.MinValue;
    private FieldArmyHolder lastArmy;
    private int lastCompositionSignature = int.MinValue;
    private Button recruitUnitsButton;
    private Button recruitAllLeviesButton;
    private Text recruitUnitsLabel;
    private Text recruitAllLeviesLabel;
    private float nextRecruitmentButtonRefresh;

    private void Awake()
    {
        if (gameObject.name == "NationHost")
        {
            NationHost = this;
            ResolveNationPanel();
        }
        if (gameObject.name == "ProvinceHost")
        {
            ProvinceHost = this;
        }
        if (gameObject.name == "ArmyHost")
        {
            ResolveArmyPanel();
            // MapScene still contains an older ArmyHost. Prefer the one carrying
            // the named campaign army-panel fields.
            if (ArmyHost == null || armyNameText != null) ArmyHost = this;
        }
    }

    private void Start()
    {
        if (gameObject.name == "NationHost") RefreshCurrency();
        if (gameObject.name == "ArmyHost") RefreshArmyPanel(true);
    }

    private void OnDestroy()
    {
        if (armyCompositionMaterial != null) Destroy(armyCompositionMaterial);
        if (compositionStatDisplay != null) Destroy(compositionStatDisplay.gameObject);
    }

    private void Update()
    {
        if (NationHost == this)
        {
            int nationGold = LocalNation() != null ? LocalNation().Gold : 0;
            int nationUpkeep = LocalNation() != null ? LocalNation().LastUnitUpkeep : 0;
            if (nationGold != lastGold || nationUpkeep != lastUpkeep) RefreshCurrency();
        }
        if (ArmyHost != this) return;
        FieldArmyHolder selected = SelectedArmy();
        if (selected != lastArmy || CompositionSignature(selected) != lastCompositionSignature)
            RefreshArmyPanel(true);
        if (Time.unscaledTime >= nextRecruitmentButtonRefresh)
        {
            nextRecruitmentButtonRefresh = Time.unscaledTime + .5f;
            RefreshArmyRecruitmentButtons();
        }
    }

    private void ResolveNationPanel()
    {
        currencyText = FindNamedText("Currency");
        if (currencyText != null) currencyTemplate = currencyText.text;
    }

    private void ResolveArmyPanel()
    {
        armyNameText = FindNamedText("Armyname");
        armyCompositionText = FindNamedText("ArmyComposition");
        armyCommanderText = FindNamedText("ArmyCommander") ?? FindNamedText("ArmyGeneral");
        if (armyCompositionText != null && armyCompositionSprites == null)
        {
            Transform existing = armyCompositionText.transform.Find("Unit Sprites");
            if (existing != null) armyCompositionSprites = existing as RectTransform;
            else
            {
                GameObject root = new GameObject("Unit Sprites", typeof(RectTransform));
                root.layer = armyCompositionText.gameObject.layer;
                root.transform.SetParent(armyCompositionText.transform, false);
                armyCompositionSprites = (RectTransform)root.transform;
                armyCompositionSprites.anchorMin = new Vector2(0f, 0f);
                armyCompositionSprites.anchorMax = new Vector2(1f, 1f);
                armyCompositionSprites.offsetMin = new Vector2(175f, 3f);
                armyCompositionSprites.offsetMax = new Vector2(-5f, -3f);
            }
        }
        if (armyNameText != null) armyNameTemplate = armyNameText.text;
        if (armyCompositionText != null) armyCompositionTemplate = armyCompositionText.text;
        if (armyCommanderText != null) armyCommanderTemplate = armyCommanderText.text;
        if (recruitUnitsButton == null) CreateArmyRecruitmentButtons();
    }

    private Text FindNamedText(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
            if (descendants[i].name == objectName) return descendants[i].GetComponent<Text>();
        return null;
    }

    private static FieldArmyHolder SelectedArmy()
    {
        return FieldArmyHolder.InspectedArmy != null
            ? FieldArmyHolder.InspectedArmy
            : FieldArmyHolder.SelectedPlayerArmy != null
            ? FieldArmyHolder.SelectedPlayerArmy
            : FieldArmyHolder.PlayerFieldArmy;
    }

    private static Nation LocalNation()
    {
        string nationName = CampaignNetworkPlayer.Local != null
            ? CampaignNetworkPlayer.Local.AssignedNation
            : string.Empty;
        if (string.IsNullOrEmpty(nationName) && SessionManager.Instance != null && SessionManager.Instance.HostFaction != null)
            nationName = SessionManager.Instance.HostFaction.name;
        if (Owners.Instance != null && !string.IsNullOrEmpty(nationName))
            return Owners.Instance.nationlist.Find(nation => nation != null && nation.name == nationName);
        FieldArmyHolder army = SelectedArmy();
        return army != null && army.fieldArmy != null ? army.fieldArmy.nation : null;
    }

    public void RefreshArmyPanel(bool includeComposition)
    {
        if (armyNameText == null) ResolveArmyPanel();
        FieldArmyHolder army = SelectedArmy();
        int compositionSignature = CompositionSignature(army);

        SetTemplateText(armyNameText, armyNameTemplate, army != null ? army.gameObject.name : "No army selected");
        if (includeComposition)
        {
            SetTemplateText(armyCompositionText, armyCompositionTemplate,
                army != null && army.fieldArmy != null ? army.fieldArmy.GrabArmySize() + " units" : "None");
            if (army != lastArmy || compositionSignature != lastCompositionSignature)
                RefreshCompositionSprites(army);
            lastCompositionSignature = compositionSignature;
        }
        SetTemplateText(armyCommanderText, armyCommanderTemplate, CommanderOf(army));
        lastArmy = army;
        RefreshArmyRecruitmentButtons();
    }

    private void CreateArmyRecruitmentButtons()
    {
        recruitUnitsButton = CreateArmyActionButton("Recruit Units", new Vector2(.04f, .12f), new Vector2(.48f, .20f),
            new Color(.18f, .30f, .16f, .95f), OpenArmyRecruitment, out recruitUnitsLabel);
        recruitAllLeviesButton = CreateArmyActionButton("Recruit All Available Levies", new Vector2(.04f, .03f), new Vector2(.48f, .11f),
            new Color(.28f, .20f, .08f, .95f), RaiseAllAvailableLevies, out recruitAllLeviesLabel);
        ((RectTransform)recruitUnitsButton.transform).anchoredPosition = new Vector2(-50f, 60f);
        ((RectTransform)recruitAllLeviesButton.transform).anchoredPosition = new Vector2(-50f, 25f);
    }

    private Button CreateArmyActionButton(string objectName, Vector2 anchorMin, Vector2 anchorMax, Color color,
        UnityEngine.Events.UnityAction action, out Text label)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.layer = gameObject.layer; root.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)root.transform;
        Vector2 anchor = (anchorMin + anchorMax) * .5f;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(150f, 40f);
        root.GetComponent<Image>().color = color;
        Button button = root.GetComponent<Button>(); button.onClick.AddListener(action);
        GameObject textRoot = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textRoot.layer = root.layer; textRoot.transform.SetParent(root.transform, false);
        RectTransform textRect = (RectTransform)textRoot.transform; textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 2f); textRect.offsetMax = new Vector2(-4f, -2f);
        label = textRoot.GetComponent<Text>();
        Text reference = GetComponentInChildren<Text>(true);
        label.font = reference != null && reference.font != null ? reference.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.resizeTextForBestFit = true;
        label.text = objectName;
        return button;
    }

    private void RefreshArmyRecruitmentButtons()
    {
        if (recruitUnitsButton == null || recruitAllLeviesButton == null) return;
        FieldArmyHolder army = FieldArmyHolder.SelectedPlayerArmy;
        Province province = army != null ? army.GrabNearestProvince() : null;
        bool friendlyLocal = army != null && army.IsFriendlyToLocalPlayer() && province != null &&
            army.fieldArmy != null && province.nation == army.fieldArmy.nation;
        int availableLevies = friendlyLocal ? province.GetAvailableRegionLevies(army.fieldArmy.nation).Count : 0;
        int capacity = friendlyLocal ? army.fieldArmy.MaxArmySize - army.fieldArmy.GrabArmySize() - army.fieldArmy.GrabQueuedArmySize() : 0;
        recruitUnitsButton.interactable = friendlyLocal;
        recruitAllLeviesButton.interactable = friendlyLocal && availableLevies > 0 && capacity > 0;
        if (recruitUnitsLabel != null) recruitUnitsLabel.text = "Recruit Units";
        if (recruitAllLeviesLabel != null) recruitAllLeviesLabel.text = "Recruit All Available Levies (" +
            Mathf.Min(Mathf.Max(0, capacity), availableLevies) + ")";
    }

    private void OpenArmyRecruitment()
    {
        FieldArmyHolder army = FieldArmyHolder.SelectedPlayerArmy;
        Province province = army != null ? army.GrabNearestProvince() : null;
        if (army == null || !army.IsFriendlyToLocalPlayer() || province == null) return;
        RecruitmentMenu.EnsureExists();
        if (RecruitmentMenu.Instance != null) RecruitmentMenu.Instance.Open(army, province);
    }

    private void RaiseAllAvailableLevies()
    {
        FieldArmyHolder army = FieldArmyHolder.SelectedPlayerArmy;
        Province province = army != null ? army.GrabNearestProvince() : null;
        if (army == null || !army.IsFriendlyToLocalPlayer() || province == null) return;
        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsSpawned)
            CampaignNetworkPlayer.Local.RequestRaiseAllLevies();
        else province.RaiseAllAvailableRegionLevies(army);
        RefreshArmyRecruitmentButtons();
    }

    public void RefreshCurrency()
    {
        if (currencyText == null) ResolveNationPanel();
        Nation localNation = LocalNation();
        lastGold = localNation != null ? localNation.Gold : 0;
        lastUpkeep = localNation != null ? localNation.LastUnitUpkeep : 0;
        string value = localNation != null
            ? lastGold + " (income " + localNation.LastGrossIncome + ", upkeep " + lastUpkeep +
                (localNation.UpkeepDebt > 0 ? ", debt " + localNation.UpkeepDebt : string.Empty) + ")"
            : lastGold.ToString();
        SetTemplateText(currencyText, currencyTemplate, value);
    }

    private static void SetTemplateText(Text target, string template, string value)
    {
        if (target == null) return;
        target.text = !string.IsNullOrEmpty(template) && template.Contains("<X>")
            ? template.Replace("<X>", value ?? string.Empty)
            : value ?? string.Empty;
    }

    private static string CompositionOf(FieldArmyHolder army)
    {
        if (army == null || army.fieldArmy == null) return "None";
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < army.fieldArmy.USDReserves.Count; i++)
        {
            ArmyReserves reserve = army.fieldArmy.USDReserves[i];
            if (reserve == null || reserve.USD == null || reserve.amount <= 0) continue;
            if (text.Length > 0) text.Append('\n');
            text.Append(reserve.amount).Append("X : ").Append(reserve.USD.name);
        }
        return text.Length > 0 ? text.ToString() : "None";
    }

    private static int CompositionSignature(FieldArmyHolder army)
    {
        unchecked
        {
            int hash = 17;
            if (army == null || army.fieldArmy == null) return hash;
            foreach (ArmyReserves reserve in army.fieldArmy.USDReserves)
            {
                if (reserve == null || reserve.USD == null || reserve.amount <= 0) continue;
                hash = hash * 31 + reserve.USD.GetInstanceID();
                hash = hash * 31 + reserve.amount;
            }
            return hash;
        }
    }

    private void RefreshCompositionSprites(FieldArmyHolder army)
    {
        if (armyCompositionSprites == null) return;
        for (int i = armyCompositionSprites.childCount - 1; i >= 0; i--)
            Destroy(armyCompositionSprites.GetChild(i).gameObject);
        if (armyCompositionMaterial != null) Destroy(armyCompositionMaterial);
        armyCompositionMaterial = null;
        if (army == null || army.fieldArmy == null) return;

        List<UnitSaveData> units = new List<UnitSaveData>();
        foreach (ArmyReserves reserve in army.fieldArmy.USDReserves)
            if (reserve != null && reserve.USD != null)
                for (int i = 0; i < reserve.amount; i++) units.Add(reserve.USD);
        if (units.Count == 0) return;

        Material baseMaterial = FindUnitArtworkMaterial();
        if (baseMaterial != null)
        {
            armyCompositionMaterial = Instantiate(baseMaterial);
            Faction faction = army.fieldArmy.nation != null ? army.fieldArmy.nation.faction : null;
            if (faction != null)
            {
                if (armyCompositionMaterial.HasProperty("_FactionColor")) armyCompositionMaterial.SetColor("_FactionColor", faction.color);
                if (armyCompositionMaterial.HasProperty("_FactionColor2")) armyCompositionMaterial.SetColor("_FactionColor2", faction.color2);
                if (armyCompositionMaterial.HasProperty("_FactionColor3")) armyCompositionMaterial.SetColor("_FactionColor3", faction.color3);
            }
        }

        float availableWidth = armyCompositionSprites.rect.width > 0f ? armyCompositionSprites.rect.width : 1020f;
        float layoutSize = Mathf.Clamp(availableWidth / units.Count, 44f, 78f);
        float iconSize = layoutSize * 2f;
        float iconSpacing = layoutSize * .94f;
        float usedWidth = iconSize + iconSpacing * Mathf.Max(0, units.Count - 1);
        float startX = Mathf.Max(0f, (availableWidth - usedWidth) * .5f);
        for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            CreateCompositionPortrait(units[unitIndex], unitIndex, startX + iconSize * .5f + iconSpacing * unitIndex, iconSize);
    }

    private void CreateCompositionPortrait(UnitSaveData unit, int index, float x, float size)
    {
        if (unit == null || unit.bodyparts == null) return;
        GameObject portrait = new GameObject("Unit " + index + " - " + unit.name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ArmyCompositionUnitHover));
        portrait.layer = armyCompositionText.gameObject.layer;
        portrait.transform.SetParent(armyCompositionSprites, false);
        Image hitArea = portrait.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        ArmyCompositionUnitHover hover = portrait.GetComponent<ArmyCompositionUnitHover>();
        hover.Owner = this;
        hover.Unit = unit;
        RectTransform portraitRect = (RectTransform)portrait.transform;
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, .5f);
        portraitRect.pivot = new Vector2(.5f, .5f);
        portraitRect.anchoredPosition = new Vector2(x, size * .08f);
        // Keep the enlarged artwork, but make overlapping portraits easier to target individually.
        portraitRect.sizeDelta = new Vector2(size * .6f, size);

        Vector2[] offsets =
        {
            Vector2.zero,
            new Vector2(-.072f, -.216f) * size,
            new Vector2(.146f, -.082f) * size
        };
        for (int layerIndex = 0; layerIndex < Mathf.Min(3, unit.bodyparts.Count); layerIndex++)
        {
            Sprite sprite = unit.bodyparts[layerIndex];
            if (sprite == null) continue;
            GameObject layer = new GameObject("Layer " + layerIndex, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            layer.layer = portrait.layer;
            layer.transform.SetParent(portrait.transform, false);
            Image image = layer.GetComponent<Image>();
            image.sprite = sprite;
            image.material = armyCompositionMaterial;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform rect = (RectTransform)layer.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = offsets[layerIndex];
        }
    }

    private static Material FindUnitArtworkMaterial()
    {
        foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
            if (material != null && (material.name == "New Material 1" || material.name.StartsWith("New Material 1 (")))
                return material;
        return null;
    }

    public void ShowCompositionUnitDetails(UnitSaveData unit)
    {
        if (unit == null || !EnsureCompositionStatDisplay()) return;
        KeepCompositionUnitDetailsOpen();
        compositionStatDisplay.gameObject.SetActive(true);
        compositionStatDisplay.LoadNewUnit(unit, armyCompositionMaterial);
        PositionCompositionStatDisplay();
        compositionStatDisplay.transform.SetAsLastSibling();
    }

    public void HideCompositionUnitDetails()
    {
        if (compositionHideRoutine != null) StopCoroutine(compositionHideRoutine);
        compositionHideRoutine = null;
        if (compositionStatDisplay != null) compositionStatDisplay.gameObject.SetActive(false);
    }

    public void KeepCompositionUnitDetailsOpen()
    {
        if (compositionHideRoutine == null) return;
        StopCoroutine(compositionHideRoutine);
        compositionHideRoutine = null;
    }

    public void RequestHideCompositionUnitDetails()
    {
        KeepCompositionUnitDetailsOpen();
        compositionHideRoutine = StartCoroutine(HideCompositionUnitDetailsAfterGracePeriod());
    }

    private IEnumerator HideCompositionUnitDetailsAfterGracePeriod()
    {
        yield return new WaitForSecondsRealtime(.3f);
        compositionHideRoutine = null;
        if (compositionStatDisplay != null) compositionStatDisplay.gameObject.SetActive(false);
    }

    private bool EnsureCompositionStatDisplay()
    {
        if (compositionStatDisplay != null) return true;
        GameObject prefab = Resources.Load<GameObject>("Prefabs/UnitStatDisplayMenu");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (prefab == null || canvas == null) return false;
        GameObject display = Instantiate(prefab, canvas.rootCanvas.transform, false);
        display.name = "Army Unit Stat Display";
        compositionStatDisplay = display.GetComponent<UnitStatDisplayMenu>();
        if (compositionStatDisplay == null)
        {
            Destroy(display);
            return false;
        }
        RectTransform rect = (RectTransform)display.transform;
        CompositionStatDisplayHover hoverArea = display.AddComponent<CompositionStatDisplayHover>();
        hoverArea.Owner = this;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.localScale = Vector3.one * .7f;
        display.SetActive(false);
        return true;
    }

    private void PositionCompositionStatDisplay()
    {
        RectTransform displayRect = compositionStatDisplay.transform as RectTransform;
        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (displayRect == null || canvasRect == null) return;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, eventCamera,
            out Vector2 pointer)) return;

        Vector2 scaledSize = Vector2.Scale(displayRect.rect.size, displayRect.localScale);
        Vector2 position = pointer + new Vector2(scaledSize.x * .7f, scaledSize.y * .1f);
        float halfWidth = scaledSize.x * .5f;
        float halfHeight = scaledSize.y * .5f;
        position.x = Mathf.Clamp(position.x, canvasRect.rect.xMin + halfWidth, canvasRect.rect.xMax - halfWidth);
        position.y = Mathf.Clamp(position.y, canvasRect.rect.yMin + halfHeight, canvasRect.rect.yMax - halfHeight);
        displayRect.anchoredPosition = position;
    }

    private static string CommanderOf(FieldArmyHolder army)
    {
        if (army == null) return "None";
        ProjectX.TileBattle.TileGeneralPersonality personality =
            ProjectX.TileBattle.TileBattleCampaignAdapter.CreatePersonality(army);
        StringBuilder text = new StringBuilder(personality.Name);
        AppendTrait(text, "Aggressive", personality.Aggressive);
        AppendTrait(text, "Defensive", personality.Defensive);
        AppendTrait(text, "Cautious", personality.Cautious);
        AppendTrait(text, "Opportunistic", personality.Opportunistic);
        AppendTrait(text, "Cavalry Commander", personality.CavalryMinded);
        AppendTrait(text, "Methodical", personality.Methodical);
        AppendTrait(text, "Bold", personality.Bold);
        AppendTrait(text, "Patient", personality.Patient);
        AppendTrait(text, "Stubborn", personality.Stubborn);
        return text.ToString();
    }

    private static void AppendTrait(StringBuilder text, string name, int value)
    {
        if (value > 0) text.Append('\n').Append(name);
    }
    public void UpdateTitle(string text, string supply = "")
    {
        if (this == ArmyHost && armyNameText != null)
            SetTemplateText(armyNameText, armyNameTemplate, text);
        else if (transform.childCount > 0)
            transform.GetChild(0).gameObject.GetComponent<Text>().text = text;
    }
    public void UpdateSecond(string text, string supply = "")
    {
        if (this != ArmyHost && transform.childCount > 1)
            transform.GetChild(1).gameObject.GetComponent<Text>().text = supply + "\n Supply Available";
    }
    public void UpdateThree(string text, string supply = "")
    {
        if (this == ArmyHost && armyCompositionText != null)
            SetTemplateText(armyCompositionText, armyCompositionTemplate, text.TrimEnd());
        else if (transform.childCount > 2)
            transform.GetChild(2).gameObject.GetComponent<Text>().text = text;
    }
}
