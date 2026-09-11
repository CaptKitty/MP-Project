#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectX.SectorBattle;
using UnityEditor;
using UnityEngine;

public enum SectorBenchmarkVariable
{
    None,
    RoutMorale,
    ExhaustionPerAttack,
    FlankFrontage,
    FlankMoralePenalty,
    RangedSupportDamagePercent,
    AiDecisionIntervalTicks,
    AiForwardScore,
    AiEnemyScore,
    AiContestedScore,
    AiFlankScore,
    AiWideScore,
    SideAStartingStrengthPercent,
    SideBStartingStrengthPercent
}

[Serializable]
public sealed class SectorBenchmarkArmyEntry
{
    public UnitSaveData Unit;
    public int Count = 1;
    public BattleLane Lane = BattleLane.Centre;
}

[Serializable]
public sealed class SectorBenchmarkArmySpec
{
    public string AssetPath;
    public string UnitName;
    public int Count;
    public BattleLane Lane;
}

[Serializable]
public sealed class SectorUnitPerformance
{
    public int Side;
    public string UnitName;
    public int Formations;
    public int InitialStrength;
    public int RemainingStrength;
    public int Routed;
    public int Attacks;
    public int DamageDealt;
    public int Casualties => InitialStrength - RemainingStrength;
}

[Serializable]
public sealed class SectorBenchmarkResult
{
    public SectorBattlePreset Preset;
    public SectorBenchmarkVariable Variable;
    public int Value;
    public ulong Seed;
    public int CapacityA;
    public int CapacityB;
    public string FactionA;
    public string FactionB;
    public string FactionAssetPathA;
    public string FactionAssetPathB;
    public string GeneralA;
    public string GeneralB;
    public SectorGeneralTactic TacticA;
    public SectorGeneralTactic TacticB;
    public List<SectorBenchmarkArmySpec> ArmyA = new List<SectorBenchmarkArmySpec>();
    public List<SectorBenchmarkArmySpec> ArmyB = new List<SectorBenchmarkArmySpec>();
    public List<SectorUnitPerformance> UnitPerformance = new List<SectorUnitPerformance>();
    public int Winner;
    public int Ticks;
    public int InitialStrengthA;
    public int InitialStrengthB;
    public int RemainingStrengthA;
    public int RemainingStrengthB;
    public int RoutedA;
    public int RoutedB;
    public double RuntimeMilliseconds;
    public string EndReason;
    public string Error;

    public int CasualtiesA => InitialStrengthA - RemainingStrengthA;
    public int CasualtiesB => InitialStrengthB - RemainingStrengthB;
    public string WinnerLabel => Winner < 0 ? "Draw" : "Side " + (Winner == 0 ? "A" : "B");
}

[Serializable]
internal sealed class SectorBenchmarkViewRequest
{
    public SectorBattlePreset Preset;
    public SectorBenchmarkVariable Variable;
    public int Value;
    public ulong Seed;
    public int CapacityA;
    public int CapacityB;
    public string FactionA;
    public string FactionB;
    public string FactionAssetPathA;
    public string FactionAssetPathB;
    public string GeneralA;
    public string GeneralB;
    public SectorGeneralTactic TacticA;
    public SectorGeneralTactic TacticB;
    public List<SectorBenchmarkArmySpec> ArmyA = new List<SectorBenchmarkArmySpec>();
    public List<SectorBenchmarkArmySpec> ArmyB = new List<SectorBenchmarkArmySpec>();
}

public sealed class SectorBattleBenchmarkWindow : EditorWindow
{
    private const string DefaultCsvPath = "Assets/BattleTestResults/sector-battle-benchmarks.csv";
    [SerializeField] private SectorBattlePreset preset = SectorBattlePreset.Basic10v10;
    [SerializeField] private int firstSeed = 1;
    [SerializeField] private int repetitions = 20;
    [SerializeField] private int capacityA = 6;
    [SerializeField] private int capacityB = 6;
    [SerializeField] private FactionArmyTemplate templateA;
    [SerializeField] private FactionArmyTemplate templateB;
    [SerializeField] private bool useGeneratedArmies = true;
    [SerializeField] private int generatedFormationsA = 10;
    [SerializeField] private int generatedFormationsB = 10;
    [SerializeField] private string factionA = "Side A";
    [SerializeField] private string factionB = "Side B";
    [SerializeField] private string generalA = "Side A General";
    [SerializeField] private string generalB = "Side B General";
    [SerializeField] private SectorGeneralTactic tacticA;
    [SerializeField] private SectorGeneralTactic tacticB;
    [SerializeField] private List<SectorBenchmarkArmyEntry> armyA = new List<SectorBenchmarkArmyEntry>();
    [SerializeField] private List<SectorBenchmarkArmyEntry> armyB = new List<SectorBenchmarkArmyEntry>();
    private bool showArmies = true;
    [SerializeField] private SectorBenchmarkVariable variable;
    [SerializeField] private int minimum;
    [SerializeField] private int maximum;
    [SerializeField] private int step = 1;
    [SerializeField] private List<SectorBenchmarkResult> results = new List<SectorBenchmarkResult>();
    [SerializeField] private int sortColumn;
    [SerializeField] private bool descending;
    [SerializeField] private int winnerFilter = -2;
    [SerializeField] private string seedFilter = string.Empty;
    private Vector2 scroll;
    private Queue<SectorBenchmarkViewRequest> pending;
    private int totalRuns;
    private bool running;
    private string status = string.Empty;

