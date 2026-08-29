using System;
using System.Collections.Generic;

namespace ProjectX.TileBattle
{
    public enum TileBattleSide { Left = 0, Right = 1 }
    public enum TileFacing { North, East, South, West }
    public enum TileTerrain { Open, Forest, Rough, Hill, Marsh, Water }
    public enum TileBattlePhase { Vanguard, MainDeployment, MainBattle, Reserves, Decisive, Finished }
    public enum TileBattlePlan { AttackCentre, Hold, FlankLeft, FlankRight }
    public enum TileUnitState { Ready, Engaged, Routing, Withdrawn, Destroyed }
    public enum TileWeaponControl { Sword, Spear, Pike, Ranged }
    public enum TileFormationType { None, Phalanx, Shieldwall, Testudo, CavalryCharge }
    public enum TileActionType { Wait, Move, Charge, Turn, Attack, Disengage, Brace }
    public enum TileBattleEventType
    {
        RoundStarted, PlanChosen, OrderIssued, ActionStarted, UnitMoved, UnitTurned,
        UnitAttacked, UnitPushed, UnitEngaged, UnitDisengaged, UnitBlocked,
        ChargeStarted, ChargeEnded, ChargeImpact,
        UnitDamaged, ProjectileLaunched, UnitDeployed, ReserveCommitted,
        UnitRouted, UnitWithdrawn, RoundEnded, BattleEnded
    }

