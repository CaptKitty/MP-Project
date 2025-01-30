using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;   

public class Piechart : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static Piechart Instance;
    public Image[] imagesPieChart;
    public Tooltip[] ToolTips;
    public float[] values;
    public string message;
    public Vector2 positiontime;
    private int lines = 0;
    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        //SetValues(values);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //OnMouseEnter();
        ToolTipManager._instance.SetAndShowToolTip(message, new Vector2(275,400), size: new Vector2(200, 30+20*lines)); // + new Vector3(-675,-125,0))(this.transform.position)new Vector3(-275,-20,0)
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        //OnMouseExit();
        ToolTipManager._instance.HideToolTip();
    }
    public void SetValues(List<Culture> ValuesToSet) //List<float> 
    {
        message = "";
        lines = 0;
        float totalValues = 0;
        for (int i = 0; i < ValuesToSet.Count; i++)
        {
            totalValues += FindPercentage(ValuesToSet,index:i);
            imagesPieChart[i].fillAmount = totalValues;
            imagesPieChart[i].color = ValuesToSet[i].ownerIdentity;
            ToolTips[i].message = ValuesToSet[i].name;
            message += ValuesToSet[i].population + " " + ValuesToSet[i].name + "\n";
            lines += 1;
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
