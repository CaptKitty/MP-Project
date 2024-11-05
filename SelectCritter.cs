using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectCritter : MonoBehaviour
{
    private bool firsttime = true;
    public GameObject heldcritter;
    public bool CanPlay = false;
    public int minpagans = -1;
    public void Awake()
    {
        if(firsttime)
        {
            firsttime = false;
            heldcritter.GetComponent<CritterHolder>().Wakey();
            transform.GetComponent<Image>().color = new Color32(0,0,0,255);
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
        if(GeneralManager.Instance)
        {
            GeneralManager.Instance.highlight = true;
            GeneralManager.Instance.delete = false;
            GeneralManager.Instance.SelectedCritter = heldcritter;
        }
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
