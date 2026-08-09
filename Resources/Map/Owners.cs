using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Owners : MonoBehaviour
{
    public static Owners Instance;
    public List<Nation> nationlist;
    public Dictionary<string, Nation> nationdict;
    public List<State> statelist;
    public List<Culture> culturelist;
    public Dictionary<string, Culture> culturedict;
    public List<Province> provincelist;
    public GameObject CityObject;
    public Dictionary<string, Province> provincedict;
    public Dictionary<Color32, Province> provincedictcolor;
    public List<FieldArmyHolder> armylist = new List<FieldArmyHolder>();
    public double timer;
    public int turncounter;
    public int xxx = 25;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        this.transform.GetComponent<LoadProvinces>().LoadinCultures();

        culturedict = new Dictionary<string, Culture>();
        foreach (Culture culture in culturelist)
        {
            culturedict.Add(culture.name, culture);
        }

        this.transform.GetComponent<LoadProvinces>().LoadStuff();
        nationdict = new Dictionary<string, Nation>();
        foreach (Nation nation in nationlist)
        {
            nationdict.Add(nation.name, nation);
            nation.IsPlayer = false;
            nation.faction = nation.faction.Init();
            //Debug.LogError(nation.name);
            nation.faction.Set();
            nation.nationalbrainy = new NationalBrain();
            nation.nationalbrainy.nation = nation.name;
            nation.nationalbrainy.name = nation.name + "_brain";
            

            nation.faction.color = nation.ownerIdentity;

            if (SessionManager.Instance.HostFaction.name.Contains(nation.name))
            {
                nation.IsPlayer = true;
                nation.faction = SessionManager.Instance.HostFaction;
            }
        }

        provincedict = new Dictionary<string, Province>();
        provincedictcolor = new Dictionary<Color32, Province>();
        foreach (Province province in provincelist)
        {
            province.CreateGarrison();
            province.SetAdjacents();
            try
            {
                provincedict.Add(province.name, province);
                provincedictcolor.Add(new Color32(province.identity.r, province.identity.g, province.identity.b, 0), province);
            }
            catch
            {
                //Debug.LogError(province.name);
            }
            province.OriginalNation = province.nation;
        }
        Mapshower.Instance.Paint();

        PlantCities();

        foreach (Nation nation in nationlist)
        {
            nation.nationalbrainy.Startie();
        }
    }
    public void PlantCities()
    {
        foreach (var province in provincelist)
        {
            var a = Instantiate(CityObject, this.transform.GetChild(0).GetChild(1).GetChild(1));
            a.transform.localScale = new Vector3(25f, 25f, 25f);
            a.transform.position = new Vector3(province.position.x * 1f - 512f, province.position.y * 1f - 331f, 0);
        }
    }
    public Nation CallPlayer()
    {
        foreach (Nation Nation in Owners.Instance.nationlist)
        {
            if (Nation.IsPlayer == true)
            {
                Nation nation = Nation;
                return Nation;
            }
        }
        return new Nation();
    }
    public Nation CallNation(string nationname)
    {
        return nationdict[nationname];
    }
    public Province CallProvinceByString(string provincename)
    {
        return provincedict[provincename];
    }
    public Province CallProvinceByColor(Color32 provincecolor)
    {
        if (provincecolor.r == 0 && provincecolor.g == 0 & provincecolor.b == 0)
        {
            return null;
        }
        return provincedictcolor[provincecolor];
    }
    public Culture CallCultureByName(string culturename)
    {
        return culturedict[culturename];
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!FieldArmyHolder.PlayerFieldArmy.IsTargetNull() || Input.GetKey("space"))
        {
            foreach (var item in armylist)
            {
                item.Act();
                if (timer % 50 == 0) //50
                {
                    //Recruitment etc
                    item.NextTurn();
                }
            }
            if (timer % 100 == 0)//1000 == 0) //if (timer % 250 == 0)
            {
                TakeTurns();
            }
            if (timer % 10 == 0)
            {
                TakeTurns();
            }
            timer++;
            foreach(var nation in nationlist)
            {
                nation.nationalbrainy.Think();
            }
        }

    }
    public void TakeTurns()
    {
        foreach (var nation in nationlist)
        {
            nation.TakeTurn();
        }
    }
}
[System.Serializable]
public class Province
{
    public string name;
    public Color32 identity;
    public Nation nation;
    public Nation OriginalNation;
    public string state;
    public Vector2 position;
    public int population = 1000;
    public int supply = 1000;
    public FieldArmy garrison;

