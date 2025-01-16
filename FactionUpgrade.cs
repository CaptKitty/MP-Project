using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FactionUpgrade : MonoBehaviour
{
    public static FactionUpgrade Instance;
    private Modifier upgrade;
    private GameObject gameobject;
    public void Start()
    {
        Instance = this;
        gameObject.SetActive(false);
    }
    public void OnEnable()
    {
        gameobject = SessionManager.Instance.HostFaction.UnitList[Random.Range(0,SessionManager.Instance.HostFaction.UnitList.Count)];
        upgrade = Instantiate(gameobject.GetComponent<TestCritter>().Upgrade);
        transform.GetChild(3).GetChild(0).GetComponent<Text>().text = gameobject.name + " gets " + upgrade.name;
        transform.GetChild(3).GetChild(0).GetComponent<Text>().text = transform.GetChild(3).GetChild(0).GetComponent<Text>().text.Replace ("(Clone)", "");
    }
    public void PressButton(string input)
    {
        if(input.Contains("Allied"))
        {
            var a = Owners.Instance.CallPlayer();
            a.SetDiplomaticStatus(Mapshower.Instance.SelectedProvince.nation.name, "ally");
        }
        if(input.Contains("Neutral"))
        {
            var a = Owners.Instance.CallPlayer();
            a.SetDiplomaticStatus(Mapshower.Instance.SelectedProvince.nation.name, "peace");
        }
        if(input.Contains("Enemy"))
        {
            //We Declaring war boys
            var a = Owners.Instance.CallPlayer();
            a.SetDiplomaticStatus(Mapshower.Instance.SelectedProvince.nation.name, "war");
            
            SpawnDiplomaticEffect b = new SpawnDiplomaticEffect();
            b.nation = Mapshower.Instance.SelectedProvince.nation.name;
            b.othercountry = Owners.Instance.CallPlayer().name;
            b.newstatus = "war";

            if(TestRelay.Instance.PlayerObjects.Find(x => x.GetComponent<RpcTest>().PlayerNation == b.nation) == null)
            {
                BaseEvents potato = Instantiate(Resources.Load<BaseEvents>("Events/Declared War"));
                potato.OptionList[0].EffectList[0] = b;
                General_Manager.Instance.TriggerEvent(potato, Mapshower.Instance.SelectedProvince.nation.name);////);
            }
            else
            {
                TestRelay.Instance.PlayerObjects.Find(x => x.GetComponent<RpcTest>().PlayerNation == b.nation).GetComponent<RpcTest>().SendEffectToLoadListRpc("SpawnDiplomaticEffect", b.nation, b.othercountry, "war");
                TestRelay.Instance.PlayerObjects.Find(x => x.GetComponent<RpcTest>().PlayerNation == b.nation).GetComponent<RpcTest>().SendDynamicEventToExecuteRpc(Title: "Potato", Description: "FuckYouThatsWhy", Option: "No u", targetnation: b.nation, bonusdata: "Rome Declares war on Gaul");
            }
        }
        if(input.Contains("Upgrade"))
        {
            Upgrade(input);
        }
    }
    public void Upgrade(string input)
    {
        if(input.Contains("Barracks"))
        {
            //SessionManager.Instance.HostFaction.UpgradeBarracks();
            if(Mapshower.Instance.SelectedProvince.troops > 10)
            {
                Mapshower.Instance.SelectedProvince.AddTroops(-10);
                ProvinceModifier moddie = new ProvinceModifier();
                moddie.BaseTroops = 5;
                Mapshower.Instance.SelectedProvince.AddModifier(moddie);
            }
        }
        if(input.Contains("Merc"))
        {
            if(Mapshower.Instance.SelectedProvince.troops > 10)
            {
                Mapshower.Instance.SelectedProvince.AddTroops(-10);
                ProvinceModifier moddie = new ProvinceModifier();
                moddie.DefensiveDice = 1;
                Mapshower.Instance.SelectedProvince.AddModifier(moddie);
            }
            //SessionManager.Instance.HostFaction.UpgradeMercenaries();
            //Mapshower.Instance.SelectedProvince.AddModifier();
        }
        if(input.Contains("Farm"))
        {
            if(Mapshower.Instance.SelectedProvince.troops > 10)
            {
                Mapshower.Instance.SelectedProvince.AddTroops(-10);
                ProvinceModifier moddie = new ProvinceModifier();
                moddie.BonusSpawns = 1;
                Mapshower.Instance.SelectedProvince.AddModifier(moddie);
            }
            //SessionManager.Instance.HostFaction.FarmLevel++;
            //Mapshower.Instance.SelectedProvince.AddModifier();
        }
        if(input.Contains("Unit"))
        {
            //AddUnitModifier();
            //Mapshower.Instance.SelectedProvince.AddModifier();
        }
        gameObject.SetActive(false);
    }
    public void AddUnitModifier()
    {
        gameobject.GetComponent<CritterHolder>().modifierlist.Add(upgrade);
    }
}
