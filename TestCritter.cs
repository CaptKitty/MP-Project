using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCritter : MonoBehaviour
{
    public Color color, color2, color3;
    public List<GameObject> listy = new List<GameObject>();
    public Material material;
    
    // Start is called before the first frame update
    void Start()
    {
        material = Instantiate(this.GetComponent<SpriteRenderer>().material);
        foreach (var item in listy)
        {
            item.GetComponent<SpriteRenderer>().material = material;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        material.SetColor("_FactionColor", color);
        material.SetColor("_FactionColor2", color2);
        material.SetColor("_FactionColor3", color3);
    }
}
