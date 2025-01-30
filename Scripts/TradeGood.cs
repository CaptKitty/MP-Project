using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "TradeGood/Base")]
public class TradeGood : ScriptableObject
{
    public string name;
    public int amount;
    public Sprite sprite;
    public Object GrabResource()
    {
        TradeGood cult = new TradeGood();
        cult.name = name;
        cult.amount = amount;
        cult.sprite = sprite;
        return cult;
    }
}