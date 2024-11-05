using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityEditor
{
    [CustomEditor(typeof(AdvancedRuleTile))]
    [CanEditMultipleObjects]
    public class AdvancedRuleTileEditor : RuleTileEditor
    {
        public Texture2D SandTileIcon;
        public Texture2D WaterTile;
        public Texture2D GrassTile;
        public Texture2D RiverTile;
        public Texture2D MountainTile;
        public Texture2D ForestTile;
        
        public override void RuleOnGUI(Rect rect, Vector3Int position, int neighbor)
        {
            switch(neighbor)
            {
                case 5:
                    GUI.DrawTexture(rect, SandTileIcon);
                    return; 
                case 6:
                    GUI.DrawTexture(rect, WaterTile);
                    return;
                case 7:
                    GUI.DrawTexture(rect, GrassTile);
                    return;
                case 8:
                    GUI.DrawTexture(rect, RiverTile);
                    return;
                case 9:
                    GUI.DrawTexture(rect, MountainTile);
                    return;
                case 10:
                    GUI.DrawTexture(rect, ForestTile);
                    return;
            }

            base.RuleOnGUI(rect, position, neighbor);
        }
    }
    
}