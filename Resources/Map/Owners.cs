using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class Owners : MonoBehaviour
{
    public static Owners Instance;
    public List<Nation> nationlist;
    public Dictionary<string, Nation> nationdict;
    public List<State> statelist;
    public List<Culture> culturelist;
    public Dictionary<string, Culture> culturedict;
    public List<Province> provincelist = new List<Province>();
    public Dictionary<string, Province> provincedict;
    public Dictionary<Color32, Province> provincedictcolor;
    public List<GameObject> armylist = new List<GameObject>();
    private float timer;
    public int Turn = 0;
    //How Long does a Turn last ingame?
    public float TimeScale = 1f;
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
            nation.IsPlayer = false;
            if(SessionManager.Instance.HostFaction.name.Contains(nation.name))
            {
                nation.IsPlayer = true;
            }
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
                Debug.LogError(province.name);
            }
        }
        Mapshower.Instance.Paint();
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

    void Update()
    {
        if(timer <= Time.time)
        {
            timer = Time.time + (TimeScale / 50);
            // foreach (var RPC in TestRelay.Instance.PlayerObjects)
            // {
            //     RPC.GetComponent<RpcTest>().HandleUpdate();
            // }
            if(RpcTest.Serverchecker.IsServer)
            {
                ServerUpdateHandler();
            }
        }
    }
    public void SetTime(float time)
    {
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().SetSecondsPerTurnServerRpc(time);
        }
    }

    public void ServerUpdateHandler()
    {
        Turn++;
        if(Turn % 250 == 0) //OncePerFiveSeconds
        {
            foreach(var a in provincelist)
            {
                if(a.troops < a.GrabMaxTroops())
                {
                    a.AddTroops(a.GrabTroopstoAdd());
                }
            }
            
            UIElement.ProvinceHost.Updatethird(Mapshower.Instance.SelectedProvince.troops.ToString());
        }
        if(Turn % 50 == 0) //OncePerOneSecond
        {
            var ModifiersToRemove = new List<ProvinceModifier>();
            foreach(var a in nationlist)
            {
                foreach (var aa in a.NationModifier)
                {
                    if(aa.Enddate != -1 && aa.Enddate < Turn)
                    {
                        ModifiersToRemove.Add(aa);
                    }
                }
                foreach (var item in ModifiersToRemove)
                {
                    a.RemoveModifier(item);
                }
            }
            
            UIElement.ProvinceHost.Updatethird(Mapshower.Instance.SelectedProvince.troops.ToString());
        }
        
        var b = new List<GameObject>();
        foreach(var a in armylist)
        {
            if(a != null)
            {
                //a.GetComponent<ArmyMovement>().Movement();
                a.GetComponent<ArmyMovement>().Fighty();
            }
            if(a == null)
            {
                b.Add(a);
            }
        }
        foreach(var a in b)
        {
            armylist.Remove(a);
        }
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().UpdateTroopsMovementServerRpc();
        }
    }
    public void HandleMovement()
    {
        foreach(var a in armylist)
        {
            if(a != null)
            {
                a.GetComponent<ArmyMovement>().Movement();
            }
        }
    }
    public void UpdateCount(string armyname)
    {
        foreach(var a in armylist)
        {
            if(a != null)
            {
                if(a.name == armyname)
                {
                    a.GetComponent<ArmyMovement>().SetTroopsMarker();
                }              
            }
        }
    }
    public void Kill(string armyname)
    {
        foreach(var a in armylist)
        {
            if(a != null)
            {
                if(a.name == armyname)
                {
                    Destroy(a);
                }              
            }
        }
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
    public int troops = 10;
    public List<Culture> cultures;
    public int taxincome;
    public int taxpercentage;
    public int levyincome;
    public int levypercentage;
    public int unrest;
    public List<ProvinceModifier> provincemodifiers = new List<ProvinceModifier>();
    public GameObject Drafty = null;
    
    public void AddModifier(ProvinceModifier moddie)
    {
        //provincemodifiers.Add(moddie);
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().AddProvinceModifierServerRpc(moddie.name, name);
        }
        UIElement.ProvinceHost.UpdateDescription(this);
    }
    public void AddTroops(int a)
    {
        troops += a;
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().SendCityUpdateServerRpc(name, nation.name, troops);
        }
        UIElement.ProvinceHost.Updatethird(Mapshower.Instance.SelectedProvince.troops.ToString());
    }
    public void AddLocalModifier(string moddie)
    {
        var modi = Resources.Load<ProvinceModifier>("Prefabs/Modifiers/" + moddie);
        provincemodifiers.Add(modi);
        UIElement.ProvinceHost.UpdateDescription(this);
    }
    public int MaxDice()
    {
        return 6;
    }
    public int GrabDefensiveDice()
    {
        int dice = 0;
        foreach (var item in provincemodifiers)
        {
            if(item == null)
            {
                continue;
            }
            dice += item.DefensiveDice;
        }
        foreach (var item in nation.NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            dice += item.DefensiveDice;
        }
        return dice;
    }
    public int GrabOffensiveDice()
    {
        int dice = 0;
        foreach (var item in nation.NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            dice += item.OffensiveDice;
        }
        return dice;
    }
    public int GrabMaxTroops()
    {
        int troopcount = 20;
        foreach (var item in provincemodifiers)
        {
            if(item == null)
            {
                continue;
            }
            troopcount += item.BaseTroops;
        }
        foreach (var item in nation.NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            troopcount += item.BaseTroops;
        }
        foreach (var item in provincemodifiers)
        {
            if(item == null)
            {
                continue;
            }
            troopcount = (int)((float)troopcount * item.BaseTroopsModifier);
        }
        foreach (var item in nation.NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            troopcount = (int)((float)troopcount * item.BaseTroopsModifier);
        }
        return troopcount;
    }
    public int GrabTroopstoAdd()
    {
        int recruitcount = 1;
        foreach (var item in provincemodifiers)
        {
            if(item == null)
            {
                continue;
            }
            recruitcount += item.BonusSpawns;
        }
        return recruitcount;
    }

    public void SetTroopsMarker()
    {
        if(Drafty == null)
        {
            GameObject potato = Resources.Load<GameObject>("Prefabs/Map_Farmer");
            GameObject Corn = Owners.Instance.gameObject;
            GameObject tomato = GameObject.Instantiate(potato, GameObject.Find("Map").transform.GetChild(2));
            Vector2 location = position;
            location = new Vector2(location.x-366,location.y-218);
            tomato.transform.position = location;
            tomato.name = troops.ToString();
            tomato.transform.GetChild(0).GetChild(0).GetComponent<Text>().text = troops.ToString();
            tomato.transform.GetComponent<Image>().color = new Color32(nation.ownerIdentity.r, nation.ownerIdentity.g, nation.ownerIdentity.b, 255);

            GameObject.Destroy(tomato.GetComponent<ArmyMovement>());
            Drafty = tomato;
        }
        Drafty.transform.GetChild(0).GetChild(0).GetComponent<Text>().text = troops.ToString();
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
    public int manpower;
    public int treasury;
    public List<Weapon> unlockedweapons;
    public List<Armor> unlockedarmor;
    public List<Regiment> regimentdesigns;
    public List<GameObject> armies;
    // public List<Nation> Enemies;
    public Faction faction;
    public List<ProvinceModifier> NationModifier = new List<ProvinceModifier>();

    public void AddModifier(ProvinceModifier moddie)
    {
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().AddNationModifierServerRpc(moddie.name, name);
        }
    }
    public void AddNationalModifier(string moddie)
    {
        var modi = Resources.Load<ProvinceModifier>("Prefabs/Modifiers/" + moddie);
        modi = modi.Init();
        if(modi.Enddate != -1)
        {
            modi.Enddate = Owners.Instance.Turn + modi.Enddate;
        }
        NationModifier.Add(modi);
    }
    public void RemoveModifier(ProvinceModifier moddie)
    {
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().RemoveNationModifierServerRpc(moddie.name, name);
        }
    }
    public void RemoveNationalModifier(string moddie)
    {
        var modi = NationModifier.Find(x => x.name == moddie);
        NationModifier.Remove(modi);
    }
    public int GrabMaxTroops()
    {
        int troopcount = 0;
        foreach (var item in NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            troopcount += item.BaseTroops;
        }
        foreach (var item in NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            troopcount = (int)((float)troopcount * item.BaseTroopsModifier);
        }
        return troopcount;
    }
    public int GrabDefensiveDice()
    {
        int dice = 0;
        foreach (var item in NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            dice += item.DefensiveDice;
        }
        return dice;
    }
    public int GrabOffensiveDice()
    {
        int dice = 0;
        foreach (var item in NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            dice += item.OffensiveDice;
        }
        return dice;
    }
    public float GrabSpeedModifier()
    {
        float speed = 1;
        foreach (var item in NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            speed = speed * item.SpeedModifier;
        }
        return speed;
    }
    public int GrabCombatWidth()
    {
        int CombatWidth = 0;
        foreach (var item in NationModifier)
        {
            if(item == null)
            {
                continue;
            }
            CombatWidth += item.BonusCombatWidth;
        }
        return CombatWidth;
    }
    
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
    public Color32 stateIdentity; 
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