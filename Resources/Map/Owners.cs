using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class Owners : MonoBehaviour
{
    public static Owners Instance;
    public List<Nation> nationlist;
    public Dictionary<string, Nation> nationdict;
    public List<State> statelist;
    public List<CampaignRegion> regionlist = new List<CampaignRegion>();
    public Dictionary<string, CampaignRegion> regiondict;
    public List<Culture> culturelist;
    public Dictionary<string, Culture> culturedict;
    public List<Province> provincelist;
    public GameObject CityObject;
    public Dictionary<string, Province> provincedict;
    public Dictionary<Color32, Province> provincedictcolor;
    public List<FieldArmyHolder> armylist = new List<FieldArmyHolder>();
    public double timer;
    public int turncounter;
    [Range(0f, 10f)] public float CampaignSimulationSpeed = 0.25f;
    public bool CampaignPaused;
    private float campaignStepAccumulator;
    [Tooltip("Maximum provinces whose slow holding economy is evaluated in one campaign tick. Keeping this small prevents periodic frame spikes.")]
    [Range(1, 16)] public int HoldingEvolutionProvinceBudget = 3;
    private int holdingEvolutionCursor;
    private bool hadActiveCampaignQueues;
    private readonly Dictionary<string, List<ProvinceLevyEntitlement>> levyMobilizationQueues =
        new Dictionary<string, List<ProvinceLevyEntitlement>>();
    private const int LevyMobilizationBatchSize = 3;
    private const int LevyMobilizationBatchTicks = 3;
    public int xxx = 25;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.HasAssignment)
        {
            SessionManager.Instance.ApplyNetworkFaction(CampaignNetworkPlayer.Local.AssignedNation);
        }

        LoadCulturesFromNationData();

        this.transform.GetComponent<LoadProvinces>().LoadStuff();
        nationdict = new Dictionary<string, Nation>();
        foreach (Nation nation in nationlist)
        {
            nationdict.Add(nation.name, nation);
            nation.EnsureDefaultLaws();
            nation.IsPlayer = false;
            nation.faction = nation.faction.Init();
            //Debug.LogError(nation.name);
            nation.faction.Set();
            nation.nationalbrainy = ScriptableObject.CreateInstance<NationalBrain>();
            nation.nationalbrainy.nation = nation.name;
            nation.nationalbrainy.name = nation.name + "_brain";
            

            nation.faction.color = nation.ownerIdentity;

            if (SessionManager.Instance.HostFaction.name.Contains(nation.name))
            {
                nation.IsPlayer = true;
                nation.faction = SessionManager.Instance.HostFaction;
            }
        }
        DiplomacySystem.EnsureDefaultPeace();

        provincedict = new Dictionary<string, Province>();
        provincedictcolor = new Dictionary<Color32, Province>();
        foreach (Province province in provincelist)
        {
            province.CreateGarrison();
            province.SetAdjacents();
            try
            {
                provincedict.Add(province.name, province);
                provincedictcolor.Add(new Color32(province.identity.r, province.identity.g, province.identity.b, 0), province);
            }
            catch
            {
                //Debug.LogError(province.name);
            }
            province.OriginalNation = province.nation;
            province.InitializeRecruitment();
        }
        foreach (Province province in provincelist)
        {
            province.InitializeHoldings();
            province.InitializeUrbanizationAtTarget();
        }
        Mapshower.Instance.Paint();

        if (RecruitmentMenu.Instance == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null) RecruitmentMenu.Create(canvas);
        }

        PlantCities();

        foreach (Nation nation in nationlist)
        {
            nation.nationalbrainy.Startie();
        }

        StartCoroutine(EnsureFactionStarterArmies());

        if (GetComponent<CampaignPersistence>() == null)
        {
            gameObject.AddComponent<CampaignPersistence>();
        }
        if (GetComponent<DeterministicBattleManager>() == null)
        {
            gameObject.AddComponent<DeterministicBattleManager>();
        }
        if (GetComponent<ProjectX.TileBattle.TileBattleCampaignManager>() == null)
        {
            gameObject.AddComponent<ProjectX.TileBattle.TileBattleCampaignManager>();
        }
    }
    public void PlantCities()
    {
        foreach (var province in provincelist)
        {
            var a = Instantiate(CityObject, this.transform.GetChild(0).GetChild(1).GetChild(1));
            a.transform.localScale = new Vector3(25f, 25f, 25f);
            a.transform.position = new Vector3(province.position.x * 1f - 512f, province.position.y * 1f - 331f, 0);
        }
    }
    public Nation CallPlayer()
    {
        foreach (Nation Nation in Owners.Instance.nationlist)
        {
            if (Nation.IsPlayer == true)
            {
                Nation nation = Nation;
                return Nation;
            }
        }
        return new Nation();
    }
    public Nation CallNation(string nationname)
    {
        return nationdict[nationname];
    }
    public Province CallProvinceByString(string provincename)
    {
        return provincedict[provincename];
    }
    public CampaignRegion CallRegionByString(string regionname)
    {
        if (string.IsNullOrWhiteSpace(regionname) || regiondict == null) return null;
        regiondict.TryGetValue(regionname, out CampaignRegion region);
        return region;
    }
    public Province CallProvinceByColor(Color32 provincecolor)
    {
        if (provincecolor.r == 0 && provincecolor.g == 0 & provincecolor.b == 0)
        {
            return null;
        }
        return provincedictcolor[provincecolor];
    }
    public Culture CallCultureByName(string culturename)
    {
        if (string.IsNullOrWhiteSpace(culturename) || culturedict == null) return null;
        culturedict.TryGetValue(culturename, out Culture culture);
        return culture;
    }

    private void LoadCulturesFromNationData()
    {
        culturelist = new List<Culture>();
        culturedict = new Dictionary<string, Culture>(System.StringComparer.OrdinalIgnoreCase);
        NationCultureData[] definitions = Resources.LoadAll<NationCultureData>("Prefabs/NationData/Culture");
        System.Array.Sort(definitions, (left, right) => string.CompareOrdinal(left != null ? left.DisplayName : string.Empty,
            right != null ? right.DisplayName : string.Empty));
        foreach (NationCultureData definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.DisplayName)) continue;
            Color32 color = definition.color; color.a = 255;
            Culture culture = new Culture { name = definition.DisplayName, ownerIdentity = color };
            culturelist.Add(culture);
            culturedict[definition.DisplayName] = culture;
            // The asset name remains a compatibility alias for older saves.
            culturedict[definition.name] = culture;
        }
    }

    public NationCultureData CultureDefinition(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName)) return null;
        return System.Array.Find(Resources.LoadAll<NationCultureData>("Prefabs/NationData/Culture"),
            definition => definition != null && definition.Matches(cultureName));
    }

    public Color32 CultureColor(string cultureName, Color32 fallback)
    {
        Culture culture = CallCultureByName(cultureName);
        if (culture != null) { Color32 configured = culture.ownerIdentity; configured.a = 255; return configured; }
        fallback.a = 255;
        return fallback;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (CampaignPaused) return;
        campaignStepAccumulator += Mathf.Max(0.01f, CampaignSimulationSpeed);
        int steps = 0;
        while (campaignStepAccumulator >= 1f && steps++ < 8)
        {
            campaignStepAccumulator -= 1f;
            RunCampaignStep();
        }
    }

    private IEnumerator EnsureFactionStarterArmies()
    {
        // Allow scene-placed armies to finish Start() and register first.
        yield return null;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
            yield break;

        foreach (Nation nation in nationlist)
        {
            if (nation == null) continue;
            nation.armies.RemoveAll(existingArmy => existingArmy == null);
            bool braindead = NationContentResolver.HasFlag(nation, "Braindead");
            if (braindead)
            {
                // Braindead nations begin without either generated or scene-placed armies.
                foreach (FieldArmyHolder passiveArmy in new List<FieldArmyHolder>(nation.armies))
                    if (passiveArmy != null) Destroy(passiveArmy.gameObject);
                nation.armies.Clear();
                continue;
            }
            if (nation.armies.Count > 0) continue;
            Province start = provincelist.Find(province => province != null && province.nation == nation);
            if (start == null) continue;

            FieldArmyHolder starterArmy = Mapshower.Instance.SpawnArmy(start, "1st Army of " + nation.name);
            if (starterArmy == null) continue;
            starterArmy.PreserveConfiguredRoster = false;
            starterArmy.ConfigureNetworkIdentity("starter_" + nation.name, ulong.MaxValue, false, nation);
        }
    }

    private void RunCampaignStep()
    {
        bool battleRunning = DeterministicBattleManager.Instance != null && DeterministicBattleManager.Instance.ActiveBattles.Count > 0 ||
            ProjectX.TileBattle.TileBattleCampaignManager.Instance != null &&
            ProjectX.TileBattle.TileBattleCampaignManager.Instance.ActiveBattles.Count > 0;
        bool playerMoving = FieldArmyHolder.PlayerFieldArmy != null && !FieldArmyHolder.PlayerFieldArmy.IsTargetNull();
        if (1 == 1)//playerMoving || battleRunning || Input.GetKey("space"))
        {
            foreach (var item in armylist)
            {
                item.Act();
                if (timer % 50 == 0) //50
                {
                    //Recruitment etc
                    item.NextTurn();
                }
            }
            if (timer % 100 == 0)//1000 == 0) //if (timer % 250 == 0)
            {
                //TakeTurns();
            }
            if (timer % 10 == 0)
            {
                TakeTurns();
            }
            timer++;
            foreach(var nation in nationlist)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    nation.IsPlayer = CampaignNetworkPlayer.IsNationPlayerControlled(nation.name);
                if (!nation.IsPlayer) nation.nationalbrainy.Think();
            }
        }
    }
    public void TakeTurns()
    {
        long totalStamp = CampaignPerformanceTrace.Stamp();
        turncounter++;
        foreach (FieldArmyHolder army in armylist)
            if (army != null && army.fieldArmy != null && army.IsTargetNull()) army.fieldArmy.ProcessRecruitmentTick();
        ProcessLevyMobilizationQueues();
        double recruitmentMs = CampaignPerformanceTrace.MillisecondsSince(totalStamp);
        long phaseStamp = CampaignPerformanceTrace.Stamp();
        foreach (CampaignRegion region in regionlist)
            if (region != null) region.ProcessLoyaltyTurn();
        double regionsMs = CampaignPerformanceTrace.MillisecondsSince(phaseStamp);
        phaseStamp = CampaignPerformanceTrace.Stamp();
        foreach (var nation in nationlist)
        {
            nation.TakeTurn();
        }
        double nationsMs = CampaignPerformanceTrace.MillisecondsSince(phaseStamp);
        phaseStamp = CampaignPerformanceTrace.Stamp();
        foreach (var province in provincelist)
        {
            if (ProvinceMercenaryPool.Enabled) province.RegenerateMercenaries();
            province.ProcessConstructionTick();
            province.ProcessHoldingConstructionTick();
            province.ProcessLevyTick();
        }
        double provincesMs = CampaignPerformanceTrace.MillisecondsSince(phaseStamp);
        phaseStamp = CampaignPerformanceTrace.Stamp();
        ProcessHoldingEvolutionBudget();
        double evolutionMs = CampaignPerformanceTrace.MillisecondsSince(phaseStamp);
        phaseStamp = CampaignPerformanceTrace.Stamp();
        bool hasActiveQueues = HasActiveCampaignQueues();
        if (CampaignNetworkPlayer.Local != null && (hasActiveQueues || hadActiveCampaignQueues))
        {
            CampaignNetworkPlayer.Local.BroadcastQueueStateNow();
            CampaignNetworkPlayer.Local.BroadcastLevyQueueStateNow();
        }
        hadActiveCampaignQueues = hasActiveQueues;
        double networkQueuesMs = CampaignPerformanceTrace.MillisecondsSince(phaseStamp);
        double totalMs = CampaignPerformanceTrace.MillisecondsSince(totalStamp);
        if (totalMs >= 4.0)
            CampaignPerformanceTrace.Report("EconomyTick", totalMs,
                "recruit=" + recruitmentMs.ToString("0.00") +
                " regions=" + regionsMs.ToString("0.00") +
                " nations=" + nationsMs.ToString("0.00") +
                " provinces=" + provincesMs.ToString("0.00") +
                " evolution=" + evolutionMs.ToString("0.00") +
                " queues=" + networkQueuesMs.ToString("0.00") +
                " counts[n=" + nationlist.Count + ",r=" + regionlist.Count + ",p=" + provincelist.Count + "]");
    }

    private bool HasActiveCampaignQueues()
    {
        foreach (FieldArmyHolder army in armylist)
            if (army != null && army.fieldArmy != null && army.fieldArmy.recruitmentOrders != null &&
                army.fieldArmy.recruitmentOrders.Count > 0) return true;
        foreach (Province province in provincelist)
            if (province != null && province.levyEntitlements != null &&
                province.levyEntitlements.Exists(entitlement => entitlement != null &&
                    entitlement.state == LevyEntitlementState.Mobilizing)) return true;
        foreach (Province province in provincelist)
            if (province != null && (province.constructionOrders != null && province.constructionOrders.Count > 0 ||
                province.holdingConstructionOrders != null && province.holdingConstructionOrders.Count > 0)) return true;
        return false;
    }

    private void ProcessLevyMobilizationQueues()
    {
        foreach (List<ProvinceLevyEntitlement> queue in levyMobilizationQueues.Values) queue.Clear();
        foreach (Province province in provincelist)
        {
            if (province == null || province.levyEntitlements == null) continue;
            foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
            {
                if (entitlement == null || entitlement.state != LevyEntitlementState.Mobilizing ||
                    string.IsNullOrEmpty(entitlement.raisedArmyId)) continue;
                if (!levyMobilizationQueues.TryGetValue(entitlement.raisedArmyId, out List<ProvinceLevyEntitlement> queue))
                {
                    queue = new List<ProvinceLevyEntitlement>();
                    levyMobilizationQueues.Add(entitlement.raisedArmyId, queue);
                }
                queue.Add(entitlement);
            }
        }

        foreach (KeyValuePair<string, List<ProvinceLevyEntitlement>> entry in levyMobilizationQueues)
        {
            List<ProvinceLevyEntitlement> queue = entry.Value;
            if (queue.Count == 0) continue;
            FieldArmyHolder target = armylist.Find(candidate => candidate != null && candidate.NetworkArmyId == entry.Key &&
                candidate.fieldArmy != null);
            if (target == null)
            {
                foreach (ProvinceLevyEntitlement entitlement in queue)
                { entitlement.state = LevyEntitlementState.Available; entitlement.remainingTicks = 0; entitlement.raisedArmyId = null; }
                continue;
            }
            if (!target.IsTargetNull())
            {
                RecruitmentMenu.RefreshQueueFor(target.fieldArmy);
                continue;
            }

            int active = 0;
            foreach (ProvinceLevyEntitlement entitlement in queue) if (entitlement.remainingTicks > 0) active++;
            if (active > LevyMobilizationBatchSize)
            {
                int keptActive = 0;
                foreach (ProvinceLevyEntitlement entitlement in queue)
                    if (entitlement.remainingTicks > 0 && ++keptActive > LevyMobilizationBatchSize)
                        entitlement.remainingTicks = 0;
                active = LevyMobilizationBatchSize;
            }
            if (active == 0)
            {
                int start = Mathf.Min(LevyMobilizationBatchSize, queue.Count);
                for (int i = 0; i < start; i++) queue[i].remainingTicks = LevyMobilizationBatchTicks;
            }

            for (int i = 0; i < queue.Count; i++)
            {
                ProvinceLevyEntitlement entitlement = queue[i];
                if (entitlement.remainingTicks <= 0) continue;
                entitlement.remainingTicks--;
                if (entitlement.remainingTicks > 0) continue;
                if (!entitlement.eligible || target.fieldArmy.GrabArmySize() >= target.fieldArmy.MaxArmySize)
                {
                    entitlement.state = LevyEntitlementState.Available;
                    entitlement.raisedArmyId = null;
                    continue;
                }
                target.fieldArmy.AddTroop(entitlement.unit, 1, true, CampaignUnitOrigin.Levy, entitlement.id);
                CampaignRecruitmentVisual.Present(entitlement.unit, target);
                entitlement.state = LevyEntitlementState.Raised;
            }
            RecruitmentMenu.RefreshQueueFor(target.fieldArmy);
        }
    }

    private void ProcessHoldingEvolutionBudget()
    {
        if (provincelist == null || provincelist.Count == 0) return;
        int budget = Mathf.Min(Mathf.Max(1, HoldingEvolutionProvinceBudget), provincelist.Count);
        for (int processed = 0; processed < budget; processed++)
        {
            if (holdingEvolutionCursor >= provincelist.Count) holdingEvolutionCursor = 0;
            Province province = provincelist[holdingEvolutionCursor++];
            if (province != null) province.ProcessHoldingEvolutionTick(turncounter);
        }
    }
}
public enum CampaignTerrainProfile : byte
{
    Auto, Plains, Forested, Hilly, Mountainous, Marshland, RiverValley, RoughCountry, Coastal
}

[System.Serializable]
public class Province
{
    public string name;
    public Color32 identity;
    public Nation nation;
    public Nation OriginalNation;
    [Tooltip("Foreign nation currently controlling this province militarily. Legal ownership remains in nation.")]
    public Nation OccupyingNation;
    public bool IsOccupied => OccupyingNation != null && OccupyingNation != nation;
    public Nation ControllerNation => IsOccupied ? OccupyingNation : nation;
    public Color32 OwnershipMapColor => DiplomacySystem.MapColor(ControllerNation);
    public string state;
    public string region;
    [System.NonSerialized] public bool regionConfiguredFromData;
    public Vector2 position;
    public CampaignTerrainProfile terrainProfile = CampaignTerrainProfile.Auto;
    public int population = 1000;
    public int supply = 1000;
    [Range(-100, 100)] public int urbanization;
    [Min(0)] public int baseMaximumDevelopment = 100;
    public List<ProvinceNamedModifier> uniqueModifiers = new List<ProvinceNamedModifier>();
    public int MaxDevelopmentModifier
    {
        get
        {
            ProvinceLocalModifiers total = GetLocalModifiers();
            return total != null ? total.maxDevelopment : 0;
        }
    }
    public int MaximumDevelopment => Mathf.Max(0, baseMaximumDevelopment + MaxDevelopmentModifier);
    public FieldArmy garrison;

    public List<string> AdjacentProvinces = new List<string>();

    public List<Culture> cultures;
    public Culture PrimaryCulture => cultures != null && cultures.Count > 0 ? cultures[0] : null;
    public float GetCulturePercentage(string cultureName)
    {
        if (string.IsNullOrEmpty(cultureName) || cultures == null || population <= 0) return 0f;
        int culturalPopulation = 0;
        foreach (Culture culture in cultures)
            if (culture != null && string.Equals(culture.name, cultureName, System.StringComparison.OrdinalIgnoreCase))
                culturalPopulation += Mathf.Max(0, culture.population);
        return culturalPopulation * 100f / population;
    }
    public int taxincome;
    public int taxpercentage;
    public int levyincome;
    public int levypercentage;
    public int unrest;
    public List<ProvinceBuilding> buildings = new List<ProvinceBuilding>();
    public List<ProvinceConstructionOrder> constructionOrders = new List<ProvinceConstructionOrder>();
    public List<ProvinceMercenaryPool> mercenaryPools = new List<ProvinceMercenaryPool>();
    public List<ProvinceLevyEntitlement> levyEntitlements = new List<ProvinceLevyEntitlement>();
    [System.NonSerialized] private int nextLevyReconcileTick = -1;
    [System.NonSerialized] private string levyAllInArmedArmyId;
    private sealed class HoldingRecoveryCacheEntry
    {
        public int frame;
        public readonly Dictionary<string, int> ticks = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public readonly HashSet<string> mobilized = new HashSet<string>(System.StringComparer.Ordinal);
    }
    private static readonly Dictionary<string, HoldingRecoveryCacheEntry> HoldingRecoveryCache =
        new Dictionary<string, HoldingRecoveryCacheEntry>(System.StringComparer.Ordinal);
    public List<ProvinceHolding> holdings = new List<ProvinceHolding>();
    public List<HoldingConstructionOrder> holdingConstructionOrders = new List<HoldingConstructionOrder>();
    [Header("Holding composition")]
    public List<HoldingTagModifier> baseHoldingTagDesires = new List<HoldingTagModifier>();
    public HoldingEvolutionSettings holdingEvolution = new HoldingEvolutionSettings();
    [System.NonSerialized] public int holdingEvolutionCursor;
    [System.NonSerialized] public int lastHoldingEvolutionTick = -1;
    [System.NonSerialized] public int lastUrbanizationEvolutionTick = -1;
    [System.NonSerialized] private int holdingEfficiencyCacheTurn = int.MinValue;
    [System.NonSerialized] private Dictionary<HoldingDefinition, float> holdingEfficiencyCache;

