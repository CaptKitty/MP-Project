using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Array2DEditor;

[System.Serializable]
[CreateAssetMenu(menuName = "Ability/CutterAbility")]
public class CutterAbility : Ability
{
    //public string food = "grass";
    public int score = 1;
    public int damage = 1;
    public string input = "null";
    [SerializeField]
    public Resource output;
    public bool all = true;
    public int amount = 0;
    public Array2DBool InputarrayBool;

    public override Ability Init()
    {
        var potato = new CutterAbility();
        
        base.Init();

        potato.name = name;
        potato.food = food;
        potato.damage = damage;
        potato.score = score;
        potato.arrayBool = arrayBool;
        potato.output = output;
        potato.all = all;
        potato.amount = amount;
        potato.Description = Description;
        return potato;
    }
    public override void Execute(CritterHolder critter)
    {
        //Debug.Log(this.name);
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
                    if(!BattleManager1.Instance)
                    {
                        return;
                    }
                    if (!BattleManager1.Instance.ownermap.HasTile(target))
                    {
                        continue;
                    }
                    if(all == true)
                    {
                        if(BattleManager1.Instance.dicty[target] != null)
                        {
                            if(BattleManager1.Instance.dicty[target].GetComponent<CritterHolder>().IsThisViable(food))
                            {
                                DoTheThing(critter, target);
                            }
                        }
                    //     if(GeneralManager.Instance.tiledict[target] != null)
                    //     {
                    //         if(GeneralManager.Instance.tiledict[target].name == food)
                    //         {
                    //             GeneralManager.Instance.ChangeScore(score);
                    //             CityManager.Instance.AddResource(resource:output);
                    //         }
                    //     }
                    // }
                    // else
                    // {
                    //     if(dicty[target] != null)
                    //     {
                    //         if(dicty[target].GetComponent<CritterHolder>().IsThisViable(food))
                    //         {
                    //             viabletargets.Add(target);
                    //         }
                    //         // if(dicty[target].name == food)
                    //         // {
                                
                    //         // }
                    //     }
                    //     if(GeneralManager.Instance.tiledict[target] != null)
                    //     {
                    //         if(GeneralManager.Instance.tiledict[target].name == food)
                    //         {
                    //             viabletargets.Add(target);
                    //         }
                    //     }
                    }
                }
            }
        }
    }
    public void DoTheThing(CritterHolder critter, Vector3Int target)
    {
        BattleManager1.Instance.dicty[target].GetComponent<CritterHolder>().ReducePopulation(damage);
        GeneralManager.Instance.ChangeScore(score);
        BattleManager1.Instance.ChangeScore(score);
    }
}
