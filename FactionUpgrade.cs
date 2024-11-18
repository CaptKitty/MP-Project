using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FactionUpgrade : MonoBehaviour
{
    public static FactionUpgrade Instance;
    public void Start()
    {
        Instance = this;
        gameObject.SetActive(false);
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
            
        }
        gameObject.SetActive(false);
    }
}
