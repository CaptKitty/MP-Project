using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

using Unity.Netcode;
using UnityEngine;
using Unity.Services.Authentication;

public class TestCritter : MonoBehaviour
{
    public Color color, color2, color3;
    public Faction faction;
    public List<GameObject> listy = new List<GameObject>();
    public Material material;
    public bool DoesThisHaveSword = false;
    public bool DoesThisHaveSpear = false;
    public bool DoesThisHaveJavelin = false;
    public GameObject Upgrade;
    public GameObject UpgradeButtons;
    public bool Mercenary = false;
    
    // Start is called before the first frame update
    public void Start()
    {
        // faction = BattleManager1.Instance.Playerfaction;
        material = Instantiate(this.GetComponent<SpriteRenderer>().material);
        material.SetColor("_FactionColor", faction.color);
        material.SetColor("_FactionColor2", faction.color2);
        material.SetColor("_FactionColor3", faction.color3);
        if(Mercenary == true)
        {
            material.SetColor("_FactionColor3", color3);
        }
        
        
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
    public void OnMouseEnter()
    {
        if(UpgradeButtons != null && Upgrade != null)
        {
            var a = Instantiate(UpgradeButtons, position:new Vector3(transform.position.x, transform.position.y + 0.5f, 0),transform.rotation, BattleManager1.Instance.gameObject.transform);
            a.GetComponent<UpgradeButton>().testy = this;
        }
        
    }
    public void OnMouseDown()
    {
        
        if(Upgrade != null)
        {
            //UpgradeTroop(Upgrade);
        }
    }
    public void UpgradeTroop(GameObject PickedUpgrade)
    {
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            Vector3Int target =    BattleManager1.Instance.ownermap.WorldToCell(this.transform.position);//Camera.main.ScreenToWorldPoint(Input.mousePosition)
            BattleManager1.Instance.dicty[target] = null;
            RPC.GetComponent<RpcTest>().Spawn(target, PickedUpgrade);
        }
        gameObject.SetActive(false);
    }
}
