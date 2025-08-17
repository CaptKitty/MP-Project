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
        //Debug.LogError(BattleManager1.Instance.Playerfaction.UnitDataList.Count);
        foreach (UnitSaveData item in BattleManager1.Instance.Playerfaction.UnitDataList)
        {
            var menu = Instantiate(prefab, this.transform);
            menu.GetComponent<SelectMilitaryCritter>().unitSaveData = item;
            item.NewCritterHolder(menu.GetComponent<SelectMilitaryCritter>().heldcritter.GetComponent<CritterHolder>());
            item.NewCritterHolder(menu.transform.GetChild(0).GetComponent<CritterHolder>());
            // menu.transform.GetChild(0).GetComponent<CritterHolder>().RangedWeapon = item.GetComponent<CritterHolder>().RangedWeapon;
            // menu.transform.GetChild(0).GetComponent<CritterHolder>().MeleeWeapon = item.GetComponent<CritterHolder>().MeleeWeapon;
            // menu.transform.GetChild(0).GetComponent<CritterHolder>().cost = item.GetComponent<CritterHolder>().cost;
            // menu.transform.GetChild(0).GetComponent<CritterHolder>().name = item.name;
            menu.GetComponent<SelectMilitaryCritter>().UpdateSprite();
            menu.transform.localPosition = new Vector3(0, 340 - 200 * objectList.Count, 0);
            objectList.Add(menu);
        }
    }
    public void UpdateSprites()
    {
        foreach (var item in objectList)
        {
            item.GetComponent<SelectMilitaryCritter>().UpdateSprite();
        }
    }
}
