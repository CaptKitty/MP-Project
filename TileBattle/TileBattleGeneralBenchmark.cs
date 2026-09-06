using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectX.TileBattle
{
    [Serializable]
    public sealed class TileGeneralBenchmarkResult
    {
        public string Scenario, LeftGeneral, RightGeneral, Winner;
        public int Rounds, LeftStrength, RightStrength, Attacks, Pushes, ReserveCommitments;
        public int LeftPlanChanges, RightPlanChanges;
        public string LeftFinalPlan, RightFinalPlan;
        public int LeftFirstReserveRound = -1, RightFirstReserveRound = -1;
        public string LeftReserveRows, RightReserveRows, LeftReservePurposes, RightReservePurposes;
        public int LeftReserveSurvival, RightReserveSurvival;
    }

    public static class TileBattleGeneralBenchmark
    {
        public static List<TileGeneralBenchmarkResult> RunStandardSuite()
        {
            List<TileGeneralBenchmarkResult> results = new List<TileGeneralBenchmarkResult>();
            TileGeneralPersonality balanced = Profile("Balanced", 50);
            TileGeneralPersonality skilled = Profile("Skilled", 90, methodical: 35, opportunistic: 30);
            TileGeneralPersonality inexperienced = Profile("Inexperienced", 20, aggressive: 20);
            TileGeneralPersonality bold = Profile("Bold", 55, aggressive: 55, bold: 65);
            TileGeneralPersonality cautious = Profile("Cautious", 55, cautious: 65, defensive: 45, patient: 35);
            TileGeneralPersonality methodical = Profile("Methodical", 70, methodical: 70, patient: 30);
            TileGeneralPersonality opportunist = Profile("Opportunist", 70, opportunistic: 75, cavalry: 20);
            TileGeneralPersonality cavalry = Profile("Cavalry Minded", 65, cavalry: 80, bold: 25);
            TileGeneralPersonality[,] pairs = { { skilled, inexperienced }, { bold, cautious }, { methodical, opportunist }, { cavalry, balanced } };
            string[] scenarios = { "Balanced Field", "Weak Centre", "Open Flanks", "Reserve Crisis" };
            for (int s = 0; s < scenarios.Length; s++)
            for (int p = 0; p < pairs.GetLength(0); p++)
            {
                results.Add(RunMatch(scenarios[s], pairs[p, 0], pairs[p, 1], false));
                results.Add(RunMatch(scenarios[s], pairs[p, 1], pairs[p, 0], true));
            }
            return results;
        }

        public static string ToCsv(List<TileGeneralBenchmarkResult> results)
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Scenario,LeftGeneral,RightGeneral,Winner,Rounds,LeftStrength,RightStrength,LeftPlanChanges,RightPlanChanges,LeftFinalPlan,RightFinalPlan,Attacks,Pushes,ReserveCommitments,LeftFirstReserveRound,RightFirstReserveRound,LeftReserveRows,RightReserveRows,LeftReservePurposes,RightReservePurposes,LeftReserveSurvival,RightReserveSurvival");
            for (int i = 0; i < results.Count; i++)
            {
                TileGeneralBenchmarkResult r = results[i];
                csv.Append(Escape(r.Scenario)).Append(',').Append(Escape(r.LeftGeneral)).Append(',').Append(Escape(r.RightGeneral)).Append(',')
                    .Append(Escape(r.Winner)).Append(',').Append(r.Rounds).Append(',').Append(r.LeftStrength).Append(',').Append(r.RightStrength).Append(',')
                    .Append(r.LeftPlanChanges).Append(',').Append(r.RightPlanChanges).Append(',').Append(r.LeftFinalPlan).Append(',').Append(r.RightFinalPlan).Append(',')
                    .Append(r.Attacks).Append(',').Append(r.Pushes).Append(',').Append(r.ReserveCommitments).Append(',')
                    .Append(r.LeftFirstReserveRound).Append(',').Append(r.RightFirstReserveRound).Append(',')
                    .Append(Escape(r.LeftReserveRows)).Append(',').Append(Escape(r.RightReserveRows)).Append(',')
                    .Append(Escape(r.LeftReservePurposes)).Append(',').Append(Escape(r.RightReservePurposes)).Append(',')
                    .Append(r.LeftReserveSurvival).Append(',').Append(r.RightReserveSurvival).AppendLine();
            }
            return csv.ToString();
        }

        private static TileGeneralBenchmarkResult RunMatch(string scenario, TileGeneralPersonality leftProfile,
            TileGeneralPersonality rightProfile, bool mirrored)
        {
            TileBattleSimulation simulation = CreateScenario(scenario, leftProfile, rightProfile, mirrored);
            simulation.RunToCompletion(60);
            int left = 0, right = 0;
            for (int i = 0; i < simulation.Units.Count; i++)
                if (simulation.Units[i].Side == 0) left += Math.Max(0, simulation.Units[i].Strength); else right += Math.Max(0, simulation.Units[i].Strength);
            List<TileBattleEvent> leftReserveEvents = simulation.Events.FindAll(item => item.Type == TileBattleEventType.ReserveCommitted && item.UnitId < 10000);
            List<TileBattleEvent> rightReserveEvents = simulation.Events.FindAll(item => item.Type == TileBattleEventType.ReserveCommitted && item.UnitId >= 10000);
            TileGeneralBenchmarkResult result = new TileGeneralBenchmarkResult { Scenario = scenario + (mirrored ? " (mirrored)" : string.Empty),
                LeftGeneral = leftProfile.Name, RightGeneral = rightProfile.Name,
                Winner = simulation.Result.WinningSide == 0 ? leftProfile.Name : simulation.Result.WinningSide == 1 ? rightProfile.Name : "Draw",
                Rounds = simulation.CommandRound, LeftStrength = left, RightStrength = right,
                LeftPlanChanges = CountPlanChanges(simulation.Events, "Left committed "),
                RightPlanChanges = CountPlanChanges(simulation.Events, "Right committed "),
                LeftFinalPlan = simulation.LeftGeneral.DebugState.CurrentPlan.ToString(),
                RightFinalPlan = simulation.RightGeneral.DebugState.CurrentPlan.ToString(),
                Attacks = simulation.Events.FindAll(item => item.Type == TileBattleEventType.UnitAttacked).Count,
                Pushes = simulation.Events.FindAll(item => item.Type == TileBattleEventType.UnitPushed).Count,
                ReserveCommitments = leftReserveEvents.Count + rightReserveEvents.Count,
                LeftFirstReserveRound = leftReserveEvents.Count > 0 ? leftReserveEvents[0].CommandRound : -1,
                RightFirstReserveRound = rightReserveEvents.Count > 0 ? rightReserveEvents[0].CommandRound : -1,
                LeftReserveRows = JoinReserveRows(leftReserveEvents), RightReserveRows = JoinReserveRows(rightReserveEvents),
                LeftReservePurposes = JoinReservePurposes(leftReserveEvents), RightReservePurposes = JoinReservePurposes(rightReserveEvents) };
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                TileBattleUnit unit = simulation.Units[i]; int localId = unit.Id % 10000;
                if (localId < 7) continue;
                if (unit.Side == 0) result.LeftReserveSurvival += Math.Max(0, unit.Strength);
                else result.RightReserveSurvival += Math.Max(0, unit.Strength);
            }
            return result;
        }

        private static TileBattleSimulation CreateScenario(string scenario, TileGeneralPersonality leftProfile,
            TileGeneralPersonality rightProfile, bool mirrored)
        {
            TileBattleSimulation sim = new TileBattleSimulation(new TileBattleRules(),
                new PersonalityTileGeneral(leftProfile), new PersonalityTileGeneral(rightProfile));
            TileBattleUnitDefinition infantry = Definition("Infantry", 7, 2, 120, 100, 20);
            TileBattleUnitDefinition heavy = Definition("Heavy", 8, 2, 165, 120, 24);
            TileBattleUnitDefinition light = Definition("Light", 5, 3, 75, 80, 15);
            TileBattleUnitDefinition cavalry = Definition("Cavalry", 4, 4, 105, 90, 22, true);
            TileBattleUnitDefinition ranged = Definition("Ranged", 5, 3, 65, 75, 10, false, true);
            TileBattleUnitDefinition[] left = { light, infantry, heavy, infantry, cavalry, ranged, heavy, infantry };
            TileBattleUnitDefinition[] right = { light, infantry, heavy, infantry, cavalry, ranged, heavy, infantry };
            if (scenario == "Weak Centre") { left[2] = heavy; right[2] = light; right[3] = light; }
            else if (scenario == "Open Flanks") { left[4] = cavalry; left[6] = cavalry; right[0] = light; right[4] = light; }
            else if (scenario == "Reserve Crisis") { left[6] = heavy; left[7] = heavy; right[6] = cavalry; right[7] = heavy; }
            AddSide(sim, mirrored ? right : left, 0); AddSide(sim, mirrored ? left : right, 1); return sim;
        }

        private static void AddSide(TileBattleSimulation simulation, TileBattleUnitDefinition[] definitions, int side)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                bool vanguard = i < 2, reserve = i >= definitions.Length - 2;
                simulation.AddUnit(new TileBattleUnit { Id = side * 10000 + i + 1, Side = side, Definition = definitions[i],
                    Position = new TileCoord(side == 0 ? 2 : 17, 4 + i), Facing = side == 0 ? TileFacing.East : TileFacing.West,
                    Strength = definitions[i].Strength, IsVanguard = vanguard, IsReserve = reserve, Deployed = vanguard });
            }
        }

        private static TileBattleUnitDefinition Definition(string name, int initiative, int actions, int mass, int strength,
            int damage, bool cavalry = false, bool ranged = false) => new TileBattleUnitDefinition { Id = name,
            DisplayName = name, Initiative = initiative, Actions = actions, BaseMass = mass, Strength = strength,
            MeleeDamage = damage, FrontThreat = 1, Cavalry = cavalry, Ranged = ranged, RangedRange = ranged ? 3 : 0,
            RangedDamage = ranged ? 14 : 0, Ammunition = ranged ? 20 : 0 };

        private static TileGeneralPersonality Profile(string name, int competence, int bold = 0, int cautious = 0,
            int patient = 0, int aggressive = 0, int methodical = 0, int opportunistic = 0, int cavalry = 0, int defensive = 0) =>
            new TileGeneralPersonality { Name = name, Competence = competence, Bold = bold, Cautious = cautious,
                Patient = patient, Aggressive = aggressive, Methodical = methodical, Opportunistic = opportunistic,
                CavalryMinded = cavalry, Defensive = defensive };

        private static int CountPlanChanges(List<TileBattleEvent> events, string prefix)
        {
            string previous = null; int changes = 0;
            for (int i = 0; i < events.Count; i++)
            {
                string message = events[i].Message;
                if (events[i].Type != TileBattleEventType.PlanChosen || string.IsNullOrEmpty(message) || !message.StartsWith(prefix, StringComparison.Ordinal)) continue;
                int end = message.IndexOf(':'); string plan = end > prefix.Length ? message.Substring(prefix.Length, end - prefix.Length) : message;
                if (previous != null && previous != plan) changes++; previous = plan;
            }
            return changes;
        }

        private static string JoinReserveRows(List<TileBattleEvent> events)
        {
            List<string> values = new List<string>(); for (int i = 0; i < events.Count; i++) values.Add(events[i].To.Y.ToString());
            return string.Join("|", values);
        }

        private static string JoinReservePurposes(List<TileBattleEvent> events)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < events.Count; i++)
            {
                string message = events[i].Message ?? string.Empty; int start = message.IndexOf(" for ", StringComparison.Ordinal);
                int end = message.IndexOf(" on row", StringComparison.Ordinal);
                values.Add(start >= 0 && end > start ? message.Substring(start + 5, end - start - 5) : message);
            }
            return string.Join("|", values);
        }

        private static string Escape(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
