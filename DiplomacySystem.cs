using System;
using UnityEngine;

public static class DiplomacySystem
{
    public static bool IsTributarySubjectOf(Nation subject, Nation master)
    {
        return subject != null && master != null && !string.IsNullOrWhiteSpace(subject.TributaryMasterName) &&
            string.Equals(subject.TributaryMasterName, master.name, StringComparison.OrdinalIgnoreCase);
    }

    public static bool AreTributaryAllies(Nation first, Nation second) =>
        IsTributarySubjectOf(first, second) || IsTributarySubjectOf(second, first) ||
        first != null && second != null && FindMaster(first) != null && FindMaster(first) == FindMaster(second);

    public static bool AreFriendly(Nation first, Nation second) =>
        first != null && second != null && (first == second || AreTributaryAllies(first, second));

    public static bool AreHostile(Nation first, Nation second) =>
        first != null && second != null && !AreFriendly(first, second) && AreAtWar(first, second);

    public static bool AreAtWar(Nation first, Nation second) => first != null && second != null &&
        (IsTotalWar(first, second) || first.WarNationNames != null && first.WarNationNames.Exists(name =>
            string.Equals(name, second.name, StringComparison.OrdinalIgnoreCase)) ||
         second.WarNationNames != null && second.WarNationNames.Exists(name =>
            string.Equals(name, first.name, StringComparison.OrdinalIgnoreCase)));

    public static bool IsTotalWar(Nation first, Nation second) => first != null && second != null &&
        (first.TotalWarNationNames != null && first.TotalWarNationNames.Exists(name =>
            string.Equals(name, second.name, StringComparison.OrdinalIgnoreCase)) ||
         second.TotalWarNationNames != null && second.TotalWarNationNames.Exists(name =>
            string.Equals(name, first.name, StringComparison.OrdinalIgnoreCase)));

    public static bool AreAtPeace(Nation first, Nation second) => first != null && second != null &&
        first.PeaceTreatyNationNames != null && first.PeaceTreatyNationNames.Exists(name =>
            string.Equals(name, second.name, StringComparison.OrdinalIgnoreCase));

    public static void SetPeace(Nation first, Nation second)
    {
        if (first == null || second == null || first == second) return;
        // Total War is intentionally permanent. It is the pre-diplomacy fallback mode.
        if (IsTotalWar(first, second)) return;
        System.Collections.Generic.List<Nation> firstBloc = GetDiplomaticBloc(first);
        System.Collections.Generic.List<Nation> secondBloc = GetDiplomaticBloc(second);
        foreach (Nation firstMember in firstBloc)
            foreach (Nation secondMember in secondBloc)
            {
                if (firstMember == null || secondMember == null || firstMember == secondMember) continue;
                if (firstMember.PeaceTreatyNationNames == null)
                    firstMember.PeaceTreatyNationNames = new System.Collections.Generic.List<string>();
                if (secondMember.PeaceTreatyNationNames == null)
                    secondMember.PeaceTreatyNationNames = new System.Collections.Generic.List<string>();
                if (!firstMember.PeaceTreatyNationNames.Exists(name => string.Equals(name, secondMember.name,
                    StringComparison.OrdinalIgnoreCase))) firstMember.PeaceTreatyNationNames.Add(secondMember.name);
                if (!secondMember.PeaceTreatyNationNames.Exists(name => string.Equals(name, firstMember.name,
                    StringComparison.OrdinalIgnoreCase))) secondMember.PeaceTreatyNationNames.Add(firstMember.name);
                firstMember.WarNationNames?.RemoveAll(name => string.Equals(name, secondMember.name,
                    StringComparison.OrdinalIgnoreCase));
                secondMember.WarNationNames?.RemoveAll(name => string.Equals(name, firstMember.name,
                    StringComparison.OrdinalIgnoreCase));
            }
    }

