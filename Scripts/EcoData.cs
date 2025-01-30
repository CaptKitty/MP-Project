using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EcoData
{
    public TradeGood resource;
    public float amount;
    public EcoData GrabEcoData()
    {
        var potato =  new EcoData();
        potato.resource = resource;
        potato.amount = amount;
        return potato; 
    }
}
