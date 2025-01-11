using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[System.Serializable]
public class Tooltip : MonoBehaviour
{
    public string message;
    public bool armory = true;
    public bool events = false;
    public bool skills = false;
    public bool resize = false;
    public Vector2 resizesize;
    public Vector3 positions;

    void OnMouseEnter()
    {
        // //print(ToolTipManager._instance.gameObject.name);
        ToolTipManager._instance.SetAndShowToolTip(message, positions);

        //ToolTipManager._instance.SetAndShowToolTip(message, new Vector3(this.transform.position.x+positions.x,this.transform.position.y+positions.y,0), true);
        // Debug.Log("Ping from " + this.transform.parent.gameObject.name);
        // Debug.Log(message);
        // if(skills)
        // {
        //     // if(ToolTipManager._instance3.gameObject != null)
        //     // {
        //         // try
        //         // {
        //             ToolTipManager._instance = GameObject.Find("TooltipManager").GetComponent<ToolTipManager>();
        //             ToolTipManager._instance.SetAndShowToolTip(message, new Vector3(this.transform.position.x+positions.x,this.transform.position.y+positions.y,0), true);
        //         //     // ToolTipManager._instance3.SetAndShowToolTip(message, this.transform.position, true);
        //         // }
        //         // catch{}
        //     // }
        //     return;
        // }
        // if(events)
        // {
        //     // if(ToolTipManager._instance3.gameObject != null)
        //     // {
        //         // try
        //         // {
        //             ToolTipManager._instance = GameObject.Find("TooltipManager(Event)").GetComponent<ToolTipManager>();
        //             ToolTipManager._instance.SetAndShowToolTip(message, new Vector3(this.transform.position.x+positions.x,this.transform.position.y+positions.y,0), true);
        //             if(resize == true)
        //             {
        //                 ToolTipManager._instance.SetAndShowToolTip(message, new Vector3(this.transform.position.x+positions.x,this.transform.position.y+positions.y,0), true, resizesize);
        //             }
        //         //     // ToolTipManager._instance3.SetAndShowToolTip(message, this.transform.position, true);
        //         // }
        //         // catch{}
        //     // }
        //     return;
        // }
        // if(armory)
        // {
        //     ToolTipManager._instance = GameObject.Find("TooltipManager(Armory)").GetComponent<ToolTipManager>();
        //     ToolTipManager._instance.SetAndShowToolTip(message, this.transform.position, true);
        //     // if(ToolTipManager._instance.gameObject != null)
        //     // {
        //         // try
        //         // {
        //         //     ToolTipManager._instance.SetAndShowToolTip(message, this.transform.position, true);
        //         // }
        //         // catch{}
        //     // }
        //     return;
        // }
        // if(!armory && !events)
        // {
        //     ToolTipManager._instance = GameObject.Find("TooltipManager(Battle)").GetComponent<ToolTipManager>();
        //     ToolTipManager._instance.SetAndShowToolTip(message, this.transform.position, true);
        //     if(resize == true)
        //     {
        //         ToolTipManager._instance.SetAndShowToolTip(message, new Vector3(this.transform.position.x+positions.x,this.transform.position.y+positions.y,0), true, resizesize);
        //     }
        // }
    }

    void OnMouseExit()
    {
        ToolTipManager._instance.HideToolTip();
        return;
        if(ToolTipManager._instance)
        {
            if(ToolTipManager._instance.gameObject != null){
            try
            {
                ToolTipManager._instance.HideToolTip();
            }
            catch{}}
        }
    }
}
