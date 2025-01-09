using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIElement : MonoBehaviour
{
    public static UIElement NationHost;
    public static UIElement ProvinceHost;
    // Start is called before the first frame update
    void Start()
    {
        if(gameObject.name == "NationHost")
        {
            NationHost = this;
        }
        if(gameObject.name == "ProvinceHost")
        {
            ProvinceHost = this;
        }
    }
    public void UpdateTitle(string text)
    {
        transform.GetChild(0).gameObject.GetComponent<Text>().text = text;
    }
    public void UpdateDescription(string text)
    {
        transform.GetChild(1).gameObject.GetComponent<Text>().text = text;
    }
    public void UpdateDescription(Nation nation)//List<ProvinceModifier> provincemodifiers)
    {
        var texty = "Modifiers:";
        texty += "\nMaxTroops: " + nation.GrabMaxTroops().ToString();
        texty += "\nDefenceBonus: " + nation.GrabDefensiveDice().ToString();
        texty += "\nOffenceBonus: " + nation.GrabOffensiveDice().ToString();

        transform.GetChild(1).gameObject.GetComponent<Text>().text = texty;

        // foreach (var modifiers in province.provincemodifiers)
        // {
        // }
    }
    public void UpdateDescription(Province province)//List<ProvinceModifier> provincemodifiers)
    {
        var texty = "Modifiers:";
        texty += "\nMaxTroops: " + province.GrabMaxTroops().ToString();
        texty += "\nDefenceBonus: " + province.GrabDefensiveDice().ToString();

        transform.GetChild(1).gameObject.GetComponent<Text>().text = texty;

        // foreach (var modifiers in province.provincemodifiers)
        // {
        // }
    }
    public void Updatethird(string text)
    {
        transform.GetChild(2).gameObject.GetComponent<Text>().text = text;
    }
}
