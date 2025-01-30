using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Jobs/Base")]
public class Jobs : ScriptableObject
{
    public string name;
    public BaseTrigger trigger;
    public SocialClass socialClass;
    public List<EcoData> input;// = new List<EcoData>();
    public List<EcoData> output;// = new List<EcoData>();
    public int Jobpriority = 0;
    public Jobs GrabJobs()
    {
        Jobs cult = new Jobs();
        cult.trigger = trigger;
        cult.socialClass = socialClass;
        cult.input = input;
        cult.output = output;
        cult.Jobpriority = Jobpriority;
        return cult;
    }
    public List<EcoData> GrabJobData(State state = null, Province province = null)
    {
        var potato = new List<EcoData>();
        List<EcoData> _output = new List<EcoData>();
        foreach (var item in output)
        {
            _output.Add(item.GrabEcoData());
        }
        if(state != null)
        {
            state.GrabJobModifier(name, _output);
        }
        // if(province != null)
        // {
        //     province.GrabJobModifier(name, _output);
        // }

        foreach (var item in _output)
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
        foreach (var item in input)
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
        if(potato.Find(x => x.resource == socialClass.consumption.resource) != null)
        {
            potato.Find(x => x.resource == socialClass.consumption.resource).amount += socialClass.consumption.amount;
        }
        else
        {
            EcoData tomato = socialClass.consumption.GrabEcoData();
            potato.Add(tomato);
        }
        return potato;
    }
    public bool CanWeProduce()
    {
        if(input.Count == 0)
        {
            return true;
        }
        return true;
    }
}