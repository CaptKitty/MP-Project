#if UNITY_EDITOR
using NUnit.Framework;
using ProjectX.DeterministicBattle;
using ProjectX.BattleValidation;

public class DeterministicBattleTests
{
    private static BattleStartState CreateState(ulong seed)
    {
        BattleStartState state = new BattleStartState { BattleId = "test", Seed = seed, TickRate = 10 };
        state.Definitions.Add(new BattleUnitDefinition
        {
            DefinitionId = 1, UnitName = "Infantry", MembersPerCampaignUnit = 4,
            HealthPerMember = 50, SpeedMilliPerTick = 100, MeleeDamage = 12,
            MeleeReachMilli = 900, AttackCooldownTicks = 8, ArmorPercent = 10, ShieldPercent = 10
        });
        state.Formations.Add(new BattleFormationStart
        {
            FormationId = 1, Side = 0, DefinitionId = 1, CampaignUnitCount = 3,
            Position = new Int2(0, -5000), Facing = new Int2(0, 1000)
        });
        state.Formations.Add(new BattleFormationStart
        {
            FormationId = 10001, Side = 1, DefinitionId = 1, CampaignUnitCount = 3,
            Position = new Int2(0, 5000), Facing = new Int2(0, -1000)
        });
        return state;
    }

    [Test]
    public void SameStartStateAndSeedProduceSameResult()
    {
        BattleSimulation first = new BattleSimulation(CreateState(123456UL));
        BattleSimulation second = new BattleSimulation(CreateState(123456UL));
        first.AdvanceTicks(5000);
        second.AdvanceTicks(5000);
        Assert.AreEqual(first.Tick, second.Tick);
        Assert.AreEqual(first.WinningSide, second.WinningSide);
        Assert.AreEqual(first.Rng.State, second.Rng.State);
        Assert.AreEqual(first.ComputeHash(), second.ComputeHash());
    }

    [Test]
    public void TickChunkingDoesNotChangeAuthoritativeResult()
    {
        BattleSimulation perTick = new BattleSimulation(CreateState(789UL));
        BattleSimulation chunked = new BattleSimulation(CreateState(789UL));
        for (int i = 0; i < 1000 && perTick.Status != BattleStatus.Finished; i++) perTick.AdvanceTicks(1);
        chunked.AdvanceTicks(1000);
        Assert.AreEqual(perTick.ComputeHash(), chunked.ComputeHash());
    }

    private sealed class PassiveObserver : IBattleObserver
    {
        public void OnBattleTick(BattleSimulation simulation) { }
        public void OnBattleFinished(BattleSimulation simulation) { }
    }

    private sealed class ChargeObserver : IBattleObserver
    {
        public bool SawCharge;
        public void OnBattleTick(BattleSimulation simulation)
        {
            for (int i = 0; i < simulation.Formations.Count; i++)
                if (simulation.Formations[i].Status == FormationStatus.Charging) SawCharge = true;
        }
        public void OnBattleFinished(BattleSimulation simulation) { }
    }

    [Test]
    public void ObserverDoesNotChangeSimulation()
    {
        BattleSimulation unseen = new BattleSimulation(CreateState(42UL));
        BattleSimulation viewed = new BattleSimulation(CreateState(42UL));
        viewed.AttachObserver(new PassiveObserver());
        unseen.AdvanceTicks(1000);
        viewed.AdvanceTicks(1000);
        Assert.AreEqual(unseen.ComputeHash(), viewed.ComputeHash());
    }

    [Test]
    public void RangedProjectilesAreDeterministicAndConsumeAmmunition()
    {
        BattleStartState firstState = CreateState(991UL);
        BattleUnitDefinition ranged = firstState.Definitions[0];
        ranged.HasRangedWeapon = true;
        ranged.RangedDamage = 10;
        ranged.RangedReachMilli = 12000;
        ranged.RangedCooldownTicks = 10;
        ranged.ProjectileSpeedMilliPerTick = 700;
        ranged.AmmunitionPerCombatant = 3;

        BattleStartState secondState = CreateState(991UL);
        BattleUnitDefinition secondRanged = secondState.Definitions[0];
        secondRanged.HasRangedWeapon = true;
        secondRanged.RangedDamage = 10;
        secondRanged.RangedReachMilli = 12000;
        secondRanged.RangedCooldownTicks = 10;
        secondRanged.ProjectileSpeedMilliPerTick = 700;
        secondRanged.AmmunitionPerCombatant = 3;

        BattleSimulation first = new BattleSimulation(firstState);
        BattleSimulation second = new BattleSimulation(secondState);
        first.AdvanceTicks(300);
        second.AdvanceTicks(300);
        Assert.Greater(first.Projectiles.Count, 0);
        Assert.Less(first.Combatants[0].Ammunition, 3);
        Assert.AreEqual(first.ComputeHash(), second.ComputeHash());
    }

