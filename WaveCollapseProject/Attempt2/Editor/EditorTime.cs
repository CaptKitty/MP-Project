using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TilemapEditor))]
public class EditorTime : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TilemapEditor myScript = (TilemapEditor)target;
        if (GUILayout.Button("Grab Inputs"))
        {
            myScript.GrabInputs();
        }
        if (GUILayout.Button("Generate Terrain"))
        {
            myScript.GenerateTerrain();
        }
    }
}
