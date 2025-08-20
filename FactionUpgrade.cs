using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FactionUpgrade : MonoBehaviour
{
    public static FactionUpgrade Instance;
    private Modifier upgrade;
    private UpgradeModule mod;
    private GameObject gameobject;
    private UnitSaveData unitSaveData;
    public void Start()
    {
        Instance = this;
        gameObject.SetActive(false);
    }
    public void OnEnable()
    {
        Instance = this;
        // gameobject = SessionManager.Instance.HostFaction.UnitList[Random.Range(0, SessionManager.Instance.HostFaction.UnitList.Count)];

        // mod = gameobject.GetComponent<TestCritter>().GrabModule();

        unitSaveData = SessionManager.Instance.HostFaction.UnitDataList[Random.Range(0, SessionManager.Instance.HostFaction.UnitDataList.Count)];
        mod = unitSaveData.GrabModule();

        transform.GetChild(3).GetChild(0).GetComponent<Text>().text = unitSaveData.name + " " + mod.name;//"Upgrade " +  + " gets " + mod.name;
        transform.GetChild(3).GetChild(0).GetComponent<Text>().text = transform.GetChild(3).GetChild(0).GetComponent<Text>().text.Replace("(Clone)", "");
    }
    public void PressButton(string input)
    {
        if (input.Contains("Upgrade"))
        {
            Upgrade(input);
        }
    }
    public void Upgrade(string input)
    {
        if (input.Contains("Barracks"))
        {
            SessionManager.Instance.HostFaction.UpgradeBarracks();
        }
        if (input.Contains("Merc"))
        {
            SessionManager.Instance.HostFaction.UpgradeMercenaries();
        }
        if (input.Contains("Farm"))
        {
            SessionManager.Instance.HostFaction.FarmLevel++;
        }
        if (input.Contains("Unit"))
        {
            AddUnitModifier();
        }
        gameObject.SetActive(false);
    }
    public void AddUnitModifier()
    {
        if (mod.modifier != null)
        {
            unitSaveData.modifierlist.Add(mod.modifier);
        }
        if (mod.NewRangedWeapon != null)
        {
            unitSaveData.RangedWeapon = mod.NewRangedWeapon;
        }
        if (mod.NewMeleeWeapon != null)
        {
            unitSaveData.MeleeWeapon = mod.NewMeleeWeapon;
        }
        if (mod.NewArmors != null)
        {
            unitSaveData.Armor = mod.NewArmors;
        }
        if (mod.NewShields != null)
        {
            unitSaveData.Shield = mod.NewShields;
        }
        // if (mod.NewShield != null)
        // {
        //     unitSaveData.bodyparts[1] = mod.NewShield;
        // }
        // if (mod.NewArmor != null)
        // {
        //     unitSaveData.bodyparts[0] = mod.NewArmor;
        // }
        unitSaveData.upgradeModules.Remove(mod);
    }
}
