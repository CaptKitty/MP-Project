using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Array2DEditor;

[System.Serializable]
[CreateAssetMenu(menuName = "Ability/ProcessAbility")]
public class ProcessAbility : Ability
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
        var potato = new ProcessAbility();
        
        base.Init();

        potato.name = name;
        potato.food = food;
        potato.damage = damage;
        potato.input = input;
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
        bool foodistrue = false;
        foreach (var item in CityManager.Instance.ResourceList)
        {
            if(item.name == input.name)
            {
                if(item.amount >= -input.amount)
                {
                    CityManager.Instance.AddResource(resource:input);
                    foodistrue = true;
                }
            }
        }
        if(foodistrue)
        {
            DoTheThing();
        }
    }
    public void DoTheThing()
    {
        CityManager.Instance.AddResource(resource:output);
    }
}
