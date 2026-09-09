using System;
using System.Collections.Generic;
using ProjectX.CombatBackend;
using ProjectX.TileBattle;

namespace ProjectX.SectorBattle
{
    public enum SectorGroupOrder : byte { Hold, Move, Advance, Withdraw }
    public enum SectorGroupRole : byte { HeavyInfantry, LineInfantry, LightInfantry, Phalanx, Skirmisher, MissileInfantry, LightCavalry, HeavyCavalry, Large }

    [Serializable]
    public sealed class SectorCommandGroup
    {
        public int GroupId;
        public int Side;
        public string GeneralName;
        public SectorGroupRole Role;
        public readonly List<int> MemberFormationIds = new List<int>();
        public SectorCoord CurrentSector;
        public SectorCoord DestinationSector;
        public SectorGroupOrder CurrentOrder = SectorGroupOrder.Hold;
        public bool Moving;
        public bool PlayerControlled;
        public bool MembershipLocked;
        public string DisplayName;
    }

    /// <summary>Command abstraction only. Formations retain all combat state and frontage.</summary>
    public sealed class SectorCommandController
    {
        private readonly SectorBattleSimulation simulation;
        private readonly List<SectorCommandGroup> groups = new List<SectorCommandGroup>();
        private readonly Dictionary<int, SectorCommandGroup> byFormation = new Dictionary<int, SectorCommandGroup>();
        private readonly Dictionary<int, BattleSideCommandConfig> commanders = new Dictionary<int, BattleSideCommandConfig>();
        private readonly Dictionary<int, BattleLane> aiFlankAssignments = new Dictionary<int, BattleLane>();
        private int nextGroupId = 1;
        public IReadOnlyList<SectorCommandGroup> Groups => groups;

        public SectorCommandController(SectorBattleSimulation owner) { simulation = owner; }

        public void Initialize(BattleSimulationRequest request)
        {
            groups.Clear(); byFormation.Clear(); commanders.Clear(); aiFlankAssignments.Clear(); nextGroupId = 1;
            if (request != null) for (int i = 0; i < request.Commanders.Count; i++)
                commanders[request.Commanders[i].Side] = request.Commanders[i];
            AutoCreate(0); AutoCreate(1); AssignAIFlankers(0); AssignAIFlankers(1); ConsolidateGroups(); RefreshLocations();
        }

        public void LockMembership() { for (int i = 0; i < groups.Count; i++) groups[i].MembershipLocked = true; }
        public SectorCommandGroup GroupForFormation(int formationId) => byFormation.TryGetValue(formationId, out SectorCommandGroup group) ? group : null;
        public int Capacity(int side) => commanders.TryGetValue(side, out BattleSideCommandConfig config) ? Math.Max(1, config.CommandGroupCapacity) : 5;
        public void SetPlayerControlled(int side, bool playerControlled)
        {
            if (commanders.TryGetValue(side, out BattleSideCommandConfig config)) config.PlayerControlled = playerControlled;
            for (int i = 0; i < groups.Count; i++) if (groups[i].Side == side) groups[i].PlayerControlled = playerControlled;
            RefreshAIFlanker(side);
        }

        public void AddFormation(SectorFormation formation)
        {
            if (formation == null || byFormation.ContainsKey(formation.Id)) return;
            SectorGroupRole role = RoleOf(formation);
            SectorCommandGroup target = groups.Find(item => item.Side == formation.Side && item.Role == role);
            if (target == null && groups.FindAll(item => item.Side == formation.Side).Count < Capacity(formation.Side))
            {
                BattleSideCommandConfig config = commanders.TryGetValue(formation.Side, out BattleSideCommandConfig found) ? found :
                    new BattleSideCommandConfig { Side = formation.Side, GeneralName = "General", CommandGroupCapacity = 5 };
                target = NewGroup(formation.Side, role, config.GeneralName, config.PlayerControlled); target.MembershipLocked = simulation.IsStarted;
            }
            if (target == null) target = groups.Find(item => item.Side == formation.Side);
            if (target == null) return;
            target.MemberFormationIds.Add(formation.Id); byFormation[formation.Id] = target; NameGroups();
        }

