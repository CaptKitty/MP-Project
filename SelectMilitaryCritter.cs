using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectMilitaryCritter : MonoBehaviour
{
    private bool firsttime = true;
    public GameObject heldcritter;
    public bool CanPlay = false;
    public int minpagans = -1;
    public void Start()
    {
        if(firsttime)
        {
            firsttime = false;
            heldcritter.GetComponent<CritterHolder>().Wakey();
            transform.GetComponent<Image>().color = new Color32(255,255,255,255);//new Color32(0,0,0,255);

            UpdateSprite();
        }
    }
    public void UpdateSprite()
    {
        transform.GetChild(0).GetComponent<TestCritter>().faction = BattleManager1.Instance.Playerfaction;
        for (int i = 0; i < 5 ; i++)
        {
            try
            {
                transform.GetChild(0).GetChild(i).GetComponent<SpriteRenderer>().sprite = heldcritter.transform.GetChild(i).GetComponent<SpriteRenderer>().sprite;
                transform.GetChild(0).GetChild(i).localPosition = heldcritter.transform.GetChild(i).position;
                transform.GetChild(0).GetChild(i).rotation = heldcritter.transform.GetChild(i).rotation;
            }
            catch{}
        }
        transform.GetChild(0).GetComponent<TestCritter>().Mercenary = heldcritter.GetComponent<TestCritter>().Mercenary;
        transform.GetChild(0).GetComponent<TestCritter>().color  = heldcritter.GetComponent<TestCritter>().color;
        transform.GetChild(0).GetComponent<TestCritter>().color2 = heldcritter.GetComponent<TestCritter>().color2;
        transform.GetChild(0).GetComponent<TestCritter>().color3 = heldcritter.GetComponent<TestCritter>().color3;
        
        transform.GetChild(0).GetComponent<TestCritter>().Start();
        transform.GetChild(1).GetComponent<Text>().text = heldcritter.name + "    " + heldcritter.GetComponent<CritterHolder>().cost.amount + " cost";
        transform.GetChild(2).GetComponent<Text>().text = heldcritter.GetComponent<CritterHolder>().population.ToString() + " Health";
        var f = Math.Round((heldcritter.GetComponent<CritterHolder>().GrabAttack() / heldcritter.GetComponent<CritterHolder>().GrabAttackTime()));
        transform.GetChild(3).GetComponent<Text>().text = f.ToString() + " DPS";
        var a = heldcritter.GetComponent<CritterHolder>().AIScript;
        // if(a.GetType() == typeof(basic_Ranged_AI_script))
        // {
        //     var b = (basic_Ranged_AI_script)a;
        //     transform.GetChild(3).GetComponent<Text>().text += "    " + b.ammo + "x " + b.modifier.base_attack * b.modifier.base_attacktime + " DPS";
        // }
        if(a.GetType() == typeof(basic_Ranged_AI_script_ammo))
        {
            var b = (basic_Ranged_AI_script_ammo)a;
            transform.GetChild(3).GetComponent<Text>().text += "    " + b.ammo + "x " + b.modifier.base_attack + " Damage";
        }
        if(a.GetType() == typeof(basic_Skirmish_Ranged_AI_script_ammo))
        {
            var b = (basic_Skirmish_Ranged_AI_script_ammo)a;
            transform.GetChild(3).GetComponent<Text>().text += "    " + b.ammo + "x " + b.modifier.base_attack + " Damage";
        }
    }
    public void UpdateMinPagans(int a)
    {
        if(a > minpagans)
        {
            CanPlay = true;
            transform.GetComponent<Image>().color = new Color32(82,82,82,255);
        }
    }
    public void OnMouseDown()
    {
        
        
        if(firsttime)
        {
            firsttime = false;
            heldcritter.GetComponent<CritterHolder>().Wakey();
        }
        // if(heldcritter == GeneralManager.Instance.SelectedCritter)
        // {
        //     heldcritter.GetComponent<CritterHolder>().AbilityList.Add(Resources.Load<DeathAbility>("Forages/DeathOfADoge"));
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
        var a = heldcritter.GetComponent<CritterHolder>();
        string texty = "";

        texty += "Name: " + a.name;
        texty += "\nCost: " + a.cost.name + ":" + (a.cost.amount/10);
        
        foreach (var item in a.AbilityList)
        {
            texty += "\n";
            texty += "\n" + item.Description;
            //texty += "\n" + item.Description; 
        }
        
        if(DescriptionManager.Instance)
        {
            DescriptionManager.Instance.UpdateDescriptionTo(texty);
        }
    }
}
