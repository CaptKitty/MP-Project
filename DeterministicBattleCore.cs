using System;
using System.Collections.Generic;

namespace ProjectX.DeterministicBattle
{
    public enum BattleUnitRole : byte { Infantry, Ranged, Cavalry }
    public enum BattleTerrainKind : byte { Open, Hill, Forest, Rough, Road, River, Impassable }
    public enum BattleGeneralTrait : byte { Balanced, Aggressive, Defensive, CavalryCommander, Cautious, Opportunistic }
    public enum BattleAbilityType : byte { Rally, ForcedMarch, ShieldWall, LooseFormation, FocusedVolley, CoordinatedCharge }
    [Serializable]
    public struct Int2
    {
        public int X, Y;
        public Int2(int x, int y) { X = x; Y = y; }
        public static Int2 operator +(Int2 a, Int2 b) => new Int2(a.X + b.X, a.Y + b.Y);
        public static Int2 operator -(Int2 a, Int2 b) => new Int2(a.X - b.X, a.Y - b.Y);
        public long SqrMagnitude => (long)X * X + (long)Y * Y;
    }

    public sealed class DeterministicRng
    {
        public ulong State { get; private set; }
        public DeterministicRng(ulong seed) { State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
        public uint NextUInt()
        {
            ulong x = State;
            x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
            State = x;
            return (uint)((x * 2685821657736338717UL) >> 32);
        }
        public int Range(int minimum, int maximumExclusive)
        {
            if (maximumExclusive <= minimum) return minimum;
            return minimum + (int)(NextUInt() % (uint)(maximumExclusive - minimum));
        }
    }

    [Serializable]
    public sealed class BattleUnitDefinition
    {
        public int DefinitionId;
        public string UnitName;
        public int MembersPerCampaignUnit;
        public int HealthPerMember;
        public int SpeedMilliPerTick;
        public int MeleeDamage;
        public int MeleeReachMilli;
        public int AttackCooldownTicks;
        public int ArmorPercent;
        public int ShieldPercent;
        public bool HasRangedWeapon;
        public int RangedDamage;
        public int RangedReachMilli;
        public int RangedCooldownTicks;
        public int ProjectileSpeedMilliPerTick;
        public int AmmunitionPerCombatant;
        public BattleUnitRole Role;
        public int Mass;
        public int ChargeDamage;
        public int ChargeSpeedMultiplier;
        public int MinimumChargeDistanceMilli;
        public int ChargeCooldownTicks;
        public int TurnRateMilli;
        public bool Disciplined;
        public bool ForestryImmune;
        public bool Forester;
        public bool FormationTerrainPenalty;
        public int PreferredFrontage;
    }

    [Serializable]
    public sealed class BattleFormationStart
    {
        public int FormationId;
        public int Side;
        public int DefinitionId;
        public int CampaignUnitCount;
        public Int2 Position;
        public Int2 Facing;
        public bool Reserve;
        public FormationOrder InitialOrder;
    }

    [Serializable]
    public sealed class BattleStartState
    {
        public string BattleId;
        public ulong Seed;
        public int TickRate = 10;
        public int MaximumTicks = 2000;
        public string SideAArmyId;
        public string SideBArmyId;
        public string BattlefieldSource;
        public string TerrainArchetype;
        public List<BattleUnitDefinition> Definitions = new List<BattleUnitDefinition>();
        public List<BattleFormationStart> Formations = new List<BattleFormationStart>();
        public List<BattleTerrainArea> Terrain = new List<BattleTerrainArea>();
        public List<BattleGeneralProfile> Generals = new List<BattleGeneralProfile>();
    }

    [Serializable]
    public sealed class BattleGeneralProfile
    {
        public int Side;
        public string Name;
        public BattleGeneralTrait Trait;
        public int CommandIntervalTicks = 50;
        public int MoraleAura = 2;
        public int AbilityCooldownTicks = 200;
    }

    [Serializable]
    public sealed class BattleTerrainArea
    {
        public int Id;
        public BattleTerrainKind Kind;
        public Int2 Center;
        public int RadiusMilli;
        public int MovementPermille = 1000;
        public int ChargePermille = 1000;
        public int RangedAccuracyPermille = 1000;
        public int VisibilityPermille = 1000;
        public int DefenseBonusPercent;
        public bool Impassable;
    }

    public enum FormationStatus : byte { Advancing, Charging, Engaged, Wavering, Routing, Destroyed }
    public enum FormationOrder : byte { Advance, Hold, FlankLeft, FlankRight, Reserve, Withdraw }
    public enum BattleStatus : byte { Deploying, Fighting, Finished }
    [Serializable] public sealed class BattleTelemetry
    {
        public int Charges, FlankAttacks, RearAttacks, MeleeAttacks, ProjectilesLaunched, ProjectileHits;
        public int AbilitiesUsed, ReservesReleased, RoutedFormations;
    }

    [Serializable]
    public sealed class SimCombatant
    {
        public int Id;
        public int FormationId;
        public int DefinitionId;
        public int Health;
        public Int2 Position;
        public int NextAttackTick;
        public int Ammunition;
        public bool Alive = true;
    }

    [Serializable]
    public sealed class SimProjectile
    {
        public int Id;
        public int Side;
        public int SourceCombatantId;
        public int TargetCombatantId;
        public Int2 Position;
        public int SpeedMilliPerTick;
        public int Damage;
        public int RemainingTicks;
        public int AccuracyPermille = 1000;
        public bool Active = true;
    }

    [Serializable]
    public sealed class SimFormation
    {
        public int Id;
        public int Side;
        public int DefinitionId;
        public int StartingCampaignUnits;
        public Int2 Position;
        public Int2 Facing;
        public int Morale = 1000;
        public int Cohesion = 1000;
        public FormationStatus Status;
        public int ChargeStartTick;
        public int NextChargeTick;
        public FormationOrder Order;
        public int OrderLockedUntilTick;
        public int TargetFormationId = -1;
        public int Frontage;
        public int Depth;
        public bool Pursuing;
        public int TerrainAreaId = -1;
        public bool RoutingCounted;
        public int FlankStage;
        public int FlankTargetId = -1;
        public int FlankBlockedTicks;
        public readonly List<int> CombatantIds = new List<int>();
    }

    [Serializable]
    public sealed class SimBattleEffect
    {
        public int Id;
        public int FormationId;
        public BattleAbilityType Ability;
        public int StartTick;
        public int EndTick;
        public int MovementPermille = 1000;
        public int DamagePermille = 1000;
        public int DefenseBonusPercent;
        public int CohesionPerTick;
        public int MoralePerTick;
    }

    [Serializable]
    public sealed class SimGeneral
    {
        public int Side;
        public string Name;
        public BattleGeneralTrait Trait;
        public int CommandIntervalTicks;
        public int MoraleAura;
        public int AbilityCooldownTicks;
        public int NextDecisionTick;
        public readonly int[] NextAbilityTicks = new int[6];
        public string LastDecision;
    }

    public interface IBattleObserver
    {
        void OnBattleTick(BattleSimulation simulation);
        void OnBattleFinished(BattleSimulation simulation);
    }

    public interface IBattleCommand
    {
        int Tick { get; }
        void Apply(BattleSimulation simulation);
    }

    [Serializable]
    public sealed class BattleCommandRecord
    {
        public int Tick, FormationId, Side, LockDurationTicks;
        public FormationOrder Order;
        public BattleAbilityType Ability;
        public bool IsAbility;
    }

    public sealed class ReinforcementCommand : IBattleCommand
    {
        public int Tick { get; set; }
        public readonly List<BattleUnitDefinition> Definitions = new List<BattleUnitDefinition>();
        public readonly List<BattleFormationStart> Formations = new List<BattleFormationStart>();
        public void Apply(BattleSimulation simulation)
        {
            for (int i = 0; i < Definitions.Count; i++) simulation.AddDefinition(Definitions[i]);
            for (int i = 0; i < Formations.Count; i++) simulation.AddFormation(Formations[i]);
        }
    }

    public sealed class FormationOrderCommand : IBattleCommand
    {
        public int Tick { get; set; }
        public int FormationId { get; set; }
        public FormationOrder Order { get; set; }
        public int LockDurationTicks { get; set; } = 100;
        public void Apply(BattleSimulation simulation) =>
            simulation.SetFormationOrder(FormationId, Order, Tick + Math.Max(0, LockDurationTicks));
    }

    public sealed class BattleAbilityCommand : IBattleCommand
    {
        public int Tick { get; set; }
        public int Side { get; set; }
        public int FormationId { get; set; }
        public BattleAbilityType Ability { get; set; }
        public void Apply(BattleSimulation simulation) => simulation.TryActivateAbility(Side, FormationId, Ability);
    }

    public sealed class BattleSimulation
    {
        public const int PositionScale = 1000;
        public BattleStartState StartState { get; }
        public int Tick { get; private set; }
        public BattleStatus Status { get; private set; } = BattleStatus.Deploying;
        public int WinningSide { get; private set; } = -1;
        public DeterministicRng Rng { get; }
        public readonly List<SimFormation> Formations = new List<SimFormation>();
        public readonly List<SimCombatant> Combatants = new List<SimCombatant>();
        public readonly List<SimProjectile> Projectiles = new List<SimProjectile>();
        public readonly List<SimBattleEffect> Effects = new List<SimBattleEffect>();
        public readonly List<SimGeneral> Generals = new List<SimGeneral>();
        public readonly BattleTelemetry Telemetry = new BattleTelemetry();
        private readonly List<IBattleCommand> commands = new List<IBattleCommand>();
        public readonly List<BattleCommandRecord> CommandHistory = new List<BattleCommandRecord>();
        private readonly List<IBattleObserver> observers = new List<IBattleObserver>();
        private readonly Dictionary<int, BattleUnitDefinition> definitions = new Dictionary<int, BattleUnitDefinition>();
        private int nextCombatantId;
        private int nextProjectileId;
        private int nextEffectId;
        private int lastCombatActivityTick;
        private readonly List<PendingMeleeHit> pendingMeleeHits = new List<PendingMeleeHit>();
        private struct PendingMeleeHit { public int TargetId, Damage, MoraleDamage, CohesionDamage; }