        public bool CreateGroup(int side, int formationId)
        {
            if (simulation.IsStarted || groups.FindAll(item => item.Side == side).Count >= Capacity(side)) return false;
            SectorFormation formation = Find(formationId); SectorCommandGroup source = GroupForFormation(formationId);
            if (formation == null || source == null || source.MemberFormationIds.Count <= 1) return false;
            SectorCommandGroup group = NewGroup(side, source.Role, source.GeneralName, source.PlayerControlled);
            source.MemberFormationIds.Remove(formationId); group.MemberFormationIds.Add(formationId); byFormation[formationId] = group;
            NameGroups(); RefreshAIFlanker(side); return true;
        }

        public bool MoveMember(int formationId, int targetGroupId)
        {
            if (simulation.IsStarted) return false;
            SectorCommandGroup source = GroupForFormation(formationId), target = groups.Find(item => item.GroupId == targetGroupId);
            SectorFormation formation = Find(formationId);
            if (source == null || target == null || source == target || formation == null || source.Side != target.Side || !Compatible(RoleOf(formation), target.Role)) return false;
            source.MemberFormationIds.Remove(formationId); target.MemberFormationIds.Add(formationId); byFormation[formationId] = target;
            if (source.MemberFormationIds.Count == 0) groups.Remove(source); NameGroups(); RefreshAIFlanker(target.Side); return true;
        }

        public bool Merge(int sourceId, int targetId)
        {
            if (simulation.IsStarted) return false;
            SectorCommandGroup source = groups.Find(item => item.GroupId == sourceId), target = groups.Find(item => item.GroupId == targetId);
            if (source == null || target == null || source == target || source.Side != target.Side || !Compatible(source.Role, target.Role)) return false;
            for (int i = 0; i < source.MemberFormationIds.Count; i++) { int id = source.MemberFormationIds[i]; target.MemberFormationIds.Add(id); byFormation[id] = target; }
            groups.Remove(source); NameGroups(); RefreshAIFlanker(target.Side); return true;
        }

        public bool DeleteEmpty(int groupId)
        { if (simulation.IsStarted) return false; SectorCommandGroup group = groups.Find(item => item.GroupId == groupId); if (group == null || group.MemberFormationIds.Count > 0) return false; groups.Remove(group); return true; }

        public bool IssueMove(int groupId, SectorCoord destination)
        {
            SectorCommandGroup group = groups.Find(item => item.GroupId == groupId);
            if (group == null || group.MemberFormationIds.Count == 0 || !Adjacent(group.CurrentSector, destination)) return false;
            if (group.Moving) CancelMovement(group);
            int longest = 1; bool any = false;
            for (int i = 0; i < group.MemberFormationIds.Count; i++)
            {
                SectorFormation formation = Find(group.MemberFormationIds[i]);
                if (formation == null || !formation.Active || formation.Moving || !formation.Sector.Equals(group.CurrentSector)) continue;
                if (simulation.IssueMove(formation.Id, destination)) { longest = Math.Max(longest, formation.MovementTicksRemaining); any = true; }
            }
            if (!any) return false;
            for (int i = 0; i < group.MemberFormationIds.Count; i++) { SectorFormation f = Find(group.MemberFormationIds[i]); if (f != null && f.Moving) { f.MovementTicksRemaining = longest; f.MovementTicksTotal = longest; } }
            group.CurrentOrder = SectorGroupOrder.Move; group.DestinationSector = destination; group.Moving = true; return true;
        }

        public bool IssueOrder(int groupId, SectorGroupOrder order)
        {
            SectorCommandGroup group = groups.Find(item => item.GroupId == groupId); if (group == null) return false;
            if (order == SectorGroupOrder.Hold)
            {
                CancelMovement(group); group.CurrentOrder = SectorGroupOrder.Hold;
                group.DestinationSector = group.CurrentSector; return true;
            }
            SectorCoord target = order == SectorGroupOrder.Withdraw ? new SectorCoord(group.CurrentSector.Lane,
                group.Side == 0 ? BattleDepth.SideAReserve : BattleDepth.SideBReserve) : AdvanceTarget(group);
            if (target.Equals(group.CurrentSector))
            { CancelMovement(group); group.CurrentOrder = order; group.DestinationSector = target; return true; }
            bool issued = IssueMove(groupId, NextStep(group.CurrentSector, target));
            if (issued) group.CurrentOrder = order;
            return issued;
        }

        private void CancelMovement(SectorCommandGroup group)
        {
            if (group == null) return;
            for (int i = 0; i < group.MemberFormationIds.Count; i++)
            {
                SectorFormation formation = Find(group.MemberFormationIds[i]);
                if (formation == null || !formation.Moving) continue;
                formation.Moving = false; formation.State = SectorFormationState.Ready;
                formation.MovementTarget = formation.Sector;
                formation.MovementTicksRemaining = formation.MovementTicksTotal = 0;
            }
            group.Moving = false; group.DestinationSector = group.CurrentSector;
        }

