using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class FieldArmyHolder : MonoBehaviour
{
    public FieldArmy fieldArmy;
    public static FieldArmyHolder PlayerFieldArmy;
    public static FieldArmyHolder SelectedPlayerArmy;
    public static FieldArmyHolder InspectedArmy;
    public Vector3 adjustment = new Vector3(948, 533);
    public Vector3 modification = new Vector3(0.5f, 0.5f);
    public Vector3 offset = new Vector3(364f, 232f);
    public int speed = 30;
    public Vector3 target;
    public Vector3 LocalProvince;
    public Province TargetProvince;
    private float timer;
    public int turnCounter = 0;
    private int NextRecruitTime = 0;
    public int _RecruitTimer = 4;
    private int RecruitTimer = 4;
    public int _DomesticSupplyUsage = 1;
    private int DomesticSupplyUsage = 1;
    public int _ForeignSupplyUsage = 4;
    private int ForeignSupplyUsage = 4;
    public int _ActivityTimer = 10;
    private int ActivityTimer = 10;
    public List<string> flaglist = new List<string>();
    public bool IsPlayer = false;
    public string NetworkArmyId;
    public ulong NetworkOwnerClientId = ulong.MaxValue;
    public bool IsHumanControlled;
    public bool IsNetworkReplica;
    public bool PreserveConfiguredRoster;
    [Header("AI Reinforcement")]
    [Min(1)] public int AIReinforcementIntervalTurns = 8;
    [HideInInspector] public int NextAIReinforcementTurn;
    [HideInInspector] public int CannotEngageUntilTurn;
    [HideInInspector] public int MovementPenaltyUntilTurn;
    [Min(0.1f)] public float NetworkInterpolationSpeed = 12f;

    private Vector3 networkVisualTarget;
    private bool hasNetworkVisualTarget;
    public GeneralBrain generalbrain;
    
    public void Awake()
    {
        DomesticSupplyUsage = _DomesticSupplyUsage;
        ForeignSupplyUsage = _ForeignSupplyUsage;
        RecruitTimer = _RecruitTimer;
        ActivityTimer = _ActivityTimer;
        if (gameObject.name == "PlayerArmy")
        {
            if (FieldArmyHolder.PlayerFieldArmy == null)
            {
                PlayerFieldArmy = this;
                IsPlayer = true;
                ////Debug.LogError("potato");
            }
        }
        else
        {
            fieldArmy = ScriptableObject.CreateInstance<FieldArmy>();
        }

        generalbrain = ScriptableObject.CreateInstance<GeneralBrain>();
        generalbrain.army = this;

        fieldArmy.ArmySupply = 500;

        // Owners.Instance.armylist.Add(this);
    }
    public void Start()
    {
        if (gameObject.name == "PlayerArmy")
        {
            FieldArmyHolder.PlayerFieldArmy.fieldArmy.nation = Owners.Instance.nationlist.Find(x => x.faction.name == SessionManager.Instance.HostFaction.name); //SessionManager.Instance.HostFaction
            fieldArmy.nation.armies.Add(this);
            Province startingProvince = FindStartingProvince(fieldArmy.nation);
            if (startingProvince != null)
                SetPositionTo(startingProvince);
            else
                Debug.LogWarning($"Could not position {name}: {fieldArmy.nation.name} does not own a province.", this);

            Camera.main.gameObject.transform.localPosition = new Vector3(FieldArmyHolder.PlayerFieldArmy.gameObject.transform.position.x, FieldArmyHolder.PlayerFieldArmy.gameObject.transform.position.y, -10);
        }
        else
        {
            if (fieldArmy.nation == null)
            {
                fieldArmy.nation = GrabFieldArmyProvince().nation;
                fieldArmy.nation.armies.Add(this);
            }
        }
        generalbrain.nation = fieldArmy.nation.name;
        generalbrain.Startie();
        Owners.Instance.armylist.Add(this);
        Material mat = Instantiate(transform.GetChild(0).GetComponent<SpriteRenderer>().material);
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = mat;
        transform.GetChild(1).GetComponent<SpriteRenderer>().material = mat;
        transform.GetChild(2).GetComponent<SpriteRenderer>().material = mat;
        mat.SetColor("_FactionColor", fieldArmy.nation.faction.color);
        mat.SetColor("_FactionColor2", fieldArmy.nation.faction.color2);
        mat.SetColor("_FactionColor3", fieldArmy.nation.faction.color3);

        if (!PreserveConfiguredRoster)
        {
            InitializeStarterRoster();
        }
        

        if (fieldArmy.USDReserves.Count > 0 && fieldArmy.USDReserves[0].USD != null)
        {
            transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = fieldArmy.USDReserves[0].USD.bodyparts[0];
            transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = fieldArmy.USDReserves[0].USD.bodyparts[2];
        }

        if (fieldArmy.nation.faction.HasFlag("EqualOpportunityPillagers"))
        {
            DomesticSupplyUsage = 2;
            ForeignSupplyUsage = 2;
        }
        if (fieldArmy.nation.faction.HasFlag("Decentralized"))
        {
            ActivityTimer = 20;
            RecruitTimer *= 2;
        }
        if (fieldArmy.nation.faction.HasFlag("Braindead"))
        {
            ActivityTimer = 1000;
            RecruitTimer *= 10;
        }
        if (fieldArmy.nation.faction.HasFlag("FastAsFuckBoi"))
        {
            speed *= 2;
            ActivityTimer = 0;
        }
        
    }
    public void OnDestroy()
    {
        if ((NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer) &&
            fieldArmy != null && fieldArmy.formationRecords != null)
        {
            foreach (ArmyFormationRecord record in new List<ArmyFormationRecord>(fieldArmy.formationRecords))
            {
                if (record == null || record.origin != CampaignUnitOrigin.Levy || string.IsNullOrEmpty(record.entitlementId) || Owners.Instance == null) continue;
                Province source = Owners.Instance.provincelist.Find(province => province != null && province.levyEntitlements != null &&
                    province.levyEntitlements.Exists(item => item != null && item.id == record.entitlementId));
                if (source != null) source.BeginLevyRecovery(record.entitlementId);
            }
        }
        if (InspectedArmy == this) InspectedArmy = null;
        if (SelectedPlayerArmy == this) SelectedPlayerArmy = null;
        try{
            fieldArmy.nation.armies.Remove(this);
        }catch{}
        try{
            Owners.Instance.armylist.Remove(this);
        }catch{}
        if (generalbrain != null) Destroy(generalbrain);
    }
    public void Update()
    {
        if (IsPlayer && Input.GetMouseButtonDown(1))
        {
            //SetTarget(Input.mousePosition);
        }
    }
    public void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.gameObject.GetComponent<FieldArmyHolder>() != null)
        {
            OnMeeting(collider.gameObject.GetComponent<FieldArmyHolder>());
        }
    }
    public void OnMouseOver()
    {
        fieldArmy.UpdateUI();
    }
    public void OnMouseDown()
    {
        InspectedArmy = this;
        if (IsFriendlyToLocalPlayer())
        {
            SelectedPlayerArmy = this;
        }
        if (fieldArmy != null) fieldArmy.UpdateUI();
    }
    public bool IsFriendlyToLocalPlayer()
    {
        if (fieldArmy == null || fieldArmy.nation == null) return false;
        string localNation = CampaignNetworkPlayer.Local != null
            ? CampaignNetworkPlayer.Local.AssignedNation
            : string.Empty;
        if (string.IsNullOrEmpty(localNation) && SessionManager.Instance != null && SessionManager.Instance.HostFaction != null)
        {
            localNation = SessionManager.Instance.HostFaction.name;
        }
        if (string.IsNullOrEmpty(localNation) && PlayerFieldArmy != null && PlayerFieldArmy.fieldArmy != null && PlayerFieldArmy.fieldArmy.nation != null)
        {
            localNation = PlayerFieldArmy.fieldArmy.nation.name;
        }
        return !string.IsNullOrEmpty(localNation) ? fieldArmy.nation.name == localNation : IsPlayer;
    }
    public void OpenRecruitmentMenu()
    {
        RecruitmentMenu.Show(this);
    }
    public void OnMeeting(FieldArmyHolder otherarmy)
    {
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening &&
            !Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (Owners.Instance != null && (Owners.Instance.turncounter < CannotEngageUntilTurn ||
            Owners.Instance.turncounter < otherarmy.CannotEngageUntilTurn)) return;
        if (otherarmy.fieldArmy.nation == fieldArmy.nation)
        {
            if (ProjectX.TileBattle.TileBattleCampaignManager.Instance != null)
                ProjectX.TileBattle.TileBattleCampaignManager.Instance.TryJoinFriendlyBattle(this, otherarmy);
            return;
        }
        if (DeterministicBattleManager.Instance != null &&
            DeterministicBattleManager.Instance.BattleSystemMode == CampaignBattleSystemMode.TileBased &&
            ProjectX.TileBattle.TileBattleCampaignManager.Instance != null)
        {
            ProjectX.TileBattle.TileBattleCampaignManager.Instance.TryStartBattle(this, otherarmy);
            return;
        }
        if (DeterministicBattleManager.Instance != null &&
            DeterministicBattleManager.Instance.BattleSystemMode == CampaignBattleSystemMode.Deterministic)
        {
            DeterministicBattleManager.Instance.TryStartBattle(this, otherarmy);
            return;
        }
        if (IsPlayer || otherarmy.IsPlayer)
        {
            if (IsPlayer)
            {
                Mapshower.Instance.ArmyBattle(this, otherarmy, otherarmy.fieldArmy);
            }
        }
        else
        {
            //HandleAIonAICombat
            HandleAIonAICombat(otherarmy);
            flaglist.Remove("Battle");
        }
    }
    public bool HandleAIonAICombat(FieldArmyHolder otherarmy, FieldArmy actualarmy = null)
    {
        
        if (otherarmy != null)
        {
            otherarmy.flaglist.Add("Battle");
        }
        if (!flaglist.Contains("Battle"))
        {
            if (otherarmy != null)
            {
                actualarmy = otherarmy.fieldArmy;
            }
            var a = actualarmy.GrabArmySize() + Random.Range(-actualarmy.GrabArmySize() / 2, actualarmy.GrabArmySize() / 2);
            var b = this.fieldArmy.GrabArmySize() + Random.Range(-this.fieldArmy.GrabArmySize() / 2, this.fieldArmy.GrabArmySize() / 2);
            if (this.HasFlag("CrusherOfGarrisons") && otherarmy == null)
            {
                a -= 2;
            }
            //If Theirs is stronger
                if (a >= b)
                {
                    for (int i = 0; i < b; i++)
                    {
                        actualarmy.RemoveRandomUnit();
                    }
                    Destroy(this.gameObject);
                    return false;
                }
                //If Ours is stronger
                else
                {
                    for (int i = 0; i < a; i++)
                    {
                        this.fieldArmy.RemoveRandomUnit();
                    }
                    if (otherarmy != null)
                    {
                        Destroy(otherarmy.gameObject);
                    }
                    return true;
                }
        }
        flaglist.Remove("Battle");
        return false;
    }
    public float GrabDistanceToProvince(Province province)
    {
        var heading = transform.position - new Vector3((province.position.x - adjustment.x) * modification.x, (province.position.y - adjustment.y) * modification.y, 0);
        return heading.magnitude;
    }
    public void Act()
    {
        if (flaglist.Contains("Battle")) return;
        if (IsPlayer && target.x + target.y != 0)
        {
            Move();
            return;
        }
        generalbrain.Think();
        if(!CanArmyAct())
        {
            return;
        }
        //Move();
    }
    public bool Move()
    {
        var heading = transform.position - new Vector3((target.x - adjustment.x) * modification.x, (target.y - adjustment.y) * modification.y, 0);
        var distance = heading.magnitude;

        if (IsTargetNull())
        {
            return false;
        }

        if (distance < 1)
        {
            LocalProvince = target;
            var destination = TargetProvince;
            if (destination != null && destination.nation != fieldArmy.nation)
            {
                if (DeterministicBattleManager.Instance != null &&
                    DeterministicBattleManager.Instance.BattleSystemMode == CampaignBattleSystemMode.TileBased &&
                    ProjectX.TileBattle.TileBattleCampaignManager.Instance != null)
                {
                    ProjectX.TileBattle.TileBattleCampaignManager.Instance.TryStartGarrisonBattle(this, destination);
                }
                else if (DeterministicBattleManager.Instance != null &&
                    DeterministicBattleManager.Instance.BattleSystemMode == CampaignBattleSystemMode.Deterministic)
                {
                    DeterministicBattleManager.Instance.TryStartGarrisonBattle(this, destination);
                }
                else if (!IsPlayer && HandleAIonAICombat(null, destination.garrison))
                {
                    EnterProvince(destination);
                }
            }
            target = Vector3.zero;
            TargetProvince = null;
            //Debug.LogError("Arrived");
            return true;
        }
        var direction = heading / distance;
        float battleFatigueMultiplier = Owners.Instance != null && Owners.Instance.turncounter < MovementPenaltyUntilTurn ? .5f : 1f;
        transform.localPosition -= direction * Time.deltaTime * speed * battleFatigueMultiplier;
        //Debug.Log("Walking");
        return false;
    }
    public bool IsTargetNull()
    {
        if (target.x + target.y == 0)
        {
            return true;
        }
        return false;
    }
    public bool CanArmyAct()
    {
        if(generalbrain.currentAction != null)
        {
            return false;
        }
        if(IsTargetNull())
        {
            return false;
        }
        return true;
    }
    public bool IsArmyAvailable()
    {
        if(generalbrain.currentAction == null)
        {
            //Debug.Log(gameObject.name + " Is Available");
            return true;
        }
        return false;
    }
    public void EnterProvince(Province province)
    {
        ConquerProvince(province);
    }
    public void ConquerProvince(Province province)
    {
        Nation previousOwner = province.nation;
        province.nation = fieldArmy.nation;
        if (fieldArmy.nation != null && fieldArmy.nation.nationalbrainy != null)
            fieldArmy.nation.nationalbrainy.ReSetPriorities();
        if (previousOwner != null && previousOwner != fieldArmy.nation && previousOwner.nationalbrainy != null)
            previousOwner.nationalbrainy.ReSetPriorities();
        Mapshower.Instance.RePaint();
        province.CreateGarrison();
    }

    private void LateUpdate()
    {
        if (!IsNetworkReplica || !hasNetworkVisualTarget)
        {
            return;
        }

        float blend = 1f - Mathf.Exp(-NetworkInterpolationSpeed * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, networkVisualTarget, blend);

        if ((transform.position - networkVisualTarget).sqrMagnitude < 0.0001f)
        {
            transform.position = networkVisualTarget;
        }
    }

    public void ConfigureNetworkIdentity(string armyId, ulong ownerClientId, bool humanControlled, Nation nation)
    {
        NetworkArmyId = armyId;
        NetworkOwnerClientId = ownerClientId;
        IsHumanControlled = humanControlled;
        IsPlayer = humanControlled;
        IsNetworkReplica = Unity.Netcode.NetworkManager.Singleton != null &&
                           Unity.Netcode.NetworkManager.Singleton.IsListening &&
                           !Unity.Netcode.NetworkManager.Singleton.IsServer;

        if (fieldArmy == null)
        {
            fieldArmy = ScriptableObject.CreateInstance<FieldArmy>();
        }
        if (nation != null)
        {
            if (fieldArmy.nation != null && fieldArmy.nation != nation)
                fieldArmy.nation.armies.Remove(this);
            fieldArmy.nation = nation;
            if (!nation.armies.Contains(this))
            {
                nation.armies.Add(this);
            }
        }
    }

    public void ApplyNetworkState(CampaignArmyState state)
    {
        if (!hasNetworkVisualTarget || Vector3.Distance(transform.position, state.MapPosition) > 25f)
        {
            transform.position = state.MapPosition;
        }

        networkVisualTarget = state.MapPosition;
        hasNetworkVisualTarget = true;
        target = state.MapTarget;
        fieldArmy.ArmySupply = state.Supply;
        if (state.InEncounter)
        {
            if (!flaglist.Contains("Battle")) flaglist.Add("Battle");
        }
        else
        {
            flaglist.Remove("Battle");
        }
    }
    public void SetPositionTo(Vector3 newposition)
    {
        transform.localPosition = new Vector3((newposition.x - adjustment.x) * modification.x, (newposition.y - adjustment.y) * modification.y, 0);
    }
    public void SetPositionTo(Province province)
    {
        //transform.localPosition = new Vector3(province.position.x * 1f - offset.x, province.position.y * 1f - offset.y, 0);
        transform.position = new Vector3(province.position.x * 1f - offset.x, province.position.y * 1f - offset.y, 0);
    }
    public void AddTroop(UnitSaveData unittoAdd = null, string name = "", int amount = 1)
    {
        ////Debug.LogError("Trying to add " + amount + " of " + name + unittoAdd);
        if (name != "")
        {
            try
            {
                var a = FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves.Find(x => x.name == name).USD;
                fieldArmy.AddTroop(a, amount);
            }
            catch
            {
                try
                {
                    var b = Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/" + name);
                    var c = Instantiate(b);
                    c.name = b.name;
                    fieldArmy.AddTroop(c, amount);
                }
                catch
                {
                    //Debug.LogError("Could not find " + name + " Unit in database");
                }

            }

        }
        else
        {
            if (unittoAdd == null)
            {
                List<NationUnitEntry> roster = NationContentResolver.ResolveUnits(fieldArmy.nation);
                if (roster.Count > 0) fieldArmy.AddTroop(roster[Random.Range(0, roster.Count)].unit, amount);
            }
            else
            {

                fieldArmy.AddTroop(unittoAdd, amount);
            }
        }
        if (IsPlayer)
        {
            fieldArmy.UpdateUI();
        }
    }
    public void NextTurn()
    {
        turnCounter++;
        Province province = GrabFieldArmyProvince(); //Mapshower.Instance.SelectedProvince;

        // //HandleAIBehaviour
        // if (!IsPlayer)
        // {
        //     if (target.x + target.y == 0)
        //     {
        //         if (turnCounter % ActivityTimer == 0)
        //         {
        //             var a = new List<Province>();
        //             foreach (var item in Owners.Instance.provincelist)
        //             {

        //                 Vector3 temptarget = item.position;
        //                 var heading = transform.position - new Vector3((temptarget.x - adjustment.x) * modification.x, (temptarget.y - adjustment.y) * modification.y, 0);
        //                 var distance = heading.magnitude;
        //                 if (distance < 75) // 50) //150)
        //                 {
        //                     a.Add(item);
        //                     if (item.nation == fieldArmy.nation)
        //                     {
        //                         a.Add(item);
        //                     }
        //                 }
        //             }
        //             var b = a[Random.Range(0, a.Count)];
        //             SetTarget(b);
        //             TargetProvince = b;
        //             //SetPositionTo(b);
        //         }
        //     }
        // }

        //HandleSupply
        if (IsPlayer)
        {
            //AtHome
            if (province != null && province.nation.faction.name == fieldArmy.nation.faction.name)
            {
                fieldArmy.AddSupply(-fieldArmy.GrabArmySize() * DomesticSupplyUsage);
            }
            //Abroad
            else
            {
                fieldArmy.AddSupply(-fieldArmy.GrabArmySize() * ForeignSupplyUsage);
            }

            if (province != null && province.supply >= 100)
            {
                int lootableAmount = province.supply / 10;
                lootableAmount = lootableAmount;

                province.supply -= lootableAmount;
                fieldArmy.AddSupply(lootableAmount);
            }
        }
    }
    public void Recruit()
    {
        Province province = GrabFieldArmyProvince();
        //HandleRecruitment
        //if (turnCounter % RecruitTimer == 0)
        //{
            if(province != null)
            {
                Nation nation = Owners.Instance.nationlist.Find(x => x.name == province.OriginalNation.name);
                //AtHome
                if (province.nation.faction.name == fieldArmy.nation.faction.name)
                {
                    if (nation.faction.name == fieldArmy.nation.faction.name)
                    {
                        if (fieldArmy.nation.faction.HasFlag("DoubleLocalRecruitment"))
                        {
                            AddTroop();
                        }
                    }
                    AddTroop();
                }
                //Abroad
                else
                {
                    if (fieldArmy.nation.faction.HasFlag("ForeignBarbarianRecruitment"))
                    {   
                        if (nation.faction.HasFlag("Barbarian"))
                        {
                            ////Debug.LogError(nation.name);
                            var a = Instantiate(nation.faction.UnitDataList[0]);
                            a.name = province.nation.faction.UnitDataList[0].name;
                            if (Random.Range(0, 3) == 1)
                            {
                                a = Instantiate(nation.faction.UnitDataList[1]);
                                a.name = province.nation.faction.UnitDataList[1].name;
                            }
                            //a.name = province.nation.faction.UnitDataList[0].name;
                            a.Mercenary = true;
                            AddTroop(a);
                        }
                    }
                }
            }
            
        //}
        //HandleEvents
        if (IsPlayer && turnCounter % 8 == 0)
        {
            //EventManager.eventManager.TriggerEvent(grabRandomViableEvent().name);
        }
    }
    public BaseEvents grabRandomViableEvent()
    {
        var a = Resources.LoadAll<BaseEvents>("EventGroup/");
        var b = new List<BaseEvents>();
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Trigger())
            {
                b.Add(a[i]);
            }
        }
        return b[Random.Range(0, b.Count)];
    }

    private void InitializeStarterRoster()
    {
        const int starterUnitCount = 6;
        const int starterBarracksLevels = 3;

        fieldArmy.USDReserves.Clear();
        List<NationUnitEntry> resolvedRoster = NationContentResolver.ResolveUnits(fieldArmy.nation);
        List<NationUnitEntry> barracksLine = new List<NationUnitEntry>();

        foreach (NationUnitEntry entry in resolvedRoster)
        {
            if (entry == null || entry.unit == null) continue;
            if (!string.Equals(entry.RequiredBuildingId, "Barracks", System.StringComparison.OrdinalIgnoreCase)) continue;
            barracksLine.Add(entry);
        }

        // Treat the first three distinct progression tiers as barracks-line levels 0, 1 and 2.
        // The serialized building requirement itself is one-based, so sorting first keeps
        // this compatible with both older level-zero data and newer level-one data.
        barracksLine.Sort((left, right) =>
        {
            int tierComparison = left.minimumBuildingLevel.CompareTo(right.minimumBuildingLevel);
            if (tierComparison != 0) return tierComparison;
            return string.CompareOrdinal(left.unit.name, right.unit.name);
        });

        int availableStarterTypes = 0;
        int includedLevels = 0;
        int previousLevel = int.MinValue;
        foreach (NationUnitEntry entry in barracksLine)
        {
            if (entry.minimumBuildingLevel != previousLevel)
            {
                if (includedLevels >= starterBarracksLevels) break;
                previousLevel = entry.minimumBuildingLevel;
                includedLevels++;
            }
            availableStarterTypes++;
        }

        if (availableStarterTypes == 0)
        {
            Debug.LogWarning($"Could not create the six-unit starter army for {fieldArmy.nation.name}: its resolved roster has no Barracks units.", this);
            return;
        }

        for (int i = 0; i < starterUnitCount; i++)
        {
            UnitSaveData starterUnit = barracksLine[i % availableStarterTypes].unit;
            fieldArmy.AddTroop(starterUnit, 1, true);
        }
    }

    private Province FindStartingProvince(Nation armyNation)
    {
        if (armyNation == null || Owners.Instance == null || Owners.Instance.provincelist == null) return null;

        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.nation == null) continue;
            if (province.nation == armyNation || province.nation.name == armyNation.name) return province;
        }

        return null;
    }

    public bool HasFlag(string flag)
    {
        foreach (var item in flaglist)
        {
            if (item == flag)
            {
                return true;
            }
        }
        return false;
    }
    public Province GrabFieldArmyProvince()
    {
        Province nearest = GrabNearestProvince();
        return nearest != null ? nearest : Mapshower.Instance.SelectProvinceFromLocation(GrabFieldArmyHolderPosition());
    }
    public Province GrabNearestProvince()
    {
        if (Owners.Instance == null || Owners.Instance.provincelist == null) return null;
        Province nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Province province in Owners.Instance.provincelist)
        {
            Vector3 provinceWorldPosition = new Vector3(province.position.x - offset.x, province.position.y - offset.y, 0f);
            float distance = (transform.position - provinceWorldPosition).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = province;
            }
        }
        return nearest;
    }
    public Vector3 GrabFieldArmyHolderPosition()
    {
        return Camera.main.WorldToScreenPoint(transform.position);
    }
    public void SetTarget(Vector3 newtarget)
    {
        ////Debug.LogError(newtarget);
        target = newtarget;
    }
    public void SetTarget(Province province)
    {
        ////Debug.LogError(province.position);
        SetTarget(province.position);
        // int x = (int)Mathf.Floor(province.position.x) + Mapshower.Instance.width / 2;
        // int y = (int)Mathf.Floor(province.position.y) + Mapshower.Instance.height / 2;
        // SetTarget(new Vector3(x, y, 0));

        //target = Camera.main.WorldToScreenPoint(new Vector3(province.position.x * 1f - offset.x, province.position.y * 1f - offset.y, 0));
    }
}