    public List<string> AdjacentProvinces = new List<string>();

    public List<Culture> cultures;
    public int taxincome;
    public int taxpercentage;
    public int levyincome;
    public int levypercentage;
    public int unrest;

    public List<Province> GrabAdjacents()
    {
        var a = new List<Province>();
        foreach(var b in AdjacentProvinces)
        {
            a.Add(Owners.Instance.provincedict[b]);
        }
        return a;
    }
    public void SetAdjacents()
    {
        //Debug.LogError("setting adjacents");
        AdjacentProvinces.Clear();
        
        foreach(var b in Owners.Instance.provincelist)
        {
            if(b == this)
            {
                continue;
            }
            if(Vector3.Distance(b.position, position) < 50)
            {
                AdjacentProvinces.Add(b.name);
            }
        }
        // if(AdjacentProvinces.Count < 5)
        // {
        //     foreach(var b in Owners.Instance.provincelist)
        //     {
        //         if(b == this)
        //         {
        //             continue;
        //         }
        //         if(Vector3.Distance(b.position, position) < 70)
        //         {
        //             AdjacentProvinces.Add(b.name);
        //         }
        //     }
        // }
        // if(AdjacentProvinces.Count < 5)
        // {
        //     foreach(var b in Owners.Instance.provincelist)
        //     {
        //         if(b == this)
        //         {
        //             continue;
        //         }
        //         if(Vector3.Distance(b.position, position) < 90)
        //         {
        //             AdjacentProvinces.Add(b.name);
        //         }
        //     }
        // }
    }

    public void CreateGarrison()
    {
        garrison = new FieldArmy();
        garrison.nation = nation;
        for (int i = 0; i < 2; i++)
        {
            garrison.AddTroop(nation.faction.UnitDataList[i], nation.faction.UnitDataList[i].name, 3);
        }
    }
    public FieldArmyHolder SallyOut(FieldArmyHolder sally)
    {
        return Mapshower.Instance.SpawnArmy(this);
    }
    
    public void UpdatePopulation()
    {
        population = 0;
        foreach (Culture culture in cultures)
        {
            population += culture.population;
        }
    }
    public void LosePopulation(int percentage)
    {
        foreach (Culture culture in cultures)
        {
            culture.population -= (int)(culture.population*percentage/100);
        }
        UpdatePopulation();
    }
}
[System.Serializable]
public class Nation
{
    public string name;
    public Color32 ownerIdentity;
    public bool IsPlayer;
    public List<FieldArmyHolder> armies = new List<FieldArmyHolder>();
    // public List<Nation> Enemies;
    public Faction faction;
    public NationalBrain nationalbrainy;
    public int Manpower = 0;
    public int ArmyNumber = 0;

    public void TakeTurn()
    {
        Manpower++;
    }
    public void SpawnArmy()
    {
        if (armies.Count < 5)
        {
            var a = new List<Province>();
            foreach (var province in Owners.Instance.provincelist)
            {
                if (province.nation == this)
                {
                    a.Add(province);
                }
            }
            if (a.Count > 0)
            {
                ArmyNumber++;
                Mapshower.Instance.SpawnArmy(a[Random.Range(0, a.Count)], ArmyNumber.ToString() + "st Army of " + name);
            }
        }
    }
}
[System.Serializable]
public class Culture
{
    public string name;
    public Color32 ownerIdentity;
    public int population;
}
[System.Serializable]
public class State
{
    public string name;
    public List<Province> provincelist;
    public Color32 stateIdentity; 
    public Nation nation;
    public int taxpercentage;
    public int levypercentage;
}