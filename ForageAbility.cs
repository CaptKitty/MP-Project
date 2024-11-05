using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Array2DEditor;

[System.Serializable]
[CreateAssetMenu(menuName = "Ability/ForageAbility")]
public class ForageAbility : Ability
{
    //public string food = "grass";
    public int score = 1;
    public List<Vector3Int> targetlists = new List<Vector3Int>();

    public override Ability Init()
    {
        var potato = new ForageAbility();
        
        base.Init();

        potato.name = name;
        potato.food = food;
        potato.score = score;
        potato.Description = Description;
        potato.arrayBool = arrayBool;
        return potato;
    }
    public override void Execute(CritterHolder critter)
    {
        Debug.Log(this.name);
        Vector2Int vector = arrayBool.GridSize;
        Vector3Int spot = critter.spot;
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
                    if(GeneralManager.Instance.dicty[target] != null)
                    {
                        if(GeneralManager.Instance.dicty[target].name == food)
                        {
                            GeneralManager.Instance.ChangeScore(score);
                            GeneralManager.Instance.dicty[target].GetComponent<CritterHolder>().ReducePopulation(1);
                            //viabletargets.Add(target);
                        }
                    }
                    //GeneralManager.Instance.Spawn(new Vector3Int(spot.x+x,spot.y+y,0),name:"grass");
                }
            }
        }
    }
}
