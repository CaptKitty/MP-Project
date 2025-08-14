using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectMilitaryCritter : MonoBehaviour
{
    private bool firsttime = true;
    public GameObject heldcritter;
    public CritterHolder NewCritter;
    public TestCritter NewTestCritter;
    public UnitSaveData unitSaveData;
    public bool CanPlay = false;
    public int minpagans = -1;
    public void Start()
    {
        if (firsttime)
        {
            firsttime = false;
            //heldcritter.GetComponent<CritterHolder>().Wakey();
            // NewCritter = heldcritter.GetComponent<CritterHolder>();
            // unitSaveData.NewCritterHolder(NewCritter);
            // //NewCritter.Wakey();
            // NewTestCritter = heldcritter.GetComponent<TestCritter>();
            // unitSaveData.NewTestCritter(NewTestCritter);
            transform.GetComponent<Image>().color = new Color32(255, 255, 255, 255);//new Color32(0,0,0,255);

            UpdateSprite();
        }
    }
    public void UpdateSprite()
    {
        unitSaveData.NewCritterHolder(heldcritter.GetComponent<CritterHolder>());
        heldcritter.name = unitSaveData.name;
        unitSaveData.NewTestCritter(heldcritter.GetComponent<TestCritter>());
        NewCritter = heldcritter.GetComponent<CritterHolder>();
        //Debug.LogError(unitSaveData.name);

        transform.GetChild(0).GetComponent<TestCritter>().faction = BattleManager1.Instance.Playerfaction;
        for (int i = 0; i < 5; i++)
        {
            try
            {
                transform.GetChild(0).GetChild(i).GetComponent<SpriteRenderer>().sprite = heldcritter.transform.GetChild(i).GetComponent<SpriteRenderer>().sprite;
                //transform.GetChild(0).GetChild(i).localPosition = heldcritter.transform.GetChild(i).position;
                transform.GetChild(0).GetChild(i).rotation = heldcritter.transform.GetChild(i).rotation;
            }
            catch { }
        }
        transform.GetChild(0).GetComponent<TestCritter>().Mercenary = unitSaveData.Mercenary;
        transform.GetChild(0).GetComponent<TestCritter>().color = unitSaveData.color;
        transform.GetChild(0).GetComponent<TestCritter>().color2 = unitSaveData.color2;
        transform.GetChild(0).GetComponent<TestCritter>().color3 = unitSaveData.color3;

        transform.GetChild(0).GetComponent<TestCritter>().Start();
        transform.GetChild(1).GetComponent<Text>().text = unitSaveData.name + "    " + unitSaveData.cost + " cost";
        //transform.GetChild(2).GetComponent<Text>().text = NewCritter.GrabHealth().ToString() + " Health";
        
        // var f = Math.Round((NewCritter.GrabAttack() / NewCritter.GrabAttackTime()));
        // transform.GetChild(3).GetComponent<Text>().text = f.ToString() + " DPS";
        // var a = NewCritter.AIScript;
        // // if(a.GetType() == typeof(basic_Ranged_AI_script))
        // // {
        // //     var b = (basic_Ranged_AI_script)a;
        // //     transform.GetChild(3).GetComponent<Text>().text += "    " + b.ammo + "x " + b.modifier.base_attack * b.modifier.base_attacktime + " DPS";
        // // }
        // if (a.GetType() == typeof(basic_Ranged_AI_script_ammo))
        // {
        //     var b = (basic_Ranged_AI_script_ammo)a;
        //     transform.GetChild(3).GetComponent<Text>().text += "    " + b.ammo + "x " + b.modifier.base_attack + " Damage";
        // }
        // if (a.GetType() == typeof(basic_Skirmish_Ranged_AI_script_ammo))
        // {
        //     var b = (basic_Skirmish_Ranged_AI_script_ammo)a;
        //     transform.GetChild(3).GetComponent<Text>().text += "    " + b.ammo + "x " + b.modifier.base_attack + " Damage";
        // }
    }
    public void UpdateMinPagans(int a)
    {
        if (a > minpagans)
        {
            CanPlay = true;
            transform.GetComponent<Image>().color = new Color32(82, 82, 82, 255);
        }
    }
    public void OnMouseDown()
    {


        if (firsttime)
        {
            firsttime = false;
            NewCritter.Wakey();
        }
        // if(heldcritter == GeneralManager.Instance.SelectedCritter)
        // {
        //     NewCritter.AbilityList.Add(Resources.Load<DeathAbility>("Forages/DeathOfADoge"));
        // }
        // if(GeneralManager.Instance)
        // {
        //     GeneralManager.Instance.highlight = true;
        //     GeneralManager.Instance.delete = false;
        //     GeneralManager.Instance.SelectedCritter = heldcritter;
        // }
        // if(BattleManager1.Instance)
        // {
        BattleManager1.Instance.SelectedCritter = heldcritter;
        //     //Debug.LogError(heldcritter.name);
        //     BattleManager1.Instance.MousePet.GetComponent<SpriteRenderer>().sprite = this.transform.GetChild(0).gameObject.GetComponent<Image>().sprite;
        // }
        //Debug.LogError(heldcritter.name);
    }
    public void OnMouseOver()
    {
        var a = NewCritter;
        string texty = "";

        texty += "Name: " + a.name;
        texty += "\nCost: " + a.cost.name + ":" + (a.cost.amount / 10);

        foreach (var item in a.AbilityList)
        {
            if (item == null)
            {
                continue;
            }
            texty += "\n";
            texty += "\n" + item.Description;
            //texty += "\n" + item.Description; 
        }

        if (DescriptionManager.Instance)
        {
            DescriptionManager.Instance.UpdateDescriptionTo(texty);
        }

    }
    public void OnMouseEnter()
    {
        //UnitStatDisplayMenu.Instance.LoadNewUnit(NewCritter);
        UnitStatDisplayMenu.Instance.LoadNewUnit(NewCritter); //transform.GetChild(0).GetComponent<CritterHolder>());
    }
}
