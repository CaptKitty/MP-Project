using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "UnitSaveData/Basic")]
public class UnitSaveData : ScriptableObject
{
    [Header("CritterHolder")]
    public string unitname;
    public UnitTypes unittype;
    public int cost = 100;
    public int health = 100;
    [Header("Campaign Recruitment")]
    [Min(1)] public int recruitmentTicks = 3;
    public int EffectiveRecruitmentTicks => recruitmentTicks > 0 ? recruitmentTicks : 3;
    [Tooltip("Gold charged every campaign economy tick for each professional formation. Levies ignore this value.")]
    [Min(0)] public int upkeep = 2;

    [Header("Tile Battle Timing")]
    [Tooltip("Maximum number of pre-committed actions this formation may attempt in one command round.")]
    [Min(1)] public int actions = 2;
    [FormerlySerializedAs("Initiative")]
    [Tooltip("Base reaction time in resolution ticks. Lower values execute actions faster.")]
    [Min(1)] public int reactionTime = 7;
    public int ReactionTime => Mathf.Max(1, reactionTime);
    // Source compatibility for older scripts and editor tests. Unity serializes reactionTime.
    public int Initiative { get => reactionTime; set => reactionTime = value; }

    public List<string> flaglist;
    public Weapon RangedWeapon;
    public Weapon MeleeWeapon;
    public Weapon Armor;
    public Weapon Shield;
    public List<Modifier> modifierlist = new List<Modifier>();

    [Header("Mercenary")]
    public bool Mercenary = false;
    [FormerlySerializedAs("color3")]
    [InspectorName("Native skintone")]
    public Color nativeSkintone;

    [Header("TestCritter")]
    public List<UpgradeModule> upgradeModules = new List<UpgradeModule>();
    public List<Sprite> bodyparts = new List<Sprite>();
    public bool Big = false;

    public void NewCritterHolder(CritterHolder oldCritter)
    {
        oldCritter.name = unitname;
        oldCritter.unittype = unittype;
        oldCritter.cost.amount = cost;
        oldCritter.population = health;
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
        oldCritter.color3 = nativeSkintone;
        oldCritter.upgradeModules = upgradeModules;
        oldCritter.Mercenary = Mercenary;

        for (int i = 0; i < 3; i++)
        {
            oldCritter.listy[i].GetComponent<SpriteRenderer>().sprite = bodyparts[i];
            //Debug.LogError(oldCritter.listy[i].GetComponent<SpriteRenderer>().sprite);
        }
        if (Big)
        {
            oldCritter.listy[0].GetComponent<SpriteRenderer>().size = new Vector2(2, 2);
        }
    }
    public UpgradeModule GrabModule()
    {
        foreach (var item in upgradeModules)
        {
            if (!item.generic)
            {
                return item;
            }
        }
        if (upgradeModules.Count > 0)
        {
            return upgradeModules[Random.Range(0, upgradeModules.Count)];
        }
        return null;
    }
}
