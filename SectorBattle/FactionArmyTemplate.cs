using System.Collections.Generic;
using UnityEngine;

namespace ProjectX.SectorBattle
{
    [CreateAssetMenu(fileName = "FactionArmyTemplate", menuName = "Combat/Faction Army Template")]
    public sealed class FactionArmyTemplate : ScriptableObject
    {
        [Tooltip("Faction identity used for names and unit colours when this template is viewed in a sector benchmark.")]
        public Faction faction;
        public string factionName = "Faction";
        public string generalName = "General";
        public SectorGeneralTactic tactic = SectorGeneralTactic.WingedCenter;
        [Range(4, 8)] public int commandGroupCapacity = 6;
        public List<SectorCustomFormationSpec> formations = new List<SectorCustomFormationSpec>();
    }
}
