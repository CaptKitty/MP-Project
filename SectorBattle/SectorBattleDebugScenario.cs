using System;
using System.Collections.Generic;
using ProjectX.CombatBackend;
using UnityEngine;

namespace ProjectX.SectorBattle
{
    /// <summary>Deterministic 10v10 geometry demonstration used by tests and the debug window.</summary>
    public static class SectorBattleDebugScenario
    {
        public const int CounterFlankFormationId = 8;

        public static SectorBattleSimulation Create()
        {
            // Movement in this fixture is scripted so the flank transition is easy to inspect.
            SectorBattleSimulation simulation = new SectorBattleSimulation { PrototypeAIEnabled = false };
            BattleSimulationRequest request = new BattleSimulationRequest { BattleId = "sector-debug-10v10", Seed = 1 };
            request.Commanders.Add(new BattleSideCommandConfig { Side = 0, GeneralName = "Roman General", CommandGroupCapacity = 6, PlayerControlled = true });
            request.Commanders.Add(new BattleSideCommandConfig { Side = 1, GeneralName = "Carthaginian General", CommandGroupCapacity = 6, PlayerControlled = false });
            int id = 1;
            Add(request, ref id, 0, "LegionaryLevy", 4, BattleLane.Centre, BattleDepth.SideAReserve);
            Add(request, ref id, 0, "LegionaryLight", 2, BattleLane.Centre, BattleDepth.SideAReserve);
            Add(request, ref id, 0, "Cavalry_Light", 2, BattleLane.UpperWing, BattleDepth.SideAReserve);
            Add(request, ref id, 0, "Velite", 2, BattleLane.LowerWing, BattleDepth.SideAReserve);
            id = 1001;
            Add(request, ref id, 1, "Phoenician Spear", 3, BattleLane.Centre, BattleDepth.SideBReserve);
            Add(request, ref id, 1, "Iberian Light", 2, BattleLane.UpperWing, BattleDepth.SideBReserve);
            Add(request, ref id, 1, "Cavalry_Shock", 2, BattleLane.UpperWing, BattleDepth.SideBReserve);
            // There is no elephant UnitSaveData yet; the large chariot is the prototype large-unit representative.
            Add(request, ref id, 1, "Numidian_Charioteer_Spear", 1, BattleLane.LowerWing, BattleDepth.SideBReserve);
            Add(request, ref id, 1, "Iberian_SpearChucker", 2, BattleLane.LowerWing, BattleDepth.SideBReserve);
            simulation.Initialize(request);
            simulation.SetTerrain(new SectorCoord(BattleLane.Centre, BattleDepth.CentralGround), SectorTerrain.Forest);
            simulation.StartBattle();
            return simulation;
        }

        public static void AdvanceOneTick(SectorBattleSimulation simulation)
        {
            if (simulation == null || simulation.IsResolved) return;
            // Roman reserve cavalry counters the initially uncontested Carthaginian right flank.
            simulation.TickBattle();
        }

        private static void Add(BattleSimulationRequest request, ref int id, int side, string resourceName,
            int count, BattleLane column, BattleDepth row)
        {
            UnitSaveData unit = Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/" + resourceName);
            if (unit == null) throw new InvalidOperationException("Missing debug unit " + resourceName);
            for (int i = 0; i < count; i++) request.Formations.Add(new BattleFormationInput { FormationId = id++, Side = side,
                Unit = unit, Strength = Mathf.Max(1, unit.health), DeploymentColumn = (int)column, DeploymentRow = (int)row });
        }
    }
}