        public BattleSimulation(BattleStartState startState)
        {
            StartState = startState ?? throw new ArgumentNullException(nameof(startState));
            Rng = new DeterministicRng(startState.Seed);
            for (int i = 0; i < startState.Definitions.Count; i++) definitions.Add(startState.Definitions[i].DefinitionId, startState.Definitions[i]);
            for (int i = 0; i < startState.Generals.Count; i++)
            {
                BattleGeneralProfile profile = startState.Generals[i];
                Generals.Add(new SimGeneral { Side = profile.Side, Name = profile.Name, Trait = profile.Trait,
                    CommandIntervalTicks = Math.Max(10, profile.CommandIntervalTicks), MoraleAura = profile.MoraleAura,
                    AbilityCooldownTicks = Math.Max(1, profile.AbilityCooldownTicks), NextDecisionTick = 1 });
            }
            for (int i = 0; i < startState.Formations.Count; i++) AddFormation(startState.Formations[i]);
            Status = BattleStatus.Fighting;
        }

        public void AttachObserver(IBattleObserver observer) { if (observer != null && !observers.Contains(observer)) observers.Add(observer); }
        public void DetachObserver(IBattleObserver observer) => observers.Remove(observer);
        public void ScheduleCommand(IBattleCommand command)
        {
            if (command == null) return;
            commands.Add(command);
            commands.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            if (command is FormationOrderCommand order) CommandHistory.Add(new BattleCommandRecord { Tick = order.Tick,
                FormationId = order.FormationId, Order = order.Order, LockDurationTicks = order.LockDurationTicks });
            else if (command is BattleAbilityCommand ability) CommandHistory.Add(new BattleCommandRecord { Tick = ability.Tick,
                FormationId = ability.FormationId, Side = ability.Side, Ability = ability.Ability, IsAbility = true });
        }

        public void AddFormation(BattleFormationStart start)
        {
            BattleUnitDefinition definition = definitions[start.DefinitionId];
            SimFormation formation = new SimFormation
            {
                Id = start.FormationId, Side = start.Side, DefinitionId = start.DefinitionId,
                StartingCampaignUnits = start.CampaignUnitCount, Position = start.Position,
                Facing = start.Facing, Status = FormationStatus.Advancing,
                Order = start.Reserve ? FormationOrder.Reserve : start.InitialOrder
            };
            int members = Math.Max(1, start.CampaignUnitCount * definition.MembersPerCampaignUnit);
            int columns = definition.PreferredFrontage > 0 ? Math.Min(members, definition.PreferredFrontage) : IntegerCeilingSqrt(members);
            formation.Frontage = columns;
            formation.Depth = (members + columns - 1) / columns;
            for (int i = 0; i < members; i++)
            {
                int column = i % columns;
                int row = i / columns;
                SimCombatant combatant = new SimCombatant
                {
                    Id = nextCombatantId++, FormationId = formation.Id, DefinitionId = definition.DefinitionId,
                    Health = definition.HealthPerMember,
                    Ammunition = definition.AmmunitionPerCombatant,
                    Position = start.Position + new Int2((column - columns / 2) * 350, row * 350 * (start.Side == 0 ? -1 : 1))
                };
                formation.CombatantIds.Add(combatant.Id);
                Combatants.Add(combatant);
            }
            Formations.Add(formation);
            Formations.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        public void AddDefinition(BattleUnitDefinition definition)
        {
            if (definition == null || definitions.ContainsKey(definition.DefinitionId)) return;
            definitions.Add(definition.DefinitionId, definition);
            if (StartState.Definitions.Find(item => item.DefinitionId == definition.DefinitionId) == null)
                StartState.Definitions.Add(definition);
        }

        public bool SetFormationOrder(int formationId, FormationOrder order, int lockedUntilTick = 0)
        {
            SimFormation formation = Formations.Find(item => item.Id == formationId);
            if (formation == null || formation.Status == FormationStatus.Destroyed) return false;
            formation.Order = order;
            formation.OrderLockedUntilTick = Math.Max(Tick, lockedUntilTick);
            return true;
        }

        public bool TryActivateAbility(int side, int formationId, BattleAbilityType ability)
        {
            SimGeneral general = Generals.Find(item => item.Side == side);
            SimFormation formation = Formations.Find(item => item.Id == formationId && item.Side == side);
            int abilityIndex = (int)ability;
            if (general == null || formation == null || CountLiving(formation) == 0 ||
                general.NextAbilityTicks[abilityIndex] > Tick) return false;
            SimBattleEffect effect = new SimBattleEffect
            {
                Id = nextEffectId++, FormationId = formation.Id, Ability = ability,
                StartTick = Tick, EndTick = Tick + 80
            };
            switch (ability)
            {
                case BattleAbilityType.Rally: formation.Morale = Math.Min(1000, formation.Morale + 180); effect.MoralePerTick = 1; break;
                case BattleAbilityType.ForcedMarch: effect.MovementPermille = 1350; effect.CohesionPerTick = -1; break;
                case BattleAbilityType.ShieldWall: effect.DefenseBonusPercent = 18; effect.MovementPermille = 600; break;
                case BattleAbilityType.LooseFormation: effect.DefenseBonusPercent = 10; effect.DamagePermille = 850; break;
                case BattleAbilityType.FocusedVolley: effect.DamagePermille = 1250; effect.MovementPermille = 700; break;
                case BattleAbilityType.CoordinatedCharge:
                    effect.DamagePermille = 1200; effect.MovementPermille = 1100;
                    formation.NextChargeTick = Tick; formation.Order = FormationOrder.Advance; break;
            }
            Effects.Add(effect);
            Telemetry.AbilitiesUsed++;
            general.NextAbilityTicks[abilityIndex] = Tick + general.AbilityCooldownTicks;
            general.LastDecision = ability + " formation " + formation.Id;
            return true;
        }

        private void UpdateEffects()
        {
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                SimBattleEffect effect = Effects[i];
                if (Tick > effect.EndTick) { Effects.RemoveAt(i); continue; }
                SimFormation formation = Formations.Find(item => item.Id == effect.FormationId);
                if (formation == null) continue;
                formation.Cohesion = Clamp(formation.Cohesion + effect.CohesionPerTick, 0, 1000);
                formation.Morale = Clamp(formation.Morale + effect.MoralePerTick, 0, 1000);
            }
        }

        private void UpdateGeneralAbilities()
        {
            for (int i = 0; i < Generals.Count; i++)
            {
                SimGeneral general = Generals[i];
                if (Tick < general.NextDecisionTick) continue;
                general.NextDecisionTick = Tick + general.CommandIntervalTicks;
                SimFormation rally = null, ranged = null, cavalry = null, infantry = null;
                for (int f = 0; f < Formations.Count; f++)
                {
                    SimFormation formation = Formations[f];
                    if (formation.Side != general.Side || CountLiving(formation) == 0) continue;
                    BattleUnitRole role = definitions[formation.DefinitionId].Role;
                    if (formation.Morale < 500 && (rally == null || formation.Morale < rally.Morale)) rally = formation;
                    if (role == BattleUnitRole.Ranged && ranged == null) ranged = formation;
                    if (role == BattleUnitRole.Cavalry && cavalry == null) cavalry = formation;
                    if (role == BattleUnitRole.Infantry && infantry == null) infantry = formation;
                    formation.Morale = Math.Min(1000, formation.Morale + Math.Max(0, general.MoraleAura));
                }
                if (rally != null && TryActivateAbility(general.Side, rally.Id, BattleAbilityType.Rally)) continue;
                if ((general.Trait == BattleGeneralTrait.CavalryCommander || general.Trait == BattleGeneralTrait.Aggressive) &&
                    cavalry != null && TryActivateAbility(general.Side, cavalry.Id, BattleAbilityType.CoordinatedCharge)) continue;
                if (general.Trait == BattleGeneralTrait.Defensive && infantry != null &&
                    TryActivateAbility(general.Side, infantry.Id, BattleAbilityType.ShieldWall)) continue;
                if (ranged != null && EnemyWithinWeaponRange(ranged))
                    TryActivateAbility(general.Side, ranged.Id, BattleAbilityType.FocusedVolley);
            }
        }

        private SimBattleEffect EffectFor(int formationId)
        {
            SimBattleEffect combined = null;
            for (int i = 0; i < Effects.Count; i++)
            {
                SimBattleEffect effect = Effects[i];
                if (effect.FormationId != formationId || effect.StartTick > Tick || effect.EndTick < Tick) continue;
                if (combined == null) combined = new SimBattleEffect { MovementPermille = 1000, DamagePermille = 1000 };
                combined.MovementPermille = combined.MovementPermille * effect.MovementPermille / 1000;
                combined.DamagePermille = combined.DamagePermille * effect.DamagePermille / 1000;
                combined.DefenseBonusPercent += effect.DefenseBonusPercent;
            }
            return combined;
        }

        private int EffectDamagePermille(int formationId)
        {
            int result = 1000;
            for (int i = 0; i < Effects.Count; i++)
            {
                SimBattleEffect effect = Effects[i];
                if (effect.FormationId == formationId && effect.StartTick <= Tick && effect.EndTick >= Tick)
                    result = result * effect.DamagePermille / 1000;
            }
            return result;
        }

        public void AdvanceTicks(int count)
        {
            for (int i = 0; i < count && Status != BattleStatus.Finished; i++) Step();
        }

        public void Step()
        {
            if (Status == BattleStatus.Finished) return;
            Tick++;
            for (int i = 0; i < commands.Count; i++) if (commands[i].Tick == Tick) commands[i].Apply(this);
            UpdateProjectiles();
            UpdateEffects();
            UpdateGeneralAbilities();
            if (Tick == 1 || Tick % 50 == 0) UpdateGeneralAI();
            pendingMeleeHits.Clear();
            for (int i = 0; i < Formations.Count; i++) UpdateFormation(Formations[i]);
            ApplyPendingMeleeHits();
            ResolveFormationSeparation();
            ResolveVictory();
            for (int i = 0; i < observers.Count; i++) observers[i].OnBattleTick(this);
        }

