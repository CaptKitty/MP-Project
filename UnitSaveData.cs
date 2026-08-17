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
    [Header("Campaign Recruitment")]
    [Min(1)] public int recruitmentTicks = 3;
    public int EffectiveRecruitmentTicks => recruitmentTicks > 0 ? recruitmentTicks : 3;

    [Header("Tile Battle Timing")]
    [Tooltip("Maximum number of pre-committed actions this formation may attempt in one command round.")]
    [Min(1)] public int actions = 2;
    [Tooltip("Base action interval in resolution ticks. Lower values execute actions faster.")]
    [Min(1)] public int Initiative = 7;

    [Header("Tile Battle Shield Direction")]
    [Tooltip("Percentage of the shield's armor applied against attacks arriving from the front.")]
    [Range(0, 100)] public int shieldFrontEffectiveness = 100;
    [Tooltip("Percentage of the shield's armor applied against attacks arriving from either side. Currently defaults to zero.")]
    [Range(0, 100)] public int shieldSideEffectiveness = 0;

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
    public bool Big = false;

    [Header("Formation")]
    public bool useFormation = true;
    [Min(1)] public int formationSize = 6;
    [Min(1)] public int memberHealth = 50;
    [Min(0.1f)] public float memberSpacing = 0.55f;
    public FormationLayout formationLayout = FormationLayout.Compact;

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
        oldCritter.useFormation = useFormation;
        oldCritter.formationTuning.memberCount = formationSize;
        oldCritter.formationTuning.healthPerMember = memberHealth;
        oldCritter.formationTuning.memberSpacing = memberSpacing;
        oldCritter.formationTuning.layout = formationLayout;
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