    public static void SetTotalWar(Nation first, Nation second)
    {
        if (first == null || second == null || first == second || AreFriendly(first, second)) return;
        System.Collections.Generic.List<Nation> firstBloc = GetDiplomaticBloc(first);
        System.Collections.Generic.List<Nation> secondBloc = GetDiplomaticBloc(second);
        foreach (Nation firstMember in firstBloc)
            foreach (Nation secondMember in secondBloc)
            {
                if (firstMember == null || secondMember == null || firstMember == secondMember) continue;
                if (firstMember.TotalWarNationNames == null)
                    firstMember.TotalWarNationNames = new System.Collections.Generic.List<string>();
                if (secondMember.TotalWarNationNames == null)
                    secondMember.TotalWarNationNames = new System.Collections.Generic.List<string>();
                AddUniqueName(firstMember.TotalWarNationNames, secondMember.name);
                AddUniqueName(secondMember.TotalWarNationNames, firstMember.name);
                RemoveName(firstMember.PeaceTreatyNationNames, secondMember.name);
                RemoveName(secondMember.PeaceTreatyNationNames, firstMember.name);
                RemoveName(firstMember.WarNationNames, secondMember.name);
                RemoveName(secondMember.WarNationNames, firstMember.name);
            }
    }

    public static void DeclareWar(Nation first, Nation second)
    {
        if (first == null || second == null) return;
        // Tributaries inherit their master's foreign policy and cannot initiate wars.
        // They can still become belligerents when another nation declares war on them.
        if (FindMaster(first) != null) return;
        System.Collections.Generic.List<Nation> firstBloc = GetDiplomaticBloc(first);
        System.Collections.Generic.List<Nation> secondBloc = GetDiplomaticBloc(second);
        int turn = Owners.Instance != null ? Owners.Instance.turncounter : 0;
        foreach (Nation firstMember in firstBloc)
            foreach (Nation secondMember in secondBloc)
            {
                if (firstMember == null || secondMember == null || firstMember == secondMember) continue;
                firstMember.PeaceTreatyNationNames?.RemoveAll(name => string.Equals(name, secondMember.name,
                    StringComparison.OrdinalIgnoreCase));
                secondMember.PeaceTreatyNationNames?.RemoveAll(name => string.Equals(name, firstMember.name,
                    StringComparison.OrdinalIgnoreCase));
                if (firstMember.WarNationNames == null)
                    firstMember.WarNationNames = new System.Collections.Generic.List<string>();
                if (secondMember.WarNationNames == null)
                    secondMember.WarNationNames = new System.Collections.Generic.List<string>();
                if (!firstMember.WarNationNames.Exists(name => string.Equals(name, secondMember.name,
                    StringComparison.OrdinalIgnoreCase))) firstMember.WarNationNames.Add(secondMember.name);
                if (!secondMember.WarNationNames.Exists(name => string.Equals(name, firstMember.name,
                    StringComparison.OrdinalIgnoreCase))) secondMember.WarNationNames.Add(firstMember.name);
                firstMember.LastWarDeclarationTurn = turn;
                secondMember.LastWarDeclarationTurn = turn;
            }
        Nation defendingRoot = FindMaster(second) ?? second;
        Debug.Log(first.name + " declared war on " + defendingRoot.name + ". Their tributaries joined the war.");
    }

    private static System.Collections.Generic.List<Nation> GetDiplomaticBloc(Nation member)
    {
        System.Collections.Generic.List<Nation> result = new System.Collections.Generic.List<Nation>();
        if (member == null) return result;
        Nation root = FindMaster(member) ?? member;
        result.Add(root);
        if (Owners.Instance == null || Owners.Instance.nationlist == null) return result;
        foreach (Nation candidate in Owners.Instance.nationlist)
            if (candidate != null && candidate != root && IsTributarySubjectOf(candidate, root)) result.Add(candidate);
        return result;
    }

