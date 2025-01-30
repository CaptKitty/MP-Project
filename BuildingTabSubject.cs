using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BuildingTabSubject : MonoBehaviour
{
    public Buildings thisBuilding;
    public TextMeshProUGUI texty;
    public void SetBuilding(Buildings building)
    {
        thisBuilding = building;
        gameObject.name = building.name;
        texty.text = building.name;
    }
    public void DestroyBuilding()
    {
        BuildingTab.Instance.provinces.BuildingList.Remove(thisBuilding);
        BuildingTab.Instance.LoadBuildingsInCity(BuildingTab.Instance.provinces);
    }
}
