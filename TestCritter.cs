using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCritter : MonoBehaviour
{
    public Color color, color2, color3;
    public Faction faction;
    public List<GameObject> listy = new List<GameObject>();
    public Material material;
    public bool DoesThisHaveSword = false;
    public bool DoesThisHaveSpear = false;
    public bool DoesThisHaveJavelin = false;
    
    // Start is called before the first frame update
    void Start()
    {
        faction = BattleManager1.Instance.Playerfaction;
        material = Instantiate(this.GetComponent<SpriteRenderer>().material);
        material.SetColor("_FactionColor", faction.color);
        material.SetColor("_FactionColor2", faction.color2);
        material.SetColor("_FactionColor3", faction.color3);
        
        GetComponent<Animator>().SetBool("Sword", DoesThisHaveSword);
        GetComponent<Animator>().SetBool("Spear", DoesThisHaveSpear);
        GetComponent<Animator>().SetBool("Javelin", DoesThisHaveJavelin);
        foreach (var item in listy)
        {
            item.GetComponent<SpriteRenderer>().material = material;
        }
    }
    void FixedUpdate()
    {
        foreach (var item in listy)
        {
            item.GetComponent<SpriteRenderer>().material = material;
        }
    }
}
