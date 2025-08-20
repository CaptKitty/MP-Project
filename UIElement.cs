using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIElement : MonoBehaviour
{
    public static UIElement NationHost;
    public static UIElement ProvinceHost;
    public static UIElement ArmyHost;
    // Start is called before the first frame update
    void Start()
    {
        if (gameObject.name == "NationHost")
        {
            NationHost = this;
        }
        if (gameObject.name == "ProvinceHost")
        {
            ProvinceHost = this;
        }
        if (gameObject.name == "ArmyHost")
        {
            ArmyHost = this;
        }
    }
    public void UpdateTitle(string text, string supply = "")
    {
        transform.GetChild(0).gameObject.GetComponent<Text>().text = text;
    }
    public void UpdateSecond(string text, string supply = "")
    {
        transform.GetChild(1).gameObject.GetComponent<Text>().text = supply + "\n Supply Available";
    }
    public void UpdateThree(string text, string supply = "")
    {
        transform.GetChild(2).gameObject.GetComponent<Text>().text = text;
    }
}
