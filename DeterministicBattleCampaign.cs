using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectX.DeterministicBattle;

public enum CampaignBattleSystemMode { Legacy, Deterministic, TileBased }

[Serializable]
public sealed class SavedBattleDeployment
{
    public List<SavedFormationDeployment> Formations = new List<SavedFormationDeployment>();
}

[Serializable]
public sealed class SavedFormationDeployment
{
    public string UnitName;
    public int RelativeX;
    public int RelativeY;
    public int FacingX;
    public int FacingY = BattleSimulation.PositionScale;
    public FormationOrder InitialOrder = FormationOrder.Advance;
    public bool Reserve;
}

[Serializable]
public sealed class ActiveBattleSummary
{
    public string BattleId;
    public string ArmyA;
    public string ArmyB;
    public int Tick;
    public ulong StateHash;
    public ulong Seed;
    public ulong RngState;
    public string Phase;
    public int Formations;
    public int Combatants;
    public int SideACasualties;
    public int SideBCasualties;
    public int RoutingFormations;
    public int ActiveProjectiles;
    public int ProjectilesLaunched;
    public int ProjectileHits;
    public int RemainingAmmunition;
    public int ChargingFormations;
    public int Advantage;
    public bool Finished;
    public int WinningSide;
    public int AverageMorale;
    public int AverageCohesion;
    public List<FormationBattleSummary> FormationDetails;
    public int TerrainAreas;
    public string EncounterProvince;
    public string TerrainArchetype;
    public int Reinforcements;
    public int ActiveEffects;
    public List<string> GeneralDecisions;
    public List<string> GeneralCooldowns;
    public List<string> ReinforcementArrivals;
}

[Serializable]
public sealed class FormationBattleSummary
{
    public int FormationId;
    public int Side;
    public string UnitName;
    public string Status;
    public string Order;
    public int Morale;
    public int Cohesion;
    public int Living;
    public int Casualties;
    public int TargetFormationId;
    public int Frontage;
    public int Depth;
    public bool Pursuing;
    public string Terrain;
    public List<string> ActiveAbilities;
}

public sealed class CampaignActiveBattle
{
    public FieldArmyHolder ArmyA;
    public FieldArmyHolder ArmyB;
    public FieldArmy GarrisonArmy;
    public Province DefendedProvince;
    public BattleSimulation Simulation;
    public BattleStartState StartState;
    public readonly List<CampaignBattleReinforcement> ReinforcementArmies = new List<CampaignBattleReinforcement>();
    public readonly List<int> ArmyAFormationIds = new List<int>();
    public readonly List<int> ArmyBFormationIds = new List<int>();
    public int LastChecksumReportTick;

    public ActiveBattleSummary GetSummary()
    {
        int aliveA = 0, aliveB = 0, startA = 0, startB = 0, routing = 0, remainingAmmunition = 0;
        for (int i = 0; i < Simulation.Combatants.Count; i++)
        {
            SimCombatant combatant = Simulation.Combatants[i];
            if (combatant.Alive) remainingAmmunition += combatant.Ammunition;
            SimFormation formation = Simulation.Formations.Find(item => item.Id == combatant.FormationId);
            if (formation.Side == 0) { startA++; if (combatant.Alive) aliveA++; }
            else { startB++; if (combatant.Alive) aliveB++; }
        }
        int charging = 0, morale = 0, cohesion = 0;
        List<FormationBattleSummary> details = new List<FormationBattleSummary>();
        for (int i = 0; i < Simulation.Formations.Count; i++)
        {
            SimFormation formation = Simulation.Formations[i];
            if (formation.Status == FormationStatus.Routing) routing++;
            if (formation.Status == FormationStatus.Charging) charging++;
            morale += formation.Morale; cohesion += formation.Cohesion;
            int living = 0;
            for (int c = 0; c < formation.CombatantIds.Count; c++)
                if (Simulation.Combatants[formation.CombatantIds[c]].Alive) living++;
            BattleUnitDefinition definition = StartState.Definitions.Find(item => item.DefinitionId == formation.DefinitionId);
            BattleTerrainArea terrain = StartState.Terrain.Find(item => item.Id == formation.TerrainAreaId);
            details.Add(new FormationBattleSummary
            {
                FormationId = formation.Id, Side = formation.Side,
                UnitName = definition != null ? definition.UnitName : formation.DefinitionId.ToString(),
                Status = formation.Status.ToString(), Order = formation.Order.ToString(),
                Morale = formation.Morale, Cohesion = formation.Cohesion, Living = living,
                Casualties = formation.CombatantIds.Count - living, TargetFormationId = formation.TargetFormationId,
                Frontage = formation.Frontage, Depth = formation.Depth, Pursuing = formation.Pursuing
                ,Terrain = terrain != null ? terrain.Kind.ToString() : BattleTerrainKind.Open.ToString(),
                ActiveAbilities = Simulation.Effects.FindAll(item => item.FormationId == formation.Id)
                    .ConvertAll(item => item.Ability + " until " + item.EndTick)
            });
        }
        return new ActiveBattleSummary
        {
            BattleId = StartState.BattleId, ArmyA = ArmyA != null ? ArmyA.gameObject.name : StartState.SideAArmyId,
            ArmyB = ArmyB != null ? ArmyB.gameObject.name : StartState.SideBArmyId,
            Tick = Simulation.Tick, StateHash = Simulation.ComputeHash(), Formations = Simulation.Formations.Count,
            Seed = StartState.Seed, RngState = Simulation.Rng.State, Phase = Simulation.Status.ToString(),
            Combatants = Simulation.Combatants.Count, SideACasualties = startA - aliveA,
            ActiveProjectiles = Simulation.Projectiles.FindAll(item => item.Active).Count,
            ProjectilesLaunched = Simulation.Telemetry.ProjectilesLaunched,
            ProjectileHits = Simulation.Telemetry.ProjectileHits,
            RemainingAmmunition = remainingAmmunition,
            ChargingFormations = charging,
            AverageMorale = Simulation.Formations.Count > 0 ? morale / Simulation.Formations.Count : 0,
            AverageCohesion = Simulation.Formations.Count > 0 ? cohesion / Simulation.Formations.Count : 0,
            FormationDetails = details,
            TerrainAreas = StartState.Terrain.Count,
            EncounterProvince = StartState.BattlefieldSource,
            TerrainArchetype = StartState.TerrainArchetype,
            Reinforcements = ReinforcementArmies.Count,
            ActiveEffects = Simulation.Effects.Count,
            GeneralDecisions = Simulation.Generals.ConvertAll(item => item.Name + ": " + (item.LastDecision ?? "Awaiting orders")),
            GeneralCooldowns = Simulation.Generals.ConvertAll(item => item.Name + ": " +
                string.Join(",", Array.ConvertAll(item.NextAbilityTicks, tick => tick.ToString()))),
            ReinforcementArrivals = ReinforcementArmies.ConvertAll(item =>
                (item.Army != null ? item.Army.gameObject.name : "Army") + " side " + item.Side + " tick " + item.ArrivalTick),
            SideBCasualties = startB - aliveB, RoutingFormations = routing,
            Advantage = aliveA - aliveB, Finished = Simulation.Status == BattleStatus.Finished,
            WinningSide = Simulation.WinningSide
        };
    }
}

