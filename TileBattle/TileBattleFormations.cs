using System;
using System.Collections.Generic;

namespace ProjectX.TileBattle
{
    public enum TileFormationShape { Line, Compact, Column }
    public enum TileFormationOrderType { Advance, Hold, Support, Screen, Flank, Regroup }
    public enum TileFormationCohesionState { Intact, Stretched, Dispersed, Regrouping }
    public enum TileBattleSector { Left, Centre, Right }

    [Serializable]
    public sealed class TileFormationSlot
    {
        public int UnitId;
        public TileCoord Offset;
        public TileCoord PreferredPosition;
    }

    [Serializable]
    public sealed class TileCommandFormation
    {
        public int Id;
        public int Side;
        public string GeneralName;
        public string Name;
        public TileFormationShape Shape;
        public TileFormationOrderType Order;
        public TileFormationCohesionState Cohesion;
        public TileBattleSector Sector;
        public TileCoord Anchor;
        public TileCoord Objective;
        public TileFacing Facing;
        public int SupportFormationId = -1;
        public int Strength;
        public bool Engaged;
        public readonly List<int> MemberUnitIds = new List<int>();
        public readonly List<TileFormationSlot> Slots = new List<TileFormationSlot>();

        public TileFormationSlot SlotFor(int unitId) => Slots.Find(slot => slot.UnitId == unitId);
        public bool Active => MemberUnitIds.Count > 0;
    }

    /// <summary>
    /// Round-boundary formation planner. Formations provide soft destinations to the existing
    /// per-card AI; they never move, attack, pathfind, or take damage as composite entities.
    /// </summary>
    public sealed class TileFormationCommandSystem
    {
        private readonly List<TileCommandFormation> formations = new List<TileCommandFormation>();
        private int side = -1;
        private int lastPlannedRound = -1;
        public IReadOnlyList<TileCommandFormation> Formations => formations;

        public void Update(TileBattleObservation observation, TileBattlePlan plan, TileGeneralPersonality personality)
        {
            if (observation == null || observation.CommandRound == lastPlannedRound) return;
            lastPlannedRound = observation.CommandRound;
            if (side != observation.Side || MembershipNeedsRebuild(observation)) Build(observation, personality);
            RemoveCasualties(observation);
            Evaluate(observation);
            AssignOrders(observation, plan, personality);
            RebuildSlots(observation);
        }

        public void InfluenceOrders(TileBattleObservation observation, TileOrderSet set)
        {
            if (observation == null || set == null) return;
            HashSet<TileCoord> occupied = new HashSet<TileCoord>();
            for (int i = 0; i < observation.Units.Count; i++)
                if (observation.Units[i].Deployed && observation.Units[i].Strength > 0)
                    occupied.Add(observation.Units[i].Position);

            for (int i = 0; i < set.Orders.Count; i++)
            {
                TileUnitOrder order = set.Orders[i];
                TileCommandFormation formation = FormationFor(order.UnitId);
                TileObservedUnit unit = observation.Units.Find(item => item.Id == order.UnitId);
                if (formation == null || unit == null) continue;
                TileFormationSlot slot = formation.SlotFor(unit.Id);
                order.FormationId = formation.Id;
                order.FormationOrder = formation.Order;
                order.FormationAnchor = formation.Anchor;
                order.FormationObjective = formation.Objective;
                order.PreferredFormationPosition = slot != null ? slot.PreferredPosition : formation.Anchor;
                order.FormationShape = formation.Shape;
                order.Purpose = "F" + formation.Id + " " + formation.Order + " " + formation.Sector + " | " + order.Purpose;

                // Immediate combat remains wholly local. Soft slots only redirect movement when
                // the member is not engaged and is meaningfully separated from its preferred area.
                if (unit.State == TileUnitState.Engaged) continue;
                TileCoord preferred = order.PreferredFormationPosition;
                int tolerance = formation.Order == TileFormationOrderType.Regroup ? 0 : 1;
                if (unit.Position.ManhattanDistance(preferred) <= tolerance) preferred = formation.Objective;
                TileCoord planned = unit.Position;
                for (int a = 0; a < order.Actions.Count; a++)
                {
                    TileUnitAction action = order.Actions[a];
                    if (action.Type != TileActionType.Move && action.Type != TileActionType.Charge) continue;
                    TileCoord next = StepToward(planned, preferred);
                    next = NearbyValid(next, planned, preferred, observation, occupied);
                    if (next == planned) continue; // Let the original local path attempt degrade gracefully.
                    action.Target = next;
                    planned = next;
                }
            }
        }

