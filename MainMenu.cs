using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject Canvas1, Canvas2, ButtonA, ButtonB;
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
}