    public void ApplyConquestDevastation(Nation previousOwner, Nation conqueror, int campaignTick)
    {
        if (buildings == null) buildings = new List<ProvinceBuilding>();
        else buildings.RemoveAll(building => building != null &&
            (building.DefinitionGoldUpkeep > 0 || building.DefinitionFoodConsumption > 0 ||
                ConquestBuildingDestroyed(building, previousOwner, conqueror, campaignTick)));
        if (constructionOrders == null) constructionOrders = new List<ProvinceConstructionOrder>();
        else constructionOrders.Clear();

        urbanization = Mathf.Clamp(urbanization - 25, -100, MaximumDevelopment);

        if (holdings == null || holdings.Count == 0) return;
        List<ProvinceHolding> downgradeable = holdings.FindAll(holding => holding != null && holding.definition != null &&
            holding.definition.categoryTier > 1 && HoldingEvolutionSystem.FindCategoryTier(
                holding.definition.category, holding.definition.categoryTier - 1) != null);
        downgradeable.Sort((left, right) => ConquestHoldingHash(left, previousOwner, conqueror, campaignTick)
            .CompareTo(ConquestHoldingHash(right, previousOwner, conqueror, campaignTick)));
        int downgradeCount = Mathf.Min(downgradeable.Count, holdings.Count / 2);
        for (int i = 0; i < downgradeCount; i++)
        {
            ProvinceHolding holding = downgradeable[i];
            HoldingDefinition lower = HoldingEvolutionSystem.FindCategoryTier(holding.definition.category,
                holding.definition.categoryTier - 1);
            if (lower == null) continue;
            holding.definition = lower; holding.id = lower.StableId;
            holding.level = Mathf.Clamp(holding.level, 1, Mathf.Max(1, lower.maximumLevel));
            holding.adaptationTargetId = string.Empty; holding.adaptationPressure = 0;
            holding.adaptationCooldownTicks = Mathf.Max(holding.adaptationCooldownTicks, 8);
        }
        ClampDevelopment(); ReconcileLevyEntitlements();
    }

    public void ApplyOccupationDevastation(Nation defender, Nation occupier, int campaignTick)
    {
        if (buildings == null) buildings = new List<ProvinceBuilding>();
        else buildings.RemoveAll(building => building != null &&
            ConquestBuildingDestroyed(building, defender, occupier, campaignTick));
        if (constructionOrders == null) constructionOrders = new List<ProvinceConstructionOrder>();
        else constructionOrders.Clear();
        urbanization = Mathf.Clamp(urbanization - 25, -100, MaximumDevelopment);

        if (holdings != null && holdings.Count > 0)
        {
            List<ProvinceHolding> downgradeable = holdings.FindAll(holding => holding != null &&
                holding.definition != null && holding.definition.categoryTier > 1 &&
                HoldingEvolutionSystem.FindCategoryTier(holding.definition.category,
                    holding.definition.categoryTier - 1) != null);
            downgradeable.Sort((left, right) => ConquestHoldingHash(left, defender, occupier, campaignTick)
                .CompareTo(ConquestHoldingHash(right, defender, occupier, campaignTick)));
            int count = Mathf.Min(downgradeable.Count, holdings.Count / 2);
            for (int i = 0; i < count; i++)
            {
                HoldingDefinition lower = HoldingEvolutionSystem.FindCategoryTier(
                    downgradeable[i].definition.category, downgradeable[i].definition.categoryTier - 1);
                if (lower == null) continue;
                downgradeable[i].definition = lower;
                downgradeable[i].id = lower.StableId;
                downgradeable[i].level = 1;
                downgradeable[i].adaptationTargetId = string.Empty;
                downgradeable[i].adaptationPressure = 0;
            }
        }
        ClampDevelopment();
        ReconcileLevyEntitlements();
    }

    private bool ConquestBuildingDestroyed(ProvinceBuilding building, Nation previousOwner, Nation conqueror,
        int campaignTick)
    {
        if (building == null) return false;
        unchecked
        {
            int hash = 17;
            string value = name + "|" + building.BuildingId + "|" + building.slotIndex + "|" +
                (previousOwner != null ? previousOwner.name : string.Empty) + "|" +
                (conqueror != null ? conqueror.name : string.Empty) + "|" + campaignTick;
            for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return (hash & int.MaxValue) % 4 == 0;
        }
    }

    private int ConquestHoldingHash(ProvinceHolding holding, Nation previousOwner, Nation conqueror, int campaignTick)
    {
        unchecked
        {
            int hash = 17;
            string value = name + "|" + (holding != null ? holding.instanceId : string.Empty) + "|" +
                (previousOwner != null ? previousOwner.name : string.Empty) + "|" +
                (conqueror != null ? conqueror.name : string.Empty) + "|" + campaignTick;
            for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash & int.MaxValue;
        }
    }

