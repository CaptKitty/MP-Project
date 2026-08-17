using System;
using System.Collections.Generic;

namespace ProjectX.TileBattle
{
    public sealed class TileBattleSimulation
    {
        private sealed class ScheduledAction
        {
            public int Tick;
            public int Sequence;
            public int UnitId;
            public TileUnitAction Action;
        }

        private sealed class MoveIntent
        {
            public TileBattleUnit Unit;
            public TileCoord From;
            public TileCoord To;
            public TileUnitAction Action;
        }

        public TileBattleRules Rules { get; }
        public TileBattleGrid Grid { get; }
        public readonly List<TileBattleUnit> Units = new List<TileBattleUnit>();
        public readonly List<TileBattleEvent> Events = new List<TileBattleEvent>();
        public readonly List<TileBattleRoundSnapshot> History = new List<TileBattleRoundSnapshot>();
        public TileBattlePhase Phase { get; private set; } = TileBattlePhase.Vanguard;
        public int CommandRound { get; private set; }
        public int ResolutionTick { get; private set; }
        public TileBattleResult Result { get; } = new TileBattleResult();
        public ITileBattleGeneral LeftGeneral { get; }
        public ITileBattleGeneral RightGeneral { get; }
        public bool EnableDebugLogging;

        public TileBattleSimulation(TileBattleRules rules, ITileBattleGeneral leftGeneral, ITileBattleGeneral rightGeneral)
        {
            Rules = rules ?? new TileBattleRules();
            Grid = new TileBattleGrid(Rules.Width, Rules.Height);
            LeftGeneral = leftGeneral; RightGeneral = rightGeneral;
        }

        public void AddUnit(TileBattleUnit unit)
        {
            if (unit == null || unit.Definition == null) throw new ArgumentNullException(nameof(unit));
            if (!Grid.Contains(unit.Position)) throw new ArgumentOutOfRangeException(nameof(unit.Position));
            if (Units.Exists(item => item.Id == unit.Id)) throw new InvalidOperationException("Duplicate unit id " + unit.Id);
            if (unit.Deployed && Grid.OccupantAt(unit.Position) >= 0) throw new InvalidOperationException("Occupied deployment tile " + unit.Position);
            unit.Strength = unit.Strength > 0 ? unit.Strength : unit.Definition.Strength;
            if (unit.Ammunition < 0) unit.Ammunition = Math.Max(0, unit.Definition.Ammunition);
            Units.Add(unit); Units.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (unit.Deployed) Grid.SetOccupant(unit.Position, unit.Id);
        }

        public void RunCommandRound()
        {
            if (Result.Finished) return;
            if (History.Count == 0) CaptureSnapshot();
            CommandRound++; ResolutionTick = 0; UpdatePhase();
            CommitGeneralReserves();
            Emit(TileBattleEventType.RoundStarted, message: "Command round " + CommandRound + " begins in " + Phase);
            RefreshEngagementStates();
            CaptureSnapshot();
            // Both observations are captured before either general plans. Orders are then
            // generated independently and only committed after both calls return.
            TileBattleObservation leftObservation = Observe((int)TileBattleSide.Left);
            TileBattleObservation rightObservation = Observe((int)TileBattleSide.Right);
            TileOrderSet left = LeftGeneral != null ? LeftGeneral.FormulateOrders(leftObservation) : EmptyOrders(0);
            TileOrderSet right = RightGeneral != null ? RightGeneral.FormulateOrders(rightObservation) : EmptyOrders(1);
            CommitAndResolve(left, right);
        }

        public void ResolveOrders(TileOrderSet left, TileOrderSet right)
        {
            if (Result.Finished) return;
            if (History.Count == 0) CaptureSnapshot();
            CommandRound++; ResolutionTick = 0; UpdatePhase();
            Emit(TileBattleEventType.RoundStarted, message: "Command round " + CommandRound + " begins in " + Phase);
            CaptureSnapshot();
            CommitAndResolve(left ?? EmptyOrders(0), right ?? EmptyOrders(1));
        }

        public TileBattleResult RunToCompletion(int maximumRounds = -1)
        {
            int limit = maximumRounds > 0 ? maximumRounds : Rules.SafetyMaximumRounds;
            while (!Result.Finished && CommandRound < limit) RunCommandRound();
            if (!Result.Finished) Finish(LeadingSide(), "Safety round limit reached; result determined by remaining strength and morale");
            return Result;
        }

        public TileBattleObservation Observe(int side)
        {
            TileBattleObservation result = new TileBattleObservation { Side = side, CommandRound = CommandRound + 1,
                IsAttacker = side == (int)TileBattleSide.Left,
                Phase = Phase, Width = Grid.Width, Height = Grid.Height };
            for (int i = 0; i < Units.Count; i++)
            {
                TileBattleUnit unit = Units[i];
                result.Units.Add(new TileObservedUnit { Id = unit.Id, Side = unit.Side, Strength = unit.Strength,
                    Morale = unit.Morale, Cohesion = unit.Cohesion, Ammunition = unit.Ammunition,
                    Position = unit.Position, Facing = unit.Facing,
                    State = unit.State, Definition = unit.Definition, IsReserve = unit.IsReserve, Deployed = unit.Deployed });
            }
            return result;
        }

