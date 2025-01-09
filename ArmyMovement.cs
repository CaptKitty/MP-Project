using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArmyMovement : MonoBehaviour
{
    public string name;
    public Vector3 target = new Vector3();
    public string province;
    public string nation;
    public int troops;
    private float timer;
    // Start is called before the first frame update
    void Start()
    {
        Owners.Instance.armylist.Add(this.gameObject);
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
    public void Movement()
    {
        var heading  = target - gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;
        if(direction != null)
        {
            gameObject.transform.position += direction * 0.02f * 25f;
        }
    }
    public void Fighty()
    {
        var heading  = target - gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;
        if(distance < 0.35f)
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
    public void Combat(string province)
    {
        Province relevantprovince = Owners.Instance.provincelist.Find(x => x.name == province);
        
        int ArmyDice = Random.Range(0, MaxDice()+Owners.Instance.nationlist.Find(x => x.name == nation).GrabOffensiveDice());
        int ProvinceDice = Random.Range(0, relevantprovince.MaxDice()) + relevantprovince.GrabDefensiveDice();

        if(ArmyDice !< ProvinceDice)
        {
            troops -= 1;
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
    public void Victory()
    {
        Mapshower.Instance.ChangeProvinceOwner(province, nation);
        Owners.Instance.provincelist.Find(x => x.name == province).AddTroops(troops);
        Die();
    }
}
