using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/Nation Gold")]
public class NationGoldTrigger : BaseTrigger
{
    public int gold;
    public bool atLeast = true;

    public override bool CanTrigger(EventContext context)
    {
        Nation target = context != null ? context.ResolveNation() : null;
        return target != null && (atLeast ? target.Gold >= gold : target.Gold < gold);
    }
}