        public ulong ComputeHash()
        {
            ulong hash = 1469598103934665603UL;
            Hash(ref hash, CommandRound); Hash(ref hash, ResolutionTick); Hash(ref hash, (int)Phase);
            Hash(ref hash, Result.Finished ? 1 : 0); Hash(ref hash, Result.WinningSide);
            for (int i = 0; i < Units.Count; i++)
            {
                TileBattleUnit unit = Units[i];
                Hash(ref hash, unit.Id); Hash(ref hash, unit.Side); Hash(ref hash, unit.Position.X); Hash(ref hash, unit.Position.Y);
                Hash(ref hash, (int)unit.Facing); Hash(ref hash, (int)unit.State); Hash(ref hash, unit.Strength);
                Hash(ref hash, unit.Morale); Hash(ref hash, unit.Cohesion); Hash(ref hash, unit.Deployed ? 1 : 0);
                Hash(ref hash, unit.Ammunition);
                Hash(ref hash, unit.IsReserve ? 1 : 0); Hash(ref hash, unit.DeploymentRound);
            }
            return hash;
        }

        private static void Hash(ref ulong hash, int value)
        {
            unchecked { hash ^= (uint)value; hash *= 1099511628211UL; hash ^= (uint)(value >> 16); hash *= 1099511628211UL; }
        }

        private void CaptureSnapshot()
        {
            TileBattleRoundSnapshot snapshot = new TileBattleRoundSnapshot { CommandRound = CommandRound,
                ResolutionTick = ResolutionTick, Phase = Phase, EventCount = Events.Count, StateHash = ComputeHash() };
            for (int i = 0; i < Units.Count; i++)
            {
                TileBattleUnit unit = Units[i];
                snapshot.Units.Add(new TileBattleUnitViewState { Id = unit.Id, Side = unit.Side, Strength = unit.Strength,
                    Morale = unit.Morale, Cohesion = unit.Cohesion, Ammunition = unit.Ammunition,
                    Position = unit.Position, Facing = unit.Facing,
                    State = unit.State, Deployed = unit.Deployed });
            }
            History.Add(snapshot);
            if (History.Count > Rules.SafetyMaximumRounds * 32 + 2) History.RemoveAt(0);
        }

        private void CommitAndResolve(TileOrderSet left, TileOrderSet right)
        {
            Emit(TileBattleEventType.PlanChosen, message: "Left committed " + left.Plan + ": " + left.Reason);
            Emit(TileBattleEventType.PlanChosen, message: "Right committed " + right.Plan + ": " + right.Reason);
            List<ScheduledAction> timeline = new List<ScheduledAction>();
            ScheduleOrderSet(left, timeline); ScheduleOrderSet(right, timeline);
            timeline.Sort((a, b) => { int tick = a.Tick.CompareTo(b.Tick); if (tick != 0) return tick;
                int unit = a.UnitId.CompareTo(b.UnitId); return unit != 0 ? unit : a.Sequence.CompareTo(b.Sequence); });
            int index = 0;
            while (index < timeline.Count && !Result.Finished)
            {
                int tick = timeline[index].Tick; int end = index + 1;
                while (end < timeline.Count && timeline[end].Tick == tick) end++;
                ResolutionTick = tick; ResolveSimultaneousTick(timeline, index, end); index = end;
                EvaluateBattleEnd();
                CaptureSnapshot();
            }
            Emit(TileBattleEventType.RoundEnded, message: "Command round " + CommandRound + " resolved");
            EvaluateBattleEnd();
            if (History.Count == 0 || History[History.Count - 1].CommandRound != CommandRound)
                CaptureSnapshot();
            else
            {
                History[History.Count - 1].EventCount = Events.Count;
                History[History.Count - 1].StateHash = ComputeHash();
            }
        }

        private void ScheduleOrderSet(TileOrderSet set, List<ScheduledAction> timeline)
        {
            set.Orders.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
            for (int i = 0; i < set.Orders.Count; i++)
            {
                TileUnitOrder order = set.Orders[i]; TileBattleUnit unit = FindUnit(order.UnitId);
                if (unit == null || unit.Side != set.Side || !unit.Active || unit.IsReserve) continue;
                unit.CurrentOrder = order; unit.QueuedActions.Clear(); unit.AttackedThisRound = false; unit.Braced = false;
                int count = Math.Min(unit.Definition.Actions, order.Actions.Count); int tick = 0;
                for (int a = 0; a < count; a++)
                {
                    TileUnitAction action = order.Actions[a]; unit.QueuedActions.Add(action);
                    int interval = Math.Max(1, unit.Definition.Initiative * Math.Max(100, action.IntervalPermille) / 1000);
                    tick += interval;
                    timeline.Add(new ScheduledAction { Tick = tick, Sequence = a, UnitId = unit.Id, Action = action });
                }
                unit.ActionsRemaining = count; unit.NextActionTick = count > 0 ? timeline[timeline.Count - 1].Tick : 0;
                Emit(TileBattleEventType.OrderIssued, unit.Id, message: "Unit " + unit.Id + " ordered to " + order.Purpose + " with " + count + " actions");
            }
        }

