using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Effect/Change Nation Gold")]
public class ChangeNationGoldEffect : BaseEffect
{
    public int amount;

    public override void Execute(EventContext context)
    {
        Nation target = context != null ? context.ResolveNation() : null;
        if (target != null) target.Gold = Mathf.Max(0, target.Gold + amount);
    }
}