    public void InitializeRecruitment()
    {
        urbanization = Mathf.Clamp(urbanization, -100, MaximumDevelopment);
        if (buildings == null) buildings = new List<ProvinceBuilding>();
        if (constructionOrders == null) constructionOrders = new List<ProvinceConstructionOrder>();
        if (mercenaryPools == null) mercenaryPools = new List<ProvinceMercenaryPool>();
        if (levyEntitlements == null) levyEntitlements = new List<ProvinceLevyEntitlement>();
        if (holdings == null) holdings = new List<ProvinceHolding>();
        if (holdingConstructionOrders == null) holdingConstructionOrders = new List<HoldingConstructionOrder>();
        EnsureStartingCultureBuildings();
        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] == null) continue;
            if (buildings[i].slotIndex < 0) buildings[i].slotIndex = i;
            buildings[i].maxLevel = buildings[i].definition != null
                ? buildings[i].definition.maximumLevel
                : Mathf.Max(buildings[i].maxLevel, ProvinceBuilding.MaximumLevelFor(buildings[i].id));
        }
        if (mercenaryPools.Count > 0 || OriginalNation == null || OriginalNation.faction == null) return;

        List<UnitSaveData> candidates = new List<UnitSaveData>();
        candidates.AddRange(OriginalNation.faction.MercenaryDataList);
        foreach (UnitSaveData unit in OriginalNation.faction.UnitDataList)
        {
            if (unit != null && unit.Mercenary && !candidates.Contains(unit)) candidates.Add(unit);
        }
        if (candidates.Count == 0 && OriginalNation.faction.UnitDataList.Count > 0)
        {
            candidates.Add(OriginalNation.faction.UnitDataList[0]);
        }
        foreach (UnitSaveData unit in candidates)
        {
            if (unit == null) continue;
            mercenaryPools.Add(new ProvinceMercenaryPool
            {
                unit = unit,
                available = 2,
                capacity = 3,
                regenerationPerTurn = 0.25f
            });
        }
    }
    public void EnsureCulture()
    {
        if (cultures == null) cultures = new List<Culture>();
        cultures.RemoveAll(culture => culture == null);
        if (cultures.Count > 0) return;
        string cultureName = nation != null && nation.culture != null
            ? nation.culture.DisplayName
            : nation != null ? nation.name : "Unassigned";
        cultures.Add(new Culture
        {
            name = cultureName,
            ownerIdentity = nation != null ? nation.ownerIdentity : new Color32(128, 128, 128, 255),
            population = Mathf.Max(1, population)
        });
    }
    public void InitializeHoldings()
    {
        if (holdings == null) holdings = new List<ProvinceHolding>();
        holdings.RemoveAll(holding => holding == null);
        if (holdings.Count == 0)
        {
            List<string> cultureNames = new List<string>();
            EnsureCulture();
            if (PrimaryCulture != null && !string.IsNullOrWhiteSpace(PrimaryCulture.name)) cultureNames.Add(PrimaryCulture.name);
            if (Owners.Instance != null)
            {
                foreach (string adjacentName in AdjacentProvinces)
                {
                    Province adjacent = Owners.Instance.provincelist.Find(candidate => candidate != null && candidate.name == adjacentName);
                    string adjacentCulture = adjacent != null && adjacent.PrimaryCulture != null ? adjacent.PrimaryCulture.name : null;
                    if (!string.IsNullOrWhiteSpace(adjacentCulture) && !cultureNames.Exists(value =>
                        string.Equals(value, adjacentCulture, System.StringComparison.OrdinalIgnoreCase))) cultureNames.Add(adjacentCulture);
                    if (cultureNames.Count >= 3) break;
                }
                if (cultureNames.Count < 3)
                    foreach (Province candidate in Owners.Instance.provincelist)
                    {
                        string candidateCulture = candidate != null && candidate.PrimaryCulture != null ? candidate.PrimaryCulture.name : null;
                        if (!string.IsNullOrWhiteSpace(candidateCulture) && !cultureNames.Exists(value =>
                            string.Equals(value, candidateCulture, System.StringComparison.OrdinalIgnoreCase))) cultureNames.Add(candidateCulture);
                        if (cultureNames.Count >= 3) break;
                    }
            }
            while (cultureNames.Count < 3) cultureNames.Add(cultureNames.Count > 0 ? cultureNames[0] : "Unassigned");
            LevyGrantRule migrationRule = LevySystem.ResolveRules(nation).Find(rule => rule != null && rule.unit != null &&
                buildings.Exists(building => rule.Applies(this, building)));
            HoldingDefinition citizenFarm = migrationRule != null
                ? HoldingDefinition.DefaultCitizenFarm(migrationRule.unit) : HoldingDefinition.DefaultCitizenFarm();
            bool barbarian = nation != null && nation.civilization != null &&
                string.Equals(nation.civilization.name, "Barbarian", System.StringComparison.OrdinalIgnoreCase);
            int initialHoldingCount = InitialHoldingCount(barbarian);
            for (int i = 0; i < initialHoldingCount; i++)
            {
                HoldingDefinition definition = FallbackHoldingDefinition(i, barbarian, citizenFarm);
                if (definition == null) definition = citizenFarm;
                SocioEconomicClass socialClass = FallbackHoldingClass(i, barbarian, definition);
                holdings.Add(new ProvinceHolding { instanceId = name + "-holding-" + i,
                    definition = definition, id = definition.StableId, level = 1, slotIndex = i,
                    cultureName = i <= 5 || i == 8 ? cultureNames[0] : i <= 7 ? cultureNames[1] : cultureNames[2],
                    socioEconomicClass = socialClass,
                    allegiance = definition != null ? definition.suggestedAllegiance : string.Empty });
            }
        }
        for (int i = 0; i < holdings.Count; i++)
        {
            ProvinceHolding holding = holdings[i];
            if (holding.slotIndex < 0) holding.slotIndex = i;
            if (string.IsNullOrWhiteSpace(holding.instanceId)) holding.instanceId = name + "-holding-" + holding.slotIndex;
            if (holding.definition == null) holding.definition = HoldingDefinition.Find(holding.id);
            holding.socioEconomicClass = SocioEconomicClassRules.Normalize(holding.socioEconomicClass);
            nation?.ApplyHoldingClassLaws(holding);
        }
        RebuildPopulationFromHoldings();
        ReconcileLevyEntitlements();
    }

    private int InitialHoldingCount(bool barbarian)
    {
        string culture = PrimaryCulture != null ? PrimaryCulture.name :
            nation != null && nation.culture != null ? nation.culture.DisplayName : string.Empty;
        if (culture.Equals("Germanic", System.StringComparison.OrdinalIgnoreCase)) return 5;
        if (culture.Equals("Celtiberian", System.StringComparison.OrdinalIgnoreCase)) return 10;
        if (culture.Equals("Latin", System.StringComparison.OrdinalIgnoreCase) ||
            culture.Equals("Punic", System.StringComparison.OrdinalIgnoreCase)) return 15;
        return barbarian ? 5 : 10;
    }

    public void InitializeUrbanizationAtTarget()
    {
        urbanization = HoldingEvolutionSystem.DesiredUrbanization(this);
        ClampDevelopment();
    }

    private HoldingDefinition FallbackHoldingDefinition(int slot, bool barbarian, HoldingDefinition citizenFarm)
    {
        if (barbarian)
        {
            // Barbarian provinces begin as a varied rural economy instead of a
            // mostly homogeneous block of tier-one subsistence holdings. Every
            // province receives all three barbarian holding lines and examples
            // from each tier; the additional tenth holding favours subsistence.
            switch (slot)
            {
                case 0: return HoldingDefinition.Find("Homestead");
                case 1: return HoldingDefinition.Find("Homestead");
                case 2: return HoldingDefinition.Find("Hamlet");
                case 3: return HoldingDefinition.Find("Village");
                case 4: return HoldingDefinition.Find("HerdingHomestead");
                case 5: return HoldingDefinition.Find("PastoralHolding");
                case 6: return HoldingDefinition.Find("PastoralEstate");
                case 7: return HoldingDefinition.Find("HunterCamp");
                case 8: return HoldingDefinition.Find("HunterCommunity");
                default: return HoldingDefinition.Find("HunterLodge");
            }
        }
        if (slot <= 3) return citizenFarm;
        if (slot <= 6) return HoldingDefinition.Find("TenantFarm");
        if (slot == 7) return TerrainSpecialistHolding();
        if (slot == 8) return HoldingDefinition.Find("Manor");
        return HoldingDefinition.Find("SlaveFarm");
    }

    private HoldingDefinition TerrainSpecialistHolding()
    {
        if (terrainProfile == CampaignTerrainProfile.Forested) return HoldingDefinition.Find("HunterCamp");
        if (terrainProfile == CampaignTerrainProfile.Hilly || terrainProfile == CampaignTerrainProfile.Mountainous)
            return HoldingDefinition.Find("SurfaceWorking");
        if (terrainProfile == CampaignTerrainProfile.RoughCountry) return HoldingDefinition.Find("HerdingHomestead");
        return HoldingDefinition.Find("TraderHousehold");
    }

    private static SocioEconomicClass FallbackHoldingClass(int slot, bool barbarian, HoldingDefinition definition)
    {
        if (!barbarian && slot == 8) return SocioEconomicClass.Aristocracy;
        if (!barbarian && slot == 9) return SocioEconomicClass.Enslaved;
        return definition != null ? definition.defaultClass : SocioEconomicClass.Freemen;
    }

    public void RebuildPopulationFromHoldings()
    {
        population = holdings != null ? holdings.FindAll(holding => holding != null).Count : 0;
        Dictionary<string, int> totals = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
        {
            if (holding == null) continue;
            string holdingCulture = !string.IsNullOrWhiteSpace(holding.cultureName) ? holding.cultureName : "Unassigned";
            totals[holdingCulture] = totals.TryGetValue(holdingCulture, out int count) ? count + 1 : 1;
        }
        cultures = new List<Culture>();
        foreach (KeyValuePair<string, int> pair in totals)
        {
            Culture source = Owners.Instance != null ? Owners.Instance.CallCultureByName(pair.Key) : null;
            cultures.Add(new Culture { name = pair.Key, population = pair.Value,
                ownerIdentity = source != null ? source.ownerIdentity : nation != null ? nation.ownerIdentity : identity });
        }
        cultures.Sort((left, right) => right.population.CompareTo(left.population));
    }

    public Dictionary<SocioEconomicClass, int> GetSocioEconomicComposition()
    {
        Dictionary<SocioEconomicClass, int> result = new Dictionary<SocioEconomicClass, int>();
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
        {
            if (holding == null) continue;
            SocioEconomicClass socialClass = SocioEconomicClassRules.Normalize(holding.socioEconomicClass);
            result[socialClass] = result.TryGetValue(socialClass, out int count) ? count + 1 : 1;
        }
        return result;
    }

    public ProvinceLocalModifiers GetLocalModifiers()
    {
        ProvinceLocalModifiers result = new ProvinceLocalModifiers();
        if (buildings != null) foreach (ProvinceBuilding building in buildings)
        {
            if (building == null || building.definition == null || building.definition.levels == null) continue;
            foreach (BuildingLevelDefinition level in building.definition.levels)
                if (level != null && level.level <= building.level) result.Add(level.localModifiers);
        }
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
        {
            if (holding == null || holding.definition == null || holding.definition.levels == null) continue;
            foreach (HoldingLevelDefinition level in holding.definition.levels)
                if (level != null && level.level <= holding.level) result.Add(level.localModifiers);
        }
        if (uniqueModifiers != null) foreach (ProvinceNamedModifier modifier in uniqueModifiers)
            if (modifier != null) result.Add(modifier.localModifiers);
        return result;
    }

    public void ClampDevelopment()
    {
        urbanization = Mathf.Clamp(urbanization, -100, MaximumDevelopment);
        holdingEfficiencyCacheTurn = int.MinValue;
    }

    public ProvinceHolding AddHoldingPopulation(HoldingDefinition definition, string cultureName)
    {
        if (definition == null) return null;
        if (holdings == null) holdings = new List<ProvinceHolding>();
        int slot = 0; while (GetHoldingInSlot(slot) != null) slot++;
        ProvinceHolding holding = new ProvinceHolding { instanceId = name + "-holding-" + System.Guid.NewGuid().ToString("N"),
            definition = definition, id = definition.StableId, level = 1, slotIndex = slot,
            cultureName = !string.IsNullOrWhiteSpace(cultureName) ? cultureName : PrimaryCulture != null ? PrimaryCulture.name : "Unassigned",
            socioEconomicClass = SocioEconomicClassRules.Normalize(definition.defaultClass) };
        nation?.ApplyHoldingClassLaws(holding);
        holdings.Add(holding); RebuildPopulationFromHoldings(); ReconcileLevyEntitlements(); return holding;
    }

    public bool RemoveHoldingPopulation(string instanceId)
    {
        ProvinceHolding holding = GetHolding(instanceId);
        if (holding == null || IsHoldingMobilized(instanceId)) return false;
        holdings.Remove(holding); RebuildPopulationFromHoldings(); ReconcileLevyEntitlements(); return true;
    }

    private sealed class LevyUnitPool
    {
        public UnitSaveData unit;
        public long accumulated;
        public long remainder;
        public Province remainderProvince;
        public ProvinceHolding remainderHolding;
        public readonly List<string> pendingContributors = new List<string>();
    }

    public void ReconcileLevyEntitlements()
    {
        if (levyEntitlements == null) levyEntitlements = new List<ProvinceLevyEntitlement>();
        foreach (ProvinceLevyEntitlement entitlement in levyEntitlements) if (entitlement != null) entitlement.eligible = false;
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        if (nation == null) return;
        bool callupsAllowed = campaignRegion == null || campaignRegion.AllowsLevyCallups(nation);

        // Fractional holding contributions are pooled across the occupied part of the region.
        // Integer arithmetic keeps entitlement allocation deterministic in multiplayer.
        List<Province> sourceProvinces = GetOccupiedRegionProvinces(nation);
        sourceProvinces.Sort((left, right) => string.CompareOrdinal(left != null ? left.name : string.Empty,
            right != null ? right.name : string.Empty));
        const long completeFormation = 1000000L;
        Dictionary<string, LevyUnitPool> unitPools = new Dictionary<string, LevyUnitPool>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Province sourceProvince in sourceProvinces)
        {
            if (sourceProvince == null || sourceProvince.holdings == null) continue;
            List<ProvinceHolding> sources = sourceProvince.holdings.FindAll(item => item != null && item.CanRaiseLevies);
            sources.Sort((left, right) =>
            {
                int bySlot = left.slotIndex.CompareTo(right.slotIndex);
                return bySlot != 0 ? bySlot : string.CompareOrdinal(left.instanceId, right.instanceId);
            });
            foreach (ProvinceHolding holding in sources)
            {
                UnitSaveData resolvedLevy = HoldingEvolutionSystem.ResolveLevyUnit(sourceProvince, holding);
                if (resolvedLevy == null) continue;
                string poolId = !string.IsNullOrWhiteSpace(resolvedLevy.name) ? resolvedLevy.name : resolvedLevy.unitname;
                if (!unitPools.TryGetValue(poolId, out LevyUnitPool pool))
                { pool = new LevyUnitPool { unit = resolvedLevy }; unitPools.Add(poolId, pool); }
                long before = pool.accumulated / completeFormation;
                if (!pool.pendingContributors.Contains(holding.instanceId)) pool.pendingContributors.Add(holding.instanceId);
                pool.accumulated += (long)holding.LevyContributionPermille *
                    nation.GetHoldingLawAmount(NationalLawEffectType.LevyConscription, holding, sourceProvince.region);
                pool.remainderProvince = sourceProvince;
                pool.remainderHolding = holding;
                int formations = (int)(pool.accumulated / completeFormation - before);
                for (int ordinal = 0; ordinal < formations; ordinal++)
                {
                    List<string> contributors = new List<string>(pool.pendingContributors);
                    pool.pendingContributors.Clear();
                    long completedBoundary = (before + ordinal + 1L) * completeFormation;
                    if (completedBoundary < pool.accumulated) pool.pendingContributors.Add(holding.instanceId);
                    if (sourceProvince != this) continue;
                    long formationIndex = before + ordinal;
                    string entitlementId = name + "|" + nameof(ProvinceHolding) + "|" + poolId + "|" + formationIndex;
                    ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
                    if (entitlement == null)
                    {
                        entitlement = new ProvinceLevyEntitlement { id = entitlementId, ruleId = string.Empty,
                            holdingId = holding.HoldingId, holdingInstanceId = holding.instanceId,
                            unitName = pool.unit.name,
                            unit = pool.unit, buildingSlot = holding.slotIndex, ordinal = (int)formationIndex,
                            beneficiaryNation = nation.name, state = LevyEntitlementState.Available };
                        levyEntitlements.Add(entitlement);
                    }
                    entitlement.holdingId = holding.HoldingId; entitlement.holdingInstanceId = holding.instanceId;
                    entitlement.unit = pool.unit;
                    entitlement.unitName = pool.unit.name; entitlement.eligible = callupsAllowed;
                    entitlement.beneficiaryNation = nation.name;
                    entitlement.contributorHoldingInstanceIds = contributors;
                }
            }
        }

        // Whole formations remain type-pure. Only the fractional leftovers are pooled:
        // the largest remainder claims the rounded formation and absorbs smaller pools.
        List<LevyUnitPool> remainderPools = new List<LevyUnitPool>(unitPools.Values);
        foreach (LevyUnitPool pool in remainderPools) pool.remainder = pool.accumulated % completeFormation;
        int roundedOrdinal = 0;
        while (true)
        {
            remainderPools.Sort((left, right) =>
            {
                int byCapacity = right.remainder.CompareTo(left.remainder);
                return byCapacity != 0 ? byCapacity : string.CompareOrdinal(left.unit.name, right.unit.name);
            });
            long totalRemainder = 0; foreach (LevyUnitPool pool in remainderPools) totalRemainder += pool.remainder;
            if (remainderPools.Count == 0 || remainderPools[0].remainder <= 0 || totalRemainder < completeFormation) break;
            LevyUnitPool recipient = remainderPools[0];
            long needed = completeFormation - recipient.remainder;
            List<string> contributors = new List<string>(recipient.pendingContributors);
            recipient.remainder = 0;
            recipient.pendingContributors.Clear();
            for (int i = 1; i < remainderPools.Count && needed > 0; i++)
            {
                LevyUnitPool donor = remainderPools[i];
                if (donor.remainder <= 0) continue;
                long absorbed = System.Math.Min(needed, donor.remainder);
                donor.remainder -= absorbed; needed -= absorbed;
                foreach (string contributor in donor.pendingContributors)
                    if (!contributors.Contains(contributor)) contributors.Add(contributor);
                if (donor.remainder == 0) donor.pendingContributors.Clear();
            }
            if (needed > 0 || recipient.remainderProvince != this || recipient.remainderHolding == null)
            { roundedOrdinal++; continue; }
            ProvinceHolding holding = recipient.remainderHolding;
            string poolId = !string.IsNullOrWhiteSpace(recipient.unit.name) ? recipient.unit.name : recipient.unit.unitname;
            string entitlementId = name + "|RoundedLevy|" + poolId + "|" + roundedOrdinal;
            ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
            if (entitlement == null)
            {
                entitlement = new ProvinceLevyEntitlement { id = entitlementId, ruleId = string.Empty,
                    holdingId = holding.HoldingId, holdingInstanceId = holding.instanceId,
                    unitName = recipient.unit.name, unit = recipient.unit, buildingSlot = holding.slotIndex,
                    ordinal = roundedOrdinal, beneficiaryNation = nation.name, state = LevyEntitlementState.Available };
                levyEntitlements.Add(entitlement);
            }
            entitlement.holdingId = holding.HoldingId; entitlement.holdingInstanceId = holding.instanceId;
            entitlement.unit = recipient.unit; entitlement.unitName = recipient.unit.name;
            entitlement.eligible = callupsAllowed; entitlement.beneficiaryNation = nation.name;
            entitlement.contributorHoldingInstanceIds = contributors;
            roundedOrdinal++;
        }

        // Records whose source capacity disappeared are marked ineligible at the beginning of
        // reconciliation. Once call-ups are allowed, an unraised obsolete record has no state
        // worth preserving and would otherwise remain forever as phantom maximum capacity.
        if (callupsAllowed)
            levyEntitlements.RemoveAll(entitlement => entitlement != null && !entitlement.eligible &&
                entitlement.state == LevyEntitlementState.Available &&
                string.IsNullOrWhiteSpace(entitlement.raisedArmyId));
    }

    public void ProcessLevyTick()
    {
        if (IsOccupied) return;
        int currentTick = Owners.Instance != null ? Owners.Instance.turncounter : 0;
        if (nextLevyReconcileTick < 0)
            nextLevyReconcileTick = currentTick + StableLevyReconcilePhase(name, 8);
        if (currentTick >= nextLevyReconcileTick)
        {
            ReconcileLevyEntitlements();
            nextLevyReconcileTick = currentTick + 8;
        }
        foreach (ProvinceLevyEntitlement entitlement in levyEntitlements)
        {
            // Mobilization is an army-level FIFO queue processed in batches by Owners.
            // Province ticks continue to own only levy recovery/demobilization.
            if (entitlement != null && entitlement.state == LevyEntitlementState.Mobilizing) continue;
            if (entitlement == null || entitlement.remainingTicks <= 0) continue;
            if (entitlement.state == LevyEntitlementState.Mobilizing && !entitlement.eligible)
            { entitlement.state = LevyEntitlementState.Available; entitlement.remainingTicks = 0; entitlement.raisedArmyId = null; continue; }
            entitlement.remainingTicks--;
            if (entitlement.remainingTicks != 0) continue;
            if (entitlement.state == LevyEntitlementState.Recovering)
            { entitlement.state = LevyEntitlementState.Available; entitlement.raisedArmyId = null; HoldingRecoveryCache.Clear(); }
        }
    }

    private void EnsureStartingCultureBuildings()
    {
        string culture = PrimaryCulture != null ? PrimaryCulture.name :
            nation != null && nation.culture != null ? nation.culture.DisplayName : string.Empty;
        if (string.IsNullOrWhiteSpace(culture)) return;
        if (culture.Equals("Germanic", System.StringComparison.OrdinalIgnoreCase))
            EnsureStartingBuilding("SacredGrove");
        else if (culture.Equals("Latin", System.StringComparison.OrdinalIgnoreCase))
            EnsureStartingBuilding("Sewer");
        else if (culture.Equals("Punic", System.StringComparison.OrdinalIgnoreCase))
        {
            EnsureStartingBuilding("Sewer");
            EnsureStartingBuilding("Roads");
        }
    }

    private void EnsureStartingBuilding(string buildingId)
    {
        if (buildings.Exists(building => building != null &&
            building.BuildingId.Equals(buildingId, System.StringComparison.OrdinalIgnoreCase))) return;
        int slot = -1;
        for (int candidate = 0; candidate < 4; candidate++)
            if (GetBuildingInSlot(candidate) == null && (constructionOrders == null ||
                !constructionOrders.Exists(order => order != null && order.slotIndex == candidate)))
            { slot = candidate; break; }
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        if (slot < 0 || definition == null) return;
        buildings.Add(new ProvinceBuilding { definition = definition, id = definition.StableId,
            level = 1, maxLevel = definition.maximumLevel, slotIndex = slot });
    }

    private static int StableLevyReconcilePhase(string value, int interval)
    {
        unchecked
        {
            int hash = 17;
            if (value != null) for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return (hash & int.MaxValue) % Mathf.Max(1, interval);
        }
    }

    public List<ProvinceLevyEntitlement> GetAvailableRegionLevies(Nation owner)
    {
        List<ProvinceLevyEntitlement> result = new List<ProvinceLevyEntitlement>();
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        if (campaignRegion != null && !campaignRegion.AllowsLevyCallups(owner)) return result;
        foreach (Province province in GetOccupiedRegionProvinces(owner))
        {
            province.ReconcileLevyEntitlements();
            result.AddRange(province.levyEntitlements.FindAll(item => item != null && item.eligible &&
                item.state == LevyEntitlementState.Available && item.unit != null));
        }
        result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        return result;
    }

    public bool RaiseLevy(string entitlementId, FieldArmyHolder army)
    {
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
        if (campaignRegion != null && !campaignRegion.AllowsLevyCallups(nation) || entitlement == null || army == null ||
            army.fieldArmy == null || !army.IsTargetNull() || !entitlement.eligible ||
            entitlement.state != LevyEntitlementState.Available || army.fieldArmy.nation != nation ||
            army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize() >= army.fieldArmy.MaxArmySize) return false;
        // Every newly mobilized unit consumes one point from the region it comes from.
        // The army object itself is free; this charge applies to levies and professionals alike.
        if (nation == null || !nation.TrySpendManpower(this, 1f)) return false;
        entitlement.raisedArmyId = army.NetworkArmyId;
        entitlement.remainingTicks = 0; // Waiting for an open slot in the next three-unit batch.
        entitlement.state = LevyEntitlementState.Mobilizing;
        return true;
    }

    public int RaiseAllAvailableRegionLevies(FieldArmyHolder army, bool allowAllInSecondClick = false)
    {
        if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null || nation != army.fieldArmy.nation) return 0;
        int capacity = army.fieldArmy.MaxArmySize - army.fieldArmy.GrabArmySize() - army.fieldArmy.GrabQueuedArmySize();
        if (capacity <= 0) return 0;
        List<Province> regionProvinces = GetOccupiedRegionProvinces(army.fieldArmy.nation);
        List<ProvinceLevyEntitlement> available = GetAvailableRegionLevies(army.fieldArmy.nation);
        string armyKey = !string.IsNullOrEmpty(army.NetworkArmyId) ? army.NetworkArmyId : army.GetInstanceID().ToString();
        bool allIn = allowAllInSecondClick && levyAllInArmedArmyId == armyKey;
        bool skippedForFood = false;
        if (allIn) levyAllInArmedArmyId = null;
        int raised = 0;
        foreach (ProvinceLevyEntitlement entitlement in available)
        {
            if (raised >= capacity) break;
            if (!allIn && !CanMobilizeWithoutFoodDeficit(entitlement, army.fieldArmy.nation, regionProvinces))
            { skippedForFood = true; continue; }
            Province source = regionProvinces.Find(candidate => candidate.levyEntitlements != null &&
                candidate.levyEntitlements.Contains(entitlement));
            if (source != null && source.RaiseLevy(entitlement.id, army)) raised++;
        }
        if (allowAllInSecondClick)
            levyAllInArmedArmyId = !allIn && skippedForFood ? armyKey : null;
        return raised;
    }

    public int RaiseAllAvailableLocalAndAdjacentRegionLevies(FieldArmyHolder army,
        bool allowAllInSecondClick = false)
    {
        if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null ||
            nation != army.fieldArmy.nation) return 0;
        Nation owner = army.fieldArmy.nation;
        int capacity = army.fieldArmy.MaxArmySize - army.fieldArmy.GrabArmySize() -
            army.fieldArmy.GrabQueuedArmySize();
        if (capacity <= 0) return 0;
        List<Province> accessibleProvinces = GetLocalAndAdjacentRegionProvinces(owner);
        List<ProvinceLevyEntitlement> available = GetAvailableLocalAndAdjacentRegionLevies(owner);
        string armyKey = !string.IsNullOrEmpty(army.NetworkArmyId)
            ? army.NetworkArmyId : army.GetInstanceID().ToString();
        bool allIn = allowAllInSecondClick && levyAllInArmedArmyId == armyKey;
        bool skippedForFood = false;
        if (allIn) levyAllInArmedArmyId = null;
        int raised = 0;
        foreach (ProvinceLevyEntitlement entitlement in available)
        {
            if (raised >= capacity) break;
            Province source = accessibleProvinces.Find(candidate => candidate.levyEntitlements != null &&
                candidate.levyEntitlements.Contains(entitlement));
            if (source == null) continue;
            List<Province> sourceRegionProvinces = accessibleProvinces.FindAll(candidate =>
                candidate != null && candidate.SharesRegionWith(source));
            if (!allIn && !source.CanMobilizeWithoutFoodDeficit(entitlement, owner, sourceRegionProvinces))
            { skippedForFood = true; continue; }
            if (source.RaiseLevy(entitlement.id, army)) raised++;
        }
        if (allowAllInSecondClick)
            levyAllInArmedArmyId = !allIn && skippedForFood ? armyKey : null;
        return raised;
    }

    private bool CanMobilizeWithoutFoodDeficit(ProvinceLevyEntitlement entitlement, Nation owner,
        List<Province> regionProvinces)
    {
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        if (campaignRegion == null || entitlement == null) return true;
        return campaignRegion.RegionalFood(owner) - LevyEconomicImpact(entitlement, regionProvinces, true) >= 0;
    }

    public int RaiseBestAIRegionLevies(FieldArmyHolder army, int maximumToRaise)
    {
        if (army == null || army.fieldArmy == null || army.fieldArmy.nation != nation || maximumToRaise <= 0) return 0;
        List<Province> regionProvinces = GetOccupiedRegionProvinces(nation);
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        int currentFood = campaignRegion != null ? campaignRegion.RegionalFood(nation) : int.MaxValue;
        List<ProvinceLevyEntitlement> candidates = GetAvailableRegionLevies(nation);
        Dictionary<string, int> impacts = new Dictionary<string, int>(System.StringComparer.Ordinal);
        foreach (ProvinceLevyEntitlement entitlement in candidates)
        {
            int foodLoss = LevyEconomicImpact(entitlement, regionProvinces, true);
            int impact = LevyEconomicImpact(entitlement, regionProvinces, false);
            // A deficit is strategically expensive, but never an absolute prohibition.
            impact += Mathf.Max(0, foodLoss - Mathf.Max(0, currentFood)) * 500;
            impacts[entitlement.id] = impact;
        }
        candidates.Sort((left, right) =>
        {
            int byImpact = impacts[left.id].CompareTo(impacts[right.id]);
            return byImpact != 0 ? byImpact : string.CompareOrdinal(left.id, right.id);
        });
        int capacity = Mathf.Min(maximumToRaise, army.fieldArmy.MaxArmySize - army.fieldArmy.GrabArmySize() -
            army.fieldArmy.GrabQueuedArmySize());
        int raised = 0;
        foreach (ProvinceLevyEntitlement candidate in candidates)
        {
            if (raised >= capacity) break;
            Province source = regionProvinces.Find(province => province != null && province.levyEntitlements != null &&
                province.levyEntitlements.Contains(candidate));
            if (source != null && source.RaiseLevy(candidate.id, army)) raised++;
        }
        return raised;
    }

    private int LevyEconomicImpact(ProvinceLevyEntitlement entitlement, List<Province> regionProvinces, bool foodOnly)
    {
        if (entitlement == null) return int.MaxValue / 4;
        int impact = 0;
        HashSet<string> contributors = new HashSet<string>();
        if (!string.IsNullOrEmpty(entitlement.holdingInstanceId)) contributors.Add(entitlement.holdingInstanceId);
        if (entitlement.contributorHoldingInstanceIds != null)
            foreach (string id in entitlement.contributorHoldingInstanceIds) if (!string.IsNullOrEmpty(id)) contributors.Add(id);
        foreach (Province source in regionProvinces)
        foreach (string id in contributors)
        {
            ProvinceHolding holding = source != null ? source.GetHolding(id) : null;
            if (holding == null || source.IsHoldingMobilized(id)) continue;
            int foodLoss = Mathf.Max(0, source.GetHoldingOutput(holding, HoldingOutputType.Food, false) -
                source.GetHoldingOutput(holding, HoldingOutputType.Food, true));
            if (foodOnly) { impact += foodLoss; continue; }
            int incomeLoss = Mathf.Max(0, source.GetHoldingOutput(holding, HoldingOutputType.Income, false) -
                source.GetHoldingOutput(holding, HoldingOutputType.Income, true));
            int manpowerLoss = Mathf.Max(0, source.GetHoldingOutput(holding, HoldingOutputType.Manpower, false) -
                source.GetHoldingOutput(holding, HoldingOutputType.Manpower, true));
            impact += foodLoss * 100 + incomeLoss * 20 + manpowerLoss * 10 +
                Mathf.Max(1, holding.definition != null ? holding.definition.categoryTier : 1) * 2;
        }
        return impact;
    }

    public void BeginLevyRecovery(string entitlementId)
    {
        ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
        if (entitlement == null) return;
        LevyGrantRule rule = LevySystem.FindRule(nation, entitlement.ruleId);
        entitlement.state = LevyEntitlementState.Recovering; entitlement.raisedArmyId = null;
        HoldingRecoveryCache.Clear();
        HoldingDefinition holding = HoldingDefinition.Find(entitlement.holdingId);
        int baseRecoveryTicks = holding != null ? Mathf.Max(0, holding.levyRecoveryTicks) :
            rule != null ? Mathf.Max(0, rule.recoveryTicks) : 120;
        entitlement.remainingTicks = nation != null
            ? Mathf.Max(0, nation.ApplyLawModifiers(NationalLawEffectType.LevyRecoveryTime,
                baseRecoveryTicks, GetHolding(entitlement.holdingInstanceId), CampaignUnitOrigin.Levy))
            : baseRecoveryTicks;
        if (entitlement.remainingTicks == 0 && entitlement.eligible) entitlement.state = LevyEntitlementState.Available;
    }

    public bool CanRecruitLocal(UnitSaveData unit)
    {
        if (nation == null || !AllowsRecruitment(nation) || NationContentResolver.GetUnitTier(nation, unit) <= 0) return false;
        return buildings.Exists(building => building != null && building.Unlocks(unit, nation));
    }

    public bool AllowsRecruitment(Nation recruitingNation)
    {
        if (IsOccupied || recruitingNation == null || nation != recruitingNation) return false;
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        return campaignRegion == null || campaignRegion.AllowsRecruitment(recruitingNation);
    }

    public List<UnitSaveData> GetRecruitableLocalUnits()
    {
        List<UnitSaveData> result = new List<UnitSaveData>();
        if (nation == null) return result;
        foreach (NationUnitEntry entry in NationContentResolver.ResolveUnits(nation))
        {
            UnitSaveData unit = entry != null ? entry.unit : null;
            if (unit != null && CanRecruitLocal(unit) && !result.Contains(unit)) result.Add(unit);
        }
        return result;
    }

    public bool SharesRegionWith(Province other)
    {
        return other != null && !string.IsNullOrWhiteSpace(region) &&
            string.Equals(region, other.region, System.StringComparison.OrdinalIgnoreCase);
    }

    public List<Province> GetOccupiedRegionProvinces(Nation occupyingNation = null)
    {
        List<Province> result = new List<Province>();
        Nation owner = occupyingNation != null ? occupyingNation : nation;
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        if (campaignRegion != null && campaignRegion.provincelist != null)
        {
            foreach (Province province in campaignRegion.provincelist)
                if (province != null && !province.IsOccupied && province.nation == owner) result.Add(province);
        }
        else if (!IsOccupied && nation == owner) result.Add(this);
        return result;
    }

    public List<UnitSaveData> GetRecruitableRegionUnits(Nation recruitingNation = null)
    {
        List<UnitSaveData> result = new List<UnitSaveData>();
        Nation owner = recruitingNation != null ? recruitingNation : nation;
        if (!AllowsRecruitment(owner)) return result;
        foreach (Province province in GetOccupiedRegionProvinces(owner))
            foreach (UnitSaveData unit in province.GetRecruitableLocalUnits())
                if (unit != null && !result.Contains(unit)) result.Add(unit);
        return result;
    }

    public Province FindRegionalRecruitmentSource(UnitSaveData unit, Nation recruitingNation = null)
    {
        Nation owner = recruitingNation != null ? recruitingNation : nation;
        return GetOccupiedRegionProvinces(owner).Find(province => province.CanRecruitLocal(unit));
    }

    public List<ProvinceMercenaryPool> GetAvailableMercenaries()
    {
        if (!ProvinceMercenaryPool.Enabled) return new List<ProvinceMercenaryPool>();
        return mercenaryPools.FindAll(pool => pool != null && pool.unit != null && pool.available > 0);
    }

    public ProvinceMercenaryPool FindMercenary(string unitName)
    {
        if (!ProvinceMercenaryPool.Enabled) return null;
        return mercenaryPools.Find(pool => pool != null && pool.unit != null && pool.unit.name == unitName);
    }

    public void RegenerateMercenaries()
    {
        foreach (ProvinceMercenaryPool pool in mercenaryPools) pool?.Regenerate(nation);
    }

    public ProvinceBuilding GetBuilding(string buildingId)
    {
        return buildings.Find(building => building != null && building.BuildingId.Equals(buildingId, System.StringComparison.OrdinalIgnoreCase));
    }

    public bool UpgradeBuilding(string buildingId)
    {
        ProvinceBuilding building = GetBuilding(buildingId);
        if (building == null)
        {
            buildings.Add(new ProvinceBuilding
            {
                id = buildingId, level = 1, maxLevel = ProvinceBuilding.MaximumLevelFor(buildingId)
            });
            return true;
        }
        if (building.level >= building.maxLevel) return false;
        building.level++;
        return true;
    }

    public int GetGoldIncome()
    {
        return Mathf.RoundToInt(GetGoldIncomeUnrounded());
    }

    public float GetGoldIncomeUnrounded()
    {
        ProvinceBuilding farm = GetBuilding("Farm");
        float income = 0f;
        if (buildings != null) foreach (ProvinceBuilding building in buildings)
            if (building != null && building.definition != null)
                income += building.DefinitionGoldIncomeUnrounded(urbanization);
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null)
            {
                float holdingIncome = GetHoldingOutputUnrounded(holding, HoldingOutputType.Income,
                    IsHoldingMobilized(holding.instanceId));
                int taxationPermille = nation != null ? nation.ApplyLawModifiers(
                    NationalLawEffectType.HoldingTaxation, 1000, holding) : 1000;
                income += holdingIncome * Mathf.Max(0, taxationPermille) / 1000f;
            }
        if (farm != null && farm.definition == null)
            income += Mathf.Max(0, farm.level) * CampaignEconomy.FarmIncomePerLevel;
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        float loyalty = campaignRegion != null ? campaignRegion.GetLoyalty(nation) : 100f;
        return income * Mathf.Clamp(loyalty, 0f, 100f) / 100f * CampaignEconomy.GoldIncomeRate;
    }

    public int GetHoldingOutput(HoldingOutputType type)
    {
        int total = 0;
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null) total += GetHoldingOutput(holding, type);
        return total;
    }

    public int GetHoldingOutput(ProvinceHolding holding, HoldingOutputType type)
    {
        return GetHoldingOutput(holding, type, holding != null && IsHoldingMobilized(holding.instanceId));
    }

    public int GetHoldingOutput(ProvinceHolding holding, HoldingOutputType type, bool mobilized)
    {
        if (holding == null) return 0;
        bool recoveringFromLevyLoss = GetHoldingLevyRecoveryTicks(holding.instanceId) > 0;
        if (recoveringFromLevyLoss)
            return type == HoldingOutputType.Food ? -holding.FoodConsumption : 0;
        int value = holding.GetOutput(type, urbanization, mobilized);
        float efficiency = GetHoldingEfficiency(holding.definition);
        if (type == HoldingOutputType.Food)
        {
            int consumption = holding.FoodConsumption;
            int grossProduction = value + consumption;
            return Mathf.RoundToInt(grossProduction * (1f + efficiency / 100f)) - consumption;
        }
        return Mathf.RoundToInt(value * (1f + efficiency / 100f));
    }

    public float GetHoldingOutputUnrounded(HoldingOutputType type)
    {
        float total = 0f;
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null) total += GetHoldingOutputUnrounded(holding, type,
                IsHoldingMobilized(holding.instanceId));
        return total;
    }

    public float GetHoldingOutputUnrounded(ProvinceHolding holding, HoldingOutputType type, bool mobilized)
    {
        if (holding == null) return 0f;
        if (GetHoldingLevyRecoveryTicks(holding.instanceId) > 0)
            return type == HoldingOutputType.Food ? -holding.FoodConsumption : 0f;
        if (type == HoldingOutputType.PoliticalInfluence) return 0f;

        float value = 0f;
        HoldingDefinition definition = holding.definition;
        if (definition != null && definition.outputs != null)
            foreach (HoldingOutputDefinition output in definition.outputs)
                if (output != null && output.type == type && !(mobilized && output.disabledWhileMobilized))
                    value += UrbanizationOutputScaling.ApplyUnrounded(output.baseValue,
                        output.EffectiveUrbanizationResponse, urbanization);
        if (type == HoldingOutputType.Income && Mathf.Approximately(value, 0f) &&
            definition != null && definition.levels != null)
            foreach (HoldingLevelDefinition level in definition.levels)
                if (level != null && level.level <= holding.level)
                    value += UrbanizationOutputScaling.ApplyUnrounded(level.goldIncome,
                        level.urbanizationResponse, urbanization);

        float efficiencyMultiplier = 1f + GetHoldingEfficiency(definition) / 100f;
        if (type == HoldingOutputType.Food)
            return value * efficiencyMultiplier - holding.FoodConsumption;
        return value * efficiencyMultiplier;
    }

    private float GetHoldingEfficiency(HoldingDefinition definition)
    {
        if (definition == null) return 0f;
        int campaignTurn = Owners.Instance != null ? Owners.Instance.turncounter : 0;
        if (holdingEfficiencyCache == null)
            holdingEfficiencyCache = new Dictionary<HoldingDefinition, float>();
        if (holdingEfficiencyCacheTurn != campaignTurn)
        {
            holdingEfficiencyCacheTurn = campaignTurn;
            holdingEfficiencyCache.Clear();
        }
        if (!holdingEfficiencyCache.TryGetValue(definition, out float efficiency))
        {
            efficiency = HoldingEvolutionSystem.OutputEfficiencyPercent(this, definition);
            holdingEfficiencyCache[definition] = efficiency;
        }
        return efficiency;
    }

    public void ProcessHoldingEvolutionTick(int campaignTick)
    {
        if (IsOccupied) return;
        HoldingEvolutionSystem.ProcessTick(this, campaignTick);
    }

    public int GetFoodOutput()
    {
        return Mathf.RoundToInt(GetFoodOutputUnrounded());
    }

    public float GetFoodOutputUnrounded() => GetFoodProductionUnrounded() - GetFoodConsumption();

    public int GetFoodProduction() => Mathf.RoundToInt(GetFoodProductionUnrounded());

    public float RuralizationFoodBonusUnrounded() => urbanization < 0
        ? Mathf.Lerp(0f, 10f, Mathf.Clamp01(-urbanization / 100f)) : 0f;

    public float GetFoodProductionUnrounded()
    {
        float total = 0f;
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null)
                total += GetHoldingOutputUnrounded(holding, HoldingOutputType.Food,
                    IsHoldingMobilized(holding.instanceId)) + holding.FoodConsumption;
        if (buildings != null) foreach (ProvinceBuilding building in buildings)
            if (building != null && building.definition != null)
                total += building.DefinitionFoodOutputUnrounded(urbanization);
        return total + RuralizationFoodBonusUnrounded();
    }

    public int GetFoodConsumption()
    {
        int total = 0;
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null) total += holding.FoodConsumption;
        if (buildings != null) foreach (ProvinceBuilding building in buildings)
            if (building != null && building.definition != null) total += building.DefinitionFoodConsumption;
        return total;
    }

    public bool IsHoldingMobilized(string holdingInstanceId)
    {
        if (string.IsNullOrWhiteSpace(holdingInstanceId)) return false;
        HoldingRecoveryCacheEntry cache = GetRegionalLevyStateCache();
        return cache.mobilized.Contains(holdingInstanceId);
    }

    public int GetHoldingLevyRecoveryTicks(string holdingInstanceId)
    {
        if (string.IsNullOrWhiteSpace(holdingInstanceId)) return 0;
        HoldingRecoveryCacheEntry cache = GetRegionalLevyStateCache();
        return cache.ticks.TryGetValue(holdingInstanceId, out int remaining) ? remaining : 0;
    }

    private HoldingRecoveryCacheEntry GetRegionalLevyStateCache()
    {
        string cacheKey = (region ?? string.Empty) + "|" + (nation != null ? nation.name : string.Empty);
        if (!HoldingRecoveryCache.TryGetValue(cacheKey, out HoldingRecoveryCacheEntry cache))
        { cache = new HoldingRecoveryCacheEntry(); HoldingRecoveryCache.Add(cacheKey, cache); }
        if (cache.frame != Time.frameCount)
        {
            cache.frame = Time.frameCount;
            cache.ticks.Clear();
            cache.mobilized.Clear();
            foreach (Province province in GetOccupiedRegionProvinces(nation))
            {
                if (province == null || province.levyEntitlements == null) continue;
                foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
                {
                    if (entitlement == null || entitlement.state != LevyEntitlementState.Mobilizing &&
                        entitlement.state != LevyEntitlementState.Raised &&
                        entitlement.state != LevyEntitlementState.Recovering) continue;
                    AddMobilizedHolding(cache.mobilized, entitlement.holdingInstanceId);
                    if (entitlement.contributorHoldingInstanceIds != null)
                        foreach (string contributor in entitlement.contributorHoldingInstanceIds)
                            AddMobilizedHolding(cache.mobilized, contributor);
                    if (entitlement.state != LevyEntitlementState.Recovering) continue;
                    AddRecoveryTicks(cache.ticks, entitlement.holdingInstanceId, entitlement.remainingTicks);
                    if (entitlement.contributorHoldingInstanceIds != null)
                        foreach (string contributor in entitlement.contributorHoldingInstanceIds)
                            AddRecoveryTicks(cache.ticks, contributor, entitlement.remainingTicks);
                }
            }
        }
        return cache;
    }

    private static void AddMobilizedHolding(HashSet<string> values, string holdingId)
    {
        if (!string.IsNullOrEmpty(holdingId)) values.Add(holdingId);
    }

    private static void AddRecoveryTicks(Dictionary<string, int> values, string holdingId, int ticks)
    {
        if (string.IsNullOrEmpty(holdingId)) return;
        if (!values.TryGetValue(holdingId, out int current) || ticks > current) values[holdingId] = ticks;
    }

    public List<ProvinceLevyEntitlement> GetRegionalLevyEntitlementsForHolding(string holdingInstanceId)
    {
        List<ProvinceLevyEntitlement> result = new List<ProvinceLevyEntitlement>();
        if (string.IsNullOrWhiteSpace(holdingInstanceId)) return result;
        foreach (Province province in GetOccupiedRegionProvinces(nation))
        {
            if (province == null || province.levyEntitlements == null) continue;
            foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
                if (entitlement != null && entitlement.eligible &&
                    (entitlement.holdingInstanceId == holdingInstanceId || entitlement.contributorHoldingInstanceIds != null &&
                        entitlement.contributorHoldingInstanceIds.Contains(holdingInstanceId)) &&
                    !result.Contains(entitlement)) result.Add(entitlement);
        }
        return result;
    }

    public List<ProvinceLevyEntitlement> GetAvailableLocalAndAdjacentRegionLevies(Nation owner)
    {
        List<ProvinceLevyEntitlement> result = new List<ProvinceLevyEntitlement>();
        foreach (Province province in GetLocalAndAdjacentRegionProvinces(owner))
        {
            CampaignRegion sourceRegion = Owners.Instance != null
                ? Owners.Instance.CallRegionByString(province.region) : null;
            if (sourceRegion != null && !sourceRegion.AllowsLevyCallups(owner)) continue;
            province.ReconcileLevyEntitlements();
            result.AddRange(province.levyEntitlements.FindAll(item => item != null && item.eligible &&
                item.state == LevyEntitlementState.Available && item.unit != null));
        }
        result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        return result;
    }

    public List<Province> GetLocalAndAdjacentRegionProvinces(Nation owner)
    {
        List<Province> result = new List<Province>();
        if (owner == null || Owners.Instance == null || string.IsNullOrWhiteSpace(region)) return result;
        HashSet<string> accessibleRegions = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { region };
        CampaignRegion localRegion = Owners.Instance.CallRegionByString(region);
        List<Province> localProvinces = localRegion != null && localRegion.provincelist != null
            ? localRegion.provincelist : new List<Province> { this };
        foreach (Province local in localProvinces)
        {
            if (local == null || local.AdjacentProvinces == null) continue;
            foreach (string adjacentName in local.AdjacentProvinces)
            {
                if (string.IsNullOrWhiteSpace(adjacentName) || Owners.Instance.provincedict == null ||
                    !Owners.Instance.provincedict.TryGetValue(adjacentName, out Province adjacent) || adjacent == null ||
                    string.IsNullOrWhiteSpace(adjacent.region)) continue;
                accessibleRegions.Add(adjacent.region);
            }
        }
        foreach (Province candidate in Owners.Instance.provincelist)
            if (candidate != null && candidate.nation == owner && accessibleRegions.Contains(candidate.region))
                result.Add(candidate);
        result.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return result;
    }

    public bool CanAccessRecruitmentSource(Province source, Nation sourceOwner)
    {
        return source != null && sourceOwner != null &&
            GetLocalAndAdjacentRegionProvinces(sourceOwner).Contains(source);
    }

    public int GetBuildingUpkeep()
    {
        int total = 0;
        if (buildings != null) foreach (ProvinceBuilding building in buildings)
            if (building != null) total += building.DefinitionGoldUpkeep;
        return total;
    }

    public void ApplyTempleCultureInfluence(int campaignTurn)
    {
        const int conversionInterval = 10;
        if ((campaignTurn + StableCultureConversionPhase(name, conversionInterval)) % conversionInterval != 0) return;
        ProvinceBuilding temple = GetBuilding("Temple");
        string nationalCulture = nation != null && nation.culture != null ? nation.culture.DisplayName : string.Empty;
        if (temple == null || string.IsNullOrWhiteSpace(nationalCulture) || holdings == null) return;
        ProvinceHolding converted = holdings.FindLast(holding => holding != null &&
            !string.Equals(holding.cultureName, nationalCulture, System.StringComparison.OrdinalIgnoreCase));
        if (converted != null) { converted.cultureName = nationalCulture; RebuildPopulationFromHoldings(); }
    }

    private static int StableCultureConversionPhase(string provinceName, int interval)
    {
        unchecked
        {
            int hash = 17;
            if (provinceName != null)
                for (int i = 0; i < provinceName.Length; i++) hash = hash * 31 + provinceName[i];
            return (hash & int.MaxValue) % Mathf.Max(1, interval);
        }
    }

    public ProvinceBuilding GetBuildingInSlot(int slotIndex)
    {
        return buildings.Find(building => building != null && building.slotIndex == slotIndex);
    }

    public bool DestroyBuildingInSlot(int slotIndex)
    {
        if (IsOccupied) return false;
        if (slotIndex < 0 || buildings == null) return false;
        ProvinceBuilding building = GetBuildingInSlot(slotIndex);
        if (building == null) return false;
        buildings.Remove(building);
        ClampDevelopment();
        RefreshGarrisonForFort();
        return true;
    }

    public ProvinceHolding GetHoldingInSlot(int slotIndex) => holdings != null
        ? holdings.Find(holding => holding != null && holding.slotIndex == slotIndex) : null;
    public ProvinceHolding GetHolding(string instanceId) => holdings != null
        ? holdings.Find(holding => holding != null && holding.instanceId == instanceId) : null;

    public bool BeginHoldingTransformation(string instanceId, string targetHoldingId, int transformationTicks)
    {
        ProvinceHolding holding = GetHolding(instanceId);
        return holding != null && BeginHoldingConstruction(holding.slotIndex, targetHoldingId, 1, transformationTicks);
    }

    public bool BeginHoldingConstruction(int slotIndex, string holdingId, int targetLevel, int constructionTicks)
    {
        if (IsOccupied) return false;
        if (slotIndex < 0 || string.IsNullOrWhiteSpace(holdingId)) return false;
        if (holdings == null) holdings = new List<ProvinceHolding>();
        if (holdingConstructionOrders == null) holdingConstructionOrders = new List<HoldingConstructionOrder>();
        if (holdingConstructionOrders.Exists(order => order != null && order.slotIndex == slotIndex)) return false;
        HoldingDefinition definition = HoldingDefinition.Find(holdingId);
        if (definition == null || targetLevel < 1 || targetLevel > Mathf.Max(1, definition.maximumLevel)) return false;
        ProvinceHolding existing = GetHoldingInSlot(slotIndex);
        if (existing == null || existing.HoldingId.Equals(definition.StableId, System.StringComparison.OrdinalIgnoreCase)) return false;
        if (existing.definition != null && !existing.definition.CanTransformTo(definition.StableId, this)) return false;
        HoldingConstructionOrder order = new HoldingConstructionOrder { slotIndex = slotIndex,
            holdingInstanceId = existing.instanceId, holdingId = definition.StableId, targetLevel = 1,
            remainingTicks = Mathf.Max(0, constructionTicks) };
        holdingConstructionOrders.Add(order);
        if (order.remainingTicks == 0) CompleteHoldingConstruction(order);
        return true;
    }

    public void ProcessHoldingConstructionTick()
    {
        if (IsOccupied) return;
        if (holdingConstructionOrders == null) return;
        for (int i = holdingConstructionOrders.Count - 1; i >= 0; i--)
        {
            HoldingConstructionOrder order = holdingConstructionOrders[i];
            if (order == null) { holdingConstructionOrders.RemoveAt(i); continue; }
            order.remainingTicks--;
            if (order.remainingTicks <= 0) CompleteHoldingConstruction(order);
        }
    }

    private void CompleteHoldingConstruction(HoldingConstructionOrder order)
    {
        HoldingDefinition definition = HoldingDefinition.Find(order.holdingId);
        if (definition == null) { holdingConstructionOrders.Remove(order); return; }
        ProvinceHolding holding = GetHolding(order.holdingInstanceId) ?? GetHoldingInSlot(order.slotIndex);
        if (holding == null) { holdingConstructionOrders.Remove(order); return; }
        holding.definition = definition; holding.id = definition.StableId; holding.level = 1;
        holdingConstructionOrders.Remove(order);
        ClampDevelopment();
        ReconcileLevyEntitlements();
    }

    public bool BeginBuildingConstruction(int slotIndex, string buildingId, int targetLevel, int constructionTicks,
        bool initiatedByAI = false)
    {
        if (IsOccupied) return false;
        if (slotIndex < 0 || string.IsNullOrEmpty(buildingId) || constructionOrders.Exists(order => order.slotIndex == slotIndex)) return false;
        if (nation == null || !NationContentResolver.CanConstructBuildingLevel(nation, buildingId, targetLevel)) return false;
        ProvinceBuilding existing = GetBuildingInSlot(slotIndex);
        if (existing != null && !existing.BuildingId.Equals(buildingId, System.StringComparison.OrdinalIgnoreCase)) return false;
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        if (definition != null && definition.provinceUnique)
        {
            if (buildings.Exists(building => building != null && building.slotIndex != slotIndex &&
                building.BuildingId.Equals(buildingId, System.StringComparison.OrdinalIgnoreCase))) return false;
            if (constructionOrders.Exists(order => order != null && order.slotIndex != slotIndex &&
                order.buildingId.Equals(buildingId, System.StringComparison.OrdinalIgnoreCase))) return false;
        }
        ProvinceConstructionOrder order = new ProvinceConstructionOrder
        {
            slotIndex = slotIndex, buildingId = buildingId,
            targetLevel = Mathf.Max(1, targetLevel), remainingTicks = Mathf.Max(0, constructionTicks),
            initiatedByAI = initiatedByAI
        };
        constructionOrders.Add(order);
        if (order.remainingTicks == 0) CompleteConstruction(order);
        return true;
    }

    public void ProcessConstructionTick()
    {
        if (IsOccupied) return;
        if (constructionOrders == null) return;
        for (int i = constructionOrders.Count - 1; i >= 0; i--)
        {
            ProvinceConstructionOrder order = constructionOrders[i];
            order.remainingTicks--;
            if (order.remainingTicks <= 0) CompleteConstruction(order);
        }
    }

    private void CompleteConstruction(ProvinceConstructionOrder order)
    {
        ProvinceBuilding building = GetBuildingInSlot(order.slotIndex);
        if (building == null)
        {
            int maximum = ProvinceBuilding.MaximumLevelFor(order.buildingId);
            buildings.Add(new ProvinceBuilding
            {
                definition = BuildingDefinition.Find(order.buildingId),
                id = order.buildingId, level = order.targetLevel, maxLevel = maximum, slotIndex = order.slotIndex
            });
        }
        else building.level = Mathf.Min(building.maxLevel, Mathf.Max(building.level, order.targetLevel));
        constructionOrders.Remove(order);
        ClampDevelopment();
        if (building != null && building.DefinitionGarrisonCapacity > 0 ||
            order.buildingId.Equals("Fort", System.StringComparison.OrdinalIgnoreCase) ||
            order.buildingId.Equals("Hillfort", System.StringComparison.OrdinalIgnoreCase))
            RefreshGarrisonForFort();
    }

    public List<Province> GrabAdjacents()
    {
        var a = new List<Province>();
        foreach(var b in AdjacentProvinces)
        {
            a.Add(Owners.Instance.provincedict[b]);
        }
        return a;
    }
    public void SetAdjacents()
    {
        //Debug.LogError("setting adjacents");
        AdjacentProvinces.Clear();
        
        foreach(var b in Owners.Instance.provincelist)
        {
            if(b == this)
            {
                continue;
            }
            if(Vector3.Distance(b.position, position) < 50)
            {
                AdjacentProvinces.Add(b.name);
            }
        }
        // if(AdjacentProvinces.Count < 5)
        // {
        //     foreach(var b in Owners.Instance.provincelist)
        //     {
        //         if(b == this)
        //         {
        //             continue;
        //         }
        //         if(Vector3.Distance(b.position, position) < 70)
        //         {
        //             AdjacentProvinces.Add(b.name);
        //         }
        //     }
        // }
        // if(AdjacentProvinces.Count < 5)
        // {
        //     foreach(var b in Owners.Instance.provincelist)
        //     {
        //         if(b == this)
        //         {
        //             continue;
        //         }
        //         if(Vector3.Distance(b.position, position) < 90)
        //         {
        //             AdjacentProvinces.Add(b.name);
        //         }
        //     }
        // }
    }

    public void CreateGarrison()
    {
        if (IsOccupied) { garrison = null; return; }
        garrison = ScriptableObject.CreateInstance<FieldArmy>();
        garrison.nation = nation;
        garrison.MaxArmySize = GetGarrisonCapacity();
        ReinforceGarrisonToCapacity();
    }

    public int GetGarrisonCapacity()
    {
        const int baseGarrisonSize = 3;
        const int troopsPerFortLevel = 3;
        ProvinceBuilding fort = GetBuilding("Fort");
        int capacity = baseGarrisonSize;
        foreach (ProvinceBuilding building in buildings)
            if (building != null && building.definition != null) capacity += building.DefinitionGarrisonCapacity;
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null && holding.definition != null)
                capacity += Mathf.Max(0, holding.definition.garrisonCapacity);
        if (fort != null && fort.definition == null)
            capacity += Mathf.Max(0, fort.level) * troopsPerFortLevel;
        return capacity;
    }

    public void RefreshGarrisonForFort()
    {
        if (garrison == null)
        {
            CreateGarrison();
            return;
        }
        garrison.nation = nation;
        garrison.MaxArmySize = GetGarrisonCapacity();
        ReinforceGarrisonToCapacity();
    }

    private void ReinforceGarrisonToCapacity()
    {
        if (garrison == null || nation == null || nation.faction == null) return;
        List<UnitSaveData> roster = NationContentResolver.ResolveUnits(nation)
            .ConvertAll(entry => entry != null ? entry.unit : null);
        roster.RemoveAll(unit => unit == null);
        if (roster == null || roster.Count == 0) return;

        int missing = Mathf.Max(0, garrison.MaxArmySize - garrison.GrabArmySize());
        int unitTypes = Mathf.Min(2, roster.Count);
        for (int i = 0; i < missing; i++)
        {
            UnitSaveData unit = roster[i % unitTypes];
            if (unit != null) garrison.AddTroop(unit, 1, true);
        }
    }

    public void EnsureMinimumGarrison(int minimum = 1)
    {
        if (IsOccupied) { garrison = null; return; }
        if (garrison == null) CreateGarrison();
        if (garrison == null || nation == null || nation.faction == null) return;
        garrison.nation = nation;
        garrison.MaxArmySize = GetGarrisonCapacity();
        int desired = Mathf.Clamp(minimum, 0, Mathf.Max(0, garrison.MaxArmySize));
        int missing = Mathf.Max(0, desired - garrison.GrabArmySize());
        if (missing == 0) return;

        List<UnitSaveData> roster = NationContentResolver.ResolveUnits(nation)
            .ConvertAll(entry => entry != null ? entry.unit : null);
        roster.RemoveAll(unit => unit == null);
        if (roster.Count == 0) return;

        int unitTypes = Mathf.Min(2, roster.Count);
        for (int i = 0; i < missing; i++)
            garrison.AddTroop(roster[i % unitTypes], 1, true);
    }

    public FieldArmyHolder SallyOut(FieldArmyHolder sally)
    {
        return Mapshower.Instance.SpawnArmy(this);
    }
    
    public void UpdatePopulation()
    {
        RebuildPopulationFromHoldings();
    }
    public void LosePopulation(int percentage)
    {
        if (holdings == null || holdings.Count == 0) return;
        int remove = Mathf.Clamp(Mathf.RoundToInt(holdings.Count * Mathf.Clamp(percentage, 0, 100) / 100f), 0, holdings.Count);
        for (int i = 0; i < remove; i++) holdings.RemoveAt(holdings.Count - 1);
        RebuildPopulationFromHoldings(); ReconcileLevyEntitlements();
    }
}
[System.Serializable]
public class Nation
{
    public string name;
    public Color32 ownerIdentity;
    public bool IsPlayer;
    public List<FieldArmyHolder> armies = new List<FieldArmyHolder>();
    // public List<Nation> Enemies;
    public Faction faction;
    [Header("Nation identity")]
    public CivilizationData civilization;
    public NationCultureData culture;
    public ReligionData religion;
    public NationalBrain nationalbrainy;
    [Header("Diplomacy")]
    [Tooltip("Nation name of this nation's tributary master. Empty means this nation is not a tributary ally.")]
    public string TributaryMasterName;
    [Tooltip("Nation names with which this nation currently has a peace treaty.")]
    public List<string> PeaceTreatyNationNames = new List<string>();
    [Tooltip("Nation names against which this nation has an active declared war.")]
    public List<string> WarNationNames = new List<string>();
    [HideInInspector] public int LastWarDeclarationTurn = -1000;
    [HideInInspector] public int NextAIDiplomacyTurn;
    [HideInInspector] public string PendingPeaceOfferFrom;
    [HideInInspector] public BasicPeaceTerms PendingPeaceTerms;
    [Tooltip("Cached nation-wide total of all regional manpower pools.")]
    public float Manpower = 0f;
    public int Gold = CampaignEconomy.StartingGold;
    public int ArmyNumber = 0;
    public bool IsPacifist =>
        NationContentResolver.HasFlag(this, "Pacificst") || NationContentResolver.HasFlag(this, "Pacifist");
    public bool SuppressesAIMilitaryRecruitment => IsPacifist;
    public int NextAIInfrastructureTurn;
    public int AIInfrastructureIntervalTurns = 12;
    public int NextAIEconomyTurn;
    public int NextAICoordinationTurn;
    public int LastGrossIncome;
    public int LastUnitUpkeep;
    public int UpkeepDebt;
    [Header("National laws")]
    public List<NationalLaw> laws = new List<NationalLaw>();
    [Header("National politics")]
    public List<PoliticalGroup> politicalGroups = new List<PoliticalGroup>();
    [Header("National allegiances")]
    public List<Allegiance> allegiances = new List<Allegiance>();
    [HideInInspector] public bool startingAllegiancesAssigned;
    public List<PoliticalProposal> politicalProposals = new List<PoliticalProposal>();
    public List<ActiveNationalEdict> activeEdicts = new List<ActiveNationalEdict>();
    public string latestPassedEdict;
    [HideInInspector] public int levyRecoveryBoostTicks;
    [HideInInspector] public int levyRecoveryBonusPerTick;
    [HideInInspector] public int LevyLawPermille = 200;
    [System.NonSerialized] private Dictionary<NationalLawEffectType, List<NationalLawEffect>> compiledLawEffects;
    [System.NonSerialized] private List<NationalClassRule> compiledClassRules;
    [System.NonSerialized] private bool identityLawsEnsured;
    [Tooltip("National laws and temporary effects can influence desired holding tags and their efficiency here.")]
    public List<HoldingTagModifier> holdingEconomyModifiers = new List<HoldingTagModifier>();
    [Min(1)] public int AIMinimumCampaignArmySize = 10;

