using System;
using ProjectX.TileBattle;

namespace ProjectX.CombatBackend
{
    /// <summary>Non-invasive adapter around the existing tile simulation.</summary>
    public sealed class TileBattleSimulationAdapter : IBattleSimulation
    {
        private TileBattleSimulation simulation;
        private BattleSimulationRequest request;
        private bool started;
        public BattleSimulationType SimulationType => BattleSimulationType.Tile;
        public int Tick => simulation != null ? simulation.CommandRound : 0;
        public bool IsStarted => started;
        public bool IsResolved => simulation != null && simulation.Result.Finished;
        public int WinningSide => simulation != null ? simulation.Result.WinningSide : -1;
        public TileBattleSimulation Simulation => simulation;

        public void Initialize(BattleSimulationRequest value)
        {
            request = value ?? throw new ArgumentNullException(nameof(value));
            simulation = new TileBattleSimulation(new TileBattleRules(),
                new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Side A" }),
                new PersonalityTileGeneral(new TileGeneralPersonality { Name = "Side B" }));
            foreach (BattleFormationInput formation in request.Formations) AddFormation(formation);
            started = false;
        }

        public void AddFormation(BattleFormationInput formation)
        {
            if (simulation == null || formation == null || formation.Unit == null) return;
            TileBattleUnitDefinition definition = TileBattleCampaignAdapter.CreateDefinition(formation.Unit);
            int side = formation.Side == 0 ? 0 : 1;
            int row = Math.Max(1, Math.Min(simulation.Grid.Height - 2, formation.DeploymentRow + simulation.Grid.Height / 2));
            int x = side == 0 ? 2 : simulation.Grid.Width - 3;
            simulation.AddUnit(new TileBattleUnit { Id = formation.FormationId, Side = side, Definition = definition,
                Position = new TileCoord(x, row), Facing = side == 0 ? TileFacing.East : TileFacing.West,
                Strength = formation.Strength > 0 ? formation.Strength : definition.Strength });
        }

        public void StartBattle() { started = simulation != null; }
        public void TickBattle() { if (started && !IsResolved) simulation.RunCommandRound(); }
        public ulong ComputeHash() => simulation != null ? simulation.ComputeHash() : 0UL;

        public BattleSimulationOutcome GetOutcome()
        {
            BattleSimulationOutcome result = new BattleSimulationOutcome { Finished = IsResolved,
                WinningSide = WinningSide, EndReason = simulation != null ? simulation.Result.EndReason : string.Empty };
            if (simulation != null) foreach (TileBattleUnit unit in simulation.Units)
                result.Formations.Add(new BattleFormationOutcome { FormationId = unit.Id, Side = unit.Side,
                    RemainingStrength = Math.Max(0, unit.Strength), Routed = unit.State == TileUnitState.Routing || unit.State == TileUnitState.Withdrawn });
            return result;
        }

        public string GetDebugText() => simulation == null ? "Tile battle not initialized" :
            "Tile battle " + (request != null ? request.BattleId : string.Empty) + " round " + simulation.CommandRound +
            " tick " + simulation.ResolutionTick + " units " + simulation.Units.Count;
    }
}
