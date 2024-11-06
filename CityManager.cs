using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CityManager : MonoBehaviour
{
    public static CityManager Instance;
    public List<Resource> ResourceList = new List<Resource>();
    public Text datafile;

    public void Awake()
    {
        Instance = this;
    }
    public void Update()
    {
        Vector3Int target = GeneralManager.Instance.ownermap.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        target.z = 0;
        if (!GeneralManager.Instance.ownermap.HasTile(target)) {
            return;
        }
        if (!GeneralManager.Instance.highlightmap.HasTile(target)) {
            return;
        }
        if (Input.GetMouseButtonDown(1))
        {
            CritterHolder testy = GeneralManager.Instance.SelectedCritter.GetComponent<CritterHolder>();

            bool canbreathe = false;
            foreach (var item in testy.ViablePlacingSpots)
            {
                if(item == GeneralManager.Instance.tiledict[target].name)
                {
                    canbreathe = true;
                }
            }

            if(canbreathe)
            {
                if(testy.cost.name == "" || testy.cost == null)
                {
                    GeneralManager.Instance.Spawn(target, GeneralManager.Instance.SelectedCritter);
                }
                else
                {
                    bool canspawn = false;
                    foreach (var item in CityManager.Instance.ResourceList)
                    {
                        if(item.name == testy.cost.name)
                        {
                            if(item.amount >= -testy.cost.amount)
                            {
                                CityManager.Instance.AddResource(resource:testy.cost);
                                canspawn = true;
                            }
                        }
                    }
                    if(canspawn)
                    {
                        GeneralManager.Instance.Spawn(target, GeneralManager.Instance.SelectedCritter);
                    }
                }
            }
            UIManager.Instance.UpdateUI();
        }
        foreach (Vector3Int position in GeneralManager.Instance.highlightmap.cellBounds.allPositionsWithin)
        {
            GeneralManager.Instance.highlightmap.SetTile(position, GeneralManager.Instance.tilea);
        }
        foreach(Ability item in GeneralManager.Instance.SelectedCritter.GetComponent<CritterHolder>().AbilityList)
        {
            if(item.GetType() == typeof(ForageAbility) || item.GetType() == typeof(HarvesterAbility))
            {
                //ForageAbility items = (ForageAbility)item;
                Vector2Int vector = item.arrayBool.GridSize;
                for (int x = -(vector.x-1)/2; x <= (vector.y)/2; x++)
                {
                    for (int y = -(vector.y-1)/2; y <= (vector.y)/2; y++)
                    {
                        if(item.arrayBool.GetCell((x+(vector.x)/2),(y+(vector.y)/2)))
                        {
                            Vector3Int potatoes = new Vector3Int(target.x + x, (target.y + y), 0);
                            if (!GeneralManager.Instance.highlightmap.HasTile(potatoes))
                            {
                                continue;
                            }
                            GeneralManager.Instance.highlightmap.SetTile(potatoes, GeneralManager.Instance.tileb);
                        }
                    }
                }
            }
        }
        foreach(Ability item in GeneralManager.Instance.SelectedCritter.GetComponent<CritterHolder>().AbilityList)
        {
            if(item.GetType() == typeof(ForageAbility) || item.GetType() == typeof(HarvesterAbility))
            {
                //ForageAbility items = (ForageAbility)item;
                Vector2Int vector = item.arrayBool.GridSize;
                for (int x = -(vector.x-1)/2; x <= (vector.y)/2; x++)
                {
                    for (int y = -(vector.y-1)/2; y <= (vector.y)/2; y++)
                    {
                        if(item.arrayBool.GetCell((x+(vector.x)/2),(y+(vector.y)/2)))
                        {
                            Vector3Int potatoes = new Vector3Int(target.x + x, (target.y + y), 0);
                            if (!GeneralManager.Instance.highlightmap.HasTile(potatoes))
                            {
                                continue;
                            }
                            if(GeneralManager.Instance.dicty[potatoes] != null)
                            {
                                if(GeneralManager.Instance.dicty[potatoes].GetComponent<CritterHolder>().IsThisViable(item.food))
                                {
                                    GeneralManager.Instance.highlightmap.SetTile(potatoes, GeneralManager.Instance.tilec);
                                }
                            }
                            if(GeneralManager.Instance.tiledict[potatoes] != null)
                            {
                                if(GeneralManager.Instance.tiledict[potatoes].name == item.food)
                                {
                                    GeneralManager.Instance.highlightmap.SetTile(potatoes, GeneralManager.Instance.tilec);
                                }
                            }
                            
                        }
                    }
                }
            }
            GeneralManager.Instance.highlightmap.SetTile(target, GeneralManager.Instance.tiled);
        }
        
    }
    public void UpdateStockpiles()
    {
        string potato = "";
        foreach (var item in ResourceList)
        {
            float duck = (float)item.amount/10;
            potato += item.name + " : " + duck.ToString() + "\n";
        }
        
        //datafile.text = potato;
    }
    public void AddResource(string name = null, int amount = 0, Resource resource = null)
    {
        if(resource != null)
        {
            foreach (var item in ResourceList)
            {
                if(item.name == resource.name)
                {
                    item.amount += resource.amount;
                    UpdateStockpiles();
                    return;
                }
            }
            Resource resource1 = new Resource();
            resource1.name = resource.name;
            resource1.amount = resource.amount;
            ResourceList.Add(resource1);
            UpdateStockpiles();
            return;
        }
        if(name != null)
        {
            foreach (var item in ResourceList)
            {
                if(item.name == name)
                {
                    item.amount += amount;
                    UpdateStockpiles();
                    return;
                }
            }
        }
    }
}
[System.Serializable]
public class Resource
{
    public string name;
    public int amount;
    public Sprite sprite;
}
