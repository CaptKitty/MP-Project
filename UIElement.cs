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
}