        public void Tick()
        {
            RefreshLocations();
            for (int i = 0; i < groups.Count; i++)
            {
                SectorCommandGroup group = groups[i];
                if (group.Moving) continue;
                if (simulation.IsSectorContested(group.CurrentSector))
                {
                    if (group.CurrentOrder != SectorGroupOrder.Withdraw && !group.PlayerControlled)
                        group.CurrentOrder = SectorGroupOrder.Hold;
                    group.DestinationSector = group.CurrentSector;
                    continue;
                }
                if (group.CurrentOrder == SectorGroupOrder.Withdraw || group.PlayerControlled && group.CurrentOrder == SectorGroupOrder.Advance)
                    IssueOrder(group.GroupId, group.CurrentOrder);
                else if (!group.PlayerControlled && simulation.PrototypeAIEnabled && simulation.Tick % 4 == group.GroupId % 4) RunAI(group);
            }
        }

        private void RunAI(SectorCommandGroup group)
        {
            if (simulation.IsSectorContested(group.CurrentSector)) { group.CurrentOrder = SectorGroupOrder.Hold; return; }
            // The designated manoeuvre group always goes wide. Independently mobile cavalry
            // and skirmisher groups may also recognize the same opening instead of relying on
            // a single setup-time assignment surviving command-group consolidation.
            bool flanker = aiFlankAssignments.ContainsKey(group.GroupId) || FlankSuitability(group.Role) >= 3;
            if (flanker && SupportsFriendlyFightFrom(group.CurrentSector, group.Side))
            {
                group.CurrentOrder = SectorGroupOrder.Hold;
                group.DestinationSector = group.CurrentSector;
                return;
            }

            SectorCoord best = group.CurrentSector;
            int bestScore = ScorePosition(group, best, flanker);
            foreach (SectorCoord candidate in AdjacentSectors(group.CurrentSector))
            {
                int score = ScorePosition(group, candidate, flanker);
                if (score > bestScore || score == bestScore && PreferTie(group, candidate, best))
                { best = candidate; bestScore = score; }
            }
            if (!best.Equals(group.CurrentSector) && IssueMove(group.GroupId, best)) return;
            group.CurrentOrder = SectorGroupOrder.Hold;
            group.DestinationSector = group.CurrentSector;
        }

        private int ScorePosition(SectorCommandGroup group, SectorCoord position, bool flanker)
        {
            int direction = group.Side == 0 ? 1 : -1;
            int score = ((int)position.Depth - (int)group.CurrentSector.Depth) * direction * 28;
            int friendly = CountAt(position, group.Side), enemy = CountAt(position, 1 - group.Side);
            if (enemy > 0) score += 125 + enemy * 12;
            if (simulation.IsSectorContested(position)) score += 90;
            score -= friendly * 4;

            int nearestEnemy = 20;
            foreach (SectorFormation formation in simulation.Formations)
                if (formation.Active && formation.Side != group.Side)
                    nearestEnemy = Math.Min(nearestEnemy, Distance(position, formation.Sector));
            score -= nearestEnemy * 9;

            int flankTargets = AdjacentContestedEnemies(position, group.Side);
            if (flankTargets > 0) score += flankTargets * (70 + FlankSuitability(group.Role) * 12);
            int wingDistance = Math.Abs((int)position.Lane - (int)BattleLane.Centre);
            if (flanker)
            {
                score += wingDistance * (14 + FlankSuitability(group.Role) * 2);
                if (position.Lane == BattleLane.TopFlank || position.Lane == BattleLane.BottomFlank)
                    score += 55 + FlankSuitability(group.Role) * 5;
                // Spread vertically while protected by the reserve, then advance horizontally.
                if (group.CurrentSector.Lane == BattleLane.Centre && position.Depth != group.CurrentSector.Depth) score -= 35;
            }
            else
            {
                bool line = group.Role == SectorGroupRole.HeavyInfantry || group.Role == SectorGroupRole.LineInfantry ||
                            group.Role == SectorGroupRole.Phalanx || group.Role == SectorGroupRole.Large;
                if (line) score -= wingDistance * 16;
            }
            BattleDepth enemyReserve = group.Side == 0 ? BattleDepth.SideBReserve : BattleDepth.SideAReserve;
            if (position.Depth == enemyReserve && enemy == 0) score += 35;
            if (position.Depth == enemyReserve && !AnySectorContested()) score -= 100;
            return score;
        }

