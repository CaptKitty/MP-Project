using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DescriptionManager : MonoBehaviour
{
    public static DescriptionManager Instance;
    public Text texty;

    public void Awake()
    {
        Instance = this;
    }
    public void UpdateDescriptionTo(string texx)
    {
        texty.text = texx;
    }
}
