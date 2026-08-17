using System;
using System.Collections.Generic;

namespace ProjectX.DeterministicBattle
{
    [Serializable]
    public sealed class BattleSnapshot
    {
        public string BattleId;
        public int Tick;
        public BattleStatus Status;
        public int WinningSide;
        public ulong StateHash;
        public int SourceTick;
        public readonly List<FormationSnapshot> Formations = new List<FormationSnapshot>();
        public readonly List<CombatantSnapshot> Combatants = new List<CombatantSnapshot>();
        public readonly List<ProjectileSnapshot> Projectiles = new List<ProjectileSnapshot>();
        public readonly List<TerrainSnapshot> Terrain = new List<TerrainSnapshot>();
        public readonly List<EffectSnapshot> Effects = new List<EffectSnapshot>();
    }

    [Serializable] public sealed class FormationSnapshot
    {
        public int Id, Side, DefinitionId, Morale, Cohesion, TargetFormationId, Frontage, Depth, TerrainAreaId;
        public Int2 Position, Facing;
        public FormationStatus Status;
        public FormationOrder Order;
        public bool Pursuing;
    }
    [Serializable] public sealed class CombatantSnapshot
    {
        public int Id, FormationId, DefinitionId, Health, NextAttackTick;
        public Int2 Position;
        public bool Alive;
    }
    [Serializable] public sealed class ProjectileSnapshot
    {
        public int Id, Side, SourceCombatantId, TargetCombatantId;
        public Int2 Position;
        public bool Active;
    }
    [Serializable] public sealed class TerrainSnapshot
    {
        public int Id, RadiusMilli;
        public BattleTerrainKind Kind;
        public Int2 Center;
        public bool Impassable;
    }
    [Serializable] public sealed class EffectSnapshot
    {
        public int Id, FormationId, EndTick;
        public BattleAbilityType Ability;
    }
}
