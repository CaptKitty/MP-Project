using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseActions/SpawnCritters")]
public class SpawnCritters : GAction
{
    public Vector3Int Corespot;
    public GameObject unitToSpawn;
    public UnitSaveData unitDataToSpawn;
    public override bool IsAchievable()
    {

        Corespot = brainy.PrimaryLineSpot;

        var a = SessionManager.Instance.ClientFaction.UnitList;
        //unitToSpawn = a[Random.Range(0, a.Count)];

        var b = SessionManager.Instance.ClientFaction.UnitDataList;
        if (SessionManager.Instance.savedArmy != null)
        {
            b.Clear();
            foreach (var item in SessionManager.Instance.savedArmy.fieldArmy.USDReserves)
            {
                for (int i = 0; i < item.amount; i++)
                {
                    b.Add(item.USD);
                }
            }
        }
        if (b.Count == 0)
        {
            Debug.Log("Out of Units M'Lord");
            return false;
        }

        var c = b[Random.Range(0, b.Count)];
        c.NewCritterHolder(unitToSpawn.GetComponent<CritterHolder>());
        unitToSpawn.gameObject.name = c.name;

        if (unitToSpawn.GetComponent<CritterHolder>().unittype == UnitTypes.Ranged)
        {
            Corespot = brainy.SecondaryLineSpot;
        }
        if (unitToSpawn.GetComponent<CritterHolder>().unittype == UnitTypes.LightCavalry)
        {
            Corespot = brainy.TertiaryLineSpotA;
            if (brainy.k % 2 == 0)
            {
                Corespot = brainy.TertiaryLineSpotB;
            }
        }


        if (brainy.resources <= unitToSpawn.GetComponent<CritterHolder>().cost.amount)
        {
            return false;
        }
        brainy.resources -= unitToSpawn.GetComponent<CritterHolder>().cost.amount;

        return true;
    }
    public override bool Execute()
    {
        var a = unitToSpawn.GetComponent<CritterHolder>().unittype;
        

        SpawnBait unit = new SpawnBait();
        
        switch (a)
        {
            case UnitTypes.Ranged:
                unit.target = new Vector3Int(Corespot.x+brainy.j, Corespot.y+brainy.j,0);
                brainy.j++;
                break;
            case UnitTypes.LightCavalry:
                unit.target = new Vector3Int(Corespot.x+(brainy.k/2), Corespot.y+(brainy.k/2),0);
                brainy.k++;
                break;
            default:
                unit.target = new Vector3Int(Corespot.x+brainy.i, Corespot.y+brainy.i,0);
                brainy.i++;
                break;
        }
        
        

        unit.name = unitToSpawn.gameObject.name;
        unit.AIorNot = false;
        unit.ClientOrHost = "Client";


        //var unit = _ClientArmy[i];
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().Spawn(unit.target, unit.name, unit.AIorNot, unit.name + "" + brainy.i.ToString() + "" + brainy.j.ToString() +  "" + brainy.k.ToString(), unit.ClientOrHost);
        }
        return true;
    }
    
}