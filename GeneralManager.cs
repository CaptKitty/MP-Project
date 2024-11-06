using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GeneralManager : MonoBehaviour
{
    public static GeneralManager Instance;
    public Tilemap ownermap;
    public Tilemap highlightmap;
    public GameObject SelectedCritter;
    public bool delete;
    public Dictionary <Vector3Int, GameObject> dicty = new Dictionary<Vector3Int,GameObject>();
    public Dictionary <Vector3Int, TileBase> tiledict = new Dictionary<Vector3Int, TileBase>();

    public List<GameObject> critterobjects = new List<GameObject>();
    public List<GameObject> futurecritterobjects = new List<GameObject>();
    
    public int turncounter, totalturncounter;
    public int requiredscore;
    public Text turntext;
    public int Score;
    public bool dead;
    public GameObject Deadscreen;
    public Text textscore;
    public bool highlight = false;

    public Tile tilea, tileb, tilec, tiled;
    public Camera cam1, cam2, cam3;
    private int Timer = 0;
    public int turntimer = 10;
    public List<AudioClip> AudioClips = new List<AudioClip>();
    public AudioSource Audioclippy;
    public int EnemyIntensity = 5;
    public int BaseReserves = 2;
    public bool gamepaused = false;
    public void Awake()
    {
        
        if(GeneralManager.Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(GeneralManager.Instance.gameObject);
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            // GeneralManager.Instance.ownermap = CityManager.Instance.gameObject.transform.GetChild(0).GetChild(0).GetComponent<Tilemap>();
            // GeneralManager.Instance.highlightmap = CityManager.Instance.gameObject.transform.GetChild(1).GetChild(0).GetComponent<Tilemap>();
            // GeneralManager.Instance.cam1 = CityManager.Instance.gameObject.transform.parent.GetComponent<Camera>();
            //Destroy(this.gameObject);
        }
        int i = 0;

        foreach (Vector3Int position in ownermap.cellBounds.allPositionsWithin)
        {
            tiledict.Add(position, ownermap.GetTile(position));
            dicty.Add(position, null);
        }
        Debug.Log(tiledict[new Vector3Int(0,0,0)]);
        
        
        
        if(!cam2)
        {
            SceneManager.LoadScene("SampleScene 1", LoadSceneMode.Additive);
        }
        if(!cam3)
        {
            SceneManager.LoadScene("UIScene", LoadSceneMode.Additive);
        }

        // Turn();
    }
    public void Start()
    {
        Turn();
        foreach (Vector3Int position in ownermap.cellBounds.allPositionsWithin)
        {
            if(Random.Range(0,3) == 0)
            {
                if(dicty[position] == null)
                {
                    if(tiledict[position] != null)
                    {
                        if(tiledict[position].name == "Grassland")
                        {
                            Spawn(position,name:"Tree");
                        }
                    }
                }
            }
        }
    }
    public void ChangeScore(int change)
    {
        Score += change;
        //textscore.text = Score.ToString() + " / " + requiredscore;
        if(Score < requiredscore)
        {
            dead = true;
            Deadscreen.SetActive(true);
        }
    }
    public void StartBattle()
    {
        cam1.gameObject.SetActive(false);
        cam2.gameObject.SetActive(false);
        cam3.gameObject.SetActive(false);
        SceneManager.LoadScene("FightScene 1", LoadSceneMode.Additive);
    }
    public void Update()
    {
        HandleAudio();
        if(Input.GetKeyDown("1"))
        {
            cam2.gameObject.SetActive(false);
            cam1.gameObject.SetActive(true);
        }
        if(Input.GetKeyDown("2"))
        {
            cam1.gameObject.SetActive(false);
            cam2.gameObject.SetActive(true);
        }
        if(Input.GetKeyDown("3"))
        {
            StartBattle();
            return;
        }
        if(CityManager.Instance.gameObject.active && Input.GetKeyDown("space"))
        {
            gamepaused = !gamepaused;
        }
        if(Timer < Time.time)
        {
            Timer = (int)Time.time + turntimer;
            if(!gamepaused)
            {
                if(CityManager.Instance.gameObject.active)
                {
                    Turn();      
                }
            }
        }
        float speed = 0.05f;
        if(Input.GetKey("left shift"))
        {
            speed = 0.15f;
        }
        if(Input.GetKey("q"))
        {
            if(cam1)
            {
                cam1.orthographicSize += speed;
            }
            if(cam2)
            {
                cam2.orthographicSize += speed;
            }
            if(cam3)
            {
                cam3.orthographicSize += speed;
            }
        }
        if(Input.GetKey("e"))
        {
            if(cam1)
            {
                cam1.orthographicSize -= speed;
            }
            if(cam2)
            {
                cam2.orthographicSize -= speed;
            }
            if(cam3)
            {
                cam3.orthographicSize -= speed;
            }
        }
        // if(CityManager.Instance.transform.gameObject.active)
        // {
        //     if(Input.GetKey("w"))
        //     {
        //         cam1.gameObject.transform.position = new Vector3(cam1.gameObject.transform.position.x, cam1.gameObject.transform.position.y - speed,0);
        //     }
        //     if(Input.GetKey("a"))
        //     {
        //         cam1.gameObject.transform.position = new Vector3(cam1.gameObject.transform.position.x + speed, cam1.gameObject.transform.position.y,0);
        //     }
        //     if(Input.GetKey("s"))
        //     {
        //         cam1.gameObject.transform.position = new Vector3(cam1.gameObject.transform.position.x, cam1.gameObject.transform.position.y + speed,0);
        //     }
        //     if(Input.GetKey("d"))
        //     {
        //         cam1.gameObject.transform.position = new Vector3(cam1.gameObject.transform.position.x - speed, cam1.gameObject.transform.position.y,0);
        //     }
        // }
        // if(GlobalManager.Instance.transform.gameObject.active)
        // {
        //     if(Input.GetKey("w"))
        //     {
        //         cam2.gameObject.transform.position = new Vector3(cam2.gameObject.transform.position.x, cam2.gameObject.transform.position.y - speed,0);
        //     }
        //     if(Input.GetKey("a"))
        //     {
        //         cam2.gameObject.transform.position = new Vector3(cam2.gameObject.transform.position.x + speed, cam2.gameObject.transform.position.y,0);
        //     }
        //     if(Input.GetKey("s"))
        //     {
        //         cam2.gameObject.transform.position = new Vector3(cam2.gameObject.transform.position.x, cam2.gameObject.transform.position.y + speed,0);
        //     }
        //     if(Input.GetKey("d"))
        //     {
        //         cam2.gameObject.transform.position = new Vector3(cam2.gameObject.transform.position.x - speed, cam2.gameObject.transform.position.y,0);
        //     }
        // }
        if(Input.GetKeyDown("space"))
        {
            //Turn();
        }
        if(dead == true)
        {
            return;
        }
        // if (EventSystem.current.IsPointerOverGameObject())
        // {
        //     return;
        // }
        if(Camera.main)
        {
            Vector3Int target = ownermap.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            target.z = 0;
            if (!ownermap.HasTile(target)) {
                return;
            }
            if (!highlightmap.HasTile(target)) {
                return;
            }
            if(Input.GetKeyDown("delete"))
            {
                delete = true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if(delete == true)
                {
                    Destroy(dicty[target]);
                    dicty[target] = null;
                    delete = false;
                    return;
                }
            }
        }
        // if (Input.GetMouseButtonDown(0))
        // {
        //     CritterHolder testy = SelectedCritter.GetComponent<CritterHolder>();

        //     bool canbreathe = false;
        //     foreach (var item in testy.ViablePlacingSpots)
        //     {
        //         if(item == GeneralManager.Instance.tiledict[target].name)
        //         {
        //             canbreathe = true;
        //         }
        //     }

        //     if(canbreathe)
        //     {
        //         if(testy.cost.name == "" || testy.cost == null)
        //         {
        //             Spawn(target, SelectedCritter);
        //         }
        //         else
        //         {
        //             bool canspawn = false;
        //             foreach (var item in CityManager.Instance.ResourceList)
        //             {
        //                 if(item.name == testy.cost.name)
        //                 {
        //                     if(item.amount >= -testy.cost.amount)
        //                     {
        //                         CityManager.Instance.AddResource(resource:testy.cost);
        //                         canspawn = true;
        //                     }
        //                 }
        //             }
        //             if(canspawn)
        //             {
        //                 Spawn(target, SelectedCritter);
        //             }
        //         }
        //     }
        // }
    }
    public void Turn()
    {
        foreach (var item in futurecritterobjects)
        {
            critterobjects.Add(item);
        }
        futurecritterobjects.Clear();

        var potatolist = new List<GameObject>();

        foreach (var Critter in critterobjects)
        {
            Debug.Log(Critter);
            if(Critter != null)
            {
                potatolist.Add(Critter);
            }
            else
            {
                continue;
            }
            foreach(Ability ability in Critter.GetComponent<CritterHolder>().AbilityList)
            {
                Debug.Log(ability);
                if(ability == null)
                {
                    continue;
                }
                ability.Turn(Critter.GetComponent<CritterHolder>());
            }
        }
        critterobjects = potatolist;
        UIManager.Instance.UpdateUI();
        turncounter++;
        totalturncounter++;
        if(turncounter >= 25)
        {
            turncounter = 0;
            StartBattle();
            return;
        }
    }
    public void HandleAudio()
    {
        if(!Audioclippy.isPlaying)
        {
            Audioclippy.clip = AudioClips[Random.Range(0,AudioClips.Count)];
            Audioclippy.Play();
        }
    }
    public void Spawn(Vector3Int target, GameObject spawnee = null, string name = "null")
    {
        if(name != "null")
        {
            GameObject spawner = Resources.Load<GameObject>("Prefabs/"+name);
            GameObject trader = Instantiate(spawner, ownermap.transform);
            futurecritterobjects.Add(trader);
            trader.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y +0.25f, 0);
            trader.GetComponent<CritterHolder>().spot = target;
            trader.name = trader.GetComponent<CritterHolder>().name;
            if(trader.GetComponent<CritterHolder>().DoesThisgoOnTheCity)
            {
                Destroy(trader);
                return;
            }
            Destroy(dicty[target]);
            dicty[target] = trader;
            ChangeScore(0);
            return;
        }
        if(dicty[target] == null || dicty[target].name == "Tree")
        {
            GameObject trader = Instantiate(spawnee, ownermap.transform);
            futurecritterobjects.Add(trader);
            trader.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y +0.25f, 0);
            trader.GetComponent<CritterHolder>().spot = target;
            trader.name = trader.GetComponent<CritterHolder>().name;
            if(trader.GetComponent<CritterHolder>().DoesThisgoOnTheCity)
            {
                Destroy(trader);
                return;
            }
            Destroy(dicty[target]);
            dicty[target] = trader;
            //Turn();
            ChangeScore(0);
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
