using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject Canvas1, Canvas2, ButtonA, ButtonB, Main, Multi;
    public void JoinHost()
    {
        Canvas1.SetActive(false);
        Canvas2.SetActive(true);
        ButtonA.SetActive(true);
        //SceneManager.LoadScene("SampleScene");
    }
    public void JoinClient()
    {
        Canvas1.SetActive(false);
        Canvas2.SetActive(true);
        ButtonB.SetActive(true);
        //SceneManager.LoadScene("SampleScene");
    }
    public void SelectMulti()
    {
        Main.SetActive(false);
        Multi.SetActive(true);
    }
    public void ChangePick(int whichone)
    {
        for (int i = 0; i < 3; i++)
        {
            string a = transform.GetChild(1).GetChild(i).GetChild(0).GetComponent<Text>().text;
            a = a.Replace("\n(Current)", "");
            a = a.Replace("\n(Default)", "");
            transform.GetChild(1).GetChild(i).GetChild(0).GetComponent<Text>().text = a;
        }
        transform.GetChild(1).GetChild(whichone).GetChild(0).GetComponent<Text>().text += "\n(Current)";
    }
    public void ChangeEnemy(int whichone)
    {
        for (int i = 0; i < 3; i++)
        {
            string a = transform.GetChild(2).GetChild(i).GetChild(0).GetComponent<Text>().text;
            a = a.Replace("\n(Current)", "");
            a = a.Replace("\n(Default)", "");
            transform.GetChild(2).GetChild(i).GetChild(0).GetComponent<Text>().text = a;
        }
        transform.GetChild(2).GetChild(whichone).GetChild(0).GetComponent<Text>().text += "\n(Current)";
    }
}