        public void WriteDebug(TileGeneralDebugState debug)
        {
            if (debug == null) return;
            debug.Formations.Clear();
            for (int i = 0; i < formations.Count; i++)
            {
                TileCommandFormation f = formations[i];
                debug.Formations.Add("F" + f.Id + " " + f.Name + " [" + f.MemberUnitIds.Count + "] " +
                    f.Shape + " / " + f.Order + " / " + f.Cohesion + " anchor=" + f.Anchor +
                    " objective=" + f.Objective + " strength=" + f.Strength);
            }
        }

        private void Build(TileBattleObservation observation, TileGeneralPersonality personality)
        {
            formations.Clear(); side = observation.Side;
            List<TileObservedUnit> candidates = observation.Units.FindAll(unit => unit.Side == side && unit.Strength > 0);
            candidates.Sort((a, b) => { int role = RoleKey(a).CompareTo(RoleKey(b)); return role != 0 ? role : a.Id.CompareTo(b.Id); });
            int nextId = side * 100 + 1;
            for (int index = 0; index < candidates.Count;)
            {
                TileObservedUnit first = candidates[index]; string key = RoleKey(first);
                int maximum = first.Definition != null && first.Definition.FormationType == TileFormationType.Phalanx ? 12 :
                    first.Definition != null && first.Definition.Cavalry ? 2 : 4;
                TileCommandFormation formation = new TileCommandFormation { Id = nextId++, Side = side,
                    GeneralName = personality != null ? personality.Name : "General", Name = FormationName(first, formations),
                    Shape = DefaultShape(first), Facing = side == 0 ? TileFacing.East : TileFacing.West };
                while (index < candidates.Count && formation.MemberUnitIds.Count < maximum && RoleKey(candidates[index]) == key)
                    formation.MemberUnitIds.Add(candidates[index++].Id);
                formations.Add(formation);
            }
        }

        private bool MembershipNeedsRebuild(TileBattleObservation observation)
        {
            int known = 0, actual = 0;
            for (int i = 0; i < formations.Count; i++) known += formations[i].MemberUnitIds.Count;
            for (int i = 0; i < observation.Units.Count; i++)
                if (observation.Units[i].Side == observation.Side && observation.Units[i].Strength > 0) actual++;
            if (formations.Count == 0 || actual > known) return true; // Includes reinforcements.
            return false;
        }

        private void RemoveCasualties(TileBattleObservation observation)
        {
            HashSet<int> alive = new HashSet<int>();
            for (int i = 0; i < observation.Units.Count; i++)
                if (observation.Units[i].Side == side && observation.Units[i].Strength > 0) alive.Add(observation.Units[i].Id);
            for (int f = formations.Count - 1; f >= 0; f--)
            {
                formations[f].MemberUnitIds.RemoveAll(id => !alive.Contains(id));
                if (!formations[f].Active) formations.RemoveAt(f);
            }
        }

        private void Evaluate(TileBattleObservation observation)
        {
            for (int f = 0; f < formations.Count; f++)
            {
                TileCommandFormation formation = formations[f]; int x = 0, y = 0, count = 0, strength = 0, isolated = 0;
                formation.Engaged = false;
                for (int i = 0; i < formation.MemberUnitIds.Count; i++)
                {
                    TileObservedUnit member = observation.Units.Find(unit => unit.Id == formation.MemberUnitIds[i]);
                    if (member == null || !member.Deployed || member.Strength <= 0) continue;
                    x += member.Position.X; y += member.Position.Y; strength += member.Strength; count++;
                    formation.Engaged |= member.State == TileUnitState.Engaged;
                    int neighbours = 0;
                    for (int j = 0; j < formation.MemberUnitIds.Count; j++)
                    {
                        TileObservedUnit other = observation.Units.Find(unit => unit.Id == formation.MemberUnitIds[j]);
                        if (other != null && other.Id != member.Id && member.Position.ManhattanDistance(other.Position) <= 2) neighbours++;
                    }
                    if (formation.MemberUnitIds.Count > 1 && neighbours == 0) isolated++;
                }
                if (count == 0) continue;
                formation.Anchor = new TileCoord(x / count, y / count); formation.Strength = strength;
                int spread = 0;
                for (int i = 0; i < formation.MemberUnitIds.Count; i++)
                {
                    TileObservedUnit member = observation.Units.Find(unit => unit.Id == formation.MemberUnitIds[i]);
                    if (member != null && member.Deployed && member.Strength > 0) spread += member.Position.ManhattanDistance(formation.Anchor);
                }
                int average = spread / count;
                formation.Cohesion = isolated * 2 >= count || average >= 4 ? TileFormationCohesionState.Dispersed :
                    average >= 2 ? TileFormationCohesionState.Stretched : TileFormationCohesionState.Intact;
            }
        }