    [Test]
    public void CavalryChargeIsSpatialAndDeterministic()
    {
        BattleStartState state = CreateState(1776UL);
        BattleUnitDefinition cavalry = state.Definitions[0];
        cavalry.Role = BattleUnitRole.Cavalry;
        cavalry.SpeedMilliPerTick = 240;
        cavalry.Mass = 200;
        cavalry.ChargeDamage = 18;
        cavalry.ChargeSpeedMultiplier = 1800;
        cavalry.MinimumChargeDistanceMilli = 2000;
        cavalry.ChargeCooldownTicks = 80;
        cavalry.TurnRateMilli = 180;

        BattleSimulation first = new BattleSimulation(state);
        BattleSimulation second = new BattleSimulation(CreateChargeClone());
        ChargeObserver observer = new ChargeObserver();
        first.AttachObserver(observer);
        first.AdvanceTicks(150);
        second.AdvanceTicks(150);

        Assert.IsTrue(observer.SawCharge);
        Assert.Less(first.Formations[1].Cohesion, 1000);
        Assert.AreEqual(first.ComputeHash(), second.ComputeHash());
    }

    private static BattleStartState CreateChargeClone()
    {
        BattleStartState state = CreateState(1776UL);
        BattleUnitDefinition cavalry = state.Definitions[0];
        cavalry.Role = BattleUnitRole.Cavalry;
        cavalry.SpeedMilliPerTick = 240;
        cavalry.Mass = 200;
        cavalry.ChargeDamage = 18;
        cavalry.ChargeSpeedMultiplier = 1800;
        cavalry.MinimumChargeDistanceMilli = 2000;
        cavalry.ChargeCooldownTicks = 80;
        cavalry.TurnRateMilli = 180;
        return state;
    }

    [Test]
    public void ReserveWaitsUntilDeterministicGeneralRelease()
    {
        BattleStartState state = CreateState(81UL);
        state.Formations.Add(new BattleFormationStart
        {
            FormationId = 2, Side = 0, DefinitionId = 1, CampaignUnitCount = 1,
            Position = new Int2(4000, -8000), Facing = new Int2(0, 1000), Reserve = true
        });
        BattleSimulation simulation = new BattleSimulation(state);
        simulation.ScheduleCommand(new FormationOrderCommand
            { Tick = 1, FormationId = 1, Order = FormationOrder.Hold, LockDurationTicks = 1000 });
        simulation.ScheduleCommand(new FormationOrderCommand
            { Tick = 1, FormationId = 10001, Order = FormationOrder.Hold, LockDurationTicks = 1000 });
        Int2 initial = simulation.Formations.Find(item => item.Id == 2).Position;
        simulation.AdvanceTicks(200);
        Assert.AreEqual(initial.X, simulation.Formations.Find(item => item.Id == 2).Position.X);
        Assert.AreEqual(initial.Y, simulation.Formations.Find(item => item.Id == 2).Position.Y);
        simulation.AdvanceTicks(150);
        Assert.AreNotEqual(initial.Y, simulation.Formations.Find(item => item.Id == 2).Position.Y);
    }

    [Test]
    public void GeneralAssignsCavalryARealLateralFlankPath()
    {
        BattleStartState state = CreateChargeClone();
        state.Definitions[0].MinimumChargeDistanceMilli = 20000;
        BattleSimulation simulation = new BattleSimulation(state);
        int initialX = simulation.Formations[0].Position.X;
        simulation.AdvanceTicks(20);
        Assert.AreNotEqual(FormationOrder.Advance, simulation.Formations[0].Order);
        Assert.AreNotEqual(initialX, simulation.Formations[0].Position.X);
    }

