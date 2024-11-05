using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Array2DEditor;

public class Ability : ScriptableObject
{
    [SerializeField]
    public Array2DBool arrayBool;
    public string Description;
    public string food;
    public virtual Ability Init()
    {
        var potato = new Ability();
        potato.name = name;
        potato.arrayBool = arrayBool;
        potato.Description = Description;
        potato.food = food;
        return potato;
    }
    public virtual void Turn(CritterHolder critter)
    {

    }
    public virtual void Execute(CritterHolder critter)
    {

    }
    public virtual void OnDeath(CritterHolder critter)
    {

    }
}
