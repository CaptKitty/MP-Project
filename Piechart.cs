using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   

public class Piechart : MonoBehaviour
{
    public static Piechart Instance;
    public Image[] imagesPieChart;
    public float[] values;
    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        //SetValues(values);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SetValues(List<Culture> ValuesToSet) //List<float> 
    {
        float totalValues = 0;
        for (int i = 0; i < ValuesToSet.Count; i++)
        {
            totalValues += FindPercentage(ValuesToSet,index:i);
            imagesPieChart[i].fillAmount = totalValues;
            imagesPieChart[i].color = ValuesToSet[i].ownerIdentity;
        }
    }
    public float FindPercentage(List<Culture> valueToSet, int index)
    {
        float totalAmount = 0;
        for (int i = 0; i < valueToSet.Count; i++)
        {
            totalAmount += valueToSet[i].population;
        }
        return (float)valueToSet[index].population / totalAmount;
    }
}