        private void UpdateFormation(SimFormation formation)
        {
            int living = CountLiving(formation);
            if (living == 0) { formation.Status = FormationStatus.Destroyed; return; }
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            BattleTerrainArea occupiedTerrain = TerrainAt(formation.Position);
            formation.TerrainAreaId = occupiedTerrain != null ? occupiedTerrain.Id : -1;
            RecoverCohesion(formation, definition);
            ReformMembers(formation, definition);
            SimFormation finishingOpportunity = FindFinishingOpportunity(formation);
            if (formation.Order == FormationOrder.Reserve || formation.Order == FormationOrder.Hold &&
                !EnemyWithinWeaponRange(formation) && finishingOpportunity == null)
                return;
            if (formation.Order == FormationOrder.Withdraw)
            {
                if (!formation.RoutingCounted) { formation.RoutingCounted = true; Telemetry.RoutedFormations++; }
                formation.Status = FormationStatus.Routing;
                MoveToward(formation, BattleExit(formation.Side), definition.SpeedMilliPerTick);
                if ((BattleExit(formation.Side) - formation.Position).SqrMagnitude < 1000000L)
                    formation.Status = FormationStatus.Destroyed;
                return;
            }
            SimFormation enemy = finishingOpportunity ?? FindFormationTarget(formation);
            if (enemy == null) return;
            long distanceSquared = (enemy.Position - formation.Position).SqrMagnitude;
            formation.TargetFormationId = enemy.Id;
            formation.Pursuing = enemy.Status == FormationStatus.Routing;
            bool canShoot = definition.HasRangedWeapon && FormationHasAmmunition(formation);
            int preferredReach = canShoot ? definition.RangedReachMilli : definition.MeleeReachMilli;
            int combinedRadius = FormationRadius(formation) + FormationRadius(enemy);
            // Ranged formations use their full footprint. Melee formations must close to the
            // same compressed spacing enforced by collision separation, otherwise formations
            // can report Engaged while every individual member remains outside weapon reach.
            int engageDistance = preferredReach + (canShoot ? combinedRadius : combinedRadius * 55 / 100);
            if (formation.Morale <= 180)
            {
                if (!formation.RoutingCounted) { formation.RoutingCounted = true; Telemetry.RoutedFormations++; }
                formation.Status = FormationStatus.Routing;
                formation.Order = FormationOrder.Withdraw;
                MoveToward(formation, BattleExit(formation.Side), definition.SpeedMilliPerTick);
                return;
            }
            if (finishingOpportunity == null && IsFlankOrder(formation.Order) && formation.FlankStage < 2)
            {
                SimFormation interceptor = FindFlankInterceptor(formation, enemy, engageDistance);
                if (interceptor != null)
                {
                    formation.Order = FormationOrder.Advance; formation.FlankStage = 0; formation.FlankTargetId = -1;
                    enemy = interceptor; distanceSquared = (enemy.Position - formation.Position).SqrMagnitude;
                }
                else
                {
                    formation.Status = FormationStatus.Advancing;
                    Int2 staging = FlankStagingPoint(formation, enemy);
                    long before = (staging - formation.Position).SqrMagnitude;
                    MoveToward(formation, staging, definition.SpeedMilliPerTick);
                    if ((staging - formation.Position).SqrMagnitude <= 2250000L)
                    { formation.FlankStage++; formation.FlankBlockedTicks = 0; }
                    else if ((staging - formation.Position).SqrMagnitude >= before) formation.FlankBlockedTicks++;
                    else formation.FlankBlockedTicks = 0;
                    if (formation.FlankBlockedTicks > 40)
                    { formation.Order = FormationOrder.Advance; formation.FlankStage = 0; formation.FlankTargetId = -1; }
                    return;
                }
            }
            if (CanBeginOrContinueCharge(formation, enemy, definition, distanceSquared))
            {
                formation.Status = FormationStatus.Charging;
                int chargeSpeed = definition.SpeedMilliPerTick * Math.Max(1000, definition.ChargeSpeedMultiplier) / 1000;
                MoveToward(formation, enemy.Position, chargeSpeed);
                if ((enemy.Position - formation.Position).SqrMagnitude <= (long)engageDistance * engageDistance)
                    ResolveMelee(formation, enemy, definition);
            }
            else if (canShoot && distanceSquared <= (long)engageDistance * engageDistance)
            {
                formation.Status = formation.Morale < 400 ? FormationStatus.Wavering : FormationStatus.Engaged;
                int skirmishDistance = Math.Max(1500, definition.RangedReachMilli / 3);
                if (distanceSquared < (long)skirmishDistance * skirmishDistance)
                    MoveAway(formation, enemy.Position, Math.Max(1, definition.SpeedMilliPerTick / 2));
                ResolveRanged(formation, enemy, definition);
            }
            else if (distanceSquared > (long)engageDistance * engageDistance)
            {
                formation.Status = FormationStatus.Advancing;
                int advanceSpeed = finishingOpportunity != null && enemy.Status == FormationStatus.Routing
                    ? definition.SpeedMilliPerTick * 5 / 4 : definition.SpeedMilliPerTick;
                MoveToward(formation, finishingOpportunity != null ? enemy.Position : TacticalMovementTarget(formation, enemy),
                    advanceSpeed);
            }
            else
            {
                formation.Status = formation.Morale < 400 ? FormationStatus.Wavering : FormationStatus.Engaged;
                ResolveMelee(formation, enemy, definition);
            }
        }

        private static bool IsFlankOrder(FormationOrder order) => order == FormationOrder.FlankLeft || order == FormationOrder.FlankRight;

        private SimFormation FindFormationTarget(SimFormation formation)
        {
            if (IsFlankOrder(formation.Order))
            {
                SimFormation existing = Formations.Find(item => item.Id == formation.FlankTargetId && item.Side != formation.Side &&
                    CountLiving(item) > 0 && item.Status != FormationStatus.Routing);
                if (existing == null) existing = Formations.Find(item => item.Id == formation.TargetFormationId &&
                    item.Side != formation.Side && CountLiving(item) > 0 && item.Status != FormationStatus.Routing);
                if (existing != null) return existing;
                existing = FindNearestEnemy(formation);
                formation.FlankTargetId = existing != null ? existing.Id : -1;
                formation.FlankStage = 0; formation.FlankBlockedTicks = 0;
                return existing;
            }
            formation.FlankTargetId = -1; formation.FlankStage = 0;
            SimFormation assigned = Formations.Find(item => item.Id == formation.TargetFormationId &&
                item.Side != formation.Side && CountLiving(item) > 0 && item.Status != FormationStatus.Routing);
            if (assigned != null) return assigned;
            return FindNearestEnemy(formation);
        }

        private Int2 FlankStagingPoint(SimFormation formation, SimFormation enemy)
        {
            int facingLength = Math.Max(1, IntegerSqrt(enemy.Facing.SqrMagnitude));
            Int2 forward = new Int2(enemy.Facing.X * PositionScale / facingLength, enemy.Facing.Y * PositionScale / facingLength);
            Int2 lateral = formation.Order == FormationOrder.FlankLeft
                ? new Int2(-forward.Y, forward.X) : new Int2(forward.Y, -forward.X);
            int lateralDistance = formation.FlankStage == 0 ? 7000 : 4200;
            int rearDistance = formation.FlankStage == 0 ? 1500 : 3600;
            return enemy.Position + new Int2(lateral.X * lateralDistance / PositionScale - forward.X * rearDistance / PositionScale,
                lateral.Y * lateralDistance / PositionScale - forward.Y * rearDistance / PositionScale);
        }

        private SimFormation FindFlankInterceptor(SimFormation formation, SimFormation target, int engageDistance)
        {
            SimFormation result = null; long best = long.MaxValue;
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation candidate = Formations[i];
                if (candidate.Id == target.Id || candidate.Side == formation.Side || CountLiving(candidate) == 0 ||
                    candidate.Status == FormationStatus.Routing) continue;
                long distance = (candidate.Position - formation.Position).SqrMagnitude;
                long interception = (long)(engageDistance + 800) * (engageDistance + 800);
                if (distance <= interception && distance < best) { result = candidate; best = distance; }
            }
            return result;
        }

        private void UpdateGeneralAI()
        {
            for (int side = 0; side <= 1; side++)
            {
                int active = 0;
                for (int i = 0; i < Formations.Count; i++)
                    if (Formations[i].Side == side && Formations[i].Order != FormationOrder.Reserve &&
                        CountLiving(Formations[i]) > 0 && Formations[i].Status != FormationStatus.Routing) active++;
                AssignCoordinatedTargets(side);
                for (int i = 0; i < Formations.Count; i++)
                {
                    SimFormation formation = Formations[i];
                    if (formation.Side != side || formation.OrderLockedUntilTick > Tick || CountLiving(formation) == 0) continue;
                    BattleUnitDefinition definition = definitions[formation.DefinitionId];
                    if (formation.Order == FormationOrder.Reserve && (Tick >= 120 || FrontLineNeedsReserve(side) || SideHasContact(side)))
                    {
                        formation.Order = FormationOrder.Advance;
                        Telemetry.ReservesReleased++;
                        active++;
                    }
                    else if (formation.Order == FormationOrder.Advance && definition.Role == BattleUnitRole.Cavalry)
                        formation.Order = (formation.Id & 1) == 0 ? FormationOrder.FlankLeft : FormationOrder.FlankRight;
                    else if (definition.Role == BattleUnitRole.Infantry && EnemyCavalryThreatensFlank(formation))
                        formation.Order = FormationOrder.Hold;
                    else if (definition.Role == BattleUnitRole.Ranged && TerrainDefense(formation) >= 10)
                        formation.Order = FormationOrder.Hold;
                }
            }
        }

