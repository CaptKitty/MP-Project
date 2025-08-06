using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "Weapon/Basic")]
public class Weapon : ScriptableObject
{
    public Sprite sprite;
    public double combatdistance = 1f;
    public double speed = 1f;
    public int attack = 1;
    public double attacktime = 1;
    public double NextAvailableAttack = 0;

    public GameObject Throwable;
    public int ammo;
    public Modifier modifier;

    public Weapon GrabCopy()
    {
        Weapon potato = new Weapon();
        potato.sprite = sprite;
        potato.combatdistance = combatdistance;
        potato.speed = speed;
        potato.attack = attack;
        potato.attacktime = attacktime;
        potato.Throwable = Throwable;
        potato.ammo = ammo;
        potato.modifier = modifier;
        return potato;
    }
}