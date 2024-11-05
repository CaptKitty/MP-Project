using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class AdvancedRuleTile : RuleTile<AdvancedRuleTile.Neighbor> {
    public bool customField;

    public bool alwaysConnect;
    public TileBase[] SandTile;
    public TileBase[] WaterTile;
    public TileBase[] GrassTile;
    public TileBase[] RiverTile;
    public TileBase[] MountainTile;
    public TileBase[] ForestTile;
    public bool checkSelf;

    public class Neighbor : RuleTile.TilingRule.Neighbor {
        public const int This = 1;
        public const int NotThis = 2;
        public const int Any = 3;
        public const int Nothing = 4;
        public const int SandTile = 5;
        public const int WaterTile = 6;
        public const int GrassTile = 7;
        public const int RiverTile = 8;
        public const int MountainTile = 9;
        public const int ForestTile = 10;
    }

    public override bool RuleMatch(int neighbor, TileBase tile) {
        switch (neighbor) {
            case Neighbor.This: return Check_This(tile);
            case Neighbor.NotThis: return Check_NotThis(tile);
            case Neighbor.Any: return Check_Any(tile);
            case Neighbor.SandTile: return SandTiles(tile);
            case Neighbor.WaterTile: return WaterTiles(tile);
            case Neighbor.GrassTile: return GrassTiles(tile);
            case Neighbor.RiverTile: return RiverTiles(tile);
            case Neighbor.MountainTile: return MountainTiles(tile);
            case Neighbor.ForestTile: return ForestTiles(tile);
            case Neighbor.Nothing: return Check_Nothing(tile);
        }
        return base.RuleMatch(neighbor, tile);
    }
    bool Check_This(TileBase tile)
    {
        if(!alwaysConnect) return tile == this;
        else return SandTile.Contains(tile) || tile == this;
    }
    bool Check_NotThis(TileBase tile)
    {
        return tile != this;
    }
    bool Check_Any(TileBase tile)
    {
        if(checkSelf) return tile != null;
        else return tile != null && tile != this;
    }
    bool SandTiles(TileBase tile)
    {
        if (SandTile.Contains(tile)) return tile != null;
        else return tile != null && tile != this && tile == SandTile.Contains(tile);
    }
    bool WaterTiles(TileBase tile)
    {
        if (WaterTile.Contains(tile)) return tile != null;
        else return tile != null && tile != this && tile == WaterTile.Contains(tile);
    }
    bool GrassTiles(TileBase tile)
    {
        if (GrassTile.Contains(tile)) return tile != null;
        else return tile != null && tile != this && tile == GrassTile.Contains(tile);
    }
    bool RiverTiles(TileBase tile)
    {
        if (RiverTile.Contains(tile)) return tile != null;
        else return tile != null && tile != this && tile == RiverTile.Contains(tile);
    }
    bool MountainTiles(TileBase tile)
    {
        if (MountainTile.Contains(tile)) return tile != null;
        else return tile != null && tile != this && tile == MountainTile.Contains(tile);
    }
    bool ForestTiles(TileBase tile)
    {
        if (ForestTile.Contains(tile)) return tile != null;
        else return tile != null && tile != this && tile == ForestTile.Contains(tile);
    }
    bool Check_Nothing(TileBase tile)
    {
        return tile == null;
    }
} 