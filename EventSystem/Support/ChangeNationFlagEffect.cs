using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Effect/Change Nation Flag")]
public class ChangeNationFlagEffect : BaseEffect
{
    public string flag;
    public bool add = true;

    public override void Execute(EventContext context)
    {
        Nation target = context != null ? context.ResolveNation() : null;
        if (target == null || target.faction == null || string.IsNullOrWhiteSpace(flag)) return;
        if (target.faction.Flaglist == null) target.faction.Flaglist = new System.Collections.Generic.List<string>();
        if (add)
        {
            if (!target.faction.Flaglist.Contains(flag)) target.faction.Flaglist.Add(flag);
        }
        else target.faction.Flaglist.Remove(flag);
    }
}