        private void ResolveSimultaneousTick(List<ScheduledAction> timeline, int start, int end)
        {
            // All validation and intents are collected from the same pre-tick state.
            List<MoveIntent> moves = new List<MoveIntent>();
            List<ScheduledAction> attacks = new List<ScheduledAction>();
            List<ScheduledAction> remaining = new List<ScheduledAction>();
            for (int i = start; i < end; i++)
            {
                ScheduledAction scheduled = timeline[i]; TileBattleUnit unit = FindUnit(scheduled.UnitId);
                if (unit == null || !unit.Active || unit.State == TileUnitState.Routing) continue;
                Emit(TileBattleEventType.ActionStarted, unit.Id, message: scheduled.Action.Type + " completes");
                if (scheduled.Action.Type == TileActionType.Move)
                    moves.Add(new MoveIntent { Unit = unit, From = unit.Position, To = scheduled.Action.Target, Action = scheduled.Action });
                else if (scheduled.Action.Type == TileActionType.Attack) attacks.Add(scheduled);
                else remaining.Add(scheduled);
                unit.ActionsRemaining = Math.Max(0, unit.ActionsRemaining - 1);
            }
            ResolveTurnsAndPreparation(remaining);
            ResolveMoves(moves);
            ResolveAttacks(attacks);
        }

