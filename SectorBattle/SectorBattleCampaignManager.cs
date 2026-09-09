using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectX.CombatBackend;

namespace ProjectX.SectorBattle
{
    public sealed class SectorCampaignBattle
    {
        public string BattleId;
        public string DisplayFactionA, DisplayFactionB;
        public FieldArmyHolder ArmyA, ArmyB;
        public FieldArmy Garrison;
        public Province DefendedProvince;
        public Vector3 MapPosition;
        public IBattleSimulation Simulation;
        public bool DebugScenario;
        public bool NonCampaignBattle;
        public bool ResultsApplied;
        public int StartDelaySectorTicksRemaining = 2;
        public readonly HashSet<int> HumanPlayerSides = new HashSet<int>();
        public readonly List<SectorReplayFrame> Replay = new List<SectorReplayFrame>();
        public readonly List<FieldArmyHolder> SideA = new List<FieldArmyHolder>();
        public readonly List<FieldArmyHolder> SideB = new List<FieldArmyHolder>();
        public readonly Dictionary<int, UnitSaveData> UnitSources = new Dictionary<int, UnitSaveData>();
        public readonly Dictionary<int, FieldArmyHolder> ArmySources = new Dictionary<int, FieldArmyHolder>();
        public readonly Dictionary<int, ArmyFormationRecord> FormationSources = new Dictionary<int, ArmyFormationRecord>();
    }

    /// <summary>Optional campaign host for sector simulations. Tile remains the default backend.</summary>
    public sealed class SectorBattleCampaignManager : MonoBehaviour
    {
        public static SectorBattleCampaignManager Instance { get; private set; }
        public readonly List<SectorCampaignBattle> ActiveBattles = new List<SectorCampaignBattle>();
        public bool EnableDiagnosticLogging = true;
        public SectorCustomBattleLauncher CustomBattleLauncher { get; private set; }
        private readonly HashSet<SectorCampaignBattle> manuallySteppedBattles = new HashSet<SectorCampaignBattle>();
        public const int CampaignStepsPerSectorTick = 10;
        private int campaignStepsTowardSectorTick;

        public int CampaignStepsUntilNextSectorTick => CampaignStepsPerSectorTick - campaignStepsTowardSectorTick;

        public bool TickClockPaused => Owners.Instance == null || Owners.Instance.CampaignPaused || Owners.Instance.CampaignSimulationSpeed <= 0f;
        public float SecondsUntilNextTick
        {
            get
            {
                float speed = Owners.Instance != null ? Owners.Instance.CampaignSimulationSpeed : 0f;
                return speed <= 0f ? float.PositiveInfinity : CampaignStepsUntilNextSectorTick * Time.fixedDeltaTime / speed;
            }
        }