[Serializable]
public sealed class BattleChecksumReport
{
    public string BattleId;
    public int Tick;
    public ulong Hash;
}

[Serializable]
public sealed class SavedActiveBattle
{
    public BattleStartState StartState;
    public int Tick;
    public string ArmyAId;
    public string ArmyBId;
    public string DefendedProvinceName;
    public List<BattleCommandRecord> Commands = new List<BattleCommandRecord>();
    public List<SavedBattleReinforcement> Reinforcements = new List<SavedBattleReinforcement>();
}
[Serializable] public sealed class SavedBattleReinforcement { public string ArmyId; public int Side; public int ArrivalTick; }

public sealed class CampaignBattleReinforcement
{
    public FieldArmyHolder Army;
    public int Side;
    public int ArrivalTick;
    public readonly List<int> FormationIds = new List<int>();
}

public static class CampaignBattleStateAdapter
{
    public static BattleStartState Create(FieldArmyHolder armyA, FieldArmyHolder armyB, string battleId, ulong seed)
    {
        BattleStartState state = new BattleStartState
        {
            BattleId = battleId, Seed = seed, TickRate = 10,
            SideAArmyId = GetArmyId(armyA), SideBArmyId = GetArmyId(armyB)
        };
        Dictionary<string, int> definitionIds = new Dictionary<string, int>(StringComparer.Ordinal);
        AddArmy(state, armyA.fieldArmy, 0, definitionIds);
        AddArmy(state, armyB.fieldArmy, 1, definitionIds);
        state.Generals.Add(CreateGeneral(armyA, 0));
        state.Generals.Add(CreateGeneral(armyB, 1));
        Province encounter = armyA.GrabNearestProvince();
        state.BattlefieldSource = encounter != null ? encounter.name : "open";
        Province battlefieldProvince = Owners.Instance != null
            ? Owners.Instance.provincelist.Find(item => item != null && item.name == state.BattlefieldSource)
            : null;
        AddDeterministicTerrain(state, battlefieldProvince);
        return state;
    }

    public static BattleStartState CreateGarrisonBattle(FieldArmyHolder attacker, Province province, string battleId, ulong seed)
    {
        BattleStartState state = new BattleStartState
        {
            BattleId = battleId, Seed = seed, TickRate = 10,
            SideAArmyId = GetArmyId(attacker), SideBArmyId = "garrison:" + province.name,
            BattlefieldSource = province.name
        };
        Dictionary<string, int> definitionIds = new Dictionary<string, int>(StringComparer.Ordinal);
        AddArmy(state, attacker.fieldArmy, 0, definitionIds);
        AddArmy(state, province.garrison, 1, definitionIds);
        state.Generals.Add(CreateGeneral(attacker, 0));
        state.Generals.Add(new BattleGeneralProfile { Side = 1, Name = province.name + " Garrison",
            Trait = BattleGeneralTrait.Defensive, CommandIntervalTicks = 50, MoraleAura = 4, AbilityCooldownTicks = 200 });
        AddDeterministicTerrain(state, province);
        return state;
    }