    public void EnsureDefaultLaws()
    {
        if (laws == null) laws = new List<NationalLaw>();
        if (!identityLawsEnsured)
        {
            identityLawsEnsured = true;
            List<NationalLaw> identityLaws = NationContentResolver.ResolveLaws(this);
            foreach (NationalLaw identityLaw in identityLaws)
            {
                if (identityLaw == null || laws.Exists(law => law != null && string.Equals(law.id, identityLaw.id,
                    System.StringComparison.OrdinalIgnoreCase))) continue;
                laws.Add(identityLaw);
                compiledLawEffects = null;
                compiledClassRules = null;
            }
        }
        bool barbarianIdentity = civilization != null && string.Equals(civilization.name, "Barbarian",
            System.StringComparison.OrdinalIgnoreCase);
        if (barbarianIdentity && !laws.Exists(law => law != null && string.Equals(law.id, "kings_share",
            System.StringComparison.OrdinalIgnoreCase)))
        {
            laws.Add(NationalLawDefaults.KingsShare());
            compiledLawEffects = null; compiledClassRules = null;
        }
        bool romanIdentity = string.Equals(name, "Rome", System.StringComparison.OrdinalIgnoreCase);
        if (romanIdentity && !laws.Exists(law => law != null && string.Equals(law.id, "roman_muster_rolls",
            System.StringComparison.OrdinalIgnoreCase)))
        {
            laws.Add(NationalLawDefaults.RomanLevyRecovery());
            compiledLawEffects = null; compiledClassRules = null;
        }
        if (laws.Count == 0)
        {
            bool rome = string.Equals(name, "Rome", System.StringComparison.OrdinalIgnoreCase);
            bool carthage = string.Equals(name, "Carthage", System.StringComparison.OrdinalIgnoreCase);
            bool barbarian = civilization != null && string.Equals(civilization.name, "Barbarian",
                System.StringComparison.OrdinalIgnoreCase);
            if (rome)
                laws.Add(NationalLawDefaults.RepublicanLevy());
            else if (carthage)
            {
                laws.Add(NationalLawDefaults.Levy("punic_citizen_obligation", "Citizen Obligation", 100, false,
                    SocioEconomicClass.Citizen, NationalLawCultureScope.PrimaryCulture));
                laws.Add(NationalLawDefaults.Levy("punic_subject_muster", "Subject Muster", 200, false,
                    SocioEconomicClass.Freemen, NationalLawCultureScope.NonPrimaryCulture));
                laws.Add(NationalLawDefaults.MercenaryContracts());
            }
            else if (barbarian)
            {
                laws.Add(NationalLawDefaults.TribalMuster());
                laws.Add(NationalLawDefaults.WarriorRites());
            }
            else
                laws.Add(NationalLawDefaults.Levy("levy_conscription", "Levy Conscription",
                    Mathf.Clamp(LevyLawPermille, 0, 1000), true, SocioEconomicClass.Freemen));
        }
        if (string.Equals(name, "Carthage", System.StringComparison.OrdinalIgnoreCase) &&
            !laws.Exists(law => law != null && string.Equals(law.id, "carthaginian_subject_mustering",
                System.StringComparison.OrdinalIgnoreCase)))
        {
            laws.Add(NationalLawDefaults.CarthaginianManpowerRecovery());
            compiledLawEffects = null;
        }
        EnsureLawExtensions("roman_citizen_levy");
        EnsureLawExtensions("tribal_muster");
        if (compiledLawEffects == null || compiledClassRules == null) RebuildLawCache();
    }