    [Serializable]
    public struct TileCoord : IEquatable<TileCoord>, IComparable<TileCoord>
    {
        public int X;
        public int Y;
        public TileCoord(int x, int y) { X = x; Y = y; }
        public int ManhattanDistance(TileCoord other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        public bool Equals(TileCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is TileCoord other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public int CompareTo(TileCoord other) { int x = X.CompareTo(other.X); return x != 0 ? x : Y.CompareTo(other.Y); }
        public static bool operator ==(TileCoord a, TileCoord b) => a.Equals(b);
        public static bool operator !=(TileCoord a, TileCoord b) => !a.Equals(b);
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    [Serializable]
    public sealed class TileBattleRules
    {
        public const int DefaultTicksPerSecond = 10;
        public int TicksPerSecond = DefaultTicksPerSecond;
        public int Width = 20;
        public int Height = 20;
        public int MinimumResolutionTicks = 16;
        public int SimilarMassPermille = 1250;
        public int OverwhelmingMassPermille = 2000;
        public int PushCohesionDamage = 12;
        public int BreakthroughCohesionDamage = 30;
        public int PushHealthDamage = 4;
        public int PushMoraleDamage = 35;
        public int OverwhelmingPushHealthDamage = 10;
        public int OverwhelmingPushMoraleDamage = 90;
        public int BlockedPushHealthDamage = 14;
        public int BlockedPushMoraleDamage = 120;
        public int ChargeDamagePermillePerMomentum = 200;
        public int FormationMassPermille = 1250;
        public int PhalanxPushPermille = 1400;
        public int ShieldwallShieldPermille = 1250;
        public int TestudoShieldPermille = 1500;
        public int TestudoCoverageBonusPercent = 25;
        public int CavalryFormationChargePermille = 1400;
        public int ForestMoveIntervalPermille = 1500;
        public int ForestAttackMassPermille = 500;
        public int ForestDefenceMassPermille = 1500;
        public int ForestIncomingRangedDamagePermille = 500;
        public int ForesterMassPermille = 1500;
        public int HillPushPermille = 1250;
        public int UphillPushPermille = 750;
        public int HillRangedDamagePermille = 1250;
        public int HillRangedRangeBonus = 1;
        public int BaseMeleeDamage = 20;
        public int FlankDamagePermille = 1400;
        public int RearDamagePermille = 1750;
        public int BraceMassPermille = 1400;
        public int ThreatInterceptionDamagePermille = 500;
        public int RoutMoraleThreshold = 150;
        public int CollapsePercent = 70;
        public int VanguardRounds = 3;
        public int MainBattleRounds = 4;
        public int ReserveRound = 7;
        public int SafetyMaximumRounds = 100;
    }

    [Serializable]
    public sealed class TileBattleCell
    {
        public TileCoord Coordinate;
        public TileTerrain Terrain;
        public int Elevation;
        public int MovementCost = 1;
        public int OccupantUnitId = -1;
    }

    [Serializable]
    public sealed class TileBattleGrid
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        private readonly TileBattleCell[] cells;

        public TileBattleGrid(int width, int height)
        {
            if (width < 4 || height < 4) throw new ArgumentOutOfRangeException("Battle grid must be at least 4x4.");
            Width = width; Height = height; cells = new TileBattleCell[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) cells[Index(new TileCoord(x, y))] = new TileBattleCell { Coordinate = new TileCoord(x, y) };
        }

        public bool Contains(TileCoord coordinate) => coordinate.X >= 0 && coordinate.Y >= 0 && coordinate.X < Width && coordinate.Y < Height;
        public TileBattleCell Get(TileCoord coordinate) => Contains(coordinate) ? cells[Index(coordinate)] : null;
        public int OccupantAt(TileCoord coordinate) { TileBattleCell cell = Get(coordinate); return cell != null ? cell.OccupantUnitId : -1; }
        public void SetOccupant(TileCoord coordinate, int unitId) { TileBattleCell cell = Get(coordinate); if (cell != null) cell.OccupantUnitId = unitId; }
        public void SetTerrain(TileCoord coordinate, TileTerrain terrain, int movementCost = 1, int elevation = 0)
        { TileBattleCell cell = Get(coordinate); if (cell == null) return; cell.Terrain = terrain; cell.MovementCost = Math.Max(1, movementCost); cell.Elevation = elevation; }
        private int Index(TileCoord coordinate) => coordinate.Y * Width + coordinate.X;
    }

    [Serializable]
    public sealed class TileBattleUnitDefinition
    {
        public string Id;
        public string DisplayName;
        public int ReactionTime = 7;
        public int Initiative { get => ReactionTime; set => ReactionTime = value; }
        public int Actions = 2;
        public int BaseMass = 100;
        public int Strength = 100;
        public int MeleeDamage = 20;
        public int MeleeRange = 1;
        public MeleeReachPattern MeleeReachPattern = MeleeReachPattern.Standard;
        public int MeleeAttackIntervalTicks = 1;
        public int ArmorPercent;
        public int ShieldPercent;
        public int ShieldFrontEffectivenessPercent = 100;
        public int ShieldSideEffectivenessPercent;
        public int FrontThreat = 1;
        public int SideThreat;
        public TileWeaponControl WeaponControl = TileWeaponControl.Sword;
        public bool Cavalry;
        public bool Ranged;
        // A one-use ranged capability that does not change the formation's melee tactical role.
        public bool OpeningThrowable;
        public int RangedRange;
        public int RangedDamage;
        public int RangedAttackIntervalTicks = 1;
        public int Ammunition;
        public TileFormationType FormationType;
        public bool ForestImmune;
        public bool Forester;
        public bool RetainsMomentum;
    }

    [Serializable]
    public sealed class TileBattleUnit
    {
        public int Id;
        public int Side;
        public TileBattleUnitDefinition Definition;
        public TileCoord Position;
        public TileFacing Facing;
        public TileUnitState State = TileUnitState.Ready;
        public int Strength;
        public int Ammunition = -1;
        public int Morale = 1000;
        public int Cohesion = 1000;
        public bool Braced;
        public bool IsVanguard;
        public bool IsReserve;
        public int DeploymentRound;
        public bool Deployed = true;
        public int ActionsRemaining;
        public int NextActionTick;
        // Shared active-weapon clock. Switching between ranged and backup melee resets it.
        public int WeaponAttackProgressTicks;
        public bool UsingRangedWeapon;
        public bool ChargeActive;
        public int ChargeMomentum;
        public int ChargeTargetUnitId = -1;
        public TileCoord ChargeTarget;
        public bool HoldPosition;
        public bool SuppressAutomaticAttacks;
        // The general's persistent objective; local weapon targeting may select an interceptor instead.
        public int AttackOrderTargetUnitId = -1;
        public TileUnitOrder CurrentOrder;
        public readonly List<TileUnitAction> QueuedActions = new List<TileUnitAction>();

        public bool Active => Deployed && State != TileUnitState.Destroyed && State != TileUnitState.Withdrawn && Strength > 0;
        public int EffectiveMass(TileBattleRules rules, TileFacing collisionDirection)
        {
            int mass = Definition.BaseMass * Math.Max(250, Cohesion) / 1000;
            if (Braced && Facing == collisionDirection) mass = mass * rules.BraceMassPermille / 1000;
            return Math.Max(1, mass);
        }
    }

    [Serializable]
    public sealed class TileUnitAction
    {
        public TileActionType Type;
        public TileCoord Target;
        public int TargetUnitId = -1;
        public TileFacing Facing;
        public int IntervalPermille = 1000;
        public static TileUnitAction Move(TileCoord target) => new TileUnitAction { Type = TileActionType.Move, Target = target };
        public static TileUnitAction Charge(TileCoord target, int targetUnitId = -1) => new TileUnitAction
            { Type = TileActionType.Charge, Target = target, TargetUnitId = targetUnitId };
        public static TileUnitAction Attack(TileCoord target) => new TileUnitAction { Type = TileActionType.Attack, Target = target };
        public static TileUnitAction Attack(int targetUnitId, TileCoord lastKnownPosition) => new TileUnitAction
            { Type = TileActionType.Attack, Target = lastKnownPosition, TargetUnitId = targetUnitId };
        public static TileUnitAction Turn(TileFacing facing) => new TileUnitAction { Type = TileActionType.Turn, Facing = facing };
        public static TileUnitAction Disengage() => new TileUnitAction { Type = TileActionType.Disengage, IntervalPermille = 1500 };
        public static TileUnitAction Brace() => new TileUnitAction { Type = TileActionType.Brace };
        public static TileUnitAction Wait() => new TileUnitAction { Type = TileActionType.Wait };
    }

    [Serializable]
    public sealed class TileUnitOrder
    {
        public int UnitId;
        public string Purpose;
        public bool SuppressAutomaticAttacks;
        public readonly List<TileUnitAction> Actions = new List<TileUnitAction>();
    }

    [Serializable]
    public sealed class TileOrderSet
    {
        public int Side;
        public int CommandRound;
        public TileBattlePlan Plan;
        public string Reason;
        public readonly List<TileUnitOrder> Orders = new List<TileUnitOrder>();
    }

    [Serializable]
    public sealed class TilePlanScore
    {
        public TileBattlePlan Plan;
        public int BaseScore;
        public int PersonalityInfluence;
        public int SituationInfluence;
        public int Total => BaseScore + PersonalityInfluence + SituationInfluence;
        public string Reason;
    }

    [Serializable]
    public sealed class TileGeneralPersonality
    {
        public string Name = "General";
        public int Bold;
        public int Cautious;
        public int Patient;
        public int Aggressive;
        public int Methodical;
        public int Opportunistic;
        public int CavalryMinded;
        public int Defensive;
        public int Stubborn;
        public int Competence = 50;
    }

    [Serializable]
    public sealed class TileGeneralDebugState
    {
        public string GeneralName;
        public TileBattlePlan CurrentPlan;
        public string ChangeReason;
        public int OwnStrength;
        public int EnemyStrength;
        public int StrengthTrend;
        public int PlanAge;
        public string Assessment;
        public readonly List<TilePlanScore> PlansConsidered = new List<TilePlanScore>();
        public readonly List<string> Threats = new List<string>();
        public readonly List<string> Opportunities = new List<string>();
        public readonly List<string> OrdersIssued = new List<string>();
    }

    [Serializable]
    public sealed class TileBattleEvent
    {
        public int CommandRound;
        public int Tick;
        public TileBattleEventType Type;
        public int UnitId = -1;
        public int OtherUnitId = -1;
        public TileCoord From;
        public TileCoord To;
        public int Amount;
        // True when this attack consumed ammunition and used the primary ranged weapon;
        // false means the backup melee weapon was used.
        public bool RangedAttack;
        public string Message;
        public override string ToString() => "R" + CommandRound + " T" + Tick + " " + Type + ": " + Message;
    }

    [Serializable]
    public sealed class TileBattleResult
    {
        public bool Finished;
        public int WinningSide = -1;
        public int CommandRounds;
        public string EndReason;
        public readonly Dictionary<int, int> RemainingStrength = new Dictionary<int, int>();
        // Campaign-level formations restored to the victorious army after battlefield casualties were applied.
        public readonly Dictionary<int, int> RecoveredFormations = new Dictionary<int, int>();
    }

    [Serializable]
    public sealed class TileBattleUnitViewState
    {
        public int Id, Side, Strength, Morale, Cohesion, Ammunition;
        public int WeaponAttackProgressTicks, AttackOrderTargetUnitId;
        public TileCoord Position;
        public TileFacing Facing;
        public TileUnitState State;
        public bool Deployed;
        public bool UsingRangedWeapon, ChargeActive, HoldPosition, SuppressAutomaticAttacks;
        public int ChargeMomentum, ChargeTargetUnitId;
        public TileCoord ChargeTarget;
    }

    [Serializable]
    public sealed class TileBattleRoundSnapshot
    {
        public int CommandRound;
        public int ResolutionTick;
        public TileBattlePhase Phase;
        public int EventCount;
        public ulong StateHash;
        public readonly List<TileBattleUnitViewState> Units = new List<TileBattleUnitViewState>();
    }
}
