using System;
using System.Collections.Generic;
using System.Text;
using ProjectX.CombatBackend;
using ProjectX.TileBattle;

namespace ProjectX.SectorBattle
{
    public interface ISectorCombatAbility
    {
        int ModifyDamagePercent(SectorAbilityContext context, int currentPercent);
        int ModifyExhaustionPercent(SectorAbilityContext context, int currentPercent);
    }

    public sealed class SectorAbilityContext
    {
        public SectorBattleSimulation Simulation;
        public SectorFormation Formation;
        public SectorFormation Target;
        public SectorState Sector;
        public bool Flanking;
        public bool RangedSupport;
    }

    /// <summary>
    /// Deterministic coarse battlefield. Exact positions are deliberately absent: formations
    /// occupy sectors, and frontage selects which ones participate on each resolution tick.
    /// </summary>
    public sealed class SectorBattleSimulation : IBattleSimulation
    {
        private sealed class Attack
        {
            public SectorFormation Attacker, Target;
            public SectorCoord TargetSector;
            public bool Flanking, Ranged;
        }

        private readonly Dictionary<SectorCoord, SectorState> sectors = new Dictionary<SectorCoord, SectorState>();
        private readonly List<SectorFormation> formations = new List<SectorFormation>();
        private readonly List<SectorCombatEvent> events = new List<SectorCombatEvent>();
        private readonly List<ISectorCombatAbility> abilities = new List<ISectorCombatAbility>();
        private BattleSimulationRequest request;
        private BattleSimulationOutcome outcome = new BattleSimulationOutcome();
        private SectorBattleRules rules;
        private bool started;
        private bool commandsInitialized;
        private readonly int[] initialStrengthBySide = new int[2];
        private readonly int[] enemyReserveOccupationTicks = new int[2];
        private string decisiveCollapseReason;

        public BattleSimulationType SimulationType => BattleSimulationType.Sector;
        public int Tick { get; private set; }
        public bool IsStarted => started;
        public bool IsResolved => outcome.Finished;
        public int WinningSide => outcome.WinningSide;
        public IReadOnlyList<SectorFormation> Formations => formations;
        public IReadOnlyList<SectorCombatEvent> Events => events;
        public IEnumerable<SectorState> Sectors => sectors.Values;
        public SectorBattleRules Rules => rules;
        public SectorCommandController Commands { get; private set; }
        public IReadOnlyList<SectorCommandGroup> CommandGroups => Commands.Groups;
        public bool PrototypeAIEnabled { get; set; } = true;

        public SectorBattleSimulation(SectorBattleRules configuredRules = null)
        {
            rules = configuredRules != null ? configuredRules : SectorBattleRules.Current;
            if (rules == null) throw new InvalidOperationException("A Resources/SectorBattleRules asset is required.");
            Commands = new SectorCommandController(this);
            CreateGrid();
        }

        public void Initialize(BattleSimulationRequest value)
        {
            request = value ?? throw new ArgumentNullException(nameof(value));
            formations.Clear(); events.Clear(); outcome = new BattleSimulationOutcome(); Tick = 0; started = false; commandsInitialized = false;
            initialStrengthBySide[0] = initialStrengthBySide[1] = 0; decisiveCollapseReason = null;
            enemyReserveOccupationTicks[0] = enemyReserveOccupationTicks[1] = 0;
            CreateGrid();
            foreach (BattleFormationInput formation in value.Formations) AddFormation(formation);
            Commands.Initialize(value); commandsInitialized = true;
        }

        public void AddFormation(BattleFormationInput input)
        {
            if (input == null || input.Unit == null || formations.Exists(item => item.Id == input.FormationId)) return;
            // Generic battles enter from their side's reserve. DeploymentColumn now carries
            // the stable BattleLane id; the default value is Centre, while scenarios may
            // explicitly use either outer manoeuvre lane.
            BattleLane lane = (BattleLane)Math.Max(0, Math.Min(4, input.DeploymentColumn));
            SectorCoord coordinate = new SectorCoord(lane,
                input.Side == 0 ? BattleDepth.SideAReserve : BattleDepth.SideBReserve);
            TileBattleUnitDefinition combat = TileBattleCampaignAdapter.CreateDefinition(input.Unit);
            SectorFormation formation = new SectorFormation { Id = input.FormationId, Side = input.Side == 0 ? 0 : 1,
                Source = input.Unit, Combat = combat, Strength = input.Strength > 0 ? input.Strength : combat.Strength,
                Ammunition = combat.Ammunition, Sector = coordinate, MovementTarget = coordinate };
            formations.Add(formation); sectors[coordinate].Formations.Add(formation);
            initialStrengthBySide[formation.Side] += Math.Max(0, formation.Strength);
            if (commandsInitialized) Commands.AddFormation(formation);
        }

        public void StartBattle()
        {
            if (started) return;
            started = true;
            Commands.LockMembership();
            formations.Sort((a, b) => a.Id.CompareTo(b.Id));
            Log("Sector battle started with " + CountActive(0) + " vs " + CountActive(1) + " formations");
        }