        private bool AnySectorContested()
        {
            foreach (BattleLane lane in Enum.GetValues(typeof(BattleLane)))
                foreach (BattleDepth depth in Enum.GetValues(typeof(BattleDepth)))
                    if (simulation.IsSectorContested(new SectorCoord(lane, depth))) return true;
            return false;
        }

        private int CountAt(SectorCoord position, int side)
        {
            int count = 0;
            foreach (SectorFormation formation in simulation.Formations)
                if (formation.Active && formation.Side == side && formation.Sector.Equals(position)) count++;
            return count;
        }

        private int AdjacentContestedEnemies(SectorCoord position, int side)
        {
            int count = 0;
            foreach (SectorCoord candidate in AdjacentSectors(position))
                if (candidate.Depth == position.Depth && simulation.IsSectorContested(candidate) && CountAt(candidate, 1 - side) > 0) count++;
            return count;
        }

        private bool SupportsFriendlyFightFrom(SectorCoord position, int side)
            => AdjacentContestedEnemies(position, side) > 0;

        private static IEnumerable<SectorCoord> AdjacentSectors(SectorCoord origin)
        {
            int lane = (int)origin.Lane, depth = (int)origin.Depth;
            if (lane > 0) yield return new SectorCoord((BattleLane)(lane - 1), origin.Depth);
            if (lane < 4) yield return new SectorCoord((BattleLane)(lane + 1), origin.Depth);
            if (depth > 0) yield return new SectorCoord(origin.Lane, (BattleDepth)(depth - 1));
            if (depth < 4) yield return new SectorCoord(origin.Lane, (BattleDepth)(depth + 1));
        }

        private static int Distance(SectorCoord a, SectorCoord b)
            => Math.Abs((int)a.Lane - (int)b.Lane) + Math.Abs((int)a.Depth - (int)b.Depth);

        private static bool PreferTie(SectorCommandGroup group, SectorCoord candidate, SectorCoord currentBest)
        {
            int desiredWing = (group.GroupId + group.Side) % 2 == 0 ? 1 : -1;
            int candidateBias = ((int)candidate.Lane - (int)BattleLane.Centre) * desiredWing;
            int bestBias = ((int)currentBest.Lane - (int)BattleLane.Centre) * desiredWing;
            if (candidateBias != bestBias) return candidateBias > bestBias;
            return (int)candidate.Depth * 5 + (int)candidate.Lane < (int)currentBest.Depth * 5 + (int)currentBest.Lane;
        }

        private void AssignAIFlankers(int side)
        {
            List<SectorCommandGroup> candidates = groups.FindAll(item => item.Side == side && !item.PlayerControlled);
            if (candidates.Count < 2) return;
            candidates.Sort((a, b) =>
            {
                int role = FlankSuitability(b.Role).CompareTo(FlankSuitability(a.Role));
                return role != 0 ? role : a.GroupId.CompareTo(b.GroupId);
            });
            SectorCommandGroup chosen = candidates[0];
            aiFlankAssignments[chosen.GroupId] = (chosen.GroupId + side) % 2 == 0 ? BattleLane.BottomFlank : BattleLane.TopFlank;
        }

        private void RefreshAIFlanker(int side)
        {
            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, BattleLane> assignment in aiFlankAssignments)
            {
                SectorCommandGroup group = groups.Find(item => item.GroupId == assignment.Key);
                if (group == null || group.Side == side) remove.Add(assignment.Key);
            }
            for (int i = 0; i < remove.Count; i++) aiFlankAssignments.Remove(remove[i]);
            AssignAIFlankers(side);
        }

        private static int FlankSuitability(SectorGroupRole role)
        {
            switch (role)
            {
                case SectorGroupRole.LightCavalry: return 5;
                case SectorGroupRole.HeavyCavalry: return 4;
                case SectorGroupRole.Skirmisher: return 3;
                case SectorGroupRole.LightInfantry: return 2;
                case SectorGroupRole.MissileInfantry: return 1;
                default: return 0;
            }
        }

