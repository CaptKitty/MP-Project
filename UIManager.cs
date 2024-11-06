using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public Text texty;
    public Text textoid;
    public GameObject potato;
    public List<GameObject> potatolist = new List<GameObject>();
    public double PaganCount = 0;

    public void Awake()
    {
        if(!UIManager.Instance)
        {
            Instance = this;
            
        }   
    }
    public void Start()
    {
        GeneralManager.Instance.cam3 = GameObject.Find("UICamera").GetComponent<Camera>();
    }
    public void UpdateUI()
    {
        int a = potatolist.Count;
        for (int i = 0; i < a; i++)
        {
            Destroy(potatolist[0]);
            potatolist.Remove(potatolist[0]);
        }
        potatolist.Clear();
        int x = 0;
        int y = 0;
        foreach (var item in CityManager.Instance.ResourceList)
        {
            var newpotato = Instantiate(potato, transform.GetChild(1));
            newpotato.transform.localPosition = new Vector2(-810+250*x, 25-50*y);
            y++;
            if(y == 2)
            {
                x++;
                y = 0;
            }

            string potatos = "";
            float duck = (float)item.amount/10;
            potatos += item.name + " : " + duck.ToString() + "\n";
            //datafile.text = potato;
            newpotato.name = potatos;
            newpotato.transform.GetChild(1).GetComponent<Text>().text = potatos;
            newpotato.transform.GetChild(0).GetComponent<Image>().sprite = item.sprite;
            potatolist.Add(newpotato);
        }
        texty.text = "Pagans converted: \n" + PaganCounter().ToString();
        textoid.text = "Days Passed: " + GeneralManager.Instance.totalturncounter;
        UpdateBuildings();
    }
    public double PaganCounter()
    {
        float aka = 0;
        if(GlobalManager.Instance)
        {
            foreach (Vector3Int position in GlobalManager.Instance.ownermap.cellBounds.allPositionsWithin)
            {
                aka += GlobalManager.Instance.faithamount[position];
            }
        }
        double aks = aka * 100;
        aks = Math.Round(aka);
        PaganCount = aks;
        return aks;
    }
    public void UpdateBuildings()
    {
        for (int i = 0; i < 10; i++)
        {
            if(transform.GetChild(0).GetChild(i))
            {
                transform.GetChild(0).GetChild(i).GetComponent<SelectCritter>().UpdateMinPagans((int)PaganCount);
            }
        }
    }
}
