using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   

public class Piechart : MonoBehaviour
{
    public Image[] imagesPieChart;
    public float[] values;
    // Start is called before the first frame update
    void Start()
    {
        SetValues(values);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SetValues(float[] ValuesToSet)
    {
        float totalValues = 0;
        for (int i = 0; i < imagesPieChart.Length; i++)
        {
            totalValues += FindPercentage(ValuesToSet,i);
            imagesPieChart[i].fillAmount = totalValues;
        }
    }
    public float FindPercentage(float[] valueToSet, int index)
    {
        float totalAmount = 0;
        for (int i = 0; i < imagesPieChart.Length; i++)
        {
            totalAmount += valueToSet[i];
        }
        return valueToSet[index] / totalAmount;
    }
}
