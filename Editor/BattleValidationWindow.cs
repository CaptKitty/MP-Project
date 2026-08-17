#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectX.DeterministicBattle;
using ProjectX.BattleValidation;

public sealed class BattleValidationWindow : EditorWindow
{
    private int runs = 25;
    private ulong firstSeed = 1000;
    private Vector2 scroll;
    private List<ScenarioBatchResult> results = new List<ScenarioBatchResult>();

    [MenuItem("ProjectX/Deterministic Battle Validator")]
    private static void Open() => GetWindow<BattleValidationWindow>("Battle Validator");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Headless AI and Scenario Validation", EditorStyles.boldLabel);
        runs = EditorGUILayout.IntSlider("Runs per scenario", runs, 1, 500);
        string seedText = EditorGUILayout.TextField("First seed", firstSeed.ToString());
        if (ulong.TryParse(seedText, out ulong parsed)) firstSeed = parsed;
        if (GUILayout.Button("Run all named scenarios")) RunAll();
        using (new EditorGUI.DisabledScope(results.Count == 0))
            if (GUILayout.Button("Export CSV")) Export();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < results.Count; i++)
        {
            ScenarioBatchResult r = results[i];
            EditorGUILayout.Space(); EditorGUILayout.LabelField(r.Scenario, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Runs " + r.Runs + " | A wins " + r.SideAWinPercent + "% | draws " + r.DrawPercent + "% | avg ticks " + r.AverageTicks);
            EditorGUILayout.LabelField("Determinism failures " + r.DeterminismFailures + " | invariant failures " + r.InvariantFailures + " | behavioral failures " + r.BehavioralFailures);
            for (int a = 0; a < r.Anomalies.Count; a++) EditorGUILayout.HelpBox(r.Anomalies[a], MessageType.Warning);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunAll()
    {
        results.Clear(); List<BattleScenario> scenarios = NamedBattleScenarios.CreateAll();
        for (int i = 0; i < scenarios.Count; i++) results.Add(BattleScenarioRunner.RunBatch(scenarios[i], firstSeed, runs));
        Repaint();
    }

    private void Export()
    {
        string path = EditorUtility.SaveFilePanel("Export battle validation", "", "battle-validation.csv", "csv");
        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, BattleScenarioRunner.ToCsv(results));
    }
}

public static class NamedBattleScenarios
{
    public static List<BattleScenario> CreateAll() => new List<BattleScenario>
    {
        EqualInfantry(), CavalryFlank(), RangedForest(), RiverCrossing(), ReserveCounterattack(), SurroundedRetreat(), ReinforcementArrival(), SaveReload()
    };

    public static BattleScenario EqualInfantry()
    {
        BattleScenario s = Base("Equal Infantry Lines");
        s.Expected = new ScenarioExpectations { MustFinish = true, MinimumCasualties = 1,
            MinimumSideAWinPercent = 20, MaximumSideAWinPercent = 80, MaximumDrawPercent = 30 };
        return s;
    }

    public static BattleScenario CavalryFlank()
    {
        BattleScenario s = Base("Cavalry Flank");
        BattleUnitDefinition cavalry = new BattleUnitDefinition { DefinitionId = 2, UnitName = "Cavalry", MembersPerCampaignUnit = 4,
            HealthPerMember = 50, SpeedMilliPerTick = 230, MeleeDamage = 12, MeleeReachMilli = 900, AttackCooldownTicks = 8,
            ArmorPercent = 10, ShieldPercent = 5, Role = BattleUnitRole.Cavalry, Mass = 190, ChargeDamage = 20,
            ChargeSpeedMultiplier = 1800, MinimumChargeDistanceMilli = 2000, ChargeCooldownTicks = 80, TurnRateMilli = 180, PreferredFrontage = 4 };
        s.StartState.Definitions.Add(cavalry); s.StartState.Formations[0].DefinitionId = 2;
        s.StartState.Formations[0].CampaignUnitCount = 2;
        s.StartState.Formations[0].Position = new Int2(-5000, -6000);
        s.StartState.Formations.Add(new BattleFormationStart { FormationId = 2, Side = 0, DefinitionId = 1,
            CampaignUnitCount = 1, Position = new Int2(0, -5000), Facing = new Int2(0, 1000), InitialOrder = FormationOrder.Advance });
        s.Expected = new ScenarioExpectations { MustFinish = true, RequireCharge = true, RequireFlankOrRear = true,
            MaximumDurationTicks = 5000, MinimumSideAWinPercent = 55, MaximumSideAWinPercent = 75, MaximumDrawPercent = 10 };
        return s;
    }

