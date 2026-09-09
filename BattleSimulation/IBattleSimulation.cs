using System;
using System.Collections.Generic;

namespace ProjectX.CombatBackend
{
    public enum BattleSimulationType : byte { Tile, Sector }

    [Serializable]
    public sealed class BattleFormationInput
    {
        public int FormationId;
        public int Side;
        public UnitSaveData Unit;
        public int Strength;
        public string SourceArmyId;
        public int DeploymentColumn = 2;
        public int DeploymentRow = 1;
    }

    [Serializable]
    public sealed class BattleSimulationRequest
    {
        public string BattleId;
        public ulong Seed;
        public readonly List<BattleSideCommandConfig> Commanders = new List<BattleSideCommandConfig>();
        public readonly List<BattleFormationInput> Formations = new List<BattleFormationInput>();
    }

    [Serializable]
    public sealed class BattleSideCommandConfig
    {
        public int Side;
        public string GeneralName;
        public int CommandGroupCapacity = 5;
        public bool PlayerControlled;
    }

    [Serializable]
    public sealed class BattleFormationOutcome
    {
        public int FormationId;
        public int Side;
        public int RemainingStrength;
        public bool Routed;
    }

    [Serializable]
    public sealed class BattleSimulationOutcome
    {
        public bool Finished;
        public int WinningSide = -1;
        public string EndReason;
        public readonly List<BattleFormationOutcome> Formations = new List<BattleFormationOutcome>();
    }

    /// <summary>
    /// Common deterministic battle-backend contract. Campaign orchestration can select a
    /// backend without knowing whether it uses tiles or coarse battlefield sectors.
    /// </summary>
    public interface IBattleSimulation
    {
        BattleSimulationType SimulationType { get; }
        int Tick { get; }
        bool IsStarted { get; }
        bool IsResolved { get; }
        int WinningSide { get; }
        void Initialize(BattleSimulationRequest request);
        void AddFormation(BattleFormationInput formation);
        void StartBattle();
        void TickBattle();
        BattleSimulationOutcome GetOutcome();
        string GetDebugText();
        ulong ComputeHash();
    }
}
