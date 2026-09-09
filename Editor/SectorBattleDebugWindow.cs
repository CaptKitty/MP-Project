#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectX.SectorBattle;

public sealed class SectorBattleDebugWindow : EditorWindow
{
    private SectorBattleSimulation simulation;
    private Vector2 scroll;

    [MenuItem("Project X/Sector Battle Debugger")]
    public static void Open() => GetWindow<SectorBattleDebugWindow>("Sector Battle");

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create deterministic 10v10")) simulation = SectorBattleDebugScenario.Create();
        GUI.enabled = simulation != null && !simulation.IsResolved;
        if (GUILayout.Button("Step")) SectorBattleDebugScenario.AdvanceOneTick(simulation);
        if (GUILayout.Button("Run 10 ticks")) for (int i = 0; i < 10 && !simulation.IsResolved; i++) SectorBattleDebugScenario.AdvanceOneTick(simulation);
        if (GUILayout.Button("Run to completion")) while (!simulation.IsResolved) SectorBattleDebugScenario.AdvanceOneTick(simulation);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        if (simulation == null) { EditorGUILayout.HelpBox("Create the test battle to inspect its 5x3 sectors.", MessageType.Info); return; }

        EditorGUILayout.LabelField("Tick " + simulation.Tick + "  Hash " + simulation.ComputeHash() +
            (simulation.IsResolved ? "  Winner: " + simulation.WinningSide : string.Empty), EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (SectorDebugState sector in simulation.GetDebugState())
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(sector.Coordinate + " — " + sector.Terrain + " — " + sector.Control +
                " — Frontage " + sector.BaseFrontage, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Side A present: " + string.Join(", ", sector.SideA));
            EditorGUILayout.LabelField("Side A active: " + string.Join(", ", sector.ActiveA) + " | support: " + string.Join(", ", sector.ReserveA));
            EditorGUILayout.LabelField("Side B present: " + string.Join(", ", sector.SideB));
            EditorGUILayout.LabelField("Side B active: " + string.Join(", ", sector.ActiveB) + " | support: " + string.Join(", ", sector.ReserveB));
            if (sector.FlankAttackers.Count > 0) EditorGUILayout.LabelField("Flank: " + string.Join("; ", sector.FlankAttackers));
            if (sector.RangedSupporters.Count > 0) EditorGUILayout.LabelField("Ranged: " + string.Join("; ", sector.RangedSupporters));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Recent combat log", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(simulation.GetDebugText(), GUILayout.MinHeight(300));
        EditorGUILayout.EndScrollView();
    }
}
#endif
