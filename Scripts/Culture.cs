using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Serializable]
[CreateAssetMenu(fileName = "Culture/Base")]
public class Culture : ScriptableObject
{
    public string name = "";
    public Color32 ownerIdentity = new Color32(0,0,0,255);
    public int population = 1;
    public List<Jobs> jobs = new List<Jobs>();
    public List<Trait> TraitList = new List<Trait>();
    public Culture GrabCulture()
    {
        Culture cult = new Culture();
        cult.name = name;
        cult.ownerIdentity = ownerIdentity;
        cult.population = population;
        cult.jobs = jobs;
        cult.TraitList = TraitList;
        return cult;
    }
    public List<EcoData> GrabIncome(State state = null, Province province = null)
    {
        var potato = new List<EcoData>();
        jobs = jobs.OrderBy(x => x.Jobpriority).ToList();
        foreach (var items in jobs)
        {
            foreach (var item in items.GrabJobData(state,province))
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
            //Debug.LogError(TraitList.Count);
            foreach (var item in TraitList)
            {
                foreach (var itemss in item.GrabOutput(items))
                {
                    if(potato.Find(x => x.resource == itemss.resource) != null)
                    {
                        potato.Find(x => x.resource == itemss.resource).amount += itemss.amount;
                    }
                    else
                    {
                        EcoData tomato = itemss.GrabEcoData();
                        potato.Add(tomato);
                    }
                    
                }
                
            }
        }
        return potato;
    }
    public void ResetJobs(List<Jobs> potato)
    {
        jobs.Clear();
        var a = Resources.Load<Jobs>("EcoTime/Jobs/Peasant");
        for (int i = 0; i < population; i++)
        {
            if(potato.Count > 0)
            {
                jobs.Add(potato[0]);
                potato.RemoveAt(0);
            }
            else
            {
                jobs.Add(Instantiate(a));
            }
        }
    }
}