    public static BattleScenario RangedForest()
    {
        BattleScenario s = Base("Ranged Units in Forest"); BattleUnitDefinition ranged = s.StartState.Definitions[0];
        ranged.Role = BattleUnitRole.Ranged; ranged.HasRangedWeapon = true; ranged.RangedDamage = 9; ranged.RangedReachMilli = 12000;
        ranged.RangedCooldownTicks = 10; ranged.ProjectileSpeedMilliPerTick = 700; ranged.AmmunitionPerCombatant = 5;
        s.StartState.Terrain.Add(new BattleTerrainArea { Id = 1, Kind = BattleTerrainKind.Forest, Center = new Int2(0, -5000),
            RadiusMilli = 3500, MovementPermille = 650, ChargePermille = 350, RangedAccuracyPermille = 700, VisibilityPermille = 650, DefenseBonusPercent = 18 });
        s.Expected = new ScenarioExpectations { MustFinish = true, RequireProjectiles = true };
        return s;
    }

    public static BattleScenario RiverCrossing()
    {
        BattleScenario s = Base("River Crossing");
        s.StartState.Terrain.Add(new BattleTerrainArea { Id = 2, Kind = BattleTerrainKind.River, Center = new Int2(0, 0),
            RadiusMilli = 2200, MovementPermille = 400, ChargePermille = 200, RangedAccuracyPermille = 1000, VisibilityPermille = 900, DefenseBonusPercent = -5 });
        s.Expected = new ScenarioExpectations { MustFinish = true, MinimumCasualties = 1, MaximumDurationTicks = 7000 };
        return s;
    }

    public static BattleScenario ReserveCounterattack()
    {
        BattleScenario s = Base("Reserve Counterattack");
        s.StartState.Formations.Add(new BattleFormationStart { FormationId = 2, Side = 0, DefinitionId = 1, CampaignUnitCount = 2,
            Position = new Int2(4000, -9000), Facing = new Int2(0, 1000), Reserve = true });
        s.Expected = new ScenarioExpectations { MustFinish = true, RequireReserveRelease = true };
        return s;
    }

    public static BattleScenario ReinforcementArrival()
    {
        BattleScenario s = Base("Reinforcement Arrival");
        ScenarioReinforcement reinforcement = new ScenarioReinforcement { Tick = 75 };
        reinforcement.Formations.Add(new BattleFormationStart { FormationId = 2, Side = 0, DefinitionId = 1, CampaignUnitCount = 2,
            Position = new Int2(-18000, 0), Facing = new Int2(1000, 0) }); s.Reinforcements.Add(reinforcement);
        s.Expected = new ScenarioExpectations { MustFinish = true, MinimumCasualties = 1 };
        return s;
    }

    public static BattleScenario SurroundedRetreat()
    {
        BattleScenario s = Base("Surrounded Retreat");
        s.Commands.Add(new BattleCommandRecord { Tick = 25, FormationId = 1, Order = FormationOrder.Withdraw, LockDurationTicks = 1000 });
        s.Expected = new ScenarioExpectations { MustFinish = true, RequireRouting = true, MaximumDurationTicks = 5000 };
        return s;
    }

    public static BattleScenario SaveReload()
    {
        BattleScenario s = Base("Save Reload During Battle"); s.SaveReloadTick = 100;
        s.Commands.Add(new BattleCommandRecord { Tick = 50, FormationId = 1, Order = FormationOrder.Hold, LockDurationTicks = 100 });
        s.Expected = new ScenarioExpectations { MustFinish = true, MinimumCasualties = 1 };
        return s;
    }

    private static BattleScenario Base(string name)
    {
        BattleStartState state = new BattleStartState { BattleId = name, Seed = 1, TickRate = 10 };
        state.Definitions.Add(new BattleUnitDefinition { DefinitionId = 1, UnitName = "Infantry", MembersPerCampaignUnit = 4,
            HealthPerMember = 50, SpeedMilliPerTick = 100, MeleeDamage = 12, MeleeReachMilli = 900,
            AttackCooldownTicks = 8, ArmorPercent = 10, ShieldPercent = 10, Mass = 90, TurnRateMilli = 180, PreferredFrontage = 4 });
        state.Formations.Add(new BattleFormationStart { FormationId = 1, Side = 0, DefinitionId = 1, CampaignUnitCount = 3,
            Position = new Int2(0, -5000), Facing = new Int2(0, 1000) });
        state.Formations.Add(new BattleFormationStart { FormationId = 10001, Side = 1, DefinitionId = 1, CampaignUnitCount = 3,
            Position = new Int2(0, 5000), Facing = new Int2(0, -1000) });
        state.Generals.Add(new BattleGeneralProfile { Side = 0, Name = "General A", Trait = BattleGeneralTrait.Defensive,
            CommandIntervalTicks = 40, AbilityCooldownTicks = 150, MoraleAura = 2 });
        state.Generals.Add(new BattleGeneralProfile { Side = 1, Name = "General B", Trait = BattleGeneralTrait.Defensive,
            CommandIntervalTicks = 40, AbilityCooldownTicks = 150, MoraleAura = 2 });
        return new BattleScenario { Name = name, Description = name, StartState = state, MaximumTicks = 7000, SeedRuns = 20 };
    }
}
#endif