        public bool IssueMove(int formationId, SectorCoord destination)
        {
            SectorFormation formation = formations.Find(item => item.Id == formationId);
            if (formation == null || !formation.Active || formation.Moving || !sectors.ContainsKey(destination) ||
                !MovementAdjacent(formation.Sector, destination)) return false;
            if (IsSectorContested(formation.Sector))
            {
                int retreatDirection = formation.Side == 0 ? -1 : 1;
                int depthDelta = (int)destination.Depth - (int)formation.Sector.Depth;
                if (depthDelta != retreatDirection) return false;
            }
            foreach (SectorFormation enemy in formations)
                if (enemy.Active && enemy.Side != formation.Side && enemy.Moving && enemy.Sector.Equals(destination) &&
                    enemy.MovementTarget.Equals(formation.Sector)) return false;
            SectorTerrainRule terrain = rules.Terrain(sectors[destination].Terrain);
            if (sectors[destination].Terrain == SectorTerrain.Mountain && !HasTag(formation, "Mountaineer")) return false;
            int speed = formation.Combat != null && formation.Combat.Cavalry ? 2 : formation.Source != null ? Math.Max(1, formation.Source.actions) : 1;
            int cost = terrain != null ? Math.Max(1, terrain.movementCost) : 1;
            formation.Moving = true; formation.State = SectorFormationState.Moving; formation.MovementTarget = destination;
            formation.MovementTicksRemaining = Math.Max(1, (cost * 2 + speed - 1) / speed);
            formation.MovementTicksTotal = formation.MovementTicksRemaining;
            Emit(SectorPresentationEventType.MovementStarted, formation, null, destination,
                Name(formation) + " began moving from " + formation.Sector + " to " + destination);
            return true;
        }

        public bool TriggerWarcry(int formationId)
        {
            SectorFormation formation = formations.Find(item => item.Id == formationId);
            if (formation == null || !formation.Active || !HasTag(formation, "Warcry")) return false;
            formation.WarcryTicks = Math.Max(1, rules.warcryDurationTicks);
            Emit(SectorPresentationEventType.Warcry, formation, null, formation.Sector, Name(formation) + " used Warcry"); return true;
        }

        public void SetTerrain(SectorCoord coordinate, SectorTerrain terrain)
        { if (sectors.TryGetValue(coordinate, out SectorState sector)) sector.Terrain = terrain; }

        public void RegisterAbility(ISectorCombatAbility ability)
        { if (ability != null && !abilities.Contains(ability)) abilities.Add(ability); }

        public bool IssueGroupMove(int groupId, SectorCoord destination) => Commands.IssueMove(groupId, destination);
        public bool IssueGroupOrder(int groupId, SectorGroupOrder order) => Commands.IssueOrder(groupId, order);
        public bool IsSectorContested(SectorCoord coordinate) => sectors.TryGetValue(coordinate, out SectorState state) && Control(state) == SectorControl.Contested;
        public bool IsEnemyApproaching(SectorCoord coordinate, int side)
        {
            foreach (SectorFormation formation in formations)
                if (formation.Active && formation.Side != side && formation.Moving && formation.MovementTarget.Equals(coordinate)) return true;
            return false;
        }
        internal void PlaceFormationForSetup(int formationId, SectorCoord coordinate)
        {
            if (started || !sectors.ContainsKey(coordinate)) return;
            SectorFormation formation = formations.Find(item => item.Id == formationId); if (formation == null) return;
            sectors[formation.Sector].Formations.Remove(formation); formation.Sector = coordinate; formation.MovementTarget = coordinate;
            sectors[coordinate].Formations.Add(formation);
        }

        public void TickBattle()
        {
            if (!started || outcome.Finished) return;
            Tick++;
            Commands.Tick();
            ResolveMovement();
            if (Tick % Math.Max(1, rules.combatIntervalTicks) == 0)
            {
                List<Attack> attacks = BuildAttacks();
                ResolveAttacks(attacks);
            }
            ResolveStates();
            ResolveBacklineCollapse();
            ResolveEnemyReserveOccupation();
            if (Tick >= Math.Max(1, rules.maximumTicks)) FinishByStrength("Safety tick limit reached");
            else CheckVictory();
        }

        private List<Attack> BuildAttacks()
        {
            List<Attack> attacks = new List<Attack>();
            List<SectorState> ordered = OrderedSectors();
            foreach (SectorState sector in ordered)
            {
                if (Control(sector) != SectorControl.Contested) continue;
                int frontage = rules.Frontage(sector.Coordinate.Lane, sector.Terrain);
                List<SectorFormation> activeA = DirectParticipants(sector, 0, frontage);
                List<SectorFormation> activeB = DirectParticipants(sector, 1, frontage);
                LogOnce(sector.Coordinate + ": Base Frontage " + activeA.Count + "v" + activeB.Count);
                AddPairedAttacks(attacks, activeA, activeB, sector.Coordinate, false);
                AddFlankAttacks(attacks, sector, 0, activeB);
                AddFlankAttacks(attacks, sector, 1, activeA);
            }
            AddRangedSupport(attacks, ordered);
            attacks.Sort((a, b) => { int id = a.Attacker.Id.CompareTo(b.Attacker.Id); return id != 0 ? id : a.Target.Id.CompareTo(b.Target.Id); });
            return attacks;
        }

