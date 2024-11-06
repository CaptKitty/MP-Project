using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickFactionButton : MonoBehaviour
{
    public string Faction;
    public void OnMouseDown()
    {
        BattleManager1.Instance.ChangeFaction(Faction);
    }
}