    public static void EnsureDefaultTotalWar()
    {
        if (Owners.Instance == null || Owners.Instance.nationlist == null) return;
        for (int i = 0; i < Owners.Instance.nationlist.Count; i++)
            for (int j = i + 1; j < Owners.Instance.nationlist.Count; j++)
            {
                Nation first = Owners.Instance.nationlist[i], second = Owners.Instance.nationlist[j];
                if (first == null || second == null) continue;
                if (AreFriendly(first, second))
                {
                    RemoveName(first.TotalWarNationNames, second.name);
                    RemoveName(second.TotalWarNationNames, first.name);
                    RemoveName(first.WarNationNames, second.name);
                    RemoveName(second.WarNationNames, first.name);
                    if (first != second)
                    {
                        if (first.PeaceTreatyNationNames == null) first.PeaceTreatyNationNames = new System.Collections.Generic.List<string>();
                        if (second.PeaceTreatyNationNames == null) second.PeaceTreatyNationNames = new System.Collections.Generic.List<string>();
                        AddUniqueName(first.PeaceTreatyNationNames, second.name);
                        AddUniqueName(second.PeaceTreatyNationNames, first.name);
                    }
                    continue;
                }
                SetTotalWar(first, second);
            }
    }

    // Retained for old scene/event references; defaults now mean Total War.
    public static void EnsureDefaultPeace() => EnsureDefaultTotalWar();

    private static void AddUniqueName(System.Collections.Generic.List<string> names, string value)
    {
        if (names == null || string.IsNullOrWhiteSpace(value) || names.Exists(name =>
            string.Equals(name, value, StringComparison.OrdinalIgnoreCase))) return;
        names.Add(value);
    }

    private static void RemoveName(System.Collections.Generic.List<string> names, string value)
    {
        names?.RemoveAll(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasMasterAccess(Nation actor, Nation territoryOwner) =>
        actor == territoryOwner || IsTributarySubjectOf(territoryOwner, actor);

    public static Nation FindMaster(Nation subject)
    {
        if (subject == null || string.IsNullOrWhiteSpace(subject.TributaryMasterName) || Owners.Instance == null) return null;
        return Owners.Instance.nationlist.Find(candidate => candidate != null &&
            string.Equals(candidate.name, subject.TributaryMasterName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool CanRecruitTributaryRoster(Nation recruiter, Nation localNation) =>
        recruiter != null && localNation != null && IsTributarySubjectOf(localNation, recruiter);

    public static Nation RestorationRecipient(Nation conqueror, Province province)
    {
        Nation original = province != null ? province.OriginalNation : null;
        if (conqueror == null || original == null || province.nation == original) return null;
        if (original == conqueror && FindMaster(original) != null) return original;
        if (IsTributarySubjectOf(original, conqueror)) return original;
        Nation conquerorMaster = FindMaster(conqueror);
        return conquerorMaster != null && IsTributarySubjectOf(original, conquerorMaster) ? original : null;
    }

    public static Color32 MapColor(Nation nation)
    {
        if (nation == null) return new Color32(96, 96, 96, 255);
        Nation master = FindMaster(nation);
        if (master == null) { Color32 own = nation.ownerIdentity; own.a = 255; return own; }

        // Give every subject of a master a stable position in that master's palette.
        // Name ordering makes the result deterministic in multiplayer and prevents two
        // tributaries from receiving the same hash-based shade.
        int subjectCount = 0;
        int subjectIndex = 0;
        if (Owners.Instance != null && Owners.Instance.nationlist != null)
        {
            foreach (Nation candidate in Owners.Instance.nationlist)
            {
                if (!IsTributarySubjectOf(candidate, master)) continue;
                subjectCount++;
                if (string.Compare(candidate.name, nation.name, StringComparison.OrdinalIgnoreCase) < 0)
                    subjectIndex++;
            }
        }

        Color baseColor = master.ownerIdentity;
        Color.RGBToHSV(baseColor, out float hue, out float saturation, out float value);
        float palettePosition = subjectCount > 1 ? subjectIndex / (float)(subjectCount - 1) : 0.5f;
        hue = Mathf.Repeat(hue + Mathf.Lerp(-0.08f, 0.08f, palettePosition), 1f);
        saturation = Mathf.Clamp01(saturation * (subjectIndex % 2 == 0 ? 0.82f : 1.05f));
        value = Mathf.Clamp01(value * (subjectIndex % 2 == 0 ? 1.12f : 0.82f));

        Color distinctSubjectColor = Color.HSVToRGB(hue, saturation, value);
        Color result = Color.Lerp(baseColor, distinctSubjectColor, 0.58f);
        result.a = 1f;
        return result;
    }
}
