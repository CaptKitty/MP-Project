using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Array2DEditor;

[System.Serializable]
[CreateAssetMenu(menuName = "Ability/HarvesterAbility")]
public class HarvesterAbility : Ability
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
        var potato = new HarvesterAbility();
        
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
    public override void Turn(CritterHolder critter)
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
                    if (!GeneralManager.Instance.ownermap.HasTile(target))
                    {
                        continue;
                    }
                    if(all == true)
                    {
                        if(GeneralManager.Instance.dicty[target] != null)
                        {
                            if(GeneralManager.Instance.dicty[target].GetComponent<CritterHolder>().IsThisViable(food))
                            {
                                DoTheThing(critter, target);
                            }
                        }
                        if(GeneralManager.Instance.tiledict[target] != null)
                        {
                            if(GeneralManager.Instance.tiledict[target].name == food)
                            {
                                GeneralManager.Instance.ChangeScore(score);
                                CityManager.Instance.AddResource(resource:output);
                            }
                        }
                    }
                    else
                    {
                        if(GeneralManager.Instance.dicty[target] != null)
                        {
                            if(GeneralManager.Instance.dicty[target].GetComponent<CritterHolder>().IsThisViable(food))
                            {
                                viabletargets.Add(target);
                            }
                            // if(GeneralManager.Instance.dicty[target].name == food)
                            // {
                                
                            // }
                        }
                        if(GeneralManager.Instance.tiledict[target] != null)
                        {
                            if(GeneralManager.Instance.tiledict[target].name == food)
                            {
                                viabletargets.Add(target);
                            }
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
                if(GeneralManager.Instance.dicty[target] != null)
                {
                    if(GeneralManager.Instance.dicty[target].name == food)
                    {
                        DoTheThing(critter, target);
                    }
                }
                if(GeneralManager.Instance.tiledict[target] != null)
                {
                    if(GeneralManager.Instance.tiledict[target].name == food)
                    {
                        GeneralManager.Instance.ChangeScore(score);
                        CityManager.Instance.AddResource(resource:output);
                    }
                }
            }
        }
    }
    public void DoTheThing(CritterHolder critter, Vector3Int target)
    {
        GeneralManager.Instance.ChangeScore(score);
        GeneralManager.Instance.dicty[target].GetComponent<CritterHolder>().ReducePopulation(damage);
        CityManager.Instance.AddResource(resource:output);
    }
}
