using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectX.TileBattle;

namespace ProjectX.SectorBattle
{
    public enum BattleLane : byte { TopFlank, UpperWing, Centre, LowerWing, BottomFlank }
    public enum BattleDepth : byte { SideAReserve, SideALine, CentralGround, SideBLine, SideBReserve }
    public enum SectorTerrain : byte { OpenPlain, DryPlain, Scrubland, Forest, RockyGround, Hill, ShallowRiver, Mountain, Other }
    public enum SectorControl : byte { Empty, SideA, SideB, Contested }
    public enum SectorFormationState : byte { Ready, Moving, Routing, Withdrawn, Destroyed }
    public enum SectorFormationRole : byte { Idle, BaseFrontage, Supporting, FlankAttacker, RangedSupport, Moving, Routing }
    public enum SectorPresentationEventType : byte { None, Attack, MovementStarted, MovementCompleted, Routed, Destroyed, Warcry }

    [Serializable]
    public struct SectorCoord : IEquatable<SectorCoord>, IComparable<SectorCoord>
    {
        public BattleLane Lane;
        public BattleDepth Depth;
        public SectorCoord(BattleLane lane, BattleDepth depth) { Lane = lane; Depth = depth; }
        public bool Equals(SectorCoord other) => Lane == other.Lane && Depth == other.Depth;
        public override bool Equals(object obj) => obj is SectorCoord other && Equals(other);
        public override int GetHashCode() => ((int)Lane * 397) ^ (int)Depth;
        public int CompareTo(SectorCoord other) { int lane = Lane.CompareTo(other.Lane); return lane != 0 ? lane : Depth.CompareTo(other.Depth); }
        public override string ToString() => Lane + " / " + Depth;
    }

    [Serializable]
    public sealed class SectorTerrainRule
    {
        public SectorTerrain terrain;
        public int frontageModifier;
        [Min(1)] public int movementCost = 1;
        public bool allowsPhalanx = true;
        public bool allowsForester;
        public bool allowsAquatic;
        public bool allowsMountaineer;
        [Range(0, 200)] public int combatEffectivenessPercent = 100;
    }

    [Serializable]
    public sealed class SectorFormation
    {
        public int Id;
        public int Side;
        public UnitSaveData Source;
        public TileBattleUnitDefinition Combat;
        public int Strength;
        public int Ammunition;
        public int Morale = 1000;
        public int Exhaustion;
        public SectorCoord Sector;
        public SectorFormationState State;
        public bool Moving;
        public SectorCoord MovementTarget;
        public int MovementTicksRemaining;
        public int MovementTicksTotal;
        public int ArrivalTick = -1;
        public int WarcryTicks;
        public readonly HashSet<int> OpeningVolleyTargets = new HashSet<int>();
        public bool Active => Strength > 0 && State != SectorFormationState.Destroyed &&
            State != SectorFormationState.Withdrawn && State != SectorFormationState.Routing;
        public bool Ranged => Combat != null && Combat.Ranged;
    }

    [Serializable]
    public sealed class SectorState
    {
        public SectorCoord Coordinate;
        public SectorTerrain Terrain;
        public readonly List<SectorFormation> Formations = new List<SectorFormation>();
    }

    [Serializable]
    public sealed class SectorCombatEvent
    {
        public int Tick;
        public SectorPresentationEventType Type;
        public int FormationId = -1;
        public int TargetFormationId = -1;
        public int Damage;
        public SectorCoord Sector;
        public string Message;
        public override string ToString() => !string.IsNullOrEmpty(Message) ? "T" + Tick + " " + Message :
            "T" + Tick + " " + Type + " " + FormationId + (TargetFormationId >= 0 ? " -> " + TargetFormationId : string.Empty);
    }

    [Serializable]
    public sealed class SectorFormationPresentationState
    {
        public int FormationId;
        public int Side;
        public UnitSaveData Unit;
        public string DisplayName;
        public SectorCoord Sector;
        public SectorCoord TargetSector;
        public SectorFormationRole Role;
        public SectorFormationState State;
        public int Strength;
        public int MaximumStrength;
        public int Morale;
        public int Exhaustion;
        public int MovementTicksRemaining;
        public int MovementTicksTotal;
        public float MovementProgress;
    }

    [Serializable]
    public sealed class SectorDebugState
    {
        public SectorCoord Coordinate;
        public SectorTerrain Terrain;
        public SectorControl Control;
        public int BaseFrontage;
        public readonly List<int> SideA = new List<int>();
        public readonly List<int> SideB = new List<int>();
        public readonly List<int> ActiveA = new List<int>();
        public readonly List<int> ActiveB = new List<int>();
        public readonly List<int> ReserveA = new List<int>();
        public readonly List<int> ReserveB = new List<int>();
        public readonly List<string> FlankAttackers = new List<string>();
        public int FlankFrontageA;
        public int FlankFrontageB;
        public readonly List<SectorCoord> FlankSourcesA = new List<SectorCoord>();
        public readonly List<SectorCoord> FlankSourcesB = new List<SectorCoord>();
        public readonly List<string> RangedSupporters = new List<string>();
    }

    [Serializable]
    public sealed class SectorReplayFrame
    {
        public int Tick;
        public List<SectorFormationPresentationState> Formations;
        public List<SectorDebugState> Sectors;
    }
}