        private void Awake() { Instance = this; }
        private void Start()
        {
            SectorBattlePresentation presentation = GetComponent<SectorBattlePresentation>();
            if (presentation == null) presentation = gameObject.AddComponent<SectorBattlePresentation>();
            presentation.Initialize(this);
            CustomBattleLauncher = GetComponent<SectorCustomBattleLauncher>();
            if (CustomBattleLauncher == null) CustomBattleLauncher = gameObject.AddComponent<SectorCustomBattleLauncher>();
            CustomBattleLauncher.Initialize(this, presentation);
            if (SectorCustomBattleLaunchRequest.Consume()) CustomBattleLauncher.Show();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void AdvanceFromCampaignStep()
        {
            if (!IsAuthority() || ActiveBattles.Count == 0) return;
            bool sectorSelected = DeterministicBattleManager.Instance != null &&
                DeterministicBattleManager.Instance.BattleSystemMode == CampaignBattleSystemMode.Sector;
            if (!sectorSelected && !ActiveBattles.Exists(item => item.DebugScenario || item.NonCampaignBattle)) return;
            campaignStepsTowardSectorTick++;
            if (campaignStepsTowardSectorTick < CampaignStepsPerSectorTick) return;
            campaignStepsTowardSectorTick = 0;
            List<SectorCampaignBattle> current = new List<SectorCampaignBattle>(ActiveBattles);
            for (int i = 0; i < current.Count; i++)
            {
                SectorCampaignBattle battle = current[i];
                if (battle == null || battle.Simulation == null || battle.Simulation.IsResolved) continue;
                ApplyHumanOrAIControl(battle, manuallySteppedBattles.Contains(battle));
                int index = ActiveBattles.IndexOf(battle);
                if (index < 0) continue;
                if (battle.StartDelaySectorTicksRemaining > 0)
                { battle.StartDelaySectorTicksRemaining--; RecordReplayFrame(battle); continue; }
                AdvanceBattleOneTick(index, battle);
            }
        }

        public void SetManualTickMode(SectorCampaignBattle battle, bool enabled)
        {
            if (battle == null) return;
            if (enabled && !battle.Simulation.IsResolved) manuallySteppedBattles.Add(battle);
            else manuallySteppedBattles.Remove(battle);
            ApplyHumanOrAIControl(battle, enabled);
        }

        public bool IsManualTickMode(SectorCampaignBattle battle) => battle != null && manuallySteppedBattles.Contains(battle);

        public bool AdvanceManualBattleOneTick(SectorCampaignBattle battle)
        {
            if (!IsAuthority() || battle == null || battle.Simulation == null || battle.Simulation.IsResolved ||
                !manuallySteppedBattles.Contains(battle)) return false;
            if (ActiveBattles.IndexOf(battle) < 0) return false;
            int before = battle.Simulation.Tick;
            if (Owners.Instance != null)
            {
                if (!Owners.Instance.AdvancePausedCampaignSteps(CampaignStepsPerSectorTick)) return false;
            }
            else
            {
                for (int step = 0; step < CampaignStepsPerSectorTick; step++)
                    AdvanceFromCampaignStep();
            }
            return battle.Simulation.Tick != before || battle.Simulation.IsResolved;
        }

        private void AdvanceBattleOneTick(int index, SectorCampaignBattle battle)
        {
            if (battle.DebugScenario && battle.Simulation is SectorBattleSimulation debugSimulation)
                SectorBattleDebugScenario.AdvanceOneTick(debugSimulation);
            else battle.Simulation.TickBattle();
            RecordReplayFrame(battle);
            if (battle.Simulation.IsResolved) FinishBattle(index, battle);
        }

        private static void RememberHumanSides(SectorCampaignBattle battle)
        {
            if (battle?.Simulation is not SectorBattleSimulation simulation) return;
            foreach (SectorCommandGroup group in simulation.CommandGroups)
                if (group.PlayerControlled) battle.HumanPlayerSides.Add(group.Side);
        }

        private static void ApplyHumanOrAIControl(SectorCampaignBattle battle, bool beingWatched)
        {
            if (battle?.Simulation is not SectorBattleSimulation simulation) return;
            foreach (int side in battle.HumanPlayerSides)
                simulation.Commands.SetPlayerControlled(side, beingWatched);
        }

        public bool TryStartBattle(FieldArmyHolder a, FieldArmyHolder b)
        {
            if (!Enabled || a == null || b == null || a == b || a.fieldArmy == null || b.fieldArmy == null ||
                DiplomacySystem.AreFriendly(a.fieldArmy.nation, b.fieldArmy.nation)) return false;
            SectorCampaignBattle existingA = FindBattle(a), existingB = FindBattle(b);
            if (existingA != null && existingB == null) return TryJoinBattle(existingA, b);
            if (existingB != null && existingA == null) return TryJoinBattle(existingB, a);
            if (existingA != null || a.fieldArmy.GrabArmySize() <= 0 || b.fieldArmy.GrabArmySize() <= 0) return existingA != null;
            string id = "sector-" + Owners.Instance.turncounter + "-" + a.NetworkArmyId + "-" + b.NetworkArmyId;
            SectorCampaignBattle battle = CreateBattle(id, a, b, null); if (battle == null) return false;
            ActiveBattles.Add(battle); SetEncounter(a, true); SetEncounter(b, true); return true;
        }

        public bool TryStartGarrisonBattle(FieldArmyHolder attacker, Province province)
        {
            if (!Enabled || attacker == null || province == null || attacker.fieldArmy == null || province.garrison == null ||
                attacker.fieldArmy.GrabArmySize() <= 0 || province.garrison.GrabArmySize() <= 0) return false;
            SectorCampaignBattle existing = FindBattle(attacker);
            if (existing != null) return true;
            SectorCampaignBattle defended = ActiveBattles.Find(item => item.DefendedProvince == province);
            if (defended != null) return TryJoinBattle(defended, attacker);
            string id = "sector-garrison-" + Owners.Instance.turncounter + "-" + attacker.NetworkArmyId + "-" + province.name;
            SectorCampaignBattle battle = CreateBattle(id, attacker, null, province); if (battle == null) return false;
            ActiveBattles.Add(battle); SetEncounter(attacker, true); return true;
        }

        public bool TryJoinFriendlyBattle(FieldArmyHolder moving, FieldArmyHolder encountered)
        {
            SectorCampaignBattle battle = FindBattle(encountered); return battle != null && TryJoinBattle(battle, moving);
        }

        public SectorCampaignBattle FindBattle(FieldArmyHolder army) => ActiveBattles.Find(item => !item.Simulation.IsResolved &&
            (item.SideA.Contains(army) || item.SideB.Contains(army)));

        public SectorCampaignBattle StartVisualDebugScenario()
        {
            SectorCampaignBattle old = ActiveBattles.Find(item => item.DebugScenario);
            if (old != null) ActiveBattles.Remove(old);
            SectorCampaignBattle battle = new SectorCampaignBattle { BattleId = "sector-debug-10v10",
                Simulation = SectorBattleDebugScenario.Create(), DebugScenario = true };
            RememberHumanSides(battle); ApplyHumanOrAIControl(battle, false);
            RecordReplayFrame(battle); ActiveBattles.Add(battle); return battle;
        }

        public SectorCampaignBattle RegisterCustomBattle(SectorBattleSimulation simulation, string name,
            string displayFactionA = null, string displayFactionB = null)
        {
            if (simulation == null) return null;
            SectorCampaignBattle old = ActiveBattles.Find(item => item.NonCampaignBattle);
            if (old != null) ActiveBattles.Remove(old);
            SectorCampaignBattle battle = new SectorCampaignBattle { BattleId = name, Simulation = simulation,
                DisplayFactionA = displayFactionA, DisplayFactionB = displayFactionB, NonCampaignBattle = true };
            RememberHumanSides(battle); ApplyHumanOrAIControl(battle, false);
            RecordReplayFrame(battle); ActiveBattles.Add(battle); return battle;
        }

        private bool TryJoinBattle(SectorCampaignBattle battle, FieldArmyHolder reinforcement)
        {
            if (battle == null || reinforcement == null || reinforcement.fieldArmy == null || FindBattle(reinforcement) != null) return false;
            Nation left = battle.SideA.Count > 0 && battle.SideA[0] != null ? battle.SideA[0].fieldArmy.nation : null;
            int side = DiplomacySystem.AreFriendly(reinforcement.fieldArmy.nation, left) ? 0 : 1;
            (side == 0 ? battle.SideA : battle.SideB).Add(reinforcement);
            AddArmy(battle, reinforcement.fieldArmy, reinforcement, side, true); SetEncounter(reinforcement, true);
            return true;
        }

        private SectorCampaignBattle CreateBattle(string id, FieldArmyHolder a, FieldArmyHolder b, Province province)
        {
            FieldArmy right = b != null ? b.fieldArmy : province.garrison;
            BattleSimulationRequest request = new BattleSimulationRequest { BattleId = id,
                Seed = (ulong)(uint)StableHash(id) };
            request.Commanders.Add(CommanderFor(a, 0, false));
            request.Commanders.Add(CommanderFor(b, 1, province != null));
            SectorBattleSimulation simulation = new SectorBattleSimulation();
            SectorCampaignBattle battle = new SectorCampaignBattle { BattleId = id, ArmyA = a, ArmyB = b,
                Garrison = province != null ? province.garrison : null, DefendedProvince = province,
                MapPosition = b != null ? (a.transform.position + b.transform.position) * .5f : a.transform.position,
                Simulation = simulation };
            battle.SideA.Add(a); if (b != null) battle.SideB.Add(b);
            AddArmyInputs(request, battle, a.fieldArmy, a, 0, false);
            AddArmyInputs(request, battle, right, b, 1, false);
            simulation.Initialize(request); ApplyTerrain(simulation, province != null ? province : a.GrabNearestProvince()); simulation.StartBattle();
            RememberHumanSides(battle); ApplyHumanOrAIControl(battle, false);
            RecordReplayFrame(battle);
            return simulation.Formations.Count > 0 ? battle : null;
        }

        private static void AddArmyInputs(BattleSimulationRequest request, SectorCampaignBattle battle, FieldArmy army,
            FieldArmyHolder holder, int side, bool reinforcement)
        {
            if (army == null) return; army.ReconcileFormationRecords();
            List<ArmyFormationRecord> records = new List<ArmyFormationRecord>(army.formationRecords);
            int index = battle.UnitSources.Count + 1;
            foreach (ArmyReserves reserve in army.USDReserves)
            {
                if (reserve == null || reserve.USD == null) continue;
                for (int copy = 0; copy < reserve.amount; copy++)
                {
                    int id = side * 100000 + index++;
                    BattleLane lane = DefaultDeploymentLane(reserve.USD, id);
                    int depth = side == 0 ? (int)BattleDepth.SideAReserve : (int)BattleDepth.SideBReserve;
                    request.Formations.Add(new BattleFormationInput { FormationId = id, Side = side, Unit = reserve.USD,
                        Strength = reserve.USD.health, SourceArmyId = holder != null ? holder.NetworkArmyId : string.Empty,
                        DeploymentColumn = (int)lane, DeploymentRow = depth });
                    battle.UnitSources[id] = reserve.USD; battle.ArmySources[id] = holder;
                    int recordIndex = records.FindIndex(item => item != null && item.unit == reserve.USD);
                    if (recordIndex >= 0) { battle.FormationSources[id] = records[recordIndex]; records.RemoveAt(recordIndex); }
                }
            }
        }

        private static BattleLane DefaultDeploymentLane(UnitSaveData unit, int stableIndex)
        {
            if (unit == null) return BattleLane.Centre;
            if (unit.unittype == UnitTypes.HeavyInfantry) return BattleLane.Centre;
            if (unit.unittype == UnitTypes.HeavyCavalry || unit.unittype == UnitTypes.LightCavalry)
                return stableIndex % 2 == 0 ? BattleLane.UpperWing : BattleLane.LowerWing;
            if (unit.unittype == UnitTypes.Ranged || unit.unittype == UnitTypes.LightInfantry)
                return stableIndex % 2 == 0 ? BattleLane.LowerWing : BattleLane.UpperWing;
            return BattleLane.Centre;
        }

        private static void AddArmy(SectorCampaignBattle battle, FieldArmy army, FieldArmyHolder holder, int side, bool reinforcement)
        {
            BattleSimulationRequest additions = new BattleSimulationRequest();
            AddArmyInputs(additions, battle, army, holder, side, reinforcement);
            foreach (BattleFormationInput input in additions.Formations) battle.Simulation.AddFormation(input);
        }

        private void FinishBattle(int index, SectorCampaignBattle battle)
        {
            if (battle.ResultsApplied) return;
            battle.ResultsApplied = true;
            if (battle.NonCampaignBattle || battle.DebugScenario)
            {
                if (EnableDiagnosticLogging) Debug.Log("Finished custom sector battle " + battle.BattleId);
                ReleaseFinishedBattleUnlessViewed(battle); return;
            }
            BattleSimulationOutcome result = battle.Simulation.GetOutcome();
            ApplyResults(battle, result, battle.SideA, 0); ApplyResults(battle, result, battle.SideB, 1);
            if (battle.Garrison != null) ApplyResultToArmy(battle, result, battle.Garrison, null, 1);
            List<FieldArmyHolder> winners = result.WinningSide == 0 ? battle.SideA : battle.SideB;
            List<FieldArmyHolder> losers = result.WinningSide == 0 ? battle.SideB : battle.SideA;
            foreach (FieldArmyHolder winner in winners) if (winner != null) winner.MovementPenaltyUntilTurn = Owners.Instance.turncounter + 3;
            foreach (FieldArmyHolder loser in losers)
            {
                if (loser == null) continue; loser.CannotEngageUntilTurn = Owners.Instance.turncounter + 4;
                if (loser.fieldArmy.GrabArmySize() <= 0) Destroy(loser.gameObject);
            }
            foreach (FieldArmyHolder participant in battle.SideA) SetEncounter(participant, false);
            foreach (FieldArmyHolder participant in battle.SideB) SetEncounter(participant, false);
            if (battle.DefendedProvince != null && result.WinningSide == 0 && battle.ArmyA != null)
            { battle.Garrison.USDReserves.Clear(); battle.ArmyA.ConquerProvince(battle.DefendedProvince); }
            if (EnableDiagnosticLogging) Debug.Log("Finished sector battle " + battle.BattleId + " at tick " + battle.Simulation.Tick + "; winner=" + result.WinningSide);
            ReleaseFinishedBattleUnlessViewed(battle);
        }

        private void ReleaseFinishedBattleUnlessViewed(SectorCampaignBattle battle)
        {
            SectorBattlePresentation presentation = SectorBattlePresentation.Instance;
            if (presentation != null && presentation.IsViewingBattle(battle)) return;
            manuallySteppedBattles.Remove(battle);
            ActiveBattles.Remove(battle);
        }

        public void DismissBattle(SectorCampaignBattle battle)
        {
            if (battle != null && battle.Simulation != null && battle.Simulation.IsResolved)
            { manuallySteppedBattles.Remove(battle); ActiveBattles.Remove(battle); }
        }

        private static void RecordReplayFrame(SectorCampaignBattle battle)
        {
            if (battle?.Simulation is not SectorBattleSimulation simulation) return;
            battle.Replay.Add(new SectorReplayFrame
            {
                Tick = simulation.Tick,
                Formations = simulation.GetPresentationState(),
                Sectors = simulation.GetDebugState()
            });
        }

        private static void ApplyResults(SectorCampaignBattle battle, BattleSimulationOutcome result, List<FieldArmyHolder> armies, int side)
        { foreach (FieldArmyHolder holder in armies) if (holder != null) ApplyResultToArmy(battle, result, holder.fieldArmy, holder, side); }

        private static void ApplyResultToArmy(SectorCampaignBattle battle, BattleSimulationOutcome result, FieldArmy army,
            FieldArmyHolder holder, int side)
        {
            if (army == null) return;
            Dictionary<string, int> survivors = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (BattleFormationOutcome formation in result.Formations)
            {
                if (formation.Side != side || !battle.ArmySources.TryGetValue(formation.FormationId, out FieldArmyHolder source) || source != holder) continue;
                if (formation.RemainingStrength > 0 && !formation.Routed && battle.UnitSources.TryGetValue(formation.FormationId, out UnitSaveData unit))
                    survivors[unit.name] = survivors.TryGetValue(unit.name, out int count) ? count + 1 : 1;
                else if (battle.FormationSources.TryGetValue(formation.FormationId, out ArmyFormationRecord record) &&
                    record != null && record.origin == CampaignUnitOrigin.Levy && !string.IsNullOrEmpty(record.entitlementId))
                    BeginLevyRecovery(record.entitlementId);
            }
            foreach (ArmyReserves reserve in army.USDReserves) if (reserve != null && reserve.USD != null)
                reserve.amount = survivors.TryGetValue(reserve.USD.name, out int count) ? count : 0;
            army.formationRecords.RemoveAll(record => record == null || !battle.FormationSources.ContainsValue(record) ||
                !result.Formations.Exists(item => battle.FormationSources.TryGetValue(item.FormationId, out ArmyFormationRecord source) &&
                    source == record && item.RemainingStrength > 0 && !item.Routed));
        }

        private static void BeginLevyRecovery(string id)
        {
            if (Owners.Instance == null) return;
            Province province = Owners.Instance.provincelist.Find(item => item != null && item.levyEntitlements.Exists(levy => levy != null && levy.id == id));
            if (province != null) province.BeginLevyRecovery(id);
        }

        private static void ApplyTerrain(SectorBattleSimulation simulation, Province province)
        {
            if (province == null) return;
            SectorTerrain terrain = province.terrainProfile == CampaignTerrainProfile.Forested ? SectorTerrain.Forest :
                province.terrainProfile == CampaignTerrainProfile.Hilly ? SectorTerrain.Hill :
                province.terrainProfile == CampaignTerrainProfile.Mountainous ? SectorTerrain.RockyGround :
                province.terrainProfile == CampaignTerrainProfile.RoughCountry ? SectorTerrain.Scrubland : SectorTerrain.OpenPlain;
            foreach (SectorState sector in simulation.Sectors) simulation.SetTerrain(sector.Coordinate, terrain);
        }

        private bool Enabled => IsAuthority() && DeterministicBattleManager.Instance != null &&
            DeterministicBattleManager.Instance.BattleSystemMode == CampaignBattleSystemMode.Sector;
        private static void SetEncounter(FieldArmyHolder army, bool active)
        { if (army == null) return; if (active) { if (!army.flaglist.Contains("Battle")) army.flaglist.Add("Battle"); } else army.flaglist.Remove("Battle"); }
        private static int StableHash(string value) { unchecked { int hash = 17; foreach (char c in value ?? string.Empty) hash = hash * 31 + c; return hash; } }
        private static BattleSideCommandConfig CommanderFor(FieldArmyHolder army, int side, bool garrison)
        {
            ProjectX.TileBattle.TileGeneralPersonality personality = army != null ? ProjectX.TileBattle.TileBattleCampaignAdapter.CreatePersonality(army) : null;
            int competence = personality != null ? personality.Competence : garrison ? 25 : 50;
            int capacity = army != null && army.SectorCommandGroupCapacity > 0
                ? army.SectorCommandGroupCapacity : Mathf.Clamp(4 + competence / 25, 4, 8);
            return new BattleSideCommandConfig { Side = side,
                GeneralName = personality != null ? personality.Name : garrison ? "Garrison Commander" : "General",
                CommandGroupCapacity = capacity,
                PlayerControlled = army != null && army.IsFriendlyToLocalPlayer() };
        }
        private static bool IsAuthority() => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    }
}
