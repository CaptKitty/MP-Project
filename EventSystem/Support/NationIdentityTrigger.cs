using System;
using UnityEngine;

public enum NationIdentityKind { Nation, Faction, Civilization, Culture, Religion }

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/Nation Identity")]
public class NationIdentityTrigger : BaseTrigger
{
    public NationIdentityKind identityKind;
    public string identityName;
    public bool mustMatch = true;

    public override bool CanTrigger(EventContext context)
    {
        Nation target = context != null ? context.ResolveNation() : null;
        if (target == null) return false;
        string actual = identityKind == NationIdentityKind.Nation ? target.name :
            identityKind == NationIdentityKind.Faction ? (target.faction != null ? target.faction.name : string.Empty) :
            identityKind == NationIdentityKind.Civilization ? (target.civilization != null ? target.civilization.name : string.Empty) :
            identityKind == NationIdentityKind.Culture ? (target.culture != null ? target.culture.name : string.Empty) :
            target.religion != null ? target.religion.name : string.Empty;
        bool matches = string.Equals(actual, identityName, StringComparison.OrdinalIgnoreCase);
        return mustMatch ? matches : !matches;
    }
}
