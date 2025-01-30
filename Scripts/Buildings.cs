using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Buildings/Base")]
public class Buildings : ScriptableObject
{
    public string name = "";
    public BaseTrigger trigger;
    public List<EcoData> Cost = new List<EcoData>();
    public List<EcoData> input = new List<EcoData>();
    public List<EcoData> output = new List<EcoData>();
    public List<Jobs> BuildingJobs = new List<Jobs>();
    public Buildings GrabBuildings()
    {
        Buildings cult = new Buildings();
        cult.trigger = trigger;
        cult.Cost = Cost;
        cult.input = input;
        cult.output = output;
        cult.BuildingJobs = BuildingJobs;
        return cult;
    }
}