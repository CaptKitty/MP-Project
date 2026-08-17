using System;
using System.Collections.Generic;
using System.Text;
using ProjectX.DeterministicBattle;

namespace ProjectX.BattleValidation
{
    [Serializable]
    public sealed class BattleScenario
    {
        public string Name;
        public string Description;
        public BattleStartState StartState;
        public int MaximumTicks = 5000;
        public int SeedRuns = 20;
        public int SaveReloadTick;
        public ScenarioExpectations Expected = new ScenarioExpectations();
        public List<BattleCommandRecord> Commands = new List<BattleCommandRecord>();
        public List<ScenarioReinforcement> Reinforcements = new List<ScenarioReinforcement>();
    }
    [Serializable] public sealed class ScenarioReinforcement { public int Tick; public List<BattleFormationStart> Formations = new List<BattleFormationStart>(); }
    [Serializable] internal sealed class ValidationCheckpoint { public BattleStartState State; public List<BattleCommandRecord> Commands; public int Tick; }

    [Serializable]
    public sealed class ScenarioExpectations
    {
        public bool MustFinish = true;
        public bool RequireCharge, RequireFlankOrRear, RequireProjectiles, RequireReserveRelease, RequireAbility;
        public bool RequireRouting;
        public int MinimumCasualties, MaximumDurationTicks = int.MaxValue;
        public int MinimumSideAWinPercent, MaximumSideAWinPercent = 100;
        public int MaximumDrawPercent = 25;
    }

    [Serializable]
    public sealed class BattleRunMetrics
    {
        public string Scenario;
        public ulong Seed, FinalHash, ReplayHash;
        public int Tick, Winner, SideACasualties, SideBCasualties, Charges, FlankAttacks, RearAttacks;
        public int Projectiles, ProjectileHits, MeleeAttacks, Abilities, ReservesReleased, RoutedFormations;
        public bool Finished, Deterministic, InvariantsPassed;
        public List<string> Failures = new List<string>();
    }

    [Serializable]
    public sealed class ScenarioBatchResult
    {
        public string Scenario;
        public int Runs, SideAWins, SideBWins, Draws, DeterminismFailures, InvariantFailures, BehavioralFailures;
        public long TotalTicks, TotalSideACasualties, TotalSideBCasualties;
        public List<string> Anomalies = new List<string>();
        public List<BattleRunMetrics> Results = new List<BattleRunMetrics>();
        public int SideAWinPercent => Runs == 0 ? 0 : SideAWins * 100 / Runs;
        public int DrawPercent => Runs == 0 ? 0 : Draws * 100 / Runs;
        public int AverageTicks => Runs == 0 ? 0 : (int)(TotalTicks / Runs);
    }

    public static class BattleScenarioRunner
    {
        public static BattleRunMetrics Run(BattleScenario scenario, ulong seed)
        {
            BattleStartState state = Clone(scenario.StartState); state.Seed = seed;
            BattleSimulation simulation = new BattleSimulation(state); Schedule(simulation, scenario.Commands, scenario.Reinforcements);
            int startA = CountSide(simulation, 0), startB = CountSide(simulation, 1);
            simulation.AdvanceTicks(scenario.MaximumTicks);
            BattleRunMetrics result = Capture(scenario, simulation, seed, startA, startB);
            BattleSimulation replay = new BattleSimulation(Clone(state)); Schedule(replay, scenario.Commands, scenario.Reinforcements);
            if (scenario.SaveReloadTick > 0)
            {
                replay.AdvanceTicks(scenario.SaveReloadTick);
                // Reconstruct from the same start state and command stream to the saved tick, then continue.
                string json = UnityEngine.JsonUtility.ToJson(new ValidationCheckpoint { State = state, Commands = scenario.Commands, Tick = scenario.SaveReloadTick });
                ValidationCheckpoint loaded = UnityEngine.JsonUtility.FromJson<ValidationCheckpoint>(json);
                BattleSimulation reconstructed = new BattleSimulation(Clone(loaded.State)); Schedule(reconstructed, loaded.Commands, scenario.Reinforcements);
                reconstructed.AdvanceTicks(scenario.SaveReloadTick);
                if (replay.ComputeHash() != reconstructed.ComputeHash()) result.Failures.Add("Save/reload checkpoint hash diverged");
                replay = reconstructed; replay.AdvanceTicks(scenario.MaximumTicks - scenario.SaveReloadTick);
            }
            else replay.AdvanceTicks(scenario.MaximumTicks);
            result.ReplayHash = replay.ComputeHash();
            result.Deterministic = result.FinalHash == result.ReplayHash;
            if (!result.Deterministic) result.Failures.Add("Replay hash diverged");
            ValidateInvariants(simulation, result);
            ValidateBehavior(scenario.Expected, result);
            return result;
        }

