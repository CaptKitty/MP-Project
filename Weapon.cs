using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "Weapon/Basic")]
public class Weapon : ScriptableObject
{
    [Header("Generic")]
    public Sprite sprite;

    [Header("Weapon")]
    public double combatdistance = 1f;
    public double speed = 1f;
    public int attack = 1;
    public string attacktype = "attack";
    public double attacktime = 1;
    public double NextAvailableAttack = 0;
    public string animationtype;

    [Header("Throwable")]
    public GameObject Throwable;
    public int ammo = 0;
    public Modifier modifier;

    [Header("Gear")]
    public Armor armor;



    public Weapon GrabCopy()
    {
        Weapon potato = new Weapon();
        potato.name = name;
        potato.sprite = sprite;
        potato.combatdistance = combatdistance;
        potato.speed = speed;
        potato.attack = attack;
        potato.attacktype = attacktype;
        potato.attacktime = attacktime;
        potato.Throwable = Throwable;
        potato.ammo = ammo;
        potato.modifier = modifier;
        potato.animationtype = animationtype;
        potato.armor = armor.GrabArmor();
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