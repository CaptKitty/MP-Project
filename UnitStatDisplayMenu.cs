using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UnitStatDisplayMenu : MonoBehaviour
{
    public static UnitStatDisplayMenu Instance;
    public Text TextUnitName, TextHealthName, TextCost, TextPrimary, TextSecondary, TextPrimaryStats, TextSecondaryStats, TextMArmor, TextPArmor;
    public Image Bone1, Bone2, Bone3;
    public GameObject TerrainHolder;
    public List<GameObject> templist;
    public TestCritter testCritter;
    public CritterHolder critter;

    public void Awake()
    {
        Instance = this;
    }
    public void LoadNewUnit(CritterHolder newcritter)
    {
        critter = newcritter;
        testCritter = newcritter.gameObject.GetComponent<TestCritter>();
        ResetUnit();
        LoadUnit();
    }
    public void ResetUnit()
    {
        TextUnitName.text = "";
        TextHealthName.text = "";
        TextCost.text = "";
        TextPrimary.text = "";
        TextPrimaryStats.text = "";
        TextSecondary.text = "";
        TextSecondaryStats.text = "";
    }
    public void LoadUnit()
    {
        foreach (var item in templist)
        {
            Destroy(item);
        }
        templist.Clear();

        TextUnitName.text = critter.name;
        TextHealthName.text = critter.population + " Health";
        TextCost.text = "Cost: " + critter.cost.amount.ToString();
        if (critter.RangedWeapon != null)
        {
            TextPrimary.text = critter.RangedWeapon.name;
            TextPrimaryStats.text = critter.RangedWeapon.GrabWeaponInformation();
        }
        if (critter.MeleeWeapon != null)
        {
            TextSecondary.text = critter.MeleeWeapon.name;
            TextSecondaryStats.text = critter.MeleeWeapon.GrabWeaponInformation();
        }
        TextMArmor.text = critter.GrabArmor("attack") + " melee armor";
        TextPArmor.text = critter.GrabArmor("ranged") + " ranged armor";


        Bone1.sprite = testCritter.listy[0].GetComponent<SpriteRenderer>().sprite;
        Bone2.enabled = true;
        Bone2.sprite = testCritter.listy[2].GetComponent<SpriteRenderer>().sprite;

        if (Bone2.sprite == null)
        {
            Bone2.enabled = false;
        }
        Bone3.enabled = true;
        Bone3.sprite = testCritter.listy[1].GetComponent<SpriteRenderer>().sprite;
        if (Bone3.sprite == null)
        {
            Bone3.enabled = false;
        }

        Bone1.material = testCritter.material;
        Bone2.material = testCritter.material;
        Bone3.material = testCritter.material;
        
        foreach (var item in critter.flaglist)
        {
            TerrainFlagModifier tfm = Resources.Load<TerrainFlagModifier>("Modifiers/Terrain/TerrainFlags/" + item);
            if (tfm != null)
            {
                var tfmo = Instantiate(Resources.Load<GameObject>("Prefabs/TerrainHolder"), TerrainHolder.transform);
                tfmo.transform.localPosition = new Vector3(-80 + 50 * templist.Count, 25, 0);
                tfmo.GetComponent<Tooltip>().message = tfm.flag + "\n" + tfm.tooltip;
                if (tfm.icon != null)
                {
                    tfmo.GetComponent<Image>().sprite = tfm.icon;
                }
                templist.Add(tfmo);
            }
        }
    }
}
