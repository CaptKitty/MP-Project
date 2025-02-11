using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Trait/NaturalJobs")]
public class NaturalJobs : Trait
{
    public List<string> jobnames;
    public List<EcoData> EcoModifier;
    public override Trait GrabTrait()
    {
        NaturalJobs traits = new NaturalJobs();
        traits.name = name;
        traits.jobnames = jobnames;
        traits.EcoModifier = EcoModifier;
        return traits;
    }
    public override List<EcoData> GrabOutput(Jobs jobs)
    {
        List<EcoData> returndata = new List<EcoData>();
        foreach (var item in jobnames)
        {
            if(item == jobs.name)
            {
                foreach (var items in EcoModifier)
                {
                    returndata.Add(items);
                }
            }        
        }
        return returndata;
    }
}