using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/Nation Flag")]
public class NationFlagTrigger : BaseTrigger
{
    public string flag;
    public bool mustBePresent = true;

    public override bool CanTrigger(EventContext context)
    {
        Nation target = context != null ? context.ResolveNation() : null;
        bool present = target != null && NationContentResolver.HasFlag(target, flag);
        return target != null && (mustBePresent ? present : !present);
    }
}
