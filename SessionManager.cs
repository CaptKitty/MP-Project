using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance;
    public List<CritterHolder> HostArmy = new List<CritterHolder>();
    public List<CritterHolder> ClientArmy = new List<CritterHolder>();
    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    public void SpawnToHost(Vector3Int target, GameObject spawnee = null, string Faction = "Royal", string name = "null", bool AIorNot = false, string futurename = "null")
    {
        // CritterHolder critter = new CritterHolder();
        // var spawner = Resources.Load<GameObject>("Prefabs/Units/"+ Faction + "/" + name);
        // critter = spawner.GetComponent<CritterHolder>();
        // var critters = Instantiate(critter, this.transform);
        // critters.spot = target;
        // critters.IsthisAI = AIorNot;
        // critters.name = futurename;
        // //critter.SetActive(false);

        // HostArmy.Add(critters);
    }
}
