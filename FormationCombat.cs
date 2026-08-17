using System;
using System.Collections.Generic;
using UnityEngine;

public enum FormationLayout { Compact, Loose }
public enum FormationIntent { HoldPosition, AdvanceToTarget, MeleeAttack, RangedAttack, TurnToThreat, FallBack, ReformFormation, Rout }

[Serializable]
public class UnitMember
{
    public int index;
    public int currentHealth;
    public int maximumHealth;
    public bool alive = true;
    public Vector2 desiredOffset;
    [NonSerialized] public Transform visual;
    public float nextAttackTime;
}

[Serializable]
public class FormationTuning
{
    [Min(1)] public int memberCount = 6;
    [Min(1)] public int healthPerMember = 50;
    [Min(0.1f)] public float memberSpacing = 0.55f;
    public FormationLayout layout = FormationLayout.Compact;
    [Min(0.1f)] public float reformSpeed = 7f;
    [Min(0.05f)] public float decisionInterval = 0.5f;
    [Min(1f)] public float perceptionRadius = 8f;
    [Range(0f, 100f)] public float routMorale = 15f;
    [Range(0f, 100f)] public float lowMorale = 35f;
    [Range(0f, 100f)] public float lowCohesion = 45f;
    [Range(0f, 100f)] public float highFatigue = 75f;
}

public class FormationCombat : MonoBehaviour
{
    public CritterHolder Unit { get; private set; }
    public List<UnitMember> members = new List<UnitMember>();
    public FormationLayout Layout { get; private set; }
    public FormationIntent Intent { get; private set; } = FormationIntent.HoldPosition;
    public Vector2 Facing { get; private set; } = Vector2.right;
    [Range(0f, 100f)] public float morale = 100f;
    [Range(0f, 100f)] public float fatigue;
    [Range(0f, 100f)] public float cohesion = 100f;
    public bool EnemyOnFlank { get; private set; }
    public bool EnemyBehind { get; private set; }
    public bool LocalOutnumbered { get; private set; }
    public bool CommanderNearby { get; private set; }
    public int LivingCount { get; private set; }
    public int MaximumHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public float OccupiedRadius => Mathf.Max(0.4f,
        tuning != null ? tuning.memberSpacing * Mathf.Sqrt(Mathf.Max(1, LivingCount)) * 0.65f : 0.4f);
    public bool AllowsLegacyCombatAI => Unit == null || Unit.unitbrain == null || Unit.unitbrain.TargetEnemy == null ||
        Intent == FormationIntent.AdvanceToTarget || Intent == FormationIntent.MeleeAttack || Intent == FormationIntent.RangedAttack;
    public bool IsDisciplined => Unit != null && Unit.flaglist != null &&
        (Unit.flaglist.Contains("Formation") || Unit.flaglist.Contains("Phalanx"));

    private FormationTuning tuning;
    private float nextDecisionTime;
    private float disciplinedHoldUntil;
    private int previousLivingCount;

    public void Initialize(CritterHolder unit, FormationTuning settings)
    {
        Unit = unit;
        tuning = settings ?? new FormationTuning();
        Layout = tuning.layout;
        members.Clear();
        int count = Mathf.Max(1, tuning.memberCount);
        for (int i = 0; i < count; i++)
        {
            UnitMember member = new UnitMember
            {
                index = i,
                maximumHealth = Mathf.Max(1, tuning.healthPerMember),
                currentHealth = Mathf.Max(1, tuning.healthPerMember),
                desiredOffset = CalculateSlot(i, count)
            };
            member.visual = CreateMemberVisual(i);
            members.Add(member);
        }
        SetOriginalArtworkVisible(false);
        previousLivingCount = count;
        RefreshAggregateState();
    }