    private void EnsureLawExtensions(string lawId)
    {
        NationalLaw law = laws.Find(candidate => candidate != null && string.Equals(candidate.id, lawId,
            System.StringComparison.OrdinalIgnoreCase));
        if (law == null || law.availableExtensions != null && law.availableExtensions.Count > 0) return;
        NationalLaw template = string.Equals(lawId, "roman_citizen_levy", System.StringComparison.OrdinalIgnoreCase)
            ? NationalLawDefaults.RepublicanLevy() : NationalLawDefaults.TribalMuster();
        law.availableExtensions = new List<NationalEdict>();
        if (template.availableExtensions != null) foreach (NationalEdict extension in template.availableExtensions)
            if (extension != null) law.availableExtensions.Add(extension.Clone());
    }

    public void ResetLawResolution()
    {
        identityLawsEnsured = false;
        compiledLawEffects = null;
        compiledClassRules = null;
    }

    public int GetHoldingLawAmount(NationalLawEffectType effect, ProvinceHolding holding,
        string sourceRegionId = null)
    {
        return Mathf.Clamp(ApplyLawModifiers(effect, 0, holding, CampaignUnitOrigin.Professional,
            sourceRegionId), 0, 1000);
    }

    public int ApplyLawModifiers(NationalLawEffectType effect, int baseValue, ProvinceHolding holding = null,
        CampaignUnitOrigin origin = CampaignUnitOrigin.Professional, string sourceRegionId = null)
    {
        EnsureDefaultLaws();
        int value = baseValue;
        if (compiledLawEffects.TryGetValue(effect, out List<NationalLawEffect> entries))
        {
            value = ApplyLawOperation(entries, NationalLawOperation.AddFlat, value, holding, origin, sourceRegionId);
            value = ApplyLawOperation(entries, NationalLawOperation.AddPercent, value, holding, origin, sourceRegionId);
            value = ApplyLawOperation(entries, NationalLawOperation.Multiply, value, holding, origin, sourceRegionId);
            value = ApplyLawOperation(entries, NationalLawOperation.Override, value, holding, origin, sourceRegionId);
        }
        value = ApplyActiveEdictModifiers(effect, value, holding, origin, sourceRegionId);
        return value;
    }

    private int ApplyActiveEdictModifiers(NationalLawEffectType effect, int value, ProvinceHolding holding,
        CampaignUnitOrigin origin, string sourceRegionId)
    {
        if (activeEdicts == null || activeEdicts.Count == 0) return value;
        value = ApplyActiveEdictOperation(effect, NationalLawOperation.AddFlat, value, holding, origin, sourceRegionId);
        value = ApplyActiveEdictOperation(effect, NationalLawOperation.AddPercent, value, holding, origin, sourceRegionId);
        value = ApplyActiveEdictOperation(effect, NationalLawOperation.Multiply, value, holding, origin, sourceRegionId);
        return ApplyActiveEdictOperation(effect, NationalLawOperation.Override, value, holding, origin, sourceRegionId);
    }

    private int ApplyActiveEdictOperation(NationalLawEffectType effectType, NationalLawOperation operation,
        int value, ProvinceHolding holding, CampaignUnitOrigin origin, string sourceRegionId)
    {
        int additive = 0;
        foreach (ActiveNationalEdict active in activeEdicts)
        {
            if (active == null || active.edict == null || active.remainingTicks <= 0 || active.edict.coreEffects == null) continue;
            foreach (NationalLawEffect effect in active.edict.coreEffects)
            {
                if (effect == null || effect.type != effectType || effect.operation != operation ||
                    !effect.AppliesTo(this, holding, origin, sourceRegionId)) continue;
                if (operation == NationalLawOperation.AddFlat || operation == NationalLawOperation.AddPercent)
                    additive += effect.amountPermille;
                else if (operation == NationalLawOperation.Multiply)
                    value = Mathf.RoundToInt(value * effect.amountPermille / 1000f);
                else value = effect.amountPermille;
            }
        }
        if (operation == NationalLawOperation.AddFlat) value += additive;
        else if (operation == NationalLawOperation.AddPercent) value = Mathf.RoundToInt(value * (1f + additive / 1000f));
        return value;
    }

