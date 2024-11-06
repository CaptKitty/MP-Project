using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Array2DEditor;

[System.Serializable]
[CreateAssetMenu(menuName = "Ability/SpreadFaithAbility")]
public class SpreadFaithAbility : Ability
{
    public int score = 1;
    public int damage = 1;
    public Resource input;
    [SerializeField]
    public Resource output;
    public bool all = true;
    public int amount = 0;
    public Array2DBool InputarrayBool;

    public override Ability Init()
    {
        var potato = new SpreadFaithAbility();
        
        base.Init();

        potato.name = name;
        potato.food = food;
        potato.damage = damage;
        potato.score = score;
        potato.arrayBool = arrayBool;
        potato.input = input;
        potato.output = output;
        potato.all = all;
        potato.amount = amount;
        potato.Description = Description;
        return potato;
    }
    public override void Turn(CritterHolder critter)
    {
        bool foodistrue = false;
        foreach (var item in CityManager.Instance.ResourceList)
        {
            if(item.name == input.name)
            {
                if(item.amount >= input.amount)
                {
                    CityManager.Instance.AddResource(resource:input);
                    foodistrue = true;
                }
            }
        }
        if(foodistrue)
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
                        if (!GlobalManager.Instance.ownermap.HasTile(target))
                        {
                            continue;
                        }
                        DoTheThing(critter, target);
                    }
                }
            }
        }
    }
    public void DoTheThing(CritterHolder critter, Vector3Int target)
    {
        float dammy = (float)(damage) / 100;
        if(GlobalManager.Instance.faithamount[target] < 1.01f)
        {
            GlobalManager.Instance.faithamount[target] += dammy;
        }
    }
}