        private void AddPairedAttacks(List<Attack> attacks, List<SectorFormation> a, List<SectorFormation> b,
            SectorCoord targetSector, bool flanking)
        {
            if (a.Count == 0 || b.Count == 0) return;
            for (int i = 0; i < a.Count; i++) attacks.Add(new Attack { Attacker = a[i], Target = b[i % b.Count], TargetSector = targetSector, Flanking = flanking });
            for (int i = 0; i < b.Count; i++) attacks.Add(new Attack { Attacker = b[i], Target = a[i % a.Count], TargetSector = targetSector, Flanking = flanking });
        }

        private void AddFlankAttacks(List<Attack> attacks, SectorState target, int side, List<SectorFormation> defenders)
        {
            if (defenders.Count == 0) return;
            foreach (SectorCoord sourceCoord in FlankSources(target.Coordinate))
            {
                SectorState source = sectors[sourceCoord];
                SectorControl wanted = side == 0 ? SectorControl.SideA : SectorControl.SideB;
                if (Control(source) != wanted) continue;
                List<SectorFormation> attackers = CombatCapable(source, side);
                attackers.RemoveAll(item => item.Ranged || item.Moving);
                attackers.Sort(CompareFormation);
                int count = Math.Min(Math.Max(0, rules.flankFrontage), attackers.Count);
                if (count <= 0) continue;
                LogOnce(sourceCoord + " grants +" + count + " flank frontage into " + target.Coordinate + " for Side " + SideName(side));
                for (int i = 0; i < count; i++) attacks.Add(new Attack { Attacker = attackers[i],
                    Target = defenders[i % defenders.Count], TargetSector = target.Coordinate, Flanking = true });
            }
        }

        private void AddRangedSupport(List<Attack> attacks, List<SectorState> ordered)
        {
            foreach (SectorFormation ranged in formations)
            {
                if (!ranged.Active || ranged.Moving || !ranged.Ranged || ranged.Ammunition == 0 ||
                    Control(sectors[ranged.Sector]) == SectorControl.Contested) continue;
                SectorState target = null;
                foreach (SectorCoord candidate in SectorsInRange(ranged.Sector, Math.Max(1, rules.rangedAdjacentRange)))
                    if (Control(sectors[candidate]) == SectorControl.Contested && HasEnemy(sectors[candidate], ranged.Side))
                    { target = sectors[candidate]; break; }
                if (target == null) continue;
                List<SectorFormation> enemies = CombatCapable(target, 1 - ranged.Side);
                enemies.Sort(CompareFormation); if (enemies.Count == 0) continue;
                attacks.Add(new Attack { Attacker = ranged, Target = enemies[0], TargetSector = target.Coordinate, Ranged = true });
            }
        }

        private void ResolveAttacks(List<Attack> attacks)
        {
            Dictionary<int, int> damage = new Dictionary<int, int>();
            Dictionary<int, int> morale = new Dictionary<int, int>();
            foreach (Attack attack in attacks)
            {
                if (!attack.Attacker.Active || !attack.Target.Active) continue;
                if (attack.Ranged && attack.Attacker.Ammunition > 0) attack.Attacker.Ammunition--;
                int value = AttackDamage(attack);
                Emit(SectorPresentationEventType.Attack, attack.Attacker, attack.Target, attack.TargetSector, string.Empty);
                events[events.Count - 1].Damage = value;
                if (!damage.ContainsKey(attack.Target.Id)) damage[attack.Target.Id] = 0;
                damage[attack.Target.Id] += value;
                int moraleDamage = Math.Max(1, value / 2) + (attack.Flanking ? Math.Max(0, rules.flankMoralePenalty) : 0);
                if (!morale.ContainsKey(attack.Target.Id)) morale[attack.Target.Id] = 0;
                morale[attack.Target.Id] += moraleDamage;
                int exhaustion = Math.Max(0, rules.exhaustionPerAttack);
                if (HasTag(attack.Attacker, "Disciplined")) exhaustion = exhaustion * rules.disciplinedExhaustionPercent / 100;
                SectorAbilityContext exhaustionContext = new SectorAbilityContext { Simulation = this,
                    Formation = attack.Attacker, Target = attack.Target, Sector = sectors[attack.TargetSector],
                    Flanking = attack.Flanking, RangedSupport = attack.Ranged };
                foreach (ISectorCombatAbility ability in abilities)
                    exhaustion = ability.ModifyExhaustionPercent(exhaustionContext, exhaustion);
                attack.Attacker.Exhaustion = Math.Min(1000, attack.Attacker.Exhaustion + exhaustion);
                if (attack.Attacker.Combat.OpeningThrowable && !attack.Ranged &&
                    attack.Attacker.OpeningVolleyTargets.Add(attack.Target.Id))
                {
                    int openingDamage = Math.Max(0, rules.openingVolleyDamage);
                    damage[attack.Target.Id] += openingDamage;
                    events[events.Count - 1].Damage += openingDamage;
                    Log(Name(attack.Attacker) + " used Opening Volley against " + Name(attack.Target));
                }
            }
            foreach (SectorFormation target in formations)
            {
                if (!target.Active) continue;
                if (damage.TryGetValue(target.Id, out int amount)) target.Strength = Math.Max(0, target.Strength - amount);
                if (morale.TryGetValue(target.Id, out int moraleDamage)) target.Morale = Math.Max(0, target.Morale - moraleDamage);
            }
        }

