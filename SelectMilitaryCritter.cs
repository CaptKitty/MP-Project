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
            transform.GetComponent<Image>().color = new Color32(0,0,0,255);

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
