using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldArmyHolder : MonoBehaviour
{
    public FieldArmy fieldArmy;
    public static FieldArmyHolder PlayerFieldArmy;
    public Vector3 adjustment = new Vector3(948, 533);
    public Vector3 modification = new Vector3(0.5f, 0.5f);
    public Vector3 offset = new Vector3(364f, 232f);
    public int speed = 30;
    private Vector3 target;
    public Vector3 LocalProvince;
    private float timer;
    public int turnCounter = 0;
    private int NextRecruitTime = 0;
    public int _RecruitTimer = 4;
    private int RecruitTimer = 4;
    public int _DomesticSupplyUsage = 1;
    private int DomesticSupplyUsage = 1;
    public int _ForeignSupplyUsage = 4;
    private int ForeignSupplyUsage = 4;
    public int _ActivityTimer = 10;
    private int ActivityTimer = 10;
    public List<string> flaglist = new List<string>();
    public bool IsPlayer = false;
    public void Awake()
    {
        DomesticSupplyUsage = _DomesticSupplyUsage;
        ForeignSupplyUsage = _ForeignSupplyUsage;
        RecruitTimer = _RecruitTimer;
        ActivityTimer = _ActivityTimer;

        if (gameObject.name == "PlayerArmy")
        {
            if (FieldArmyHolder.PlayerFieldArmy == null)
            {
                PlayerFieldArmy = this;
                IsPlayer = true;
                //Debug.LogError("potato");
            }
        }
        else
        {
            fieldArmy = new FieldArmy();
            RecruitTimer = _RecruitTimer * 2;
        }

        fieldArmy.ArmySupply = 500;

        // Owners.Instance.armylist.Add(this);
    }
    public void Start()
    {
        if (gameObject.name == "PlayerArmy")
        {
            FieldArmyHolder.PlayerFieldArmy.fieldArmy.nation = Owners.Instance.nationlist.Find(x => x.faction.name == SessionManager.Instance.HostFaction.name); //SessionManager.Instance.HostFaction
        }
        else
        {
            if (fieldArmy.nation == null)
            {
                fieldArmy.nation = GrabFieldArmyProvince().nation;
                fieldArmy.nation.armies.Add(this);
            }
        }
        Owners.Instance.armylist.Add(this);
        Material mat = Instantiate(transform.GetChild(0).GetComponent<SpriteRenderer>().material);
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = mat;
        mat.SetColor("_FactionColor", fieldArmy.nation.faction.color);
        mat.SetColor("_FactionColor2", fieldArmy.nation.faction.color2);
        mat.SetColor("_FactionColor3", fieldArmy.nation.faction.color3);

        fieldArmy.USDReserves.Clear();

        foreach (UnitSaveData item in fieldArmy.nation.faction.UnitDataList)
        {
            fieldArmy.AddTroop(item, 3);
        }
        if (fieldArmy.nation.faction.HasFlag("EqualOpportunityPillagers"))
        {
            DomesticSupplyUsage = 2;
            ForeignSupplyUsage = 2;
        }
        if (fieldArmy.nation.faction.HasFlag("Decentralized"))
        {
            ActivityTimer = 20;
            RecruitTimer *= 2;
        }
        if (fieldArmy.nation.faction.HasFlag("Braindead"))
        {
            ActivityTimer = 1000;
            RecruitTimer *= 10;
        }
        if (fieldArmy.nation.faction.HasFlag("FastAsFuckBoi"))
        {
            speed *= 2;
        }
    }
    public void OnDestroy()
    {
        fieldArmy.nation.armies.Remove(this);
        Owners.Instance.armylist.Remove(this);
    }
    public void Update()
    {
        if (IsPlayer && Input.GetMouseButtonDown(1))
        {
            SetTarget(Input.mousePosition);
        }
    }
    public void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.gameObject.GetComponent<FieldArmyHolder>() != null)
        {
            OnMeeting(collider.gameObject.GetComponent<FieldArmyHolder>());
        }
    }
    public void OnMouseOver()
    {
        fieldArmy.UpdateUI();
    }
    public void OnMeeting(FieldArmyHolder otherarmy)
    {
        if (otherarmy.fieldArmy.nation == fieldArmy.nation)
        {
            return;
        }
        if (IsPlayer || otherarmy.IsPlayer)
        {
            if (IsPlayer)
            {
                Mapshower.Instance.ArmyBattle(this, otherarmy);
            }
        }
        else
        {
            //HandleAIonAICombat
            if (Random.Range(0, 2) == 1)
            {
                Destroy(otherarmy.gameObject);
            }
            else
            {
                Destroy(this.gameObject);
            }
        }

    }
    public void Act()
    {
        if (IsPlayer && target.x + target.y == 0)
        {
            return;
        }
        var heading = transform.position - new Vector3((target.x - adjustment.x) * modification.x, (target.y - adjustment.y) * modification.y, 0);
        var distance = heading.magnitude;

        //SetPositionTo(target);

        //Mapshower.Instance.SelectProvince(GrabFieldArmyHolderPosition());

        // if (timer < Time.time)
        // {
        //     timer = Time.time + 0.5f;
        //     NextTurn();
        // }

        if (IsTargetNull())
        {
            return;
        }

        if (distance < 1)
        {
            //Mapshower.Instance.SelectProvince(target);
            LocalProvince = target;
            try
            {
                if (!IsPlayer)
                {
                    var a = GrabFieldArmyProvince();
                    EnterProvince(a);
                }
            }
            catch { }

            target = new Vector3(0, 0, 0);
            return;
        }
        var direction = heading / distance;
        transform.localPosition -= direction * Time.deltaTime * speed;
    }
    public bool IsTargetNull()
    {
        if (target.x + target.y == 0)
        {
            return true;
        }
        return false;
    }
    public void EnterProvince(Province province)
    {
        province.nation = fieldArmy.nation;
        Mapshower.Instance.RePaint();
    }
    public void SetPositionTo(Vector3 newposition)
    {
        transform.localPosition = new Vector3((newposition.x - adjustment.x) * modification.x, (newposition.y - adjustment.y) * modification.y, 0);
    }
    public void SetPositionTo(Province province)
    {
        transform.localPosition = new Vector3(province.position.x * 1f - offset.x, province.position.y * 1f - offset.y, 0);
    }
    public void AddTroop(UnitSaveData unittoAdd = null, string name = "", int amount = 1)
    {
        //Debug.LogError("Trying to add " + amount + " of " + name + unittoAdd);
        if (name != "")
        {
            try
            {
                var a = FieldArmyHolder.PlayerFieldArmy.fieldArmy.USDReserves.Find(x => x.name == name).USD;
                fieldArmy.AddTroop(a, amount);
            }
            catch
            {
                try
                {
                    var b = Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/" + name);
                    var c = Instantiate(b);
                    c.name = b.name;
                    fieldArmy.AddTroop(c, amount);
                }
                catch
                {
                    Debug.LogError("Could not find " + name + " Unit in database");
                }

            }

        }
        else
        {
            if (unittoAdd == null)
            {
                var a = fieldArmy.nation.faction.UnitDataList[Random.Range(0, fieldArmy.nation.faction.UnitDataList.Count)];
                fieldArmy.AddTroop(a, amount);
            }
            else
            {

                fieldArmy.AddTroop(unittoAdd, amount);
            }
        }
        if (IsPlayer)
        {
            fieldArmy.UpdateUI();
        }
    }
    public void NextTurn()
    {
        turnCounter++;
        Province province = GrabFieldArmyProvince(); //Mapshower.Instance.SelectedProvince;

        //HandleAIBehaviour
        if (!IsPlayer)
        {
            if (target.x + target.y == 0)
            {
                if (turnCounter % ActivityTimer == 0)
                {
                    var a = new List<Province>();
                    foreach (var item in Owners.Instance.provincelist)
                    {
                        if (item.nation == fieldArmy.nation)
                        {
                            a.Add(item);
                        }
                        var temptarget = Camera.main.WorldToScreenPoint(new Vector3(item.position.x * 1f - offset.x, item.position.y * 1f - offset.y, 0));
                        var heading = transform.position - new Vector3((temptarget.x - adjustment.x) * modification.x, (temptarget.y - adjustment.y) * modification.y, 0);
                        var distance = heading.magnitude;
                        if (distance < 150)
                        {
                            a.Add(item);
                        }
                    }
                    SetTarget(a[Random.Range(0, a.Count)]);
                }
            }
        }

        //HandleSupply
        if (IsPlayer)
        {
            //AtHome
            if (province != null && province.nation.faction.name == fieldArmy.nation.faction.name)
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
        }
        //HandleRecruitment
        if (turnCounter % RecruitTimer == 0)
        {
            //AtHome
            if (province != null && province.nation.faction.name == fieldArmy.nation.faction.name)
            {
                if (fieldArmy.nation.faction.HasFlag("DoubleLocalRecruitment"))
                {
                    AddTroop();
                }
                AddTroop();
            }
            //Abroad
            else
            {
                if (fieldArmy.nation.faction.HasFlag("ForeignNonRomanRecruitment"))
                {
                    if (province != null && !province.nation.faction.name.Contains("Rome"))
                    {
                        Nation nation = Owners.Instance.nationlist.Find(x => x.name == province.OriginalNation.name);
                        Debug.LogError(nation.name);
                        var a = Instantiate(nation.faction.UnitDataList[0]);
                        a.name = province.nation.faction.UnitDataList[0].name;
                        if (Random.Range(0, 3) == 1)
                        {
                            a = Instantiate(nation.faction.UnitDataList[1]);
                            a.name = province.nation.faction.UnitDataList[1].name;
                        }
                        //a.name = province.nation.faction.UnitDataList[0].name;
                        a.Mercenary = true;
                        AddTroop(a);
                    }
                }
            }
        }
        //HandleEvents
        if (IsPlayer && turnCounter % 8 == 0)
        {

            EventManager.eventManager.TriggerEvent(grabRandomViableEvent().name);
        }
    }
    public BaseEvents grabRandomViableEvent()
    {
        var a = Resources.LoadAll<BaseEvents>("EventGroup/");
        var b = new List<BaseEvents>();
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Trigger())
            {
                b.Add(a[i]);
            }
        }
        return b[Random.Range(0, b.Count)];
    }

    public bool HasFlag(string flag)
    {
        foreach (var item in flaglist)
        {
            if (item == flag)
            {
                return true;
            }
        }
        return false;
    }
    public Province GrabFieldArmyProvince()
    {
        return Mapshower.Instance.SelectProvinceFromLocation(GrabFieldArmyHolderPosition());
    }
    public Vector3 GrabFieldArmyHolderPosition()
    {
        return Camera.main.WorldToScreenPoint(transform.position);
    }
    public void SetTarget(Vector3 newtarget)
    {
        // Debug.LogError("Target Set");
        target = newtarget;
    }
    public void SetTarget(Province province)
    {
        // Debug.LogError("Target Set");
        target = Camera.main.WorldToScreenPoint(new Vector3(province.position.x * 1f - offset.x, province.position.y * 1f - offset.y, 0));
    }
}