        private int AttackDamage(Attack attack)
        {
            SectorFormation attacker = attack.Attacker;
            int baseDamage = attack.Ranged ? Math.Max(1, attacker.Combat.RangedDamage) : Math.Max(1, attacker.Combat.MeleeDamage);
            if (attack.Ranged) baseDamage = baseDamage * rules.rangedSupportDamagePercent / 100;
            int maximum = Math.Max(1, attacker.Combat.Strength);
            int percent = Math.Max(10, attacker.Strength * 100 / maximum);
            percent = percent * Math.Max(25, 100 - attacker.Exhaustion / 12) / 100;
            SectorState battleSector = sectors[attack.TargetSector];
            SectorTerrainRule terrain = rules.Terrain(battleSector.Terrain);
            if (terrain != null && !TerrainSpecialist(attacker, battleSector.Terrain))
                percent = percent * terrain.combatEffectivenessPercent / 100;
            if (PhalanxActive(attacker, battleSector, attack.Flanking)) percent = percent * rules.phalanxCombatPercent / 100;
            if (HasFriendlyWarchief(attacker.Side, battleSector)) percent = percent * rules.warchiefCombatPercent / 100;
            if (attacker.WarcryTicks > 0) percent = percent * rules.warcryCombatPercent / 100;
            SectorAbilityContext context = new SectorAbilityContext { Simulation = this, Formation = attacker,
                Target = attack.Target, Sector = battleSector, Flanking = attack.Flanking, RangedSupport = attack.Ranged };
            foreach (ISectorCombatAbility ability in abilities) percent = ability.ModifyDamagePercent(context, percent);
            int armor = Math.Max(0, attack.Target.Combat.ArmorPercent);
            int result = Math.Max(1, baseDamage * percent / 100);
            return Math.Max(1, result * Math.Max(10, 100 - armor) / 100);
        }

        private bool PhalanxActive(SectorFormation formation, SectorState sector, bool attackedFromFlank)
        {
            if (attackedFromFlank || formation.Combat.FormationType != TileFormationType.Phalanx) return false;
            SectorTerrainRule terrain = rules.Terrain(sector.Terrain); if (terrain != null && !terrain.allowsPhalanx) return false;
            int count = 0; foreach (SectorFormation friendly in sector.Formations)
                if (friendly.Active && friendly.Side == formation.Side && friendly.Combat.FormationType == TileFormationType.Phalanx) count++;
            return count >= Math.Max(1, rules.phalanxRequiredFormations);
        }

        private bool HasFriendlyWarchief(int side, SectorState sector)
        { return sector.Formations.Exists(item => item.Active && item.Side == side && HasTag(item, "Warchief")); }

        private void ResolveMovement()
        {
            foreach (SectorFormation formation in formations)
            {
                if (!formation.Active) continue;
                if (formation.WarcryTicks > 0) formation.WarcryTicks--;
                if (!formation.Moving || --formation.MovementTicksRemaining > 0) continue;
                sectors[formation.Sector].Formations.Remove(formation);
                SectorCoord from = formation.Sector; formation.Sector = formation.MovementTarget;
                formation.Moving = false; formation.State = SectorFormationState.Ready;
                formation.ArrivalTick = Tick;
                sectors[formation.Sector].Formations.Add(formation);
                Emit(SectorPresentationEventType.MovementCompleted, formation, null, formation.Sector,
                    Name(formation) + " entered " + formation.Sector + " from " + from);
            }
        }

        private void ResolveStates()
        {
            foreach (SectorFormation formation in formations)
            {
                if (formation.State == SectorFormationState.Destroyed || formation.State == SectorFormationState.Withdrawn ||
                    formation.State == SectorFormationState.Routing) continue;
                if (formation.Strength <= 0)
                { formation.Strength = 0; formation.State = SectorFormationState.Destroyed;
                  Emit(SectorPresentationEventType.Destroyed, formation, null, formation.Sector, Name(formation) + " was destroyed"); }
                else if (formation.Morale <= rules.routMorale)
                { formation.State = SectorFormationState.Routing;
                  Emit(SectorPresentationEventType.Routed, formation, null, formation.Sector, Name(formation) + " routed from " + formation.Sector); }
            }
        }

