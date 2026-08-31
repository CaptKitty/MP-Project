using System;
using UnityEngine;

/// <summary>
/// Explicit campaign target for an event. Nation is the primary target; the
/// remaining fields are optional so the same event pipeline can also describe
/// provincial, holding, or army incidents without consulting player globals.
/// </summary>
[Serializable]
public sealed class EventContext
{
    public string nationName;
    [NonSerialized] public Nation nation;
    [NonSerialized] public Province province;
    [NonSerialized] public ProvinceHolding holding;
    [NonSerialized] public FieldArmyHolder army;

    public Nation ResolveNation()
    {
        if (nation != null) return nation;
        if (army != null && army.fieldArmy != null) nation = army.fieldArmy.nation;
        else if (province != null) nation = province.nation;
        else if (Owners.Instance != null && !string.IsNullOrEmpty(nationName))
            nation = Owners.Instance.nationlist.Find(candidate => candidate != null &&
                string.Equals(candidate.name, nationName, StringComparison.OrdinalIgnoreCase));
        else if (Owners.Instance != null)
            nation = Owners.Instance.CallPlayer();
        if (nation != null) nationName = nation.name;
        return nation;
    }

    public FieldArmyHolder ResolveArmy()
    {
        if (army != null) return army;
        Nation target = ResolveNation();
        if (target == null && FieldArmyHolder.PlayerFieldArmy != null) return FieldArmyHolder.PlayerFieldArmy;
        if (target == null || target.armies == null) return null;
        return target.armies.Find(candidate => candidate != null);
    }

    public static EventContext ForNation(string targetNation) => new EventContext { nationName = targetNation };
}
