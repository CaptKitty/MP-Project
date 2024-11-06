using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JoinCodeStuff : MonoBehaviour
{
    public static JoinCodeStuff Instance;
    public InputField Texty;
    void Start()
    {
        Instance = this;
    }
}
