using System;
using System.Collections.Generic;

namespace ProjectX.TileBattle
{
    public interface ITileBattleGeneral
    {
        TileGeneralDebugState DebugState { get; }
        TileOrderSet FormulateOrders(TileBattleObservation observation);
        List<int> ChooseReserveCommitments(TileBattleObservation observation);
        int ChooseReserveDeploymentRow(TileBattleObservation observation, int unitId);
    }

    public sealed class TileBattleObservation
    {
        public int Side;
        public bool IsAttacker;
        public int CommandRound;
        public TileBattlePhase Phase;
        public int Width;
        public int Height;
        public readonly List<TileObservedUnit> Units = new List<TileObservedUnit>();
    }

    public sealed class TileObservedUnit
    {
        public int Id, Side, Strength, Morale, Cohesion, Ammunition;
        public TileCoord Position;
        public TileFacing Facing;
        public TileUnitState State;
        public TileBattleUnitDefinition Definition;
        public bool IsReserve, Deployed;
    }

    public sealed class PersonalityTileGeneral : ITileBattleGeneral
    {
        private readonly TileGeneralPersonality personality;
        private TileBattlePlan currentPlan;
        private bool hasPlan;
        private int planStartRound;
        private int previousStrengthDifference;
        public TileGeneralDebugState DebugState { get; } = new TileGeneralDebugState();

        public PersonalityTileGeneral(TileGeneralPersonality personality)
        {
            this.personality = personality ?? new TileGeneralPersonality();
            DebugState.GeneralName = this.personality.Name;
        }

        public TileOrderSet FormulateOrders(TileBattleObservation observation)
        {
            DebugState.PlansConsidered.Clear(); DebugState.Threats.Clear(); DebugState.Opportunities.Clear(); DebugState.OrdersIssued.Clear();
            List<TilePlanScore> scores = ScorePlans(observation);
            scores.Sort((a, b) => { int score = b.Total.CompareTo(a.Total); return score != 0 ? score : a.Plan.CompareTo(b.Plan); });
            TileBattlePlan selected = scores[0].Plan;
            if (hasPlan)
            {
                TilePlanScore currentScore = scores.Find(item => item.Plan == currentPlan);
                int reassessmentInterval = personality.Competence < 40 ? 3 : personality.Competence < 70 ? 5 : 6;
                int requiredAdvantage = personality.Competence < 40 ? 8 : personality.Competence < 70 ? 14 : 20;
                bool mayReassess = observation.CommandRound - planStartRound >= reassessmentInterval;
                bool compellingAlternative = selected != currentPlan && currentScore != null &&
                    scores[0].Total >= currentScore.Total + requiredAdvantage;
                if (!mayReassess || !compellingAlternative) selected = currentPlan;
                DebugState.ChangeReason = selected != currentPlan
                    ? "Changed plan after a sustained reassessment window; the alternative now leads by " +
                        (scores[0].Total - currentScore.Total) + " score"
                    : "Maintaining the current plan until a clearly better alternative survives the reassessment interval";
            }
            else DebugState.ChangeReason = "Initial plan selected from army composition and battlefield shape";
            if (!hasPlan || selected != currentPlan) planStartRound = observation.CommandRound;
            currentPlan = selected; hasPlan = true; DebugState.CurrentPlan = selected; DebugState.PlanAge = observation.CommandRound - planStartRound;
            DebugState.PlansConsidered.AddRange(scores);
            TileOrderSet result = new TileOrderSet { Side = observation.Side, CommandRound = observation.CommandRound,
                Plan = selected, Reason = DebugState.ChangeReason };
            GenerateOrders(observation, result);
            return result;
        }

