#if UNITY_EDITOR
using NUnit.Framework;
using ProjectX.SectorBattle;

public sealed class SectorBattleTests
{
    [Test]
    public void VisualScenarioContainsExactlyTenFormationsPerSide()
    {
        SectorBattleSimulation simulation = SectorBattleDebugScenario.Create();
        Assert.AreEqual(10, ((System.Collections.Generic.List<SectorFormationPresentationState>)simulation.GetPresentationState()).FindAll(item => item.Side == 0).Count);
        Assert.AreEqual(10, ((System.Collections.Generic.List<SectorFormationPresentationState>)simulation.GetPresentationState()).FindAll(item => item.Side == 1).Count);
    }

    [Test]
    public void TestScenarioIsDeterministic()
    {
        SectorBattleSimulation a = SectorBattleDebugScenario.Create();
        SectorBattleSimulation b = SectorBattleDebugScenario.Create();
        for (int i = 0; i < 20 && !a.IsResolved && !b.IsResolved; i++)
        { SectorBattleDebugScenario.AdvanceOneTick(a); SectorBattleDebugScenario.AdvanceOneTick(b); Assert.AreEqual(a.ComputeHash(), b.ComputeHash()); }
    }

    [Test]
    public void EveryFormationStartsInItsReserveAndOuterFlanksAreEmpty()
    {
        SectorBattleSimulation simulation = SectorBattleDebugScenario.Create();
        foreach (SectorFormationPresentationState formation in simulation.GetPresentationState())
        {
            BattleDepth expected = formation.Side == 0 ? BattleDepth.SideAReserve : BattleDepth.SideBReserve;
            Assert.AreEqual(expected, formation.Sector.Depth, "Formation " + formation.FormationId + " started outside its reserve.");
            Assert.IsTrue(formation.Sector.Lane == BattleLane.UpperWing || formation.Sector.Lane == BattleLane.Centre ||
                formation.Sector.Lane == BattleLane.LowerWing, "Normal deployment populated an outer manoeuvre lane.");
        }
    }