    [Test]
    public void FrontageLimitsSimultaneousMeleeAttackers()
    {
        BattleStartState state = CreateState(900UL);
        state.Definitions[0].PreferredFrontage = 2;
        state.Formations[0].Position = new Int2(0, -500);
        state.Formations[1].Position = new Int2(0, 500);
        BattleSimulation simulation = new BattleSimulation(state);
        simulation.AdvanceTicks(1);
        int attackers = 0;
        SimFormation formation = simulation.Formations[0];
        for (int i = 0; i < formation.CombatantIds.Count; i++)
            if (simulation.Combatants[formation.CombatantIds[i]].NextAttackTick > 0) attackers++;
        Assert.LessOrEqual(attackers, 2);
    }

    [Test]
    public void FriendlyFormationsCannotOccupyTheSameSpace()
    {
        BattleStartState state = CreateState(901UL);
        state.Formations.Add(new BattleFormationStart
        {
            FormationId = 2, Side = 0, DefinitionId = 1, CampaignUnitCount = 1,
            Position = state.Formations[0].Position, Facing = new Int2(0, 1000)
        });
        BattleSimulation simulation = new BattleSimulation(state);
        simulation.AdvanceTicks(1);
        Assert.Greater((simulation.Formations[0].Position - simulation.Formations[1].Position).SqrMagnitude, 0);
    }

    [Test]
    public void DisciplinedHoldingFormationRecoversCohesion()
    {
        BattleStartState state = CreateState(902UL);
        state.Definitions[0].Disciplined = true;
        BattleSimulation simulation = new BattleSimulation(state);
        simulation.Formations[0].Cohesion = 500;
        simulation.ScheduleCommand(new FormationOrderCommand
            { Tick = 1, FormationId = 1, Order = FormationOrder.Hold, LockDurationTicks = 100 });
        simulation.AdvanceTicks(10);
        Assert.Greater(simulation.Formations[0].Cohesion, 500);
    }

    [Test]
    public void RoughTerrainDeterministicallySlowsFormationMovement()
    {
        BattleStartState openState = CreateState(903UL);
        BattleStartState roughState = CreateState(903UL);
        roughState.Terrain.Add(new BattleTerrainArea
        {
            Id = 1, Kind = BattleTerrainKind.Rough, Center = roughState.Formations[0].Position,
            RadiusMilli = 3000, MovementPermille = 500, ChargePermille = 500, RangedAccuracyPermille = 900
        });
        BattleSimulation open = new BattleSimulation(openState);
        BattleSimulation rough = new BattleSimulation(roughState);
        int openStart = open.Formations[0].Position.Y;
        int roughStart = rough.Formations[0].Position.Y;
        open.AdvanceTicks(5);
        rough.AdvanceTicks(5);
        Assert.Greater(open.Formations[0].Position.Y - openStart, rough.Formations[0].Position.Y - roughStart);
    }

    [Test]
    public void ImpassableTerrainProducesDeterministicSteering()
    {
        BattleStartState firstState = CreateState(904UL);
        firstState.Terrain.Add(new BattleTerrainArea
        {
            Id = 9, Kind = BattleTerrainKind.Impassable, Center = new Int2(0, -3500),
            RadiusMilli = 700, MovementPermille = 0, ChargePermille = 0, Impassable = true
        });
        BattleStartState secondState = CreateState(904UL);
        secondState.Terrain.Add(new BattleTerrainArea
        {
            Id = 9, Kind = BattleTerrainKind.Impassable, Center = new Int2(0, -3500),
            RadiusMilli = 700, MovementPermille = 0, ChargePermille = 0, Impassable = true
        });
        BattleSimulation first = new BattleSimulation(firstState);
        BattleSimulation second = new BattleSimulation(secondState);
        first.AdvanceTicks(30);
        second.AdvanceTicks(30);
        Assert.AreEqual(first.ComputeHash(), second.ComputeHash());
        Assert.Greater((first.Formations[0].Position - firstState.Terrain[0].Center).SqrMagnitude, 700L * 700L);
    }

    [Test]
    public void DeploymentInitialOrderEntersAuthoritativeState()
    {
        BattleStartState state = CreateState(905UL);
        state.Formations[0].InitialOrder = FormationOrder.Hold;
        BattleSimulation simulation = new BattleSimulation(state);
        Assert.AreEqual(FormationOrder.Hold, simulation.Formations[0].Order);
    }

