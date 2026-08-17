using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FactionUpgrade : MonoBehaviour
{
    public static FactionUpgrade Instance;
    private Modifier upgrade;
    private UpgradeModule mod;
    private UpgradeModule mod2;
    private GameObject gameobject;
    private UnitSaveData unitSaveData;
    private UnitSaveData unitSaveData2;
    public void Start()
    {
        Instance = this;
        gameObject.SetActive(false);
    }
    public void OnEnable()
    {
        Instance = this;
        if (FieldArmyHolder.PlayerFieldArmy == null)
        {
            return;
        }

        // gameobject = SessionManager.Instance.HostFaction.UnitList[Random.Range(0, SessionManager.Instance.HostFaction.UnitList.Count)];

        // mod = gameobject.GetComponent<TestCritter>().GrabModule();

        unitSaveData = SessionManager.Instance.HostFaction.UnitDataList[Random.Range(0, SessionManager.Instance.HostFaction.UnitDataList.Count)];
        unitSaveData = FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves[Random.Range(0, FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves.Count)].USD;
        mod = unitSaveData.GrabModule();

        unitSaveData2 = SessionManager.Instance.HostFaction.UnitDataList[Random.Range(0, SessionManager.Instance.HostFaction.UnitDataList.Count)];
        unitSaveData2 = FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves[Random.Range(0, FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves.Count)].USD;
        for (int i = 0; i < 5; i++)
        {
            if (unitSaveData2 == unitSaveData)
            {
                unitSaveData2 = FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves[Random.Range(0, FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves.Count)].USD;
            }
        }
        
        mod2 = unitSaveData2.GrabModule();

        transform.GetChild(3).GetChild(0).GetComponent<Text>().text = "Upgrade " + unitSaveData.name;// + " " + mod.name;//"Upgrade " +  + " gets " + mod.name;
        transform.GetChild(3).GetChild(0).GetComponent<Text>().text = transform.GetChild(3).GetChild(0).GetComponent<Text>().text.Replace("(Clone)", "");
        transform.GetChild(3).GetComponent<Tooltip>().message = unitSaveData.name + ":\n" + mod.GrabTooltip();

        transform.GetChild(4).GetChild(0).GetComponent<Text>().text = "Upgrade " + unitSaveData2.name;// + " " + mod.name;//"Upgrade " +  + " gets " + mod.name;
        transform.GetChild(4).GetChild(0).GetComponent<Text>().text = transform.GetChild(4).GetChild(0).GetComponent<Text>().text.Replace("(Clone)", "");
        transform.GetChild(4).GetComponent<Tooltip>().message = unitSaveData2.name + ":\n" + mod2.GrabTooltip();
    }
    public void PressButton(string input)
    {
        if (input.Contains("Upgrade"))
        {
            Upgrade(input);
        }
        ToolTipManager._instance.HideToolTip();
    }
    public void PressButton2(string input)
    {
        if (input.Contains("Upgrade"))
        {
            Upgrade2(input);
        }
        ToolTipManager._instance.HideToolTip();
    }
    public void Upgrade(string input)
    {
        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsSpawned)
        {
            CampaignNetworkPlayer.Local.RequestFactionUpgrade(input);
            gameObject.SetActive(false);
            return;
        }
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
    public void Upgrade2(string input)
    {
        if (input.Contains("Unit"))
        {
            AddUnitModifier2();
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
        unitSaveData.upgradeModules.Remove(mod);
    }
    public void AddUnitModifier2()
    {
        if (mod2.modifier != null)
        {
            unitSaveData2.modifierlist.Add(mod2.modifier);
        }
        if (mod2.NewRangedWeapon != null)
        {
            unitSaveData2.RangedWeapon = mod2.NewRangedWeapon;
        }
        if (mod2.NewMeleeWeapon != null)
        {
            unitSaveData2.MeleeWeapon = mod2.NewMeleeWeapon;
        }
        if (mod2.NewArmors != null)
        {
            unitSaveData2.Armor = mod2.NewArmors;
        }
        if (mod2.NewShields != null)
        {
            unitSaveData2.Shield = mod2.NewShields;
        }
        unitSaveData2.upgradeModules.Remove(mod2);
    }
}