        public List<int> ChooseReserveCommitments(TileBattleObservation observation)
        {
            List<int> reserves = observation.Units.FindAll(item => item.Side == observation.Side && item.IsReserve && !item.Deployed && item.Strength > 0)
                .ConvertAll(item => item.Id);
            reserves.Sort(); if (reserves.Count == 0) return reserves;
            int own = 0, enemy = 0;
            for (int i = 0; i < observation.Units.Count; i++)
            {
                TileObservedUnit unit = observation.Units[i]; if (!unit.Deployed || unit.Strength <= 0) continue;
                if (unit.Side == observation.Side) own += unit.Strength; else enemy += unit.Strength;
            }
            bool underPressure = own * 100 < enemy * 85;
            int delay = personality.Patient > 0 || personality.Cautious > 0 ? 2 : personality.Aggressive > 0 ? 0 : 1;
            if (!underPressure && observation.CommandRound < 7 + delay) return new List<int>();
            int count = personality.Aggressive > personality.Patient ? reserves.Count : 1;
            if (count < reserves.Count) reserves.RemoveRange(count, reserves.Count - count);
            DebugState.OrdersIssued.Add("Commit reserves: " + string.Join(",", reserves));
            return reserves;
        }

        public int ChooseReserveDeploymentRow(TileBattleObservation observation, int unitId)
        {
            if (currentPlan == TileBattlePlan.FlankLeft) return observation.Height * 4 / 5;
            if (currentPlan == TileBattlePlan.FlankRight) return observation.Height / 5;
            int[] enemyStrengthByRow = new int[observation.Height];
            for (int i = 0; i < observation.Units.Count; i++)
            {
                TileObservedUnit unit = observation.Units[i];
                if (unit.Side != observation.Side && unit.Deployed && unit.Strength > 0 && unit.Position.Y >= 0 && unit.Position.Y < observation.Height)
                    enemyStrengthByRow[unit.Position.Y] += unit.Strength;
            }
            int row = observation.Height / 2;
            if (currentPlan == TileBattlePlan.Hold)
                for (int i = 0; i < enemyStrengthByRow.Length; i++) if (enemyStrengthByRow[i] > enemyStrengthByRow[row]) row = i;
            return row;
        }

        private List<TilePlanScore> ScorePlans(TileBattleObservation observation)
        {
            int own = 0, enemy = 0, cavalry = 0, enemyCavalry = 0, ownRanged = 0;
            int enemyUpper = 0, enemyLower = 0, enemyCentre = 0;
            List<TileObservedUnit> observedEnemies = new List<TileObservedUnit>();
            for (int i = 0; i < observation.Units.Count; i++)
            {
                TileObservedUnit unit = observation.Units[i];
                if (!unit.Deployed || unit.State == TileUnitState.Destroyed) continue;
                if (unit.Side == observation.Side)
                { own += unit.Strength; if (unit.Definition.Cavalry) cavalry++; if (unit.Definition.Ranged) ownRanged++; }
                else
                {
                    enemy += unit.Strength; if (unit.Definition.Cavalry) enemyCavalry++;
                    observedEnemies.Add(unit);
                }
            }
            observedEnemies.Sort((a, b) => { int y = a.Position.Y.CompareTo(b.Position.Y); return y != 0 ? y : a.Id.CompareTo(b.Id); });
            for (int i = 0; i < observedEnemies.Count; i++)
            {
                if (i < observedEnemies.Count / 3) enemyLower += observedEnemies[i].Strength;
                else if (i >= (observedEnemies.Count * 2 + 2) / 3) enemyUpper += observedEnemies[i].Strength;
                else enemyCentre += observedEnemies[i].Strength;
            }
            int advantage = own - enemy;
            int strengthDifference = advantage;
            int trend = hasPlan ? strengthDifference - previousStrengthDifference : 0;
            previousStrengthDifference = strengthDifference;
            int competence = Math.Max(10, personality.Competence);
            int weakestFlank = Math.Min(enemyUpper, enemyLower);
            int centreOpportunity = enemyCentre == 0 ? 50 : Math.Max(-40, (weakestFlank - enemyCentre) / 4);
            int upperOpportunity = Math.Max(-30, (enemyCentre - enemyUpper) / 8);
            int lowerOpportunity = Math.Max(-30, (enemyCentre - enemyLower) / 8);
            int lateralPreference = StableLateralPreference(personality.Name);
            DebugState.OwnStrength = own; DebugState.EnemyStrength = enemy; DebugState.StrengthTrend = trend;
            DebugState.Assessment = "Strength " + own + " vs " + enemy + "; trend " + trend +
                "; enemy line lower/centre/upper " + enemyLower + "/" + enemyCentre + "/" + enemyUpper;
            if (advantage < -own / 5) DebugState.Threats.Add("Army is materially outmatched");
            if (enemyCavalry > 0) DebugState.Threats.Add(enemyCavalry + " enemy cavalry formations threaten the flanks");
            if (enemyCentre < enemyUpper && enemyCentre < enemyLower) DebugState.Opportunities.Add("Enemy centre is weaker than both flanks");
            if (weakestFlank * 2 < Math.Max(1, enemyCentre)) DebugState.Opportunities.Add("One enemy flank is exposed");
            List<TilePlanScore> result = new List<TilePlanScore>
            {
                new TilePlanScore { Plan = TileBattlePlan.AttackCentre, BaseScore = 125,
                    PersonalityInfluence = personality.Bold + personality.Aggressive - personality.Cautious,
                    SituationInfluence = (advantage / 12 + centreOpportunity * 2) * competence / 50,
                    Reason = "Concentrate infantry and reserves against the enemy centre while flank units pin" },
                new TilePlanScore { Plan = TileBattlePlan.FlankLeft, BaseScore = 90,
                    PersonalityInfluence = personality.CavalryMinded + personality.Methodical + personality.Opportunistic + cavalry * 8,
                    SituationInfluence = (advantage / 20 + upperOpportunity + cavalry * 4) * competence / 50 + lateralPreference,
                    Reason = "Pin the centre while mobile formations turn the upper flank" },
                new TilePlanScore { Plan = TileBattlePlan.FlankRight, BaseScore = 90,
                    PersonalityInfluence = personality.CavalryMinded + personality.Methodical + personality.Opportunistic + cavalry * 8,
                    SituationInfluence = (advantage / 20 + lowerOpportunity + cavalry * 4) * competence / 50 - lateralPreference,
                    Reason = "Pin the centre while mobile formations turn the lower flank" }
            };
            if (!observation.IsAttacker)
                result.Add(new TilePlanScore { Plan = TileBattlePlan.Hold, BaseScore = 100,
                    PersonalityInfluence = personality.Defensive + personality.Patient + personality.Cautious - personality.Impatient(),
                    SituationInfluence = (-advantage / 12 + ownRanged * 5 + (trend < 0 ? 12 : 0)) * competence / 50,
                    Reason = "Defend ground, exploit ranged troops and retain a counterattack reserve" });
            return result;
        }