    public static ReinforcementCommand CreateReinforcement(BattleStartState state, FieldArmyHolder army, int side, int arrivalTick)
    {
        Dictionary<string, int> ids = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < state.Definitions.Count; i++) ids[state.Definitions[i].UnitName] = state.Definitions[i].DefinitionId;
        int definitionCount = state.Definitions.Count;
        int formationStart = 1;
        for (int i = 0; i < state.Formations.Count; i++) formationStart = Mathf.Max(formationStart, state.Formations[i].FormationId + 1);
        BattleStartState temporary = new BattleStartState();
        temporary.Definitions.AddRange(state.Definitions);
        AddArmy(temporary, army.fieldArmy, side, ids, formationStart);
        ReinforcementCommand command = new ReinforcementCommand { Tick = arrivalTick };
        for (int i = definitionCount; i < temporary.Definitions.Count; i++)
        {
            command.Definitions.Add(temporary.Definitions[i]);
            if (state.Definitions.Find(item => item.DefinitionId == temporary.Definitions[i].DefinitionId) == null)
                state.Definitions.Add(temporary.Definitions[i]);
        }
        command.Formations.AddRange(temporary.Formations);
        return command;
    }

    private static BattleGeneralProfile CreateGeneral(FieldArmyHolder army, int side)
    {
        BattleGeneralTrait trait = BattleGeneralTrait.Balanced;
        if (army != null && army.flaglist != null)
        {
            if (army.flaglist.Contains("Aggressive")) trait = BattleGeneralTrait.Aggressive;
            else if (army.flaglist.Contains("Defensive")) trait = BattleGeneralTrait.Defensive;
            else if (army.flaglist.Contains("CavalryCommander")) trait = BattleGeneralTrait.CavalryCommander;
            else if (army.flaglist.Contains("Cautious")) trait = BattleGeneralTrait.Cautious;
            else if (army.flaglist.Contains("Opportunistic")) trait = BattleGeneralTrait.Opportunistic;
        }
        return new BattleGeneralProfile { Side = side, Name = army != null ? army.gameObject.name : "General",
            Trait = trait, CommandIntervalTicks = trait == BattleGeneralTrait.Opportunistic ? 30 : 50,
            MoraleAura = trait == BattleGeneralTrait.Defensive ? 4 : 2,
            AbilityCooldownTicks = trait == BattleGeneralTrait.Aggressive ? 150 : 200 };
    }

    private static void AddArmy(BattleStartState state, FieldArmy army, int side, Dictionary<string, int> definitionIds, int formationIdStart = -1)
    {
        List<ArmyReserves> reserves = new List<ArmyReserves>(army.USDReserves);
        reserves.Sort((a, b) => string.CompareOrdinal(a != null && a.USD != null ? a.USD.name : "", b != null && b.USD != null ? b.USD.name : ""));
        int formationIndex = 0;
        for (int i = 0; i < reserves.Count; i++)
        {
            ArmyReserves reserve = reserves[i];
            if (reserve == null || reserve.USD == null || reserve.amount <= 0) continue;
            if (!definitionIds.TryGetValue(reserve.USD.name, out int definitionId))
            {
                definitionId = definitionIds.Count + 1;
                definitionIds.Add(reserve.USD.name, definitionId);
                state.Definitions.Add(Translate(definitionId, reserve.USD));
            }
            int sideDirection = side == 0 ? 1 : -1;
            SavedFormationDeployment saved = army.battleDeployment != null
                ? army.battleDeployment.Formations.Find(item => item != null && item.UnitName == reserve.USD.name) : null;
            // Each campaign unit is an independently manoeuvrable formation. Previously the
            // complete reserve amount was multiplied into one megaformation per unit type.
            for (int copy = 0; copy < reserve.amount; copy++)
            {
                int row = formationIndex / 4;
                int column = formationIndex % 4;
                Int2 position = saved != null
                    ? new Int2(saved.RelativeX + (copy % 3 - 1) * 1600,
                        sideDirection * saved.RelativeY - sideDirection * (copy / 3) * 1600)
                    : new Int2((column - 2) * 4000, sideDirection * (-12000 - row * 4000));
                state.Formations.Add(new BattleFormationStart
                {
                    FormationId = formationIdStart >= 0 ? formationIdStart + formationIndex : side * 10000 + formationIndex + 1,
                    Side = side, DefinitionId = definitionId, CampaignUnitCount = 1,
                    Position = position,
                    Facing = saved != null
                        ? new Int2(saved.FacingX, sideDirection * saved.FacingY)
                        : new Int2(0, sideDirection * BattleSimulation.PositionScale),
                    Reserve = saved != null && saved.Reserve,
                    InitialOrder = saved != null ? saved.InitialOrder : FormationOrder.Advance
                });
                formationIndex++;
            }
        }
    }

    private enum BattlefieldArchetype : byte
    {
        OpenPlain, DenseForest, ForestClearing, Ridgeline, MountainPass,
        RiverCrossing, Marsh, RoughGround, RoadAmbush, CoastalPlain, MixedCountryside
    }

    private static void AddDeterministicTerrain(BattleStartState state, Province province)
    {
        string provinceName = province != null ? province.name : state.BattlefieldSource;
        ulong terrainSeed = state.Seed ^ StableTextHash(provinceName);
        DeterministicRng rng = new DeterministicRng(terrainSeed);
        CampaignTerrainProfile profile = ResolveTerrainProfile(province);
        BattlefieldArchetype archetype = ChooseArchetype(profile, ref rng);
        state.TerrainArchetype = archetype.ToString();
        int id = 1;

        switch (archetype)
        {
            case BattlefieldArchetype.OpenPlain:
                if (rng.Range(0, 3) == 0) AddTerrain(state, ref id, BattleTerrainKind.Road, new Int2(0, rng.Range(-1500, 1501)), 5000);
                break;
            case BattlefieldArchetype.DenseForest:
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(-7500, -3500), 6000);
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(6500, -2500), 6500);
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(-5000, 5000), 6000);
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(7500, 5000), 6200);
                break;
            case BattlefieldArchetype.ForestClearing:
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(-10500, 0), 7000);
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(10500, 0), 7000);
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(0, 8000), 4000);
                break;
            case BattlefieldArchetype.Ridgeline:
                AddTerrain(state, ref id, BattleTerrainKind.Hill, new Int2(rng.Range(-2500, 2501), 4500), 6500);
                AddTerrain(state, ref id, BattleTerrainKind.Rough, new Int2(rng.Range(-9000, 9001), -4500), 2800);
                break;
            case BattlefieldArchetype.MountainPass:
                AddTerrain(state, ref id, BattleTerrainKind.Impassable, new Int2(-11000, 0), 8500);
                AddTerrain(state, ref id, BattleTerrainKind.Impassable, new Int2(11000, 0), 8500);
                AddTerrain(state, ref id, BattleTerrainKind.Hill, new Int2(0, 5500), 3500);
                break;
            case BattlefieldArchetype.RiverCrossing:
                AddTerrain(state, ref id, BattleTerrainKind.River, new Int2(-6500, 0), 4300);
                AddTerrain(state, ref id, BattleTerrainKind.River, new Int2(0, 0), 4300);
                AddTerrain(state, ref id, BattleTerrainKind.River, new Int2(6500, 0), 4300);
                break;
            case BattlefieldArchetype.Marsh:
                AddTerrain(state, ref id, BattleTerrainKind.Rough, new Int2(-5500, -2500), 6000);
                AddTerrain(state, ref id, BattleTerrainKind.Rough, new Int2(6000, 3000), 6500);
                AddTerrain(state, ref id, BattleTerrainKind.River, new Int2(0, 0), 3800);
                break;
            case BattlefieldArchetype.RoughGround:
                AddTerrain(state, ref id, BattleTerrainKind.Rough, new Int2(-6500, 1000), 5800);
                AddTerrain(state, ref id, BattleTerrainKind.Rough, new Int2(6500, -1500), 6000);
                AddTerrain(state, ref id, BattleTerrainKind.Hill, new Int2(0, 5000), 3300);
                break;
            case BattlefieldArchetype.RoadAmbush:
                AddTerrain(state, ref id, BattleTerrainKind.Road, new Int2(0, 0), 5200);
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(-9000, 0), 6500);
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(9000, 0), 6500);
                break;
            case BattlefieldArchetype.CoastalPlain:
                AddTerrain(state, ref id, BattleTerrainKind.Rough, new Int2(-10000, rng.Range(-3000, 3001)), 5000);
                if (rng.Range(0, 2) == 0) AddTerrain(state, ref id, BattleTerrainKind.Road, new Int2(2500, 0), 4200);
                break;
            default:
                AddTerrain(state, ref id, BattleTerrainKind.Hill, new Int2(-6500, 3500), 3600);
                AddTerrain(state, ref id, BattleTerrainKind.Forest, new Int2(7000, 2500), 3800);
                AddTerrain(state, ref id, BattleTerrainKind.Rough, new Int2(0, -4500), 3200);
                break;
        }
    }

    private static CampaignTerrainProfile ResolveTerrainProfile(Province province)
    {
        if (province != null && province.terrainProfile != CampaignTerrainProfile.Auto) return province.terrainProfile;
        string nation = province != null && province.OriginalNation != null ? province.OriginalNation.name : string.Empty;
        ulong variation = StableTextHash(province != null ? province.name : nation);
        if (nation == "Gaul" || nation == "Germania" || nation == "Franks") return CampaignTerrainProfile.Forested;
        if (nation == "Spain" || nation == "Galicia") return (variation & 1UL) == 0 ? CampaignTerrainProfile.RoughCountry : CampaignTerrainProfile.Hilly;
        if (nation == "Rome") return (variation & 3UL) == 0 ? CampaignTerrainProfile.Plains : CampaignTerrainProfile.Hilly;
        if (nation == "Egypt") return (variation & 3UL) == 0 ? CampaignTerrainProfile.Plains : CampaignTerrainProfile.RiverValley;
        if (nation == "Carthage") return CampaignTerrainProfile.Coastal;
        return (CampaignTerrainProfile)(1 + variation % 8UL);
    }

    private static BattlefieldArchetype ChooseArchetype(CampaignTerrainProfile profile, ref DeterministicRng rng)
    {
        BattlefieldArchetype[] choices;
        switch (profile)
        {
            case CampaignTerrainProfile.Plains: choices = new[] { BattlefieldArchetype.OpenPlain, BattlefieldArchetype.OpenPlain, BattlefieldArchetype.Ridgeline }; break;
            case CampaignTerrainProfile.Forested: choices = new[] { BattlefieldArchetype.DenseForest, BattlefieldArchetype.DenseForest, BattlefieldArchetype.ForestClearing, BattlefieldArchetype.RoadAmbush }; break;
            case CampaignTerrainProfile.Hilly: choices = new[] { BattlefieldArchetype.Ridgeline, BattlefieldArchetype.Ridgeline, BattlefieldArchetype.RoughGround, BattlefieldArchetype.MountainPass }; break;
            case CampaignTerrainProfile.Mountainous: choices = new[] { BattlefieldArchetype.MountainPass, BattlefieldArchetype.MountainPass, BattlefieldArchetype.Ridgeline }; break;
            case CampaignTerrainProfile.Marshland: choices = new[] { BattlefieldArchetype.Marsh, BattlefieldArchetype.Marsh, BattlefieldArchetype.RiverCrossing }; break;
            case CampaignTerrainProfile.RiverValley: choices = new[] { BattlefieldArchetype.RiverCrossing, BattlefieldArchetype.RiverCrossing, BattlefieldArchetype.Marsh, BattlefieldArchetype.OpenPlain }; break;
            case CampaignTerrainProfile.RoughCountry: choices = new[] { BattlefieldArchetype.RoughGround, BattlefieldArchetype.RoughGround, BattlefieldArchetype.Ridgeline, BattlefieldArchetype.ForestClearing }; break;
            case CampaignTerrainProfile.Coastal: choices = new[] { BattlefieldArchetype.CoastalPlain, BattlefieldArchetype.CoastalPlain, BattlefieldArchetype.OpenPlain, BattlefieldArchetype.RiverCrossing }; break;
            default: choices = new[] { BattlefieldArchetype.MixedCountryside }; break;
        }
        return choices[rng.Range(0, choices.Length)];
    }

    private static void AddTerrain(BattleStartState state, ref int id, BattleTerrainKind kind, Int2 center, int radius)
    {
        BattleTerrainArea area = new BattleTerrainArea { Id = id++, Kind = kind, Center = center, RadiusMilli = radius };
        switch (kind)
        {
            case BattleTerrainKind.Hill: area.MovementPermille = 800; area.ChargePermille = 750; area.DefenseBonusPercent = 12; area.RangedAccuracyPermille = 1100; break;
            case BattleTerrainKind.Forest: area.MovementPermille = 650; area.ChargePermille = 350; area.DefenseBonusPercent = 18; area.RangedAccuracyPermille = 700; area.VisibilityPermille = 650; break;
            case BattleTerrainKind.Rough: area.MovementPermille = 700; area.ChargePermille = 450; area.DefenseBonusPercent = 8; area.RangedAccuracyPermille = 900; break;
            case BattleTerrainKind.Road: area.MovementPermille = 1250; area.ChargePermille = 1000; break;
            case BattleTerrainKind.River: area.MovementPermille = 400; area.ChargePermille = 200; area.DefenseBonusPercent = -5; area.VisibilityPermille = 900; break;
            case BattleTerrainKind.Impassable: area.MovementPermille = 0; area.ChargePermille = 0; area.Impassable = true; break;
        }
        state.Terrain.Add(area);
    }

    private static ulong StableTextHash(string value)
    {
        ulong hash = 1469598103934665603UL;
        if (value == null) return hash;
        for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 1099511628211UL; }
        return hash;
    }

    private static BattleUnitDefinition Translate(int id, UnitSaveData unit)
    {
        Weapon melee = unit.MeleeWeapon != null ? unit.MeleeWeapon : unit.RangedWeapon;
        int armor = unit.Armor != null && unit.Armor.armor != null ? unit.Armor.armor.armor : 0;
        // Existing shield assets commonly store their value in rangedarmor while leaving
        // armor at zero. ShieldPercent is used by both melee and projectile resolution, so
        // translate the strongest configured shield value into that shared protection stat.
        int shield = unit.Shield != null && unit.Shield.armor != null
            ? Mathf.Max(unit.Shield.armor.armor, unit.Shield.armor.rangedarmor) : 0;
        return new BattleUnitDefinition
        {
            DefinitionId = id, UnitName = unit.name,
            MembersPerCampaignUnit = Mathf.Max(1, unit.formationSize),
            HealthPerMember = Mathf.Max(1, unit.memberHealth > 0 ? unit.memberHealth : unit.health),
            SpeedMilliPerTick = Mathf.Max(40, unit.speed * 90),
            MeleeDamage = Mathf.Max(1, melee != null ? melee.attack : 1),
            MeleeReachMilli = Mathf.Max(350, Mathf.RoundToInt((float)(melee != null ? melee.combatdistance : 1d) * 1000f)),
            AttackCooldownTicks = Mathf.Max(1, Mathf.RoundToInt((float)(melee != null ? melee.attacktime : 1d) * stateTickRate)),
            ArmorPercent = armor, ShieldPercent = shield
            ,HasRangedWeapon = unit.RangedWeapon != null && unit.RangedWeapon.Throwable != null,
            RangedDamage = Mathf.Max(1, unit.RangedWeapon != null ? unit.RangedWeapon.attack : 1),
            RangedReachMilli = Mathf.Max(1000, Mathf.RoundToInt((float)(unit.RangedWeapon != null ? unit.RangedWeapon.combatdistance : 1d) * 1000f)),
            RangedCooldownTicks = Mathf.Max(1, Mathf.RoundToInt((float)(unit.RangedWeapon != null ? unit.RangedWeapon.attacktime : 1d) * stateTickRate)),
            ProjectileSpeedMilliPerTick = Mathf.Max(250, Mathf.RoundToInt((float)(unit.RangedWeapon != null ? unit.RangedWeapon.speed : 1d) * 500f)),
            AmmunitionPerCombatant = unit.RangedWeapon != null ? Mathf.Max(0, unit.RangedWeapon.ammo) : 0
            ,Role = unit.unittype == UnitTypes.LightCavalry || unit.unittype == UnitTypes.HeavyCavalry
                ? BattleUnitRole.Cavalry
                : unit.unittype == UnitTypes.Ranged ? BattleUnitRole.Ranged : BattleUnitRole.Infantry,
            Mass = unit.unittype == UnitTypes.HeavyCavalry ? 220 : unit.unittype == UnitTypes.LightCavalry ? 150 :
                unit.unittype == UnitTypes.HeavyInfantry ? 120 : 80,
            ChargeDamage = unit.unittype == UnitTypes.HeavyCavalry ? Mathf.Max(8, unit.speed * 8) :
                unit.unittype == UnitTypes.LightCavalry ? Mathf.Max(5, unit.speed * 5) : 0,
            ChargeSpeedMultiplier = unit.unittype == UnitTypes.HeavyCavalry || unit.unittype == UnitTypes.LightCavalry ? 1800 : 1000,
            MinimumChargeDistanceMilli = 3000,
            ChargeCooldownTicks = 100,
            TurnRateMilli = unit.unittype == UnitTypes.HeavyCavalry ? 100 : 180,
            Disciplined = unit.flaglist != null && (unit.flaglist.Contains("Formation") || unit.flaglist.Contains("Phalanx")),
            ForestryImmune = unit.flaglist != null && unit.flaglist.Contains("Forestry_Immunity"),
            Forester = unit.flaglist != null && unit.flaglist.Contains("Forester"),
            FormationTerrainPenalty = unit.flaglist != null && unit.flaglist.Contains("Formation"),
            PreferredFrontage = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, unit.formationSize))) +
                (unit.unittype == UnitTypes.Ranged ? 2 : 0), 2, 12)
        };
    }

    private const int stateTickRate = 10;
    public static string GetArmyId(FieldArmyHolder army) => army == null ? string.Empty :
        !string.IsNullOrEmpty(army.NetworkArmyId) ? army.NetworkArmyId : army.gameObject.name;
}