    private Transform CreateMemberVisual(int index)
    {
        GameObject root = new GameObject("Formation Member " + index);
        root.transform.SetParent(transform, false);
        Animator sourceAnimator = Unit.GetComponent<Animator>();
        Animator animator = null;
        if (sourceAnimator != null && sourceAnimator.runtimeAnimatorController != null)
        {
            animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            animator.avatar = sourceAnimator.avatar;
            animator.applyRootMotion = false;
            animator.updateMode = sourceAnimator.updateMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        TestCritter source = Unit.GetComponent<TestCritter>();
        if (source != null && source.listy != null)
        {
            for (int i = 0; i < Mathf.Min(3, source.listy.Count); i++)
            {
                SpriteRenderer original = source.listy[i] == null ? null : source.listy[i].GetComponent<SpriteRenderer>();
                if (original == null) continue;
                GameObject layer = new GameObject(source.listy[i].name);
                layer.transform.SetParent(root.transform, false);
                layer.transform.localPosition = source.listy[i].transform.localPosition;
                layer.transform.localRotation = source.listy[i].transform.localRotation;
                layer.transform.localScale = source.listy[i].transform.localScale;
                SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
                renderer.sprite = original.sprite;
                renderer.sharedMaterial = original.sharedMaterial;
                renderer.color = original.color;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = original.size;
                renderer.sortingLayerID = original.sortingLayerID;
                renderer.sortingOrder = original.sortingOrder;
            }
        }
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
        return root.transform;
    }

    private void SetOriginalArtworkVisible(bool visible)
    {
        TestCritter source = Unit.GetComponent<TestCritter>();
        if (source == null || source.listy == null) return;
        foreach (GameObject layer in source.listy)
        {
            if (layer == null) continue;
            SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = visible;
        }
    }

    private Vector2 CalculateSlot(int index, int count)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / columns);
        int column = index % columns;
        int row = index / columns;
        float spacing = tuning.memberSpacing * (Layout == FormationLayout.Loose ? 1.65f : 1f);
        return new Vector2((column - (columns - 1) * 0.5f) * spacing, (row - (rows - 1) * 0.5f) * spacing);
    }

    private void FixedUpdate()
    {
        if (Unit == null || !Unit.IsThisAlive) return;
        transform.rotation = Quaternion.identity;
        UpdateMembers(Time.fixedDeltaTime);
        UpdateFriendlySpacing(Time.fixedDeltaTime);
        UpdateTacticalState(Time.fixedDeltaTime);
        ExecuteDefensiveIntent(Time.fixedDeltaTime);
        if (Time.fixedTime >= nextDecisionTime)
        {
            nextDecisionTime = Time.fixedTime + tuning.decisionInterval;
            RefreshPerceptionAndIntent();
        }
    }

    private void UpdateFriendlySpacing(float deltaTime)
    {
        if (BattleManager1.Instance == null) return;
        Vector2 separation = Vector2.zero;
        int obstructions = 0;
        float occupiedRadius = Mathf.Max(0.75f, tuning.memberSpacing * Mathf.Sqrt(Mathf.Max(1, LivingCount)) * 0.65f);
        foreach (GameObject candidate in BattleManager1.Instance.enemylist)
        {
            if (candidate == null || candidate == gameObject) continue;
            CritterHolder other = candidate.GetComponent<CritterHolder>();
            if (other == null || !other.IsThisAlive) continue;
            FormationCombat otherFormation = other.formation;
            Vector2 difference = (Vector2)transform.position - (Vector2)candidate.transform.position;
            float distance = difference.magnitude;
            float requiredDistance = occupiedRadius + (otherFormation != null ? otherFormation.OccupiedRadius : occupiedRadius);
            bool friendly = Unit.IsthisAI == other.IsthisAI;
            // Opposing footprints must overlap enough for their front members to make contact.
            if (!friendly) requiredDistance *= IsDisciplined ? 0.72f : 0.5f;
            if (distance >= requiredDistance) continue;
            if (distance <= 0.001f) difference = friendly ? Vector2.right : Vector2.up;
            float pressure = friendly ? 1f : 0.65f;
            separation += difference.normalized * (requiredDistance - Mathf.Max(0.01f, distance)) * pressure;
            obstructions++;
        }
        if (obstructions == 0) return;
        transform.position += (Vector3)(separation / obstructions * Mathf.Min(1f, deltaTime * 2f));
        cohesion = Mathf.Max(0f, cohesion - obstructions * 2f * deltaTime);
    }

    private void ExecuteDefensiveIntent(float deltaTime)
    {
        CritterHolder target = Unit.unitbrain != null && Unit.unitbrain.TargetEnemy != null
            ? Unit.unitbrain.TargetEnemy.GetComponent<CritterHolder>() : null;
        if (target == null) return;
        Vector2 away = transform.position - target.transform.position;
        if (Intent == FormationIntent.FallBack || Intent == FormationIntent.Rout)
        {
            if (away.sqrMagnitude < 0.001f) away = -Facing;
            away.Normalize();
            SetFacing(-away);
            float multiplier = Intent == FormationIntent.Rout ? 1.15f : 0.65f;
            transform.position += (Vector3)(away * (float)Unit.GrabSpeed() * multiplier * deltaTime);
            cohesion = Mathf.Max(0f, cohesion - (Intent == FormationIntent.Rout ? 4f : 1f) * deltaTime);
        }
    }

    private void UpdateMembers(float deltaTime)
    {
        float displacement = 0f;
        int displaced = 0;
        CritterHolder combatTarget = Unit.unitbrain != null && Unit.unitbrain.TargetEnemy != null
            ? Unit.unitbrain.TargetEnemy.GetComponent<CritterHolder>() : null;
        bool memberEngagement = !IsDisciplined && combatTarget != null && combatTarget.IsThisAlive &&
            Vector2.Distance(transform.position, combatTarget.transform.position) <=
            OccupiedRadius * 2f + (float)Unit.GrabCombatDistance() + 1f;
        for (int i = 0; i < members.Count; i++)
        {
            UnitMember member = members[i];
            if (!member.alive || member.visual == null) continue;
            Vector3 target = new Vector3(member.desiredOffset.x, member.desiredOffset.y, 0f);
            if (memberEngagement)
            {
                Vector3 enemyLocal = transform.InverseTransformPoint(
                    GetClosestLivingMemberPosition(combatTarget, member.visual.position));
                Vector3 surge = enemyLocal - target;
                float maximumSurge = tuning.memberSpacing * 1.75f;
                if (surge.sqrMagnitude > 0.001f)
                    target += surge.normalized * Mathf.Min(maximumSurge, surge.magnitude);
            }
            displacement += Vector3.Distance(member.visual.localPosition, target);
            displaced++;
            member.visual.localPosition = Vector3.Lerp(member.visual.localPosition, target,
                1f - Mathf.Exp(-tuning.reformSpeed * deltaTime));
        }
        if (displaced > 0 && displacement / displaced > tuning.memberSpacing)
            cohesion = Mathf.Max(0f, cohesion - 5f * deltaTime);
        else if (Intent == FormationIntent.HoldPosition || Intent == FormationIntent.ReformFormation)
            cohesion = Mathf.Min(100f, cohesion + 7f * deltaTime);
        if (memberEngagement) cohesion = Mathf.Max(0f, cohesion - 1.5f * deltaTime);
    }

    private void UpdateTacticalState(float deltaTime)
    {
        float fatigueRate = Intent == FormationIntent.MeleeAttack || Intent == FormationIntent.RangedAttack ? 2.5f :
            Intent == FormationIntent.AdvanceToTarget || Intent == FormationIntent.FallBack ? 1f : -1.5f;
        fatigue = Mathf.Clamp(fatigue + fatigueRate * deltaTime, 0f, 100f);
        if (LivingCount < previousLivingCount)
        {
            int casualties = previousLivingCount - LivingCount;
            morale = Mathf.Max(0f, morale - casualties * 9f);
            cohesion = Mathf.Max(0f, cohesion - casualties * 7f);
            previousLivingCount = LivingCount;
        }
        if (CommanderNearby) morale = Mathf.Min(100f, morale + 1.25f * deltaTime);
    }

    private void RefreshPerceptionAndIntent()
    {
        CritterHolder target = Unit.unitbrain != null && Unit.unitbrain.TargetEnemy != null
            ? Unit.unitbrain.TargetEnemy.GetComponent<CritterHolder>() : null;
        if (target == null || !target.IsThisAlive)
        {
            Intent = cohesion < tuning.lowCohesion ? FormationIntent.ReformFormation : FormationIntent.HoldPosition;
            return;
        }

        Vector2 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            float angle = Vector2.SignedAngle(Facing, toTarget.normalized);
            EnemyBehind = Mathf.Abs(angle) > 135f;
            EnemyOnFlank = Mathf.Abs(angle) > 55f && !EnemyBehind;
        }
        CountLocalStrength(out int allies, out int enemies, out bool commanderNearby);
        CommanderNearby = commanderNearby;
        LocalOutnumbered = enemies > allies;

        float distance = toTarget.magnitude;
        bool rangedFormation = Unit.RangedWeapon != null && Unit.RangedWeapon.Throwable != null;
        bool charger = Unit.flaglist != null && Unit.flaglist.Contains("Charger");
        float rangedReach = (float)Unit.GrabCombatDistance();
        float meleeReach = (float)(Unit.MeleeWeapon != null ? Unit.MeleeWeapon.combatdistance : 1f);

        if (morale <= tuning.routMorale) Intent = FormationIntent.Rout;
        else if (morale <= tuning.lowMorale) Intent = FormationIntent.FallBack;
        else if (fatigue >= tuning.highFatigue && distance > meleeReach * 1.5f) Intent = FormationIntent.ReformFormation;
        else if (LocalOutnumbered && !charger && morale < 60f) Intent = FormationIntent.FallBack;
        else if (EnemyBehind || EnemyOnFlank) Intent = FormationIntent.TurnToThreat;
        else if (cohesion <= tuning.lowCohesion) Intent = FormationIntent.ReformFormation;
        else if (rangedFormation && distance < Mathf.Max(1.25f, rangedReach * 0.3f) && Unit.MeleeWeapon != null)
            Intent = FormationIntent.FallBack;
        else if (CanEngageTarget(target, rangedFormation))
            Intent = rangedFormation ? FormationIntent.RangedAttack : FormationIntent.MeleeAttack;
        else if (IsDisciplined && !charger && distance <= tuning.perceptionRadius && cohesion >= 70f &&
                 ShouldHoldDisciplinedLine()) Intent = FormationIntent.HoldPosition;
        else Intent = FormationIntent.AdvanceToTarget;

        if (Intent == FormationIntent.TurnToThreat && toTarget.sqrMagnitude > 0.001f) SetFacing(toTarget.normalized);
        PublishWorldState(target, distance);
    }

    private bool ShouldHoldDisciplinedLine()
    {
        if (disciplinedHoldUntil == 0f)
        {
            // A short deterministic commitment creates a line-holding phase without deadlocking two formations forever.
            disciplinedHoldUntil = Time.fixedTime + 1.5f + (Mathf.Abs(gameObject.GetInstanceID()) % 100) / 100f * 1.5f;
        }
        if (disciplinedHoldUntil > 0f && Time.fixedTime < disciplinedHoldUntil) return true;
        disciplinedHoldUntil = -1f;
        return false;
    }

    private void CountLocalStrength(out int allies, out int enemies, out bool commander)
    {
        allies = LivingCount;
        enemies = 0;
        commander = false;
        if (BattleManager1.Instance == null) return;
        foreach (GameObject candidate in BattleManager1.Instance.enemylist)
        {
            if (candidate == null || candidate == gameObject) continue;
            CritterHolder other = candidate.GetComponent<CritterHolder>();
            if (other == null || !other.IsThisAlive || Vector2.Distance(transform.position, candidate.transform.position) > tuning.perceptionRadius) continue;
            FormationCombat formation = other.GetComponent<FormationCombat>();
            int strength = formation != null ? formation.LivingCount : 1;
            if (other.IsthisAI == Unit.IsthisAI)
            {
                allies += strength;
                commander |= other.flaglist != null && other.flaglist.Contains("Warchief");
            }
            else enemies += strength;
        }
    }

    private void PublishWorldState(CritterHolder target, float distance)
    {
        if (Unit.unitbrain == null) return;
        WorldStates beliefs = Unit.unitbrain.beliefs;
        SetFact(beliefs, "HasTarget", target != null);
        SetFact(beliefs, "EnemyInMeleeRange", target != null && distance <= (float)(Unit.MeleeWeapon != null ? Unit.MeleeWeapon.combatdistance : Unit.GrabCombatDistance()));
        SetFact(beliefs, "EnemyInRangedRange", target != null && distance <= Unit.GrabCombatDistance());
        SetFact(beliefs, "EnemyOnFlank", EnemyOnFlank);
        SetFact(beliefs, "EnemyBehind", EnemyBehind);
        SetFact(beliefs, "MoraleLow", morale <= tuning.lowMorale);
        SetFact(beliefs, "FatigueHigh", fatigue >= tuning.highFatigue);
        SetFact(beliefs, "CohesionLow", cohesion <= tuning.lowCohesion);
        SetFact(beliefs, "FormationDisrupted", cohesion <= tuning.lowCohesion);
        SetFact(beliefs, "LocalOutnumbered", LocalOutnumbered);
        SetFact(beliefs, "LocalAdvantage", !LocalOutnumbered);
        SetFact(beliefs, "CommanderNearby", CommanderNearby);
    }

    private static void SetFact(WorldStates states, string key, bool value)
    {
        Dictionary<string, int> dictionary = states.GetStates();
        bool exists = dictionary.ContainsKey(key);
        if (value && !exists) states.SetState(key, 1);
        else if (!value && exists) states.RemoveState(key);
    }

    public void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            float turn = Vector2.Angle(Facing, direction.normalized);
            if (turn > 45f && (Intent == FormationIntent.MeleeAttack || EnemyOnFlank || EnemyBehind))
                cohesion = Mathf.Max(0f, cohesion - turn / 45f * 3f);
            Facing = direction.normalized;
            bool flip = Facing.x < 0f;
            foreach (UnitMember member in members)
            {
                if (member.visual == null) continue;
                foreach (SpriteRenderer renderer in member.visual.GetComponentsInChildren<SpriteRenderer>())
                    renderer.flipX = flip;
            }
        }
    }

    public int ApplyDamage(int damage, Vector3 attackerPosition)
    {
        int remaining = Mathf.Max(0, damage);
        int casualties = 0;
        while (remaining > 0)
        {
            UnitMember member = FindFrontLivingMember(attackerPosition);
            if (member == null) break;
            int applied = Mathf.Min(member.currentHealth, remaining);
            member.currentHealth -= applied;
            remaining -= applied;
            if (member.currentHealth <= 0)
            {
                member.alive = false;
                casualties++;
                if (member.visual != null) member.visual.gameObject.SetActive(false);
            }
            else if (member.visual != null)
            {
                Animator animator = member.visual.GetComponent<Animator>();
                if (animator != null) animator.SetTrigger("Hurt");
            }
        }
        RefreshAggregateState();
        return casualties;
    }

    private UnitMember FindFrontLivingMember(Vector3 attackerPosition)
    {
        UnitMember result = null;
        float best = float.MaxValue;
        foreach (UnitMember member in members)
        {
            if (!member.alive) continue;
            Vector3 position = member.visual != null ? member.visual.position : transform.position;
            float distance = (position - attackerPosition).sqrMagnitude;
            if (distance < best) { best = distance; result = member; }
        }
        return result;
    }

    public int CountEligibleAttackers(CritterHolder target, bool ranged)
    {
        if (target == null) return 0;
        float reach = (float)(ranged || Unit.MeleeWeapon == null ? Unit.GrabCombatDistance() : Unit.MeleeWeapon.combatdistance);
        int count = 0;
        foreach (UnitMember member in members)
        {
            if (!member.alive || member.visual == null) continue;
            Vector3 targetPosition = GetClosestLivingMemberPosition(target, member.visual.position);
            if (Vector2.Distance(member.visual.position, targetPosition) <= reach + tuning.memberSpacing * 0.35f) count++;
        }
        return count;
    }

    public bool CanEngageTarget(CritterHolder target, bool ranged)
    {
        return CountEligibleAttackers(target, ranged) > 0;
    }

    public int ResolveMemberAttacks(CritterHolder target, int damagePerMember, string attackType, bool ranged)
    {
        if (target == null || !target.IsThisAlive) return 0;
        float reach = (float)(ranged || Unit.MeleeWeapon == null ? Unit.GrabCombatDistance() : Unit.MeleeWeapon.combatdistance);
        int attacks = 0;
        foreach (UnitMember member in members)
        {
            if (!member.alive || member.visual == null) continue;
            Vector3 enemyPosition = GetClosestLivingMemberPosition(target, member.visual.position);
            if (Vector2.Distance(member.visual.position, enemyPosition) > reach + tuning.memberSpacing * 0.35f) continue;
            target.LoseHealthFrom(damagePerMember, attackType, member.visual.position);
            member.nextAttackTime = Time.fixedTime + (float)Unit.GrabAttackTime();
            attacks++;
            if (!target.IsThisAlive) break;
        }
        return attacks;
    }

    public int SpawnMemberProjectiles(CritterHolder target)
    {
        if (target == null || Unit.RangedWeapon == null || Unit.RangedWeapon.Throwable == null || BattleManager1.Instance == null) return 0;
        float reach = (float)Unit.GrabCombatDistance();
        int spawned = 0;
        foreach (UnitMember member in members)
        {
            if (!member.alive || member.visual == null) continue;
            Vector3 targetPosition = GetClosestLivingMemberPosition(target, member.visual.position);
            if (Vector2.Distance(member.visual.position, targetPosition) > reach + tuning.memberSpacing * 0.35f) continue;
            GameObject projectile = Instantiate(Unit.RangedWeapon.Throwable, BattleManager1.Instance.transform);
            projectile.transform.position = member.visual.position;
            projectile.transform.LookAt(new Vector3(targetPosition.x, targetPosition.y, -90f), Vector3.forward);
            Projectile projectileLogic = projectile.GetComponent<Projectile>();
            if (projectileLogic != null) projectileLogic.TargetEnemy = target.gameObject;
            spawned++;
        }
        if (spawned > 0) Unit.RangedWeapon.ammo = Mathf.Max(0, Unit.RangedWeapon.ammo - 1);
        return spawned;
    }

    public float GetDesiredEngagementDistance(CritterHolder target, bool ranged)
    {
        float weaponReach = (float)(ranged || Unit.MeleeWeapon == null
            ? Unit.GrabCombatDistance()
            : Unit.MeleeWeapon.combatdistance);
        FormationCombat targetFormation = target != null ? target.formation : null;
        float targetRadius = targetFormation != null ? targetFormation.OccupiedRadius : 0.35f;
        // Stop when the two footprints are separated but their front ranks can use the weapon.
        return Mathf.Max(0.25f, OccupiedRadius + targetRadius + weaponReach * 0.8f);
    }

    public static Vector3 GetClosestLivingMemberPosition(CritterHolder target, Vector3 fromPosition)
    {
        if (target == null || target.formation == null) return target != null ? target.transform.position : fromPosition;
        Vector3 closest = target.transform.position;
        float bestDistance = float.MaxValue;
        foreach (UnitMember member in target.formation.members)
        {
            if (!member.alive || member.visual == null) continue;
            float distance = (member.visual.position - fromPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = member.visual.position;
            }
        }
        return closest;
    }

    public void PlayMemberAnimation(string trigger, CritterHolder target, bool ranged)
    {
        if (string.IsNullOrEmpty(trigger) || target == null) return;
        float reach = (float)(ranged || Unit.MeleeWeapon == null ? Unit.GrabCombatDistance() : Unit.MeleeWeapon.combatdistance);
        foreach (UnitMember member in members)
        {
            if (!member.alive || member.visual == null ||
                Vector2.Distance(member.visual.position, GetClosestLivingMemberPosition(target, member.visual.position)) > reach + tuning.memberSpacing * 0.35f) continue;
            Animator animator = member.visual.GetComponent<Animator>();
            if (animator != null) animator.SetTrigger(trigger);
        }
    }

    private void RefreshAggregateState()
    {
        LivingCount = 0;
        MaximumHealth = 0;
        CurrentHealth = 0;
        foreach (UnitMember member in members)
        {
            MaximumHealth += member.maximumHealth;
            CurrentHealth += Mathf.Max(0, member.currentHealth);
            if (member.alive) LivingCount++;
        }
        if (Unit != null) Unit.population = CurrentHealth;
    }
}