        private void AssignOrders(TileBattleObservation observation, TileBattlePlan plan, TileGeneralPersonality personality)
        {
            TileBattleSector focus = plan == TileBattlePlan.FlankLeft ? TileBattleSector.Left :
                plan == TileBattlePlan.FlankRight ? TileBattleSector.Right : TileBattleSector.Centre;
            formations.Sort((a, b) => a.Id.CompareTo(b.Id));
            int focused = Math.Max(1, (formations.Count + 1) / 2), assignedFocus = 0;
            TileCommandFormation lead = null;
            for (int i = 0; i < formations.Count; i++)
            {
                TileCommandFormation f = formations[i]; TileObservedUnit sample = observation.Units.Find(u => f.MemberUnitIds.Contains(u.Id));
                bool cavalry = sample != null && sample.Definition != null && sample.Definition.Cavalry;
                bool ranged = sample != null && sample.Definition != null && sample.Definition.Ranged;
                f.Sector = plan == TileBattlePlan.Hold ? SectorForRow(f.Anchor.Y, observation.Height) :
                    assignedFocus++ < focused ? focus : OppositeSupportSector(focus, i);
                if (f.Cohesion == TileFormationCohesionState.Dispersed && !f.Engaged)
                { f.Order = TileFormationOrderType.Regroup; f.Cohesion = TileFormationCohesionState.Regrouping; }
                else if (plan == TileBattlePlan.Hold)
                    f.Order = ranged ? TileFormationOrderType.Screen : TileFormationOrderType.Hold;
                else if (cavalry)
                    f.Order = TileFormationOrderType.Flank;
                else if (ranged)
                    f.Order = TileFormationOrderType.Screen;
                else if (lead == null || f.Sector == focus)
                { f.Order = TileFormationOrderType.Advance; if (lead == null) lead = f; }
                else { f.Order = TileFormationOrderType.Support; f.SupportFormationId = lead != null ? lead.Id : -1; }
                f.Objective = ObjectiveFor(f, observation);
            }
        }

        private void RebuildSlots(TileBattleObservation observation)
        {
            for (int f = 0; f < formations.Count; f++)
            {
                TileCommandFormation formation = formations[f]; formation.Slots.Clear();
                TileCoord destinationAnchor = formation.Order == TileFormationOrderType.Hold || formation.Order == TileFormationOrderType.Regroup
                    ? formation.Anchor : StepToward(formation.Anchor, formation.Objective);
                for (int i = 0; i < formation.MemberUnitIds.Count; i++)
                {
                    TileCoord offset = ShapeOffset(formation.Shape, i, formation.MemberUnitIds.Count, formation.Side);
                    TileCoord preferred = new TileCoord(Clamp(destinationAnchor.X + offset.X, 0, observation.Width - 1),
                        Clamp(destinationAnchor.Y + offset.Y, 0, observation.Height - 1));
                    formation.Slots.Add(new TileFormationSlot { UnitId = formation.MemberUnitIds[i], Offset = offset,
                        PreferredPosition = preferred });
                }
            }
        }

        private TileCoord ObjectiveFor(TileCommandFormation formation, TileBattleObservation observation)
        {
            int row = formation.Sector == TileBattleSector.Left ? observation.Height * 3 / 4 :
                formation.Sector == TileBattleSector.Right ? observation.Height / 4 : observation.Height / 2;
            if (formation.Order == TileFormationOrderType.Hold || formation.Order == TileFormationOrderType.Regroup)
                return formation.Anchor;
            if (formation.Order == TileFormationOrderType.Support)
            {
                TileCommandFormation supported = formations.Find(f => f.Id == formation.SupportFormationId);
                if (supported != null) return supported.Objective;
            }
            int rear = side == 0 ? observation.Width - 1 : 0;
            return new TileCoord(rear, Clamp(row, 1, observation.Height - 2));
        }