    [MenuItem("Window/Project X/Sector Battle Benchmarks")]
    public static void Open() => GetWindow<SectorBattleBenchmarkWindow>("Sector Benchmarks");

    private void OnDisable()
    {
        EditorApplication.update -= RunNext;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sector Battle Batch Benchmarks", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Runs deterministic AI-vs-AI battles without scenes or rendering. Select any result and View Battle to reconstruct it in Play Mode with the normal sector viewer.", MessageType.Info);
        EditorGUILayout.HelpBox("Sector combat currently stores seeds but has no random decisions. Multiple seeds repeat the same battle path for timing samples; sweep values are what change behavior.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(running))
        {
            preset = (SectorBattlePreset)EditorGUILayout.EnumPopup("Battle preset", preset);
            firstSeed = Mathf.Max(0, EditorGUILayout.IntField("First seed", firstSeed));
            repetitions = Mathf.Clamp(EditorGUILayout.IntField("Seeds per value", repetitions), 1, 10000);
            capacityA = Mathf.Clamp(EditorGUILayout.IntSlider("Side A command capacity", capacityA, 4, 8), 4, 8);
            capacityB = Mathf.Clamp(EditorGUILayout.IntSlider("Side B command capacity", capacityB, 4, 8), 4, 8);
            EditorGUILayout.BeginHorizontal();
            templateA = (FactionArmyTemplate)EditorGUILayout.ObjectField("Side A template", templateA, typeof(FactionArmyTemplate), false);
            using (new EditorGUI.DisabledScope(templateA == null)) if (GUILayout.Button("Load A", GUILayout.Width(65))) LoadTemplate(templateA, 0);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            templateB = (FactionArmyTemplate)EditorGUILayout.ObjectField("Side B template", templateB, typeof(FactionArmyTemplate), false);
            using (new EditorGUI.DisabledScope(templateB == null)) if (GUILayout.Button("Load B", GUILayout.Width(65))) LoadTemplate(templateB, 1);
            EditorGUILayout.EndHorizontal();
            factionA = EditorGUILayout.TextField("Side A faction", factionA);
            generalA = EditorGUILayout.TextField("Side A general", generalA);
            tacticA = (SectorGeneralTactic)EditorGUILayout.EnumPopup("Side A tactic", tacticA);
            factionB = EditorGUILayout.TextField("Side B faction", factionB);
            generalB = EditorGUILayout.TextField("Side B general", generalB);
            tacticB = (SectorGeneralTactic)EditorGUILayout.EnumPopup("Side B tactic", tacticB);
            EditorGUILayout.HelpBox("Winged Center holds the main army in the centre with mobile support on the wings. Focused Center commits every group through the central lane. Double Envelopment strongly favors both outer flanks. Single Flank concentrates mobile flanking groups on one wing.", MessageType.None);
            useGeneratedArmies = EditorGUILayout.Toggle("Use generated armies", useGeneratedArmies);
            if (useGeneratedArmies)
            {
                generatedFormationsA = Mathf.Clamp(EditorGUILayout.IntField("Side A formations", generatedFormationsA), 1, 100);
                generatedFormationsB = Mathf.Clamp(EditorGUILayout.IntField("Side B formations", generatedFormationsB), 1, 100);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Generate Side A")) GenerateArmy(armyA, generatedFormationsA, 0);
                if (GUILayout.Button("Generate Side B")) GenerateArmy(armyB, generatedFormationsB, 1);
                if (GUILayout.Button("Generate Both")) { GenerateArmy(armyA, generatedFormationsA, 0); GenerateArmy(armyB, generatedFormationsB, 1); }
                EditorGUILayout.EndHorizontal();
                showArmies = EditorGUILayout.Foldout(showArmies, "Army compositions", true);
                if (showArmies) { DrawArmy("Side A", armyA); DrawArmy("Side B", armyB); }
            }
            variable = (SectorBenchmarkVariable)EditorGUILayout.EnumPopup("Sweep variable", variable);
            if (variable != SectorBenchmarkVariable.None)
            {
                EditorGUI.indentLevel++;
                minimum = EditorGUILayout.IntField("Minimum", minimum);
                maximum = EditorGUILayout.IntField("Maximum", maximum);
                step = Mathf.Max(1, EditorGUILayout.IntField("Step", step));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Batch", GUILayout.Height(28))) BeginRun();
            using (new EditorGUI.DisabledScope(results.Count == 0))
            {
                if (GUILayout.Button("Export CSV", GUILayout.Height(28))) ExportCsv();
                if (GUILayout.Button("Clear", GUILayout.Height(28))) results.Clear();
            }
            EditorGUILayout.EndHorizontal();
        }
        if (running)
        {
            float progress = totalRuns == 0 ? 0f : (totalRuns - pending.Count) / (float)totalRuns;
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, progress, status);
            if (GUILayout.Button("Cancel")) StopRun("Cancelled after " + results.Count + " battles.");
        }
        else if (!string.IsNullOrEmpty(status)) EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

        DrawFilters();
        DrawResults();
    }

