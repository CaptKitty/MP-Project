using UnityEngine;

namespace ProjectX.SectorBattle
{
    [CreateAssetMenu(fileName = "SectorBattleVisualSettings", menuName = "Combat/Sector Battle Visual Settings")]
    public sealed class SectorBattleVisualSettings : ScriptableObject
    {
        [Tooltip("Palette material used by layered unit sprites. Assign New Material 1.")]
        public Material UnitMaterial;
    }
}
