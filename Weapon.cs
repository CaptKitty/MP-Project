using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum MeleeReachPattern
{
    Auto = 0,
    Short = 1,
    Standard = 2,
    Long = 3
}

public enum RangedWeaponUsage
{
    Standard = 0,
    OpeningThrowable = 1
}

[CreateAssetMenu(menuName = "Weapon/Basic")]
public class Weapon : ScriptableObject
{
    [Header("Generic")]
    public Sprite sprite;

    [Header("Weapon")]
    public double combatdistance = 1f;
    [Tooltip("Tile-battle melee coverage. Auto maps combatdistance >= 2 to Long and other weapons to Standard.")]
    public MeleeReachPattern meleeReachPattern = MeleeReachPattern.Auto;
    public double speed = 1f;
    public int attack = 1;
    public string attacktype = "attack";
    public double attacktime = 1;
    public double NextAvailableAttack = 0;
    public string animationtype;

    [Header("Battle Presentation")]
    [Tooltip("Shared animation and visual-pose definition. When assigned, this takes precedence over the legacy fields below.")]
    public WeaponAnimationClass animationClass;
    [Tooltip("Use a weapon-specific held position and angle. Create a Weapon variant for a different pose.")]
    public bool overrideVisualPose;
    public Vector2 visualOffset = new Vector2(.146f, -.082f);
    public float visualAngle;
    [Tooltip("Exact in-flight sprite. Blank falls back to the SpriteRenderer on Throwable.")]
    public Sprite projectileSprite;

    public string BattleAnimationType => animationClass != null && !string.IsNullOrEmpty(animationClass.animationType)
        ? animationClass.animationType : animationtype;
    public bool OverrideBattleVisualPose => animationClass != null ? animationClass.overrideVisualPose : overrideVisualPose;
    public Vector2 BattleVisualOffset => animationClass != null ? animationClass.visualOffset : visualOffset;
    public float BattleVisualAngle => animationClass != null ? animationClass.visualAngle : visualAngle;
    public Sprite BattleProjectileSprite => animationClass != null && animationClass.projectileSprite != null
        ? animationClass.projectileSprite : projectileSprite;
    public float BattleProjectileAngleOffset => animationClass != null ? animationClass.projectileAngleOffset : 0f;

    [Header("Throwable")]
    public GameObject Throwable;
    public int ammo = 0;
    public Modifier modifier;
    [Tooltip("Opening Throwable keeps the carrier on melee AI, permits one throw during an active charge, then uses the backup melee weapon.")]
    public RangedWeaponUsage rangedUsage = RangedWeaponUsage.Standard;

    [Header("Gear")]
    public Armor armor;

    [Header("Shield Coverage")]
    [Tooltip("Percentage of this shield's armor applied against attacks arriving from the front.")]
    [Range(0, 100)] public int shieldFrontEffectiveness = 100;
    [Tooltip("Percentage of this shield's armor applied against attacks arriving from either side.")]
    [Range(0, 100)] public int shieldSideEffectiveness = 0;



    public Weapon GrabCopy()
    {
        Weapon potato = CreateInstance<Weapon>();
        potato.name = name;
        potato.sprite = sprite;
        potato.combatdistance = combatdistance;
        potato.meleeReachPattern = meleeReachPattern;
        potato.speed = speed;
        potato.attack = attack;
        potato.attacktype = attacktype;
        potato.attacktime = attacktime;
        potato.Throwable = Throwable;
        potato.ammo = ammo;
        potato.modifier = modifier;
        potato.rangedUsage = rangedUsage;
        potato.animationtype = animationtype;
        potato.animationClass = animationClass;
        potato.overrideVisualPose = overrideVisualPose;
        potato.visualOffset = visualOffset;
        potato.visualAngle = visualAngle;
        potato.projectileSprite = projectileSprite;
        potato.armor = armor != null ? armor.GrabArmor() : null;
        potato.shieldFrontEffectiveness = shieldFrontEffectiveness;
        potato.shieldSideEffectiveness = shieldSideEffectiveness;
        return potato;
    }
    public string GrabWeaponInformation()
    {
        string newstring = "";
        newstring += attack + " " + attacktype + "\n";
        newstring += combatdistance + " range\n";
        newstring += attacktime + " atk spd";
        if (Throwable != null)
        {
            newstring += "\n" + ammo + " ammo";
            if (rangedUsage == RangedWeaponUsage.OpeningThrowable) newstring += " (opening throw during charge)";
        }
        if (modifier != null)
        {
            newstring += "\n" + modifier.name;
        }
        return newstring;
    }
}
[System.Serializable]
public class Armor
{
    [Range(0, 100)]
    public int armor = 0; 
    [Range(0, 100)]
    public int rangedarmor = 0;

    public Armor GrabArmor()
    {
        Armor newArmor = new Armor();
        newArmor.armor = armor;
        newArmor.rangedarmor = rangedarmor;
        return newArmor;
    }
}
