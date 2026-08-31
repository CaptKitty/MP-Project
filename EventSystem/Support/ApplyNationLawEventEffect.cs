using System;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Effect/Apply Nation Law")]
public class ApplyNationLawEventEffect : BaseEffect
{
    public NationalLaw law;
    [Tooltip("When enabled the law enters the political proposal queue instead of taking effect immediately.")]
    public bool propose;
    [Min(1)] public int debateTicks = 8;

    public override void Execute(EventContext context)
    {
        Nation target = context != null ? context.ResolveNation() : null;
        if (target == null || law == null) return;
        NationalLaw copy = law.Clone();
        if (propose)
        {
            PoliticalProposalSystem.ProposeLaw(target, copy, "event", debateTicks);
            return;
        }
        if (target.laws == null) target.laws = new System.Collections.Generic.List<NationalLaw>();
        int existing = target.laws.FindIndex(candidate => candidate != null &&
            string.Equals(candidate.id, copy.id, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) target.laws[existing] = copy;
        else target.laws.Add(copy);
        target.ResetLawResolution();
    }
}
