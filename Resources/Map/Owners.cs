using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Linq;

using UnityEngine.Tilemaps;

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
    public int AICounter;
    //How Long does a Turn last ingame?
    public float TimeScale = 1f;
    // public List<Province> provincelists;
    public NationalBrain ActiveBrain;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        if(this != Owners.Instance)
        {
            return;
        }
        this.transform.GetComponent<LoadProvinces>().LoadinCultures();
        
        this.transform.GetComponent<LoadProvinces>().LoadinNations();
        
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
            Debug.Log(nation.name);
            Debug.Log(nation.PrimaryCulture.name);
            nation.PrimaryCulture = culturelist.Find(x => x.name == nation.PrimaryCulture.name);
            if(SessionManager.Instance.HostFaction.name.Contains(nation.name))
            {
                nation.IsPlayer = true;
            }
            nation.Brain = new NationalBrain();
            //nation.Brain.Start();
            nation.Brain.Startie(nation);
        }
        
        provincedict = new Dictionary<string, Province>();
        provincedictcolor = new Dictionary<Color32, Province>();
        foreach (Province province in provincelist)
        {
            // if(provincedict[province.name] != null)
            // {
            //     continue;
            // }
            try
            {
                provincedict.Add(province.name, province);
                provincedictcolor.Add(new Color32(province.identity.r, province.identity.g, province.identity.b,0), province);
            }
            catch
            {
                //Debug.LogError(province.name);
            }
        }
        Mapshower.Instance.Paint();
        foreach (var item in provincelist)
        {
            item.SetAdjacencies();
        }
        //Mapshower.Instance.Potato();
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
            ExecuteUpdate();
        }
    }
    void ExecuteUpdate()
    {
        timer = Time.time + (TimeScale / 50);
        // foreach (var RPC in TestRelay.Instance.PlayerObjects)
        // {
        //     RPC.GetComponent<RpcTest>().HandleUpdate();
        // }
        if(RpcTest.Serverchecker != null && RpcTest.Serverchecker.IsServer)
        {
            ServerUpdateHandler();
        }
        
    }
    public void SetTime(float time)
    {
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().SetSecondsPerTurnServerRpc(time);
        }
        ExecuteUpdate();
    }

    public void ServerUpdateHandler()
    {

        Turn++;
        if(Turn == 1) //OncePerFiveSeconds
        {
            foreach (var provvy in provincelist)
            {
                provvy.ResetJobs();
            }
        }
        if(Turn % 250 == 0) //OncePerFiveSeconds
        {
            foreach(var a in provincelist)
            {
                if(a.Drafty != null)
                {
                    //if(a.Drafty.transform.GetChild(0).GetChild(0).GetComponent<Text>().text == "0" || a.Drafty.transform.GetChild(0).GetChild(0).GetComponent<Text>().text == "-1")
                    if(a.troops < 1);
                    {
                        a.Drafty.GetComponent<Image>().enabled = false;
                        a.Drafty.transform.GetChild(0).GetChild(0).GetComponent<Text>().text = "";
                        a.Drafty.transform.position = new Vector2(a.position.x - Mapshower.Instance.Offset.x, a.position.y - Mapshower.Instance.Offset.y);
                    }
                }
                var b = Owners.Instance.statelist.Find(x => x.name == a.state);
                if(b.Capitol.troops < b.GrabMaxTroops())
                {
                    if(CallNation(a.nation.name) != null && CallNation(a.nation.name).nationalTreasury.Find(x => x.resource.name == "Manpower") != null && CallNation(a.nation.name).nationalTreasury.Find(x => x.resource.name == "Manpower").amount > 1)
                    {
                        CallNation(a.nation.name).nationalTreasury.Find(x => x.resource.name == "Manpower").amount -= a.GrabTroopstoAdd();
                        b.Capitol.AddTroops(a.GrabTroopstoAdd());
                    }
                }
            }
            UIElement.ProvinceHost.Updatethird(Mapshower.Instance.SelectedProvince.troops.ToString());
        }
        if(Turn % 1 == 0) //OncePerTwoSecond(100)
        {
            AICounter++;
            if(nationlist.Count <= AICounter)
            {
                AICounter = 0;
            }
            Nation a = nationlist[AICounter];
            if(a.IsAlive && !a.IsPlayer)
            {
                ActiveBrain = a.Brain;
                a.Brain.Think();
            }
            else
            {
                AICounter++;
                if(nationlist.Count <= AICounter)
                {
                    AICounter = 0;
                }
                a = nationlist[AICounter];
                if(a.IsAlive && !a.IsPlayer)
                {
                    ActiveBrain = a.Brain;
                    a.Brain.Think();
                }
            }
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

            foreach (var item in provincelist)
            {
                item.AddPopulationGrowth(1);
            }
            
            foreach (var item in statelist)
            {
                item.GrabStateOutput();
                var a = nationlist.Find(x => x.name == item.nation.name);
                foreach (var items in item.stateoutput)
                {
                    if(a.nationalTreasury.Find(x => x.resource == items.resource) != null)
                    {
                        a.nationalTreasury.Find(x => x.resource == items.resource).amount += items.amount;
                    }
                    else
                    {
                        EcoData tomato = items.GrabEcoData();
                        a.nationalTreasury.Add(tomato);
                    }
                }
            }
            var aaa = CallPlayer();
            string c = "";
            if(aaa.nationalTreasury.Find(x => x.resource.name == "Coin") != null)
            {
                c = Math.Round(aaa.nationalTreasury.Find(x => x.resource.name == "Coin").amount, 2).ToString();
            }
            UIElement.TopBarHost.UpdateTitle(aaa.name);
            UIElement.TopBarHost.UpdateDescription("Coin: " + c);

            var b = new List<GameObject>();
            foreach(var a in armylist)
            {
                if(a != null)
                {
                    a.GetComponent<ArmyMovement>().Combaty();
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
            UIElement.ProvinceHost.Updatethird(Mapshower.Instance.SelectedProvince.troops.ToString());
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
public class State : Province
{
    //public string name;
    public List<Province> provincelist;
    public Province Capitol;
    public Color32 stateIdentity; 
    public List<EcoData> stateoutput = new List<EcoData>();

    public override int GrabMaxTroops()
    {
        int troopcount = 0;
        foreach (var item in provincelist)
        {
            troopcount += item.GrabMaxTroops();
        }
        return troopcount;
    }
    public void GrabPopulationPieCharts()
    {
        cultures.Clear();
        foreach (var item in provincelist)
        {
            foreach (var culture in item.cultures)
            {
                if(cultures.Find(x => x.name == culture.name) != null)
                {
                    cultures.Find(x => x.name == culture.name).population += culture.population;
                }
                else
                {
                    cultures.Add(culture.GrabCulture());
                }
            }
        }
        var a = new List<float>();
        foreach (var item in cultures)
        {
            a.Add(item.population);
        }
        Piechart.Instance.SetValues(cultures);
    }
    public List<EcoData> GrabStateOutput()
    {
        var potato = new List<EcoData>();
        foreach (var provvy in provincelist)
        {
            //provvy.ResetJobs();
            foreach (var items in provvy.cultures)
            {
                foreach (var item in items.GrabIncome(this, provvy))
                {
                    if(potato.Find(x => x.resource == item.resource) != null)
                    {
                        potato.Find(x => x.resource == item.resource).amount += item.amount;
                    }
                    else
                    {
                        EcoData tomato = item.GrabEcoData();
                        potato.Add(tomato);
                    }
                }
            }
        }
        stateoutput = potato;
        return potato;
    }
    public List<EcoData> GrabJobModifier(string jobname, List<EcoData> inputlist)
    {
        if(jobname == "Farmer")
        {
            foreach (var provvy in provincelist)
            {
                foreach (var items in provvy.cultures)
                {
                    foreach (var jobbies in items.jobs)
                    {
                        if(jobbies.name == "Baker")
                        {
                            foreach (var item in inputlist)
                            {
                                item.amount += 1;
                            }
                        }
                    }
                }
            }
        }
        return inputlist;
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
    public int troops = 0;
    public int popgrowth = 0;
    public List<Culture> cultures = new List<Culture>();
    public List<ProvinceModifier> provincemodifiers = new List<ProvinceModifier>();
    public GameObject Drafty = null;
    public List<Buildings> BuildingList = new List<Buildings>();
    public List<Jobs> jobbies = new List<Jobs>();
    public List<Vector3Int> ProvincialTileList = new List<Vector3Int>();
    public List<Color32> AdjacentProvincesByColor = new List<Color32>();
    
    public void AddModifier(ProvinceModifier moddie)
    {
        //provincemodifiers.Add(moddie);
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().AddProvinceModifierServerRpc(moddie.name, name);
        }
        UIElement.ProvinceHost.UpdateDescription(this);
    }
    public virtual void AddTroops(int a)
    {
        
        troops += a;
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().SendCityUpdateServerRpc(name, nation.name, troops);
        }
        UIElement.ProvinceHost.Updatethird(Mapshower.Instance.SelectedProvince.troops.ToString());
    }
    public void SetAdjacencies()
    {
        var material = Mapshower.Instance.GetComponent<Renderer>().material;
        var mainTex = material.GetTexture("_MainTex") as Texture2D;
        
        for (int i = -4; i < 5; i++)
        {
            for (int j = -4; j < 5; j++)
            {
                var p = position;

                var heading = position - new Vector2(position.x + (float)((float)i/4), position.y + (float)((float)j/4));
                //Debug.LogError(heading);
                
                //int x = (int)Mathf.Floor(p.x);// + Mapshower.Instance.width / 2;
                //int y = (int)Mathf.Floor(p.y);// + Mapshower.Instance.height / 2;

                for (int l = 0; l < 50; l+=5)
                {
                    int x = (int)Mathf.Floor(p.x) + (l*(int)heading.x);// + Mapshower.Instance.width / 2;
                    int y = (int)Mathf.Floor(p.y) + (l*(int)heading.y);// + Mapshower.Instance.height / 2;
                    try
                    {
                        //Debug.LogError(Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0)).name);
                        if(Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0)) != null)
                        {
                            if(Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0)) != this)
                            {
                                AdjacentProvincesByColor.Add(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
                                break;
                            }
                            if(mainTex.GetPixel(x, y) == new Color32(0,0,0,0))
                            {
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
        // foreach (var item in GrabAdjacentProvinces())
        // {
        //     //Debug.LogError(item.name);
        // }
    }
    public List<Province> GrabAdjacentProvinces()
    {
        List<Province> adjacentprovinces = new List<Province>();
        foreach (var item in AdjacentProvincesByColor)
        {
            if(adjacentprovinces.Contains(Owners.Instance.CallProvinceByColor(item)))
            {
                continue;
            }
            if(Owners.Instance.CallProvinceByColor(item) == this)
            {
                continue;
            }
            adjacentprovinces.Add(Owners.Instance.CallProvinceByColor(item));
        }
        return adjacentprovinces;
    }
    public void AddPopulationGrowth(int growth)
    {
        popgrowth++;
        if(popgrowth > 100)
        {
            popgrowth -= 100;
            SpawnPop();
        }
        if(popgrowth < -100)
        {
            popgrowth += 100;
            KillPop();
        }
    }
    public void KillPop(string whoWeKilling = "")
    {
        foreach (var item in cultures)
        {
            if(item.name == whoWeKilling)
            {
                item.population -= 1;
                return;
            }
        }
        if(cultures.Count == 0)
        {
            return;
        }
        cultures[UnityEngine.Random.Range(0,cultures.Count)].population -= 1;
        Culture MarkedForDeath = null;
        foreach (var item in cultures)
        {
            if(item.population < 1)
            {
                MarkedForDeath = item;
            }
        }
        if(MarkedForDeath != null)
        {
            cultures.Remove(MarkedForDeath);
        }
    }
    public void SpawnPop(string whoWeSpawning = "")
    {
        if(whoWeSpawning == "")
        {
            if(UnityEngine.Random.Range(0,2) == 0)
            {
                foreach (var item in cultures)
                {
                    if(item.name == nation.PrimaryCulture.name)
                    {
                        item.population += 1;
                        return;
                    }
                }
                Culture newculture = nation.PrimaryCulture.GrabCulture();
                newculture.population = 1;
                cultures.Add(newculture);
            }
            else
            {
                cultures.OrderBy(x => x.population).ToList()[0].population += 1;
            }
        }
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
    public virtual int GrabMaxTroops()
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
            //location = new Vector2(location.x-993,location.y-440); //930,2234 1794
            location = new Vector2(position.x - Mapshower.Instance.Offset.x, position.y - Mapshower.Instance.Offset.y);
            tomato.transform.position = location;
            tomato.name = troops.ToString();
            tomato.transform.GetChild(0).GetChild(0).GetComponent<Text>().text = troops.ToString();
            tomato.transform.GetComponent<Image>().color = new Color32(nation.ownerIdentity.r, nation.ownerIdentity.g, nation.ownerIdentity.b, 255);

            GameObject.Destroy(tomato.GetComponent<ArmyMovement>());
            Drafty = tomato;
        }
        Drafty.transform.GetChild(0).GetChild(0).GetComponent<Text>().text = troops.ToString();
        Drafty.GetComponent<RectTransform>().localScale = new Vector2(Camera.main.orthographicSize/300,Camera.main.orthographicSize/300);
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
    public void ResetJobs()
    {
        //var a = Resources.Load<Buildings>("EcoTime/Farm");
        //BuildingList.Add(a);

        //Debug.Log(jobbies.Count);
        jobbies.Clear();
        foreach (var item in BuildingList)
        {
            foreach (var items in item.BuildingJobs)
            {
                jobbies.Add(items);
            }
        }
        foreach (var item in cultures)
        {
            item.ResetJobs(jobbies);
        }
    }
    public List<EcoData> GrabProvincialOutput()
    {
        var potato = new List<EcoData>();
        foreach (var items in cultures)
        {
            foreach (var item in items.GrabIncome())
            {
                if(potato.Find(x => x.resource == item.resource) != null)
                {
                    potato.Find(x => x.resource == item.resource).amount += item.amount;
                }
                else
                {
                    EcoData tomato = item.GrabEcoData();
                    potato.Add(tomato);
                }
            }
        }
        return potato;
    }
}
[System.Serializable]
public class Diplomacy
{
    public string othernation;
    public string relationship = "peace";//"peace";
}
[System.Serializable]
public class Nation
{
    public string name;
    public Color32 ownerIdentity;
    public bool IsPlayer;
    public bool IsAlive;
    public int manpower;
    public int treasury;
    public Culture PrimaryCulture;
    public List<Weapon> unlockedweapons;
    public List<Armor> unlockedarmor;
    public List<Regiment> regimentdesigns;
    public List<GameObject> armies;
    public List<EcoData> nationalTreasury = new List<EcoData>();
    public List<Diplomacy> Diplomacystuff = new List<Diplomacy>();
    public Faction faction;
    public List<ProvinceModifier> NationModifier = new List<ProvinceModifier>();
    public NationalBrain Brain;

    public bool CanIDoThis(string othernation, string goal = "movement")
    {
        string status = GrabDiplomaticStatus(othernation);
        if(status == "literallyme")
        {
            return true;
        }
        if(status == "peace")
        {
            //SetDiplomaticStatus(othernation,"war");
            


            //General_Manager.Instance.TriggerEvent("Declare War");
            return false;
        }
        if(status == "war")
        {
            return true;
        }
        return false;
    }
    public string SetDiplomaticStatus(string othernation, string newstatus = "peace")
    {
        var b = GrabDiplomaticStatus(othernation);
        var a = Diplomacystuff.Find(x => x.othernation == othernation);
        a.relationship = newstatus;
        return a.relationship;
    }
    public string GrabDiplomaticStatus(string othernation)
    {
        if(othernation == name)
        {
            return "literallyme";
        }
        var a = Diplomacystuff.Find(x => x.othernation == othernation);
        if(a == null)
        {
            var b = new Diplomacy();
            b.othernation = othernation;
            Diplomacystuff.Add(b);
            return b.relationship;
        }
        return a.relationship;
    }

    public void AddModifier(ProvinceModifier moddie)
    {
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            if(RPC.GetComponent<RpcTest>().IsLocalPlayer)
            {
                RPC.GetComponent<RpcTest>().AddNationModifierServerRpc(moddie.name, name);
            }
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
    public int GrabTroopDice(int troops)
    {
        int dice = 0;
        if(troops >= 10) //OrderTime
        {
            foreach (var item in NationModifier)
            {
                if(item == null)
                {
                    continue;
                }
                dice += item.OrderDice;
            }
        }
        if(troops < 10) //ChaosTime
        {
            foreach (var item in NationModifier)
            {
                if(item == null)
                {
                    continue;
                }
                dice += item.ChaosDice;
            }
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
// [System.Serializable]
// public class Trait
// {
//     public string traitname;
// }
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