        private static TileCoord ShapeOffset(TileFormationShape shape, int index, int count, int side)
        {
            if (shape == TileFormationShape.Line) return new TileCoord(0, index - (count - 1) / 2);
            if (shape == TileFormationShape.Column) return new TileCoord((side == 0 ? -1 : 1) * index, 0);
            int width = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
            int row = index / width, column = index % width;
            return new TileCoord((side == 0 ? -1 : 1) * row, column - (width - 1) / 2);
        }

        private static TileCoord NearbyValid(TileCoord candidate, TileCoord from, TileCoord target,
            TileBattleObservation observation, HashSet<TileCoord> occupied)
        {
            if (Valid(candidate, from, observation, occupied)) return candidate;
            TileCoord[] alternatives = { new TileCoord(from.X, from.Y + 1), new TileCoord(from.X, from.Y - 1),
                new TileCoord(from.X + 1, from.Y), new TileCoord(from.X - 1, from.Y) };
            Array.Sort(alternatives, (a, b) => a.ManhattanDistance(target).CompareTo(b.ManhattanDistance(target)));
            for (int i = 0; i < alternatives.Length; i++) if (Valid(alternatives[i], from, observation, occupied)) return alternatives[i];
            return from;
        }

        private static bool Valid(TileCoord tile, TileCoord from, TileBattleObservation observation, HashSet<TileCoord> occupied)
            => tile.X >= 0 && tile.Y >= 0 && tile.X < observation.Width && tile.Y < observation.Height &&
                tile.ManhattanDistance(from) == 1 && !occupied.Contains(tile);
        private TileCommandFormation FormationFor(int unitId) => formations.Find(f => f.MemberUnitIds.Contains(unitId));
        private static TileCoord StepToward(TileCoord from, TileCoord target)
        {
            if (from.X != target.X) return new TileCoord(from.X + Math.Sign(target.X - from.X), from.Y);
            if (from.Y != target.Y) return new TileCoord(from.X, from.Y + Math.Sign(target.Y - from.Y));
            return from;
        }
        private static string RoleKey(TileObservedUnit unit)
        {
            if (unit.Definition == null) return "9|unknown";
            if (unit.Definition.FormationType == TileFormationType.Phalanx) return "0|phalanx";
            if (unit.Definition.Cavalry) return "3|cavalry|" + (unit.Definition.Ranged ? "ranged" : "melee");
            if (unit.Definition.Ranged) return "2|ranged";
            return "1|" + (unit.Definition.DisplayName ?? unit.Definition.Id ?? "infantry");
        }
        private static string FormationName(TileObservedUnit unit, List<TileCommandFormation> existing)
        {
            string root = unit.Definition != null && !string.IsNullOrEmpty(unit.Definition.DisplayName)
                ? unit.Definition.DisplayName : "Formation";
            int number = 1 + existing.FindAll(f => f.Name.StartsWith(root, StringComparison.OrdinalIgnoreCase)).Count;
            return root + " " + number;
        }
        private static TileFormationShape DefaultShape(TileObservedUnit unit)
        {
            if (unit.Definition != null && unit.Definition.Cavalry) return TileFormationShape.Column;
            if (unit.Definition != null && (unit.Definition.Ranged || unit.Definition.FormationType == TileFormationType.Phalanx)) return TileFormationShape.Line;
            return TileFormationShape.Compact;
        }
        private static TileBattleSector SectorForRow(int y, int height) => y >= height * 2 / 3 ? TileBattleSector.Left :
            y < height / 3 ? TileBattleSector.Right : TileBattleSector.Centre;
        private static TileBattleSector OppositeSupportSector(TileBattleSector focus, int index)
        {
            if (focus == TileBattleSector.Left) return index % 2 == 0 ? TileBattleSector.Centre : TileBattleSector.Right;
            if (focus == TileBattleSector.Right) return index % 2 == 0 ? TileBattleSector.Centre : TileBattleSector.Left;
            return index % 2 == 0 ? TileBattleSector.Left : TileBattleSector.Right;
        }
        private static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }
}