        private void AssignCoordinatedTargets(int side)
        {
            List<SimFormation> enemies = Formations.FindAll(item => item.Side != side && CountLiving(item) > 0 &&
                item.Status != FormationStatus.Routing && item.Status != FormationStatus.Destroyed);
            if (enemies.Count == 0) return;
            enemies.Sort((a, b) => a.Position.X != b.Position.X ? a.Position.X.CompareTo(b.Position.X) : a.Id.CompareTo(b.Id));

            List<SimFormation> infantry = Formations.FindAll(item => item.Side == side && CountLiving(item) > 0 &&
                item.Order != FormationOrder.Reserve && item.Status != FormationStatus.Routing &&
                definitions[item.DefinitionId].Role == BattleUnitRole.Infantry);
            infantry.Sort((a, b) => a.Position.X != b.Position.X ? a.Position.X.CompareTo(b.Position.X) : a.Id.CompareTo(b.Id));
            for (int i = 0; i < infantry.Count; i++)
            {
                int targetIndex = infantry.Count == 1 ? enemies.Count / 2 : i * (enemies.Count - 1) / (infantry.Count - 1);
                infantry[i].TargetFormationId = enemies[targetIndex].Id;
            }

            List<SimFormation> specialists = Formations.FindAll(item => item.Side == side && CountLiving(item) > 0 &&
                item.Order != FormationOrder.Reserve && item.Status != FormationStatus.Routing &&
                definitions[item.DefinitionId].Role != BattleUnitRole.Infantry);
            specialists.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < specialists.Count; i++)
            {
                SimFormation formation = specialists[i];
                BattleUnitRole role = definitions[formation.DefinitionId].Role;
                SimFormation best = null; long bestScore = long.MinValue;
                for (int e = 0; e < enemies.Count; e++)
                {
                    SimFormation enemy = enemies[e];
                    int pins = CountAlliedFormationsTargeting(side, enemy.Id, BattleUnitRole.Infantry);
                    long distance = (enemy.Position - formation.Position).SqrMagnitude;
                    long score = pins * 30000000L - distance;
                    if (role == BattleUnitRole.Ranged && enemy.Status == FormationStatus.Engaged) score += 20000000L;
                    if (role == BattleUnitRole.Cavalry && definitions[enemy.DefinitionId].Role == BattleUnitRole.Ranged) score += 12000000L;
                    if (enemy.Morale < 500 || enemy.Cohesion < 450) score += 8000000L;
                    if (score > bestScore || score == bestScore && (best == null || enemy.Id < best.Id))
                    { best = enemy; bestScore = score; }
                }
                if (best != null)
                {
                    formation.TargetFormationId = best.Id;
                    if (role == BattleUnitRole.Cavalry) formation.FlankTargetId = best.Id;
                }
            }
        }

