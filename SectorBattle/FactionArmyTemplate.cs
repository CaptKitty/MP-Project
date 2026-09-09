using System.Collections.Generic;
using UnityEngine;

namespace ProjectX.SectorBattle
{
    [CreateAssetMenu(fileName = "FactionArmyTemplate", menuName = "Combat/Faction Army Template")]
    public sealed class FactionArmyTemplate : ScriptableObject
    {
        public string factionName = "Faction";
        public string generalName = "General";
        public SectorGeneralTactic tactic = SectorGeneralTactic.Standard;
        [Range(4, 8)] public int commandGroupCapacity = 6;
        public List<SectorCustomFormationSpec> formations = new List<SectorCustomFormationSpec>();
    }
}