        private void ResolveTurnsAndPreparation(List<ScheduledAction> actions)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                TileBattleUnit unit = FindUnit(actions[i].UnitId); TileUnitAction action = actions[i].Action;
                if (unit == null || !unit.Active) continue;
                if (action.Type == TileActionType.Turn)
                { unit.Facing = action.Facing; Emit(TileBattleEventType.UnitTurned, unit.Id, message: "Unit " + unit.Id + " faces " + unit.Facing); }
                else if (action.Type == TileActionType.Brace)
                { unit.Braced = true; Emit(TileBattleEventType.UnitBlocked, unit.Id, message: "Unit " + unit.Id + " braces"); }
                else if (action.Type == TileActionType.Disengage)
                {
                    if (unit.State == TileUnitState.Engaged)
                    { unit.State = TileUnitState.Ready; Emit(TileBattleEventType.UnitDisengaged, unit.Id, message: "Unit " + unit.Id + " disengages"); }
                }
            }
        }

        private void RefreshEngagementStates()
        {
            for (int i = 0; i < Units.Count; i++)
            {
                TileBattleUnit unit = Units[i];
                if (!unit.Active || unit.State != TileUnitState.Engaged) continue;
                bool adjacentEnemy = Units.Exists(enemy => enemy.Active && enemy.Side != unit.Side &&
                    unit.Position.ManhattanDistance(enemy.Position) == 1);
                if (adjacentEnemy) continue;
                unit.State = TileUnitState.Ready;
                Emit(TileBattleEventType.UnitDisengaged, unit.Id, from: unit.Position, to: unit.Position,
                    message: "Engagement ended because no active enemy remains adjacent");
            }
        }

        private void ResolveMoves(List<MoveIntent> moves)
        {
            // Global ids place every left-side formation before every right-side formation.
            // Resolve corresponding local formation indices together and alternate the side
            // priority by tick, so sequential occupancy checks cannot create a permanent
            // left-army first-mover advantage.
            int preferredSide = (CommandRound + ResolutionTick) & 1;
            moves.Sort((a, b) =>
            {
                int localA = a.Unit.Id % 10000, localB = b.Unit.Id % 10000;
                int local = localA.CompareTo(localB); if (local != 0) return local;
                if (a.Unit.Side == b.Unit.Side) return a.Unit.Id.CompareTo(b.Unit.Id);
                return a.Unit.Side == preferredSide ? -1 : 1;
            });
            HashSet<int> resolved = new HashSet<int>();
            for (int i = 0; i < moves.Count; i++)
            {
                MoveIntent move = moves[i]; if (resolved.Contains(move.Unit.Id) || !move.Unit.Active) continue;
                if (!Grid.Contains(move.To) || move.From.ManhattanDistance(move.To) != 1)
                { Emit(TileBattleEventType.UnitBlocked, move.Unit.Id, from: move.From, to: move.To, message: "Invalid movement"); continue; }
                if (move.Unit.State == TileUnitState.Engaged)
                { Emit(TileBattleEventType.UnitBlocked, move.Unit.Id, message: "Engaged unit requires Disengage before moving"); continue; }
                MoveIntent reciprocal = moves.Find(item => item.Unit.Id != move.Unit.Id && item.From == move.To && item.To == move.From);
                if (reciprocal != null)
                {
                    resolved.Add(move.Unit.Id); resolved.Add(reciprocal.Unit.Id);
                    if (move.Unit.Side == reciprocal.Unit.Side)
                    { Block(move.Unit, reciprocal.Unit, "Friendly formations cannot swap tiles"); continue; }
                    ResolveCollision(move.Unit, reciprocal.Unit, move.From, move.To); continue;
                }
                List<MoveIntent> sameTarget = moves.FindAll(item => item.To == move.To && !resolved.Contains(item.Unit.Id));
                if (sameTarget.Count > 1)
                {
                    sameTarget.Sort((a, b) => a.Unit.Id.CompareTo(b.Unit.Id));
                    for (int c = 0; c < sameTarget.Count; c++) resolved.Add(sameTarget[c].Unit.Id);
                    ResolveContestedDestination(sameTarget, move.To); continue;
                }
                int occupantId = Grid.OccupantAt(move.To); TileBattleUnit occupant = FindUnit(occupantId);
                if (occupant != null && occupant.Active)
                {
                    resolved.Add(move.Unit.Id);
                    if (occupant.Side == move.Unit.Side)
                    {
                        TileCoord beyond = new TileCoord(move.To.X + (move.To.X - move.From.X), move.To.Y + (move.To.Y - move.From.Y));
                        bool claimed = moves.Exists(item => !resolved.Contains(item.Unit.Id) && item.Unit.Id != move.Unit.Id && item.To == beyond);
                        if (Grid.Contains(beyond) && Grid.OccupantAt(beyond) < 0 && !claimed)
                        {
                            ApplyThreatInterception(move.Unit, move.From, move.To);
                            if (!move.Unit.Active) continue;
                            ApplyThreatInterception(move.Unit, move.To, beyond);
                            if (!move.Unit.Active) continue;
                            Grid.SetOccupant(move.From, -1); Grid.SetOccupant(beyond, move.Unit.Id); move.Unit.Position = beyond;
                            move.Unit.Facing = FacingFromDelta(beyond.X - move.From.X, beyond.Y - move.From.Y);
                            Emit(TileBattleEventType.UnitMoved, move.Unit.Id, occupant.Id, move.From, beyond,
                                message: "Formation passes through friendly formation " + occupant.Id);
                        }
                        else Block(move.Unit, occupant, "Friendly formation blocks route");
                    }
                    else ResolveCollision(move.Unit, occupant, move.From, move.To);
                    continue;
                }
                resolved.Add(move.Unit.Id); ApplyThreatInterception(move.Unit, move.From, move.To);
                if (!move.Unit.Active) continue;
                Grid.SetOccupant(move.From, -1); Grid.SetOccupant(move.To, move.Unit.Id); move.Unit.Position = move.To;
                move.Unit.Facing = FacingFromDelta(move.To.X - move.From.X, move.To.Y - move.From.Y);
                Emit(TileBattleEventType.UnitMoved, move.Unit.Id, from: move.From, to: move.To, message: "Unit " + move.Unit.Id + " moved");
            }
        }

        private void ResolveContestedDestination(List<MoveIntent> contenders, TileCoord destination)
        {
            MoveIntent best = contenders[0]; int bestMass = best.Unit.EffectiveMass(Rules, best.Unit.Facing); bool tie = false;
            for (int i = 1; i < contenders.Count; i++)
            {
                int mass = contenders[i].Unit.EffectiveMass(Rules, contenders[i].Unit.Facing);
                if (mass > bestMass) { best = contenders[i]; bestMass = mass; tie = false; }
                else if (mass == bestMass) tie = true;
            }
            int occupied = Grid.OccupantAt(destination);
            if (tie || occupied >= 0)
            {
                for (int i = 0; i < contenders.Count; i++) Emit(TileBattleEventType.UnitBlocked, contenders[i].Unit.Id, to: destination, message: "Simultaneous destination conflict");
                EngageOpposing(contenders); return;
            }
            ApplyThreatInterception(best.Unit, best.From, destination);
            if (!best.Unit.Active) return;
            Grid.SetOccupant(best.From, -1); Grid.SetOccupant(destination, best.Unit.Id); best.Unit.Position = destination;
            Emit(TileBattleEventType.UnitMoved, best.Unit.Id, from: best.From, to: destination, message: "Greater mass wins destination");
            for (int i = 0; i < contenders.Count; i++) if (contenders[i] != best)
                Emit(TileBattleEventType.UnitBlocked, contenders[i].Unit.Id, to: destination, message: "Displaced by heavier simultaneous mover");
        }

        private void ResolveCollision(TileBattleUnit mover, TileBattleUnit defender, TileCoord origin, TileCoord destination)
        {
            int moverMass = mover.EffectiveMass(Rules, mover.Facing); int defenderMass = defender.EffectiveMass(Rules, Opposite(mover.Facing));
            if (moverMass * 1000 >= defenderMass * Rules.OverwhelmingMassPermille)
            {
                TileCoord pushTo = new TileCoord(defender.Position.X + (destination.X - origin.X), defender.Position.Y + (destination.Y - origin.Y));
                if (Grid.Contains(pushTo) && Grid.OccupantAt(pushTo) < 0)
                {
                    Grid.SetOccupant(defender.Position, -1); Grid.SetOccupant(pushTo, defender.Id); defender.Position = pushTo;
                    defender.Cohesion = Math.Max(0, defender.Cohesion - Rules.BreakthroughCohesionDamage);
                    Grid.SetOccupant(origin, -1); Grid.SetOccupant(destination, mover.Id); mover.Position = destination;
                    Emit(TileBattleEventType.UnitPushed, mover.Id, defender.Id, origin, pushTo, Rules.BreakthroughCohesionDamage, "Overwhelming mass breaks through");
                    return;
                }
            }
            if (moverMass * 1000 >= defenderMass * Rules.SimilarMassPermille)
            {
                TileCoord pushTo = new TileCoord(defender.Position.X + (destination.X - origin.X), defender.Position.Y + (destination.Y - origin.Y));
                if (Grid.Contains(pushTo) && Grid.OccupantAt(pushTo) < 0)
                {
                    Grid.SetOccupant(defender.Position, -1); Grid.SetOccupant(pushTo, defender.Id); defender.Position = pushTo;
                    defender.Cohesion = Math.Max(0, defender.Cohesion - Rules.PushCohesionDamage);
                    Grid.SetOccupant(origin, -1); Grid.SetOccupant(destination, mover.Id); mover.Position = destination;
                    Emit(TileBattleEventType.UnitPushed, mover.Id, defender.Id, destination, pushTo, Rules.PushCohesionDamage, "Heavier formation pushes defender");
                }
            }
            mover.State = TileUnitState.Engaged; defender.State = TileUnitState.Engaged;
            Emit(TileBattleEventType.UnitEngaged, mover.Id, defender.Id, origin, destination, 0, "Movement collision creates engagement");
        }

        private void ResolveAttacks(List<ScheduledAction> attacks)
        {
            Dictionary<int, int> damage = new Dictionary<int, int>();
            attacks.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
            for (int i = 0; i < attacks.Count; i++)
            {
                TileBattleUnit attacker = FindUnit(attacks[i].UnitId); if (attacker == null || !attacker.Active || attacker.AttackedThisRound) continue;
                TileUnitAction action = attacks[i].Action;
                TileBattleUnit defender = action.TargetUnitId >= 0
                    ? FindUnit(action.TargetUnitId)
                    : FindUnit(Grid.OccupantAt(action.Target));
                bool rangedAttack = attacker.Definition.Ranged && attacker.Ammunition > 0;
                int range = rangedAttack ? Math.Max(1, attacker.Definition.RangedRange) : 1;
                if (defender == null || !defender.Active || defender.Side == attacker.Side || attacker.Position.ManhattanDistance(defender.Position) > range)
                { Emit(TileBattleEventType.UnitBlocked, attacker.Id, defender != null ? defender.Id : -1,
                    attacker.Position, defender != null ? defender.Position : action.Target,
                    message: "Attack target moved or is outside current weapon range"); continue; }
                int raw = rangedAttack ? attacker.Definition.RangedDamage : attacker.Definition.MeleeDamage;
                if (raw <= 0) raw = Rules.BaseMeleeDamage;
                TileFacing incoming = FacingFromDelta(attacker.Position.X - defender.Position.X, attacker.Position.Y - defender.Position.Y);
                bool rearAttack = incoming == Opposite(defender.Facing);
                bool frontAttack = incoming == defender.Facing;
                if (rearAttack) raw = raw * Rules.RearDamagePermille / 1000;
                else if (!frontAttack) raw = raw * Rules.FlankDamagePermille / 1000;
                raw = raw * Math.Max(250, attacker.Cohesion) / 1000;
                int shieldEffectiveness = frontAttack ? defender.Definition.ShieldFrontEffectivenessPercent :
                    rearAttack ? 0 : defender.Definition.ShieldSideEffectivenessPercent;
                int effectiveShield = defender.Definition.ShieldPercent * Math.Max(0, Math.Min(100, shieldEffectiveness)) / 100;
                int effectiveArmor = Math.Min(80, defender.Definition.ArmorPercent + effectiveShield);
                int finalDamage = Math.Max(1, raw * (100 - effectiveArmor) / 100);
                damage[defender.Id] = damage.TryGetValue(defender.Id, out int existing) ? existing + finalDamage : finalDamage;
                attacker.AttackedThisRound = true;
                if (rangedAttack)
                {
                    attacker.Ammunition = Math.Max(0, attacker.Ammunition - 1);
                    Emit(TileBattleEventType.ProjectileLaunched, attacker.Id, defender.Id, attacker.Position, defender.Position,
                        finalDamage, attacker.Definition.DisplayName + " launches a projectile");
                }
                if (!rangedAttack) { attacker.State = TileUnitState.Engaged; defender.State = TileUnitState.Engaged; }
                Emit(TileBattleEventType.UnitAttacked, attacker.Id, defender.Id, attacker.Position, defender.Position,
                    finalDamage, "Attack committed simultaneously", rangedAttack);
            }
            List<int> ids = new List<int>(damage.Keys); ids.Sort();
            for (int i = 0; i < ids.Count; i++) ApplyDamage(FindUnit(ids[i]), damage[ids[i]], "simultaneous attacks");
        }

        private void ApplyThreatInterception(TileBattleUnit mover, TileCoord from, TileCoord to)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                TileBattleUnit enemy = Units[i]; if (!enemy.Active || enemy.Side == mover.Side) continue;
                bool leftThreat = Threatens(enemy, from); bool entersThreat = Threatens(enemy, to);
                if (!leftThreat && !entersThreat) continue;
                int control = enemy.Definition.WeaponControl == TileWeaponControl.Pike ? 1000 :
                    enemy.Definition.WeaponControl == TileWeaponControl.Spear ? 750 : enemy.Definition.WeaponControl == TileWeaponControl.Sword ? 250 : 150;
                if (mover.Definition.Cavalry && (enemy.Definition.WeaponControl == TileWeaponControl.Spear || enemy.Definition.WeaponControl == TileWeaponControl.Pike)) control += 250;
                int damage = Math.Max(1, enemy.Definition.MeleeDamage * control / 1000 * Rules.ThreatInterceptionDamagePermille / 1000);
                ApplyDamage(mover, damage, enemy.Definition.WeaponControl + " threat interception");
            }
        }

        private bool Threatens(TileBattleUnit unit, TileCoord target)
        {
            int dx = target.X - unit.Position.X, dy = target.Y - unit.Position.Y;
            int forward = unit.Facing == TileFacing.East ? dx : unit.Facing == TileFacing.West ? -dx : unit.Facing == TileFacing.North ? dy : -dy;
            int lateral = unit.Facing == TileFacing.East || unit.Facing == TileFacing.West ? Math.Abs(dy) : Math.Abs(dx);
            return forward > 0 && forward <= Math.Max(1, unit.Definition.FrontThreat) && lateral <= unit.Definition.SideThreat;
        }

        private void ApplyDamage(TileBattleUnit target, int amount, string reason)
        {
            if (target == null || !target.Active) return;
            target.Strength = Math.Max(0, target.Strength - amount); target.Morale = Math.Max(0, target.Morale - amount * 4);
            target.Cohesion = Math.Max(0, target.Cohesion - amount * 2);
            Emit(TileBattleEventType.UnitDamaged, target.Id, amount: amount, message: "Unit " + target.Id + " loses " + amount + " strength from " + reason);
            if (target.Strength <= 0) { target.State = TileUnitState.Destroyed; Grid.SetOccupant(target.Position, -1); }
            else if (target.Morale <= Rules.RoutMoraleThreshold)
            { target.State = TileUnitState.Routing; Emit(TileBattleEventType.UnitRouted, target.Id, message: "Unit " + target.Id + " routs"); }
        }

        private void UpdatePhase()
        {
            if (CommandRound <= Rules.VanguardRounds) Phase = TileBattlePhase.Vanguard;
            else if (CommandRound <= Rules.VanguardRounds + 2) { Phase = TileBattlePhase.MainDeployment; DeployMainArmy(); }
            else if (CommandRound < Rules.ReserveRound) Phase = TileBattlePhase.MainBattle;
            else if (CommandRound == Rules.ReserveRound) Phase = TileBattlePhase.Reserves;
            else Phase = TileBattlePhase.Decisive;
        }

        private void CommitGeneralReserves()
        {
            if (CommandRound < Rules.ReserveRound) return;
            CommitGeneralReservesForSide(0, LeftGeneral);
            CommitGeneralReservesForSide(1, RightGeneral);
        }

        private void CommitGeneralReservesForSide(int side, ITileBattleGeneral general)
        {
            if (general == null) return;
            List<int> commitments = general.ChooseReserveCommitments(Observe(side));
            for (int i = 0; i < commitments.Count; i++)
            {
                TileBattleUnit unit = FindUnit(commitments[i]);
                TileBattleObservation observation = Observe(side);
                int row = unit != null ? general.ChooseReserveDeploymentRow(observation, unit.Id) : Grid.Height / 2;
                if (CommitReserve(commitments[i], row))
                    Emit(TileBattleEventType.ReserveCommitted, commitments[i], to: unit.Position,
                        message: "General commits reserve " + commitments[i] + " for " + general.DebugState.CurrentPlan + " on row " + unit.Position.Y);
            }
        }

        private void DeployMainArmy()
        {
            DeployMainArmyForSide(0, LeftGeneral);
            DeployMainArmyForSide(1, RightGeneral);
        }

        private void DeployMainArmyForSide(int side, ITileBattleGeneral general)
        {
            List<TileBattleUnit> main = Units.FindAll(item => item.Side == side && !item.IsVanguard && !item.IsReserve);
            main.Sort(CompareMainDeploymentRole);
            TileBattlePlan plan = general != null && general.DebugState != null
                ? general.DebugState.CurrentPlan : TileBattlePlan.AttackCentre;
            Dictionary<int, TileCoord> slots = BuildMainDeploymentSlots(side, main, plan);
            for (int i = 0; i < main.Count; i++)
            {
                TileBattleUnit unit = main[i]; if (unit.Deployed ||
                    unit.DeploymentRound > 0 && CommandRound < unit.DeploymentRound) continue;
                TileCoord preferred = slots.TryGetValue(unit.Id, out TileCoord assigned) ? assigned : unit.Position;
                TileCoord candidate = FindDeploymentTile(unit.Side, preferred);
                if (!Grid.Contains(candidate)) continue;
                unit.Position = candidate; unit.Deployed = true; Grid.SetOccupant(candidate, unit.Id);
                Emit(TileBattleEventType.UnitDeployed, unit.Id, to: candidate,
                    message: "Main formation " + unit.Id + " deploys for " + plan + " at " + candidate);
            }
        }

        private static int CompareMainDeploymentRole(TileBattleUnit a, TileBattleUnit b)
        {
            int aRole = DeploymentRole(a), bRole = DeploymentRole(b);
            int role = aRole.CompareTo(bRole);
            if (role != 0) return role;
            int mass = b.Definition.BaseMass.CompareTo(a.Definition.BaseMass);
            return mass != 0 ? mass : a.Id.CompareTo(b.Id);
        }

        // Centre infantry first, protected ranged support second, then light troops and cavalry.
        private static int DeploymentRole(TileBattleUnit unit)
        {
            if (!unit.Definition.Cavalry && !unit.Definition.Ranged && unit.Definition.BaseMass >= 100) return 0;
            if (unit.Definition.Ranged && unit.Definition.BaseMass >= 100) return 1;
            if (!unit.Definition.Cavalry && !unit.Definition.Ranged) return 2;
            if (unit.Definition.Ranged && !unit.Definition.Cavalry) return 3;
            return 4;
        }

        private Dictionary<int, TileCoord> BuildMainDeploymentSlots(int side, List<TileBattleUnit> units, TileBattlePlan plan)
        {
            Dictionary<int, TileCoord> result = new Dictionary<int, TileCoord>();
            int frontX = side == 0 ? 1 : Grid.Width - 2;
            int rearX = side == 0 ? 0 : Grid.Width - 1;
            int centreIndex = 0, rangedIndex = 0, mobileIndex = 0;
            for (int i = 0; i < units.Count; i++)
            {
                TileBattleUnit unit = units[i]; int role = DeploymentRole(unit);
                bool mobile = role >= 2;
                int row;
                if (!mobile) row = CentreDeploymentRow(role == 1 ? rangedIndex++ : centreIndex++, Grid.Height);
                else row = MobileDeploymentRow(mobileIndex++, Grid.Height, plan);
                int x = unit.Definition.Ranged ? rearX : frontX;
                // Holding generals create a visibly deeper centre instead of one broad line.
                if (plan == TileBattlePlan.Hold && role == 0 && centreIndex > Math.Max(3, Grid.Height / 4) && (centreIndex & 1) == 0)
                    x = rearX;
                result[unit.Id] = new TileCoord(x, row);
            }
            return result;
        }

        private static int CentreDeploymentRow(int index, int height)
        {
            int centre = height / 2;
            if (index == 0) return centre;
            int distance = (index + 1) / 2;
            int row = centre + (index % 2 == 0 ? distance : -distance);
            return Math.Max(1, Math.Min(height - 2, row));
        }

        private static int MobileDeploymentRow(int index, int height, TileBattlePlan plan)
        {
            int margin = 1;
            if (plan == TileBattlePlan.FlankLeft) return Math.Max(margin, height - 2 - index);
            if (plan == TileBattlePlan.FlankRight) return Math.Min(height - 2, margin + index);
            // Centre attacks and holds keep balanced wings: first low, then high, moving inward.
            int depth = index / 2;
            return index % 2 == 0 ? Math.Min(height - 2, margin + depth) : Math.Max(margin, height - 2 - depth);
        }

        public bool CommitReserve(int unitId, int row)
        {
            TileBattleUnit unit = FindUnit(unitId); if (unit == null || !unit.IsReserve || unit.Deployed || CommandRound < Rules.ReserveRound) return false;
            TileCoord position = FindDeploymentTile(unit.Side, row); if (!Grid.Contains(position)) return false;
            unit.Position = position; unit.Deployed = true; unit.IsReserve = false; Grid.SetOccupant(position, unit.Id);
            Emit(TileBattleEventType.UnitDeployed, unit.Id, to: position, message: "Reserve formation " + unit.Id + " deploys"); return true;
        }

        private TileCoord FindDeploymentTile(int side, int preferredY)
        {
            int x = side == 0 ? 0 : Grid.Width - 1; preferredY = Math.Max(0, Math.Min(Grid.Height - 1, preferredY));
            for (int offset = 0; offset < Grid.Height; offset++)
            {
                int up = preferredY + offset; if (up < Grid.Height && Grid.OccupantAt(new TileCoord(x, up)) < 0) return new TileCoord(x, up);
                int down = preferredY - offset; if (down >= 0 && Grid.OccupantAt(new TileCoord(x, down)) < 0) return new TileCoord(x, down);
            }
            return new TileCoord(-1, -1);
        }

        private TileCoord FindDeploymentTile(int side, TileCoord preferred)
        {
            int minX = side == 0 ? 0 : Grid.Width * 2 / 3;
            int maxX = side == 0 ? Grid.Width / 3 : Grid.Width - 1;
            preferred = new TileCoord(Math.Max(minX, Math.Min(maxX, preferred.X)),
                Math.Max(0, Math.Min(Grid.Height - 1, preferred.Y)));
            int maximumRadius = Grid.Width + Grid.Height;
            for (int radius = 0; radius <= maximumRadius; radius++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int dx = radius - Math.Abs(dy);
                TileCoord first = new TileCoord(preferred.X + (side == 0 ? -dx : dx), preferred.Y + dy);
                if (first.X >= minX && first.X <= maxX && Grid.Contains(first) && Grid.OccupantAt(first) < 0) return first;
                if (dx == 0) continue;
                TileCoord second = new TileCoord(preferred.X + (side == 0 ? dx : -dx), preferred.Y + dy);
                if (second.X >= minX && second.X <= maxX && Grid.Contains(second) && Grid.OccupantAt(second) < 0) return second;
            }
            return new TileCoord(-1, -1);
        }

        private void EvaluateBattleEnd()
        {
            if (Result.Finished) return;
            int[] active = new int[2], start = new int[2], remaining = new int[2];
            for (int i = 0; i < Units.Count; i++)
            {
                TileBattleUnit unit = Units[i]; start[unit.Side] += unit.Definition.Strength; remaining[unit.Side] += Math.Max(0, unit.Strength);
                if (unit.Active && unit.State != TileUnitState.Routing) active[unit.Side]++;
            }
            if (active[0] == 0) Finish(1, "Left army had no active, non-routing formations remaining");
            else if (active[1] == 0) Finish(0, "Right army had no active, non-routing formations remaining");
            else if (start[0] > 0 && remaining[0] * 100 <= start[0] * (100 - Rules.CollapsePercent))
                Finish(1, "Left army reached the " + Rules.CollapsePercent + "% casualty collapse threshold");
            else if (start[1] > 0 && remaining[1] * 100 <= start[1] * (100 - Rules.CollapsePercent))
                Finish(0, "Right army reached the " + Rules.CollapsePercent + "% casualty collapse threshold");
        }

        private void Finish(int winningSide, string reason)
        {
            Result.Finished = true; Result.WinningSide = winningSide; Result.CommandRounds = CommandRound;
            Result.EndReason = reason; Phase = TileBattlePhase.Finished;
            Result.RemainingStrength.Clear();
            for (int i = 0; i < Units.Count; i++) Result.RemainingStrength[Units[i].Id] = Math.Max(0, Units[i].Strength);
            Emit(TileBattleEventType.BattleEnded, message: "Battle won by side " + winningSide + ": " + reason);
        }

        private int LeadingSide()
        {
            int left = 0, right = 0;
            for (int i = 0; i < Units.Count; i++) if (Units[i].Side == 0) left += Units[i].Strength + Units[i].Morale / 10; else right += Units[i].Strength + Units[i].Morale / 10;
            return left == right ? -1 : left > right ? 0 : 1;
        }

        private static TileOrderSet EmptyOrders(int side) => new TileOrderSet { Side = side };
        private TileBattleUnit FindUnit(int id) => id < 0 ? null : Units.Find(item => item.Id == id);
        private static TileFacing FacingFromDelta(int dx, int dy) => Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? TileFacing.East : TileFacing.West) : (dy >= 0 ? TileFacing.North : TileFacing.South);
        private static TileFacing Opposite(TileFacing facing) => (TileFacing)(((int)facing + 2) % 4);
        private void Block(TileBattleUnit first, TileBattleUnit second, string reason) => Emit(TileBattleEventType.UnitBlocked, first.Id, second != null ? second.Id : -1, message: reason);
        private void EngageOpposing(List<MoveIntent> contenders)
        {
            for (int i = 0; i < contenders.Count; i++) for (int j = i + 1; j < contenders.Count; j++)
                if (contenders[i].Unit.Side != contenders[j].Unit.Side)
                { contenders[i].Unit.State = TileUnitState.Engaged; contenders[j].Unit.State = TileUnitState.Engaged; }
        }

        private void Emit(TileBattleEventType type, int unitId = -1, int otherId = -1, TileCoord from = default(TileCoord),
            TileCoord to = default(TileCoord), int amount = 0, string message = null, bool rangedAttack = false)
        {
            Events.Add(new TileBattleEvent { CommandRound = CommandRound, Tick = ResolutionTick, Type = type,
                UnitId = unitId, OtherUnitId = otherId, From = from, To = to, Amount = amount,
                RangedAttack = rangedAttack, Message = message });
        }
    }
}