        private int CountAlliedFormationsTargeting(int side, int targetId, BattleUnitRole? role = null)
        {
            int count = 0;
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation ally = Formations[i];
                if (ally.Side != side || ally.TargetFormationId != targetId || CountLiving(ally) == 0 ||
                    ally.Status == FormationStatus.Routing || role.HasValue && definitions[ally.DefinitionId].Role != role.Value) continue;
                count++;
            }
            return count;
        }

        private bool EnemyCavalryThreatensFlank(SimFormation formation)
        {
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation enemy = Formations[i];
                if (enemy.Side == formation.Side || CountLiving(enemy) == 0 ||
                    definitions[enemy.DefinitionId].Role != BattleUnitRole.Cavalry) continue;
                Int2 offset = enemy.Position - formation.Position;
                if (Math.Abs(offset.X) > 2500 && offset.SqrMagnitude < 64000000L) return true;
            }
            return false;
        }

        private bool FrontLineNeedsReserve(int side)
        {
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation formation = Formations[i];
                if (formation.Side == side && formation.Order != FormationOrder.Reserve && CountLiving(formation) > 0 &&
                    (formation.Morale < 500 || formation.Cohesion < 450 || formation.Status == FormationStatus.Routing))
                    return true;
            }
            return false;
        }

        private bool SideHasContact(int side)
        {
            for (int i = 0; i < Formations.Count; i++)
                if (Formations[i].Side == side && (Formations[i].Status == FormationStatus.Engaged ||
                    Formations[i].Status == FormationStatus.Charging || Formations[i].Status == FormationStatus.Wavering)) return true;
            return false;
        }

        private bool EnemyWithinWeaponRange(SimFormation formation)
        {
            SimFormation enemy = FindNearestEnemy(formation);
            if (enemy == null) return false;
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            int reach = (definition.HasRangedWeapon ? definition.RangedReachMilli : definition.MeleeReachMilli) +
                FormationRadius(formation) + FormationRadius(enemy);
            return (enemy.Position - formation.Position).SqrMagnitude <= (long)reach * reach;
        }

        private Int2 TacticalMovementTarget(SimFormation formation, SimFormation enemy)
        {
            if (formation.Order != FormationOrder.FlankLeft && formation.Order != FormationOrder.FlankRight)
            {
                BattleTerrainArea useful = BestNearbyDefensiveTerrain(formation);
                return useful != null ? DefensiveTerrainSlot(useful, formation) : enemy.Position;
            }
            if (formation.FlankStage >= 2) return enemy.Position;
            int lateral = formation.Order == FormationOrder.FlankLeft ? -6000 : 6000;
            int rearward = formation.Side == 0 ? 2500 : -2500;
            Int2 waypoint = new Int2(enemy.Position.X + lateral, enemy.Position.Y + rearward);
            return (waypoint - formation.Position).SqrMagnitude < 2250000L ? enemy.Position : waypoint;
        }

        private BattleTerrainArea BestNearbyDefensiveTerrain(SimFormation formation)
        {
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            if (definition.Role == BattleUnitRole.Cavalry) return null;
            if (Tick > 300 && Tick - lastCombatActivityTick > 300) return null;
            BattleTerrainArea best = null; long bestScore = long.MinValue;
            for (int i = 0; i < StartState.Terrain.Count; i++)
            {
                BattleTerrainArea area = StartState.Terrain[i];
                if (area.Impassable || area.DefenseBonusPercent <= 0) continue;
                if ((area.Center - formation.Position).SqrMagnitude <= (long)area.RadiusMilli * area.RadiusMilli) continue;
                long distance = (area.Center - formation.Position).SqrMagnitude;
                if (distance > 100000000L) continue;
                int occupants = 0;
                for (int f = 0; f < Formations.Count; f++)
                {
                    SimFormation ally = Formations[f];
                    if (ally.Id == formation.Id || ally.Side != formation.Side || CountLiving(ally) == 0) continue;
                    if ((ally.Position - area.Center).SqrMagnitude <= (long)area.RadiusMilli * area.RadiusMilli) occupants++;
                }
                int capacity = Math.Max(1, area.RadiusMilli / 1800);
                if (occupants >= capacity) continue;
                Int2 slot = DefensiveTerrainSlot(area, formation);
                long detour = (slot - formation.Position).SqrMagnitude;
                SimFormation enemy = FindNearestEnemy(formation);
                long enemyDistance = enemy != null ? (enemy.Position - formation.Position).SqrMagnitude : long.MaxValue;
                // Do not turn away from a materially closer enemy to chase a modest bonus.
                if (enemyDistance < 36000000L && detour > enemyDistance * 3 / 2) continue;
                long score = (long)area.DefenseBonusPercent * 1000000 - distance / 100 - occupants * 6000000L;
                if (score > bestScore || score == bestScore && (best == null || area.Id < best.Id))
                { best = area; bestScore = score; }
            }
            return best;
        }

        private static Int2 DefensiveTerrainSlot(BattleTerrainArea area, SimFormation formation)
        {
            // Stable distributed positions stop every formation targeting the terrain icon's
            // exact center. Eight directions keep this integer-only and deterministic.
            Int2[] directions =
            {
                new Int2(1000, 0), new Int2(707, 707), new Int2(0, 1000), new Int2(-707, 707),
                new Int2(-1000, 0), new Int2(-707, -707), new Int2(0, -1000), new Int2(707, -707)
            };
            int slotIndex = Math.Abs(formation.Id * 31 + area.Id * 17) % directions.Length;
            Int2 direction = directions[slotIndex];
            int radius = Math.Max(300, area.RadiusMilli * 55 / 100);
            return area.Center + new Int2(direction.X * radius / PositionScale, direction.Y * radius / PositionScale);
        }

        private Int2 EnemyCenter(int side)
        {
            long x = 0, y = 0; int count = 0;
            for (int i = 0; i < Formations.Count; i++)
                if (Formations[i].Side != side && CountLiving(Formations[i]) > 0)
                { x += Formations[i].Position.X; y += Formations[i].Position.Y; count++; }
            return count == 0 ? new Int2(0, side == 0 ? 1000 : -1000) : new Int2((int)(x / count), (int)(y / count));
        }

        private static Int2 BattleExit(int side) => new Int2(0, side == 0 ? -30000 : 30000);

        private void RecoverCohesion(SimFormation formation, BattleUnitDefinition definition)
        {
            if (formation.Status == FormationStatus.Engaged || formation.Status == FormationStatus.Charging ||
                formation.Status == FormationStatus.Routing) return;
            int recovery = definition.Disciplined ? 3 : 1;
            if (formation.Order == FormationOrder.Hold || formation.Order == FormationOrder.Reserve) recovery++;
            formation.Cohesion = Math.Min(1000, formation.Cohesion + recovery);
            if (formation.Morale < 1000 && formation.Cohesion > 650) formation.Morale++;
        }

        private void ReformMembers(SimFormation formation, BattleUnitDefinition definition)
        {
            int living = CountLiving(formation);
            if (living <= 0) return;
            int frontage = Math.Max(1, Math.Min(formation.Frontage, living));
            formation.Depth = (living + frontage - 1) / frontage;
            Int2 forward = formation.Facing.SqrMagnitude > 0 ? formation.Facing : new Int2(0, formation.Side == 0 ? PositionScale : -PositionScale);
            int forwardLength = Math.Max(1, IntegerSqrt(forward.SqrMagnitude));
            forward = new Int2(forward.X * PositionScale / forwardLength, forward.Y * PositionScale / forwardLength);
            Int2 right = new Int2(forward.Y, -forward.X);
            bool inContact = formation.Status == FormationStatus.Engaged || formation.Status == FormationStatus.Wavering ||
                formation.Status == FormationStatus.Charging;
            int aliveIndex = 0;
            int reformSpeed = Math.Max(35, definition.SpeedMilliPerTick / (inContact ? 3 : 2));
            for (int i = 0; i < formation.CombatantIds.Count; i++)
            {
                SimCombatant member = Combatants[formation.CombatantIds[i]];
                if (!member.Alive) continue;
                int column = aliveIndex % frontage;
                int row = aliveIndex / frontage;
                int lateral = (column * 2 - (frontage - 1)) * 175;
                Int2 target = formation.Position + new Int2(right.X * lateral / PositionScale, right.Y * lateral / PositionScale);
                if (!inContact)
                    target -= new Int2(forward.X * row * 350 / PositionScale, forward.Y * row * 350 / PositionScale);
                else
                {
                    // Preserve progress toward the enemy while closing only the sideways hole.
                    Int2 offset = member.Position - formation.Position;
                    long forwardProjection = (long)offset.X * forward.X + (long)offset.Y * forward.Y;
                    target += new Int2((int)(forward.X * forwardProjection / ((long)PositionScale * PositionScale)),
                        (int)(forward.Y * forwardProjection / ((long)PositionScale * PositionScale)));
                }
                Int2 delta = target - member.Position;
                int distance = IntegerSqrt(delta.SqrMagnitude);
                if (distance > 0)
                {
                    int step = Math.Min(reformSpeed, distance);
                    member.Position += new Int2(delta.X * step / distance, delta.Y * step / distance);
                }
                aliveIndex++;
            }
        }

        private bool CanBeginOrContinueCharge(SimFormation formation, SimFormation enemy,
            BattleUnitDefinition definition, long distanceSquared)
        {
            if (definition.Role != BattleUnitRole.Cavalry || formation.Morale < 450 || formation.Cohesion < 500) return false;
            if (IsFlankOrder(formation.Order) && formation.FlankStage < 2) return false;
            if (TerrainCharge(formation) < 500 || TerrainCharge(enemy) < 500) return false;
            int distance = IntegerSqrt(distanceSquared);
            if (formation.Status == FormationStatus.Charging)
                return distance > definition.MeleeReachMilli && ChargeLaneOpen(formation, enemy);
            if (Tick < formation.NextChargeTick || distance < definition.MinimumChargeDistanceMilli ||
                distance > definition.MinimumChargeDistanceMilli * 4) return false;
            if (!ChargeLaneOpen(formation, enemy)) return false;
            formation.ChargeStartTick = Tick;
            return true;
        }

        private bool ChargeLaneOpen(SimFormation charger, SimFormation target)
        {
            Int2 line = target.Position - charger.Position;
            long lineLengthSquared = Math.Max(1L, line.SqrMagnitude);
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation obstruction = Formations[i];
                if (obstruction.Id == charger.Id || obstruction.Id == target.Id ||
                    obstruction.Side != charger.Side || CountLiving(obstruction) == 0) continue;
                Int2 relative = obstruction.Position - charger.Position;
                long projection = (long)relative.X * line.X + (long)relative.Y * line.Y;
                if (projection <= 0 || projection >= lineLengthSquared) continue;
                long cross = Math.Abs((long)relative.X * line.Y - (long)relative.Y * line.X);
                int corridor = FormationRadius(charger) + FormationRadius(obstruction);
                if (cross * cross <= (long)corridor * corridor * lineLengthSquared) return false;
            }
            return true;
        }

        private void ResolveRanged(SimFormation attackerFormation, SimFormation defenderFormation, BattleUnitDefinition definition)
        {
            int launched = 0;
            bool readyShooter = false;
            bool validShot = false;
            for (int i = 0; i < attackerFormation.CombatantIds.Count; i++)
            {
                SimCombatant attacker = Combatants[attackerFormation.CombatantIds[i]];
                if (!attacker.Alive || attacker.Ammunition <= 0 || attacker.NextAttackTick > Tick) continue;
                readyShooter = true;
                SimCombatant target = FindNearestLivingCombatant(defenderFormation, attacker.Position);
                if (target == null) break;
                long distanceSquared = (target.Position - attacker.Position).SqrMagnitude;
                if (distanceSquared > (long)definition.RangedReachMilli * definition.RangedReachMilli) continue;
                validShot = true;
                int distance = IntegerSqrt(distanceSquared);
                Projectiles.Add(new SimProjectile
                {
                    Id = nextProjectileId++, Side = attackerFormation.Side,
                    SourceCombatantId = attacker.Id, TargetCombatantId = target.Id,
                    Position = attacker.Position, SpeedMilliPerTick = Math.Max(1, definition.ProjectileSpeedMilliPerTick),
                    Damage = definition.RangedDamage * EffectDamagePermille(attackerFormation.Id) / 1000,
                    AccuracyPermille = TerrainRangedAccuracy(attackerFormation),
                    RemainingTicks = Math.Max(2, distance / Math.Max(1, definition.ProjectileSpeedMilliPerTick) + 5)
                });
                attacker.Ammunition--;
                attacker.NextAttackTick = Tick + Math.Max(1, definition.RangedCooldownTicks);
                launched++;
                Telemetry.ProjectilesLaunched++;
                lastCombatActivityTick = Tick;
            }
            if (launched == 0)
            {
                if (!FormationHasAmmunition(attackerFormation) || readyShooter && !validShot)
                    MoveToward(attackerFormation, defenderFormation.Position, Math.Max(1, definition.SpeedMilliPerTick));
            }
        }

        private void UpdateProjectiles()
        {
            // Remove spent projectiles immediately. Keeping them forever made this loop scan
            // every missile fired during the battle on every subsequent tick.
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                SimProjectile projectile = Projectiles[i];
                if (!projectile.Active) { Projectiles.RemoveAt(i); continue; }
                if (projectile.TargetCombatantId < 0 || projectile.TargetCombatantId >= Combatants.Count)
                {
                    Projectiles.RemoveAt(i);
                    continue;
                }
                SimCombatant target = Combatants[projectile.TargetCombatantId];
                if (!target.Alive)
                {
                    SimFormation targetFormation = Formations.Find(item => item.Id == target.FormationId);
                    target = targetFormation != null ? FindNearestLivingCombatant(targetFormation, projectile.Position) : null;
                    if (target == null) { Projectiles.RemoveAt(i); continue; }
                    projectile.TargetCombatantId = target.Id;
                }
                Int2 delta = target.Position - projectile.Position;
                int distance = IntegerSqrt(delta.SqrMagnitude);
                if (distance <= projectile.SpeedMilliPerTick)
                {
                    ResolveProjectileHit(projectile, target);
                    Projectiles.RemoveAt(i);
                    continue;
                }
                projectile.Position += new Int2(delta.X * projectile.SpeedMilliPerTick / Math.Max(1, distance),
                    delta.Y * projectile.SpeedMilliPerTick / Math.Max(1, distance));
                projectile.RemainingTicks--;
                if (projectile.RemainingTicks <= 0) Projectiles.RemoveAt(i);
            }
        }

        private void ResolveProjectileHit(SimProjectile projectile, SimCombatant target)
        {
            SimFormation targetFormation = Formations.Find(item => item.Id == target.FormationId);
            BattleUnitDefinition targetDefinition = definitions[target.DefinitionId];
            int mitigation = Math.Min(85, targetDefinition.ArmorPercent / 2 + targetDefinition.ShieldPercent +
                TerrainDefense(targetFormation) + EffectDefense(targetFormation) + FormationSupportDefense(targetFormation));
            int damage = Math.Max(1, projectile.Damage * (100 - mitigation) / 100);
            // Deterministic accuracy: distance and equipment can later feed this threshold.
            int hitThreshold = 850 * Math.Max(100, projectile.AccuracyPermille) / 1000;
            hitThreshold = hitThreshold * TerrainVisibility(target.Position) / 1000;
            if (Rng.Range(0, 1000) >= hitThreshold) return;
            target.Health -= damage;
            Telemetry.ProjectileHits++;
            if (target.Health <= 0)
            {
                target.Alive = false;
                if (targetFormation != null)
                {
                    targetFormation.Morale = Math.Max(0, targetFormation.Morale - 28);
                    targetFormation.Cohesion = Math.Max(0, targetFormation.Cohesion - 12);
                }
            }
        }

        private bool FormationHasAmmunition(SimFormation formation)
        {
            for (int i = 0; i < formation.CombatantIds.Count; i++)
            {
                SimCombatant combatant = Combatants[formation.CombatantIds[i]];
                if (combatant.Alive && combatant.Ammunition > 0) return true;
            }
            return false;
        }

        private void ResolveMelee(SimFormation attackerFormation, SimFormation defenderFormation, BattleUnitDefinition attackerDefinition)
        {
            BattleUnitDefinition defenderDefinition = definitions[defenderFormation.DefinitionId];
            bool chargeImpact = attackerFormation.Status == FormationStatus.Charging;
            AttackDirection direction = ClassifyAttackDirection(defenderFormation, attackerFormation.Position);
            if (chargeImpact)
            {
                Telemetry.Charges++;
                attackerFormation.NextChargeTick = Tick + Math.Max(1, attackerDefinition.ChargeCooldownTicks);
                defenderFormation.Cohesion = Math.Max(0, defenderFormation.Cohesion -
                    Math.Max(40, attackerDefinition.Mass - defenderDefinition.Mass / 2));
                defenderFormation.Morale = Math.Max(0, defenderFormation.Morale -
                    (direction == AttackDirection.Rear ? 150 : direction == AttackDirection.Flank ? 100 : 55));
            }
            if (direction == AttackDirection.Flank) defenderFormation.Cohesion = Math.Max(0, defenderFormation.Cohesion - 8);
            else if (direction == AttackDirection.Rear) defenderFormation.Cohesion = Math.Max(0, defenderFormation.Cohesion - 15);
            if (direction == AttackDirection.Flank) Telemetry.FlankAttacks++;
            else if (direction == AttackDirection.Rear) Telemetry.RearAttacks++;
            int attacks = 0;
            int committed = 0;
            int effectiveFrontage = Math.Max(1, attackerFormation.Frontage * Math.Max(250, attackerFormation.Cohesion) / 1000);
            // Disciplined units feed a front and supporting rank into combat. Loose units
            // are allowed to dissolve into a general melee instead of waiting in perfect rows.
            int engagementCapacity = attackerDefinition.Disciplined
                ? Math.Min(CountLiving(attackerFormation), effectiveFrontage * 2)
                : CountLiving(attackerFormation);
            int directionalShield = direction == AttackDirection.Front ? defenderDefinition.ShieldPercent :
                direction == AttackDirection.Flank ? defenderDefinition.ShieldPercent / 3 : 0;
            int mitigation = Math.Min(80, defenderDefinition.ArmorPercent + directionalShield +
                TerrainDefense(defenderFormation) + EffectDefense(defenderFormation) + FormationSupportDefense(defenderFormation));
            int chargeDamage = chargeImpact ? attackerDefinition.ChargeDamage * TerrainCharge(attackerFormation) / 1000 : 0;
            int baseRawDamage = attackerDefinition.MeleeDamage + chargeDamage;
            baseRawDamage = baseRawDamage * TerrainDamagePermille(attackerFormation) / 1000;
            int cooperatingAttackers = CountAlliedFormationsTargeting(attackerFormation.Side, defenderFormation.Id);
            baseRawDamage = baseRawDamage * (1000 + Math.Min(200, Math.Max(0, cooperatingAttackers - 1) * 100)) / 1000;
            baseRawDamage = baseRawDamage * EffectDamagePermille(attackerFormation.Id) / 1000;
            baseRawDamage = Math.Max(1, baseRawDamage * Math.Max(400, attackerFormation.Cohesion) / 1000);
            for (int i = 0; i < attackerFormation.CombatantIds.Count; i++)
            {
                SimCombatant attacker = Combatants[attackerFormation.CombatantIds[i]];
                if (!attacker.Alive) continue;
                if (committed >= engagementCapacity) break;
                SimCombatant defender = FindNearestMeleeTarget(defenderFormation, attacker.Position);
                if (defender == null) break;
                int reach = attackerDefinition.MeleeReachMilli + 400;
                Int2 contact = defender.Position - attacker.Position;
                int rank = committed / Math.Max(1, effectiveFrontage);
                // Supporting ranks stop one member-spacing behind the rank ahead. They still
                // feed forward when casualties reduce the number of living members before them.
                int desiredReach = reach + rank * 350;
                if (contact.SqrMagnitude > (long)desiredReach * desiredReach)
                {
                    int distance = IntegerSqrt(contact.SqrMagnitude);
                    int closingStep = Math.Min(Math.Max(80, attackerDefinition.SpeedMilliPerTick), Math.Max(0, distance - desiredReach));
                    if (closingStep > 0)
                        attacker.Position += new Int2(contact.X * closingStep / Math.Max(1, distance), contact.Y * closingStep / Math.Max(1, distance));
                    committed++;
                    continue;
                }
                int contactDistance = IntegerSqrt(contact.SqrMagnitude);
                if (rank > 0 && contactDistance < desiredReach - 175)
                {
                    int backingStep = Math.Min(Math.Max(60, attackerDefinition.SpeedMilliPerTick / 2), desiredReach - contactDistance);
                    attacker.Position -= new Int2(contact.X * backingStep / Math.Max(1, contactDistance),
                        contact.Y * backingStep / Math.Max(1, contactDistance));
                    committed++;
                    continue;
                }
                committed++;
                // Cooldown controls striking, not whether a member can hold or close its place
                // in the engagement line.
                if (attacker.NextAttackTick > Tick || attacks >= effectiveFrontage) continue;
                int damage = Math.Max(1, baseRawDamage * (100 - mitigation) / 100);
                damage += Rng.Range(0, Math.Max(1, damage / 5 + 1));
                pendingMeleeHits.Add(new PendingMeleeHit { TargetId = defender.Id, Damage = damage,
                    MoraleDamage = 35, CohesionDamage = 25 });
                attacker.NextAttackTick = Tick + Math.Max(1, attackerDefinition.AttackCooldownTicks);
                attacks++;
                Telemetry.MeleeAttacks++;
                lastCombatActivityTick = Tick;
            }
            if (committed == 0) MoveToward(attackerFormation, defenderFormation.Position, Math.Max(1, attackerDefinition.SpeedMilliPerTick / 2));
            else attackerFormation.Cohesion = Math.Max(0, attackerFormation.Cohesion - (attackerDefinition.Disciplined ? 1 : 2));
            if (chargeImpact)
            {
                attackerFormation.Status = FormationStatus.Engaged;
                attackerFormation.Cohesion = Math.Max(0, attackerFormation.Cohesion - 80);
            }
            // Formation.Position remains the stable command/formation origin. Recentring it on
            // whichever members stepped forward made separation drag the entire unit back and
            // forth every tick, which appeared as jumping in the viewer.
        }

        private void ApplyPendingMeleeHits()
        {
            for (int i = 0; i < pendingMeleeHits.Count; i++)
            {
                PendingMeleeHit hit = pendingMeleeHits[i];
                if (hit.TargetId < 0 || hit.TargetId >= Combatants.Count) continue;
                SimCombatant target = Combatants[hit.TargetId];
                if (!target.Alive) continue;
                target.Health -= hit.Damage;
                if (target.Health > 0) continue;
                target.Alive = false;
                SimFormation formation = Formations.Find(item => item.Id == target.FormationId);
                if (formation != null)
                {
                    formation.Morale = Math.Max(0, formation.Morale - hit.MoraleDamage);
                    formation.Cohesion = Math.Max(0, formation.Cohesion - hit.CohesionDamage);
                }
            }
        }

        private enum AttackDirection : byte { Front, Flank, Rear }
        private static AttackDirection ClassifyAttackDirection(SimFormation defender, Int2 attackerPosition)
        {
            Int2 incoming = attackerPosition - defender.Position;
            long dot = (long)defender.Facing.X * incoming.X + (long)defender.Facing.Y * incoming.Y;
            long facingLength = Math.Max(1L, defender.Facing.SqrMagnitude);
            long incomingLength = Math.Max(1L, incoming.SqrMagnitude);
            long squared = dot * dot;
            // cos(60)^2 = 0.25; sign distinguishes front and rear.
            if (squared * 4 >= facingLength * incomingLength) return dot >= 0 ? AttackDirection.Front : AttackDirection.Rear;
            return AttackDirection.Flank;
        }

        private void MoveToward(SimFormation formation, Int2 target, int distance) => Move(formation, target, distance, false);
        private void MoveAway(SimFormation formation, Int2 target, int distance) => Move(formation, target, distance, true);
        private void Move(SimFormation formation, Int2 target, int distance, bool away)
        {
            BattleTerrainArea currentTerrain = TerrainAt(formation.Position);
            if (currentTerrain != null)
                distance = Math.Max(1, distance * TerrainMovementPermille(formation, currentTerrain) / 1000);
            SimBattleEffect movementEffect = EffectFor(formation.Id);
            if (movementEffect != null) distance = Math.Max(1, distance * movementEffect.MovementPermille / 1000);
            Int2 delta = target - formation.Position;
            if (away) delta = new Int2(-delta.X, -delta.Y);
            int length = IntegerSqrt(delta.SqrMagnitude);
            if (length == 0) { delta = new Int2(formation.Side == 0 ? -1 : 1, 0); length = 1; }
            Int2 step = new Int2(delta.X * distance / length, delta.Y * distance / length);
            Int2 proposed = formation.Position + step;
            BattleTerrainArea blocked = BlockingTerrainAt(proposed);
            if (blocked != null)
            {
                Int2 tangentA = new Int2(-delta.Y, delta.X);
                Int2 tangentB = new Int2(delta.Y, -delta.X);
                Int2 tangent = ((formation.Id + blocked.Id) & 1) == 0 ? tangentA : tangentB;
                Int2 outward = formation.Position - blocked.Center;
                int outwardLength = Math.Max(1, IntegerSqrt(outward.SqrMagnitude));
                // A tangential-only step repeatedly points back into the obstacle on the next
                // tick. Add an outward component so the formation actually clears its edge.
                tangent += new Int2(outward.X * Math.Max(1, IntegerSqrt(tangent.SqrMagnitude)) / outwardLength,
                    outward.Y * Math.Max(1, IntegerSqrt(tangent.SqrMagnitude)) / outwardLength);
                int tangentLength = Math.Max(1, IntegerSqrt(tangent.SqrMagnitude));
                step = new Int2(tangent.X * distance / tangentLength, tangent.Y * distance / tangentLength);
                proposed = formation.Position + step;
                if (BlockingTerrainAt(proposed) != null) return;
                formation.Cohesion = Math.Max(0, formation.Cohesion - 1);
            }
            formation.Position += step;
            TurnFacingToward(formation, new Int2(delta.X * PositionScale / length, delta.Y * PositionScale / length));
            for (int i = 0; i < formation.CombatantIds.Count; i++)
            {
                SimCombatant combatant = Combatants[formation.CombatantIds[i]];
                if (combatant.Alive) combatant.Position += step;
            }
        }

        private BattleTerrainArea TerrainAt(Int2 position)
        {
            BattleTerrainArea result = null;
            for (int i = 0; i < StartState.Terrain.Count; i++)
            {
                BattleTerrainArea area = StartState.Terrain[i];
                if ((position - area.Center).SqrMagnitude > (long)area.RadiusMilli * area.RadiusMilli) continue;
                if (result == null || area.Impassable || area.MovementPermille < result.MovementPermille) result = area;
            }
            return result;
        }

        private BattleTerrainArea BlockingTerrainAt(Int2 position)
        {
            for (int i = 0; i < StartState.Terrain.Count; i++)
            {
                BattleTerrainArea area = StartState.Terrain[i];
                if (area.Impassable && (position - area.Center).SqrMagnitude <= (long)area.RadiusMilli * area.RadiusMilli)
                    return area;
            }
            return null;
        }

        private int TerrainDefense(SimFormation formation)
        {
            if (formation == null) return 0;
            BattleTerrainArea terrain = TerrainAt(formation.Position);
            if (terrain == null) return 0;
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            int defense = terrain.DefenseBonusPercent;
            if (terrain.Kind == BattleTerrainKind.Forest)
            {
                if (definition.Forester) defense += 25;
                else if (definition.FormationTerrainPenalty) defense -= 10;
            }
            else if (terrain.Kind == BattleTerrainKind.Rough && definition.FormationTerrainPenalty)
                defense -= 8;
            return defense;
        }

        private int EffectDefense(SimFormation formation)
        {
            if (formation == null) return 0;
            SimBattleEffect effect = EffectFor(formation.Id);
            return effect != null ? effect.DefenseBonusPercent : 0;
        }

        private int FormationSupportDefense(SimFormation formation)
        {
            int support = 0;
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation ally = Formations[i];
                if (ally.Id == formation.Id || ally.Side != formation.Side || CountLiving(ally) == 0 ||
                    ally.Status == FormationStatus.Routing || ally.Status == FormationStatus.Destroyed) continue;
                if ((ally.Position - formation.Position).SqrMagnitude <= 12250000L) support += 4;
            }
            return Math.Min(8, support);
        }

        private int TerrainCharge(SimFormation formation)
        {
            if (formation == null) return 1000;
            BattleTerrainArea terrain = TerrainAt(formation.Position);
            if (terrain == null) return 1000;
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            if (terrain.Kind == BattleTerrainKind.Forest)
            {
                if (definition.Forester || definition.ForestryImmune) return 800;
                if (definition.FormationTerrainPenalty) return 200;
            }
            if (terrain.Kind == BattleTerrainKind.Rough)
            {
                if (definition.Forester || definition.ForestryImmune) return 750;
                if (definition.FormationTerrainPenalty) return 300;
            }
            return terrain.ChargePermille;
        }

        private int TerrainRangedAccuracy(SimFormation formation)
        {
            if (formation == null) return 1000;
            BattleTerrainArea terrain = TerrainAt(formation.Position);
            if (terrain == null) return 1000;
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            if (terrain.Kind == BattleTerrainKind.Forest)
            {
                if (definition.Forester) return 1000;
                if (definition.ForestryImmune) return 900;
                if (definition.FormationTerrainPenalty) return 550;
            }
            if (terrain.Kind == BattleTerrainKind.Rough)
            {
                if (definition.Forester || definition.ForestryImmune) return 1000;
                if (definition.FormationTerrainPenalty) return 750;
            }
            return terrain.RangedAccuracyPermille;
        }

        private int TerrainMovementPermille(SimFormation formation, BattleTerrainArea terrain)
        {
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            if (terrain.Kind == BattleTerrainKind.Forest)
            {
                if (definition.Forester || definition.ForestryImmune) return 1000;
                if (definition.FormationTerrainPenalty) return 500;
            }
            if (terrain.Kind == BattleTerrainKind.Rough)
            {
                if (definition.Forester || definition.ForestryImmune) return 1000;
                if (definition.FormationTerrainPenalty) return 550;
            }
            return terrain.MovementPermille;
        }

        private int TerrainDamagePermille(SimFormation formation)
        {
            BattleTerrainArea terrain = TerrainAt(formation.Position);
            if (terrain == null) return 1000;
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            if (terrain.Kind == BattleTerrainKind.Forest)
            {
                if (definition.Forester) return 1150;
                if (definition.FormationTerrainPenalty) return 750;
            }
            if (terrain.Kind == BattleTerrainKind.Rough && definition.FormationTerrainPenalty) return 850;
            return 1000;
        }

        private int TerrainVisibility(Int2 position)
        {
            BattleTerrainArea terrain = TerrainAt(position);
            return terrain != null ? terrain.VisibilityPermille : 1000;
        }

        private void ResolveFormationSeparation()
        {
            for (int i = 0; i < Formations.Count; i++)
            for (int j = i + 1; j < Formations.Count; j++)
            {
                SimFormation a = Formations[i], b = Formations[j];
                if (CountLiving(a) == 0 || CountLiving(b) == 0 || a.Status == FormationStatus.Routing || b.Status == FormationStatus.Routing) continue;
                // Once enemies are in contact, member-level melee owns their spacing. Pushing
                // both complete formations apart here causes contact oscillation.
                if (a.Side != b.Side &&
                    (a.Status == FormationStatus.Engaged || a.Status == FormationStatus.Wavering || a.Status == FormationStatus.Charging) &&
                    (b.Status == FormationStatus.Engaged || b.Status == FormationStatus.Wavering || b.Status == FormationStatus.Charging))
                    continue;
                Int2 delta = b.Position - a.Position;
                int distance = IntegerSqrt(delta.SqrMagnitude);
                int minimum = (FormationRadius(a) + FormationRadius(b)) * (a.Side == b.Side ? 90 : 55) / 100;
                if (distance >= minimum) continue;
                if (distance == 0) { delta = new Int2(a.Id < b.Id ? 1 : -1, 0); distance = 1; }
                int overlap = minimum - distance;
                Int2 push = new Int2(delta.X * overlap / (distance * 2), delta.Y * overlap / (distance * 2));
                TranslateFormation(a, new Int2(-push.X, -push.Y));
                TranslateFormation(b, push);
                if (a.Side != b.Side)
                {
                    a.Cohesion = Math.Max(0, a.Cohesion - 2);
                    b.Cohesion = Math.Max(0, b.Cohesion - 2);
                }
            }
        }

        private void TranslateFormation(SimFormation formation, Int2 shift)
        {
            formation.Position += shift;
            for (int i = 0; i < formation.CombatantIds.Count; i++)
            {
                SimCombatant combatant = Combatants[formation.CombatantIds[i]];
                if (combatant.Alive) combatant.Position += shift;
            }
        }

        private void TurnFacingToward(SimFormation formation, Int2 desired)
        {
            BattleUnitDefinition definition = definitions[formation.DefinitionId];
            int turn = Math.Max(1, definition.TurnRateMilli);
            if (formation.Facing.SqrMagnitude == 0) { formation.Facing = desired; return; }
            int x = formation.Facing.X + Clamp(desired.X - formation.Facing.X, -turn, turn);
            int y = formation.Facing.Y + Clamp(desired.Y - formation.Facing.Y, -turn, turn);
            int length = IntegerSqrt((long)x * x + (long)y * y);
            formation.Facing = length == 0 ? desired : new Int2(x * PositionScale / length, y * PositionScale / length);
        }

        private static int Clamp(int value, int minimum, int maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;

        private void Recenter(SimFormation formation)
        {
            long x = 0, y = 0; int count = 0;
            for (int i = 0; i < formation.CombatantIds.Count; i++)
            {
                SimCombatant combatant = Combatants[formation.CombatantIds[i]];
                if (!combatant.Alive) continue;
                x += combatant.Position.X; y += combatant.Position.Y; count++;
            }
            if (count > 0) formation.Position = new Int2((int)(x / count), (int)(y / count));
        }

        private SimFormation FindNearestEnemy(SimFormation source)
        {
            SimFormation result = null; long best = long.MaxValue;
            BattleUnitDefinition sourceDefinition = definitions[source.DefinitionId];
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation candidate = Formations[i];
                if (candidate.Side == source.Side || CountLiving(candidate) == 0 ||
                    candidate.Status == FormationStatus.Routing && sourceDefinition.Role != BattleUnitRole.Cavalry) continue;
                long distance = (candidate.Position - source.Position).SqrMagnitude;
                // Cavalry naturally seeks exposed ranged formations, but distance still dominates
                // so it will not ignore a nearby intercepting enemy.
                if (sourceDefinition.Role == BattleUnitRole.Cavalry &&
                    definitions[candidate.DefinitionId].Role == BattleUnitRole.Ranged)
                    distance = distance * 3 / 4;
                if (distance < best || distance == best && (result == null || candidate.Id < result.Id))
                { result = candidate; best = distance; }
            }
            return result;
        }

        private SimFormation FindFinishingOpportunity(SimFormation source)
        {
            SimFormation result = null;
            long bestScore = long.MaxValue;
            int sourceLiving = CountLiving(source);
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation candidate = Formations[i];
                if (candidate.Side == source.Side || CountLiving(candidate) == 0) continue;
                int enemyLiving = CountLiving(candidate);
                bool collapsing = candidate.Status == FormationStatus.Routing ||
                    candidate.Status == FormationStatus.Wavering || candidate.Morale < 350 || candidate.Cohesion < 300;
                bool locallyOvermatched = enemyLiving * 2 <= sourceLiving &&
                    (candidate.Morale < 600 || candidate.Cohesion < 550);
                if (!collapsing && !locallyOvermatched) continue;
                long distance = (candidate.Position - source.Position).SqrMagnitude;
                // Opportunism is local. This avoids abandoning the battle line for a routed
                // formation on the far side of the field.
                if (distance > 81000000L) continue;
                long score = distance + enemyLiving * 250000L;
                if (score < bestScore || score == bestScore && (result == null || candidate.Id < result.Id))
                { result = candidate; bestScore = score; }
            }
            return result;
        }

        private SimCombatant FindNearestLivingCombatant(SimFormation formation, Int2 position)
        {
            SimCombatant result = null; long best = long.MaxValue;
            for (int i = 0; i < formation.CombatantIds.Count; i++)
            {
                SimCombatant candidate = Combatants[formation.CombatantIds[i]];
                if (!candidate.Alive) continue;
                long distance = (candidate.Position - position).SqrMagnitude;
                if (distance < best || distance == best && candidate.Id < result.Id) { result = candidate; best = distance; }
            }
            return result;
        }

        private SimCombatant FindNearestMeleeTarget(SimFormation formation, Int2 position)
        {
            SimCombatant result = null; long best = long.MaxValue;
            for (int i = 0; i < formation.CombatantIds.Count; i++)
            {
                SimCombatant candidate = Combatants[formation.CombatantIds[i]];
                if (!candidate.Alive) continue;
                int queuedDamage = 0;
                for (int h = 0; h < pendingMeleeHits.Count; h++)
                    if (pendingMeleeHits[h].TargetId == candidate.Id) queuedDamage += pendingMeleeHits[h].Damage;
                if (queuedDamage >= candidate.Health) continue;
                long distance = (candidate.Position - position).SqrMagnitude;
                if (distance < best || distance == best && (result == null || candidate.Id < result.Id))
                { result = candidate; best = distance; }
            }
            return result;
        }

        private int CountLiving(SimFormation formation)
        {
            int count = 0;
            for (int i = 0; i < formation.CombatantIds.Count; i++) if (Combatants[formation.CombatantIds[i]].Alive) count++;
            return count;
        }

        private int FormationRadius(SimFormation formation) => 350 * IntegerCeilingSqrt(Math.Max(1, CountLiving(formation)));

        private void ResolveVictory()
        {
            bool sideA = false, sideB = false, escapingA = false, escapingB = false;
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation formation = Formations[i];
                if (CountLiving(formation) == 0 || formation.Status == FormationStatus.Destroyed) continue;
                if (formation.Status == FormationStatus.Routing)
                {
                    if (formation.Side == 0) escapingA = true; else escapingB = true;
                    continue;
                }
                if (formation.Side == 0) sideA = true; else sideB = true;
            }
            int configuredMaximum = StartState.MaximumTicks > 0 ? StartState.MaximumTicks : 2000;
            int maximumTicks = Math.Min(2000, configuredMaximum);
            if ((!sideA && escapingA || !sideB && escapingB) && Tick < maximumTicks) return;
            if (sideA && sideB && Tick < maximumTicks) return;
            Status = BattleStatus.Finished;
            WinningSide = sideA && !sideB ? 0 : sideB && !sideA ? 1 : Tick >= maximumTicks ? LeadingSide() : -1;
            for (int i = 0; i < observers.Count; i++) observers[i].OnBattleFinished(this);
        }

        private int LeadingSide()
        {
            long[] score = new long[2];
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation formation = Formations[i];
                int living = CountLiving(formation);
                score[formation.Side] += living * 1000L + formation.Morale * living + formation.Cohesion * living;
            }
            return score[0] == score[1] ? -1 : score[0] > score[1] ? 0 : 1;
        }

        public int LivingCampaignUnits(int side, int definitionId)
        {
            int livingMembers = 0;
            for (int i = 0; i < Formations.Count; i++)
            {
                if (Formations[i].Side == side && Formations[i].DefinitionId == definitionId) livingMembers += CountLiving(Formations[i]);
            }
            int membersPerUnit = definitions[definitionId].MembersPerCampaignUnit;
            return (livingMembers + membersPerUnit - 1) / membersPerUnit;
        }

        public BattleSnapshot CreateSnapshot()
        {
            BattleSnapshot snapshot = new BattleSnapshot { BattleId = StartState.BattleId, Tick = Tick,
                Status = Status, WinningSide = WinningSide, StateHash = ComputeHash() };
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation f = Formations[i];
                snapshot.Formations.Add(new FormationSnapshot { Id = f.Id, Side = f.Side, DefinitionId = f.DefinitionId,
                    Position = f.Position, Facing = f.Facing, Morale = f.Morale, Cohesion = f.Cohesion,
                    Status = f.Status, Order = f.Order, TargetFormationId = f.TargetFormationId,
                    Frontage = f.Frontage, Depth = f.Depth, TerrainAreaId = f.TerrainAreaId, Pursuing = f.Pursuing });
            }
            for (int i = 0; i < Combatants.Count; i++)
            {
                SimCombatant c = Combatants[i];
                snapshot.Combatants.Add(new CombatantSnapshot { Id = c.Id, FormationId = c.FormationId,
                    DefinitionId = c.DefinitionId, Position = c.Position, Health = c.Health,
                    NextAttackTick = c.NextAttackTick, Alive = c.Alive });
            }
            for (int i = 0; i < Projectiles.Count; i++)
            {
                SimProjectile p = Projectiles[i];
                snapshot.Projectiles.Add(new ProjectileSnapshot { Id = p.Id, Side = p.Side,
                    SourceCombatantId = p.SourceCombatantId, TargetCombatantId = p.TargetCombatantId,
                    Position = p.Position, Active = p.Active });
            }
            for (int i = 0; i < StartState.Terrain.Count; i++)
            {
                BattleTerrainArea t = StartState.Terrain[i];
                snapshot.Terrain.Add(new TerrainSnapshot { Id = t.Id, Kind = t.Kind, Center = t.Center,
                    RadiusMilli = t.RadiusMilli, Impassable = t.Impassable });
            }
            for (int i = 0; i < Effects.Count; i++)
            {
                SimBattleEffect e = Effects[i];
                snapshot.Effects.Add(new EffectSnapshot { Id = e.Id, FormationId = e.FormationId,
                    Ability = e.Ability, EndTick = e.EndTick });
            }
            return snapshot;
        }

        public ulong ComputeHash()
        {
            ulong hash = 1469598103934665603UL;
            Hash(ref hash, Tick); Hash(ref hash, (int)Status); Hash(ref hash, WinningSide); Hash(ref hash, Rng.State);
            for (int i = 0; i < StartState.Definitions.Count; i++)
            {
                BattleUnitDefinition d = StartState.Definitions[i];
                Hash(ref hash, d.DefinitionId); Hash(ref hash, d.ForestryImmune ? 1 : 0);
                Hash(ref hash, d.Forester ? 1 : 0); Hash(ref hash, d.FormationTerrainPenalty ? 1 : 0);
            }
            for (int i = 0; i < StartState.Terrain.Count; i++)
            {
                BattleTerrainArea t = StartState.Terrain[i];
                Hash(ref hash, t.Id); Hash(ref hash, (int)t.Kind); Hash(ref hash, t.Center.X); Hash(ref hash, t.Center.Y);
                Hash(ref hash, t.RadiusMilli); Hash(ref hash, t.MovementPermille); Hash(ref hash, t.ChargePermille);
                Hash(ref hash, t.RangedAccuracyPermille); Hash(ref hash, t.DefenseBonusPercent); Hash(ref hash, t.Impassable ? 1 : 0);
                Hash(ref hash, t.VisibilityPermille);
            }
            for (int i = 0; i < Formations.Count; i++)
            {
                SimFormation f = Formations[i];
                Hash(ref hash, f.Id); Hash(ref hash, f.Position.X); Hash(ref hash, f.Position.Y);
                Hash(ref hash, f.Morale); Hash(ref hash, f.Cohesion); Hash(ref hash, (int)f.Status);
                Hash(ref hash, f.Facing.X); Hash(ref hash, f.Facing.Y); Hash(ref hash, f.ChargeStartTick); Hash(ref hash, f.NextChargeTick);
                Hash(ref hash, (int)f.Order); Hash(ref hash, f.OrderLockedUntilTick);
                Hash(ref hash, f.TargetFormationId); Hash(ref hash, f.Frontage); Hash(ref hash, f.Depth); Hash(ref hash, f.Pursuing ? 1 : 0);
                Hash(ref hash, f.TerrainAreaId);
                Hash(ref hash, f.FlankStage); Hash(ref hash, f.FlankTargetId); Hash(ref hash, f.FlankBlockedTicks);
            }
            for (int i = 0; i < Combatants.Count; i++)
            {
                SimCombatant c = Combatants[i];
                Hash(ref hash, c.Id); Hash(ref hash, c.Health); Hash(ref hash, c.Position.X); Hash(ref hash, c.Position.Y);
                Hash(ref hash, c.NextAttackTick); Hash(ref hash, c.Alive ? 1 : 0);
                Hash(ref hash, c.Ammunition);
            }
            for (int i = 0; i < Projectiles.Count; i++)
            {
                SimProjectile p = Projectiles[i];
                Hash(ref hash, p.Id); Hash(ref hash, p.SourceCombatantId); Hash(ref hash, p.TargetCombatantId);
                Hash(ref hash, p.Position.X); Hash(ref hash, p.Position.Y); Hash(ref hash, p.RemainingTicks);
                Hash(ref hash, p.Active ? 1 : 0); Hash(ref hash, p.AccuracyPermille);
            }
            for (int i = 0; i < Effects.Count; i++)
            {
                SimBattleEffect e = Effects[i];
                Hash(ref hash, e.Id); Hash(ref hash, e.FormationId); Hash(ref hash, (int)e.Ability);
                Hash(ref hash, e.StartTick); Hash(ref hash, e.EndTick); Hash(ref hash, e.MovementPermille);
                Hash(ref hash, e.DamagePermille); Hash(ref hash, e.DefenseBonusPercent);
                Hash(ref hash, e.CohesionPerTick); Hash(ref hash, e.MoralePerTick);
            }
            for (int i = 0; i < Generals.Count; i++)
            {
                SimGeneral g = Generals[i];
                Hash(ref hash, g.Side); Hash(ref hash, (int)g.Trait); Hash(ref hash, g.NextDecisionTick);
                for (int a = 0; a < g.NextAbilityTicks.Length; a++) Hash(ref hash, g.NextAbilityTicks[a]);
            }
            return hash;
        }

        private static void Hash(ref ulong hash, int value) { unchecked { hash ^= (uint)value; hash *= 1099511628211UL; } }
        private static void Hash(ref ulong hash, ulong value) { Hash(ref hash, (int)value); Hash(ref hash, (int)(value >> 32)); }
        private static int IntegerCeilingSqrt(int value) { int root = IntegerSqrt(value); return root * root == value ? root : root + 1; }
        private static int IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            long x = value, y = (x + 1) / 2;
            while (y < x) { x = y; y = (x + value / x) / 2; }
            return (int)Math.Min(int.MaxValue, x);
        }
    }
}
