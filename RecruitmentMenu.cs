using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RecruitmentMenu : MonoBehaviour
{
    public static RecruitmentMenu Instance;

    private FieldArmyHolder army;
    private Province clickedProvince;
    private RectTransform content;
    private Text title;
    private Font font;

    public static RecruitmentMenu Create(Canvas canvas)
    {
        if (Instance != null) return Instance;

        GameObject root = new GameObject("RecruitmentMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.layer = 5;
        root.transform.SetParent(canvas.transform, false);
        RecruitmentMenu menu = root.AddComponent<RecruitmentMenu>();
        menu.Build();
        root.SetActive(false);
        return menu;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        Instance = this;
        font = FindAnyObjectByType<Text>() != null
            ? FindAnyObjectByType<Text>().font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        RectTransform root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(760f, 680f);
        GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.96f);

        title = CreateText("Title", transform, "Recruitment", 24, TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -62f), new Vector2(-80f, -12f));

        Button close = CreateButton("Close", transform, "X", Close);
        SetRect((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-58f, -58f), new Vector2(-12f, -12f));

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        viewportObject.layer = 5;
        viewportObject.transform.SetParent(transform, false);
        RectTransform viewport = (RectTransform)viewportObject.transform;
        SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(15f, 15f), new Vector2(-15f, -75f));
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.layer = 5;
        contentObject.transform.SetParent(viewport, false);
        content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    public static void Show(FieldArmyHolder targetArmy = null)
    {
        if (Instance == null) return;
        Instance.Open(targetArmy != null ? targetArmy : FieldArmyHolder.PlayerFieldArmy);
    }

    public static void RefreshQueueFor(FieldArmy updatedArmy)
    {
        if (Instance == null || !Instance.gameObject.activeInHierarchy ||
            Instance.army == null || Instance.army.fieldArmy != updatedArmy) return;
        Instance.Refresh();
    }

    public static void EnsureExists()
    {
        Canvas upgradeCanvas = null;
        FactionUpgrade[] upgradeMenus = FindObjectsByType<FactionUpgrade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (FactionUpgrade upgradeMenu in upgradeMenus)
        {
            Canvas candidate = upgradeMenu.GetComponentInParent<Canvas>(true);
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                upgradeCanvas = candidate;
                break;
            }
        }
        if (Instance != null)
        {
            if (upgradeCanvas != null && Instance.transform.parent != upgradeCanvas.transform)
            {
                Instance.transform.SetParent(upgradeCanvas.transform, false);
            }
            return;
        }
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas canvas = upgradeCanvas;
        if (canvas == null) canvas = System.Array.Find(canvases, item => item != null && item.isRootCanvas && item.renderMode == RenderMode.ScreenSpaceOverlay);
        if (canvas == null && canvases.Length > 0) canvas = canvases[0];
        if (canvas != null) Create(canvas);
    }

    public void OpenForPlayerArmy()
    {
        Open(FieldArmyHolder.PlayerFieldArmy);
    }

    public void Open(FieldArmyHolder targetArmy)
    {
        Open(targetArmy, null);
    }

    public void Open(FieldArmyHolder targetArmy, Province selectedProvince)
    {
        if (targetArmy == null) return;
        army = targetArmy;
        clickedProvince = selectedProvince;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (army == null || Owners.Instance == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        Province current = army.GrabNearestProvince();
        if (current == null)
        {
            title.text = "Recruitment — no province";
            AddMessage("This army is not close enough to a province.");
            return;
        }

        title.text = "Recruitment — " + (!string.IsNullOrWhiteSpace(current.region) ? current.region : current.name);
        if (clickedProvince != null && clickedProvince != current)
        {
            title.text += " (selected " + clickedProvince.name + ")";
        }
        if (army.fieldArmy.recruitmentOrders != null && army.fieldArmy.recruitmentOrders.Count > 0)
        {
            AddHeader("Recruitment queue");
            foreach (ArmyRecruitmentOrder order in army.fieldArmy.recruitmentOrders)
                if (order != null && order.unit != null)
                    AddMessage(order.amount + "X " + order.unit.name + " - " + order.remainingTicks + " ticks remaining");
        }
        army.fieldArmy.ReconcileFormationRecords();
        List<ArmyFormationRecord> raisedLevies = army.fieldArmy.formationRecords.FindAll(record => record != null &&
            record.origin == CampaignUnitOrigin.Levy && !string.IsNullOrEmpty(record.entitlementId));
        if (raisedLevies.Count > 0)
        {
            AddHeader("Raised levies");
            foreach (ArmyFormationRecord record in raisedLevies)
            {
                ArmyFormationRecord captured = record;
                Button button = CreateButton("Demobilize " + record.unit.name, content,
                    record.unit.name + " — free upkeep — demobilize", () => { army.fieldArmy.DemobilizeLevy(captured.entitlementId); Refresh(); });
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            }
        }
        if (!army.IsTargetNull())
        {
            AddMessage("Recruitment and levy call-ups are paused while this army is moving.");
            return;
        }
        AddHeader("Regional units");
        if (current.nation != army.fieldArmy.nation)
        {
            AddMessage("Local recruitment requires an owned province.");
        }
        else
        {
            if (!current.AllowsRecruitment(army.fieldArmy.nation))
            {
                AddMessage("Regional loyalty must be at least 50% to recruit units or raise levies.");
                return;
            }
            List<UnitSaveData> locals = current.GetRecruitableRegionUnits(army.fieldArmy.nation);
            if (locals.Count == 0) AddMessage("No units are unlocked by occupied provinces in this region.");
            foreach (UnitSaveData unit in locals)
            {
                Province source = current.FindRegionalRecruitmentSource(unit, army.fieldArmy.nation);
                if (source != null) AddRecruitButton(unit, false, source, -1);
            }

            AddHeader("Recoverable levies");
            List<ProvinceLevyEntitlement> levies = current.GetAvailableRegionLevies(army.fieldArmy.nation);
            if (levies.Count == 0) AddMessage("No eligible levy formations are currently available.");
            foreach (ProvinceLevyEntitlement levy in levies)
            {
                Province levySource = current.GetOccupiedRegionProvinces(army.fieldArmy.nation).Find(candidate =>
                    candidate.levyEntitlements != null && candidate.levyEntitlements.Contains(levy));
                if (levySource == null) continue;
                string label = levy.unit.name.Replace("(Clone)", "") + " — free levy — " + levySource.name;
                Button levyButton = CreateButton("Raise " + levy.unit.name, content, label, () => RaiseLevy(levy, levySource));
                levyButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 132f;
                AddUnitArtwork(levyButton, levy.unit);
            }
        }

        if (ProvinceMercenaryPool.Enabled)
        {
            AddHeader("Mercenaries - current province");
            int mercenaryRows = 0;
            foreach (ProvinceMercenaryPool pool in current.mercenaryPools)
            {
                if (pool == null || pool.unit == null) continue;
                AddRecruitButton(pool.unit, true, current, pool.available);
                mercenaryRows++;
            }
            if (mercenaryRows == 0) AddMessage("No mercenaries are available in this province.");
        }
    }

    private void AddRecruitButton(UnitSaveData unit, bool mercenary, Province source, int stock)
    {
        Nation recruitingNation = army != null && army.fieldArmy != null ? army.fieldArmy.nation : null;
        CampaignUnitOrigin origin = mercenary ? CampaignUnitOrigin.Mercenary : CampaignUnitOrigin.Professional;
        string label = unit.name.Replace("(Clone)", "") + " — " + CampaignEconomy.UnitGoldCost(unit, 1, recruitingNation, origin) + " gold";
        label += mercenary ? " — " + source.name + " (" + stock + ")" : " — " + source.name;
        int recruitmentTicks = unit.EffectiveRecruitmentTicks;
        if (recruitingNation != null && mercenary)
            recruitmentTicks = Mathf.Max(1, recruitingNation.ApplyLawModifiers(
                NationalLawEffectType.MercenaryRecruitmentTime, recruitmentTicks, null, origin));
        label += " | " + recruitmentTicks + " ticks";
        Button button = CreateButton("Recruit " + unit.name, content, label, () => Recruit(unit, mercenary, source));
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 132f;
        button.interactable = !mercenary || stock > 0;
        AddUnitArtwork(button, unit);
    }

    private void Recruit(UnitSaveData unit, bool mercenary, Province source)
    {
        if (army == null || !army.IsTargetNull()) return;
        if (mercenary && !ProvinceMercenaryPool.Enabled) return;
        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsSpawned)
        {
            CampaignNetworkPlayer.Local.RequestProvinceRecruit(unit.name, 1, mercenary, source.name);
        }
        else if (army != null && army.fieldArmy != null && army.fieldArmy.nation != null && source != null)
        {
            Nation nation = army.fieldArmy.nation;
            CampaignUnitOrigin origin = mercenary ? CampaignUnitOrigin.Mercenary : CampaignUnitOrigin.Professional;
            int goldCost = CampaignEconomy.UnitGoldCost(unit, 1, nation, origin);
            Province current = army.GrabNearestProvince();
            if (current == null || !current.AllowsRecruitment(nation)) return;
            if (nation.Gold < goldCost || army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize() >= army.fieldArmy.MaxArmySize) return;
            if (mercenary)
            {
                if (source != current) return;
                ProvinceMercenaryPool pool = source.FindMercenary(unit.name);
                int supplyCost = Mathf.Max(1, unit.cost / 50);
                if (pool == null || pool.available <= 0 || army.fieldArmy.ArmySupply < supplyCost) return;
                pool.available--; army.fieldArmy.ArmySupply -= supplyCost;
            }
            else
            {
                int manpowerCost = Mathf.Max(1, unit.cost / 100);
                if (current == null || source.nation != nation || !current.SharesRegionWith(source) ||
                    !source.CanRecruitLocal(unit) || nation.Manpower < manpowerCost) return;
                nation.Manpower -= manpowerCost;
            }
            if (!army.fieldArmy.QueueRecruitment(unit, 1, origin)) return;
            nation.Gold -= goldCost;
        }
        Invoke(nameof(Refresh), 0.2f);
    }

    private void RaiseLevy(ProvinceLevyEntitlement entitlement, Province source)
    {
        if (army == null || !army.IsTargetNull()) return;
        if (entitlement == null || source == null || army == null) return;
        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsSpawned)
            CampaignNetworkPlayer.Local.RequestRaiseLevy(entitlement.id, source.name);
        else source.RaiseLevy(entitlement.id, army);
        Invoke(nameof(Refresh), 0.2f);
    }

    private void AddHeader(string value)
    {
        Text text = CreateText("Header", content, value, 20, TextAnchor.MiddleLeft);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 36f;
    }

    private void AddMessage(string value)
    {
        Text text = CreateText("Message", content, value, 16, TextAnchor.MiddleLeft);
        text.color = new Color(0.8f, 0.8f, 0.8f);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 32f;
    }

    private Text CreateText(string objectName, Transform parent, string value, int size, TextAnchor alignment)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.layer = 5;
        child.transform.SetParent(parent, false);
        Text text = child.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = value;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        child.layer = 5;
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        Button button = child.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        Text text = CreateText("Text", child.transform, label, 16, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));
        return button;
    }

    private void AddUnitArtwork(Button button, UnitSaveData unit)
    {
        if (unit == null || unit.bodyparts == null || unit.bodyparts.Count == 0) return;

        GameObject portrait = new GameObject("Unit Artwork", typeof(RectTransform));
        portrait.layer = 5;
        portrait.transform.SetParent(button.transform, false);
        RectTransform portraitRect = (RectTransform)portrait.transform;
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0.5f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(70f, 0f);
        portraitRect.sizeDelta = new Vector2(120f, 120f);

        Material artworkMaterial = FindArtworkMaterial();

        int layerCount = Mathf.Min(3, unit.bodyparts.Count);
        Vector2[] slotOffsets =
        {
            Vector2.zero,
            new Vector2(-0.072f, -0.216f) * portraitRect.sizeDelta.x,
            new Vector2(0.146f, -0.082f) * portraitRect.sizeDelta.x
        };
        for (int i = 0; i < layerCount; i++)
        {
            if (unit.bodyparts[i] == null) continue;
            GameObject layer = new GameObject("Artwork Layer " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            layer.layer = 5;
            layer.transform.SetParent(portrait.transform, false);
            Image image = layer.GetComponent<Image>();
            image.sprite = unit.bodyparts[i];
            image.material = artworkMaterial;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform layerRect = (RectTransform)layer.transform;
            layerRect.anchorMin = layerRect.anchorMax = new Vector2(0.5f, 0.5f);
            layerRect.pivot = new Vector2(0.5f, 0.5f);
            layerRect.sizeDelta = portraitRect.sizeDelta;
            layerRect.anchoredPosition = slotOffsets[i];
        }

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 19;
            label.rectTransform.offsetMin = new Vector2(145f, 2f);
        }
    }

    private static Material FindArtworkMaterial()
    {
        Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
        foreach (Material material in materials)
        {
            if (material != null && (material.name == "New Material 1" || material.name.StartsWith("New Material 1 (")))
            {
                return material;
            }
        }

        FieldArmyHolder exampleArmy = FieldArmyHolder.PlayerFieldArmy;
        if (exampleArmy != null)
        {
            SpriteRenderer renderer = exampleArmy.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.name.StartsWith("New Material 1"))
            {
                return renderer.sharedMaterial;
            }
        }
        return null;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