        private void RefreshLocations()
        {
            for (int i = 0; i < groups.Count; i++)
            {
                SectorCommandGroup group = groups[i]; SectorFormation first = null; bool moving = false;
                for (int m = 0; m < group.MemberFormationIds.Count; m++) { SectorFormation f = Find(group.MemberFormationIds[m]); if (f != null && f.Active) { first ??= f; moving |= f.Moving; } }
                if (first != null)
                {
                    group.CurrentSector = first.Sector; group.Moving = moving;
                    if (!moving && group.CurrentOrder == SectorGroupOrder.Move) group.CurrentOrder = SectorGroupOrder.Hold;
                    if (!moving && group.CurrentOrder == SectorGroupOrder.Hold) group.DestinationSector = group.CurrentSector;
                }
            }
        }

        private void AutoCreate(int side)
        {
            BattleSideCommandConfig config = commanders.TryGetValue(side, out BattleSideCommandConfig found) ? found :
                new BattleSideCommandConfig { Side = side, GeneralName = "General", CommandGroupCapacity = 5 };
            Dictionary<SectorGroupRole, List<int>> buckets = new Dictionary<SectorGroupRole, List<int>>();
            foreach (SectorFormation formation in simulation.Formations) if (formation.Side == side)
            { SectorGroupRole role = RoleOf(formation); if (!buckets.TryGetValue(role, out List<int> ids)) buckets[role] = ids = new List<int>(); ids.Add(formation.Id); }
            List<SectorGroupRole> roles = new List<SectorGroupRole>(buckets.Keys); roles.Sort();
            for (int i = 0; i < roles.Count; i++) { SectorCommandGroup group = NewGroup(side, roles[i], config.GeneralName, config.PlayerControlled); group.MemberFormationIds.AddRange(buckets[roles[i]]); }
            while (groups.FindAll(item => item.Side == side).Count > Capacity(side))
            {
                SectorCommandGroup source = null, target = null; List<SectorCommandGroup> sideGroups = groups.FindAll(item => item.Side == side);
                for (int i = 0; i < sideGroups.Count && source == null; i++) for (int j = i + 1; j < sideGroups.Count; j++)
                    if (Compatible(sideGroups[i].Role, sideGroups[j].Role))
                    { source = sideGroups[i].MemberFormationIds.Count <= sideGroups[j].MemberFormationIds.Count ? sideGroups[i] : sideGroups[j]; target = source == sideGroups[i] ? sideGroups[j] : sideGroups[i]; break; }
                if (source == null) break; target.MemberFormationIds.AddRange(source.MemberFormationIds); groups.Remove(source);
            }
            while (groups.FindAll(item => item.Side == side).Count < Capacity(side))
            {
                SectorCommandGroup largest = groups.FindAll(item => item.Side == side).FindMax(item => item.MemberFormationIds.Count);
                if (largest == null || largest.MemberFormationIds.Count < 2) break;
                SectorCommandGroup split = NewGroup(side, largest.Role, largest.GeneralName, largest.PlayerControlled);
                int move = largest.MemberFormationIds.Count / 2;
                split.MemberFormationIds.AddRange(largest.MemberFormationIds.GetRange(largest.MemberFormationIds.Count - move, move));
                largest.MemberFormationIds.RemoveRange(largest.MemberFormationIds.Count - move, move);
            }
            foreach (SectorCommandGroup group in groups) if (group.Side == side)
                for (int i = 0; i < group.MemberFormationIds.Count; i++) byFormation[group.MemberFormationIds[i]] = group;
            NameGroups();
        }

        private void ConsolidateGroups()
        {
            for (int i = 0; i < groups.Count; i++)
            {
                SectorCommandGroup group = groups[i]; if (group.MemberFormationIds.Count == 0) continue;
                SectorFormation anchor = Find(group.MemberFormationIds[0]); if (anchor == null) continue;
                for (int m = 1; m < group.MemberFormationIds.Count; m++) simulation.PlaceFormationForSetup(group.MemberFormationIds[m], anchor.Sector);
            }
        }