        public static ScenarioBatchResult RunBatch(BattleScenario scenario, ulong firstSeed, int runs = -1)
        {
            ScenarioBatchResult batch = new ScenarioBatchResult { Scenario = scenario.Name };
            runs = runs > 0 ? runs : Math.Max(1, scenario.SeedRuns);
            for (int i = 0; i < runs; i++)
            {
                BattleRunMetrics result = Run(scenario, firstSeed + (ulong)i); batch.Results.Add(result); batch.Runs++;
                if (result.Winner == 0) batch.SideAWins++; else if (result.Winner == 1) batch.SideBWins++; else batch.Draws++;
                if (!result.Deterministic) batch.DeterminismFailures++;
                if (!result.InvariantsPassed) batch.InvariantFailures++;
                if (result.Failures.Count > (result.Deterministic ? 0 : 1)) batch.BehavioralFailures++;
                batch.TotalTicks += result.Tick; batch.TotalSideACasualties += result.SideACasualties; batch.TotalSideBCasualties += result.SideBCasualties;
            }
            ScenarioExpectations expected = scenario.Expected;
            if (batch.SideAWinPercent < expected.MinimumSideAWinPercent || batch.SideAWinPercent > expected.MaximumSideAWinPercent)
                batch.Anomalies.Add("Side A win rate " + batch.SideAWinPercent + "% outside expected range");
            if (batch.DrawPercent > expected.MaximumDrawPercent) batch.Anomalies.Add("Draw rate " + batch.DrawPercent + "% is too high");
            if (batch.DeterminismFailures > 0) batch.Anomalies.Add(batch.DeterminismFailures + " determinism failures");
            if (batch.InvariantFailures > 0) batch.Anomalies.Add(batch.InvariantFailures + " invariant failures");
            return batch;
        }

        public static string ToCsv(IEnumerable<ScenarioBatchResult> batches)
        {
            StringBuilder csv = new StringBuilder("scenario,seed,tick,winner,a_casualties,b_casualties,charges,flanks,rears,projectiles,hits,melee,abilities,reserves,routs,deterministic,invariants,failures\n");
            foreach (ScenarioBatchResult batch in batches) foreach (BattleRunMetrics r in batch.Results)
                csv.Append(Escape(r.Scenario)).Append(',').Append(r.Seed).Append(',').Append(r.Tick).Append(',').Append(r.Winner).Append(',')
                    .Append(r.SideACasualties).Append(',').Append(r.SideBCasualties).Append(',').Append(r.Charges).Append(',')
                    .Append(r.FlankAttacks).Append(',').Append(r.RearAttacks).Append(',').Append(r.Projectiles).Append(',')
                    .Append(r.ProjectileHits).Append(',').Append(r.MeleeAttacks).Append(',').Append(r.Abilities).Append(',')
                    .Append(r.ReservesReleased).Append(',').Append(r.RoutedFormations).Append(',').Append(r.Deterministic).Append(',')
                    .Append(r.InvariantsPassed).Append(',').Append(Escape(string.Join("; ", r.Failures))).Append('\n');
            return csv.ToString();
        }

        private static BattleRunMetrics Capture(BattleScenario s, BattleSimulation sim, ulong seed, int startA, int startB)
        {
            BattleTelemetry t = sim.Telemetry;
            return new BattleRunMetrics { Scenario = s.Name, Seed = seed, Tick = sim.Tick, Winner = sim.WinningSide,
                Finished = sim.Status == BattleStatus.Finished, FinalHash = sim.ComputeHash(),
                SideACasualties = startA - CountSide(sim, 0), SideBCasualties = startB - CountSide(sim, 1), Charges = t.Charges,
                FlankAttacks = t.FlankAttacks, RearAttacks = t.RearAttacks, Projectiles = t.ProjectilesLaunched,
                ProjectileHits = t.ProjectileHits, MeleeAttacks = t.MeleeAttacks, Abilities = t.AbilitiesUsed,
                ReservesReleased = t.ReservesReleased, RoutedFormations = t.RoutedFormations };
        }