        private void GenerateOrders(TileBattleObservation observation, TileOrderSet set)
        {
            List<TileObservedUnit> enemies = observation.Units.FindAll(item => item.Side != observation.Side && item.Deployed && item.Strength > 0);
            enemies.Sort((a, b) => a.Id.CompareTo(b.Id));
            List<TileObservedUnit> allies = observation.Units.FindAll(item => item.Side == observation.Side && item.Deployed && item.Strength > 0 && !item.IsReserve);
            allies.Sort((a, b) => { int y = a.Position.Y.CompareTo(b.Position.Y); return y != 0 ? y : a.Id.CompareTo(b.Id); });
            List<TileObservedUnit> orderedEnemies = new List<TileObservedUnit>(enemies);
            orderedEnemies.Sort((a, b) => { int y = a.Position.Y.CompareTo(b.Position.Y); return y != 0 ? y : a.Id.CompareTo(b.Id); });
            Dictionary<int, TileObservedUnit> assignments = BuildAssignments(set.Plan, allies, orderedEnemies);
            HashSet<int> flankers = BuildFlankingFormationIds(set.Plan, allies);
            Dictionary<int, int> approachRows = BuildApproachRows(set.Plan, allies, orderedEnemies, observation.Height);
            Dictionary<int, TileCoord> holdSlots = BuildHoldFormationSlots(allies, observation.Side, observation.Width, observation.Height);
            HashSet<TileCoord> occupied = new HashSet<TileCoord>();
            HashSet<TileCoord> immobileFriendlies = new HashSet<TileCoord>();
            for (int u = 0; u < observation.Units.Count; u++)
            {
                TileObservedUnit observed = observation.Units[u];
                if (!observed.Deployed || observed.Strength <= 0) continue;
                occupied.Add(observed.Position);
                if (observed.Side == observation.Side && observed.State == TileUnitState.Engaged)
                    immobileFriendlies.Add(observed.Position);
            }
            List<HashSet<TileCoord>> stepReservations = new List<HashSet<TileCoord>>();
            for (int i = 0; i < observation.Units.Count; i++)
            {
                TileObservedUnit unit = observation.Units[i];
                if (unit.Side != observation.Side || !unit.Deployed || unit.Strength <= 0 || unit.IsReserve) continue;
                TileUnitOrder order = new TileUnitOrder { UnitId = unit.Id, Purpose = set.Plan.ToString() };
                int direction = observation.Side == (int)TileBattleSide.Left ? 1 : -1;
                bool hesitant = personality.Competence < 40 &&
                    (observation.CommandRound + unit.Id) % Math.Max(2, 6 - personality.Competence / 10) == 0;
                bool losesAssignment = personality.Competence < 40 && (observation.CommandRound + unit.Id * 3) % 5 == 0;
                if (losesAssignment) assignments.Remove(unit.Id);
                TileObservedUnit nearest = assignments.TryGetValue(unit.Id, out TileObservedUnit assigned) ? assigned : null;
                int nearestDistance = nearest != null ? unit.Position.ManhattanDistance(nearest.Position) : int.MaxValue;
                TileObservedUnit closestThreat = null; int closestThreatDistance = int.MaxValue;
                for (int e = 0; e < enemies.Count; e++)
                {
                    int distance = unit.Position.ManhattanDistance(enemies[e].Position);
                    if (distance < closestThreatDistance) { closestThreat = enemies[e]; closestThreatDistance = distance; }
                    if (distance < nearestDistance && (unit.State == TileUnitState.Engaged || nearest == null))
                    { nearest = enemies[e]; nearestDistance = distance; }
                }
                int attackRange = unit.Definition.Ranged && unit.Ammunition > 0 ? Math.Max(1, unit.Definition.RangedRange) : 1;
                if (hesitant && unit.State != TileUnitState.Engaged)
                {
                    order.Actions.Add(TileUnitAction.Wait());
                    set.Orders.Add(order); DebugState.OrdersIssued.Add("Unit " + unit.Id + ": hesitates while executing " + order.Purpose);
                    continue;
                }
                bool mobileSkirmisher = unit.Definition.Ranged && (unit.Definition.Cavalry || unit.Definition.BaseMass < 100);
                bool hasTakenLosses = unit.Strength < unit.Definition.Strength;
                bool noticesSkirmishThreat = NoticesOpportunity(unit, observation.CommandRound, 17);
                int dangerDistance = closestThreat != null && closestThreat.Definition != null
                    ? Math.Max(2, closestThreat.Definition.Actions + 1) : 2;
                if (mobileSkirmisher && unit.State != TileUnitState.Engaged && closestThreat != null &&
                    closestThreat.Definition != null && !closestThreat.Definition.Cavalry && noticesSkirmishThreat &&
                    closestThreatDistance <= (personality.Competence < 40 && !hasTakenLosses ? 2 : dangerDistance))
                {
                    order.Purpose = "Skirmish withdrawal";
                    if (personality.Competence < 40 && !hasTakenLosses && unit.Definition.Actions > 1)
                        order.Actions.Add(TileUnitAction.Wait());
                    if (closestThreatDistance <= attackRange) order.Actions.Add(TileUnitAction.Attack(closestThreat.Id, closestThreat.Position));
                    TileCoord planned = unit.Position;
                    for (int action = order.Actions.Count; action < unit.Definition.Actions; action++)
                    {
                        int away = Math.Sign(planned.X - closestThreat.Position.X);
                        if (away == 0) away = -direction;
                        TileCoord next = new TileCoord(planned.X + away, planned.Y);
                        while (stepReservations.Count <= action) stepReservations.Add(new HashSet<TileCoord>());
                        if (next.X < 0 || next.X >= observation.Width || occupied.Contains(next) || stepReservations[action].Contains(next))
                            next = FindDetourStep(planned, planned.Y, unit.Id, observation.Width, observation.Height,
                                occupied, stepReservations[action]);
                        if (next == planned) { order.Actions.Add(TileUnitAction.Wait()); break; }
                        order.Actions.Add(TileUnitAction.Move(next)); stepReservations[action].Add(next); planned = next;
                    }
                    set.Orders.Add(order); DebugState.OrdersIssued.Add("Unit " + unit.Id + ": attacks if able and evades infantry " + closestThreat.Id);
                    continue;
                }
                if (unit.State != TileUnitState.Engaged && nearest != null &&
                    CanAttemptEncirclement(unit) && IsPinnedByAlly(nearest, unit.Id, allies) &&
                    TryFindEncirclementTile(nearest, observation.Width, observation.Height, occupied, out TileCoord encirclementTile))
                {
                    order.Purpose = "Encircle unit " + nearest.Id;
                    TileCoord planned = unit.Position;
                    for (int action = 0; action < unit.Definition.Actions; action++)
                    {
                        if (planned == encirclementTile || IsSideOrRearPosition(planned, nearest))
                        { order.Actions.Add(TileUnitAction.Attack(nearest.Id, nearest.Position)); break; }
                        while (stepReservations.Count <= action) stepReservations.Add(new HashSet<TileCoord>());
                        TileCoord next = StepTowardEncirclement(planned, encirclementTile, observation.Width,
                            observation.Height, occupied, stepReservations[action]);
                        if (next == planned) { order.Actions.Add(TileUnitAction.Wait()); break; }
                        order.Actions.Add(TileUnitAction.Move(next)); stepReservations[action].Add(next); planned = next;
                    }
                    set.Orders.Add(order); DebugState.OrdersIssued.Add("Unit " + unit.Id +
                        ": exploits an open side/rear tile around pinned enemy " + nearest.Id);
                    continue;
                }
                if (nearest != null && (unit.State == TileUnitState.Engaged || nearestDistance <= attackRange))
                {
                    order.Actions.Add(TileUnitAction.Attack(nearest.Id, nearest.Position));
                    set.Orders.Add(order); DebugState.OrdersIssued.Add("Unit " + unit.Id + ": attack " + nearest.Id);
                    continue;
                }
                if (set.Plan == TileBattlePlan.Hold)
                {
                    order.Purpose = "Form defensive line and hold";
                    TileCoord planned = unit.Position;
                    TileCoord slot = holdSlots.TryGetValue(unit.Id, out TileCoord assignedSlot) ? assignedSlot : unit.Position;
                    for (int action = 0; action < unit.Definition.Actions; action++)
                    {
                        if (planned == slot)
                        { order.Actions.Add(TileUnitAction.Brace()); break; }
                        TileCoord next = planned.X != slot.X
                            ? new TileCoord(planned.X + Math.Sign(slot.X - planned.X), planned.Y)
                            : new TileCoord(planned.X, planned.Y + Math.Sign(slot.Y - planned.Y));
                        while (stepReservations.Count <= action) stepReservations.Add(new HashSet<TileCoord>());
                        if (immobileFriendlies.Contains(next) || stepReservations[action].Contains(next))
                            next = FindDetourStep(planned, slot.Y, unit.Id, observation.Width, observation.Height,
                                occupied, stepReservations[action]);
                        if (next == planned) { order.Actions.Add(TileUnitAction.Brace()); break; }
                        order.Actions.Add(TileUnitAction.Move(next)); stepReservations[action].Add(next); planned = next;
                    }
                    if (order.Actions.Count == 0) order.Actions.Add(TileUnitAction.Brace());
                }
                else
                {
                    TileCoord planned = unit.Position;
                    int approachRow = approachRows.TryGetValue(unit.Id, out int assignedRow) ? assignedRow : unit.Position.Y;
                    bool noticesFriendlyBlockage = NoticesOpportunity(unit, observation.CommandRound, 31);
                    int yieldRow = noticesFriendlyBlockage
                        ? FindYieldRow(unit, allies, direction, observation.Height, occupied) : unit.Position.Y;
                    int yieldDelay = yieldRow != unit.Position.Y && personality.Competence < 40 && !hasTakenLosses ? 1 : 0;
                    for (int action = 0; action < unit.Definition.Actions; action++)
                    {
                        TileObservedUnit target = nearest; int distance = target != null ? planned.ManhattanDistance(target.Position) : int.MaxValue;
                        if (target != null && distance <= attackRange)
                        { order.Actions.Add(TileUnitAction.Attack(target.Id, target.Position)); break; }
                        TileCoord next;
                        bool executeFlank = flankers.Contains(unit.Id);
                        if (yieldDelay > 0 && action == 0)
                        { order.Actions.Add(TileUnitAction.Wait()); continue; }
                        if (action == yieldDelay && yieldRow != unit.Position.Y) next = new TileCoord(planned.X, yieldRow);
                        else if (executeFlank && planned.Y != approachRow) next = new TileCoord(planned.X, planned.Y + Math.Sign(approachRow - planned.Y));
                        else if (target != null && planned.X != target.Position.X) next = new TileCoord(planned.X + Math.Sign(target.Position.X - planned.X), planned.Y);
                        else if (target != null && planned.Y != target.Position.Y) next = new TileCoord(planned.X, planned.Y + Math.Sign(target.Position.Y - planned.Y));
                        else next = new TileCoord(planned.X + direction, planned.Y);
                        while (stepReservations.Count <= action) stepReservations.Add(new HashSet<TileCoord>());
                        if (immobileFriendlies.Contains(next) || stepReservations[action].Contains(next))
                            next = FindDetourStep(planned, approachRow, unit.Id, observation.Width, observation.Height,
                                occupied, stepReservations[action]);
                        order.Actions.Add(TileUnitAction.Move(next)); planned = next;
                        stepReservations[action].Add(next);
                    }
                }
                while (order.Actions.Count > unit.Definition.Actions) order.Actions.RemoveAt(order.Actions.Count - 1);
                set.Orders.Add(order); DebugState.OrdersIssued.Add("Unit " + unit.Id + ": " + order.Purpose +
                    (nearest != null ? " targeting " + nearest.Id : " without target"));
            }
        }