        private void ResolveBacklineCollapse()
        {
            // Evaluate both sides before changing either one so a simultaneous collapse stays deterministic.
            bool collapseA = ShouldCollapseAfterBacklineLoss(0);
            bool collapseB = ShouldCollapseAfterBacklineLoss(1);
            if (!collapseA && !collapseB) return;
            if (collapseA) RouteRemainingSide(0);
            if (collapseB) RouteRemainingSide(1);
            decisiveCollapseReason = collapseA && collapseB
                ? "Both armies collapsed after their rear positions were overrun"
                : "Side " + SideName(collapseA ? 0 : 1) + " collapsed after its rear position was overrun with substantial losses";
            Log(decisiveCollapseReason);
        }

        private void ResolveEnemyReserveOccupation()
        {
            if (!string.IsNullOrEmpty(decisiveCollapseReason)) return;
            bool routA = OccupationForcesRetreat(0);
            bool routB = OccupationForcesRetreat(1);
            if (!routA && !routB) return;
            if (routA) RouteRemainingSide(0);
            if (routB) RouteRemainingSide(1);
            decisiveCollapseReason = routA && routB
                ? "Both armies retreated after their rear positions were held uncontested"
                : "Side " + SideName(routA ? 0 : 1) + " retreated after its rear position was held uncontested for " +
                  Math.Max(1, rules.uncontestedEnemyReserveTicksToRout) + " sector ticks";
            Log(decisiveCollapseReason);
        }

        private bool OccupationForcesRetreat(int defendingSide)
        {
            BattleDepth rear = defendingSide == 0 ? BattleDepth.SideAReserve : BattleDepth.SideBReserve;
            SectorControl attacker = defendingSide == 0 ? SectorControl.SideB : SectorControl.SideA;
            bool occupied = false;
            foreach (SectorState sector in sectors.Values)
                if (sector.Coordinate.Depth == rear && Control(sector) == attacker) { occupied = true; break; }
            enemyReserveOccupationTicks[defendingSide] = occupied ? enemyReserveOccupationTicks[defendingSide] + 1 : 0;
            return enemyReserveOccupationTicks[defendingSide] >= Math.Max(1, rules.uncontestedEnemyReserveTicksToRout);
        }

        private bool ShouldCollapseAfterBacklineLoss(int side)
        {
            int initial = Math.Max(1, initialStrengthBySide[side]);
            int losses = Math.Max(0, initial - TotalRemainingStrength(side));
            if (losses * 100 < initial * Math.Max(0, rules.backlineCollapseCasualtyPercent)) return false;
            BattleDepth rear = side == 0 ? BattleDepth.SideAReserve : BattleDepth.SideBReserve;
            SectorControl enemyControl = side == 0 ? SectorControl.SideB : SectorControl.SideA;
            foreach (SectorState sector in sectors.Values)
                if (sector.Coordinate.Depth == rear && Control(sector) == enemyControl) return true;
            return false;
        }

        private int TotalRemainingStrength(int side)
        {
            int total = 0;
            foreach (SectorFormation formation in formations)
                if (formation.Side == side && formation.State != SectorFormationState.Destroyed)
                    total += Math.Max(0, formation.Strength);
            return total;
        }

        private void RouteRemainingSide(int side)
        {
            foreach (SectorFormation formation in formations)
            {
                if (formation.Side != side || !formation.Active) continue;
                formation.Moving = false; formation.State = SectorFormationState.Routing;
                Emit(SectorPresentationEventType.Routed, formation, null, formation.Sector,
                    Name(formation) + " fled after the army's rear position was overrun");
            }
        }

        // Intentionally small autonomous prototype AI: contest an enemy-held adjacent sector,
        // and send unengaged cavalry toward open main-line flanks.
        private void RunPrototypeAI()
        {
            foreach (SectorFormation formation in formations)
            {
                if (!formation.Active || formation.Moving || Control(sectors[formation.Sector]) == SectorControl.Contested) continue;
                foreach (SectorCoord neighbour in MovementNeighbours(formation.Sector))
                    if (HasEnemy(sectors[neighbour], formation.Side)) { IssueMove(formation.Id, neighbour); break; }
            }
        }

        private void CheckVictory()
        {
            int a = CountActive(0), b = CountActive(1);
            if (a > 0 && b > 0) return;
            outcome.Finished = true; outcome.WinningSide = a > 0 ? 0 : b > 0 ? 1 : -1;
            outcome.EndReason = !string.IsNullOrEmpty(decisiveCollapseReason) ? decisiveCollapseReason :
                a == 0 && b == 0 ? "Mutual destruction" : "Opposing side has no combat-capable formations";
            PopulateOutcome(); Log("Battle ended: " + outcome.EndReason + "; winner=" + outcome.WinningSide);
        }

        private void FinishByStrength(string reason)
        {
            int a = TotalStrength(0), b = TotalStrength(1);
            outcome.Finished = true; outcome.WinningSide = a == b ? -1 : a > b ? 0 : 1; outcome.EndReason = reason;
            PopulateOutcome(); Log("Battle ended: " + reason + "; winner=" + outcome.WinningSide);
        }

