using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DiceRollerMenu : MonoBehaviour
{
    public static DiceRollerMenu Instance;
    public GameObject DiceToRoll;
    public List<GameObject> objectlist = new List<GameObject>();

    public void Awake()
    {
        Instance = this;
    }
    public void Reset()
    {
        foreach (var item in objectlist)
        {
            Destroy(item);
        }
        objectlist.Clear();
    }
    public void RolledDice(string lane, int value = 0)
    {
        var DetectedCritters = Instantiate(DiceToRoll, this.transform);
        DetectedCritters.GetComponent<Image>().sprite = Resources.Load<Sprite>("dice_" + value);
        objectlist.Add(DetectedCritters);

        if(lane == "Top")
        {
            DetectedCritters.transform.localPosition = new Vector2(-50, 100);
        }
        if(lane == "Bottom")
        {
            DetectedCritters.transform.localPosition = new Vector2(-50, -100);
        }
    }
}
