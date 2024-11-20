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
        if(input.Contains("Upgrade"))
        {
            Upgrade(input);
        }
    }
    public void Upgrade(string input)
    {
        if(input.Contains("Barracks"))
        {
            SessionManager.Instance.HostFaction.UpgradeBarracks();
        }
        if(input.Contains("Merc"))
        {
            SessionManager.Instance.HostFaction.UpgradeMercenaries();
        }
        if(input.Contains("Farm"))
        {
            SessionManager.Instance.HostFaction.FarmLevel++;
        }
        if(input.Contains("Unit"))
        {
            AddUnitModifier();
        }
        gameObject.SetActive(false);
    }
    public void AddUnitModifier()
    {
        gameobject.GetComponent<CritterHolder>().modifierlist.Add(upgrade);
    }
}
