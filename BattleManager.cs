using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;
    public GameObject SelectedCritter;
    public Tilemap ownermap;
    public Tilemap highlightmap;
    public Dictionary <Vector3Int, GameObject> dicty = new Dictionary<Vector3Int,GameObject>();
    public List<GameObject> enemylist = new List<GameObject>();
    public int Score;
    public int Reserves = 5;
    public Text texty;
    public Text Rtexty;
    public GameObject Banner;
    public GameObject MousePet;

    public virtual void Awake()
    {
        Instance = this;
        foreach (Vector3Int position in ownermap.cellBounds.allPositionsWithin)
        {
            dicty.Add(position, null);
        }
        int enemies = Random.Range(GeneralManager.Instance.EnemyIntensity,GeneralManager.Instance.EnemyIntensity+7);
        for (int i = 0; i < enemies; i++)
        {
            Vector3Int position = new Vector3Int(Random.Range(-2,5), Random.Range(-4,3),0);
            if(dicty[position] == null)
            {
                if (ownermap.HasTile(position))
                {
                    Spawn(position,name:"AIDude");
                    ChangeScore(-1);
                }
            }
        }

        ChangeScore(2);
        ChangeReserves(GeneralManager.Instance.BaseReserves);
    }
    public virtual void ChangeReserves(int testy)
    {
        Reserves -= testy;
        Rtexty.text = Reserves + " Friendly Reserves Remaining.";
        if(Score > 0 && Reserves <= 0)
        {
            Banner.SetActive(true);
            Banner.transform.GetChild(0).GetComponent<Text>().text = "WE ARE OUTMANOUVERED, FALL BACK!";
            IEnumerator coroutine = ReturnToCity();
            StartCoroutine(coroutine);
        }
    }
    public virtual void ChangeScore(int testy)
    {
        Score -= testy;
        texty.text = Score + " Enemy Morale Remaining.";
        if(Score <= 0)
        {
            Banner.SetActive(true);
            Banner.transform.GetChild(0).GetComponent<Text>().text = "THE COWARDS HAVE BROKEN AND ABANDON THEIR LINES";
            IEnumerator coroutine = ReturnToCity();
            StartCoroutine(coroutine);
        }
    }
    IEnumerator ReturnToCity()
    {
        yield return new WaitForSeconds(3);
        
        GeneralManager.Instance.cam1.gameObject.SetActive(true);
        GeneralManager.Instance.cam2.gameObject.SetActive(false);
        GeneralManager.Instance.cam3.gameObject.SetActive(true);
        SceneManager.UnloadScene("FightScene 1");
    }

    // Update is called once per frame
    void Update()
    {
        Vector3Int target = ownermap.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        target.z = 0;
        if (!ownermap.HasTile(target)) {
            return;
        }
        MousePet.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y +0.25f, 0);;
        // if (!highlightmap.HasTile(target)) {
        //     return;
        // }
        if (Input.GetMouseButtonDown(1))
        {
            Spawn(target, SelectedCritter);
        }
        foreach (Vector3Int position in highlightmap.cellBounds.allPositionsWithin)
        {
            highlightmap.SetTile(position, GeneralManager.Instance.tilea);
        }
        foreach(Ability item in SelectedCritter.GetComponent<CritterHolder>().AbilityList)
        {
            if(item.GetType() == typeof(ForageAbility) || item.GetType() == typeof(HarvesterAbility) || item.GetType() == typeof(CutterAbility))
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
                            if (!highlightmap.HasTile(potatoes))
                            {
                                continue;
                            }
                            highlightmap.SetTile(potatoes, GeneralManager.Instance.tileb);
                        }
                    }
                }
            }
        }
        foreach(Ability item in SelectedCritter.GetComponent<CritterHolder>().AbilityList)
        {
            if(item.GetType() == typeof(ForageAbility) || item.GetType() == typeof(HarvesterAbility) || item.GetType() == typeof(CutterAbility))
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
                            if (!highlightmap.HasTile(potatoes))
                            {
                                continue;
                            }
                            if(dicty[potatoes] != null)
                            {
                                if(dicty[potatoes].GetComponent<CritterHolder>().IsThisViable(item.food))
                                {
                                    highlightmap.SetTile(potatoes, GeneralManager.Instance.tilec);
                                }
                            }
                            // if(GeneralManager.Instance.tiledict[potatoes] != null)
                            // {
                            //     if(GeneralManager.Instance.tiledict[potatoes].name == item.food)
                            //     {
                            //         highlightmap.SetTile(potatoes, GeneralManager.Instance.tilec);
                            //     }
                            // }
                        }
                    }
                }
            }
            highlightmap.SetTile(target, GeneralManager.Instance.tiled);
        }
    }
    public virtual void Spawn(Vector3Int target, GameObject spawnee = null, string name = "null")
    {
        GameObject spawner = null;
        if(name != null)
        {
            spawner = Resources.Load<GameObject>("Prefabs/"+name);
        }
        else
        {
            spawner = spawnee;
        }
        
        
        GameObject trader = Instantiate(spawner, ownermap.transform);
        trader.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y +0.25f, 0);
        trader.GetComponent<CritterHolder>().spot = target;
        trader.name = trader.GetComponent<CritterHolder>().name;
        Destroy(dicty[target]);
        dicty[target] = trader;
        
        if(trader.name != "AIDude")
        {
            ChangeReserves(1);
        }
    }
}
