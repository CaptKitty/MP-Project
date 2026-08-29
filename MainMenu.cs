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
        for (int i = 0; i < 7; i++)
        {
            string a = transform.GetChild(1).GetChild(i).GetChild(0).GetComponent<Text>().text;
            a = a.Replace("\n(Current)", "");
            a = a.Replace("\n(Default)", "");
            transform.GetChild(1).GetChild(i).GetChild(0).GetComponent<Text>().text = a;
            transform.GetChild(1).GetChild(i).GetChild(1).gameObject.SetActive(false);
        }
        transform.GetChild(1).GetChild(whichone).GetChild(0).GetComponent<Text>().text += "\n(Current)";
        transform.GetChild(1).GetChild(whichone).GetChild(1).gameObject.SetActive(true);
    }
    public void ChangeEnemy(int whichone)
    {
        for (int i = 0; i < 5; i++)
        {
            string a = transform.GetChild(2).GetChild(i).GetChild(0).GetComponent<Text>().text;
            a = a.Replace("\n(Current)", "");
            a = a.Replace("\n(Default)", "");
            transform.GetChild(2).GetChild(i).GetChild(0).GetComponent<Text>().text = a;
        }
        transform.GetChild(2).GetChild(whichone).GetChild(0).GetComponent<Text>().text += "\n(Current)";
    }
    public void SetDifficulty(int whichone)
    {
        for (int i = 5; i < 8; i++)
        {
            string a = transform.GetChild(1).GetChild(i).GetChild(0).GetComponent<Text>().text;
            a = a.Replace("\n(Current)", "");
            a = a.Replace("\n(Default)", "");
            transform.GetChild(1).GetChild(i).GetChild(0).GetComponent<Text>().text = a;
        }
        SessionManager.Instance.CampaignLevel = whichone;
        transform.GetChild(1).GetChild(whichone+4).GetChild(0).GetComponent<Text>().text += "\n(Current)";
    }
}
