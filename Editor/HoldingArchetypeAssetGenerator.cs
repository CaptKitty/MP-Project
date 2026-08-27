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
            if (source == null || source.StableId == "CitizenFarm") continue;
            string path = Folder + "/" + source.StableId + ".asset";
            HoldingDefinition existing = AssetDatabase.LoadAssetAtPath<HoldingDefinition>(path);
            if (existing != null)
            {
                bool migrated = false;
                if (existing.tags == HoldingTag.None)
                { existing.tags = HoldingEvolutionSystem.EffectiveTags(existing); migrated = true; }
                if (existing.canRaiseLevies && existing.levyArchetype == LevyArchetype.None)
                { existing.levyArchetype = HoldingEvolutionSystem.EffectiveLevyArchetype(existing); migrated = true; }
                if (migrated) { EditorUtility.SetDirty(existing); changed = true; }
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
