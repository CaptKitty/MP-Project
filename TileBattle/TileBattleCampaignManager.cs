using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProjectX.TileBattle
{
    [Serializable]
    public sealed class SavedTileCampaignBattle
    {
        public string BattleId;
        public string ArmyAId;
        public string ArmyBId;
        public string DefendedProvinceName;
        public int CompletedCommandRounds;
        public ulong StateHash;
        public TileGeneralPersonality LeftPersonality;
        public TileGeneralPersonality RightPersonality;
        public List<SavedTileParticipant> Participants = new List<SavedTileParticipant>();
    }

    [Serializable]
    public sealed class SavedTileParticipant
    {
        public string ArmyId;
        public int Side;
        public int JoinRound;
    }

    public sealed class TileCampaignBattle
    {
        public string BattleId;
        public FieldArmyHolder ArmyA;
        public FieldArmyHolder ArmyB;
        public FieldArmy Garrison;
        public Province DefendedProvince;
        public TileBattleSimulation Simulation;
        public Vector3 MapPosition;
        public float RoundAccumulator;
        public TileGeneralPersonality LeftPersonality;
        public TileGeneralPersonality RightPersonality;
        public string LeftDisplayName;
        public string RightDisplayName;
        public readonly Dictionary<int, string> UnitSourceNames = new Dictionary<int, string>();
        public readonly Dictionary<int, UnitSaveData> UnitSources = new Dictionary<int, UnitSaveData>();
        public readonly Dictionary<int, FieldArmyHolder> UnitArmySources = new Dictionary<int, FieldArmyHolder>();
        public readonly Dictionary<int, ArmyFormationRecord> UnitFormationSources = new Dictionary<int, ArmyFormationRecord>();
        public readonly List<FieldArmyHolder> LeftParticipants = new List<FieldArmyHolder>();
        public readonly List<FieldArmyHolder> RightParticipants = new List<FieldArmyHolder>();
        public readonly Dictionary<string, int> ParticipantJoinRounds = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    /// <summary>Campaign lifecycle adapter. The authoritative battle remains pure data.</summary>
    public sealed class TileBattleCampaignManager : MonoBehaviour
    {
        public static TileBattleCampaignManager Instance { get; private set; }
        [Min(0.1f)] public float CommandRoundsPerCampaignSecond = 1f;
        [Range(1, 20)] public int MaximumRoundsPerFrame = 5;
        public bool EnableDiagnosticLogging;
        [Header("Prototype battle presentation")]
        public bool LeaveFallenUnitsOnField = true;
        public bool DropEquipmentOnDeath = true;
        public readonly List<TileCampaignBattle> ActiveBattles = new List<TileCampaignBattle>();
        private int battleSequence;

        private void Awake()
        {
            Instance = this;
            TileBattlePresentation presentation = GetComponent<TileBattlePresentation>();
            if (presentation == null) presentation = gameObject.AddComponent<TileBattlePresentation>();
            presentation.LeaveFallenUnitsOnField = LeaveFallenUnitsOnField;
            presentation.DropEquipmentOnDeath = DropEquipmentOnDeath;
            presentation.Initialize(this);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (!IsAuthority() || ActiveBattles.Count == 0) return;
            float campaignSpeed = Owners.Instance != null
                ? (Owners.Instance.CampaignPaused ? 0f : Owners.Instance.CampaignSimulationSpeed)
                : 1f;
            if (campaignSpeed <= 0f) return;
            for (int i = ActiveBattles.Count - 1; i >= 0; i--)
            {
                TileCampaignBattle battle = ActiveBattles[i];
                battle.RoundAccumulator += Time.unscaledDeltaTime * CommandRoundsPerCampaignSecond * campaignSpeed;
                int rounds = Mathf.Min(MaximumRoundsPerFrame, Mathf.FloorToInt(battle.RoundAccumulator));
                if (rounds <= 0) continue;
                battle.RoundAccumulator -= rounds;
                for (int round = 0; round < rounds && !battle.Simulation.Result.Finished; round++)
                    battle.Simulation.RunCommandRound();
                if (battle.Simulation.Result.Finished) FinishBattle(i, battle);
            }
        }

        public bool TryStartBattle(FieldArmyHolder armyA, FieldArmyHolder armyB)
        {
            if (!IsAuthority() || armyA == null || armyB == null || armyA == armyB || armyA.fieldArmy == null || armyB.fieldArmy == null ||
                armyA.fieldArmy.nation == armyB.fieldArmy.nation) return false;
            TileCampaignBattle existingA = FindBattle(armyA), existingB = FindBattle(armyB);
            if (existingA != null && existingB == null) return TryJoinBattle(existingA, armyB);
            if (existingB != null && existingA == null) return TryJoinBattle(existingB, armyA);
            if (existingA != null || existingB != null) return true;
            string id = "tile_" + (Owners.Instance != null ? Owners.Instance.turncounter : 0) + "_" + (++battleSequence);
            TileCampaignBattle battle = CreateBattle(id, armyA, armyB, null);
            if (battle == null) return false;
            ActiveBattles.Add(battle); SetEncounter(armyA, true); SetEncounter(armyB, true);
            armyA.target = Vector3.zero; armyB.target = Vector3.zero;
            if (EnableDiagnosticLogging) Debug.Log("Started tile battle " + id + " with " + battle.Simulation.Units.Count + " formations");
            return true;
        }

        public bool TryJoinFriendlyBattle(FieldArmyHolder first, FieldArmyHolder second)
        {
            TileCampaignBattle battle = FindBattle(first) ?? FindBattle(second);
            if (battle == null) return false;
            FieldArmyHolder reinforcement = FindBattle(first) == null ? first : FindBattle(second) == null ? second : null;
            return reinforcement != null && TryJoinBattle(battle, reinforcement);
        }

        private bool TryJoinBattle(TileCampaignBattle battle, FieldArmyHolder reinforcement)
        {
            if (!IsAuthority() || battle == null || reinforcement == null || reinforcement.fieldArmy == null ||
                reinforcement.fieldArmy.nation == null || FindBattle(reinforcement) != null) return false;
            Nation leftNation = battle.ArmyA != null && battle.ArmyA.fieldArmy != null ? battle.ArmyA.fieldArmy.nation : null;
            Nation rightNation = battle.ArmyB != null && battle.ArmyB.fieldArmy != null ? battle.ArmyB.fieldArmy.nation :
                battle.DefendedProvince != null ? battle.DefendedProvince.nation : null;
            int side = reinforcement.fieldArmy.nation == leftNation ? 0 : reinforcement.fieldArmy.nation == rightNation ? 1 : -1;
            if (side < 0) return false;
            List<FieldArmyHolder> participants = side == 0 ? battle.LeftParticipants : battle.RightParticipants;
            participants.Add(reinforcement); SetEncounter(reinforcement, true); reinforcement.target = Vector3.zero;
            battle.ParticipantJoinRounds[CampaignBattleStateAdapter.GetArmyId(reinforcement)] = battle.Simulation.CommandRound;
            AddArmy(battle.Simulation, battle, reinforcement.fieldArmy, side, reinforcement, true);
            if (EnableDiagnosticLogging) Debug.Log(reinforcement.name + " joins " + battle.BattleId + " on side " + side);
            return true;
        }

        public bool TryStartGarrisonBattle(FieldArmyHolder attacker, Province province)
        {
            if (!IsAuthority() || attacker == null || attacker.fieldArmy == null || province == null || province.garrison == null ||
                province.nation == attacker.fieldArmy.nation) return false;
            if (FindBattle(attacker) != null) return true;
            if (province.garrison.GrabArmySize() <= 0) { attacker.ConquerProvince(province); return true; }
            if (ActiveBattles.Exists(item => item.DefendedProvince == province)) return true;
            string id = "tile_garrison_" + (Owners.Instance != null ? Owners.Instance.turncounter : 0) + "_" + (++battleSequence);
            TileCampaignBattle battle = CreateBattle(id, attacker, null, province);
            if (battle == null) return false;
            ActiveBattles.Add(battle); SetEncounter(attacker, true); attacker.target = Vector3.zero;
            if (EnableDiagnosticLogging) Debug.Log("Started tile garrison battle " + id + " at " + province.name);
            return true;
        }

        public TileCampaignBattle FindBattle(FieldArmyHolder army) => ActiveBattles.Find(item =>
            item.ArmyA == army || item.ArmyB == army || item.LeftParticipants.Contains(army) || item.RightParticipants.Contains(army));

        public List<SavedTileCampaignBattle> CaptureActiveBattles()
        {
            List<SavedTileCampaignBattle> result = new List<SavedTileCampaignBattle>(ActiveBattles.Count);
            for (int i = 0; i < ActiveBattles.Count; i++)
            {
                TileCampaignBattle battle = ActiveBattles[i];
                SavedTileCampaignBattle saved = new SavedTileCampaignBattle { BattleId = battle.BattleId,
                    ArmyAId = CampaignBattleStateAdapter.GetArmyId(battle.ArmyA),
                    ArmyBId = CampaignBattleStateAdapter.GetArmyId(battle.ArmyB),
                    DefendedProvinceName = battle.DefendedProvince != null ? battle.DefendedProvince.name : string.Empty,
                    CompletedCommandRounds = battle.Simulation.CommandRound,
                    StateHash = battle.Simulation.ComputeHash(),
                    LeftPersonality = battle.LeftPersonality,
                    RightPersonality = battle.RightPersonality };
                for (int p = 0; p < battle.LeftParticipants.Count; p++) AddSavedParticipant(saved, battle, battle.LeftParticipants[p], 0);
                for (int p = 0; p < battle.RightParticipants.Count; p++) AddSavedParticipant(saved, battle, battle.RightParticipants[p], 1);
                result.Add(saved);
            }
            return result;
        }

        private static void AddSavedParticipant(SavedTileCampaignBattle saved, TileCampaignBattle battle, FieldArmyHolder army, int side)
        {
            if (army == null) return; string id = CampaignBattleStateAdapter.GetArmyId(army);
            saved.Participants.Add(new SavedTileParticipant { ArmyId = id, Side = side,
                JoinRound = battle.ParticipantJoinRounds.TryGetValue(id, out int round) ? round : 0 });
        }

        public void RestoreActiveBattles(List<SavedTileCampaignBattle> saved)
        {
            if (!IsAuthority() || saved == null || Owners.Instance == null) return;
            for (int i = 0; i < saved.Count; i++)
            {
                SavedTileCampaignBattle state = saved[i];
                if (state == null || ActiveBattles.Exists(item => item.BattleId == state.BattleId)) continue;
                FieldArmyHolder armyA = Owners.Instance.armylist.Find(item => CampaignBattleStateAdapter.GetArmyId(item) == state.ArmyAId);
                FieldArmyHolder armyB = Owners.Instance.armylist.Find(item => CampaignBattleStateAdapter.GetArmyId(item) == state.ArmyBId);
                Province province = !string.IsNullOrEmpty(state.DefendedProvinceName)
                    ? Owners.Instance.provincelist.Find(item => item != null && item.name == state.DefendedProvinceName) : null;
                if (armyA == null || armyB == null && province == null) continue;
                TileCampaignBattle battle = CreateBattle(state.BattleId, armyA, armyB, province, state.LeftPersonality, state.RightPersonality);
                if (battle == null) continue;
                int rounds = Mathf.Clamp(state.CompletedCommandRounds, 0, battle.Simulation.Rules.SafetyMaximumRounds);
                for (int round = 0; round < rounds && !battle.Simulation.Result.Finished; round++)
                { AddScheduledReinforcements(state, battle); battle.Simulation.RunCommandRound(); }
                AddScheduledReinforcements(state, battle);
                if (battle.Simulation.Result.Finished) continue;
                ActiveBattles.Add(battle); SetEncounter(armyA, true); SetEncounter(armyB, true);
            }
        }

        public void ReceiveNetworkState(List<SavedTileCampaignBattle> states)
        {
            if (IsAuthority() || states == null || Owners.Instance == null) return;
            HashSet<string> received = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < states.Count; i++)
            {
                SavedTileCampaignBattle state = states[i]; if (state == null) continue; received.Add(state.BattleId);
                TileCampaignBattle battle = ActiveBattles.Find(item => item.BattleId == state.BattleId);
                if (battle == null)
                {
                    FieldArmyHolder armyA = Owners.Instance.armylist.Find(item => CampaignBattleStateAdapter.GetArmyId(item) == state.ArmyAId);
                    FieldArmyHolder armyB = Owners.Instance.armylist.Find(item => CampaignBattleStateAdapter.GetArmyId(item) == state.ArmyBId);
                    Province province = !string.IsNullOrEmpty(state.DefendedProvinceName)
                        ? Owners.Instance.provincelist.Find(item => item != null && item.name == state.DefendedProvinceName) : null;
                    if (armyA == null || armyB == null && province == null) continue;
                    battle = CreateBattle(state.BattleId, armyA, armyB, province, state.LeftPersonality, state.RightPersonality);
                    if (battle == null) continue;
                    ActiveBattles.Add(battle); SetEncounter(armyA, true); SetEncounter(armyB, true);
                }
                int targetRound = Mathf.Clamp(state.CompletedCommandRounds, 0, battle.Simulation.Rules.SafetyMaximumRounds);
                while (battle.Simulation.CommandRound < targetRound && !battle.Simulation.Result.Finished)
                { AddScheduledReinforcements(state, battle); battle.Simulation.RunCommandRound(); }
                AddScheduledReinforcements(state, battle);
                if (battle.Simulation.CommandRound == targetRound && state.StateHash != 0 &&
                    battle.Simulation.ComputeHash() != state.StateHash)
                {
                    ulong badHash = battle.Simulation.ComputeHash();
                    TileCampaignBattle rebuilt = RebuildNetworkBattle(state, battle, targetRound);
                    ulong rebuiltHash = rebuilt != null ? rebuilt.Simulation.ComputeHash() : 0;
                    if (rebuiltHash != state.StateHash)
                        Debug.LogError("Tile battle desync recovery failed " + state.BattleId + " round=" + targetRound +
                            " server=" + state.StateHash + " original=" + badHash + " rebuilt=" + rebuiltHash);
                    else if (EnableDiagnosticLogging)
                        Debug.LogWarning("Recovered tile battle desync " + state.BattleId + " at round " + targetRound);
                }
            }
            for (int i = ActiveBattles.Count - 1; i >= 0; i--)
            {
                TileCampaignBattle battle = ActiveBattles[i]; if (received.Contains(battle.BattleId)) continue;
                SetEncounter(battle.ArmyA, false); SetEncounter(battle.ArmyB, false); ActiveBattles.RemoveAt(i);
            }
        }

        private TileCampaignBattle RebuildNetworkBattle(SavedTileCampaignBattle state, TileCampaignBattle existing, int targetRound)
        {
            int index = ActiveBattles.IndexOf(existing);
            FieldArmyHolder armyA = existing.ArmyA, armyB = existing.ArmyB; Province province = existing.DefendedProvince;
            TileCampaignBattle rebuilt = CreateBattle(state.BattleId, armyA, armyB, province, state.LeftPersonality, state.RightPersonality);
            if (rebuilt == null) return null;
            while (rebuilt.Simulation.CommandRound < targetRound && !rebuilt.Simulation.Result.Finished)
            { AddScheduledReinforcements(state, rebuilt); rebuilt.Simulation.RunCommandRound(); }
            AddScheduledReinforcements(state, rebuilt);
            if (index >= 0) ActiveBattles[index] = rebuilt; else ActiveBattles.Add(rebuilt);
            SetEncounter(armyA, true); SetEncounter(armyB, true);
            return rebuilt;
        }

        private static void AddScheduledReinforcements(SavedTileCampaignBattle state, TileCampaignBattle battle)
        {
            if (state == null || state.Participants == null || battle == null || Owners.Instance == null) return;
            for (int i = 0; i < state.Participants.Count; i++)
            {
                SavedTileParticipant participant = state.Participants[i];
                if (participant == null || participant.JoinRound != battle.Simulation.CommandRound) continue;
                bool alreadyPresent = battle.LeftParticipants.Exists(item => CampaignBattleStateAdapter.GetArmyId(item) == participant.ArmyId) ||
                    battle.RightParticipants.Exists(item => CampaignBattleStateAdapter.GetArmyId(item) == participant.ArmyId);
                if (alreadyPresent) continue;
                FieldArmyHolder army = Owners.Instance.armylist.Find(item => CampaignBattleStateAdapter.GetArmyId(item) == participant.ArmyId);
                if (army == null || army.fieldArmy == null) continue;
                List<FieldArmyHolder> side = participant.Side == 0 ? battle.LeftParticipants : battle.RightParticipants;
                side.Add(army); battle.ParticipantJoinRounds[participant.ArmyId] = participant.JoinRound;
                AddArmy(battle.Simulation, battle, army.fieldArmy, participant.Side, army, true); SetEncounter(army, true);
            }
        }

        private TileCampaignBattle CreateBattle(string id, FieldArmyHolder armyA, FieldArmyHolder armyB, Province province,
            TileGeneralPersonality savedLeftPersonality = null, TileGeneralPersonality savedRightPersonality = null)
        {
            FieldArmy rightArmy = armyB != null ? armyB.fieldArmy : province != null ? province.garrison : null;
            if (armyA.fieldArmy.GrabArmySize() <= 0 || rightArmy == null || rightArmy.GrabArmySize() <= 0) return null;
            TileBattleRules rules = new TileBattleRules();
            TileGeneralPersonality leftPersonality = savedLeftPersonality ?? TileBattleCampaignAdapter.CreatePersonality(armyA);
            TileGeneralPersonality rightPersonality = savedRightPersonality ?? (armyB != null ? TileBattleCampaignAdapter.CreatePersonality(armyB) :
                new TileGeneralPersonality { Name = province.name + " Garrison", Defensive = 70, Patient = 40 });
            TileBattleSimulation simulation = new TileBattleSimulation(rules, new PersonalityTileGeneral(leftPersonality), new PersonalityTileGeneral(rightPersonality));
            TileCampaignBattle result = new TileCampaignBattle { BattleId = id, ArmyA = armyA, ArmyB = armyB,
                Garrison = province != null ? province.garrison : null, DefendedProvince = province, Simulation = simulation,
                MapPosition = armyB != null ? (armyA.transform.position + armyB.transform.position) * 0.5f : armyA.transform.position,
                LeftPersonality = leftPersonality, RightPersonality = rightPersonality,
                LeftDisplayName = armyA != null ? armyA.gameObject.name : leftPersonality.Name,
                RightDisplayName = armyB != null ? armyB.gameObject.name : province != null ? province.name + " Garrison" : rightPersonality.Name };
            result.LeftParticipants.Add(armyA);
            if (armyB != null) result.RightParticipants.Add(armyB);
            result.ParticipantJoinRounds[CampaignBattleStateAdapter.GetArmyId(armyA)] = 0;
            if (armyB != null) result.ParticipantJoinRounds[CampaignBattleStateAdapter.GetArmyId(armyB)] = 0;
            AddArmy(simulation, result, armyA.fieldArmy, 0, armyA, false);
            AddArmy(simulation, result, rightArmy, 1, armyB, false);
            ApplyTerrain(simulation.Grid, province != null ? province : armyA.GrabNearestProvince());
            return simulation.Units.Exists(item => item.Side == 0) && simulation.Units.Exists(item => item.Side == 1) ? result : null;
        }

        private static void AddArmy(TileBattleSimulation simulation, TileCampaignBattle battle, FieldArmy army, int side,
            FieldArmyHolder sourceArmy, bool reinforcement)
        {
            army.ReconcileFormationRecords();
            List<ArmyFormationRecord> unusedRecords = new List<ArmyFormationRecord>(army.formationRecords);
            List<ArmyReserves> reserves = new List<ArmyReserves>(army.USDReserves);
            reserves.Sort((a, b) => string.CompareOrdinal(a != null && a.USD != null ? a.USD.name : string.Empty,
                b != null && b.USD != null ? b.USD.name : string.Empty));
            List<UnitSaveData> formations = new List<UnitSaveData>();
            for (int r = 0; r < reserves.Count; r++)
            {
                ArmyReserves reserve = reserves[r]; if (reserve == null || reserve.USD == null) continue;
                for (int copy = 0; copy < reserve.amount; copy++) formations.Add(reserve.USD);
            }
            formations.Sort((a, b) =>
            {
                bool av = IsVanguard(TileBattleCampaignAdapter.CreateDefinition(a));
                bool bv = IsVanguard(TileBattleCampaignAdapter.CreateDefinition(b));
                int role = bv.CompareTo(av); return role != 0 ? role : string.CompareOrdinal(a.name, b.name);
            });
            int vanguardSlots = Mathf.Clamp(Mathf.CeilToInt(formations.Count * .2f), 1, 4);
            int reserveSlots = formations.Count >= 5 ? Mathf.Max(1, Mathf.FloorToInt(formations.Count * .2f)) : 0;
            int frontage = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, formations.Count) * 2f)), 3,
                Mathf.Max(3, simulation.Grid.Height - 6));
            int firstRow = Mathf.Max(1, (simulation.Grid.Height - frontage) / 2);
            int index = 0;
            for (int u = 0; u < simulation.Units.Count; u++)
                if (simulation.Units[u].Side == side) index = Mathf.Max(index, simulation.Units[u].Id - side * 10000);
            int mainEntryIndex = 0;
            for (int f = 0; f < formations.Count; f++)
            {
                UnitSaveData source = formations[f]; TileBattleUnitDefinition definition = TileBattleCampaignAdapter.CreateDefinition(source);
                int id = side * 10000 + index + 1;
                int rank = index / frontage;
                int row = firstRow + index % frontage;
                int x = side == 0 ? 3 - rank : simulation.Grid.Width - 4 + rank;
                x = Mathf.Clamp(x, 1, simulation.Grid.Width - 2);
                SavedFormationDeployment saved = army.battleDeployment != null
                    ? army.battleDeployment.Formations.Find(item => item != null && item.UnitName == source.name) : null;
                bool explicitlyReserved = saved != null && saved.Reserve;
                bool vanguard = f < vanguardSlots && !explicitlyReserved;
                // A reinforcing army is an arriving main force, not a collection of tactical reserves.
                // Deploy all of its formations together on the round after it reaches the battle.
                bool reserve = !reinforcement && (explicitlyReserved || !vanguard && f >= formations.Count - reserveSlots);
                if (reinforcement) vanguard = false;
                if (vanguard) x = side == 0 ? 0 : simulation.Grid.Width - 1;
                int deploymentRound = reinforcement ? simulation.CommandRound + 1 :
                    !vanguard && !reserve ? simulation.Rules.VanguardRounds + 1 + mainEntryIndex++ % 2 : 0;
                TileBattleUnit unit = new TileBattleUnit { Id = id, Side = side, Definition = definition,
                    Position = new TileCoord(x, row), Facing = side == 0 ? TileFacing.East : TileFacing.West,
                    Strength = definition.Strength, IsVanguard = vanguard, IsReserve = reserve,
                    DeploymentRound = deploymentRound, Deployed = vanguard };
                simulation.AddUnit(unit); battle.UnitSourceNames[id] = source.name; battle.UnitSources[id] = source;
                battle.UnitArmySources[id] = sourceArmy;
                int recordIndex = unusedRecords.FindIndex(record => record != null && record.unit != null && record.unit.name == source.name);
                if (recordIndex >= 0) { battle.UnitFormationSources[id] = unusedRecords[recordIndex]; unusedRecords.RemoveAt(recordIndex); }
                index++;
            }
        }

        private static bool IsVanguard(TileBattleUnitDefinition definition) => definition.Cavalry || definition.Ranged || definition.BaseMass < 100;

        private static void ApplyTerrain(TileBattleGrid grid, Province province)
        {
            if (province == null) return;
            CampaignTerrainProfile profile = province.terrainProfile;
            if (profile == CampaignTerrainProfile.Auto) return;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                TileTerrain terrain = TileTerrain.Open; int cost = 1;
                // Strong battlefield identities: forests are connected bands and hills occupy a defensible sector.
                if (profile == CampaignTerrainProfile.Forested &&
                    (y >= grid.Height / 4 && y < grid.Height * 3 / 4 || x >= grid.Width * 2 / 5 && x < grid.Width * 3 / 5))
                { terrain = TileTerrain.Forest; cost = 2; }
                else if ((profile == CampaignTerrainProfile.Hilly || profile == CampaignTerrainProfile.Mountainous) &&
                    x >= grid.Width / 3 && x < grid.Width * 2 / 3 && y >= grid.Height / 3 && y < grid.Height * 5 / 6)
                { terrain = TileTerrain.Hill; cost = 1; }
                grid.SetTerrain(new TileCoord(x, y), terrain, cost);
            }
        }

        private void FinishBattle(int index, TileCampaignBattle battle)
        {
            int winner = battle.Simulation.Result.WinningSide;
            for (int i = 0; i < battle.LeftParticipants.Count; i++)
                ApplyArmyResult(battle, battle.LeftParticipants[i] != null ? battle.LeftParticipants[i].fieldArmy : null, 0, battle.LeftParticipants[i]);
            for (int i = 0; i < battle.RightParticipants.Count; i++)
                ApplyArmyResult(battle, battle.RightParticipants[i] != null ? battle.RightParticipants[i].fieldArmy : null, 1, battle.RightParticipants[i]);
            if (battle.Garrison != null) ApplyArmyResult(battle, battle.Garrison, 1, null);

            List<FieldArmyHolder> winners = winner == 0 ? battle.LeftParticipants : battle.RightParticipants;
            List<FieldArmyHolder> losers = winner == 0 ? battle.RightParticipants : battle.LeftParticipants;
            for (int i = 0; i < winners.Count; i++)
            {
                RecoverVictoryLosses(battle, winners[i], winner, 2);
                if (winners[i] != null && Owners.Instance != null)
                    winners[i].MovementPenaltyUntilTurn = Owners.Instance.turncounter + 3;
            }
            Vector3 victorPosition = winners.Count > 0 && winners[0] != null ? winners[0].transform.position : battle.MapPosition;
            for (int i = 0; i < losers.Count; i++) ResolveDefeatedArmy(losers[i], victorPosition);
            for (int i = 0; i < battle.LeftParticipants.Count; i++) SetEncounter(battle.LeftParticipants[i], false);
            for (int i = 0; i < battle.RightParticipants.Count; i++) SetEncounter(battle.RightParticipants[i], false);
            ActiveBattles.RemoveAt(index);
            if (EnableDiagnosticLogging) Debug.Log("Finished tile battle " + battle.BattleId + " after " + battle.Simulation.CommandRound + " rounds; winner=" + winner);
            if (battle.DefendedProvince != null)
            {
                if (winner == 0 && battle.ArmyA != null)
                {
                    if (battle.Garrison != null) battle.Garrison.USDReserves.Clear();
                    battle.ArmyA.ConquerProvince(battle.DefendedProvince);
                    if (battle.DefendedProvince.garrison != null) battle.DefendedProvince.garrison.USDReserves.Clear();
                }
            }
        }

        private static void ApplyArmyResult(TileCampaignBattle battle, FieldArmy army, int side, FieldArmyHolder sourceArmy)
        {
            if (army == null) return;
            Dictionary<string, int> survivors = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < battle.Simulation.Units.Count; i++)
            {
                TileBattleUnit unit = battle.Simulation.Units[i];
                if (unit.Side != side || !battle.UnitArmySources.TryGetValue(unit.Id, out FieldArmyHolder originArmy) || originArmy != sourceArmy ||
                    unit.Strength <= 0 || unit.State == TileUnitState.Destroyed) continue;
                string name = battle.UnitSourceNames.TryGetValue(unit.Id, out string sourceName)
                    ? sourceName
                    : unit.Definition.Id;
                survivors[name] = survivors.TryGetValue(name, out int count) ? count + 1 : 1;
            }
            for (int i = 0; i < army.USDReserves.Count; i++)
            {
                ArmyReserves reserve = army.USDReserves[i]; if (reserve == null || reserve.USD == null) continue;
                reserve.amount = survivors.TryGetValue(reserve.USD.name, out int count) ? count : 0;
            }
            army.formationRecords.Clear();
            for (int i = 0; i < battle.Simulation.Units.Count; i++)
            {
                TileBattleUnit unit = battle.Simulation.Units[i];
                if (unit.Side == side && unit.Strength > 0 && unit.State != TileUnitState.Destroyed &&
                    battle.UnitArmySources.TryGetValue(unit.Id, out FieldArmyHolder origin) && origin == sourceArmy &&
                    battle.UnitFormationSources.TryGetValue(unit.Id, out ArmyFormationRecord record)) army.formationRecords.Add(record);
            }
            // Destroyed levies return to their province's recoverable entitlement pool.
            foreach (KeyValuePair<int, ArmyFormationRecord> pair in battle.UnitFormationSources)
            {
                ArmyFormationRecord record = pair.Value;
                if (record == null || record.origin != CampaignUnitOrigin.Levy || string.IsNullOrEmpty(record.entitlementId) ||
                    !battle.UnitArmySources.TryGetValue(pair.Key, out FieldArmyHolder origin) || origin != sourceArmy) continue;
                TileBattleUnit unit = battle.Simulation.Units.Find(candidate => candidate.Id == pair.Key);
                if (unit != null && unit.Strength > 0 && unit.State != TileUnitState.Destroyed) continue;
                BeginLevyRecovery(record.entitlementId);
            }
        }

        private static void RecoverVictoryLosses(TileCampaignBattle battle, FieldArmyHolder army, int side, int maximum)
        {
            if (army == null || army.fieldArmy == null || maximum <= 0) return;
            int recovered = 0;
            for (int i = 0; i < battle.Simulation.Units.Count && recovered < maximum; i++)
            {
                TileBattleUnit unit = battle.Simulation.Units[i];
                if (unit.Side != side || !battle.UnitArmySources.TryGetValue(unit.Id, out FieldArmyHolder source) || source != army ||
                    unit.Strength > 0 && unit.State != TileUnitState.Destroyed) continue;
                if (battle.UnitSources.TryGetValue(unit.Id, out UnitSaveData data) && data != null)
                {
                    if (battle.UnitFormationSources.TryGetValue(unit.Id, out ArmyFormationRecord record) && record != null)
                    {
                        army.fieldArmy.AddTroop(data, 1, true, record.origin, record.entitlementId);
                        if (record.origin == CampaignUnitOrigin.Levy) MarkLevyRaised(record.entitlementId, army.NetworkArmyId);
                    }
                    else army.fieldArmy.AddTroop(data, 1, true);
                    battle.Simulation.Result.RecoveredFormations[unit.Id] = 1;
                    recovered++;
                }
            }
        }

        private static void BeginLevyRecovery(string entitlementId)
        {
            if (Owners.Instance == null) return;
            Province province = Owners.Instance.provincelist.Find(candidate => candidate != null && candidate.levyEntitlements != null &&
                candidate.levyEntitlements.Exists(item => item != null && item.id == entitlementId));
            if (province != null) province.BeginLevyRecovery(entitlementId);
        }

        private static void MarkLevyRaised(string entitlementId, string armyId)
        {
            if (Owners.Instance == null) return;
            foreach (Province province in Owners.Instance.provincelist)
            {
                ProvinceLevyEntitlement entitlement = province != null && province.levyEntitlements != null
                    ? province.levyEntitlements.Find(item => item != null && item.id == entitlementId) : null;
                if (entitlement == null) continue;
                entitlement.state = LevyEntitlementState.Raised; entitlement.raisedArmyId = armyId; entitlement.remainingTicks = 0; return;
            }
        }

        private static void ResolveDefeatedArmy(FieldArmyHolder army, Vector3 victorPosition)
        {
            if (army == null || army.fieldArmy == null) return;
            if (army.fieldArmy.GrabArmySize() <= 0) { Destroy(army.gameObject); return; }
            List<Province> friendly = Owners.Instance.provincelist.FindAll(item => item != null && item.nation == army.fieldArmy.nation);
            if (friendly.Count == 0) return;
            Province nearest = null, safest = null; float nearestDistance = float.MaxValue, safestDistance = -1f;
            for (int i = 0; i < friendly.Count; i++)
            {
                float fromArmy = army.GrabDistanceToProvince(friendly[i]);
                float fromVictor = Vector3.Distance(victorPosition, new Vector3(friendly[i].position.x - 364f, friendly[i].position.y - 232f));
                if (fromArmy < nearestDistance) { nearestDistance = fromArmy; nearest = friendly[i]; }
                if (fromVictor > safestDistance) { safestDistance = fromVictor; safest = friendly[i]; }
            }
            if (nearest != null) army.SetPositionTo(nearest);
            if (safest != null) { army.SetTarget(safest); army.TargetProvince = safest; }
            army.CannotEngageUntilTurn = Owners.Instance.turncounter + 4;
        }

        private static void SetEncounter(FieldArmyHolder army, bool active)
        {
            if (army == null) return;
            if (active) { if (!army.flaglist.Contains("Battle")) army.flaglist.Add("Battle"); }
            else army.flaglist.Remove("Battle");
        }

        private static bool IsAuthority() => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    }
}
