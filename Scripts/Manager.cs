using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Manager : MonoBehaviour
{
    public List<Critter> critterlist = new List<Critter>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown("space"))
        {
            Potatoes();
        }
    }
    public void Potatoes()
    {
        Critter Predator = null;
        Critter Prey = null;
        foreach (var item in critterlist)
        {
            if(item.name == "Goat")
            {
                Predator = item;
            }
            if(item.name == "Grass")
            {
                Prey = item;
            }
        }
        for (int i = 0; i < Predator.DetectionSkill.amount; i++)
        {
            DetectionCycle(Predator, Prey);
        }
        
        Predator.nutrition -= Predator.consumption;
        print(Predator.nutrition);
    }
    public void DetectionCycle(Critter Predator, Critter Prey)
    {   
        if(Predator.DetectionSkill.RollAmount() > Prey.EvasionSkill.RollAmount() + Prey.Density())
        {
            HuntingCycle(Predator, Prey);
            InjuredCycle(Predator, Prey);
        }
    }
    public void HuntingCycle(Critter Predator, Critter Prey)
    {
        if(Predator.AttackSkill.RollAmount() > Prey.DefenceSkill.RollAmount())
        {
            FeedingCycle(Predator, Prey);
            Prey.population -= Predator.FeedAmount;
        }
    }
    public void InjuredCycle(Critter Predator, Critter Prey)
    {
        if(Prey.AttackSkill.RollAmount() > Predator.AttackSkill.RollAmount())
        {
            Predator.population -= 1;
        }
    }
    public void FeedingCycle(Critter Predator, Critter Prey)
    {
        Predator.nutrition += Predator.FeedAmount * Prey.NutritionAmount;
    }
}
[System.Serializable]
public class Critter
{
    public string name;
    public string type;
    public int population = 10;
    public Skill DetectionSkill;
    public Skill EvasionSkill;
    public Skill AttackSkill;
    public Skill DefenceSkill;
    public int FeedAmount;
    public int NutritionAmount;
    public int nutrition = 0;
    public int consumption;
    public int Density()
    {
        double density = population / (population+200);
        int density2 = (int)density * 3;
        Debug.Log(density2);
        return 1;//density2;
    }
}
[System.Serializable]
public class Skill
{
    public string name;
    public int amount;
    public Dice dice = new Dice();
    public int RollAmount()
    {
        return Random.Range(dice.min,dice.max+1);
    }
}
[System.Serializable]
public class Dice
{
    public int min = 1;
    public int max = 6;
    public int RollAmount()
    {
        return Random.Range(min,max+1);
    }
}