    [Test]
    public void CommandGroupsRespectCapacityAndLockAtBattleStart()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.CommandCapacity20v20, 42, 6, 4, true, true);
        int groupsA = 0, groupsB = 0;
        foreach (SectorCommandGroup candidate in simulation.CommandGroups) { if (candidate.Side == 0) groupsA++; else if (candidate.Side == 1) groupsB++; }
        Assert.LessOrEqual(groupsA, 6);
        Assert.LessOrEqual(groupsB, 4);
        foreach (SectorFormation formation in simulation.Formations)
            Assert.IsNotNull(simulation.Commands.GroupForFormation(formation.Id), "Every formation must belong to one command group.");
        SectorCommandGroup group = null;
        foreach (SectorCommandGroup candidate in simulation.CommandGroups) if (candidate.Side == 0 && candidate.MemberFormationIds.Count > 1) { group = candidate; break; }
        Assert.IsNotNull(group);
        simulation.StartBattle();
        Assert.IsFalse(simulation.Commands.CreateGroup(0, group.MemberFormationIds[0]), "Membership must lock when combat begins.");
    }

    [Test]
    public void GroupMovementKeepsMembersTogether()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.Basic10v10, 7, 4, 4, true, true);
        SectorCommandGroup group = null;
        foreach (SectorCommandGroup candidate in simulation.CommandGroups) if (candidate.Side == 0 && candidate.MemberFormationIds.Count > 1) { group = candidate; break; }
        Assert.IsNotNull(group);
        SectorCoord destination = new SectorCoord(group.CurrentSector.Lane, BattleDepth.SideALine);
        simulation.StartBattle();
        Assert.IsTrue(simulation.IssueGroupMove(group.GroupId, destination));
        for (int i = 0; i < 8; i++) simulation.TickBattle();
        foreach (int formationId in group.MemberFormationIds)
        {
            SectorFormation formation = null;
            foreach (SectorFormation candidate in simulation.Formations) if (candidate.Id == formationId) { formation = candidate; break; }
            Assert.IsNotNull(formation);
            Assert.AreEqual(destination, formation.Sector);
        }
    }

    [Test]
    public void AutomaticAdvanceMeetsOnCentralGroundWithoutPassingThrough()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.Basic10v10, 19, 4, 4, false, false);
        simulation.StartBattle();
        bool centreCombat = false;
        bool prematureBreakthrough = false;
        for (int i = 0; i < 30 && !simulation.IsResolved; i++)
        {
            simulation.TickBattle();
            foreach (SectorCombatEvent battleEvent in simulation.Events)
                if (battleEvent.Type == SectorPresentationEventType.Attack && battleEvent.Sector.Depth == BattleDepth.CentralGround) { centreCombat = true; break; }
            if (!centreCombat) foreach (SectorFormation formation in simulation.Formations)
                if (formation.Side == 0 && formation.Sector.Depth == BattleDepth.SideBReserve ||
                    formation.Side == 1 && formation.Sector.Depth == BattleDepth.SideAReserve) prematureBreakthrough = true;
        }
        Assert.IsTrue(centreCombat, "Automatically advancing armies should meet and fight on Central Ground.");
        Assert.IsFalse(prematureBreakthrough, "A side entered the enemy reserve before fighting on Central Ground.");
    }

    [Test]
    public void AICommitsPartOfItsArmyToAFlank()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.Basic10v10, 31, 4, 4, false, false);
        simulation.StartBattle();
        for (int i = 0; i < 20 && !simulation.IsResolved; i++) simulation.TickBattle();
        bool sideAFlanker = false, sideBFlanker = false;
        foreach (SectorFormation formation in simulation.Formations)
        {
            bool wing = formation.Sector.Lane == BattleLane.TopFlank || formation.Sector.Lane == BattleLane.BottomFlank;
            if (wing && formation.Side == 0) sideAFlanker = true;
            if (wing && formation.Side == 1) sideBFlanker = true;
        }
        foreach (SectorCombatEvent battleEvent in simulation.Events)
        {
            if (battleEvent.Type != SectorPresentationEventType.MovementCompleted ||
                battleEvent.Sector.Lane != BattleLane.TopFlank && battleEvent.Sector.Lane != BattleLane.BottomFlank) continue;
            SectorFormation formation = null;
            foreach (SectorFormation candidate in simulation.Formations)
                if (candidate.Id == battleEvent.FormationId) { formation = candidate; break; }
            if (formation != null && formation.Side == 0) sideAFlanker = true;
            if (formation != null && formation.Side == 1) sideBFlanker = true;
        }
        Assert.IsTrue(sideAFlanker, "Side A should route a mobile command group through an outer flank.");
        Assert.IsTrue(sideBFlanker, "Side B should route a mobile command group through an outer flank.");
    }

    [Test]
    public void CapturedBacklineAndSubstantialLossesCollapseTheArmy()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.Basic10v10, 73, 4, 4, true, true);
        SectorFormation invader = null;
        foreach (SectorFormation formation in simulation.Formations)
            if (formation.Side == 1) { invader = formation; break; }
        Assert.IsNotNull(invader);
        System.Reflection.MethodInfo placeForSetup = typeof(SectorBattleSimulation).GetMethod("PlaceFormationForSetup",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(placeForSetup);
        placeForSetup.Invoke(simulation, new object[] { invader.Id, new SectorCoord(BattleLane.TopFlank, BattleDepth.SideAReserve) });
        simulation.StartBattle();

        int sideAInitial = 0;
        foreach (SectorFormation formation in simulation.Formations) if (formation.Side == 0) sideAInitial += formation.Strength;
        int retained = 0;
        foreach (SectorFormation formation in simulation.Formations)
        {
            if (formation.Side != 0) continue;
            int allowed = System.Math.Max(0, sideAInitial * 50 / 100 - retained);
            formation.Strength = System.Math.Min(formation.Strength, allowed); retained += formation.Strength;
        }

        simulation.TickBattle();
        Assert.IsTrue(simulation.IsResolved);
        Assert.AreEqual(1, simulation.WinningSide);
        StringAssert.Contains("rear position", simulation.GetOutcome().EndReason);
        foreach (SectorFormation formation in simulation.Formations)
            if (formation.Side == 0 && formation.Strength > 0)
                Assert.AreEqual(SectorFormationState.Routing, formation.State);
    }

    [Test]
    public void FiveUncontestedTicksInEnemyReserveForceDefendersToRetreat()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.Basic10v10, 79, 4, 4, true, true);
        SectorFormation invader = null;
        foreach (SectorFormation formation in simulation.Formations)
            if (formation.Side == 1) { invader = formation; break; }
        System.Reflection.MethodInfo placeForSetup = typeof(SectorBattleSimulation).GetMethod("PlaceFormationForSetup",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(invader);
        Assert.IsNotNull(placeForSetup);
        placeForSetup.Invoke(simulation, new object[] { invader.Id, new SectorCoord(BattleLane.TopFlank, BattleDepth.SideAReserve) });
        simulation.StartBattle();

        for (int i = 0; i < 4; i++)
        {
            simulation.TickBattle();
            Assert.IsFalse(simulation.IsResolved, "Rear occupation should require five complete sector ticks.");
        }
        simulation.TickBattle();
        Assert.IsTrue(simulation.IsResolved);
        Assert.AreEqual(1, simulation.WinningSide);
        StringAssert.Contains("held uncontested for 5 sector ticks", simulation.GetOutcome().EndReason);
    }

    [Test]
    public void PlayerCanReplaceAnOrderBeforeTheNextTick()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.Basic10v10, 91, 4, 4, true, true);
        simulation.StartBattle();
        SectorCommandGroup group = null;
        foreach (SectorCommandGroup candidate in simulation.CommandGroups)
            if (candidate.Side == 0 && candidate.PlayerControlled) { group = candidate; break; }
        Assert.IsNotNull(group);

        SectorCoord first = new SectorCoord(BattleLane.UpperWing, BattleDepth.SideAReserve);
        SectorCoord replacement = new SectorCoord(BattleLane.Centre, BattleDepth.SideALine);
        Assert.IsTrue(simulation.IssueGroupMove(group.GroupId, first));
        Assert.IsTrue(group.Moving);
        Assert.IsTrue(simulation.IssueGroupMove(group.GroupId, replacement));
        Assert.AreEqual(replacement, group.DestinationSector);
        foreach (int id in group.MemberFormationIds)
        {
            SectorFormation formation = null;
            foreach (SectorFormation candidate in simulation.Formations) if (candidate.Id == id) { formation = candidate; break; }
            if (formation != null && formation.Active) Assert.AreEqual(replacement, formation.MovementTarget);
        }

        Assert.IsTrue(simulation.IssueGroupOrder(group.GroupId, SectorGroupOrder.Hold));
        Assert.IsFalse(group.Moving);
        foreach (int id in group.MemberFormationIds)
            foreach (SectorFormation formation in simulation.Formations)
                if (formation.Id == id) Assert.IsFalse(formation.Moving);
    }

    [Test]
    public void StartedBattleCanHandPlayerGroupsToAIAndBack()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.Basic10v10, 101, 4, 4, true, false);
        simulation.StartBattle();
        simulation.Commands.SetPlayerControlled(0, false);
        foreach (SectorCommandGroup group in simulation.CommandGroups)
            if (group.Side == 0) Assert.IsFalse(group.PlayerControlled);
        simulation.Commands.SetPlayerControlled(0, true);
        foreach (SectorCommandGroup group in simulation.CommandGroups)
            if (group.Side == 0) Assert.IsTrue(group.PlayerControlled);
    }
    [Test]
    public void GeneratedArmiesHonorCompositionAndGeneralTactics()
    {
        UnitSaveData roman = UnityEngine.Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/LegionaryLevy");
        UnitSaveData carthaginian = UnityEngine.Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/Phoenician Spear");
        Assert.IsNotNull(roman);
        Assert.IsNotNull(carthaginian);
        var sideA = new System.Collections.Generic.List<SectorCustomFormationSpec>
        { new SectorCustomFormationSpec { Unit = roman, Count = 3, Lane = BattleLane.Centre } };
        var sideB = new System.Collections.Generic.List<SectorCustomFormationSpec>
        { new SectorCustomFormationSpec { Unit = carthaginian, Count = 2, Lane = BattleLane.UpperWing } };

        SectorBattleSimulation simulation = SectorCustomBattleFactory.PrepareGenerated(sideA, sideB, 12, 4, 5,
            "Aggressive General", "Flanking General", SectorGeneralTactic.Aggressive, SectorGeneralTactic.Flanking);

        Assert.AreEqual(3, new System.Collections.Generic.List<SectorFormation>(simulation.Formations).FindAll(item => item.Side == 0).Count);
        Assert.AreEqual(2, new System.Collections.Generic.List<SectorFormation>(simulation.Formations).FindAll(item => item.Side == 1).Count);
        Assert.AreEqual(SectorGeneralTactic.Aggressive, simulation.Commands.Tactic(0));
        Assert.AreEqual(SectorGeneralTactic.Flanking, simulation.Commands.Tactic(1));
    }

    [Test]
    public void AttackEventsExposeDamageForUnitPerformanceReports()
    {
        SectorBattleSimulation simulation = SectorCustomBattleFactory.Prepare(SectorBattlePreset.Basic10v10, 22, 4, 4, false, false);
        simulation.StartBattle();
        for (int tick = 0; tick < 80 && !simulation.IsResolved; tick++) simulation.TickBattle();
        bool sawDamage = false;
        foreach (SectorCombatEvent battleEvent in simulation.Events)
            if (battleEvent.Type == SectorPresentationEventType.Attack && battleEvent.Damage > 0) { sawDamage = true; break; }
        Assert.IsTrue(sawDamage, "At least one attack should report damage for unit performance aggregation.");
    }}
#endif
