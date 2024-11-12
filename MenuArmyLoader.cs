using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuArmyLoader : MonoBehaviour
{
    public static MenuArmyLoader Instance;
    public GameObject prefab;
    public List<GameObject> objectList = new List<GameObject>();
    public void Awake()
    {
        Instance = this;
    }
    public void LoadFiles()
    {
        foreach (var item in BattleManager1.Instance.Playerfaction.UnitList)
        {
            var menu = Instantiate(prefab, this.transform);
            menu.GetComponent<SelectMilitaryCritter>().heldcritter = item;
            menu.GetComponent<SelectMilitaryCritter>().UpdateSprite();
            menu.transform.localPosition = new Vector3(0,340-200*objectList.Count,0);
            objectList.Add(menu);
        }
    }
}
