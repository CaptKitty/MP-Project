using NUnit.Framework;
using ProjectX.TileBattle;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class TileBattleVerticalSliceTests
{
    [Test]
    public void ApplicableUnitVisualizerLoadsLegacyAnimatorController()
    {
        UnitSaveData velite = Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/Velite");
        Assert.That(velite, Is.Not.Null);
        Assert.That(velite.RangedWeapon, Is.Not.Null);
        Assert.That(velite.RangedWeapon.animationClass, Is.Not.Null);
        Assert.That(velite.RangedWeapon.BattleAnimationType, Is.EqualTo("Javelin"));
        Assert.That(velite.RangedWeapon.OverrideBattleVisualPose, Is.True);
        Assert.That(velite.RangedWeapon.BattleVisualAngle, Is.EqualTo(-61.051f).Within(.01f));
        Assert.That(velite.RangedWeapon.BattleProjectileSprite, Is.Not.Null);
        GameObject visualObject = new GameObject("Test Unit Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        try
        {
            LayeredBattleUnitVisual visual = visualObject.AddComponent<LayeredBattleUnitVisual>();
            visual.Configure(velite, null);
            Assert.That(visual.UsesLegacyAnimator, Is.True);
            Assert.That(visualObject.GetComponentsInChildren<Image>(true).Count(image => image.gameObject.activeSelf),
                Is.LessThanOrEqualTo(3), "Equipped weapon must replace art slot three rather than add a duplicate layer");
            visual.SetHorizontalFacing(true);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(visualObject.transform.localEulerAngles.y, 180f)), Is.LessThan(.1f));
        }
        finally { Object.DestroyImmediate(visualObject); }
    }

    [Test]
    public void CampaignAdapterUsesUnitSaveDataActionTiming()
    {
        UnitSaveData source = ScriptableObject.CreateInstance<UnitSaveData>();
        try
        {
            source.name = "Responsive Formation"; source.health = 100; source.actions = 4; source.Initiative = 3;
            TileBattleUnitDefinition definition = TileBattleCampaignAdapter.CreateDefinition(source);
            Assert.That(definition.Actions, Is.EqualTo(4));
            Assert.That(definition.Initiative, Is.EqualTo(3));
        }
        finally { Object.DestroyImmediate(source); }
    }

    [Test]
    public void ReplayHistoryCapturesEachPopulatedResolutionTick()
    {
        TileBattleUnitDefinition mover = Definition("Mover", 4, 2, 100);
        TileBattleUnitDefinition enemy = Definition("Enemy", 7, 2, 100);
        TileBattleSimulation simulation = Simulation(mover, enemy, new TileCoord(5, 5), new TileCoord(15, 5));

        simulation.ResolveOrders(Orders(0, 1,
            TileUnitAction.Move(new TileCoord(6, 5)), TileUnitAction.Move(new TileCoord(7, 5))), new TileOrderSet { Side = 1 });

        TileBattleRoundSnapshot tick4 = simulation.History.Single(frame => frame.CommandRound == 1 && frame.ResolutionTick == 4);
        TileBattleRoundSnapshot tick8 = simulation.History.Single(frame => frame.CommandRound == 1 && frame.ResolutionTick == 8);
        Assert.That(tick4.Units.Single(unit => unit.Id == 1).Position, Is.EqualTo(new TileCoord(6, 5)));
        Assert.That(tick8.Units.Single(unit => unit.Id == 1).Position, Is.EqualTo(new TileCoord(7, 5)));
    }

    [Test]
    public void ArrivingArmyDeploysAllFormationsOnItsDeploymentRound()
    {
        TileBattleUnitDefinition infantry = Definition("Reinforcing infantry", 7, 2, 120);
        TileBattleSimulation simulation = new TileBattleSimulation(new TileBattleRules(), null, null);
        simulation.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = infantry,
            Position = new TileCoord(5, 5), Facing = TileFacing.East, Deployed = true });
        simulation.AddUnit(new TileBattleUnit { Id = 10001, Side = 1, Definition = infantry,
            Position = new TileCoord(15, 5), Facing = TileFacing.West, Deployed = true });
        simulation.AddUnit(new TileBattleUnit { Id = 2, Side = 0, Definition = infantry,
            Position = new TileCoord(2, 7), Facing = TileFacing.East, DeploymentRound = 2, Deployed = false });
        simulation.AddUnit(new TileBattleUnit { Id = 3, Side = 0, Definition = infantry,
            Position = new TileCoord(2, 8), Facing = TileFacing.East, DeploymentRound = 2, Deployed = false });

        simulation.RunCommandRound();
        Assert.That(simulation.Units.Single(unit => unit.Id == 2).Deployed, Is.False);
        Assert.That(simulation.Units.Single(unit => unit.Id == 3).Deployed, Is.False);
        simulation.RunCommandRound();
        Assert.That(simulation.Units.Single(unit => unit.Id == 2).Deployed, Is.True);
        Assert.That(simulation.Units.Single(unit => unit.Id == 3).Deployed, Is.True);
    }

    private static TileBattleUnitDefinition Definition(string name, int initiative, int actions, int mass,
        TileWeaponControl control = TileWeaponControl.Sword, bool cavalry = false, bool ranged = false)
    {
        return new TileBattleUnitDefinition { Id = name, DisplayName = name, Initiative = initiative, Actions = actions,
            BaseMass = mass, Strength = 100, MeleeDamage = 20, FrontThreat = control == TileWeaponControl.Pike ? 3 :
                control == TileWeaponControl.Spear ? 2 : 1, WeaponControl = control, Cavalry = cavalry,
            Ranged = ranged, RangedRange = ranged ? 3 : 0, RangedDamage = ranged ? 12 : 0,
            Ammunition = ranged ? 10 : 0 };
    }

    private static TileBattleSimulation Simulation(TileBattleUnitDefinition left, TileBattleUnitDefinition right,
        TileCoord leftPosition, TileCoord rightPosition)
    {
        TileBattleSimulation simulation = new TileBattleSimulation(new TileBattleRules(), null, null);
        simulation.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = left, Position = leftPosition, Facing = TileFacing.East });
        simulation.AddUnit(new TileBattleUnit { Id = 2, Side = 1, Definition = right, Position = rightPosition, Facing = TileFacing.West });
        return simulation;
    }

    private static TileOrderSet Orders(int side, int unitId, params TileUnitAction[] actions)
    {
        TileOrderSet set = new TileOrderSet { Side = side };
        TileUnitOrder order = new TileUnitOrder { UnitId = unitId, Purpose = "Test" };
        order.Actions.AddRange(actions); set.Orders.Add(order); return set;
    }

    [Test]
    public void NumidianActsSeveralTimesBeforeSlowLegionaryCompletesSecondAction()
    {
        TileBattleSimulation sim = Simulation(Definition("Numidian", 4, 4, 90, cavalry: true, ranged: true),
            Definition("Legionary", 7, 2, 150), new TileCoord(3, 10), new TileCoord(10, 10));
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Move(new TileCoord(4, 10)), TileUnitAction.Move(new TileCoord(5, 10)),
            TileUnitAction.Attack(new TileCoord(8, 10)), TileUnitAction.Move(new TileCoord(4, 10))),
            Orders(1, 2, TileUnitAction.Move(new TileCoord(9, 10)), TileUnitAction.Attack(new TileCoord(8, 10))));
        Assert.That(sim.Events.Count(item => item.Type == TileBattleEventType.ActionStarted && item.UnitId == 1 && item.Tick < 14), Is.EqualTo(3));
        Assert.That(sim.Events.Count(item => item.Type == TileBattleEventType.ActionStarted && item.UnitId == 2 && item.Tick < 14), Is.EqualTo(1));
    }

    [Test]
    public void CavalryCrossingSwordThreatTakesLessDamageThanSpearThreat()
    {
        int swordLoss = ThreatCrossingLoss(TileWeaponControl.Sword);
        int spearLoss = ThreatCrossingLoss(TileWeaponControl.Spear);
        Assert.That(spearLoss, Is.GreaterThan(swordLoss));
    }

    [Test]
    public void CavalryDisengageThenTurnConsumesItsMovementAction()
    {
        TileBattleSimulation sim = Simulation(Definition("Cavalry", 4, 4, 90, cavalry: true), Definition("Sword", 7, 2, 100),
            new TileCoord(5, 5), new TileCoord(6, 5));
        sim.Units[0].State = TileUnitState.Engaged; sim.Units[1].State = TileUnitState.Engaged;
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Disengage(), TileUnitAction.Move(new TileCoord(4, 5))), Orders(1, 2, TileUnitAction.Brace()));
        Assert.That(sim.Units[0].Position, Is.EqualTo(new TileCoord(5, 5)));
        Assert.That(sim.Units[0].Facing, Is.EqualTo(TileFacing.West));
        Assert.That(sim.Events.Any(item => item.Type == TileBattleEventType.UnitDisengaged), Is.True);
    }

    [Test]
    public void HeavyInfantryPushesLightInfantry()
    {
        TileBattleSimulation sim = Simulation(Definition("Heavy", 7, 2, 180), Definition("Light", 6, 2, 80),
            new TileCoord(5, 5), new TileCoord(6, 5));
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Move(new TileCoord(6, 5))), Orders(1, 2, TileUnitAction.Brace()));
        Assert.That(sim.Events.Any(item => item.Type == TileBattleEventType.UnitPushed), Is.True);
        Assert.That(sim.Units[1].Position, Is.EqualTo(new TileCoord(7, 5)));
    }

    [Test]
    public void SimilarHeavyInfantryCreatesStalemateEngagement()
    {
        TileBattleSimulation sim = Simulation(Definition("HeavyA", 7, 2, 150), Definition("HeavyB", 7, 2, 150),
            new TileCoord(5, 5), new TileCoord(7, 5));
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Move(new TileCoord(6, 5))), Orders(1, 2, TileUnitAction.Move(new TileCoord(6, 5))));
        Assert.That(sim.Units[0].Position, Is.EqualTo(new TileCoord(5, 5)));
        Assert.That(sim.Units[1].Position, Is.EqualTo(new TileCoord(7, 5)));
        Assert.That(sim.Units[0].State, Is.EqualTo(TileUnitState.Engaged));
    }

    [Test]
    public void ExtremeMassBreaksThroughLightInfantry()
    {
        TileBattleSimulation sim = Simulation(Definition("ElephantDebug", 8, 2, 400), Definition("Light", 6, 2, 70),
            new TileCoord(5, 5), new TileCoord(6, 5));
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Move(new TileCoord(6, 5))), Orders(1, 2, TileUnitAction.Wait()));
        Assert.That(sim.Units[0].Position, Is.EqualTo(new TileCoord(6, 5)));
        Assert.That(sim.Units[1].Position, Is.EqualTo(new TileCoord(7, 5)));
    }

    [Test]
    public void SameTickAttacksApplySimultaneously()
    {
        TileBattleSimulation sim = Simulation(Definition("A", 5, 1, 100), Definition("B", 5, 1, 100),
            new TileCoord(5, 5), new TileCoord(6, 5));
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Attack(new TileCoord(6, 5))), Orders(1, 2, TileUnitAction.Attack(new TileCoord(5, 5))));
        Assert.That(sim.Units[0].Strength, Is.LessThan(100));
        Assert.That(sim.Units[1].Strength, Is.LessThan(100));
    }

    [Test]
    public void PersistentAttackOrderUsesWeaponIntervalWithoutExtraTacticalActions()
    {
        TileBattleUnitDefinition fastSword = Definition("Fast Sword", 6, 2, 100);
        fastSword.MeleeAttackIntervalTicks = 2;
        TileBattleSimulation sim = Simulation(fastSword, Definition("Target", 12, 1, 100),
            new TileCoord(5, 5), new TileCoord(6, 5));

        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Attack(2, new TileCoord(6, 5)), TileUnitAction.Wait()),
            new TileOrderSet { Side = 1 });

        Assert.That(sim.Events.Count(item => item.Type == TileBattleEventType.ActionStarted && item.UnitId == 1), Is.EqualTo(2));
        Assert.That(sim.Events.Count(item => item.Type == TileBattleEventType.UnitAttacked && item.UnitId == 1),
            Is.GreaterThan(2), "One persistent Attack order should permit several independent weapon attacks.");
    }

    [Test]
    public void LongerMeleeReachCanStrikeBeforeShortWeaponCanReply()
    {
        TileBattleUnitDefinition spear = Definition("Spear", 1, 1, 100, TileWeaponControl.Spear);
        spear.MeleeRange = 2; spear.MeleeReachPattern = MeleeReachPattern.Long; spear.MeleeAttackIntervalTicks = 1;
        TileBattleUnitDefinition sword = Definition("Sword", 1, 1, 100);
        sword.MeleeRange = 1; sword.MeleeAttackIntervalTicks = 1;
        TileBattleSimulation sim = Simulation(spear, sword, new TileCoord(5, 5), new TileCoord(7, 5));

        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Attack(2, new TileCoord(7, 5))),
            Orders(1, 2, TileUnitAction.Attack(1, new TileCoord(5, 5))));

        Assert.That(sim.Units.Single(unit => unit.Id == 2).Strength, Is.LessThan(100));
        Assert.That(sim.Units.Single(unit => unit.Id == 1).Strength, Is.EqualTo(100));
    }

    [Test]
    public void CampaignAdapterMapsWeaponAttackTimeToIndependentTicks()
    {
        UnitSaveData source = ScriptableObject.CreateInstance<UnitSaveData>();
        Weapon melee = ScriptableObject.CreateInstance<Weapon>();
        try
        {
            melee.attacktime = .5d; melee.combatdistance = 2d; source.MeleeWeapon = melee;
            TileBattleUnitDefinition definition = TileBattleCampaignAdapter.CreateDefinition(source);
            Assert.That(definition.MeleeAttackIntervalTicks, Is.EqualTo(5));
            Assert.That(definition.MeleeRange, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void AutonomousGeneralsCloseAndActuallyInflictDamage()
    {
        PersonalityTileGeneral left = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Left", Aggressive = 30 });
        PersonalityTileGeneral right = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Right", Aggressive = 30 });
        TileBattleSimulation sim = new TileBattleSimulation(new TileBattleRules(), left, right);
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        sim.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = infantry, Position = new TileCoord(5, 10), Facing = TileFacing.East });
        sim.AddUnit(new TileBattleUnit { Id = 2, Side = 1, Definition = infantry, Position = new TileCoord(14, 10), Facing = TileFacing.West });
        for (int round = 0; round < 8 && !sim.Result.Finished; round++) sim.RunCommandRound();
        Assert.That(sim.Events.Any(item => item.Type == TileBattleEventType.UnitAttacked), Is.True);
        Assert.That(sim.Units[0].Strength + sim.Units[1].Strength, Is.LessThan(200));
    }

    [Test]
    public void GeneralLedBattleReplaysToIdenticalNetworkHash()
    {
        TileGeneralPersonality leftProfile = new TileGeneralPersonality { Name = "Bold", Bold = 60, Aggressive = 40 };
        TileGeneralPersonality rightProfile = new TileGeneralPersonality { Name = "Cavalry", CavalryMinded = 70, Methodical = 20 };
        TileBattleSimulation first = ReplaySimulation(leftProfile, rightProfile);
        TileBattleSimulation second = ReplaySimulation(leftProfile, rightProfile);
        for (int round = 0; round < 6; round++) { first.RunCommandRound(); second.RunCommandRound(); }
        Assert.That(second.ComputeHash(), Is.EqualTo(first.ComputeHash()));
        Assert.That(second.Events.Count, Is.EqualTo(first.Events.Count));
    }

    [Test]
    public void VanguardMainArmyAndReserveDeployInSeparatePhases()
    {
        PersonalityTileGeneral left = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Aggressive", Aggressive = 60 });
        PersonalityTileGeneral right = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Defender", Defensive = 60 });
        TileBattleSimulation sim = new TileBattleSimulation(new TileBattleRules(), left, right);
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        sim.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = infantry, Position = new TileCoord(2, 8), Facing = TileFacing.East, IsVanguard = true });
        sim.AddUnit(new TileBattleUnit { Id = 2, Side = 0, Definition = infantry, Position = new TileCoord(2, 9), Facing = TileFacing.East, Deployed = false });
        sim.AddUnit(new TileBattleUnit { Id = 3, Side = 0, Definition = infantry, Position = new TileCoord(2, 10), Facing = TileFacing.East, Deployed = false, IsReserve = true });
        sim.AddUnit(new TileBattleUnit { Id = 10001, Side = 1, Definition = infantry, Position = new TileCoord(17, 8), Facing = TileFacing.West, IsVanguard = true });
        for (int i = 0; i < 3; i++) sim.RunCommandRound();
        Assert.That(sim.Units.Find(item => item.Id == 2).Deployed, Is.False);
        sim.RunCommandRound();
        Assert.That(sim.Units.Find(item => item.Id == 2).Deployed, Is.True);
        Assert.That(sim.Units.Find(item => item.Id == 3).Deployed, Is.False);
        while (sim.CommandRound < 7 && !sim.Result.Finished) sim.RunCommandRound();
        Assert.That(sim.Units.Find(item => item.Id == 3).Deployed, Is.True);
    }

    [Test]
    public void SecondMainWaveWaitsUntilRoundFiveAndEntersOnEdge()
    {
        TileBattleSimulation sim = new TileBattleSimulation(new TileBattleRules(), null, null);
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        sim.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = infantry,
            Position = new TileCoord(0, 8), Facing = TileFacing.East, IsVanguard = true });
        sim.AddUnit(new TileBattleUnit { Id = 2, Side = 0, Definition = infantry,
            Position = new TileCoord(2, 9), Facing = TileFacing.East, Deployed = false, DeploymentRound = 5 });
        sim.AddUnit(new TileBattleUnit { Id = 10001, Side = 1, Definition = infantry,
            Position = new TileCoord(19, 8), Facing = TileFacing.West, IsVanguard = true });

        for (int round = 0; round < 4; round++) sim.RunCommandRound();
        Assert.That(sim.Units.Single(unit => unit.Id == 2).Deployed, Is.False);
        sim.RunCommandRound();
        Assert.That(sim.Units.Single(unit => unit.Id == 2).Deployed, Is.True);
        Assert.That(sim.Units.Single(unit => unit.Id == 2).Position.X, Is.EqualTo(0));
    }

    [Test]
    public void RangedAttackProducesProjectilePresentationEvent()
    {
        TileBattleSimulation sim = Simulation(Definition("Skirmisher", 5, 3, 70, ranged: true),
            Definition("Target", 7, 2, 100), new TileCoord(5, 5), new TileCoord(8, 5));
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Attack(new TileCoord(8, 5))), Orders(1, 2, TileUnitAction.Wait()));
        Assert.That(sim.Events.Any(item => item.Type == TileBattleEventType.ProjectileLaunched), Is.True);
        Assert.That(sim.Events.Any(item => item.Type == TileBattleEventType.UnitAttacked), Is.True);
    }

    [Test]
    public void GeneralBenchmarkSuiteProducesCombatAndReserveMetrics()
    {
        var results = TileBattleGeneralBenchmark.RunStandardSuite();
        Assert.That(results.Count, Is.EqualTo(32));
        Assert.That(results.Exists(item => item.Attacks > 0), Is.True);
        Assert.That(results.Exists(item => item.ReserveCommitments > 0), Is.True);
        Assert.That(TileBattleGeneralBenchmark.ToCsv(results), Does.Contain("LeftPlanChanges"));
        Assert.That(TileBattleGeneralBenchmark.ToCsv(results), Does.Contain("LeftFirstReserveRound"));
    }

    [Test]
    public void GeneralKeepsInitialOverallBattlePlan()
    {
        TileBattleSimulation sim = ReplaySimulation(new TileGeneralPersonality { Name = "Adaptive", Competence = 90, Opportunistic = 90 },
            new TileGeneralPersonality { Name = "Enemy", Competence = 50 });
        for (int round = 0; round < 10 && !sim.Result.Finished; round++) sim.RunCommandRound();
        string first = null;
        foreach (TileBattleEvent battleEvent in sim.Events)
        {
            if (battleEvent.Type != TileBattleEventType.PlanChosen || !battleEvent.Message.StartsWith("Left committed ")) continue;
            string plan = battleEvent.Message.Substring("Left committed ".Length).Split(':')[0];
            if (first == null) first = plan; else Assert.That(plan, Is.EqualTo(first));
        }
    }

    [Test]
    public void ClearlyWeakEnemyCentreSelectsCentreAttackInitially()
    {
        PersonalityTileGeneral general = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Assessor", Competence = 70 });
        TileBattleObservation observation = new TileBattleObservation { Side = 0, CommandRound = 1, Width = 20, Height = 20 };
        TileBattleUnitDefinition strong = Definition("Strong", 7, 2, 120); strong.Strength = 100;
        TileBattleUnitDefinition weak = Definition("Weak", 6, 2, 70); weak.Strength = 20;
        observation.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 300, Deployed = true, Position = new TileCoord(2, 10), Definition = strong });
        for (int i = 0; i < 6; i++) observation.Units.Add(new TileObservedUnit { Id = 10001 + i, Side = 1,
            Strength = i == 2 || i == 3 ? 20 : 100, Deployed = true, Position = new TileCoord(16, 4 + i * 2), Definition = i == 2 || i == 3 ? weak : strong });
        Assert.That(general.FormulateOrders(observation).Plan, Is.EqualTo(TileBattlePlan.AttackCentre));
    }

    [Test]
    public void SymmetricArmiesDoNotHavePermanentLeftResolutionAdvantage()
    {
        TileGeneralPersonality profile = new TileGeneralPersonality { Name = "Same", Competence = 50 };
        TileBattleSimulation sim = new TileBattleSimulation(new TileBattleRules(), new PersonalityTileGeneral(profile), new PersonalityTileGeneral(profile));
        TileBattleUnitDefinition infantry = Definition("Same Infantry", 7, 2, 120);
        sim.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = infantry, Position = new TileCoord(4, 8), Facing = TileFacing.East });
        sim.AddUnit(new TileBattleUnit { Id = 2, Side = 0, Definition = infantry, Position = new TileCoord(4, 12), Facing = TileFacing.East });
        sim.AddUnit(new TileBattleUnit { Id = 10001, Side = 1, Definition = infantry, Position = new TileCoord(15, 8), Facing = TileFacing.West });
        sim.AddUnit(new TileBattleUnit { Id = 10002, Side = 1, Definition = infantry, Position = new TileCoord(15, 12), Facing = TileFacing.West });
        sim.RunToCompletion(40);
        int left = sim.Units.Where(item => item.Side == 0).Sum(item => item.Strength);
        int right = sim.Units.Where(item => item.Side == 1).Sum(item => item.Strength);
        Assert.That(left, Is.EqualTo(right));
    }

    [Test]
    public void SymmetricFlankOpportunityDoesNotAlwaysChooseFlankLeft()
    {
        TileBattleObservation observationA = SymmetricCavalryObservation();
        TileBattleObservation observationB = SymmetricCavalryObservation();
        TileBattlePlan planA = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "A", CavalryMinded = 80 }).FormulateOrders(observationA).Plan;
        TileBattlePlan planB = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "B", CavalryMinded = 80 }).FormulateOrders(observationB).Plan;
        Assert.That(planA, Is.Not.EqualTo(planB));
        Assert.That(new[] { planA, planB }, Does.Contain(TileBattlePlan.FlankLeft));
        Assert.That(new[] { planA, planB }, Does.Contain(TileBattlePlan.FlankRight));
    }

    [Test]
    public void GeneratedGeneralCharacterIsStableAndNonNeutral()
    {
        TileGeneralPersonality first = TileBattleCampaignAdapter.CreateGeneratedPersonality("army-17", "Test General");
        TileGeneralPersonality replay = TileBattleCampaignAdapter.CreateGeneratedPersonality("army-17", "Test General");

        Assert.That(PersonalitySignature(first), Is.EqualTo(PersonalitySignature(replay)));
        Assert.That(first.Competence, Is.InRange(35, 80));
        Assert.That(first.Bold + first.Cautious + first.Patient + first.Aggressive + first.Methodical +
            first.Opportunistic + first.CavalryMinded + first.Defensive + first.Stubborn, Is.GreaterThan(0));
    }

    [Test]
    public void DifferentArmyIdentitiesGenerateVariedCharacters()
    {
        string[] signatures = Enumerable.Range(1, 8)
            .Select(index => PersonalitySignature(TileBattleCampaignAdapter.CreateGeneratedPersonality("army-" + index)))
            .Distinct().ToArray();
        Assert.That(signatures.Length, Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void AttackerCannotSelectHoldButDefenderCan()
    {
        TileGeneralPersonality defensive = new TileGeneralPersonality
            { Name = "Defensive", Defensive = 90, Cautious = 70, Patient = 60 };
        TileBattleObservation attackerView = ConventionalObservation(true);
        TileBattleObservation defenderView = ConventionalObservation(false);

        TileBattlePlan attackerPlan = new PersonalityTileGeneral(defensive).FormulateOrders(attackerView).Plan;
        TileBattlePlan defenderPlan = new PersonalityTileGeneral(defensive).FormulateOrders(defenderView).Plan;

        Assert.That(attackerPlan, Is.Not.EqualTo(TileBattlePlan.Hold));
        Assert.That(defenderPlan, Is.EqualTo(TileBattlePlan.Hold));
    }

    [Test]
    public void NeutralAttackerUsesConventionalCentreAttackAsBaseline()
    {
        TileBattlePlan plan = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Neutral" })
            .FormulateOrders(ConventionalObservation(true)).Plan;
        Assert.That(plan, Is.EqualTo(TileBattlePlan.AttackCentre));
    }

    [Test]
    public void FlankPlanKeepsAnInfantryFrontAdvancingThroughItsLanes()
    {
        TileBattleObservation observation = new TileBattleObservation
            { Side = 0, IsAttacker = true, CommandRound = 1, Width = 20, Height = 20 };
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        TileBattleUnitDefinition cavalry = Definition("Cavalry", 4, 4, 100, cavalry: true);
        for (int i = 0; i < 6; i++)
        {
            observation.Units.Add(new TileObservedUnit { Id = i + 1, Side = 0, Strength = 100, Deployed = true,
                Position = new TileCoord(2, 3 + i * 2), Definition = i == 2 ? cavalry : infantry });
            observation.Units.Add(new TileObservedUnit { Id = 10001 + i, Side = 1, Strength = 100, Deployed = true,
                Position = new TileCoord(17, 3 + i * 2), Definition = infantry });
        }

        TileOrderSet orders = new PersonalityTileGeneral(new TileGeneralPersonality
            { Name = "Flanker", CavalryMinded = 100, Opportunistic = 40 }).FormulateOrders(observation);
        int forwardInfantry = orders.Orders.Count(order => order.UnitId != 3 && order.Actions.Count > 0 &&
            order.Actions[0].Type == TileActionType.Move && order.Actions[0].Target.X == 3);

        Assert.That(orders.Plan == TileBattlePlan.FlankLeft || orders.Plan == TileBattlePlan.FlankRight, Is.True);
        Assert.That(forwardInfantry, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void NeutralInfantryBlocksMeetNearTheBattlefieldCentre()
    {
        TileBattleUnitDefinition infantry = Definition("Line Infantry", 7, 2, 120);
        TileBattleSimulation simulation = new TileBattleSimulation(new TileBattleRules(),
            new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Left Neutral" }),
            new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Right Neutral" }));
        int[] lanes = { 8, 9, 10, 11 };
        for (int i = 0; i < lanes.Length; i++)
        {
            int depth = i / 2;
            simulation.AddUnit(new TileBattleUnit { Id = i + 1, Side = 0, Definition = infantry,
                Position = new TileCoord(3 - depth, lanes[i]), Facing = TileFacing.East });
            simulation.AddUnit(new TileBattleUnit { Id = 10001 + i, Side = 1, Definition = infantry,
                Position = new TileCoord(16 + depth, lanes[i]), Facing = TileFacing.West });
        }

        for (int round = 0; round < 5 && !simulation.Result.Finished; round++) simulation.RunCommandRound();

        int closest = simulation.Units.Where(left => left.Side == 0).Min(left =>
            simulation.Units.Where(right => right.Side == 1).Min(right => left.Position.ManhattanDistance(right.Position)));
        Assert.That(closest, Is.LessThanOrEqualTo(1));
        Assert.That(simulation.Units.Where(unit => unit.Side == 0).Max(unit => unit.Position.X), Is.InRange(8, 11));
        Assert.That(simulation.Units.Where(unit => unit.Side == 1).Min(unit => unit.Position.X), Is.InRange(8, 11));
    }

    [Test]
    public void FormationCanPassThroughFriendlyWhenBeyondTileIsFree()
    {
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        TileBattleSimulation simulation = new TileBattleSimulation(new TileBattleRules(), null, null);
        simulation.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = infantry,
            Position = new TileCoord(5, 5), Facing = TileFacing.East });
        simulation.AddUnit(new TileBattleUnit { Id = 2, Side = 0, Definition = infantry,
            Position = new TileCoord(6, 5), Facing = TileFacing.East });

        simulation.ResolveOrders(Orders(0, 1, TileUnitAction.Move(new TileCoord(6, 5))),
            new TileOrderSet { Side = 1 });

        Assert.That(simulation.Units.Single(unit => unit.Id == 1).Position, Is.EqualTo(new TileCoord(7, 5)));
        Assert.That(simulation.Units.Single(unit => unit.Id == 2).Position, Is.EqualTo(new TileCoord(6, 5)));
    }

    [Test]
    public void GeneralRoutesRearFormationAroundEngagedFriendlyBlocker()
    {
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        TileBattleObservation observation = new TileBattleObservation
            { Side = 0, IsAttacker = true, CommandRound = 1, Width = 20, Height = 20 };
        observation.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 100, Deployed = true,
            Position = new TileCoord(5, 10), Definition = infantry });
        observation.Units.Add(new TileObservedUnit { Id = 2, Side = 0, Strength = 100, Deployed = true,
            State = TileUnitState.Engaged, Position = new TileCoord(6, 10), Definition = infantry });
        observation.Units.Add(new TileObservedUnit { Id = 10001, Side = 1, Strength = 100, Deployed = true,
            State = TileUnitState.Engaged, Position = new TileCoord(7, 10), Definition = infantry });

        TileOrderSet orders = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Router" }).FormulateOrders(observation);
        TileUnitOrder rear = orders.Orders.Single(order => order.UnitId == 1);

        Assert.That(rear.Actions[0].Type, Is.EqualTo(TileActionType.Move));
        Assert.That(rear.Actions[0].Target.X, Is.EqualTo(5));
        Assert.That(rear.Actions[0].Target.Y, Is.Not.EqualTo(10));
    }

    [Test]
    public void RangedLightCavalryAttacksThenWithdrawsFromNearbyInfantry()
    {
        TileBattleUnitDefinition numidian = Definition("Numidian", 4, 4, 90, cavalry: true, ranged: true);
        numidian.RangedRange = 4;
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        TileBattleObservation observation = new TileBattleObservation
            { Side = 0, IsAttacker = true, CommandRound = 1, Width = 20, Height = 20 };
        observation.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 100, Deployed = true,
            Position = new TileCoord(8, 10), Definition = numidian });
        observation.Units.Add(new TileObservedUnit { Id = 10001, Side = 1, Strength = 100, Deployed = true,
            Position = new TileCoord(10, 10), Definition = infantry });

        TileUnitOrder order = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Skirmisher" })
            .FormulateOrders(observation).Orders.Single(item => item.UnitId == 1);

        Assert.That(order.Actions[0].Type, Is.EqualTo(TileActionType.Attack));
        Assert.That(order.Actions.Skip(1).Any(action => action.Type == TileActionType.Move && action.Target.X < 8), Is.True);
    }

    [Test]
    public void LightInfantryYieldsLaneToHeavierMainlineInfantry()
    {
        TileBattleUnitDefinition light = Definition("Light Infantry", 6, 2, 85);
        TileBattleUnitDefinition heavy = Definition("Heavy Infantry", 7, 2, 140);
        TileBattleObservation observation = new TileBattleObservation
            { Side = 0, IsAttacker = true, CommandRound = 1, Width = 20, Height = 20 };
        observation.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 100, Deployed = true,
            Position = new TileCoord(6, 10), Definition = light });
        observation.Units.Add(new TileObservedUnit { Id = 2, Side = 0, Strength = 100, Deployed = true,
            Position = new TileCoord(5, 10), Definition = heavy });
        observation.Units.Add(new TileObservedUnit { Id = 10001, Side = 1, Strength = 100, Deployed = true,
            Position = new TileCoord(16, 10), Definition = heavy });

        TileUnitOrder order = new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Organizer" })
            .FormulateOrders(observation).Orders.Single(item => item.UnitId == 1);

        Assert.That(order.Actions[0].Type, Is.EqualTo(TileActionType.Move));
        Assert.That(order.Actions[0].Target.X, Is.EqualTo(6));
        Assert.That(order.Actions[0].Target.Y, Is.Not.EqualTo(10));
    }

    [Test]
    public void IncompetentGeneralMayRecognizeSkirmishDangerOnlyAfterLosses()
    {
        TileBattleUnitDefinition numidian = Definition("Numidian", 4, 4, 90, cavalry: true, ranged: true);
        numidian.RangedRange = 4;
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        TileBattleObservation fresh = new TileBattleObservation
            { Side = 0, IsAttacker = true, CommandRound = 1, Width = 20, Height = 20 };
        fresh.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 100, Deployed = true,
            Position = new TileCoord(8, 10), Definition = numidian });
        fresh.Units.Add(new TileObservedUnit { Id = 10001, Side = 1, Strength = 100, Deployed = true,
            Position = new TileCoord(10, 10), Definition = infantry });
        TileBattleObservation bloodied = new TileBattleObservation
            { Side = 0, IsAttacker = true, CommandRound = 2, Width = 20, Height = 20 };
        bloodied.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 90, Deployed = true,
            Position = new TileCoord(8, 10), Definition = numidian });
        bloodied.Units.Add(new TileObservedUnit { Id = 10001, Side = 1, Strength = 100, Deployed = true,
            Position = new TileCoord(10, 10), Definition = infantry });
        TileGeneralPersonality incompetent = new TileGeneralPersonality { Name = "Oblivious", Competence = 0 };

        TileUnitOrder before = new PersonalityTileGeneral(incompetent).FormulateOrders(fresh).Orders.Single(item => item.UnitId == 1);
        TileUnitOrder after = new PersonalityTileGeneral(incompetent).FormulateOrders(bloodied).Orders.Single(item => item.UnitId == 1);

        Assert.That(before.Purpose, Is.Not.EqualTo("Skirmish withdrawal"));
        Assert.That(after.Purpose, Is.EqualTo("Skirmish withdrawal"));
        Assert.That(after.Actions.Any(action => action.Type == TileActionType.Move && action.Target.X < 8), Is.True);
    }

    [Test]
    public void RangedAttackConsumesAmmunitionAndCannotFireWhenEmpty()
    {
        TileBattleUnitDefinition ranged = Definition("Javelins", 5, 2, 80, ranged: true);
        ranged.Ammunition = 1;
        TileBattleSimulation simulation = Simulation(ranged, Definition("Target", 7, 2, 120),
            new TileCoord(5, 5), new TileCoord(8, 5));

        simulation.ResolveOrders(Orders(0, 1, TileUnitAction.Attack(new TileCoord(8, 5))), new TileOrderSet { Side = 1 });
        int afterFirstShot = simulation.Units.Single(unit => unit.Id == 2).Strength;
        simulation.ResolveOrders(Orders(0, 1, TileUnitAction.Attack(new TileCoord(8, 5))), new TileOrderSet { Side = 1 });

        Assert.That(simulation.Units.Single(unit => unit.Id == 1).Ammunition, Is.EqualTo(0));
        Assert.That(simulation.Units.Single(unit => unit.Id == 2).Strength, Is.EqualTo(afterFirstShot));
        Assert.That(simulation.Events.Count(item => item.Type == TileBattleEventType.ProjectileLaunched), Is.EqualTo(1));
    }

    [Test]
    public void ShieldProtectsFrontButHasZeroSideEffectiveness()
    {
        TileBattleUnitDefinition attacker = Definition("Attacker", 7, 2, 100);
        TileBattleUnitDefinition shielded = Definition("Shielded", 7, 2, 100);
        attacker.Strength = 1000; shielded.Strength = 1000;
        shielded.ShieldPercent = 50; shielded.ShieldFrontEffectivenessPercent = 100;
        shielded.ShieldSideEffectivenessPercent = 0; shielded.ArmorPercent = 0;

        TileBattleSimulation frontal = Simulation(attacker, shielded, new TileCoord(6, 7), new TileCoord(7, 7));
        frontal.ResolveOrders(Orders(0, 1, TileUnitAction.Attack(new TileCoord(7, 7))), new TileOrderSet { Side = 1 });
        int frontalDamage = 1000 - frontal.Units.Single(unit => unit.Id == 2).Strength;

        TileBattleSimulation side = Simulation(attacker, shielded, new TileCoord(7, 6), new TileCoord(7, 7));
        side.Units.Single(unit => unit.Id == 1).Facing = TileFacing.South;
        side.ResolveOrders(Orders(0, 1, TileUnitAction.Attack(new TileCoord(7, 7))), new TileOrderSet { Side = 1 });
        int sideDamage = 1000 - side.Units.Single(unit => unit.Id == 2).Strength;

        Assert.That(frontalDamage, Is.GreaterThan(0));
        Assert.That(sideDamage, Is.GreaterThan(frontalDamage));
    }

    [Test]
    public void HoldPlanFormsCenteredLayeredDefensiveLineInsteadOfEdgeColumn()
    {
        TileBattleUnitDefinition infantry = Definition("Defender", 5, 3, 120);
        TileBattleSimulation simulation = new TileBattleSimulation(new TileBattleRules(), null,
            new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Defender", Defensive = 100, Patient = 60 }));
        simulation.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = infantry,
            Position = new TileCoord(0, 10), Facing = TileFacing.East });
        for (int i = 0; i < 10; i++)
            simulation.AddUnit(new TileBattleUnit { Id = 10001 + i, Side = 1, Definition = infantry,
                Position = new TileCoord(19, 2 + i), Facing = TileFacing.West });

        for (int round = 0; round < 8 && !simulation.Result.Finished; round++) simulation.RunCommandRound();

        TileBattleUnit[] defenders = simulation.Units.Where(unit => unit.Side == 1).ToArray();
        Assert.That(defenders.Max(unit => unit.Position.X), Is.GreaterThan(defenders.Min(unit => unit.Position.X)));
        Assert.That(defenders.Max(unit => unit.Position.X), Is.LessThan(19));
        Assert.That(defenders.Max(unit => unit.Position.Y) - defenders.Min(unit => unit.Position.Y), Is.LessThan(9));
    }

    private static TileBattleObservation ConventionalObservation(bool attacker)
    {
        TileBattleObservation observation = new TileBattleObservation
            { Side = attacker ? 0 : 1, IsAttacker = attacker, CommandRound = 1, Width = 20, Height = 20 };
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        observation.Units.Add(new TileObservedUnit { Id = attacker ? 1 : 10001, Side = observation.Side,
            Strength = 100, Deployed = true, Position = new TileCoord(attacker ? 2 : 17, 10), Definition = infantry });
        observation.Units.Add(new TileObservedUnit { Id = attacker ? 10001 : 1, Side = 1 - observation.Side,
            Strength = 100, Deployed = true, Position = new TileCoord(attacker ? 17 : 2, 10), Definition = infantry });
        return observation;
    }

    private static string PersonalitySignature(TileGeneralPersonality value)
    {
        return string.Join(",", value.Competence, value.Bold, value.Cautious, value.Patient, value.Aggressive,
            value.Methodical, value.Opportunistic, value.CavalryMinded, value.Defensive, value.Stubborn);
    }

    private static TileBattleObservation SymmetricCavalryObservation()
    {
        TileBattleObservation observation = new TileBattleObservation { Side = 0, CommandRound = 1, Width = 20, Height = 20 };
        TileBattleUnitDefinition cavalry = Definition("Cavalry", 4, 4, 100, cavalry: true);
        TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120);
        observation.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 100, Deployed = true, Position = new TileCoord(2, 10), Definition = cavalry });
        observation.Units.Add(new TileObservedUnit { Id = 10001, Side = 1, Strength = 100, Deployed = true, Position = new TileCoord(17, 6), Definition = infantry });
        observation.Units.Add(new TileObservedUnit { Id = 10002, Side = 1, Strength = 100, Deployed = true, Position = new TileCoord(17, 10), Definition = infantry });
        observation.Units.Add(new TileObservedUnit { Id = 10003, Side = 1, Strength = 100, Deployed = true, Position = new TileCoord(17, 14), Definition = infantry });
        return observation;
    }

    private static TileBattleSimulation ReplaySimulation(TileGeneralPersonality left, TileGeneralPersonality right)
    {
        TileBattleSimulation sim = new TileBattleSimulation(new TileBattleRules(),
            new PersonalityTileGeneral(left), new PersonalityTileGeneral(right));
        sim.AddUnit(new TileBattleUnit { Id = 1, Side = 0, Definition = Definition("Infantry", 7, 2, 120),
            Position = new TileCoord(4, 9), Facing = TileFacing.East });
        sim.AddUnit(new TileBattleUnit { Id = 2, Side = 0, Definition = Definition("Cavalry", 4, 4, 90, cavalry: true),
            Position = new TileCoord(4, 12), Facing = TileFacing.East });
        sim.AddUnit(new TileBattleUnit { Id = 10001, Side = 1, Definition = Definition("Spear", 7, 2, 125, TileWeaponControl.Spear),
            Position = new TileCoord(15, 9), Facing = TileFacing.West });
        sim.AddUnit(new TileBattleUnit { Id = 10002, Side = 1, Definition = Definition("InfantryB", 7, 2, 120),
            Position = new TileCoord(15, 12), Facing = TileFacing.West });
        return sim;
    }

    private static int ThreatCrossingLoss(TileWeaponControl control)
    {
        TileBattleSimulation sim = Simulation(Definition("Cavalry", 4, 4, 90, cavalry: true), Definition(control.ToString(), 7, 2, 110, control),
            new TileCoord(6, 5), new TileCoord(7, 5));
        sim.Units[0].Facing = TileFacing.North;
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Move(new TileCoord(6, 6))), Orders(1, 2, TileUnitAction.Brace()));
        return 100 - sim.Units[0].Strength;
    }

    [Test]
    public void CommandRoundAlwaysAdvancesAtLeastSixteenResolutionTicks()
    {
        TileBattleSimulation sim = Simulation(Definition("Idle A", 3, 2, 100), Definition("Idle B", 8, 2, 100),
            new TileCoord(1, 1), new TileCoord(18, 18));
        sim.ResolveOrders(new TileOrderSet { Side = 0 }, new TileOrderSet { Side = 1 });
        Assert.That(sim.History.Any(frame => frame.ResolutionTick == 16), Is.True);
    }

    [Test]
    public void MeleeReachPatternsHaveDistinctCoverage()
    {
        TileBattleUnitDefinition shortWeapon = Definition("Short", 1, 1, 100);
        shortWeapon.MeleeReachPattern = MeleeReachPattern.Short;
        TileBattleSimulation shortDiagonal = Simulation(shortWeapon, Definition("Target", 1, 1, 100),
            new TileCoord(5, 5), new TileCoord(6, 6));
        shortDiagonal.ResolveOrders(new TileOrderSet { Side = 0 }, new TileOrderSet { Side = 1 });
        Assert.That(shortDiagonal.Units[1].Strength, Is.EqualTo(100));

        TileBattleUnitDefinition standard = Definition("Standard", 1, 1, 100);
        standard.MeleeReachPattern = MeleeReachPattern.Standard;
        TileBattleSimulation standardDiagonal = Simulation(standard, Definition("Target", 1, 1, 100),
            new TileCoord(5, 5), new TileCoord(6, 6));
        standardDiagonal.ResolveOrders(new TileOrderSet { Side = 0 }, new TileOrderSet { Side = 1 });
        Assert.That(standardDiagonal.Units[1].Strength, Is.LessThan(100));

        TileBattleUnitDefinition longWeapon = Definition("Long", 1, 1, 100);
        longWeapon.MeleeReachPattern = MeleeReachPattern.Long;
        TileBattleSimulation longLinear = Simulation(longWeapon, Definition("Target", 1, 1, 100),
            new TileCoord(5, 5), new TileCoord(7, 5));
        longLinear.ResolveOrders(new TileOrderSet { Side = 0 }, new TileOrderSet { Side = 1 });
        Assert.That(longLinear.Units[1].Strength, Is.LessThan(100));
    }

    [Test]
    public void ForestDefencePreventsPushThatSucceedsOnOpenGround()
    {
        TileBattleUnitDefinition attacker = Definition("Heavy", 1, 1, 150);
        TileBattleUnitDefinition defender = Definition("Line", 1, 1, 100);
        attacker.Strength = defender.Strength = 500;
        TileBattleSimulation open = Simulation(attacker, defender, new TileCoord(5, 5), new TileCoord(6, 5));
        open.ResolveOrders(new TileOrderSet { Side = 0 }, new TileOrderSet { Side = 1 });
        Assert.That(open.Events.Any(item => item.Type == TileBattleEventType.UnitPushed), Is.True);

        TileBattleSimulation forest = Simulation(attacker, defender, new TileCoord(5, 5), new TileCoord(6, 5));
        forest.Grid.SetTerrain(new TileCoord(6, 5), TileTerrain.Forest, 2);
        forest.ResolveOrders(new TileOrderSet { Side = 0 }, new TileOrderSet { Side = 1 });
        Assert.That(forest.Events.Any(item => item.Type == TileBattleEventType.UnitPushed), Is.False);
    }

    [Test]
    public void ChargeBuildsMomentumAndCreatesChargeImpact()
    {
        TileBattleUnitDefinition cavalry = Definition("Cavalry", 1, 4, 100, cavalry: true);
        cavalry.Strength = 500;
        TileBattleUnitDefinition target = Definition("Target", 6, 1, 100); target.Strength = 500;
        TileBattleSimulation sim = Simulation(cavalry, target, new TileCoord(3, 5), new TileCoord(6, 5));
        sim.ResolveOrders(Orders(0, 1, TileUnitAction.Charge(new TileCoord(4, 5), 2),
            TileUnitAction.Charge(new TileCoord(5, 5), 2)), new TileOrderSet { Side = 1 });
        Assert.That(sim.Events.Any(item => item.Type == TileBattleEventType.ChargeImpact), Is.True);
    }

    [Test]
    public void GeneralChargesThroughForestOnlyWithForestCapability()
    {
        TileBattleObservation observation = new TileBattleObservation
            { Side = 0, IsAttacker = true, CommandRound = 1, Width = 20, Height = 20 };
        TileBattleUnitDefinition cavalry = Definition("Cavalry", 2, 4, 100, cavalry: true);
        observation.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 100, Deployed = true,
            Position = new TileCoord(5, 10), Facing = TileFacing.East, Definition = cavalry });
        observation.Units.Add(new TileObservedUnit { Id = 10001, Side = 1, Strength = 100, Deployed = true,
            Position = new TileCoord(7, 10), Facing = TileFacing.West, Definition = Definition("Enemy", 7, 2, 100) });
        observation.Cells.Add(new TileObservedCell { Position = new TileCoord(6, 10), Terrain = TileTerrain.Forest, MovementCost = 2 });
        TileGeneralPersonality profile = new TileGeneralPersonality { Name = "Terrain General", Aggressive = 80, Competence = 100 };

        TileUnitOrder ordinary = new PersonalityTileGeneral(profile).FormulateOrders(observation).Orders.Single(item => item.UnitId == 1);
        Assert.That(ordinary.Actions.Any(action => action.Type == TileActionType.Charge &&
            action.Target == new TileCoord(6, 10)), Is.False);

        cavalry.ForestImmune = true;
        TileUnitOrder immune = new PersonalityTileGeneral(profile).FormulateOrders(observation).Orders.Single(item => item.UnitId == 1);
        Assert.That(immune.Actions.Any(action => action.Type == TileActionType.Charge &&
            action.Target == new TileCoord(6, 10)), Is.True);
    }

    [Test]
    public void DefensiveGeneralMovesRangedFormationTowardNearbyHill()
    {
        TileBattleObservation observation = new TileBattleObservation
            { Side = 1, IsAttacker = false, CommandRound = 1, Width = 20, Height = 20 };
        TileBattleUnitDefinition ranged = Definition("Ranged", 3, 3, 80, ranged: true);
        observation.Units.Add(new TileObservedUnit { Id = 10001, Side = 1, Strength = 100, Deployed = true,
            Position = new TileCoord(13, 10), Facing = TileFacing.West, Definition = ranged, Ammunition = 10 });
        observation.Units.Add(new TileObservedUnit { Id = 1, Side = 0, Strength = 100, Deployed = true,
            Position = new TileCoord(3, 10), Facing = TileFacing.East, Definition = Definition("Enemy", 7, 2, 100) });
        observation.Cells.Add(new TileObservedCell { Position = new TileCoord(13, 11), Terrain = TileTerrain.Hill });
        PersonalityTileGeneral general = new PersonalityTileGeneral(new TileGeneralPersonality
            { Name = "Hill Defender", Defensive = 120, Patient = 80, Competence = 100 });

        TileUnitOrder order = general.FormulateOrders(observation).Orders.Single(item => item.UnitId == 10001);
        Assert.That(order.Actions.Any(action => action.Type == TileActionType.Move && action.Target == new TileCoord(13, 11)), Is.True);
    }
}
