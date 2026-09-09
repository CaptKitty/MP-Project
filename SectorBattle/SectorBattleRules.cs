using System.Collections.Generic;
using UnityEngine;

namespace ProjectX.SectorBattle
{
    [CreateAssetMenu(menuName = "Combat/Sector Battle Rules")]
    public sealed class SectorBattleRules : ScriptableObject
    {
        private static SectorBattleRules cached;
        public int ticksPerSecond = 5;
        [Min(1)] public int combatIntervalTicks = 3;
        public int maximumTicks = 2000;
        public int baseDamage = 10;
        public int routMorale = 150;
        public int exhaustionPerAttack = 8;
        public int flankFrontage = 2;
        public int flankMoralePenalty;
        public int rangedAdjacentRange = 1;
        [Range(0, 200)] public int rangedSupportDamagePercent = 75;
        [Range(0, 200)] public int phalanxCombatPercent = 125;
        public int phalanxRequiredFormations = 3;
        [Range(0, 200)] public int warchiefCombatPercent = 110;
        [Range(0, 200)] public int warcryCombatPercent = 125;
        public int warcryDurationTicks = 8;
        [Range(0, 200)] public int disciplinedExhaustionPercent = 60;
        public int openingVolleyDamage = 10;
        [Range(0, 100)] public int backlineCollapseCasualtyPercent = 35;
        [Min(1)] public int uncontestedEnemyReserveTicksToRout = 5;
        public int[] columnFrontage = { 4, 5, 7, 5, 4 };
        public List<SectorTerrainRule> terrainRules = new List<SectorTerrainRule>();

        public static SectorBattleRules Current => cached != null ? cached :
            (cached = Resources.Load<SectorBattleRules>("SectorBattleRules"));
        public int Frontage(BattleLane lane, SectorTerrain terrain)
        {
            int index = Mathf.Clamp((int)lane, 0, 4);
            int value = columnFrontage != null && columnFrontage.Length > index ? columnFrontage[index] : index == 2 ? 7 : 4;
            SectorTerrainRule rule = Terrain(terrain);
            return Mathf.Max(1, value + (rule != null ? rule.frontageModifier : 0));
        }
        public SectorTerrainRule Terrain(SectorTerrain terrain) => terrainRules != null
            ? terrainRules.Find(item => item != null && item.terrain == terrain) : null;
    }
}
