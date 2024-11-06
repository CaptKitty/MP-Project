using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ForageAbility))]
public class CubeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if(GUILayout.Button("Generate Color"))
        {
            Debug.Log("Colortime");
        }
    }
}