public class DeterministicBattleManager : MonoBehaviour
{
    public static DeterministicBattleManager Instance { get; private set; }
    public CampaignBattleSystemMode BattleSystemMode = CampaignBattleSystemMode.TileBased;
    [Range(1, 100)] public int SimulationTicksPerCampaignSecond = 30;
    public bool EnableDiagnosticLogging;
    public bool AllowPlayerTacticalOrders = true;
    public readonly List<CampaignActiveBattle> ActiveBattles = new List<CampaignActiveBattle>();
    public event Action<BattleChecksumReport> ChecksumReady;
    public event Action<SavedActiveBattle> NetworkBattleStarted;
    public event Action<string, BattleCommandRecord> NetworkCommandScheduled;
    public event Action<string> NetworkBattleFinished;
    private readonly List<SavedActiveBattle> pendingNetworkBattles = new List<SavedActiveBattle>();
    private float tickAccumulator;
    private int battleSequence;

    private void Awake()
    {
        Instance = this;
        DeterministicBattlePresentation presentation = GetComponent<DeterministicBattlePresentation>();
        if (presentation == null) presentation = gameObject.AddComponent<DeterministicBattlePresentation>();
        presentation.Initialize(this);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public bool TryStartBattle(FieldArmyHolder armyA, FieldArmyHolder armyB)
    {
        if (BattleSystemMode != CampaignBattleSystemMode.Deterministic || armyA == null || armyB == null || armyA == armyB) return false;
        if (armyA.fieldArmy == null || armyB.fieldArmy == null || armyA.fieldArmy.nation == armyB.fieldArmy.nation) return false;
        CampaignActiveBattle existingA = FindBattle(armyA), existingB = FindBattle(armyB);
        if (existingA != null && existingB == null) return TryJoinBattle(existingA, armyB);
        if (existingB != null && existingA == null) return TryJoinBattle(existingB, armyA);
        if (existingA != null || existingB != null) return true;
        string battleId = "battle_" + Owners.Instance.turncounter + "_" + (++battleSequence);
        ulong seed = StableSeed(battleId, CampaignBattleStateAdapter.GetArmyId(armyA), CampaignBattleStateAdapter.GetArmyId(armyB));
        BattleStartState start = CampaignBattleStateAdapter.Create(armyA, armyB, battleId, seed);
        if (start.Formations.Count == 0) return false;
        CampaignActiveBattle battle = new CampaignActiveBattle
        {
            ArmyA = armyA, ArmyB = armyB, StartState = start, Simulation = new BattleSimulation(start)
        };
        for (int i = 0; i < start.Formations.Count; i++)
            (start.Formations[i].Side == 0 ? battle.ArmyAFormationIds : battle.ArmyBFormationIds).Add(start.Formations[i].FormationId);
        ActiveBattles.Add(battle);
        SetEncounter(armyA, true); SetEncounter(armyB, true);
        armyA.target = Vector3.zero; armyB.target = Vector3.zero;
        if (EnableDiagnosticLogging) Debug.Log("Started deterministic " + battleId + " seed=" + seed);
        if (IsAuthority()) NetworkBattleStarted?.Invoke(CaptureBattle(battle));
        return true;
    }

    public bool TryStartGarrisonBattle(FieldArmyHolder attacker, Province province)
    {
        if (BattleSystemMode != CampaignBattleSystemMode.Deterministic || attacker == null || province == null ||
            attacker.fieldArmy == null || province.garrison == null || province.nation == attacker.fieldArmy.nation) return false;
        if (FindBattle(attacker) != null) return true;
        CampaignActiveBattle provinceBattle = ActiveBattles.Find(item => item.DefendedProvince == province);
        if (provinceBattle != null) return TryJoinBattle(provinceBattle, attacker);
        if (province.garrison.GrabArmySize() <= 0) { attacker.ConquerProvince(province); return true; }

        string battleId = "garrison_" + Owners.Instance.turncounter + "_" + (++battleSequence);
        ulong seed = StableSeed(battleId, CampaignBattleStateAdapter.GetArmyId(attacker), province.name);
        BattleStartState start = CampaignBattleStateAdapter.CreateGarrisonBattle(attacker, province, battleId, seed);
        if (start.Formations.Count == 0) return false;
        CampaignActiveBattle battle = new CampaignActiveBattle
        {
            ArmyA = attacker, GarrisonArmy = province.garrison, DefendedProvince = province,
            StartState = start, Simulation = new BattleSimulation(start)
        };
        for (int i = 0; i < start.Formations.Count; i++)
            (start.Formations[i].Side == 0 ? battle.ArmyAFormationIds : battle.ArmyBFormationIds).Add(start.Formations[i].FormationId);
        ActiveBattles.Add(battle); SetEncounter(attacker, true); attacker.target = Vector3.zero;
        if (EnableDiagnosticLogging) Debug.Log("Started deterministic garrison battle " + battleId + " at " + province.name);
        if (IsAuthority()) NetworkBattleStarted?.Invoke(CaptureBattle(battle));
        return true;
    }

    public bool TryJoinBattle(CampaignActiveBattle battle, FieldArmyHolder army)
    {
        if (battle == null || army == null || army.fieldArmy == null || FindBattle(army) != null) return false;
        int side = battle.ArmyA != null && army.fieldArmy.nation == battle.ArmyA.fieldArmy.nation ? 0 :
            battle.ArmyB != null && army.fieldArmy.nation == battle.ArmyB.fieldArmy.nation ? 1 :
            battle.DefendedProvince != null && army.fieldArmy.nation == battle.DefendedProvince.nation ? 1 : -1;
        if (side < 0) return false;
        int arrivalTick = battle.Simulation.Tick + 1;
        ReinforcementCommand command = CampaignBattleStateAdapter.CreateReinforcement(battle.StartState, army, side, arrivalTick);
        if (command.Formations.Count == 0) return false;
        Vector3 battlePosition = battle.ArmyB != null
            ? (battle.ArmyA.transform.position + battle.ArmyB.transform.position) * 0.5f
            : battle.ArmyA.transform.position;
        Vector3 approach = army.transform.position - battlePosition;
        for (int i = 0; i < command.Formations.Count; i++)
        {
            BattleFormationStart formation = command.Formations[i];
            if (Mathf.Abs(approach.x) > Mathf.Abs(approach.y))
            {
                int edge = approach.x < 0 ? -18000 : 18000;
                formation.Position = new Int2(edge, (i - command.Formations.Count / 2) * 1800);
                formation.Facing = new Int2(edge < 0 ? 1000 : -1000, 0);
            }
            else
            {
                int edge = approach.y < 0 ? -18000 : 18000;
                formation.Position = new Int2((i - command.Formations.Count / 2) * 1800, edge);
                formation.Facing = new Int2(0, edge < 0 ? 1000 : -1000);
            }
        }
        battle.Simulation.ScheduleCommand(command);
        CampaignBattleReinforcement reinforcement = new CampaignBattleReinforcement { Army = army, Side = side, ArrivalTick = arrivalTick };
        for (int i = 0; i < command.Formations.Count; i++) reinforcement.FormationIds.Add(command.Formations[i].FormationId);
        battle.ReinforcementArmies.Add(reinforcement);
        SetEncounter(army, true); army.target = Vector3.zero;
        if (EnableDiagnosticLogging) Debug.Log(army.gameObject.name + " reinforces " + battle.StartState.BattleId + " at tick " + arrivalTick);
        return true;
    }

    public void SetBattleSystemMode(CampaignBattleSystemMode mode) { BattleSystemMode = mode; }

    public bool SchedulePlayerOrder(FieldArmyHolder army, int formationId, FormationOrder order, int delayTicks = 2)
    {
        if (!AllowPlayerTacticalOrders || army == null ||
            (!army.IsPlayer && !army.IsHumanControlled)) return false;
        if (!IsAuthority())
        {
            if (CampaignNetworkPlayer.Local == null) return false;
            CampaignNetworkPlayer.Local.RequestBattleOrder(CampaignBattleStateAdapter.GetArmyId(army), formationId, order, delayTicks);
            return true;
        }
        CampaignActiveBattle battle = FindBattle(army);
        if (battle == null) return false;
        int side = army == battle.ArmyA ? 0 : army == battle.ArmyB ? 1 :
            battle.ReinforcementArmies.Find(item => item.Army == army)?.Side ?? -1;
        SimFormation formation = battle.Simulation.Formations.Find(item => item.Id == formationId && item.Side == side);
        if (formation == null) return false;
        FormationOrderCommand command = new FormationOrderCommand { Tick = battle.Simulation.Tick + Mathf.Max(1, delayTicks),
            FormationId = formationId, Order = order, LockDurationTicks = 100 };
        battle.Simulation.ScheduleCommand(command);
        NetworkCommandScheduled?.Invoke(battle.StartState.BattleId, new BattleCommandRecord { Tick = command.Tick,
            FormationId = command.FormationId, Order = command.Order, LockDurationTicks = command.LockDurationTicks });
        return true;
    }

    private void Update()
    {
        RetryPendingNetworkBattles();
        if (BattleSystemMode != CampaignBattleSystemMode.Deterministic || ActiveBattles.Count == 0) return;
        float campaignSpeed = Owners.Instance != null
            ? (Owners.Instance.CampaignPaused ? 0f : Owners.Instance.CampaignSimulationSpeed)
            : 1f;
        if (campaignSpeed <= 0f) return;
        tickAccumulator += Time.unscaledDeltaTime * SimulationTicksPerCampaignSecond * campaignSpeed;
        int ticks = Mathf.Min(100, Mathf.FloorToInt(tickAccumulator));
        if (ticks <= 0) return;
        tickAccumulator -= ticks;
        for (int i = ActiveBattles.Count - 1; i >= 0; i--)
        {
            CampaignActiveBattle battle = ActiveBattles[i];
            battle.Simulation.AdvanceTicks(ticks);
            int reportTick = battle.Simulation.Tick / 100 * 100;
            if (reportTick > battle.LastChecksumReportTick)
            {
                battle.LastChecksumReportTick = reportTick;
                ChecksumReady?.Invoke(new BattleChecksumReport { BattleId = battle.StartState.BattleId,
                    Tick = battle.Simulation.Tick, Hash = battle.Simulation.ComputeHash() });
            }
            if (battle.Simulation.Status == BattleStatus.Finished && IsAuthority()) FinishBattle(i, battle);
        }
    }

    private void FinishBattle(int index, CampaignActiveBattle battle)
    {
        ApplyArmyResult(battle, battle.ArmyA, battle.ArmyAFormationIds);
        if (battle.ArmyB != null) ApplyArmyResult(battle, battle.ArmyB, battle.ArmyBFormationIds);
        else ApplyFieldArmyResult(battle, battle.GarrisonArmy, battle.ArmyBFormationIds);
        for (int i = 0; i < battle.ReinforcementArmies.Count; i++)
            ApplyArmyResult(battle, battle.ReinforcementArmies[i].Army, battle.ReinforcementArmies[i].FormationIds);
        SetEncounter(battle.ArmyA, false); SetEncounter(battle.ArmyB, false);
        for (int i = 0; i < battle.ReinforcementArmies.Count; i++) SetEncounter(battle.ReinforcementArmies[i].Army, false);
        FieldArmyHolder loser = battle.DefendedProvince == null
            ? battle.Simulation.WinningSide == 0 ? battle.ArmyB : battle.Simulation.WinningSide == 1 ? battle.ArmyA : null
            : null;
        if (EnableDiagnosticLogging) Debug.Log("Finished " + battle.StartState.BattleId + " tick=" + battle.Simulation.Tick + " hash=" + battle.Simulation.ComputeHash());
        ActiveBattles.RemoveAt(index);
        NetworkBattleFinished?.Invoke(battle.StartState.BattleId);
        if (loser != null) ResolveDefeatedArmy(loser);
        if (battle.DefendedProvince != null)
        {
            if (battle.Simulation.WinningSide == 0 && battle.ArmyA != null)
                battle.ArmyA.ConquerProvince(battle.DefendedProvince);
            else if (battle.Simulation.WinningSide != 0 && battle.ArmyA != null)
                ResolveDefeatedArmy(battle.ArmyA);
        }
        for (int i = 0; i < battle.ReinforcementArmies.Count; i++)
            if (battle.ReinforcementArmies[i].Side != battle.Simulation.WinningSide)
                ResolveDefeatedArmy(battle.ReinforcementArmies[i].Army);
    }

    private static void ApplyArmyResult(CampaignActiveBattle battle, FieldArmyHolder army, List<int> formationIds)
    {
        if (army == null) return;
        ApplyFieldArmyResult(battle, army.fieldArmy, formationIds);
    }

    private static void ApplyFieldArmyResult(CampaignActiveBattle battle, FieldArmy army, List<int> formationIds)
    {
        if (army == null) return;
        for (int i = 0; i < army.USDReserves.Count; i++)
        {
            ArmyReserves reserve = army.USDReserves[i];
            if (reserve == null || reserve.USD == null) continue;
            BattleUnitDefinition definition = battle.StartState.Definitions.Find(item => item.UnitName == reserve.USD.name);
            if (definition == null) { reserve.amount = 0; continue; }
            int livingMembers = 0;
            for (int f = 0; f < battle.Simulation.Formations.Count; f++)
            {
                SimFormation formation = battle.Simulation.Formations[f];
                if (!formationIds.Contains(formation.Id) || formation.DefinitionId != definition.DefinitionId) continue;
                for (int c = 0; c < formation.CombatantIds.Count; c++)
                    if (battle.Simulation.Combatants[formation.CombatantIds[c]].Alive) livingMembers++;
            }
            reserve.amount = (livingMembers + definition.MembersPerCampaignUnit - 1) / definition.MembersPerCampaignUnit;
        }
    }

    private void ResolveDefeatedArmy(FieldArmyHolder army)
    {
        if (army == null || army.fieldArmy == null) return;
        if (army.fieldArmy.GrabArmySize() <= 0) { Destroy(army.gameObject); return; }
        Province current = army.GrabNearestProvince();
        List<Province> destinations = current != null ? current.GrabAdjacents().FindAll(item =>
            item != null && item.nation == army.fieldArmy.nation) : new List<Province>();
        destinations.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (destinations.Count > 0)
        {
            army.SetPositionTo(destinations[0]);
            army.target = Vector3.zero;
            army.flaglist.Remove("Surrounded");
            if (!army.flaglist.Contains("Retreated")) army.flaglist.Add("Retreated");
            return;
        }
        // A surrounded army has no valid retreat corridor and loses additional captives.
        for (int i = 0; i < army.fieldArmy.USDReserves.Count; i++)
            army.fieldArmy.USDReserves[i].amount /= 2;
        army.flaglist.Remove("Retreated");
        if (!army.flaglist.Contains("Surrounded")) army.flaglist.Add("Surrounded");
        if (army.fieldArmy.GrabArmySize() <= 0) Destroy(army.gameObject);
    }

    public CampaignActiveBattle FindBattle(FieldArmyHolder army) => ActiveBattles.Find(item => item.ArmyA == army || item.ArmyB == army ||
        item.ReinforcementArmies.Exists(reinforcement => reinforcement.Army == army));
    public List<ActiveBattleSummary> GetSummaries()
    {
        List<ActiveBattleSummary> result = new List<ActiveBattleSummary>(ActiveBattles.Count);
        for (int i = 0; i < ActiveBattles.Count; i++) result.Add(ActiveBattles[i].GetSummary());
        return result;
    }

    public bool RunDeterminismProbe(BattleStartState state, int repetitions, int maximumTicks, out ulong finalHash)
    {
        repetitions = Mathf.Max(2, repetitions);
        BattleSimulation baseline = new BattleSimulation(state);
        baseline.AdvanceTicks(maximumTicks);
        finalHash = baseline.ComputeHash();
        for (int i = 1; i < repetitions; i++)
        {
            BattleSimulation repeat = new BattleSimulation(state);
            int remaining = maximumTicks;
            while (remaining > 0 && repeat.Status != BattleStatus.Finished)
            {
                int chunk = Mathf.Min(remaining, 1 + i % 17);
                repeat.AdvanceTicks(chunk);
                remaining -= chunk;
            }
            if (repeat.ComputeHash() != finalHash) return false;
        }
        return true;
    }

    public List<SavedActiveBattle> CaptureActiveBattles()
    {
        List<SavedActiveBattle> result = new List<SavedActiveBattle>();
        for (int i = 0; i < ActiveBattles.Count; i++)
        {
            CampaignActiveBattle battle = ActiveBattles[i];
            result.Add(new SavedActiveBattle { StartState = battle.StartState, Tick = battle.Simulation.Tick,
                ArmyAId = CampaignBattleStateAdapter.GetArmyId(battle.ArmyA), ArmyBId = CampaignBattleStateAdapter.GetArmyId(battle.ArmyB),
                DefendedProvinceName = battle.DefendedProvince != null ? battle.DefendedProvince.name : string.Empty });
            result[result.Count - 1].Commands.AddRange(battle.Simulation.CommandHistory);
            for (int r = 0; r < battle.ReinforcementArmies.Count; r++)
                result[result.Count - 1].Reinforcements.Add(new SavedBattleReinforcement {
                    ArmyId = CampaignBattleStateAdapter.GetArmyId(battle.ReinforcementArmies[r].Army),
                    Side = battle.ReinforcementArmies[r].Side, ArrivalTick = battle.ReinforcementArmies[r].ArrivalTick });
        }
        return result;
    }

    private static SavedActiveBattle CaptureBattle(CampaignActiveBattle battle)
    {
        SavedActiveBattle result = new SavedActiveBattle { StartState = battle.StartState, Tick = battle.Simulation.Tick,
            ArmyAId = CampaignBattleStateAdapter.GetArmyId(battle.ArmyA), ArmyBId = CampaignBattleStateAdapter.GetArmyId(battle.ArmyB),
            DefendedProvinceName = battle.DefendedProvince != null ? battle.DefendedProvince.name : string.Empty };
        result.Commands.AddRange(battle.Simulation.CommandHistory); return result;
    }

    public void ReceiveNetworkBattle(SavedActiveBattle state)
    {
        if (state == null || state.StartState == null || IsAuthority()) return;
        if (ActiveBattles.Exists(item => item.StartState.BattleId == state.StartState.BattleId)) return;
        pendingNetworkBattles.Add(state); RetryPendingNetworkBattles();
    }

    public void ReceiveNetworkCommand(string battleId, BattleCommandRecord record)
    {
        CampaignActiveBattle battle = ActiveBattles.Find(item => item.StartState.BattleId == battleId);
        if (battle == null || record == null || record.Tick <= battle.Simulation.Tick) return;
        if (record.IsAbility) battle.Simulation.ScheduleCommand(new BattleAbilityCommand { Tick = record.Tick, Side = record.Side,
            FormationId = record.FormationId, Ability = record.Ability });
        else battle.Simulation.ScheduleCommand(new FormationOrderCommand { Tick = record.Tick, FormationId = record.FormationId,
            Order = record.Order, LockDurationTicks = record.LockDurationTicks });
    }

    public void ReceiveNetworkBattleFinished(string battleId)
    {
        if (IsAuthority()) return;
        CampaignActiveBattle battle = ActiveBattles.Find(item => item.StartState.BattleId == battleId);
        if (battle == null) return;
        SetEncounter(battle.ArmyA, false); SetEncounter(battle.ArmyB, false); ActiveBattles.Remove(battle);
    }

    public bool ReconcileNetworkChecksum(string battleId, int authoritativeTick, ulong authoritativeHash, out ulong localHash)
    {
        localHash = 0;
        CampaignActiveBattle battle = ActiveBattles.Find(item => item.StartState.BattleId == battleId);
        if (battle == null) return false;
        if (!IsAuthority() && battle.Simulation.Tick < authoritativeTick)
            battle.Simulation.AdvanceTicks(authoritativeTick - battle.Simulation.Tick);
        if (battle.Simulation.Tick != authoritativeTick) return false;
        localHash = battle.Simulation.ComputeHash(); return localHash == authoritativeHash;
    }

    private void RetryPendingNetworkBattles()
    {
        if (IsAuthority() || pendingNetworkBattles.Count == 0 || Owners.Instance == null) return;
        for (int i = pendingNetworkBattles.Count - 1; i >= 0; i--)
        {
            SavedActiveBattle state = pendingNetworkBattles[i]; int before = ActiveBattles.Count;
            RestoreActiveBattles(new List<SavedActiveBattle> { state });
            if (ActiveBattles.Count > before) pendingNetworkBattles.RemoveAt(i);
        }
    }

    public void RestoreActiveBattles(List<SavedActiveBattle> saved)
    {
        if (saved == null || Owners.Instance == null) return;
        for (int i = 0; i < saved.Count; i++)
        {
            SavedActiveBattle state = saved[i];
            if (state == null || state.StartState == null || ActiveBattles.Exists(item => item.StartState.BattleId == state.StartState.BattleId)) continue;
            FieldArmyHolder armyA = Owners.Instance.armylist.Find(item => CampaignBattleStateAdapter.GetArmyId(item) == state.ArmyAId);
            FieldArmyHolder armyB = Owners.Instance.armylist.Find(item => CampaignBattleStateAdapter.GetArmyId(item) == state.ArmyBId);
            Province defendedProvince = !string.IsNullOrEmpty(state.DefendedProvinceName)
                ? Owners.Instance.provincelist.Find(item => item.name == state.DefendedProvinceName) : null;
            if (armyA == null || armyB == null && defendedProvince == null) continue;
            BattleSimulation simulation = new BattleSimulation(state.StartState);
            List<CampaignBattleReinforcement> restoredReinforcements = new List<CampaignBattleReinforcement>();
            if (state.Reinforcements != null) for (int r = 0; r < state.Reinforcements.Count; r++)
            {
                SavedBattleReinforcement savedReinforcement = state.Reinforcements[r];
                FieldArmyHolder reinforcementArmy = Owners.Instance.armylist.Find(item => CampaignBattleStateAdapter.GetArmyId(item) == savedReinforcement.ArmyId);
                if (reinforcementArmy == null) continue;
                ReinforcementCommand reinforcement = CampaignBattleStateAdapter.CreateReinforcement(state.StartState,
                    reinforcementArmy, savedReinforcement.Side, savedReinforcement.ArrivalTick);
                simulation.ScheduleCommand(reinforcement);
                CampaignBattleReinforcement campaignReinforcement = new CampaignBattleReinforcement { Army = reinforcementArmy,
                    Side = savedReinforcement.Side, ArrivalTick = savedReinforcement.ArrivalTick };
                for (int f = 0; f < reinforcement.Formations.Count; f++) campaignReinforcement.FormationIds.Add(reinforcement.Formations[f].FormationId);
                restoredReinforcements.Add(campaignReinforcement);
            }
            if (state.Commands != null) for (int c = 0; c < state.Commands.Count; c++)
            {
                BattleCommandRecord record = state.Commands[c];
                if (record.IsAbility) simulation.ScheduleCommand(new BattleAbilityCommand { Tick = record.Tick, Side = record.Side,
                    FormationId = record.FormationId, Ability = record.Ability });
                else simulation.ScheduleCommand(new FormationOrderCommand { Tick = record.Tick, FormationId = record.FormationId,
                    Order = record.Order, LockDurationTicks = record.LockDurationTicks });
            }
            simulation.AdvanceTicks(state.Tick);
            CampaignActiveBattle battle = new CampaignActiveBattle { ArmyA = armyA, ArmyB = armyB,
                GarrisonArmy = defendedProvince != null ? defendedProvince.garrison : null, DefendedProvince = defendedProvince,
                StartState = state.StartState, Simulation = simulation };
            battle.ReinforcementArmies.AddRange(restoredReinforcements);
            for (int f = 0; f < state.StartState.Formations.Count; f++)
                (state.StartState.Formations[f].Side == 0 ? battle.ArmyAFormationIds : battle.ArmyBFormationIds).Add(state.StartState.Formations[f].FormationId);
            ActiveBattles.Add(battle); SetEncounter(armyA, true); SetEncounter(armyB, true);
        }
    }

    private static void SetEncounter(FieldArmyHolder army, bool active)
    {
        if (army == null) return;
        if (active) { if (!army.flaglist.Contains("Battle")) army.flaglist.Add("Battle"); }
        else army.flaglist.Remove("Battle");
    }
    private static bool IsAuthority() => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    private static ulong StableSeed(params string[] values)
    {
        ulong hash = 1469598103934665603UL;
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i] ?? string.Empty;
            for (int c = 0; c < value.Length; c++) { hash ^= value[c]; hash *= 1099511628211UL; }
        }
        return hash;
    }
}