        private void PopulateOutcome()
        {
            outcome.Formations.Clear(); foreach (SectorFormation formation in formations)
                outcome.Formations.Add(new BattleFormationOutcome { FormationId = formation.Id, Side = formation.Side,
                    RemainingStrength = Math.Max(0, formation.Strength), Routed = formation.State == SectorFormationState.Routing });
        }

        public BattleSimulationOutcome GetOutcome() { if (outcome.Formations.Count == 0) PopulateOutcome(); return outcome; }

        public List<SectorDebugState> GetDebugState()
        {
            List<SectorDebugState> result = new List<SectorDebugState>();
            foreach (SectorState sector in OrderedSectors())
            {
                SectorDebugState state = new SectorDebugState { Coordinate = sector.Coordinate, Terrain = sector.Terrain,
                    Control = Control(sector), BaseFrontage = rules.Frontage(sector.Coordinate.Lane, sector.Terrain) };
                foreach (SectorFormation formation in CombatCapable(sector, 0)) state.SideA.Add(formation.Id);
                foreach (SectorFormation formation in CombatCapable(sector, 1)) state.SideB.Add(formation.Id);
                if (state.Control == SectorControl.Contested)
                {
                    FillDebugParticipation(sector, 0, state.ActiveA, state.ReserveA);
                    FillDebugParticipation(sector, 1, state.ActiveB, state.ReserveB);
                    foreach (int side in new[] { 0, 1 }) foreach (SectorCoord sourceCoord in FlankSources(sector.Coordinate))
                    {
                        SectorControl wanted = side == 0 ? SectorControl.SideA : SectorControl.SideB;
                        if (Control(sectors[sourceCoord]) != wanted) continue;
                        List<SectorFormation> source = CombatCapable(sectors[sourceCoord], side);
                        source.RemoveAll(item => item.Ranged); source.Sort(CompareFormation);
                        int flankCount = Math.Min(rules.flankFrontage, source.Count);
                        if (flankCount > 0)
                        {
                            if (side == 0) { state.FlankFrontageA += flankCount; state.FlankSourcesA.Add(sourceCoord); }
                            else { state.FlankFrontageB += flankCount; state.FlankSourcesB.Add(sourceCoord); }
                        }
                        for (int i = 0; i < flankCount; i++)
                            state.FlankAttackers.Add(Name(source[i]) + " from " + sourceCoord);
                    }
                }
                foreach (SectorFormation ranged in formations)
                {
                    if (!ranged.Active || !ranged.Ranged || ranged.Moving || ranged.Ammunition == 0 ||
                        Control(sectors[ranged.Sector]) == SectorControl.Contested || ranged.Side == 0 && state.SideB.Count == 0 ||
                        ranged.Side == 1 && state.SideA.Count == 0) continue;
                    if (SectorDistance(ranged.Sector, sector.Coordinate) <= Math.Max(1, rules.rangedAdjacentRange))
                        state.RangedSupporters.Add(Name(ranged) + " from " + ranged.Sector);
                }
                result.Add(state);
            }
            return result;
        }

