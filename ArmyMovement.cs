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

    public List<ArmyMovement> EnemyList = new List<ArmyMovement>();

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
        foreach (var item in EnemyList)
        {
            if(item != null)
            {
                if(item.nation != nation)
                {
                    if(Owners.Instance.nationlist.Find(x => x.name == nation).GrabDiplomaticStatus(item.nation) != "peace")
                    {
                        return;
                    }
                }
            }
        }
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
    public void Combaty()
    {
        foreach (var item in EnemyList)
        {
            if(item != null)
            {
                if(item.nation != nation)
                {
                    if(Owners.Instance.nationlist.Find(x => x.name == nation).GrabDiplomaticStatus(item.nation) != "peace")
                    {
                        ArmyCombat(item);
                        return;
                    }
                }
            }
        }
        //AttackProvince
        Fighty();
    }
    public void OnTriggerEnter2D(Collider2D other)
    {
        var a = other.gameObject.GetComponent<ArmyMovement>();
        if(a != null)
        {
            Debug.LogError(a.name);
            EnemyList.Add(a);
        }
    }
    public void SettleDown()
    {
        var heading  = target - gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;
        if(distance < TickDistance())
        {
            Owners.Instance.statelist.Find(x => x.name == Owners.Instance.provincelist.Find(x => x.name == province).state).Capitol.AddTroops(troops);
            troops = 0;
            Die();
        }
    }
    public Province CallProvince()
    {
        var ray = Camera.main.ScreenPointToRay(this.transform.position);
        RaycastHit hitInfo;
        if(Physics.Raycast(ray, out hitInfo)){
            var p = hitInfo.point;
            int x = (int)Mathf.Floor(p.x) + Mapshower.Instance.width / 2;
            int y = (int)Mathf.Floor(p.y) + Mapshower.Instance.height / 2;

            var remapColor = Mapshower.Instance.GrabremapArr()[x + y * Mapshower.Instance.width];
            // print(remapColor.r + " " + x.ToString() + " " + y.ToString());
            int xp = remapColor[0];
            int yp = remapColor[1];

            var material = GetComponent<Renderer>().material;
            var mainTex = material.GetTexture("_MainTex") as Texture2D;
            if(mainTex.GetPixel(x,y) == new Color32(0,0,0,0))
            {
                return null;
            }
            Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
            return province;
        }
        return null;
    }
    public void OnMouseDown()
    {
        Mapshower.Instance.SelectedArmy = this;
    }
    // public void OnTriggerExit(Collider other)
    // {
    //     EnemyList.Remove()
    // }
    public void ArmyCombat(ArmyMovement enemyArmy)
    {
        int CombatWidth = GrabCombatWidth(troops);
        for (int i = 0; i < CombatWidth; i++)
        {
            int ArmyDice = Random.Range(0, 7 + Owners.Instance.nationlist.Find(x => x.name == nation).GrabTroopDice(troops));
            //MaxDice()
            int ProvinceDice = Random.Range(0, 7 + Owners.Instance.nationlist.Find(x => x.name == enemyArmy.nation).GrabTroopDice(troops));
            //relevantprovince.MaxDice())
            if(ArmyDice < ProvinceDice)
            {
                troops -= 1;
                SetTroopsMarker();
                if(troops < 1)
                {
                    Die();
                    break;
                }
            }
            if(ArmyDice > ProvinceDice)
            {
                enemyArmy.troops -= 1;
                enemyArmy.SetTroopsMarker();
                if(enemyArmy.troops < 1)
                {
                    enemyArmy.Die();
                    break;
                }
            }
        }
        foreach (var RPC in TestRelay.Instance.PlayerObjects)
        {
            RPC.GetComponent<RpcTest>().UpdateTroopsServerRpc(name);
            RPC.GetComponent<RpcTest>().UpdateTroopsServerRpc(enemyArmy.name);
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
        
        
        int CombatWidth = GrabCombatWidth(troops);

        try
        {
            for (int i = 0; i < CombatWidth; i++)
            {
                int ArmyDice = Random.Range(0, 7 +Owners.Instance.nationlist.Find(x => x.name == nation).GrabOffensiveDice() + Owners.Instance.nationlist.Find(x => x.name == nation).GrabTroopDice(troops));
                //MaxDice()
                int ProvinceDice = Random.Range(0, 7 + relevantprovince.GrabDefensiveDice() + relevantprovince.nation.GrabTroopDice(troops));
                //relevantprovince.MaxDice())
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
        catch
        {
            if(relevantprovince.troops < 1)
            {
                Victory();
            }
        }
        
        
    }
    public void SetTroopsMarker()
    {
        if(troops == 0)
        {
            this.GetComponent<Image>().enabled = false;
            transform.GetChild(0).GetChild(0).GetComponent<Text>().text = "";
        }
        else
        {
            this.GetComponent<Image>().enabled = true;
            transform.GetChild(0).GetChild(0).GetComponent<Text>().text = troops.ToString();
        }
    }
    public void Victory()
    {
        Mapshower.Instance.ChangeProvinceOwner(province, nation);

        var a = Owners.Instance.nationlist.Find(x => x.name == nation);
        if(a.IsAlive && !a.IsPlayer)
        {
            SettleDown();
        }

        // Owners.Instance.statelist.Find(x => x.name == Owners.Instance.provincelist.Find(x => x.name == province).state).Capitol.AddTroops(troops);
        // troops = 0;
        // // if(Owners.Instance.provincelist.Find(x => x.name == province).Drafty != null)
        // // {
        // //     Owners.Instance.provincelist.Find(x => x.name == province).Drafty.GetComponent<ArmyMovement>().SetTroopsMarker();
        // // }
        
        // Die();
    }
}