    [Test]
    public void AbilityEffectsAndCooldownsAreDeterministic()
    {
        BattleStartState firstState = CreateState(906UL);
        firstState.Generals.Add(new BattleGeneralProfile { Side = 0, Name = "Test General",
            Trait = BattleGeneralTrait.Defensive, CommandIntervalTicks = 1000, AbilityCooldownTicks = 100 });
        BattleStartState secondState = CreateState(906UL);
        secondState.Generals.Add(new BattleGeneralProfile { Side = 0, Name = "Test General",
            Trait = BattleGeneralTrait.Defensive, CommandIntervalTicks = 1000, AbilityCooldownTicks = 100 });
        BattleSimulation first = new BattleSimulation(firstState);
        BattleSimulation second = new BattleSimulation(secondState);
        Assert.IsTrue(first.TryActivateAbility(0, 1, BattleAbilityType.ShieldWall));
        Assert.IsTrue(second.TryActivateAbility(0, 1, BattleAbilityType.ShieldWall));
        Assert.IsFalse(first.TryActivateAbility(0, 1, BattleAbilityType.ShieldWall));
        first.AdvanceTicks(20); second.AdvanceTicks(20);
        Assert.Greater(first.Effects.Count, 0);
        Assert.AreEqual(first.ComputeHash(), second.ComputeHash());
    }

    [Test]
    public void ReinforcementArrivesOnScheduledTick()
    {
        BattleStartState state = CreateState(907UL);
        BattleSimulation simulation = new BattleSimulation(state);
        ReinforcementCommand command = new ReinforcementCommand { Tick = 5 };
        command.Formations.Add(new BattleFormationStart
        {
            FormationId = 2, Side = 0, DefinitionId = 1, CampaignUnitCount = 1,
            Position = new Int2(-18000, 0), Facing = new Int2(1000, 0)
        });
        simulation.ScheduleCommand(command);
        simulation.AdvanceTicks(4);
        Assert.IsNull(simulation.Formations.Find(item => item.Id == 2));
        simulation.AdvanceTicks(1);
        Assert.IsNotNull(simulation.Formations.Find(item => item.Id == 2));
    }

    [Test]
    public void SnapshotIsAReadOnlyCopyOfAuthoritativeValues()
    {
        BattleSimulation simulation = new BattleSimulation(CreateState(908UL));
        simulation.AdvanceTicks(10);
        ulong before = simulation.ComputeHash();
        BattleSnapshot snapshot = simulation.CreateSnapshot();
        snapshot.Formations[0].Morale = 0;
        snapshot.Combatants[0].Health = 0;
        Assert.AreEqual(before, simulation.ComputeHash());
        Assert.AreNotEqual(0, simulation.Formations[0].Morale);
        Assert.AreNotEqual(0, simulation.Combatants[0].Health);
    }

    [Test]
    public void RecordedCommandsCanReconstructRunningBattle()
    {
        BattleStartState state = CreateState(909UL);
        BattleSimulation original = new BattleSimulation(state);
        original.ScheduleCommand(new FormationOrderCommand { Tick = 12, FormationId = 1,
            Order = FormationOrder.Hold, LockDurationTicks = 80 });
        original.AdvanceTicks(100);
        BattleSimulation restored = new BattleSimulation(state);
        BattleCommandRecord record = original.CommandHistory[0];
        restored.ScheduleCommand(new FormationOrderCommand { Tick = record.Tick, FormationId = record.FormationId,
            Order = record.Order, LockDurationTicks = record.LockDurationTicks });
        restored.AdvanceTicks(100);
        Assert.AreEqual(original.ComputeHash(), restored.ComputeHash());
    }

    [Test]
    public void ValidationHarnessRunsSeedBatchAndExportsMetrics()
    {
        BattleScenario scenario = NamedBattleScenarios.EqualInfantry();
        ScenarioBatchResult result = BattleScenarioRunner.RunBatch(scenario, 100UL, 3);
        Assert.AreEqual(3, result.Runs);
        Assert.AreEqual(0, result.DeterminismFailures);
        Assert.AreEqual(0, result.InvariantFailures);
        string csv = BattleScenarioRunner.ToCsv(new[] { result });
        StringAssert.Contains("scenario,seed,tick", csv);
        StringAssert.Contains("Equal Infantry Lines", csv);
    }

    [Test]
    public void AllNamedValidationScenariosExecuteDeterministically()
    {
        foreach (BattleScenario scenario in NamedBattleScenarios.CreateAll())
        {
            BattleRunMetrics result = BattleScenarioRunner.Run(scenario, 1234UL);
            Assert.IsTrue(result.Deterministic, scenario.Name);
            Assert.IsTrue(result.InvariantsPassed, scenario.Name + ": " + string.Join("; ", result.Failures));
        }
    }
}
#endif