    private int ApplyLawOperation(List<NationalLawEffect> entries, NationalLawOperation operation, int value,
        ProvinceHolding holding, CampaignUnitOrigin origin, string sourceRegionId)
    {
        int additiveAmount = 0;
        foreach (NationalLawEffect entry in entries)
        {
            if (entry == null || entry.operation != operation ||
                !entry.AppliesTo(this, holding, origin, sourceRegionId)) continue;
            if (operation == NationalLawOperation.AddFlat || operation == NationalLawOperation.AddPercent)
                additiveAmount += entry.amountPermille;
            else if (operation == NationalLawOperation.Multiply)
                value = Mathf.RoundToInt(value * entry.amountPermille / 1000f);
            else value = entry.amountPermille;
        }
        if (operation == NationalLawOperation.AddFlat) value += additiveAmount;
        else if (operation == NationalLawOperation.AddPercent)
            value = Mathf.RoundToInt(value * (1f + additiveAmount / 1000f));
        return value;
    }

    public void RebuildLawCache()
    {
        compiledLawEffects = new Dictionary<NationalLawEffectType, List<NationalLawEffect>>();
        compiledClassRules = new List<NationalClassRule>();
        if (laws == null) return;
        foreach (NationalLaw law in laws)
        {
            if (law == null) continue;
            law.EnsureEffectsMigrated();
            foreach (NationalLawEffect entry in law.effects)
            {
                if (entry == null) continue;
                if (!compiledLawEffects.TryGetValue(entry.type, out List<NationalLawEffect> list))
                { list = new List<NationalLawEffect>(); compiledLawEffects.Add(entry.type, list); }
                list.Add(entry);
            }
            if (law.classRules != null)
                foreach (NationalClassRule rule in law.classRules) if (rule != null) compiledClassRules.Add(rule);
        }
    }

    public bool ApplyHoldingClassLaws(ProvinceHolding holding)
    {
        if (holding == null) return false;
        EnsureDefaultLaws();
        holding.socioEconomicClass = SocioEconomicClassRules.Normalize(holding.socioEconomicClass);
        bool changed = false;
        if (compiledClassRules != null)
            foreach (NationalClassRule rule in compiledClassRules) if (rule != null && rule.Apply(this, holding)) changed = true;
        return changed;
    }

    public bool IsArmyCombatReady(FieldArmyHolder army, bool includeQueuedRecruitment = false)
    {
        if (army == null || army.fieldArmy == null) return false;
        int maximum = Mathf.Max(1, army.fieldArmy.MaxArmySize);
        int size = army.fieldArmy.GrabArmySize();
        if (includeQueuedRecruitment) size += army.fieldArmy.GrabQueuedArmySize();
        if (size >= GetAIDesiredArmySize(army)) return true;
        // An army that cannot currently recruit is allowed to campaign understrength.
        // Without this escape hatch, RecruitTroops permanently outranks conquest.
        return size > 0 && Owners.Instance != null &&
            Owners.Instance.turncounter < army.AIAcceptedUnderstrengthUntilTurn;
    }

    public void AcceptFailedAIRecruitment(FieldArmyHolder army)
    {
        if (army == null) return;
        int now = Owners.Instance != null ? Owners.Instance.turncounter : 0;
        int retryDelay = Mathf.Max(8, army.AIReinforcementIntervalTurns * 2);
        army.AIAcceptedUnderstrengthUntilTurn = Mathf.Max(army.AIAcceptedUnderstrengthUntilTurn,
            Mathf.Max(now + retryDelay, army.NextAIReinforcementTurn));
    }

    public int GetAIDesiredArmySize(FieldArmyHolder army)
    {
        if (army == null || army.fieldArmy == null) return Mathf.Max(1, AIMinimumCampaignArmySize);
        int maximum = Mathf.Max(1, army.fieldArmy.MaxArmySize);
        if (IsPacifist) return Mathf.Min(6, maximum);
        if (army.AIDesiredArmySize <= 0)
        {
            int minimum = Mathf.Clamp(AIMinimumCampaignArmySize - 2, 6, maximum);
            int maximumGoal = Mathf.Clamp(AIMinimumCampaignArmySize + 6, minimum, maximum);
            army.AIDesiredArmySize = minimum + StableAIHash(name + "|" + army.NetworkArmyId + "|" +
                army.gameObject.name) % Mathf.Max(1, maximumGoal - minimum + 1);
        }
        Province position = army.GrabNearestProvince();
        int nearbyThreat = GetHostileFieldArmyStrengthNear(position);
        int strategicGoal = Mathf.Max(army.AIDesiredArmySize, nearbyThreat > 0 ? nearbyThreat + 2 : 0);
        if (Gold > 1000 && LastGrossIncome > LastUnitUpkeep) strategicGoal += 2;
        return Mathf.Clamp(strategicGoal, 1, maximum);
    }

    private static int StableAIHash(string value)
    {
        unchecked
        {
            int hash = 17;
            if (value != null) for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash & int.MaxValue;
        }
    }

    public int GetHostileFieldArmyStrengthNear(Province province, float radius = 75f)
    {
        if (province == null || Owners.Instance == null) return 0;
        int strongest = 0;
        foreach (FieldArmyHolder other in Owners.Instance.armylist)
        {
            if (other == null || other.fieldArmy == null || other.fieldArmy.nation == null ||
                !DiplomacySystem.AreHostile(other.fieldArmy.nation, this) || other.flaglist.Contains("Battle")) continue;
            if (other.GrabDistanceToProvince(province) <= radius)
                strongest = Mathf.Max(strongest, other.fieldArmy.GrabArmySize());
        }
        return strongest;
    }