    private void LoadTemplate(FactionArmyTemplate template, int side)
    {
        if (template == null) return;
        List<SectorBenchmarkArmyEntry> target = side == 0 ? armyA : armyB;
        target.Clear();
        if (template.formations != null)
            foreach (SectorCustomFormationSpec formation in template.formations)
                if (formation != null && formation.Unit != null && formation.Count > 0)
                    target.Add(new SectorBenchmarkArmyEntry { Unit = formation.Unit, Count = formation.Count, Lane = formation.Lane });
        if (side == 0)
        {
            factionA = template.faction != null ? template.faction.name : template.factionName;
            generalA = template.generalName;
            tacticA = template.tactic;
            capacityA = Mathf.Clamp(template.commandGroupCapacity, 4, 8);
            generatedFormationsA = CountFormations(target);
        }
        else
        {
            factionB = template.faction != null ? template.faction.name : template.factionName;
            generalB = template.generalName;
            tacticB = template.tactic;
            capacityB = Mathf.Clamp(template.commandGroupCapacity, 4, 8);
            generatedFormationsB = CountFormations(target);
        }
        useGeneratedArmies = true;
        showArmies = true;
    }

    private static int CountFormations(List<SectorBenchmarkArmyEntry> army)
    {
        int count = 0;
        foreach (SectorBenchmarkArmyEntry entry in army) if (entry != null) count += Mathf.Max(0, entry.Count);
        return Mathf.Max(1, count);
    }
    private void DrawArmy(string label, List<SectorBenchmarkArmyEntry> army)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        int remove = -1;
        for (int i = 0; i < army.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            army[i].Unit = (UnitSaveData)EditorGUILayout.ObjectField(army[i].Unit, typeof(UnitSaveData), false);
            army[i].Count = Mathf.Max(1, EditorGUILayout.IntField(army[i].Count, GUILayout.Width(42)));
            army[i].Lane = (BattleLane)EditorGUILayout.EnumPopup(army[i].Lane, GUILayout.Width(100));
            if (GUILayout.Button("-", GUILayout.Width(24))) remove = i;
            EditorGUILayout.EndHorizontal();
        }
        if (remove >= 0) army.RemoveAt(remove);
        if (GUILayout.Button("Add " + label + " unit")) army.Add(new SectorBenchmarkArmyEntry());
    }

    private void GenerateArmy(List<SectorBenchmarkArmyEntry> army, int formationCount, int side)
    {
        string[] guids = AssetDatabase.FindAssets("t:UnitSaveData", new[] { "Assets/Resources/Prefabs/Units/NormieData" });
        List<UnitSaveData> units = new List<UnitSaveData>();
        foreach (string guid in guids)
        {
            UnitSaveData unit = AssetDatabase.LoadAssetAtPath<UnitSaveData>(AssetDatabase.GUIDToAssetPath(guid));
            if (unit != null && unit.health > 0) units.Add(unit);
        }
        if (units.Count == 0)
        {
            EditorUtility.DisplayDialog("Generate army", "No UnitSaveData assets were found under Resources/Prefabs/Units/NormieData.", "OK");
            return;
        }
        units.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        System.Random random = new System.Random(unchecked(firstSeed * 397 + side * 7919 + formationCount));
        army.Clear();
        BattleLane[] lanes = { BattleLane.Centre, BattleLane.UpperWing, BattleLane.LowerWing };
        for (int i = 0; i < formationCount; i++)
        {
            UnitSaveData unit = units[random.Next(units.Count)];
            BattleLane lane = lanes[i % lanes.Length];
            SectorBenchmarkArmyEntry existing = army.Find(item => item.Unit == unit && item.Lane == lane);
            if (existing != null) existing.Count++;
            else army.Add(new SectorBenchmarkArmyEntry { Unit = unit, Count = 1, Lane = lane });
        }
    }

    private static List<SectorBenchmarkArmySpec> MakeSpecs(List<SectorBenchmarkArmyEntry> army)
    {
        List<SectorBenchmarkArmySpec> specs = new List<SectorBenchmarkArmySpec>();
        foreach (SectorBenchmarkArmyEntry entry in army)
            if (entry != null && entry.Unit != null && entry.Count > 0)
                specs.Add(new SectorBenchmarkArmySpec { AssetPath = AssetDatabase.GetAssetPath(entry.Unit),
                    UnitName = string.IsNullOrEmpty(entry.Unit.unitname) ? entry.Unit.name : entry.Unit.unitname,
                    Count = entry.Count, Lane = entry.Lane });
        return specs;
    }
    private void BeginRun()
    {
        if (SectorBattleRules.Current == null)
        {
            EditorUtility.DisplayDialog("Sector benchmarks", "Resources/SectorBattleRules could not be loaded.", "OK");
            return;
        }
        if (useGeneratedArmies && (MakeSpecs(armyA).Count == 0 || MakeSpecs(armyB).Count == 0))
        {
            EditorUtility.DisplayDialog("Sector benchmarks", "Generate or add at least one valid unit to both armies.", "OK");
            return;
        }
        int low = variable == SectorBenchmarkVariable.None ? 0 : Math.Min(minimum, maximum);
        int high = variable == SectorBenchmarkVariable.None ? 0 : Math.Max(minimum, maximum);
        pending = new Queue<SectorBenchmarkViewRequest>();
        for (int value = low; value <= high; value += Math.Max(1, step))
        {
            for (int seedOffset = 0; seedOffset < repetitions; seedOffset++)
                pending.Enqueue(new SectorBenchmarkViewRequest { Preset = preset, Variable = variable, Value = value,
                    Seed = (ulong)(firstSeed + seedOffset), CapacityA = capacityA, CapacityB = capacityB,
                    FactionA = factionA, FactionB = factionB, GeneralA = generalA, GeneralB = generalB, TacticA = tacticA, TacticB = tacticB,
                    FactionAssetPathA = templateA != null && templateA.faction != null ? AssetDatabase.GetAssetPath(templateA.faction) : string.Empty,
                    FactionAssetPathB = templateB != null && templateB.faction != null ? AssetDatabase.GetAssetPath(templateB.faction) : string.Empty,
                    ArmyA = useGeneratedArmies ? MakeSpecs(armyA) : new List<SectorBenchmarkArmySpec>(),
                    ArmyB = useGeneratedArmies ? MakeSpecs(armyB) : new List<SectorBenchmarkArmySpec>() });
            if (variable == SectorBenchmarkVariable.None || value > high - Math.Max(1, step)) break;
        }
        results.Clear();
        totalRuns = pending.Count;
        running = true;
        status = "Starting " + totalRuns + " battles...";
        EditorApplication.update -= RunNext;
        EditorApplication.update += RunNext;
    }

    private void RunNext()
    {
        if (!running || pending == null || pending.Count == 0)
        {
            StopRun("Completed " + results.Count + " battles.");
            return;
        }
        SectorBenchmarkViewRequest request = pending.Dequeue();
        results.Add(SectorBattleBenchmarkRunner.Run(request));
        status = "Battle " + results.Count + " / " + totalRuns;
        Repaint();
    }

    private void StopRun(string message)
    {
        running = false;
        EditorApplication.update -= RunNext;
        status = message;
        Repaint();
    }

    private void DrawFilters()
    {
        if (results.Count == 0) return;
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        string[] filterLabels = { "All winners", "Draw", "Side A", "Side B" };
        int popup = winnerFilter == -2 ? 0 : winnerFilter == -1 ? 1 : winnerFilter + 2;
        popup = EditorGUILayout.Popup("Filter", popup, filterLabels, GUILayout.MaxWidth(260));
        winnerFilter = popup == 0 ? -2 : popup == 1 ? -1 : popup - 2;
        seedFilter = EditorGUILayout.TextField("Seed contains", seedFilter, GUILayout.MaxWidth(260));
        sortColumn = EditorGUILayout.Popup("Sort", sortColumn, new[] { "Run order", "Seed", "Value", "Winner", "Ticks", "Runtime", "Casualties A", "Casualties B" }, GUILayout.MaxWidth(280));
        descending = GUILayout.Toggle(descending, "Descending", GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawResults()
    {
        if (results.Count == 0) return;
        List<SectorBenchmarkResult> visible = new List<SectorBenchmarkResult>();
        foreach (SectorBenchmarkResult result in results)
            if ((winnerFilter == -2 || result.Winner == winnerFilter) &&
                (string.IsNullOrEmpty(seedFilter) || result.Seed.ToString().Contains(seedFilter))) visible.Add(result);
        Sort(visible);
        EditorGUILayout.LabelField(visible.Count + " of " + results.Count + " results", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (SectorBenchmarkResult result in visible)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Seed " + result.Seed + " | " + (result.ArmyA != null && result.ArmyA.Count > 0 ? "Generated armies" : result.Preset.ToString()) + " | " +
                (result.Variable == SectorBenchmarkVariable.None ? "baseline" : result.Variable + " = " + result.Value), EditorStyles.boldLabel);
            if (GUILayout.Button("View Battle", GUILayout.Width(100))) SectorBattleBenchmarkPlayModeViewer.View(result);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(result.Error))
                EditorGUILayout.HelpBox(result.Error, MessageType.Error);
            else
            {
                EditorGUILayout.LabelField(result.WinnerLabel + " | " + result.Ticks + " ticks | " + result.RuntimeMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + " ms");
                EditorGUILayout.LabelField("Strength A " + result.RemainingStrengthA + "/" + result.InitialStrengthA +
                    " (lost " + result.CasualtiesA + ") | B " + result.RemainingStrengthB + "/" + result.InitialStrengthB + " (lost " + result.CasualtiesB + ")");
                EditorGUILayout.LabelField("Routed A/B: " + result.RoutedA + "/" + result.RoutedB + " | " + result.EndReason);
                EditorGUILayout.LabelField(result.FactionA + ": " + result.GeneralA + " (" + result.TacticA + ") vs " +
                    result.FactionB + ": " + result.GeneralB + " (" + result.TacticB + ")", EditorStyles.miniBoldLabel);
                if (result.UnitPerformance != null)
                    foreach (SectorUnitPerformance unit in result.UnitPerformance)
                        EditorGUILayout.LabelField("  Side " + (unit.Side == 0 ? "A" : "B") + " | " + unit.UnitName +
                            " | formations " + unit.Formations + " | strength " + unit.RemainingStrength + "/" + unit.InitialStrength +
                            " | lost " + unit.Casualties + " | routed " + unit.Routed + " | attacks " + unit.Attacks + " | damage " + unit.DamageDealt,
                            EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void Sort(List<SectorBenchmarkResult> values)
    {
        if (sortColumn == 0) { if (descending) values.Reverse(); return; }
        values.Sort((a, b) =>
        {
            int comparison;
            switch (sortColumn)
            {
                case 1: comparison = a.Seed.CompareTo(b.Seed); break;
                case 2: comparison = a.Value.CompareTo(b.Value); break;
                case 3: comparison = a.Winner.CompareTo(b.Winner); break;
                case 4: comparison = a.Ticks.CompareTo(b.Ticks); break;
                case 5: comparison = a.RuntimeMilliseconds.CompareTo(b.RuntimeMilliseconds); break;
                case 6: comparison = a.CasualtiesA.CompareTo(b.CasualtiesA); break;
                default: comparison = a.CasualtiesB.CompareTo(b.CasualtiesB); break;
            }
            return descending ? -comparison : comparison;
        });
    }

    private void ExportCsv()
    {
        string initialDirectory = Path.GetDirectoryName(DefaultCsvPath);
        string chosen = EditorUtility.SaveFilePanel("Export sector benchmark CSV", initialDirectory, Path.GetFileNameWithoutExtension(DefaultCsvPath), "csv");
        if (string.IsNullOrEmpty(chosen)) return;
        File.WriteAllText(chosen, SectorBattleBenchmarkRunner.ToCsv(results));
        if (chosen.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) AssetDatabase.Refresh();
        status = "Exported " + results.Count + " results to " + chosen;
    }
}

internal static class SectorBattleBenchmarkRunner
{
    public static SectorBenchmarkResult Run(SectorBenchmarkViewRequest request)
    {
        SectorBenchmarkResult result = new SectorBenchmarkResult { Preset = request.Preset, Variable = request.Variable,
            Value = request.Value, Seed = request.Seed, CapacityA = request.CapacityA, CapacityB = request.CapacityB,
            FactionA = request.FactionA, FactionB = request.FactionB, GeneralA = request.GeneralA, GeneralB = request.GeneralB, TacticA = request.TacticA, TacticB = request.TacticB,
            FactionAssetPathA = request.FactionAssetPathA, FactionAssetPathB = request.FactionAssetPathB,
            ArmyA = request.ArmyA, ArmyB = request.ArmyB, Winner = -1 };
        SectorBattleRules rules = null;
        try
        {
            rules = UnityEngine.Object.Instantiate(SectorBattleRules.Current);
            rules.hideFlags = HideFlags.HideAndDontSave;
            ApplyRuleOverride(rules, request.Variable, request.Value);
            SectorBattleSimulation simulation = CreateSimulation(request, rules);
            ApplyStrengthOverride(simulation, request.Variable, request.Value);
            SumStrength(simulation, out result.InitialStrengthA, out result.InitialStrengthB);
            CaptureInitialUnitPerformance(simulation, result.UnitPerformance);
            Stopwatch timer = Stopwatch.StartNew();
            simulation.StartBattle();
            while (!simulation.IsResolved) simulation.TickBattle();
            timer.Stop();
            result.RuntimeMilliseconds = timer.Elapsed.TotalMilliseconds;
            result.Ticks = simulation.Tick;
            result.Winner = simulation.WinningSide;
            SumStrength(simulation, out result.RemainingStrengthA, out result.RemainingStrengthB);
            foreach (SectorFormation formation in simulation.Formations)
                if (formation.State == SectorFormationState.Routing) { if (formation.Side == 0) result.RoutedA++; else result.RoutedB++; }
            CaptureFinalUnitPerformance(simulation, result.UnitPerformance);
            result.EndReason = simulation.GetOutcome().EndReason;
        }
        catch (Exception exception)
        {
            result.Error = exception.GetType().Name + ": " + exception.Message;
        }
        finally
        {
            if (rules != null) UnityEngine.Object.DestroyImmediate(rules);
        }
        return result;
    }

    public static SectorBattleSimulation Recreate(SectorBenchmarkViewRequest request)
    {
        SectorBattleRules rules = UnityEngine.Object.Instantiate(SectorBattleRules.Current);
        rules.hideFlags = HideFlags.HideAndDontSave;
        ApplyRuleOverride(rules, request.Variable, request.Value);
        SectorBattleSimulation simulation = CreateSimulation(request, rules);
        ApplyStrengthOverride(simulation, request.Variable, request.Value);
        simulation.StartBattle();
        return simulation;
    }

    private static SectorBattleSimulation CreateSimulation(SectorBenchmarkViewRequest request, SectorBattleRules rules)
    {
        if (request.ArmyA != null && request.ArmyA.Count > 0 && request.ArmyB != null && request.ArmyB.Count > 0)
            return SectorCustomBattleFactory.PrepareGenerated(ToRuntimeArmy(request.ArmyA), ToRuntimeArmy(request.ArmyB),
                request.Seed, request.CapacityA, request.CapacityB, request.GeneralA, request.GeneralB,
                request.TacticA, request.TacticB, false, false, rules);
        return SectorCustomBattleFactory.Prepare(request.Preset, request.Seed, request.CapacityA, request.CapacityB,
            false, false, rules, request.TacticA, request.TacticB);
    }

    private static List<SectorCustomFormationSpec> ToRuntimeArmy(List<SectorBenchmarkArmySpec> stored)
    {
        List<SectorCustomFormationSpec> result = new List<SectorCustomFormationSpec>();
        foreach (SectorBenchmarkArmySpec item in stored)
        {
            UnitSaveData unit = AssetDatabase.LoadAssetAtPath<UnitSaveData>(item.AssetPath);
            if (unit != null && item.Count > 0) result.Add(new SectorCustomFormationSpec { Unit = unit, Count = item.Count, Lane = item.Lane });
        }
        return result;
    }

    private static void CaptureInitialUnitPerformance(SectorBattleSimulation simulation, List<SectorUnitPerformance> performance)
    {
        performance.Clear();
        foreach (SectorFormation formation in simulation.Formations)
        {
            string unitName = formation.Source != null && !string.IsNullOrEmpty(formation.Source.unitname) ? formation.Source.unitname : formation.Source != null ? formation.Source.name : "Unknown";
            SectorUnitPerformance item = performance.Find(value => value.Side == formation.Side && value.UnitName == unitName);
            if (item == null) { item = new SectorUnitPerformance { Side = formation.Side, UnitName = unitName }; performance.Add(item); }
            item.Formations++; item.InitialStrength += Math.Max(0, formation.Strength);
        }
    }

    private static void CaptureFinalUnitPerformance(SectorBattleSimulation simulation, List<SectorUnitPerformance> performance)
    {
        Dictionary<int, SectorUnitPerformance> byFormation = new Dictionary<int, SectorUnitPerformance>();
        foreach (SectorFormation formation in simulation.Formations)
        {
            string unitName = formation.Source != null && !string.IsNullOrEmpty(formation.Source.unitname) ? formation.Source.unitname : formation.Source != null ? formation.Source.name : "Unknown";
            SectorUnitPerformance item = performance.Find(value => value.Side == formation.Side && value.UnitName == unitName);
            if (item == null) continue;
            item.RemainingStrength += Math.Max(0, formation.Strength);
            if (formation.State == SectorFormationState.Routing) item.Routed++;
            byFormation[formation.Id] = item;
        }
        foreach (SectorCombatEvent battleEvent in simulation.Events)
            if (battleEvent.Type == SectorPresentationEventType.Attack && byFormation.TryGetValue(battleEvent.FormationId, out SectorUnitPerformance item))
            { item.Attacks++; item.DamageDealt += Math.Max(0, battleEvent.Damage); }
        performance.Sort((a, b) => { int side = a.Side.CompareTo(b.Side); return side != 0 ? side : string.CompareOrdinal(a.UnitName, b.UnitName); });
    }
    private static void SumStrength(SectorBattleSimulation simulation, out int a, out int b)
    {
        a = b = 0;
        foreach (SectorFormation formation in simulation.Formations)
            if (formation.Side == 0) a += Math.Max(0, formation.Strength); else b += Math.Max(0, formation.Strength);
    }

    private static void ApplyStrengthOverride(SectorBattleSimulation simulation, SectorBenchmarkVariable variable, int value)
    {
        int side = variable == SectorBenchmarkVariable.SideAStartingStrengthPercent ? 0 :
            variable == SectorBenchmarkVariable.SideBStartingStrengthPercent ? 1 : -1;
        if (side < 0) return;
        foreach (SectorFormation formation in simulation.Formations)
            if (formation.Side == side) formation.Strength = Math.Max(1, formation.Strength * Math.Max(1, value) / 100);
    }

    private static void ApplyRuleOverride(SectorBattleRules rules, SectorBenchmarkVariable variable, int value)
    {
        switch (variable)
        {
            case SectorBenchmarkVariable.RoutMorale: rules.routMorale = value; break;
            case SectorBenchmarkVariable.ExhaustionPerAttack: rules.exhaustionPerAttack = Math.Max(0, value); break;
            case SectorBenchmarkVariable.FlankFrontage: rules.flankFrontage = Math.Max(0, value); break;
            case SectorBenchmarkVariable.FlankMoralePenalty: rules.flankMoralePenalty = Math.Max(0, value); break;
            case SectorBenchmarkVariable.RangedSupportDamagePercent: rules.rangedSupportDamagePercent = Math.Max(0, value); break;
            case SectorBenchmarkVariable.AiDecisionIntervalTicks: rules.aiDecisionIntervalTicks = Math.Max(1, value); break;
            case SectorBenchmarkVariable.AiForwardScore: rules.aiForwardScore = value; break;
            case SectorBenchmarkVariable.AiEnemyScore: rules.aiEnemyScore = value; break;
            case SectorBenchmarkVariable.AiContestedScore: rules.aiContestedScore = value; break;
            case SectorBenchmarkVariable.AiFlankScore: rules.aiFlankScore = value; break;
            case SectorBenchmarkVariable.AiWideScore: rules.aiWideScore = value; break;
        }
    }

    public static string ToCsv(List<SectorBenchmarkResult> results)
    {
        StringBuilder csv = new StringBuilder("preset,variable,value,seed,capacity_a,capacity_b,winner,ticks,initial_a,initial_b,remaining_a,remaining_b,casualties_a,casualties_b,routed_a,routed_b,runtime_ms,faction_a,general_a,tactic_a,faction_b,general_b,tactic_b,unit_performance,end_reason,error\n");
        foreach (SectorBenchmarkResult result in results)
            csv.Append(Cell(result.Preset.ToString())).Append(',').Append(Cell(result.Variable.ToString())).Append(',').Append(result.Value).Append(',').Append(result.Seed)
                .Append(',').Append(result.CapacityA).Append(',').Append(result.CapacityB).Append(',').Append(result.Winner).Append(',').Append(result.Ticks)
                .Append(',').Append(result.InitialStrengthA).Append(',').Append(result.InitialStrengthB).Append(',').Append(result.RemainingStrengthA).Append(',').Append(result.RemainingStrengthB)
                .Append(',').Append(result.CasualtiesA).Append(',').Append(result.CasualtiesB).Append(',').Append(result.RoutedA).Append(',').Append(result.RoutedB)
                .Append(',').Append(result.RuntimeMilliseconds.ToString("F6", CultureInfo.InvariantCulture)).Append(',').Append(Cell(result.FactionA)).Append(',').Append(Cell(result.GeneralA)).Append(',').Append(Cell(result.TacticA.ToString()))
                .Append(',').Append(Cell(result.FactionB)).Append(',').Append(Cell(result.GeneralB)).Append(',').Append(Cell(result.TacticB.ToString())).Append(',').Append(Cell(UnitSummary(result.UnitPerformance)))
                .Append(',').Append(Cell(result.EndReason)).Append(',').Append(Cell(result.Error)).Append('\n');
        return csv.ToString();
    }

    private static string UnitSummary(List<SectorUnitPerformance> performance)
    {
        if (performance == null) return string.Empty;
        List<string> values = new List<string>();
        foreach (SectorUnitPerformance unit in performance)
            values.Add((unit.Side == 0 ? "A:" : "B:") + unit.UnitName + " formations=" + unit.Formations +
                " initial=" + unit.InitialStrength + " remaining=" + unit.RemainingStrength + " casualties=" + unit.Casualties +
                " routed=" + unit.Routed + " attacks=" + unit.Attacks + " damage=" + unit.DamageDealt);
        return string.Join("; ", values);
    }

    private static string Cell(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
}

[InitializeOnLoad]
internal static class SectorBattleBenchmarkPlayModeViewer
{
    private const string PendingKey = "ProjectX.SectorBenchmark.PendingView";
    private static int attempts;

    static SectorBattleBenchmarkPlayModeViewer()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (EditorApplication.isPlaying && EditorPrefs.HasKey(PendingKey)) BeginLaunch();
    }

    public static void View(SectorBenchmarkResult result)
    {
        SectorBenchmarkViewRequest request = new SectorBenchmarkViewRequest { Preset = result.Preset, Variable = result.Variable,
            Value = result.Value, Seed = result.Seed, CapacityA = result.CapacityA, CapacityB = result.CapacityB,
            FactionA = result.FactionA, FactionB = result.FactionB, GeneralA = result.GeneralA, GeneralB = result.GeneralB, TacticA = result.TacticA, TacticB = result.TacticB,
            FactionAssetPathA = result.FactionAssetPathA, FactionAssetPathB = result.FactionAssetPathB,
            ArmyA = result.ArmyA, ArmyB = result.ArmyB };
        EditorPrefs.SetString(PendingKey, JsonUtility.ToJson(request));
        if (EditorApplication.isPlaying) BeginLaunch();
        else EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.HasKey(PendingKey)) BeginLaunch();
    }

    private static void BeginLaunch()
    {
        attempts = 0;
        EditorApplication.update -= TryLaunch;
        EditorApplication.update += TryLaunch;
    }

    private static void TryLaunch()
    {
        if (!EditorApplication.isPlaying || !EditorPrefs.HasKey(PendingKey))
        {
            EditorApplication.update -= TryLaunch;
            return;
        }
        SectorBattleCampaignManager manager = SectorBattleCampaignManager.Instance;
        if (manager == null)
        {
            new GameObject("Sector Benchmark Viewer").AddComponent<SectorBattleCampaignManager>();
            return;
        }
        if (manager.CustomBattleLauncher == null || SectorBattlePresentation.Instance == null)
        {
            if (++attempts < 300) return;
            UnityEngine.Debug.LogError("Sector benchmark viewer could not initialize the battle presentation.");
            EditorPrefs.DeleteKey(PendingKey);
            EditorApplication.update -= TryLaunch;
            return;
        }
        string json = EditorPrefs.GetString(PendingKey);
        EditorPrefs.DeleteKey(PendingKey);
        EditorApplication.update -= TryLaunch;
        SectorBenchmarkViewRequest request = JsonUtility.FromJson<SectorBenchmarkViewRequest>(json);
        SectorBattleSimulation simulation = SectorBattleBenchmarkRunner.Recreate(request);
        Faction factionA = string.IsNullOrEmpty(request.FactionAssetPathA) ? null : AssetDatabase.LoadAssetAtPath<Faction>(request.FactionAssetPathA);
        Faction factionB = string.IsNullOrEmpty(request.FactionAssetPathB) ? null : AssetDatabase.LoadAssetAtPath<Faction>(request.FactionAssetPathB);
        SectorCampaignBattle battle = manager.RegisterCustomBattle(simulation,
            "Benchmark " + request.Preset + " seed " + request.Seed + " (" + request.Variable + "=" + request.Value + ")",
            request.FactionA, request.FactionB, factionA, factionB);
        SectorBattlePresentation.Instance.OpenViewer(battle);
    }
}
#endif
