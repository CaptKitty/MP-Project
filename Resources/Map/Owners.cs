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
    public Dictionary<string, Province> provincedict;
    public Dictionary<Color32, Province> provincedictcolor;
    // public List<Province> provincelists;

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
        }
        
        provincedict = new Dictionary<string, Province>();
        provincedictcolor = new Dictionary<Color32, Province>();
        foreach (Province province in provincelist)
        {
            try
            {
                provincedict.Add(province.name, province);
                provincedictcolor.Add(new Color32(province.identity.r, province.identity.g, province.identity.b,0), province);
            }
            catch
            {
                print(province.name);
            }
        }
        // Debug.Log(nationdict["Netherlands"].manpower);
    }
    public Nation CallPlayer()
    {
        foreach (Nation Nation in Owners.Instance.nationlist)
        {
            if(Nation.IsPlayer == true)
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
        return provincedictcolor[provincecolor];
    }
    public Culture CallCultureByName(string culturename)
    {
        return culturedict[culturename];
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
[System.Serializable]
public class Province
{
    public string name;
    public Color32 identity;
    public Nation nation;
    public string state;
    public Vector2 position;
    public int population = 1000;
    public List<Culture> cultures;
    public int taxincome;
    public int taxpercentage;
    public int levyincome;
    public int levypercentage;
    public int unrest;
    
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
    public int manpower;
    public int treasury;
    public List<Weapon> unlockedweapons;
    public List<Armor> unlockedarmor;
    public List<Regiment> regimentdesigns;
    public List<GameObject> armies;
    public List<Nation> Enemies;

    public void AddManpower(int Manpower)
    {
        manpower += Manpower;
        //UIManager.Instance.ChangeGovernmentText();
    }
    public bool HasManpower(int Manpower)
    {
        if(manpower >= Manpower)
        {
            return true;
        }
        return false;
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
    public Nation nation;
    public int taxpercentage;
    public int levypercentage;
}
[System.Serializable]
public class General
{
    public string name;
    public List<Regiment> regimentList;
    public Nation nation;
    public List<Trait> Traits;
}
[System.Serializable]
public class Regiment
{
    public string name;
    public UnitType unittype;
    public string nation;
    public int health;
    public int maxhealth;
    public float movement = 1;
    public Equipment equipment;
    public bool in_range;
    public bool loaded;
    public float reload;
    public Combat Combatstance;
    public Vector2 waypoint;
    public Vector2 viewpoint;
    public Vector2 currentPosition;
    public List<GameObject> enemies;
}
[System.Serializable]
public class Equipment
{
    public Weapon weapon;
    public Armor armor;
}
[System.Serializable]
public class Weapon
{
    public string name;
    public int range;
    public int accuracy;
    public int reloadtime;
}
[System.Serializable]
public class Armor
{
    public string name;
    public int health;
}
[System.Serializable]
public class Unit
{
    public string name;
}
[System.Serializable]
public class Trait
{
    public string traitname;
}
public enum Formation
{
    None,
    Line,
    DoubleLine,
    Column
}
public enum Order
{
    None,
    Advance
}
public enum Combat
{
    None,
    OnCommand,
    AtWill
}
public enum UnitType
{
    Infantry,
    Artillery,
    Cavalry
}