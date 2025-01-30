using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BuildingTab : MonoBehaviour
{
    public static BuildingTab Instance;
    public TMP_Dropdown m_Dropdown;
    public List<Buildings> BuildingList = new List<Buildings>();
    public Province provinces;
    public GameObject prefab;
    public List<GameObject> loadedPrefabs = new List<GameObject>();
    // Start is called before the first frame update
    void Awake()
    {
        if(BuildingTab.Instance == null)
        {
            Instance = this;
        }
    }
    public void GrabBuildings(Province province)
    {
        BuildingList.Clear();
        var a = Resources.LoadAll<Buildings>("EcoTime/Buildings/");
        foreach (var item in a)
        {
            // var b = (Buildings)item;
            // if(b != null)
            // {
               BuildingList.Add(item);
            // }
        }
        //List<string> list = new List<string> { "option1", "option2" };
        //var dropdown = GetComponent<Dropdown>();
        m_Dropdown.options.Clear();
        foreach (var option in BuildingList)
        {
            m_Dropdown.options.Add(new TMP_Dropdown.OptionData(option.name));
        }
        m_Dropdown.RefreshShownValue();

        provinces = province;
        m_Dropdown.SetValueWithoutNotify(0);

        // m_Dropdown.m_Options.Clear();
        // foreach (var item in BuildingList)
        // {
        //     var c = new DropdownItem();
        //     c.m_Text = item.name;
        //     m_Dropdown.m_Options.Add(c);
        // }
        LoadBuildingsInCity(province);
    }
    public void LoadBuildingsInCity(Province province)
    {
        foreach (var item in loadedPrefabs)
        {
            Destroy(item);
        }
        loadedPrefabs.Clear();
        foreach (var item in province.BuildingList)
        {
            var preffy = Instantiate(prefab, this.transform);
            preffy.transform.localPosition = new Vector2(-50,80-(80*loadedPrefabs.Count));
            preffy.GetComponent<BuildingTabSubject>().SetBuilding(item);
            loadedPrefabs.Add(preffy);
        }
        UIElement.NationHost.UpdateFourth(Mapshower.Instance.GrabStateStuff(province.state));
        m_Dropdown.SetValueWithoutNotify(0);
    }
    void Start()
    {
        //Fetch the Dropdown GameObject
        //m_Dropdown = GetComponent<TMP_Dropdown>();
        //Add listener for when the value of the Dropdown changes, to take action
        m_Dropdown.onValueChanged.AddListener(delegate {
                DropdownValueChanged(m_Dropdown);
            });

        //Initialize the Text to say the first value of the Dropdown
        //Debug.Log("First Value : " + m_Dropdown.value);
    }

    //Output the new value of the Dropdown into Text
    void DropdownValueChanged(TMP_Dropdown change)
    {
        Debug.Log("New Value : " + change.value);
        Debug.Log(BuildingList[change.value].name);
        if(change.value != 0)
        {
            var a = Owners.Instance.nationlist.Find(x => x.name == provinces.nation.name);
            bool canbuy = true;
            foreach (var item in BuildingList[change.value].Cost)
            {
                if(a.nationalTreasury.Find(x => x.resource == item.resource) != null)
                {
                    if(a.nationalTreasury.Find(x => x.resource == item.resource).amount < item.amount)
                    {
                        canbuy = false;
                    }
                }
                else
                {
                    canbuy = false;
                }
            }
            if(canbuy)
            {
                provinces.BuildingList.Add(BuildingList[change.value]);
                foreach (var item in BuildingList[change.value].Cost)
                {
                    if(a.nationalTreasury.Find(x => x.resource == item.resource) != null)
                    {
                        a.nationalTreasury.Find(x => x.resource == item.resource).amount -= item.amount;
                        
                    }
                    else
                    {
                        canbuy = false;
                    }
                }
            }
            LoadBuildingsInCity(provinces);
        }
    }
}
