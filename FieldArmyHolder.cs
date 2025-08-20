using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldArmyHolder : MonoBehaviour
{
    public FieldArmy fieldArmy;
    public static FieldArmyHolder PlayerFieldArmy;
    public Vector3 adjustment = new Vector3(948, 533);
    public Vector3 modification = new Vector3(0.5f,0.5f);
    public int speed = 50;
    private Vector3 target;
    public Vector3 LocalProvince;
    private float timer;
    public int turnCounter = 0;
    private int NextRecruitTime = 0;
    public int _RecruitTimer = 2;
    private int RecruitTimer = 2;
    public int _DomesticSupplyUsage = 1;
    private int DomesticSupplyUsage = 1;
    public int _ForeignSupplyUsage = 4;
    private int ForeignSupplyUsage = 4;
    public void Awake()
    {
        if (gameObject.name == "PlayerArmy")
        {
            if (FieldArmyHolder.PlayerFieldArmy == null)
            {
                PlayerFieldArmy = this;
                fieldArmy.ArmySupply = 500;
                DomesticSupplyUsage = _DomesticSupplyUsage;
                ForeignSupplyUsage = _ForeignSupplyUsage;
                RecruitTimer = _RecruitTimer;
            }
        }
    }
    public void Start()
    {
        FieldArmyHolder.PlayerFieldArmy.fieldArmy.faction = SessionManager.Instance.HostFaction;
        Material mat = Instantiate(transform.GetChild(0).GetComponent<SpriteRenderer>().material);
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = mat;
        mat.SetColor("_FactionColor", fieldArmy.faction.color);
        mat.SetColor("_FactionColor2", fieldArmy.faction.color2);
        mat.SetColor("_FactionColor3", fieldArmy.faction.color3);

        fieldArmy.USDReserves.Clear();

        foreach (UnitSaveData item in SessionManager.Instance.HostFaction.UnitDataList)
        {
            fieldArmy.AddTroop(item, 5);
        }
        if (fieldArmy.faction.HasFlag("EqualOpportunityPillagers"))
        {
            DomesticSupplyUsage = 2;
            ForeignSupplyUsage = 2;
        }
        if (fieldArmy.faction.HasFlag("DoubleLocalRecruitment"))
        {
            RecruitTimer = 1;
        }
    }
    public void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            SetTarget();
        }
    }
    public void AddTroop(UnitSaveData unittoAdd = null, string name = "", int amount = 1)
    {
        if (name != "")
        {
            //var a = SessionManager.Instance.HostFaction.UnitDataList.Find(x => x.name == name);
            var a = FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves.Find(x => x.name == name).USD;
            fieldArmy.AddTroop(a, amount);
        }
        else
        {
            if (unittoAdd == null)
            {
                var a = SessionManager.Instance.HostFaction.UnitDataList[Random.Range(0, SessionManager.Instance.HostFaction.UnitDataList.Count)];
                fieldArmy.AddTroop(a, amount);
            }
            else
            {

                fieldArmy.AddTroop(unittoAdd, amount);
            }
        }
    }
    public void NextTurn()
    {
        turnCounter++;
        Province province = Mapshower.Instance.SelectedProvince;

        if (province != null && province.nation.faction.name == fieldArmy.faction.name)
        {
            fieldArmy.AddSupply(-fieldArmy.GrabArmySize() * DomesticSupplyUsage);
        }
        //Abroad
        else
        {
            fieldArmy.AddSupply(-fieldArmy.GrabArmySize() * ForeignSupplyUsage);
        }

        if (province != null && province.supply >= 100)
        {
            int lootableAmount = province.supply / 10;
            lootableAmount = lootableAmount;

            province.supply -= lootableAmount;
            fieldArmy.AddSupply(lootableAmount);
        }
        if (turnCounter >= NextRecruitTime)
        {
            NextRecruitTime = turnCounter + RecruitTimer;
            //AtHome
            if (province != null && province.nation.faction.name == fieldArmy.faction.name)
            {
                AddTroop();
            }
            //Abroad
            else
            {
                if (fieldArmy.faction.HasFlag("ForeignNonRomanRecruitment"))
                {
                    if (province != null && !province.nation.faction.name.Contains("Rome"))
                    {
                        var a = Instantiate(province.nation.faction.UnitDataList[0]);
                        a.Mercenary = true;
                        AddTroop(a);
                    }
                }
            }
        }   
    }
    public void FixedUpdate()
    {
        if (target.x + target.y == 0)
        {
            return;
        }
        var heading = transform.position - new Vector3((target.x - adjustment.x) * modification.x, (target.y - adjustment.y) * modification.y, 0);
        var distance = heading.magnitude;

        Mapshower.Instance.SelectProvince(Camera.main.WorldToScreenPoint(transform.position));

        if (timer < Time.time)
        {
            timer = Time.time + 0.5f;
            NextTurn();
        }

        if (distance < 5)
        {
            Mapshower.Instance.SelectProvince(target);

            LocalProvince = target;
            target = new Vector3(0, 0, 0);
            return;
        }
        var direction = heading / distance;
        transform.localPosition -= direction * Time.deltaTime * speed;
    }
    public void SetTarget()
    {
        // Debug.LogError("Target Set");
        target = Input.mousePosition;
    }
}