    public void TakeTurn()
    {
        PoliticalProposalSystem.ProcessTurn(this, Owners.Instance != null ? Owners.Instance.turncounter : 0);
        Dictionary<string, float> regionalIncome = new Dictionary<string, float>();
        int provincialUpkeep = 0;

        // Economy used to traverse the complete province list separately for each
        // resource. On WebGL that concentrated several full holding/building scans
        // into one frame per nation. Accumulate all provincial values in one pass.
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.IsOccupied || province.nation != this) continue;
            string regionKey = !string.IsNullOrWhiteSpace(province.region) ? province.region : "Province:" + province.name;
            regionalIncome[regionKey] = regionalIncome.TryGetValue(regionKey, out float income)
                ? income + province.GetGoldIncomeUnrounded()
                : province.GetGoldIncomeUnrounded();
            provincialUpkeep += province.GetBuildingUpkeep();
        }
        int grossIncome = 0;
        foreach (float value in regionalIncome.Values) grossIncome += Mathf.RoundToInt(value);
        Nation tributaryMaster = DiplomacySystem.FindMaster(this);
        int tribute = tributaryMaster != null ? Mathf.Max(0, Mathf.FloorToInt(grossIncome * 0.25f)) : 0;
        if (tribute > 0) tributaryMaster.Gold += tribute;
        grossIncome -= tribute;
        RefreshManpowerTotal();
        LastGrossIncome = grossIncome;
        LastUnitUpkeep = provincialUpkeep;
        foreach (FieldArmyHolder army in armies)
            if (army != null && army.fieldArmy != null) LastUnitUpkeep += army.fieldArmy.GetUpkeep();
        int netIncome = LastGrossIncome - LastUnitUpkeep;
        if (netIncome >= 0) Gold += netIncome;
        else
        {
            int paid = Mathf.Min(Gold, -netIncome);
            Gold -= paid;
            UpkeepDebt += -netIncome - paid;
        }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            IsPlayer = CampaignNetworkPlayer.IsNationPlayerControlled(name);
        if (IsPlayer) return;
        // Braindead reserves a nation as expansion space: it accumulates normal
        // campaign resources but takes no autonomous strategic actions.
        if (NationContentResolver.HasFlag(this, "Braindead")) return;
        ProcessAIDiplomacy();
        if (IsPacifist) CoordinatePacifistDefense();
        else CoordinateCampaignArmies();
        DevelopEconomicInfrastructure();
        DevelopMilitaryInfrastructure();
        ReinforceMostUnderstrengthArmy();
        if (!IsPacifist) OrderIdleFullArmiesToFrontier();
    }

    public float RefreshManpowerTotal()
    {
        Manpower = 0f;
        if (Owners.Instance == null || Owners.Instance.regionlist == null) return Manpower;
        foreach (CampaignRegion region in Owners.Instance.regionlist)
            if (region != null) Manpower += region.GetManpower(this);
        return Manpower;
    }

    public float ManpowerCapacity()
    {
        float total = 0f;
        if (Owners.Instance == null || Owners.Instance.regionlist == null) return total;
        foreach (CampaignRegion region in Owners.Instance.regionlist)
            if (region != null) total += region.GetManpowerCapacity(this);
        return total;
    }

    public bool TrySpendManpower(Province source, float amount = 1f)
    {
        if (source == null || source.nation != this || Owners.Instance == null) return false;
        amount = Mathf.Max(0f, amount);
        List<CampaignRegion> accessible = GetAccessibleManpowerRegions(source);
        float available = 0f;
        foreach (CampaignRegion region in accessible) available += region.GetManpower(this);
        if (available + 0.0001f < amount) return false;

        float remaining = amount;
        foreach (CampaignRegion region in accessible)
        {
            float spent = Mathf.Min(remaining, region.GetManpower(this));
            if (spent <= 0f) continue;
            region.TrySpendManpower(this, spent);
            remaining -= spent;
            if (remaining <= 0.0001f) break;
        }
        RefreshManpowerTotal();
        return remaining <= 0.0001f;
    }

    public void RefundManpower(Province source, float amount = 1f)
    {
        if (source == null || Owners.Instance == null) return;
        float remaining = Mathf.Max(0f, amount);
        foreach (CampaignRegion region in GetAccessibleManpowerRegions(source))
        {
            float room = Mathf.Max(0f, region.GetManpowerCapacity(this) - region.GetManpower(this));
            float refunded = Mathf.Min(remaining, room);
            if (refunded <= 0f) continue;
            region.AddManpower(this, refunded);
            remaining -= refunded;
            if (remaining <= 0.0001f) break;
        }
        RefreshManpowerTotal();
    }

    public bool AcceptPendingPeaceOffer()
    {
        if (string.IsNullOrWhiteSpace(PendingPeaceOfferFrom) || Owners.Instance == null) return false;
        Nation proposer = Owners.Instance.nationlist.Find(candidate => candidate != null &&
            string.Equals(candidate.name, PendingPeaceOfferFrom, System.StringComparison.OrdinalIgnoreCase));
        PendingPeaceOfferFrom = string.Empty;
        if (proposer == null) return false;
        int playerOccupations = Owners.Instance.provincelist.FindAll(province => province != null &&
            province.nation == proposer && province.OccupyingNation == this).Count;
        int proposerOccupations = Owners.Instance.provincelist.FindAll(province => province != null &&
            province.nation == this && province.OccupyingNation == proposer).Count;
        Nation victor = playerOccupations > proposerOccupations ? this : proposer;
        Nation defeated = victor == this ? proposer : this;
        return CampaignPeaceSystem.Resolve(victor, defeated, PendingPeaceTerms);
    }

    public void RejectPendingPeaceOffer()
    {
        PendingPeaceOfferFrom = string.Empty;
    }

    private void ProcessAIDiplomacy()
    {
        if (Owners.Instance == null || Owners.Instance.turncounter < NextAIDiplomacyTurn) return;
        int now = Owners.Instance.turncounter;
        NextAIDiplomacyTurn = now + 24 + StableAIHash(name + "|diplomacy|" + now / 24) % 9;

        List<Nation> enemies = Owners.Instance.nationlist.FindAll(candidate => candidate != null &&
            candidate != this && DiplomacySystem.AreAtWar(this, candidate));
        foreach (Nation enemy in enemies)
        {
            int duration = now - Mathf.Max(LastWarDeclarationTurn, enemy.LastWarDeclarationTurn);
            int ours = Owners.Instance.provincelist.FindAll(province => province != null &&
                province.nation == enemy && province.OccupyingNation == this).Count;
            int theirs = Owners.Instance.provincelist.FindAll(province => province != null &&
                province.nation == this && province.OccupyingNation == enemy).Count;
            if (duration < 40 || ours == 0 && theirs == 0 && duration < 80) continue;

            Nation victor = ours > theirs ? this : theirs > ours ? enemy : null;
            Nation defeated = victor == this ? enemy : victor == enemy ? this : null;
            BasicPeaceTerms terms = victor != null
                ? CampaignPeaceSystem.ChooseBasicAITerms(victor, defeated) : BasicPeaceTerms.WhitePeace;
            Nation proposer = victor ?? this;
            Nation recipient = defeated ?? enemy;
            if (enemy.IsPlayer)
            {
                enemy.PendingPeaceOfferFrom = name;
                enemy.PendingPeaceTerms = terms;
                Debug.Log(name + " offers " + enemy.name + " peace: " + terms + ".");
            }
            else if (!IsPlayer && string.CompareOrdinal(name, enemy.name) < 0)
                CampaignPeaceSystem.Resolve(proposer, recipient, terms);
            return;
        }

        // Keep early diplomacy legible: one active war per nation and declarations
        // only against nations sharing a land border with its controlled territory.
        if (DiplomacySystem.FindMaster(this) != null || IsPacifist) return;
        if (enemies.Count > 0 || now < 24 || StableAIHash(name + "|war|" + now / 24) % 3 != 0) return;
        List<Nation> candidates = new List<Nation>();
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || !DiplomacySystem.HasMasterAccess(this, province.ControllerNation)) continue;
            foreach (Province adjacent in province.GrabAdjacents())
                if (adjacent != null && adjacent.ControllerNation != null &&
                    !DiplomacySystem.AreFriendly(this, adjacent.ControllerNation) &&
                    !candidates.Contains(adjacent.ControllerNation)) candidates.Add(adjacent.ControllerNation);
        }
        candidates.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        Nation target = null;
        int ownStrength = DiplomaticMilitaryStrength(this);
        foreach (Nation candidate in candidates)
        {
            if (DiplomacySystem.AreAtWar(this, candidate)) continue;
            if (ownStrength * 5 < DiplomaticMilitaryStrength(candidate) * 4) continue;
            target = candidate;
            break;
        }
        if (target != null) DiplomacySystem.DeclareWar(this, target);
    }

    private static int DiplomaticMilitaryStrength(Nation nation)
    {
        if (nation == null || Owners.Instance == null) return 0;
        int strength = 0;
        foreach (FieldArmyHolder army in Owners.Instance.armylist)
        {
            if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null) continue;
            Nation armyNation = army.fieldArmy.nation;
            if (armyNation == nation || DiplomacySystem.IsTributarySubjectOf(armyNation, nation) ||
                DiplomacySystem.IsTributarySubjectOf(nation, armyNation))
                strength += army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize();
        }
        return strength;
    }

    private List<CampaignRegion> GetAccessibleManpowerRegions(Province source)
    {
        List<CampaignRegion> result = new List<CampaignRegion>();
        if (source == null || Owners.Instance == null) return result;
        CampaignRegion local = Owners.Instance.CallRegionByString(source.region);
        if (local == null) return result;
        result.Add(local);

        List<CampaignRegion> neighboring = new List<CampaignRegion>();
        foreach (Province localProvince in local.provincelist)
        {
            if (localProvince == null || localProvince.nation != this || localProvince.AdjacentProvinces == null) continue;
            foreach (string adjacentName in localProvince.AdjacentProvinces)
            {
                if (string.IsNullOrWhiteSpace(adjacentName) || Owners.Instance.provincedict == null ||
                    !Owners.Instance.provincedict.TryGetValue(adjacentName, out Province adjacent) ||
                    adjacent == null || adjacent.nation != this || string.IsNullOrWhiteSpace(adjacent.region)) continue;
                CampaignRegion adjacentRegion = Owners.Instance.CallRegionByString(adjacent.region);
                if (adjacentRegion != null && adjacentRegion != local && !neighboring.Contains(adjacentRegion))
                    neighboring.Add(adjacentRegion);
            }
        }
        neighboring.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        result.AddRange(neighboring);
        return result;
    }

    private void CoordinateCampaignArmies()
    {
        int now = Owners.Instance != null ? Owners.Instance.turncounter : 0;
        if (now < NextAICoordinationTurn) return;
        NextAICoordinationTurn = now + 4;
        armies.RemoveAll(army => army == null || army.fieldArmy == null);

        for (int i = armies.Count - 1; i >= 0; i--)
            if (CanCoordinateArmy(armies[i]) && armies[i].fieldArmy.GrabArmySize() <= 0)
                DisbandAIArmy(armies[i]);

        // First consolidate armies that have actually reached the same province.
        for (int left = 0; left < armies.Count; left++)
        {
            FieldArmyHolder first = armies[left];
            if (!CanCoordinateArmy(first)) continue;
            Province firstProvince = first.GrabNearestProvince();
            for (int right = left + 1; right < armies.Count; right++)
            {
                FieldArmyHolder second = armies[right];
                if (!CanCoordinateArmy(second) || second.GrabNearestProvince() != firstProvince ||
                    second.fieldArmy.nation != first.fieldArmy.nation ||
                    (second.transform.position - first.transform.position).sqrMagnitude > 64f) continue;
                int firstSize = first.fieldArmy.GrabArmySize();
                int secondSize = second.fieldArmy.GrabArmySize();
                FieldArmyHolder receiver = firstSize >= secondSize ? first : second;
                FieldArmyHolder donor = receiver == first ? second : first;
                if (firstSize + secondSize > receiver.fieldArmy.MaxArmySize) continue;
                MergeAIArmies(receiver, donor);
                if (donor == first) { left--; break; }
                right--;
            }
        }

        List<FieldArmyHolder> snapshot = new List<FieldArmyHolder>(armies);
        foreach (FieldArmyHolder army in snapshot)
        {
            if (!CanCoordinateArmy(army) || !army.IsTargetNull() || army.fieldArmy.GrabArmySize() <= 0) continue;
            int strength = army.fieldArmy.GrabArmySize();

            // Take an affordable nearby fight instead of ignoring a vulnerable enemy army.
            FieldArmyHolder enemy = FindNearestCampaignArmy(army, false, 90f);
            if (enemy != null && strength * 4 >= enemy.fieldArmy.GrabArmySize() * 3)
            {
                IssueAIMove(army, enemy.GrabNearestProvince());
                continue;
            }

            // Understrength forces rally with a compatible friendly army; other idle
            // forces reinforce a friendly operation by pursuing the same objective.
            FieldArmyHolder friendly = FindNearestCampaignArmy(army, true, 240f);
            if (friendly == null) continue;
            int combined = strength + friendly.fieldArmy.GrabArmySize();
            bool canMerge = friendly.fieldArmy.nation == army.fieldArmy.nation &&
                combined <= friendly.fieldArmy.MaxArmySize;
            Province supportTarget = canMerge ? friendly.GrabNearestProvince() : friendly.TargetProvince;
            if (supportTarget != null && (canMerge || !friendly.IsTargetNull())) IssueAIMove(army, supportTarget);
        }
    }

    private void CoordinatePacifistDefense()
    {
        if (Owners.Instance == null) return;
        int now = Owners.Instance.turncounter;
        if (now < NextAICoordinationTurn) return;
        NextAICoordinationTurn = now + 4;
        armies.RemoveAll(army => army == null || army.fieldArmy == null);
        for (int i = armies.Count - 1; i >= 0; i--)
            if (CanCoordinateArmy(armies[i]) && armies[i].fieldArmy.GrabArmySize() <= 0)
                DisbandAIArmy(armies[i]);

        List<Province> occupiedHome = Owners.Instance.provincelist.FindAll(province => province != null &&
            province.nation == this && province.IsOccupied && DiplomacySystem.AreHostile(province.ControllerNation, this));
        List<Province> threatenedHome = new List<Province>();
        foreach (FieldArmyHolder enemy in Owners.Instance.armylist)
        {
            if (enemy == null || enemy.fieldArmy == null || enemy.fieldArmy.nation == null ||
                !DiplomacySystem.AreHostile(enemy.fieldArmy.nation, this)) continue;
            Province position = enemy.GrabNearestProvince();
            if (position != null && position.nation == this && !threatenedHome.Contains(position)) threatenedHome.Add(position);
        }
        List<Province> objectives = occupiedHome.Count > 0 ? occupiedHome : threatenedHome;
        if (objectives.Count == 0) return;
        objectives.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        foreach (FieldArmyHolder army in armies)
        {
            if (!CanCoordinateArmy(army) || !army.IsTargetNull() || !IsArmyCombatReady(army, true)) continue;
            Province target = null;
            float bestDistance = float.MaxValue;
            foreach (Province objective in objectives)
            {
                float distance = army.GrabDistanceToProvince(objective);
                if (distance < bestDistance) { target = objective; bestDistance = distance; }
            }
            if (target != null) IssueAIMove(army, target);
        }
    }

    private static bool CanCoordinateArmy(FieldArmyHolder army)
    {
        return army != null && !army.IsHumanControlled && army.fieldArmy != null &&
            (army.flaglist == null || !army.flaglist.Contains("Battle")) &&
            (army.fieldArmy.recruitmentOrders == null || army.fieldArmy.recruitmentOrders.Count == 0);
    }

    private FieldArmyHolder FindNearestCampaignArmy(FieldArmyHolder origin, bool friendly, float maximumDistance)
    {
        if (origin == null || Owners.Instance == null) return null;
        FieldArmyHolder best = null;
        float bestDistance = maximumDistance * maximumDistance;
        foreach (FieldArmyHolder candidate in Owners.Instance.armylist)
        {
            if (candidate == origin || candidate == null || candidate.fieldArmy == null ||
                candidate.fieldArmy.nation == null || candidate.fieldArmy.GrabArmySize() <= 0) continue;
            bool allied = DiplomacySystem.AreFriendly(candidate.fieldArmy.nation, this);
            if (allied != friendly) continue;
            float distance = (candidate.transform.position - origin.transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static void IssueAIMove(FieldArmyHolder army, Province target)
    {
        if (army == null || target == null) return;
        army.SetTarget(target);
        if (army.generalbrain != null) army.generalbrain.NewGoal("MoveArmy");
    }

    private void MergeAIArmies(FieldArmyHolder receiver, FieldArmyHolder donor)
    {
        if (receiver == null || donor == null || receiver.fieldArmy == null || donor.fieldArmy == null) return;
        donor.fieldArmy.ReconcileFormationRecords();
        List<ArmyFormationRecord> records = new List<ArmyFormationRecord>(donor.fieldArmy.formationRecords);
        foreach (ArmyFormationRecord record in records)
            if (record != null && record.unit != null)
                receiver.fieldArmy.AddTroop(record.unit, 1, true, record.origin, record.entitlementId);

        string donorId = donor.NetworkArmyId;
        string receiverId = receiver.NetworkArmyId;
        if (Owners.Instance != null && !string.IsNullOrEmpty(donorId))
            foreach (Province province in Owners.Instance.provincelist)
                if (province != null && province.levyEntitlements != null)
                    foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
                        if (entitlement != null && entitlement.raisedArmyId == donorId)
                            entitlement.raisedArmyId = receiverId;

        // Clear after transfer so OnDestroy does not treat merged levies as casualties.
        donor.fieldArmy.formationRecords.Clear();
        donor.fieldArmy.USDReserves.Clear();
        donor.generalbrain?.CancelCurrentGoal();
        DisbandAIArmy(donor);
    }

    private void DisbandAIArmy(FieldArmyHolder army)
    {
        if (army == null) return;
        armies.Remove(army);
        if (Owners.Instance != null) Owners.Instance.armylist.Remove(army);
        UnityEngine.Object.Destroy(army.gameObject);
    }

    private void DevelopEconomicInfrastructure()
    {
        if (!NationContentResolver.HasBuilding(this, "Farm")) return;
        if (Owners.Instance.turncounter < NextAIEconomyTurn) return;
        Province selected = null; int lowestFarm = int.MaxValue;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.IsOccupied || province.nation != this) continue;
            ProvinceBuilding farm = province.GetBuilding("Farm"); int level = farm != null ? farm.level : 0;
            if (level < ProvinceBuilding.StandardMaximumLevel && level < lowestFarm) { lowestFarm = level; selected = province; }
        }
        if (selected == null) return;
        ProvinceBuilding existing = selected.GetBuilding("Farm"); int targetLevel = existing != null ? existing.level + 1 : 1;
        int cost = CampaignEconomy.BuildingGoldCost("Farm", targetLevel);
        // Retain enough money to recruit at least one ordinary formation.
        if (Gold < cost + 100) return;
        int slot = existing != null ? existing.slotIndex : FirstFreeBuildingSlot(selected);
        if (slot < 0 || !selected.BeginBuildingConstruction(slot, "Farm", targetLevel,
            BuildingDefinition.ConstructionTicks("Farm", targetLevel), true)) return;
        Gold -= cost; NextAIEconomyTurn = Owners.Instance.turncounter + 15;
    }

    private static int FirstFreeBuildingSlot(Province province)
    {
        for (int slot = 0; slot < 4; slot++)
            if (province.GetBuildingInSlot(slot) == null &&
                (province.constructionOrders == null || !province.constructionOrders.Exists(order => order != null && order.slotIndex == slot)))
                return slot;
        return -1;
    }

    private void DevelopMilitaryInfrastructure()
    {
        if (IsPlayer || faction == null || Owners.Instance.turncounter < NextAIInfrastructureTurn) return;

        string selectedBuildingId = null;
        Province selectedProvince = null;
        ProvinceBuilding selectedExisting = null;
        float bestValue = 0f;
        foreach (string buildingId in NationContentResolver.ResolveBuildings(this))
        {
            if (string.Equals(buildingId, "Farm", System.StringComparison.OrdinalIgnoreCase)) continue;
            bool alreadyConstructing = Owners.Instance.provincelist.Exists(province =>
                province != null && !province.IsOccupied && province.nation == this && province.constructionOrders != null &&
                province.constructionOrders.Exists(order => order != null &&
                    string.Equals(order.buildingId, buildingId, System.StringComparison.OrdinalIgnoreCase)));
            if (alreadyConstructing) continue;

            foreach (Province province in Owners.Instance.provincelist)
            {
                if (province == null || province.IsOccupied || province.nation != this) continue;
                ProvinceBuilding existing = province.GetBuilding(buildingId);
                int candidateLevel = existing != null ? existing.level + 1 : 1;
                if ((existing == null && FirstFreeBuildingSlot(province) < 0) ||
                    !NationContentResolver.CanConstructBuildingLevel(this, buildingId, candidateLevel)) continue;
                float value = EvaluateBuildingLevel(province, buildingId, candidateLevel, existing);
                if (value <= bestValue) continue;
                bestValue = value;
                selectedBuildingId = buildingId;
                selectedProvince = province;
                selectedExisting = existing;
            }
        }

        if (selectedProvince == null || string.IsNullOrEmpty(selectedBuildingId)) return;
        int selectedTargetLevel = selectedExisting != null ? selectedExisting.level + 1 : 1;
        int slot = selectedExisting != null ? selectedExisting.slotIndex : FirstFreeBuildingSlot(selectedProvince);
        int goldCost = CampaignEconomy.BuildingGoldCost(selectedBuildingId, selectedTargetLevel);
        if (Gold < goldCost || !selectedProvince.BeginBuildingConstruction(slot, selectedBuildingId, selectedTargetLevel,
            BuildingDefinition.ConstructionTicks(selectedBuildingId, selectedTargetLevel), true)) return;

        Gold -= goldCost;
        if (selectedBuildingId.Equals("Barracks", System.StringComparison.OrdinalIgnoreCase))
            faction.BarracksLevel = Mathf.Max(faction.BarracksLevel, selectedTargetLevel);
        else if (selectedBuildingId.IndexOf("Mercenary", System.StringComparison.OrdinalIgnoreCase) >= 0)
            faction.MercLevel = Mathf.Max(faction.MercLevel, selectedTargetLevel);
        NextAIInfrastructureTurn = Owners.Instance.turncounter + Mathf.Max(1, AIInfrastructureIntervalTurns);
    }

    private float EvaluateBuildingLevel(Province province, string buildingId, int targetLevel, ProvinceBuilding existing)
    {
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        BuildingLevelDefinition level = definition != null ? definition.GetLevel(targetLevel) : null;
        float benefit = 0f;
        if (level != null)
        {
            CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
            float incomeEfficiency = (campaignRegion != null ? Mathf.Clamp(campaignRegion.GetLoyalty(this), 0f, 100f) : 100f) / 100f;
            benefit += Mathf.Max(0, level.goldIncome) * incomeEfficiency * (LastGrossIncome < LastUnitUpkeep ? 16f : 8f);
            benefit += Mathf.Max(0, level.garrisonCapacity) * 8f;
            benefit += level.unitUnlocks != null ? level.unitUnlocks.Count * 70f : 0f;
            benefit += level.flags != null ? level.flags.Count * 45f : 0f;
            benefit += level.displayedEffects != null ? level.displayedEffects.Count * 30f : 0f;
        }

        foreach (NationUnitEntry entry in NationContentResolver.ResolveUnits(this))
            if (entry != null && entry.unit != null && entry.minimumBuildingLevel == targetLevel &&
                string.Equals(entry.RequiredBuildingId, buildingId, System.StringComparison.OrdinalIgnoreCase))
                benefit += 55f + NationContentResolver.GetUnitTier(this, entry.unit) * 20f;

        ProvinceBuilding projected = new ProvinceBuilding
        {
            definition = definition, id = buildingId, level = targetLevel,
            maxLevel = definition != null ? definition.maximumLevel : ProvinceBuilding.MaximumLevelFor(buildingId),
            slotIndex = existing != null ? existing.slotIndex : FirstFreeBuildingSlot(province)
        };
        if (benefit <= 0f) return 0f;
        int cost = CampaignEconomy.BuildingGoldCost(buildingId, targetLevel);
        int ticks = BuildingDefinition.ConstructionTicks(buildingId, targetLevel);
        float affordability = Gold >= cost + 100 ? 1f : Gold >= cost ? .55f : 0f;
        return benefit * affordability / (1f + cost / 250f + ticks / 30f);
    }

    private void OrderIdleFullArmiesToFrontier()
    {
        if (IsPlayer) return;
        List<Province> hostile = Owners.Instance.provincelist.FindAll(province => province != null &&
            DiplomacySystem.AreHostile(province.ControllerNation, this));
        if (hostile.Count == 0) return;
        List<Province> frontier = hostile.FindAll(province =>
        {
            List<Province> adjacent = province.GrabAdjacents();
            return adjacent != null && adjacent.Exists(neighbor => neighbor != null &&
                DiplomacySystem.HasMasterAccess(this, neighbor.ControllerNation));
        });
        List<Province> candidates = frontier.Count > 0 ? frontier : hostile;
        foreach (FieldArmyHolder army in armies)
        {
            if (army == null || army.IsHumanControlled || army.fieldArmy == null ||
                army.flaglist.Contains("Battle") || !army.IsTargetNull()) continue;
            // Each army has its own affordable campaign goal instead of a universal ten-unit threshold.
            if (!IsArmyCombatReady(army) || army.fieldArmy.GrabQueuedArmySize() > 0) continue;
            Province targetProvince = null;
            float bestDistance = float.MaxValue;
            foreach (Province candidate in candidates)
            {
                float distance = army.GrabDistanceToProvince(candidate);
                int enemyStrength = GetHostileFieldArmyStrengthNear(candidate);
                int danger = Mathf.Max(0, enemyStrength - army.fieldArmy.GrabArmySize());
                float riskAdjustedDistance = distance + danger * 50f;
                if (riskAdjustedDistance < bestDistance || Mathf.Approximately(riskAdjustedDistance, bestDistance) &&
                    (targetProvince == null || string.CompareOrdinal(candidate.name, targetProvince.name) < 0))
                { targetProvince = candidate; bestDistance = riskAdjustedDistance; }
            }
            if (targetProvince == null) continue;
            army.SetTarget(targetProvince);
            army.TargetProvince = targetProvince;
            army.generalbrain.NewGoal("MoveArmy");
        }
    }
    public bool ReinforceMostUnderstrengthArmy()
    {
        FieldArmyHolder army = null;
        float lowestStrength = 2f;
        foreach (FieldArmyHolder candidate in armies)
        {
            if (candidate == null || candidate.IsHumanControlled || candidate.fieldArmy == null) continue;
            int goal = GetAIDesiredArmySize(candidate);
            float strength = goal <= 0 ? 1f :
                (float)(candidate.fieldArmy.GrabArmySize() + candidate.fieldArmy.GrabQueuedArmySize()) / goal;
            if (strength < lowestStrength)
            {
                lowestStrength = strength;
                army = candidate;
            }
        }
        return army != null && ReinforceArmy(army);
    }
    public bool ReinforceArmy(FieldArmyHolder army)
    {
        if (army == null || army.fieldArmy == null || !army.IsTargetNull() ||
            army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize() >= GetAIDesiredArmySize(army)) return false;
        if (Owners.Instance.turncounter < army.NextAIReinforcementTurn) return false;
        Province province = army.GrabNearestProvince();
        Province tributarySource = FindTributaryReinforcementProvince(army);
        if (tributarySource != null && (province == null || province.nation == this))
        {
            army.SetTarget(tributarySource);
            army.TargetProvince = tributarySource;
            return false;
        }
        if (province != null && DiplomacySystem.HasMasterAccess(this, province.nation))
        {
            Nation rosterNation = province.nation;
            bool tributaryRecruitment = rosterNation != this;
            List<UnitSaveData> units = province.GetRecruitableRegionUnits(rosterNation);
            int targetSize = GetAIDesiredArmySize(army);
            int professionalGoalPercent = 40 + StableAIHash(name + "|professionals|" + army.NetworkArmyId + "|" +
                army.gameObject.name) % 31;
            int professionalGoal = Mathf.CeilToInt(targetSize * professionalGoalPercent / 100f);
            int professionals = army.fieldArmy.CountFormations(CampaignUnitOrigin.Professional) +
                army.fieldArmy.GrabQueuedArmySize();
            bool prioritizeProfessionals = professionals < professionalGoal;
            int currentSize = army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize();
            int missing = Mathf.Max(1, targetSize - currentSize);
            int nearbyThreat = GetHostileFieldArmyStrengthNear(province);
            bool emergency = nearbyThreat > currentSize;
            bool levyHeavyCallup = IsAILevyHeavy(army);
            if (prioritizeProfessionals && !emergency && !levyHeavyCallup &&
                TryQueueAIProfessional(army, units, rosterNation)) return true;
            int levyBatch = emergency || levyHeavyCallup ? missing : Mathf.Min(missing, 1 +
                StableAIHash(name + "|levy-batch|" + army.gameObject.name + "|" +
                    (Owners.Instance.turncounter / Mathf.Max(1, army.AIReinforcementIntervalTurns))) % 3);
            if (!tributaryRecruitment && province.RaiseBestAIRegionLevies(army, levyBatch) > 0)
            {
                army.NextAIReinforcementTurn = Owners.Instance.turncounter + Mathf.Max(1, army.AIReinforcementIntervalTurns);
                return true;
            }
            if (TryQueueAIProfessional(army, units, rosterNation)) return true;
        }

        if (army.IsTargetNull())
        {
            Province recruitmentProvince = FindReinforcementProvince(army, true);
            if (recruitmentProvince == null) recruitmentProvince = FindReinforcementProvince(army, false);
            if (recruitmentProvince != null && recruitmentProvince != province)
            {
                army.SetTarget(recruitmentProvince);
                army.TargetProvince = recruitmentProvince;
            }
        }
        return false;
    }

    private Province FindTributaryReinforcementProvince(FieldArmyHolder army)
    {
        if (army == null || army.fieldArmy == null || Owners.Instance == null) return null;
        int desiredSize = GetAIDesiredArmySize(army);
        int desiredTributaries = Mathf.Max(1, Mathf.CeilToInt(desiredSize * 0.3f));
        int tributaries = army.fieldArmy.CountFormations(CampaignUnitOrigin.Mercenary);
        if (army.fieldArmy.recruitmentOrders != null)
            foreach (ArmyRecruitmentOrder order in army.fieldArmy.recruitmentOrders)
                if (order != null && order.origin == CampaignUnitOrigin.Mercenary)
                    tributaries += Mathf.Max(0, order.amount);
        if (tributaries >= desiredTributaries) return null;

        Province best = null;
        float bestDistance = float.MaxValue;
        foreach (Province candidate in Owners.Instance.provincelist)
        {
            if (candidate == null || candidate.IsOccupied || candidate.nation == null ||
                !DiplomacySystem.IsTributarySubjectOf(candidate.nation, this) ||
                !candidate.AllowsRecruitment(candidate.nation) || !CanAIReinforceAt(candidate, army)) continue;
            float distance = army.GrabDistanceToProvince(candidate);
            if (distance < bestDistance || Mathf.Approximately(distance, bestDistance) &&
                (best == null || string.CompareOrdinal(candidate.name, best.name) < 0))
            { best = candidate; bestDistance = distance; }
        }
        return best;
    }

    private Province FindReinforcementProvince(FieldArmyHolder army, bool coreOnly)
    {
        if (army == null || Owners.Instance == null || Owners.Instance.provincelist == null) return null;
        Province best = null;
        float bestDistance = float.MaxValue;
        foreach (Province candidate in Owners.Instance.provincelist)
        {
            if (candidate == null || !DiplomacySystem.HasMasterAccess(this, candidate.nation) ||
                !candidate.AllowsRecruitment(candidate.nation) ||
                coreOnly && candidate.OriginalNation != this || !CanAIReinforceAt(candidate, army)) continue;
            // Province positions use map coordinates; armies use those coordinates minus their map offset.
            Vector3 candidateWorldPosition = new Vector3(candidate.position.x - army.offset.x,
                candidate.position.y - army.offset.y, 0f);
            float distance = (candidateWorldPosition - army.transform.position).sqrMagnitude;
            if (distance < bestDistance || Mathf.Approximately(distance, bestDistance) &&
                (best == null || string.CompareOrdinal(candidate.name, best.name) < 0))
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    private bool CanAIReinforceAt(Province province, FieldArmyHolder army)
    {
        if (province == null || army == null || army.fieldArmy == null) return false;
        Nation rosterNation = province.nation;
        List<UnitSaveData> professionals = province.GetRecruitableRegionUnits(rosterNation);
        bool hasUsableProfessionals = professionals.Exists(unit => unit != null &&
            rosterNation.GetRegionalManpower(province) >= 1f && Gold >= CampaignEconomy.UnitGoldCost(unit, 1, this,
                rosterNation == this ? CampaignUnitOrigin.Professional : CampaignUnitOrigin.Mercenary));
        if (hasUsableProfessionals) return true;

        // A master may use its subject's professional roster and manpower, never its levies.
        if (rosterNation != this) return false;

        int targetSize = GetAIDesiredArmySize(army);
        int currentSize = army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize();
        int professionalGoalPercent = 40 + StableAIHash(name + "|professionals|" + army.NetworkArmyId + "|" +
            army.gameObject.name) % 31;
        int professionalGoal = Mathf.CeilToInt(targetSize * professionalGoalPercent / 100f);
        int professionalsInArmy = army.fieldArmy.CountFormations(CampaignUnitOrigin.Professional) +
            army.fieldArmy.GrabQueuedArmySize();
        bool needsProfessionalCore = professionalsInArmy < professionalGoal;
        bool emergency = GetHostileFieldArmyStrengthNear(province) > currentSize;
        bool wantsLevies = emergency || IsAILevyHeavy(army) || !needsProfessionalCore;
        return wantsLevies && province.GetAvailableRegionLevies(this).Count > 0;
    }

    private bool IsAILevyHeavy(FieldArmyHolder army)
    {
        return army != null && StableAIHash(name + "|levy-policy|" + army.NetworkArmyId + "|" +
            army.gameObject.name) % 100 < 20;
    }

    private bool TryQueueAIProfessional(FieldArmyHolder army, List<UnitSaveData> units, Nation rosterNation = null)
    {
        if (army == null || army.fieldArmy == null || units == null || units.Count == 0) return false;
        // Nations favour the best locally available tier while retaining
        // some lower-tier recruitment for cost and army variety.
        rosterNation = rosterNation != null ? rosterNation : this;
        units.Sort((left, right) => NationContentResolver.GetUnitTier(rosterNation, left)
            .CompareTo(NationContentResolver.GetUnitTier(rosterNation, right)));
        int upperHalfStart = Mathf.Max(0, units.Count / 2);
        UnitSaveData unit = Random.value < 0.7f
            ? units[Random.Range(upperHalfStart, units.Count)]
            : units[Random.Range(0, units.Count)];
        bool tributary = rosterNation != this;
        CampaignUnitOrigin origin = tributary ? CampaignUnitOrigin.Mercenary : CampaignUnitOrigin.Professional;
        int goldCost = CampaignEconomy.UnitGoldCost(unit, 1, this, origin);
        Province source = army.GrabNearestProvince();
        if (source == null || source.nation != rosterNation || Gold < goldCost ||
            !rosterNation.TrySpendManpower(source, 1f)) return false;
        Gold -= goldCost;
        if (!army.fieldArmy.QueueRecruitment(unit, 1, origin, tributary ? rosterNation.name : null))
        { rosterNation.RefundManpower(source, 1f); Gold += goldCost; return false; }
        army.NextAIReinforcementTurn = Owners.Instance.turncounter + Mathf.Max(1, army.AIReinforcementIntervalTurns);
        return true;
    }

    public float GetRegionalManpower(Province province)
    {
        if (province == null || Owners.Instance == null) return 0f;
        float total = 0f;
        foreach (CampaignRegion region in GetAccessibleManpowerRegions(province))
            total += region.GetManpower(this);
        return total;
    }

    public float AverageArmyStrength()
    {
        int count = 0;
        float total = 0f;
        foreach (FieldArmyHolder army in armies)
        {
            if (army == null || army.IsHumanControlled || army.fieldArmy == null) continue;
            int goal = GetAIDesiredArmySize(army);
            total += goal > 0 ? Mathf.Clamp01((float)(army.fieldArmy.GrabArmySize() +
                army.fieldArmy.GrabQueuedArmySize()) / goal) : 1f;
            count++;
        }
        return count == 0 ? 1f : total / count;
    }
    public int DesiredAIArmyLimit()
    {
        if (IsPacifist) return 1;
        int ownedProvinces = Owners.Instance.provincelist.FindAll(province => province.nation == this).Count;
        return Mathf.Clamp(1 + Mathf.CeilToInt(ownedProvinces / 3f), 1, 8);
    }

    public bool SpawnArmy()
    {
        int desiredArmyLimit = DesiredAIArmyLimit();
        if (armies.Count < desiredArmyLimit && AverageArmyStrength() >= 0.65f && Gold >= CampaignEconomy.ArmyCreationCost)
        {
            var a = new List<Province>();
            foreach (var province in Owners.Instance.provincelist)
            {
                if (province.nation == this)
                {
                    a.Add(province);
                }
            }
            if (a.Count > 0)
            {
                ArmyNumber++;
                FieldArmyHolder spawned = Mapshower.Instance.SpawnArmy(
                    a[Random.Range(0, a.Count)], ArmyNumber.ToString() + "st Army of " + name);
                // Awake runs during Instantiate, but Start (which historically registered the
                // army) can be delayed until after several high-timescale FixedUpdates. Reserve
                // the slot now so those ticks cannot spawn duplicates.
                if (spawned != null && spawned.fieldArmy != null)
                {
                    Gold -= CampaignEconomy.ArmyCreationCost;
                    spawned.PreserveConfiguredRoster = true;
                    spawned.fieldArmy.nation = this;
                    if (!armies.Contains(spawned)) armies.Add(spawned);
                    return true;
                }
            }
        }
        return false;
    }
}
[System.Serializable]
public sealed class ProvinceNamedModifier
{
    public string name;
    public ProvinceLocalModifiers localModifiers = new ProvinceLocalModifiers();
}
[System.Serializable]
public class Culture
{
    public string name;
    public Color32 ownerIdentity;
    public int population;
}
[System.Serializable]
public class State
{
    public string name;
    public List<Province> provincelist;
    public Color32 stateIdentity; 
    public Nation nation;
    public int taxpercentage;
    public int levypercentage;
}

[System.Serializable]
public class RegionalLoyaltyShare
{
    public string nationName;
    [Range(0f, 100f)] public float loyalty;
    [Min(0)] public int foodStorage = 1000;
    [Min(1)] public int foodStorageCapacity = 1000;
    [Min(0)] public int lastFoodShortage;
}

[System.Serializable]
public class RegionalManpowerShare
{
    public string nationName;
    [Min(0f)] public float current;
    public bool initialized;
}

[System.Serializable]
public class CampaignRegion
{
    public string name;
    public bool configuredFromProvinceData;
    public Color32 identity;
    [Range(0f, 100f)] public float loyalty = 0f;
    public List<RegionalLoyaltyShare> loyaltyShares = new List<RegionalLoyaltyShare>();
    public List<RegionalManpowerShare> manpowerShares = new List<RegionalManpowerShare>();
    public List<Province> provincelist = new List<Province>();
    [System.NonSerialized] private List<Nation> ownerScratch = new List<Nation>();

    public float GetManpowerCapacity(Nation nation)
    {
        if (nation == null || provincelist == null) return 0f;
        int holdings = 0;
        foreach (Province province in provincelist)
            if (province != null && !province.IsOccupied && province.nation == nation && province.holdings != null)
                for (int i = 0; i < province.holdings.Count; i++)
                    if (province.holdings[i] != null) holdings++;
        return holdings * 0.1f;
    }

    public RegionalManpowerShare GetManpowerShare(Nation nation, bool create)
    {
        if (nation == null) return null;
        if (manpowerShares == null) manpowerShares = new List<RegionalManpowerShare>();
        RegionalManpowerShare share = manpowerShares.Find(item => item != null && item.nationName == nation.name);
        if (share == null && create)
        {
            share = new RegionalManpowerShare { nationName = nation.name };
            manpowerShares.Add(share);
        }
        if (share != null && !share.initialized)
        {
            share.current = GetManpowerCapacity(nation);
            share.initialized = true;
        }
        return share;
    }

    public float GetManpower(Nation nation)
    {
        RegionalManpowerShare share = GetManpowerShare(nation, true);
        if (share == null) return 0f;
        share.current = Mathf.Clamp(share.current, 0f, GetManpowerCapacity(nation));
        return share.current;
    }

    public bool TrySpendManpower(Nation nation, float amount)
    {
        amount = Mathf.Max(0f, amount);
        RegionalManpowerShare share = GetManpowerShare(nation, true);
        if (share == null || share.current + 0.0001f < amount) return false;
        share.current = Mathf.Max(0f, share.current - amount);
        return true;
    }

    public void AddManpower(Nation nation, float amount)
    {
        RegionalManpowerShare share = GetManpowerShare(nation, true);
        if (share != null) share.current = Mathf.Clamp(share.current + Mathf.Max(0f, amount), 0f,
            GetManpowerCapacity(nation));
    }

    public float ManpowerRecoveryPerTick(Nation nation)
    {
        if (nation == null || provincelist == null) return 0f;
        float recovery = 0f;
        foreach (Province province in provincelist)
        {
            if (province == null || province.IsOccupied || province.nation != nation) continue;
            recovery += 0.005f;
            if (province.buildings == null) continue;
            foreach (ProvinceBuilding building in province.buildings)
                if (building != null) recovery += building.DefinitionManpowerRecovery;
        }
        recovery *= 0.25f;
        int recoveryMillionths = Mathf.RoundToInt(recovery * 1000000f);
        recoveryMillionths = nation.ApplyLawModifiers(NationalLawEffectType.ManpowerRecovery, recoveryMillionths);
        return Mathf.Max(0, recoveryMillionths) / 1000000f;
    }

    public void ProcessManpowerTurn(Nation nation) => AddManpower(nation, ManpowerRecoveryPerTick(nation));

    public float GetLoyalty(Nation nation)
    {
        if (nation == null) return loyalty;
        if (loyaltyShares == null) loyaltyShares = new List<RegionalLoyaltyShare>();
        RegionalLoyaltyShare share = loyaltyShares.Find(item => item != null && item.nationName == nation.name);
        return share != null ? share.loyalty : 0f;
    }

    public bool AllowsRecruitment(Nation nation) => GetLoyalty(nation) >= 50f;
    public bool AllowsLevyCallups(Nation nation) => AllowsRecruitment(nation);

    public void SetLoyalty(Nation nation, float value)
    {
        if (nation == null) { loyalty = Mathf.Clamp(value, 0f, 100f); return; }
        if (loyaltyShares == null) loyaltyShares = new List<RegionalLoyaltyShare>();
        RegionalLoyaltyShare share = loyaltyShares.Find(item => item != null && item.nationName == nation.name);
        if (share == null) { share = new RegionalLoyaltyShare { nationName = nation.name }; loyaltyShares.Add(share); }
        share.loyalty = Mathf.Clamp(value, 0f, 100f);
    }

    public void ChangeLoyalty(Nation nation, float amount) { SetLoyalty(nation, GetLoyalty(nation) + amount); }

    public Nation ControllingNation()
    {
        Nation result = null;
        int best = 0;
        if (provincelist == null) return null;
        foreach (Province province in provincelist)
        {
            if (province == null || province.nation == null) continue;
            int count = provincelist.FindAll(candidate => candidate != null && candidate.nation == province.nation).Count;
            if (count > best) { best = count; result = province.nation; }
        }
        return result;
    }

    public float PrimaryCultureShare(Nation controllingNation)
    {
        if (controllingNation == null || controllingNation.culture == null || string.IsNullOrEmpty(controllingNation.culture.DisplayName)) return 0f;
        int total = 0;
        int matching = 0;
        foreach (Province province in GetProvincesOwnedBy(controllingNation))
        {
            total += Mathf.Max(0, province.population);
            if (province.cultures == null) continue;
            foreach (Culture culture in province.cultures)
                if (culture != null && string.Equals(culture.name, controllingNation.culture.DisplayName,
                    System.StringComparison.OrdinalIgnoreCase)) matching += Mathf.Max(0, culture.population);
        }
        return total > 0 ? Mathf.Clamp01((float)matching / total) : 0f;
    }

    public float LoyaltyDelta(Nation controllingNation)
    {
        if (controllingNation == null) return 0f;
        float delta = -.2f;
        delta += .5f * PrimaryCultureShare(controllingNation);
        foreach (Province province in GetProvincesOwnedBy(controllingNation))
        {
            ProvinceBuilding fort = province.GetBuilding("Fort");
            ProvinceBuilding temple = province.GetBuilding("Temple");
            if (fort != null) delta += .1f * Mathf.Max(1, fort.level);
            if (temple != null) delta += .1f * Mathf.Max(1, temple.level);
        }
        delta += FoodLoyaltyModifier(controllingNation);
        return delta;
    }

    public int RegionalFood(Nation nation)
    {
        float total = 0f;
        foreach (Province province in GetProvincesOwnedBy(nation))
            if (province != null) total += province.GetFoodOutputUnrounded();
        return Mathf.RoundToInt(total);
    }

    public float FoodLoyaltyModifier(Nation nation)
    {
        RegionalLoyaltyShare share = GetLoyaltyShare(nation, true);
        int shortage = share != null ? Mathf.Max(0, share.lastFoodShortage) : 0;
        if (shortage == 0) return 0f;
        int initialShortage = Mathf.Min(10, shortage);
        int severeShortage = Mathf.Max(0, shortage - 10);
        return -(initialShortage * .01f + severeShortage * .02f);
    }

    public RegionalLoyaltyShare GetLoyaltyShare(Nation nation, bool create)
    {
        if (nation == null) return null;
        if (loyaltyShares == null) loyaltyShares = new List<RegionalLoyaltyShare>();
        RegionalLoyaltyShare share = loyaltyShares.Find(item => item != null && item.nationName == nation.name);
        if (share == null && create)
        {
            share = new RegionalLoyaltyShare { nationName = nation.name, foodStorage = 1000, foodStorageCapacity = 1000 };
            loyaltyShares.Add(share);
        }
        return share;
    }

    public void ProcessFoodTurn(Nation nation)
    {
        RegionalLoyaltyShare share = GetLoyaltyShare(nation, true);
        if (share == null) return;
        share.foodStorageCapacity = Mathf.Max(1, share.foodStorageCapacity);
        share.foodStorage = Mathf.Clamp(share.foodStorage, 0, share.foodStorageCapacity);
        int balance = RegionalFood(nation);
        share.lastFoodShortage = 0;
        if (balance >= 0)
        {
            share.foodStorage = Mathf.Min(share.foodStorageCapacity, share.foodStorage + balance);
            return;
        }
        int deficit = -balance;
        int withdrawn = Mathf.Min(share.foodStorage, deficit);
        share.foodStorage -= withdrawn;
        share.lastFoodShortage = deficit - withdrawn;
    }

    public List<string> LoyaltyInfluenceLines(Nation controllingNation)
    {
        List<string> result = new List<string> { "Taxes: -0.2" };
        float cultureShare = PrimaryCultureShare(controllingNation);
        result.Add("Primary culture (" + (cultureShare * 100f).ToString("0.#") + "%): +" +
            (.5f * cultureShare).ToString("0.##"));
        int fortLevels = 0, templeLevels = 0;
        foreach (Province province in GetProvincesOwnedBy(controllingNation))
        {
            ProvinceBuilding fort = province.GetBuilding("Fort");
            ProvinceBuilding temple = province.GetBuilding("Temple");
            if (fort != null) fortLevels += Mathf.Max(1, fort.level);
            if (temple != null) templeLevels += Mathf.Max(1, temple.level);
        }
        result.Add("Forts (" + fortLevels + " levels): +" + (fortLevels * .1f).ToString("0.#"));
        result.Add("Temples (" + templeLevels + " levels): +" + (templeLevels * .1f).ToString("0.#"));
        int regionalFood = RegionalFood(controllingNation);
        result.Add("Regional food: " + (regionalFood >= 0 ? "+" : string.Empty) + regionalFood);
        RegionalLoyaltyShare foodShare = GetLoyaltyShare(controllingNation, true);
        result.Add("Food storage: " + foodShare.foodStorage + " / " + foodShare.foodStorageCapacity);
        if (foodShare.lastFoodShortage > 0)
            result.Add("Food shortage: " + FoodLoyaltyModifier(controllingNation).ToString("0.##"));
        result.Add("Net per turn: " + (LoyaltyDelta(controllingNation) >= 0f ? "+" : string.Empty) +
            LoyaltyDelta(controllingNation).ToString("0.##"));
        return result;
    }

    public void ProcessLoyaltyTurn()
    {
        if (ownerScratch == null) ownerScratch = new List<Nation>();
        ownerScratch.Clear();
        foreach (Province province in provincelist)
            if (province != null && province.nation != null && !ownerScratch.Contains(province.nation))
                ownerScratch.Add(province.nation);
        foreach (Nation owner in ownerScratch)
        {
            ProcessManpowerTurn(owner);
            float food = 0f;
            int population = 0, primaryCulturePopulation = 0;
            int fortLevels = 0, templeLevels = 0;
            string primaryCulture = owner.culture != null ? owner.culture.DisplayName : string.Empty;
            int campaignTurn = Owners.Instance != null ? Owners.Instance.turncounter : 0;

            // Gather every regional economy/loyalty input in one traversal. The old
            // path created and scanned an owned-province list four times per owner.
            foreach (Province province in provincelist)
            {
                if (province == null || province.IsOccupied || province.nation != owner) continue;
                food += province.GetFoodOutputUnrounded();
                population += Mathf.Max(0, province.population);
                if (!string.IsNullOrEmpty(primaryCulture) && province.cultures != null)
                    foreach (Culture culture in province.cultures)
                        if (culture != null && string.Equals(culture.name, primaryCulture,
                            System.StringComparison.OrdinalIgnoreCase))
                            primaryCulturePopulation += Mathf.Max(0, culture.population);
                ProvinceBuilding fort = province.GetBuilding("Fort");
                ProvinceBuilding temple = province.GetBuilding("Temple");
                if (fort != null) fortLevels += Mathf.Max(1, fort.level);
                if (temple != null) templeLevels += Mathf.Max(1, temple.level);
                province.ApplyTempleCultureInfluence(campaignTurn);
            }

            ProcessFoodBalance(owner, Mathf.RoundToInt(food));
            float cultureShare = population > 0 ? Mathf.Clamp01((float)primaryCulturePopulation / population) : 0f;
            float loyaltyDelta = -.2f + .5f * cultureShare + .1f * (fortLevels + templeLevels) +
                FoodLoyaltyModifier(owner);
            ChangeLoyalty(owner, loyaltyDelta);
            if (GetLoyalty(owner) < 25f) RepatriateLevies(owner);
        }
    }

    private void ProcessFoodBalance(Nation nation, int balance)
    {
        RegionalLoyaltyShare share = GetLoyaltyShare(nation, true);
        if (share == null) return;
        share.foodStorageCapacity = Mathf.Max(1, share.foodStorageCapacity);
        share.foodStorage = Mathf.Clamp(share.foodStorage, 0, share.foodStorageCapacity);
        share.lastFoodShortage = 0;
        if (balance >= 0)
        {
            share.foodStorage = Mathf.Min(share.foodStorageCapacity, share.foodStorage + balance);
            return;
        }
        int deficit = -balance;
        int withdrawn = Mathf.Min(share.foodStorage, deficit);
        share.foodStorage -= withdrawn;
        share.lastFoodShortage = deficit - withdrawn;
    }

    private void RepatriateLevies(Nation owner)
    {
        if (Owners.Instance == null || provincelist == null) return;
        foreach (Province province in provincelist)
        {
        if (province == null || province.nation != owner) continue;
        foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
        {
            if (entitlement == null) continue;
            if (entitlement.state == LevyEntitlementState.Mobilizing)
            {
                entitlement.state = LevyEntitlementState.Available;
                entitlement.remainingTicks = 0;
                entitlement.raisedArmyId = null;
            }
            else if (entitlement.state == LevyEntitlementState.Raised)
            {
                FieldArmyHolder army = Owners.Instance.armylist.Find(candidate => candidate != null &&
                    candidate.fieldArmy != null && candidate.NetworkArmyId == entitlement.raisedArmyId);
                if (army != null && army.fieldArmy.DemobilizeLevy(entitlement.id))
                    province.BeginLevyRecovery(entitlement.id);
            }
        }
        }
    }

    public int Population
    {
        get
        {
            int total = 0;
            foreach (Province province in provincelist)
                if (province != null) total += province.population;
            return total;
        }
    }

    public int Supply
    {
        get
        {
            int total = 0;
            foreach (Province province in provincelist)
                if (province != null) total += province.supply;
            return total;
        }
    }

    public List<Province> GetProvincesOwnedBy(Nation nation)
    {
        return provincelist.FindAll(province => province != null && !province.IsOccupied && province.nation == nation);
    }
}
