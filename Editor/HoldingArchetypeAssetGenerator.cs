#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HoldingArchetypeAssetGenerator
{
    private const string Folder = "Assets/Resources/Prefabs/NationData/HoldingData";

    static HoldingArchetypeAssetGenerator()
    {
        EditorApplication.delayCall += EnsureAssets;
    }

    [MenuItem("ProjectX/Holdings/Regenerate Missing Built-in Holdings")]
    public static void EnsureAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
        bool changed = false;
        foreach (HoldingDefinition source in HoldingArchetypeCatalog.All())
        {
            if (source == null) continue;
            string path = Folder + "/" + source.StableId + ".asset";
            HoldingDefinition existing = AssetDatabase.LoadAssetAtPath<HoldingDefinition>(path);
            if (existing != null)
            {
                // Mine and Workshop used to share IDs with tiered holdings. Rebind
                // those assets to the canonical schema while retaining their art.
                Sprite retainedIcon = existing.icon;
                EditorUtility.CopySerialized(source, existing);
                existing.name = source.StableId;
                existing.hideFlags = HideFlags.None;
                if (retainedIcon != null) existing.icon = retainedIcon;
                EditorUtility.SetDirty(existing); changed = true;
                continue;
            }
            HoldingDefinition asset = ScriptableObject.CreateInstance<HoldingDefinition>();
            EditorUtility.CopySerialized(source, asset);
            asset.name = source.StableId;
            asset.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(asset, path);
            changed = true;
        }
        if (!changed) return;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated missing built-in holding definition assets.");
    }
}
#endif
