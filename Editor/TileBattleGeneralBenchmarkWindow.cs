using System.Collections.Generic;
using System.IO;
using ProjectX.TileBattle;
using UnityEditor;
using UnityEngine;

public sealed class TileBattleGeneralBenchmarkWindow : EditorWindow
{
    private List<TileGeneralBenchmarkResult> results;
    private Vector2 scroll;

    [MenuItem("Window/Project X/Tile General Benchmarks")]
    public static void Open() => GetWindow<TileBattleGeneralBenchmarkWindow>("General Benchmarks");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Tile Battle General Skill Benchmarks", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Runs mirrored deterministic battles so side orientation does not disguise general behaviour. Results include plan changes, reserve use, attacks, losses and winners.", MessageType.Info);
        if (GUILayout.Button("Run Standard Benchmark Suite", GUILayout.Height(30)))
        {
            results = TileBattleGeneralBenchmark.RunStandardSuite();
            string path = Path.Combine(Application.dataPath, "BattleTestResults/tile-general-benchmarks.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, TileBattleGeneralBenchmark.ToCsv(results));
            AssetDatabase.Refresh();
        }
        if (results == null) return;
        EditorGUILayout.LabelField(results.Count + " matches completed. CSV: Assets/BattleTestResults/tile-general-benchmarks.csv");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < results.Count; i++)
        {
            TileGeneralBenchmarkResult r = results[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(r.Scenario + " — " + r.LeftGeneral + " vs " + r.RightGeneral, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Winner: " + r.Winner + " | rounds " + r.Rounds + " | strength " + r.LeftStrength + "–" + r.RightStrength);
            EditorGUILayout.LabelField("Plans: " + r.LeftFinalPlan + " / " + r.RightFinalPlan + " | changes " + r.LeftPlanChanges + " / " + r.RightPlanChanges);
            EditorGUILayout.LabelField("Attacks " + r.Attacks + " | pushes " + r.Pushes + " | reserves " + r.ReserveCommitments);
            EditorGUILayout.LabelField("Reserve rounds: " + r.LeftFirstReserveRound + " / " + r.RightFirstReserveRound +
                " | rows " + r.LeftReserveRows + " / " + r.RightReserveRows);
            EditorGUILayout.LabelField("Reserve purpose: " + r.LeftReservePurposes + " / " + r.RightReservePurposes +
                " | surviving strength " + r.LeftReserveSurvival + " / " + r.RightReserveSurvival);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }
}
