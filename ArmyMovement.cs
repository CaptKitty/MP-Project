using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ArmyMovement : MonoBehaviour
{
    public string name;
    public Vector3 target = new Vector3();
    public string origin;
    public string province;
    public string nation;
    public int troops;
    private float timer;
    // Start is called before the first frame update
    void Start()
    {
        Owners.Instance.armylist.Add(this.gameObject);
        transform.GetChild(0).GetChild(0).GetComponent<Text>().text = troops.ToString();
        SetTroopsMarker();
    }
    void FixedUpdate()
    {
    }
    public void Die()
    {
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().KillTroopsServerRpc(name);
        }
    }
    public float TickDistance()
    {
        float a = Owners.Instance.nationlist.Find(x => x.name == nation).GrabSpeedModifier();
        a = a * 0.35f;
        return a;
    }
    public void Movement()
    {
        var heading  = target - gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;
        if(distance < TickDistance())
        {
            gameObject.transform.position = target;
        }
        else
        {
            if(direction != null)
            {
                gameObject.transform.position += direction * 0.02f * 25f * Owners.Instance.nationlist.Find(x => x.name == nation).GrabSpeedModifier();
            }
        }
    }
    public void Fighty()
    {
        var heading  = target - gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;
        if(distance < TickDistance())
        {
            if(Time.time > timer)
            {
                timer = Time.time + 1f;
                if(Owners.Instance.provincelist.Find(x => x.name == province).nation.name == nation)
                {
                    Victory();
                }
                else
                {
                    Combat(province);
                }
            }
        }
    }
    public int MaxDice()
    {
        return 6;
    }
    public int GrabCombatWidth(int potato)
    {
        int CombatWidth = 1;
        
        int NationalBonusCombatWidth = Owners.Instance.nationlist.Find(x => x.name == nation).GrabCombatWidth();
        int TroopBasedBonusCombatWidth = (potato/10);//AddProvinceStuff?

        return CombatWidth + NationalBonusCombatWidth + TroopBasedBonusCombatWidth;
    }
    public void Combat(string province)
    {
        Province relevantprovince = Owners.Instance.provincelist.Find(x => x.name == province);
        
        
        int a = GrabCombatWidth(troops);

        try
        {
            for (int i = 0; i < a; i++)
            {
                int ArmyDice = Random.Range(0, MaxDice()+Owners.Instance.nationlist.Find(x => x.name == nation).GrabOffensiveDice());
                int ProvinceDice = Random.Range(0, relevantprovince.MaxDice()) + relevantprovince.GrabDefensiveDice();
                if(ArmyDice !< ProvinceDice)
                {
                    troops -= 1;
                    //transform.GetChild(0).GetChild(0).GetComponent<Text>().text = troops.ToString();
                    SetTroopsMarker();
                    foreach (var RPC in TestRelay.Instance.PlayerObjects)
                    {
                        RPC.GetComponent<RpcTest>().UpdateTroopsServerRpc(name);
                    }
                    if(troops < 1)
                    {
                        Die();
                        return;
                    }
                }
                else
                {
                    relevantprovince.AddTroops(-1);
                    if(relevantprovince.troops < 1)
                    {
                        Victory();
                    }
                }
            }
        }
        catch{}
        
        
    }
    public void SetTroopsMarker()
    {
        transform.GetChild(0).GetChild(0).GetComponent<Text>().text = troops.ToString();
    }
    public void Victory()
    {
        Mapshower.Instance.ChangeProvinceOwner(province, nation);
        Owners.Instance.provincelist.Find(x => x.name == province).AddTroops(troops);
        Die();
    }
}
