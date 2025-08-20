using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "UnitSaveData/Basic")]
public class UnitSaveData : ScriptableObject
{
    [Header("CritterHolder")]
    public Vector3Int spot;
    public string unitname;
    public UnitTypes unittype;
    public int cost = 100;
    public int health = 100;
    public int speed = 1;
    public List<string> flaglist;
    public Weapon RangedWeapon;
    public Weapon MeleeWeapon;
    public Weapon Armor;
    public Weapon Shield;
    public List<Modifier> modifierlist = new List<Modifier>();

    [Header("TestCritter")]
    public Color color;
    public Color color2;
    public Color color3;
    public Faction faction;
    public List<UpgradeModule> upgradeModules = new List<UpgradeModule>();
    public List<Sprite> bodyparts = new List<Sprite>();
    public bool Mercenary = false;

    public void NewCritterHolder(CritterHolder oldCritter)
    {
        oldCritter.spot = spot;
        oldCritter.name = unitname;
        oldCritter.unittype = unittype;
        oldCritter.cost.amount = cost;
        oldCritter.population = health;
        oldCritter.speed = speed;
        oldCritter.flaglist = flaglist;
        //RangedWeapon = RangedWeapon.GrabCopy();
        oldCritter.RangedWeapon = RangedWeapon;
        //MeleeWeapon = MeleeWeapon.GrabCopy();
        oldCritter.MeleeWeapon = MeleeWeapon;
        oldCritter.modifierlist = new List<Modifier>();
        foreach (var item in modifierlist)
        {
            oldCritter.modifierlist.Add(item);
        }
        oldCritter.Armor = Armor;//.GrabWeapon();
        oldCritter.Shield = Shield;//.GrabWeapon();
        
    }
    public void NewTestCritter(TestCritter oldCritter)
    {
        oldCritter.color = color;
        oldCritter.color2 = color2;
        oldCritter.color3 = color3;
        //oldCritter.faction = faction;
        oldCritter.upgradeModules = upgradeModules;
        oldCritter.Mercenary = Mercenary;

        for (int i = 0; i < 3; i++)
        {
            oldCritter.listy[i].GetComponent<SpriteRenderer>().sprite = bodyparts[i];
            //Debug.LogError(oldCritter.listy[i].GetComponent<SpriteRenderer>().sprite);
        }
    }
    public UpgradeModule GrabModule()
    {
        if (upgradeModules.Count > 0)
        {
            return upgradeModules[Random.Range(0, upgradeModules.Count)];
        }
        return null;
    }
}