        private static Dictionary<int, TileObservedUnit> BuildAssignments(TileBattlePlan plan, List<TileObservedUnit> allies,
            List<TileObservedUnit> enemies)
        {
            Dictionary<int, TileObservedUnit> result = new Dictionary<int, TileObservedUnit>();
            if (enemies.Count == 0) return result;
            TileObservedUnit lower = enemies[0], upper = enemies[enemies.Count - 1];
            for (int i = 0; i < allies.Count; i++)
            {
                TileObservedUnit ally = allies[i]; TileObservedUnit target;
                if (plan == TileBattlePlan.AttackCentre)
                    target = FrontLineTarget(enemies, ally.Position.Y);
                else if (plan == TileBattlePlan.FlankLeft)
                    target = IsFlankingFormation(plan, ally, i, allies.Count) ? upper : FrontLineTarget(enemies, ally.Position.Y);
                else if (plan == TileBattlePlan.FlankRight)
                    target = IsFlankingFormation(plan, ally, i, allies.Count) ? lower : FrontLineTarget(enemies, ally.Position.Y);
                else
                    target = FrontLineTarget(enemies, ally.Position.Y);
                result[ally.Id] = target;
            }
            return result;
        }

        private static TileObservedUnit FrontLineTarget(List<TileObservedUnit> enemies, int lane)
        {
            TileObservedUnit best = enemies[0]; int bestDistance = Math.Abs(best.Position.Y - lane);
            for (int i = 1; i < enemies.Count; i++)
            {
                int distance = Math.Abs(enemies[i].Position.Y - lane);
                if (distance < bestDistance) { best = enemies[i]; bestDistance = distance; }
            }
            return best;
        }