        private SectorCommandGroup NewGroup(int side, SectorGroupRole role, string general, bool player)
        { SectorCommandGroup group = new SectorCommandGroup { GroupId = nextGroupId++, Side = side, Role = role, GeneralName = general, PlayerControlled = player }; groups.Add(group); return group; }
        private void NameGroups()
        { Dictionary<string, int> counts = new Dictionary<string, int>(); foreach (SectorCommandGroup group in groups) { string key = group.Side + ":" + group.Role; counts[key] = counts.TryGetValue(key, out int value) ? value + 1 : 1; group.DisplayName = group.Role + " " + counts[key]; } }
        private SectorFormation Find(int id) { foreach (SectorFormation f in simulation.Formations) if (f.Id == id) return f; return null; }
        internal static SectorGroupRole RoleOf(SectorFormation f)
        {
            if (f.Source.Big) return SectorGroupRole.Large;
            if (f.Combat.FormationType == TileFormationType.Phalanx) return SectorGroupRole.Phalanx;
            if (f.Source.unittype == UnitTypes.HeavyCavalry) return SectorGroupRole.HeavyCavalry;
            if (f.Source.unittype == UnitTypes.LightCavalry) return SectorGroupRole.LightCavalry;
            if (f.Combat.Ranged) return f.Source.unittype == UnitTypes.Ranged ? SectorGroupRole.MissileInfantry : SectorGroupRole.Skirmisher;
            if (f.Source.unittype == UnitTypes.HeavyInfantry) return SectorGroupRole.HeavyInfantry;
            if (f.Source.unittype == UnitTypes.LightInfantry) return SectorGroupRole.LightInfantry;
            return SectorGroupRole.LineInfantry;
        }
        internal static bool Compatible(SectorGroupRole a, SectorGroupRole b)
        {
            if (a == b) return true;
            bool lineA = a == SectorGroupRole.HeavyInfantry || a == SectorGroupRole.LineInfantry || a == SectorGroupRole.Phalanx;
            bool lineB = b == SectorGroupRole.HeavyInfantry || b == SectorGroupRole.LineInfantry || b == SectorGroupRole.Phalanx;
            bool lightA = a == SectorGroupRole.LightInfantry || a == SectorGroupRole.Skirmisher || a == SectorGroupRole.MissileInfantry;
            bool lightB = b == SectorGroupRole.LightInfantry || b == SectorGroupRole.Skirmisher || b == SectorGroupRole.MissileInfantry;
            bool cavalryA = a == SectorGroupRole.LightCavalry || a == SectorGroupRole.HeavyCavalry;
            bool cavalryB = b == SectorGroupRole.LightCavalry || b == SectorGroupRole.HeavyCavalry;
            return lineA && lineB || lightA && lightB || cavalryA && cavalryB;
        }
        private SectorCoord AdvanceTarget(SectorCommandGroup group)
        {
            if (group.CurrentSector.Depth != BattleDepth.CentralGround)
                return new SectorCoord(group.CurrentSector.Lane, BattleDepth.CentralGround);
            // Wait for an enemy already marching into this sector. Otherwise continue
            // horizontally through the opposing line toward its reserve.
            if (simulation.IsEnemyApproaching(group.CurrentSector, group.Side)) return group.CurrentSector;
            return new SectorCoord(group.CurrentSector.Lane,
                group.Side == 0 ? BattleDepth.SideBReserve : BattleDepth.SideAReserve);
        }
        private static SectorCoord NextStep(SectorCoord from, SectorCoord to)
        { if (from.Depth != to.Depth) return new SectorCoord(from.Lane, (BattleDepth)((int)from.Depth + Math.Sign((int)to.Depth - (int)from.Depth))); return new SectorCoord((BattleLane)((int)from.Lane + Math.Sign((int)to.Lane - (int)from.Lane)), from.Depth); }
        private static SectorCoord NextFlankStep(SectorCoord from, SectorCoord to)
        {
            // Flankers spread while still screened by their own depth line, then advance.
            if (from.Lane != to.Lane)
                return new SectorCoord((BattleLane)((int)from.Lane + Math.Sign((int)to.Lane - (int)from.Lane)), from.Depth);
            return NextStep(from, to);
        }
        private static bool Adjacent(SectorCoord a, SectorCoord b) => Math.Abs((int)a.Lane - (int)b.Lane) + Math.Abs((int)a.Depth - (int)b.Depth) == 1;
    }

    internal static class SectorListExtensions
    {
        public static T FindMax<T>(this List<T> list, Func<T, int> selector) where T : class
        { T best = null; int value = int.MinValue; for (int i = 0; i < list.Count; i++) { int candidate = selector(list[i]); if (candidate > value) { value = candidate; best = list[i]; } } return best; }
        public static int CountForSide(this IReadOnlyList<SectorCommandGroup> list, int side)
        { int count = 0; for (int i = 0; i < list.Count; i++) if (list[i].Side == side) count++; return count; }
    }
}