        public List<SectorFormationPresentationState> GetPresentationState()
        {
            Dictionary<int, SectorFormationRole> roles = new Dictionary<int, SectorFormationRole>();
            Dictionary<int, SectorCoord> targets = new Dictionary<int, SectorCoord>();
            foreach (SectorFormation formation in formations) roles[formation.Id] = formation.Moving
                ? SectorFormationRole.Moving : formation.State == SectorFormationState.Routing
                    ? SectorFormationRole.Routing : SectorFormationRole.Idle;
            foreach (SectorState sector in OrderedSectors())
            {
                if (Control(sector) != SectorControl.Contested) continue;
                int frontage = rules.Frontage(sector.Coordinate.Lane, sector.Terrain);
                foreach (int side in new[] { 0, 1 })
                {
                    List<SectorFormation> all = CombatCapable(sector, side); all.RemoveAll(item => item.Moving); all.Sort(CompareFormation);
                    for (int i = 0; i < all.Count; i++) roles[all[i].Id] = i < frontage
                        ? SectorFormationRole.BaseFrontage : SectorFormationRole.Supporting;
                    List<SectorFormation> defenders = DirectParticipants(sector, 1 - side, frontage);
                    if (defenders.Count == 0) continue;
                    foreach (SectorCoord sourceCoord in FlankSources(sector.Coordinate))
                    {
                        SectorControl wanted = side == 0 ? SectorControl.SideA : SectorControl.SideB;
                        if (Control(sectors[sourceCoord]) != wanted) continue;
                        List<SectorFormation> flankers = CombatCapable(sectors[sourceCoord], side);
                        flankers.RemoveAll(item => item.Ranged || item.Moving); flankers.Sort(CompareFormation);
                        for (int i = 0; i < Math.Min(rules.flankFrontage, flankers.Count); i++)
                        { roles[flankers[i].Id] = SectorFormationRole.FlankAttacker; targets[flankers[i].Id] = sector.Coordinate; }
                    }
                }
            }
            foreach (SectorFormation ranged in formations)
            {
                if (!ranged.Active || ranged.Moving || !ranged.Ranged || ranged.Ammunition == 0 ||
                    Control(sectors[ranged.Sector]) == SectorControl.Contested) continue;
                foreach (SectorCoord candidate in SectorsInRange(ranged.Sector, Math.Max(1, rules.rangedAdjacentRange)))
                    if (Control(sectors[candidate]) == SectorControl.Contested && HasEnemy(sectors[candidate], ranged.Side))
                    { roles[ranged.Id] = SectorFormationRole.RangedSupport; targets[ranged.Id] = candidate; break; }
            }
            List<SectorFormationPresentationState> result = new List<SectorFormationPresentationState>();
            foreach (SectorFormation formation in formations)
            {
                SectorCoord target = formation.Moving ? formation.MovementTarget :
                    targets.TryGetValue(formation.Id, out SectorCoord purpose) ? purpose : formation.Sector;
                result.Add(new SectorFormationPresentationState { FormationId = formation.Id, Side = formation.Side,
                    Unit = formation.Source, DisplayName = formation.Combat != null ? formation.Combat.DisplayName : formation.Source.name,
                    Sector = formation.Sector, TargetSector = target, Role = roles[formation.Id], State = formation.State,
                    Strength = formation.Strength, MaximumStrength = formation.Combat != null ? formation.Combat.Strength : formation.Strength,
                    Morale = formation.Morale, Exhaustion = formation.Exhaustion,
                    MovementTicksRemaining = formation.MovementTicksRemaining, MovementTicksTotal = formation.MovementTicksTotal,
                    MovementProgress = formation.Moving && formation.MovementTicksTotal > 0
                        ? 1f - (float)formation.MovementTicksRemaining / formation.MovementTicksTotal : 1f });
            }
            return result;
        }

        private void FillDebugParticipation(SectorState sector, int side, List<int> active, List<int> reserve)
        {
            List<SectorFormation> all = CombatCapable(sector, side); all.RemoveAll(item => item.Moving); all.Sort(CompareFormation);
            int frontage = rules.Frontage(sector.Coordinate.Lane, sector.Terrain);
            for (int i = 0; i < all.Count; i++) (i < frontage ? active : reserve).Add(all[i].Id);
        }

        public string GetDebugText()
        {
            StringBuilder text = new StringBuilder("Sector battle ").Append(request != null ? request.BattleId : string.Empty)
                .Append(" tick ").Append(Tick).Append('\n');
            foreach (SectorDebugState state in GetDebugState())
            {
                text.Append(state.Coordinate).Append(" | ").Append(state.Terrain).Append(" | ").Append(state.Control)
                    .Append(" | frontage ").Append(state.BaseFrontage).Append('\n');
                text.Append(" A: ").Append(string.Join(",", state.SideA)).Append(" active[").Append(string.Join(",", state.ActiveA))
                    .Append("] reserve[").Append(string.Join(",", state.ReserveA)).Append("]\n");
                text.Append(" B: ").Append(string.Join(",", state.SideB)).Append(" active[").Append(string.Join(",", state.ActiveB))
                    .Append("] reserve[").Append(string.Join(",", state.ReserveB)).Append("]\n");
                if (state.FlankAttackers.Count > 0) text.Append(" flank: ").Append(string.Join("; ", state.FlankAttackers)).Append('\n');
            }
            int first = Math.Max(0, events.Count - 20); for (int i = first; i < events.Count; i++) text.Append(events[i]).Append('\n');
            return text.ToString();
        }

        public ulong ComputeHash()
        {
            ulong hash = 1469598103934665603UL; Hash(ref hash, Tick);
            Hash(ref hash, enemyReserveOccupationTicks[0]); Hash(ref hash, enemyReserveOccupationTicks[1]);
            foreach (SectorFormation formation in formations)
            { Hash(ref hash, formation.Id); Hash(ref hash, formation.Side); Hash(ref hash, formation.Strength); Hash(ref hash, formation.Morale);
              Hash(ref hash, formation.Exhaustion); Hash(ref hash, (int)formation.Sector.Lane); Hash(ref hash, (int)formation.Sector.Depth);
              Hash(ref hash, (int)formation.State); Hash(ref hash, formation.ArrivalTick); Hash(ref hash, formation.MovementTicksRemaining); }
            return hash;
        }