        private static HashSet<int> BuildFlankingFormationIds(TileBattlePlan plan, List<TileObservedUnit> allies)
        {
            HashSet<int> result = new HashSet<int>();
            for (int i = 0; i < allies.Count; i++)
                if (IsFlankingFormation(plan, allies[i], i, allies.Count)) result.Add(allies[i].Id);
            return result;
        }

        private static Dictionary<int, int> BuildApproachRows(TileBattlePlan plan, List<TileObservedUnit> allies,
            List<TileObservedUnit> enemies, int height)
        {
            Dictionary<int, int> result = new Dictionary<int, int>();
            for (int i = 0; i < allies.Count; i++) result[allies[i].Id] = allies[i].Position.Y;
            HashSet<int> flankers = BuildFlankingFormationIds(plan, allies);
            if (flankers.Count == 0 || enemies.Count == 0) return result;
            List<TileObservedUnit> orderedFlankers = allies.FindAll(item => flankers.Contains(item.Id));
            orderedFlankers.Sort((a, b) => { int cavalry = b.Definition.Cavalry.CompareTo(a.Definition.Cavalry);
                return cavalry != 0 ? cavalry : a.Id.CompareTo(b.Id); });
            int edge = plan == TileBattlePlan.FlankLeft
                ? Math.Min(height - 1, enemies[enemies.Count - 1].Position.Y + 1)
                : Math.Max(0, enemies[0].Position.Y - 1);
            int inward = plan == TileBattlePlan.FlankLeft ? -1 : 1;
            for (int i = 0; i < orderedFlankers.Count; i++)
                result[orderedFlankers[i].Id] = Math.Max(0, Math.Min(height - 1, edge + inward * i));
            return result;
        }

