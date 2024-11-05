using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Ability/SpawnAbility")]
public class SpawnAbility : Ability
{
    public List<Vector3Int> targetlists = new List<Vector3Int>();

    public bool all = false;
    public int amount = 1;
    public int chance = 10;
    public string whatToSpawn = "";
    public List<string> ViablePlacingSpots = new List<string>();

    public override Ability Init()
    {
        var potato = new SpawnAbility();
        
        base.Init();

        potato.name = name;
        potato.arrayBool = arrayBool;
        potato.all = all;
        potato.amount = amount;
        potato.Description = Description;
        potato.chance = chance;
        potato.whatToSpawn = whatToSpawn;
        potato.ViablePlacingSpots = ViablePlacingSpots;
        return potato;
    }

    public override void Turn(CritterHolder critter)
    {
        Vector2Int vector = arrayBool.GridSize;
        Vector3Int spot = critter.spot;
        List<Vector3Int> viabletargets = new List<Vector3Int>();
        for (int x = -(vector.x-1)/2; x <= (vector.y)/2; x++)
        {
            for (int y = -(vector.y-1)/2; y <= (vector.y)/2; y++)
            {
                if(arrayBool.GetCell((x+(vector.x)/2),(y+(vector.y)/2)))
                {
                    Vector3Int target = new Vector3Int(spot.x + x, spot.y + y, 0);
                    if (!GeneralManager.Instance.ownermap.HasTile(target))
                    {
                        continue;
                    }
                    bool testa = true;
                    foreach (var item in ViablePlacingSpots)
                    {
                        if(GeneralManager.Instance.tiledict[target].name == item)
                        {
                            testa = false;
                        }
                        if(GeneralManager.Instance.dicty[target] != null && GeneralManager.Instance.dicty[target].name == item)
                        {
                            testa = false;
                        }
                    }
                    if(testa == true)
                    {
                        continue;
                    }
                    if(all == true)
                    {
                        if(GeneralManager.Instance.dicty[target] == null)
                        {
                            DoTheThing(critter, target);
                        }
                    }
                    else
                    {
                        if(GeneralManager.Instance.dicty[target] == null || GeneralManager.Instance.dicty[target].GetComponent<CritterHolder>().CanBeOverwritten && critter.name != "Tree")
                        {
                            viabletargets.Add(target);
                        }
                    }
                }
            }
        }
        if(all == false)
        {
            for (int i = 0; i < amount; i++)
            {
                if(viabletargets.Count == 0)
                {
                    return;
                }
                var target = viabletargets[Random.Range(0,viabletargets.Count)];
                if(target == null)
                {
                    continue;
                }
                DoTheThing(critter, target);
            }
        }
    }
    public void DoTheThing(CritterHolder critter, Vector3Int target)
    {
        if(Random.Range(1,101) <= chance)
        {
            if(whatToSpawn == "")
            {
                GeneralManager.Instance.Spawn(target, name: critter.name);
            }
            else
            {
                GeneralManager.Instance.Spawn(target, name: whatToSpawn);
            }
        }
    }
}