        private static void ValidateBehavior(ScenarioExpectations e, BattleRunMetrics r)
        {
            if (e.MustFinish && !r.Finished) r.Failures.Add("Battle did not finish");
            if (r.Tick > e.MaximumDurationTicks) r.Failures.Add("Exceeded maximum duration");
            if (r.SideACasualties + r.SideBCasualties < e.MinimumCasualties) r.Failures.Add("Too few casualties");
            if (e.RequireCharge && r.Charges == 0) r.Failures.Add("No charge occurred");
            if (e.RequireFlankOrRear && r.FlankAttacks + r.RearAttacks == 0) r.Failures.Add("No flank/rear contact occurred");
            if (e.RequireProjectiles && r.Projectiles == 0) r.Failures.Add("No projectile was launched");
            if (e.RequireReserveRelease && r.ReservesReleased == 0) r.Failures.Add("Reserve was never released");
            if (e.RequireAbility && r.Abilities == 0) r.Failures.Add("No ability was used");
            if (e.RequireRouting && r.RoutedFormations == 0) r.Failures.Add("No formation routed");
        }

        private static void ValidateInvariants(BattleSimulation sim, BattleRunMetrics r)
        {
            HashSet<int> formationIds = new HashSet<int>(), combatantIds = new HashSet<int>();
            for (int i = 0; i < sim.Formations.Count; i++) if (!formationIds.Add(sim.Formations[i].Id)) r.Failures.Add("Duplicate formation ID");
            for (int i = 0; i < sim.Combatants.Count; i++)
            {
                SimCombatant c = sim.Combatants[i];
                if (!combatantIds.Add(c.Id)) r.Failures.Add("Duplicate combatant ID");
                if (c.Alive && c.Health <= 0) r.Failures.Add("Living combatant has non-positive health");
                if (!formationIds.Contains(c.FormationId)) r.Failures.Add("Combatant references missing formation");
            }
            for (int i = 0; i < sim.Formations.Count; i++)
                if (sim.Formations[i].Morale < 0 || sim.Formations[i].Morale > 1000 || sim.Formations[i].Cohesion < 0 || sim.Formations[i].Cohesion > 1000)
                    r.Failures.Add("Formation morale/cohesion outside bounds");
            r.InvariantsPassed = r.Failures.Count == 0;
        }

        private static int CountSide(BattleSimulation sim, int side)
        {
            int count = 0;
            for (int i = 0; i < sim.Combatants.Count; i++)
            {
                SimCombatant c = sim.Combatants[i]; SimFormation f = sim.Formations.Find(item => item.Id == c.FormationId);
                if (c.Alive && f != null && f.Side == side) count++;
            }
            return count;
        }

        private static void Schedule(BattleSimulation sim, List<BattleCommandRecord> commands, List<ScenarioReinforcement> reinforcements)
        {
            if (commands == null) return;
            for (int i = 0; i < commands.Count; i++)
            {
                BattleCommandRecord c = commands[i];
                if (c.IsAbility) sim.ScheduleCommand(new BattleAbilityCommand { Tick = c.Tick, Side = c.Side, FormationId = c.FormationId, Ability = c.Ability });
                else sim.ScheduleCommand(new FormationOrderCommand { Tick = c.Tick, FormationId = c.FormationId, Order = c.Order, LockDurationTicks = c.LockDurationTicks });
            }
            if (reinforcements != null) for (int i = 0; i < reinforcements.Count; i++)
            {
                ReinforcementCommand command = new ReinforcementCommand { Tick = reinforcements[i].Tick };
                command.Formations.AddRange(reinforcements[i].Formations); sim.ScheduleCommand(command);
            }
        }

        private static BattleStartState Clone(BattleStartState state)
        {
            // The battle schema is Unity-serializable; JSON cloning prevents one run mutating the next run's setup.
            return UnityEngine.JsonUtility.FromJson<BattleStartState>(UnityEngine.JsonUtility.ToJson(state));
        }
        private static string Escape(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}