        private static Dictionary<int, TileCoord> BuildHoldFormationSlots(List<TileObservedUnit> allies, int side,
            int width, int height)
        {
            Dictionary<int, TileCoord> result = new Dictionary<int, TileCoord>();
            if (allies.Count == 0) return result;
            List<TileObservedUnit> ordered = new List<TileObservedUnit>(allies);
            ordered.Sort((a, b) => a.Id.CompareTo(b.Id));
            int frontage = Math.Max(3, Math.Min(Math.Max(3, height - 6),
                (int)Math.Ceiling(Math.Sqrt(ordered.Count * 2.0))));
            int firstRow = Math.Max(1, (height - frontage) / 2);
            int frontX = side == (int)TileBattleSide.Left ? width / 3 : width * 2 / 3;
            int rearDirection = side == (int)TileBattleSide.Left ? -1 : 1;
            for (int i = 0; i < ordered.Count; i++)
            {
                int rank = i / frontage;
                int row = firstRow + i % frontage;
                result[ordered[i].Id] = new TileCoord(Math.Max(0, Math.Min(width - 1, frontX + rearDirection * rank)), row);
            }
            return result;
        }

        private bool CanAttemptEncirclement(TileObservedUnit unit)
        {
            if (unit.Definition == null || unit.Definition.Ranged && unit.Ammunition > 0) return false;
            return unit.Definition.Cavalry || unit.Definition.BaseMass < 100 || personality.Opportunistic >= 30;
        }