        private void CreateGrid()
        {
            sectors.Clear();
            foreach (BattleLane lane in Enum.GetValues(typeof(BattleLane))) foreach (BattleDepth depth in Enum.GetValues(typeof(BattleDepth)))
            { SectorCoord coordinate = new SectorCoord(lane, depth); sectors[coordinate] = new SectorState { Coordinate = coordinate, Terrain = SectorTerrain.OpenPlain }; }
        }
        private List<SectorState> OrderedSectors() { List<SectorState> result = new List<SectorState>(sectors.Values); result.Sort((a,b) => a.Coordinate.CompareTo(b.Coordinate)); return result; }
        private List<SectorFormation> CombatCapable(SectorState sector, int side) => sector.Formations.FindAll(item => item.Active && item.Side == side);
        private List<SectorFormation> DirectParticipants(SectorState sector, int side, int frontage)
        { List<SectorFormation> result = CombatCapable(sector, side); result.RemoveAll(item => item.Moving || item.ArrivalTick == Tick); result.Sort(CompareFormation); if (result.Count > frontage) result.RemoveRange(frontage, result.Count - frontage); return result; }
        private static int CompareFormation(SectorFormation a, SectorFormation b)
        { int ranged = a.Ranged.CompareTo(b.Ranged); return ranged != 0 ? ranged : a.Id.CompareTo(b.Id); }
        private SectorControl Control(SectorState sector)
        { bool a = sector.Formations.Exists(item => item.Active && item.Side == 0), b = sector.Formations.Exists(item => item.Active && item.Side == 1); return a && b ? SectorControl.Contested : a ? SectorControl.SideA : b ? SectorControl.SideB : SectorControl.Empty; }
        private bool HasEnemy(SectorState sector, int side) => sector.Formations.Exists(item => item.Active && item.Side != side);
        private int CountActive(int side) => formations.FindAll(item => item.Active && item.Side == side).Count;
        private int TotalStrength(int side) { int result = 0; foreach (SectorFormation item in formations) if (item.Active && item.Side == side) result += item.Strength; return result; }
        private static string Name(SectorFormation formation) => formation.Combat.DisplayName + " #" + formation.Id;
        private static string SideName(int side) => side == 0 ? "A" : "B";
        private static bool HasTag(SectorFormation formation, string tag)
        { if (formation == null || formation.Source == null || formation.Source.flaglist == null) return false; string normalized = tag.Replace(" ", "").Replace("_", ""); return formation.Source.flaglist.Exists(value => value != null && value.Replace(" ", "").Replace("_", "").Equals(normalized, StringComparison.OrdinalIgnoreCase)); }
        private static bool TerrainSpecialist(SectorFormation formation, SectorTerrain terrain)
        {
            if (terrain == SectorTerrain.Forest)
                return formation.Combat.ForestImmune || HasTag(formation, "Forester");
            if (terrain == SectorTerrain.ShallowRiver) return HasTag(formation, "Aquatic");
            if (terrain == SectorTerrain.Mountain || terrain == SectorTerrain.RockyGround)
                return HasTag(formation, "Mountaineer");
            return false;
        }
        private List<SectorCoord> FlankSources(SectorCoord target)
        { List<SectorCoord> result = new List<SectorCoord>(); int lane = (int)target.Lane; if (lane > 0) result.Add(new SectorCoord((BattleLane)(lane - 1), target.Depth)); if (lane < 4) result.Add(new SectorCoord((BattleLane)(lane + 1), target.Depth)); return result; }
        private List<SectorCoord> MovementNeighbours(SectorCoord source)
        { List<SectorCoord> result = FlankSources(source); int depth = (int)source.Depth; if (depth > 0) result.Add(new SectorCoord(source.Lane, (BattleDepth)(depth - 1))); if (depth < 4) result.Add(new SectorCoord(source.Lane, (BattleDepth)(depth + 1))); result.Sort(); return result; }
        private bool MovementAdjacent(SectorCoord a, SectorCoord b) => Math.Abs((int)a.Lane - (int)b.Lane) + Math.Abs((int)a.Depth - (int)b.Depth) == 1;
        private static int SectorDistance(SectorCoord a, SectorCoord b) =>
            Math.Abs((int)a.Lane - (int)b.Lane) + Math.Abs((int)a.Depth - (int)b.Depth);
        private List<SectorCoord> SectorsInRange(SectorCoord source, int range)
        {
            List<SectorCoord> result = new List<SectorCoord>();
            foreach (SectorCoord candidate in sectors.Keys)
                if (!candidate.Equals(source) && SectorDistance(source, candidate) <= range) result.Add(candidate);
            result.Sort();
            return result;
        }
        private void Log(string message) => events.Add(new SectorCombatEvent { Tick = Tick, Message = message });
        private void Emit(SectorPresentationEventType type, SectorFormation formation, SectorFormation target,
            SectorCoord sector, string message) => events.Add(new SectorCombatEvent { Tick = Tick, Type = type,
                FormationId = formation != null ? formation.Id : -1, TargetFormationId = target != null ? target.Id : -1,
                Sector = sector, Message = message });
        private void LogOnce(string message) { if (events.Count == 0 || events[events.Count - 1].Tick != Tick || events[events.Count - 1].Message != message) Log(message); }
        private static void Hash(ref ulong hash, int value) { unchecked { hash ^= (uint)value; hash *= 1099511628211UL; } }
    }
}
