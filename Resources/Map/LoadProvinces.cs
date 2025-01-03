using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Linq;
using System.Text;

public class LoadProvinces : MonoBehaviour
{
    public List<Province> provincelist;
    
    public void LoadStuff()
    {



        LoadinProvinces();
        Owners.Instance.provincelist.Clear();
        Owners.Instance.provincelist = provincelist;
        AddStates();
        LoadProvincesinStates();
    }

    public void LoadinCultures()
    {
        var txtarray = Resources.LoadAll("culturedatas", typeof(TextAsset));
        
        // using var sr = new StringReader(txt.text);
        int count = 0;  
        string Lines;
        int i = 0;
        foreach (TextAsset item in txtarray)
        {
            using var sr = new StringReader(item.text);
            string line = "potato";
            Culture culture = new Culture();
            while (line != null)
            {
                // Debug.Log(line);
                if(line.Contains("Name"))
                {
                    line = sr.ReadLine();
                    culture.name = line.Trim( );
                    // Debug.Log(provincename);
                }
                if(line.Contains("Color"))
                {
                    line = sr.ReadLine();
                    byte red = byte.Parse(line);
                    byte green = byte.Parse(sr.ReadLine());
                    byte blue = byte.Parse(sr.ReadLine());
                    culture.ownerIdentity = new Color32(red,green,blue,0);
                    // Debug.Log(color);
                }
                line = sr.ReadLine();
            }
            Owners.Instance.culturelist.Add(culture);
        }
    }

    void LoadinProvinces()
    {
        TextAsset txt = (TextAsset)Resources.Load("provincedata/Amsterdam", typeof(TextAsset));
        var txtarray = Resources.LoadAll("provincedatas", typeof(TextAsset));

        // var txtarray = new List<TextAsset>();
        // if(1==1)
        // {
        //     txtarray.Clear();
        //     var info = new DirectoryInfo(Application.streamingAssetsPath+ "/provincedatas");
        //     var fileInfo = info.GetFiles();
        //     foreach(FileInfo item in fileInfo)
        //     {
        //         if (item.Exists)
        //         {
        //             // Read the file contents
        //             string fileContent = File.ReadAllText(item.FullName);

        //             // Create a new TextAsset from the file contents
        //             TextAsset textAsset = new TextAsset(fileContent);
        //             txtarray.Add(textAsset);
        //         }   
        //     }
        // }
        
        
        // using var sr = new StringReader(txt.text);
        int count = 0;  
        string Lines;
        int i = 0;
        foreach (TextAsset item in txtarray)
        {
            // Debug.Log(item);
            using var sr = new StringReader(item.text);
            string line = "potato";

            string provincename = "Error";
            Color32 color = new Color32(0,0,0,0);
            Vector2 location = new Vector2(0,0);
            int population = 1;
            Nation nation = new Nation();
            Province newprovince = new Province();
            newprovince.cultures = new List<Culture>();
            Culture culture = new Culture();
            while (line != null)
            {
                // Debug.Log(line);
                if(line.Contains("Name"))
                {
                    line = sr.ReadLine();
                    provincename = line.Trim( );
                    // Debug.Log(provincename);
                }
                if(line.Contains("Color"))
                {
                    line = sr.ReadLine();
                    byte red = byte.Parse(line);
                    byte green = byte.Parse(sr.ReadLine());
                    byte blue = byte.Parse(sr.ReadLine());
                    color = new Color32(red,green,blue,0);
                    // Debug.Log(color);
                }
                if(line.Contains("Location"))
                {
                    line = sr.ReadLine();
                    int x = int.Parse(line);
                    int y = int.Parse(sr.ReadLine());
                    location = new Vector2(x,y);
                    // Debug.Log(location);
                }
                // if(line.Contains("Population"))
                // {
                //     line = sr.ReadLine();
                //     population = int.Parse(line);
                //     culture = new Culture();
                //     culture.population = population;
                //     line = sr.ReadLine();
                //     if(line == "}")
                //     {
                //         //line = "Dutch";
                //         line = "None";
                //     }
                //     culture.name = line;
                //     culture.ownerIdentity = Owners.Instance.CallCultureByName(line).ownerIdentity;
                //     culture.name = Owners.Instance.CallCultureByName(line).name;
                //     newprovince.cultures.Add(culture);
                    
                //     // Debug.Log(population);
                // }
                if(line.Contains("Owner"))
                {
                    line = sr.ReadLine();
                    line = sr.ReadLine();
                    newprovince.nation = GetNation(line.Trim( ));
                    // Debug.Log(nation);
                }
                line = sr.ReadLine();
            }
            if(culture.name == "None")
            {
                if(newprovince.nation.name == "France")
                {
                    culture.name = "French";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Spain")
                {
                    culture.name = "Spanish";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Portugal")
                {
                    culture.name = "Portuguese";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Netherlands")
                {
                    culture.name = "Dutch";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
            }
            
            newprovince.name = provincename;
            newprovince.identity = color;
            newprovince.position = location;
            if(newprovince.cultures.Count == 0)
            {
                culture.ownerIdentity = newprovince.nation.ownerIdentity;
                culture.population = 1000;
                culture.name = newprovince.nation.name;
                newprovince.cultures.Add(culture);
            }
            newprovince.UpdatePopulation();
            // newprovince.population = population;
            
            // newprovince.nation = nation;

            provincelist.Add(newprovince);
        }        
    }
    void LoadProvincesinStates()
    {
  
        foreach (Province province in Owners.Instance.provincelist)
        {
            foreach (State state in Owners.Instance.statelist)
            {
                if(state.nation.name == province.nation.name)
                {
                    if(state.stateIdentity == new Color(0,0,0,0))
                    {
                        state.stateIdentity = province.identity;
                    }
                    state.provincelist.Add(province);
                    province.state = state.name;
                }
            }
        }
    }
    void AddStates()
    {
        foreach (Nation nation in Owners.Instance.nationlist)
        {
            State state = new State();
            state.name = nation.name;
            state.nation = nation;
            state.taxpercentage = 10;
            state.levypercentage = 10;
            state.stateIdentity = new Color32(0,0,0,0);
            state.provincelist = new List<Province>();
            Owners.Instance.statelist.Add(state);
        }
    }

    Nation GetNation(string name)
    {
        foreach (Nation nation in Owners.Instance.nationlist)
        {
            // Debug.Log(name + " + " + nation.name);
            if(name == nation.name)
            {
                // Debug.Log(nation.ownerIdentity);
                return nation;
            }
        }
        return new Nation();
    }

    void LoadBasePopulation()
    {
        
    }
}