        private static bool IsPinnedByAlly(TileObservedUnit target, int maneuveringUnitId, List<TileObservedUnit> allies)
        {
            return allies.Exists(ally => ally.Id != maneuveringUnitId && ally.Strength > 0 &&
                ally.Position.ManhattanDistance(target.Position) == 1);
        }

        private static bool TryFindEncirclementTile(TileObservedUnit target, int width, int height,
            HashSet<TileCoord> occupied, out TileCoord destination)
        {
            TileCoord forward = FacingVector(target.Facing);
            TileCoord[] offsets =
            {
                new TileCoord(-forward.X, -forward.Y),
                new TileCoord(-forward.Y, forward.X),
                new TileCoord(forward.Y, -forward.X)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                TileCoord candidate = new TileCoord(target.Position.X + offsets[i].X, target.Position.Y + offsets[i].Y);
                if (candidate.X >= 0 && candidate.X < width && candidate.Y >= 0 && candidate.Y < height && !occupied.Contains(candidate))
                { destination = candidate; return true; }
            }
            destination = target.Position; return false;
        }

        private static TileCoord StepTowardEncirclement(TileCoord from, TileCoord destination, int width, int height,
            HashSet<TileCoord> occupied, HashSet<TileCoord> reserved)
        {
            TileCoord[] candidates =
            {
                new TileCoord(from.X + Math.Sign(destination.X - from.X), from.Y),
                new TileCoord(from.X, from.Y + Math.Sign(destination.Y - from.Y))
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                TileCoord candidate = candidates[i];
                if (candidate != from && candidate.X >= 0 && candidate.X < width && candidate.Y >= 0 && candidate.Y < height &&
                    !occupied.Contains(candidate) && !reserved.Contains(candidate)) return candidate;
            }
            return from;
        }

