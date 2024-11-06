using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

public class GlobalManager : MonoBehaviour
{  
    public static GlobalManager Instance;
    public Tilemap ownermap;
    public Tilemap faithmap;
    public Dictionary <Vector3Int, GameObject> dicty = new Dictionary<Vector3Int,GameObject>();
    public Dictionary <Vector3Int, float> faithamount = new Dictionary<Vector3Int, float>();
    void Start()
    {
        if(GlobalManager.Instance == null)
        {
            Instance = this;
        }
        GeneralManager.Instance.cam2 = GameObject.Find("GlobalCamera").GetComponent<Camera>();
        foreach (Vector3Int position in ownermap.cellBounds.allPositionsWithin)
        {
            dicty.Add(position, null);
            faithamount.Add(position,0);
        }
        
        //Debug.LogError("duck1");
        Spawn(new Vector3Int(36,31,0), name:"Monastery");

        GeneralManager.Instance.cam2.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Vector3Int target = faithmap.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            target.z = 0;

            CritterHolder testy = GeneralManager.Instance.SelectedCritter.GetComponent<CritterHolder>();

            // bool canbreathe = false;
            // foreach (var item in testy.ViablePlacingSpots)
            // {
            //     if(item == GeneralManager.Instance.tiledict[target].name)
            //     {
            //         canbreathe = true;
            //     }
            // }

            // if(canbreathe)
            // {
                if(testy.cost.name == "" || testy.cost == null)
                {
                    Spawn(target, GeneralManager.Instance.SelectedCritter);
                }
                else
                {
                    bool canspawn = false;
                    foreach (var item in CityManager.Instance.ResourceList)
                    {
                        if(item.name == testy.cost.name)
                        {
                            if(item.amount >= testy.cost.amount)
                            {
                                CityManager.Instance.AddResource(resource:testy.cost);
                                canspawn = true;
                            }
                        }
                    }
                    if(canspawn)
                    {
                        Spawn(target, GeneralManager.Instance.SelectedCritter);
                    }
                }
            // }

            
        }
        if(Input.GetKeyDown("f"))
        {
            var a = faithmap.gameObject.transform.parent.gameObject;
            if(a.active == true)
            {
                a.SetActive(false);
            }
            else
            {
                a.SetActive(true);
            }
        }
        if(faithmap.gameObject.transform.parent.gameObject.active)
        {
            foreach (Vector3Int position in ownermap.cellBounds.allPositionsWithin)
            {
                faithmap.SetTileFlags(position, TileFlags.None);
                Color newcolor = new Color(0, 0, 0, 0.8f-(2f*faithamount[position]));
                faithmap.SetColor(position, newcolor);
            }
        }
    }

    public void Spawn(Vector3Int target, GameObject spawnee = null, string name = "null")
    {
        //Debug.LogError("duck2");
        if(name != "null")
        {
            //Debug.LogError("duck3");
            GameObject spawner = Resources.Load<GameObject>("Prefabs/"+name);
            GameObject trader = Instantiate(spawner, ownermap.transform);
            GeneralManager.Instance.futurecritterobjects.Add(trader);
            trader.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y + 0.25f, 0);
            trader.GetComponent<CritterHolder>().spot = target;
            trader.name = trader.GetComponent<CritterHolder>().name;
            //Debug.LogError("duck4");
            if(!trader.GetComponent<CritterHolder>().DoesThisgoOnTheCity)
            {
                //Debug.LogError("duck5");
                Destroy(trader);
                return;
            }
            //Debug.LogError("duck6");
            Destroy(dicty[target]);
            dicty[target] = trader;
            return;
        }
        if(dicty[target] == null || dicty[target].name == "Tree")
        {
            GameObject trader = Instantiate(spawnee, ownermap.transform);
            GeneralManager.Instance.futurecritterobjects.Add(trader);
            trader.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y + 0.25f, 0);
            trader.GetComponent<CritterHolder>().spot = target;
            trader.name = trader.GetComponent<CritterHolder>().name;
            if(!trader.GetComponent<CritterHolder>().DoesThisgoOnTheCity)
            {
                //Debug.LogError("duck5");
                Destroy(trader);
                return;
            }
            dicty[target] = trader;
        }
        // else
        // {
        //     Destroy(dicty[target]);
        //     GameObject trader = Instantiate(spawnee);
        //     futurecritterobjects.Add(trader);
        //     trader.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.5f, ownermap.CellToWorld(target).y + 0.5f, 0);
        //     trader.GetComponent<CritterHolder>().spot = target;
        //     trader.GetComponent<CritterHolder>().population = 1;
        //     dicty[target] = trader;
        // }
    }
}