        private static TileCoord FacingVector(TileFacing facing)
        {
            if (facing == TileFacing.East) return new TileCoord(1, 0);
            if (facing == TileFacing.West) return new TileCoord(-1, 0);
            if (facing == TileFacing.North) return new TileCoord(0, 1);
            return new TileCoord(0, -1);
        }

        private static bool IsSideOrRearPosition(TileCoord position, TileObservedUnit target)
        {
            if (position.ManhattanDistance(target.Position) != 1) return false;
            TileCoord forward = FacingVector(target.Facing);
            int dx = position.X - target.Position.X, dy = position.Y - target.Position.Y;
            return dx != forward.X || dy != forward.Y;
        }

        private static TileCoord FindDetourStep(TileCoord from, int preferredRow, int unitId, int width, int height,
            HashSet<TileCoord> occupied, HashSet<TileCoord> reserved)
        {
            int preferredDirection = preferredRow == from.Y ? ((unitId & 1) == 0 ? 1 : -1) : Math.Sign(preferredRow - from.Y);
            int[] directions = { preferredDirection, -preferredDirection };
            for (int i = 0; i < directions.Length; i++)
            {
                TileCoord candidate = new TileCoord(from.X, from.Y + directions[i]);
                if (candidate.X >= 0 && candidate.X < width && candidate.Y >= 0 && candidate.Y < height &&
                    !occupied.Contains(candidate) && !reserved.Contains(candidate)) return candidate;
            }
            return from;
        }

        private static int FindYieldRow(TileObservedUnit unit, List<TileObservedUnit> allies, int forwardDirection,
            int height, HashSet<TileCoord> occupied)
        {
            if (unit.Definition == null || unit.Definition.Cavalry || unit.Definition.BaseMass >= 100 || unit.State == TileUnitState.Engaged)
                return unit.Position.Y;
            bool blocksLineInfantry = false;
            for (int i = 0; i < allies.Count; i++)
            {
                TileObservedUnit ally = allies[i]; if (ally.Id == unit.Id || ally.Definition == null || ally.Definition.BaseMass < 100) continue;
                bool behind = forwardDirection > 0 ? ally.Position.X < unit.Position.X : ally.Position.X > unit.Position.X;
                if (behind && ally.Position.Y == unit.Position.Y && Math.Abs(ally.Position.X - unit.Position.X) <= 2)
                { blocksLineInfantry = true; break; }
            }
            if (!blocksLineInfantry) return unit.Position.Y;
            int firstDirection = (unit.Id & 1) == 0 ? 1 : -1;
            int[] directions = { firstDirection, -firstDirection };
            for (int i = 0; i < directions.Length; i++)
            {
                int row = unit.Position.Y + directions[i];
                if (row >= 0 && row < height && !occupied.Contains(new TileCoord(unit.Position.X, row))) return row;
            }
            return unit.Position.Y;
        }

        private bool NoticesOpportunity(TileObservedUnit unit, int commandRound, int salt)
        {
            if (unit.Definition != null && unit.Strength < unit.Definition.Strength) return true;
            int competence = Math.Max(0, Math.Min(100, personality.Competence));
            if (competence >= 50) return true;
            unchecked
            {
                uint hash = 2166136261u;
                string value = (personality.Name ?? string.Empty) + "|" + unit.Id + "|" + commandRound + "|" + salt;
                for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 16777619u; }
                return hash % 100u < (uint)competence;
            }
        }

        private static bool IsFlankingFormation(TileBattlePlan plan, TileObservedUnit ally, int index, int allyCount)
        {
            if (plan != TileBattlePlan.FlankLeft && plan != TileBattlePlan.FlankRight) return false;
            if (ally.Definition.Cavalry) return true;
            return plan == TileBattlePlan.FlankLeft
                ? index >= Math.Max(1, allyCount * 2 / 3)
                : index < Math.Max(1, allyCount / 3);
        }

        private static int StableLateralPreference(string name)
        {
            unchecked
            {
                uint hash = 2166136261;
                string value = name ?? string.Empty;
                for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 16777619; }
                return (hash & 1) == 0 ? -2 : 2;
            }
        }
    }

    internal static class TileGeneralPersonalityExtensions
    {
        public static int Impatient(this TileGeneralPersonality personality) => Math.Max(0, personality.Aggressive / 2 - personality.Patient / 2);
    }
}
