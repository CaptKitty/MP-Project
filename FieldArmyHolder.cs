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
    public void Awake()
    {
        if (gameObject.name == "PlayerArmy")
        {
            if (FieldArmyHolder.PlayerFieldArmy == null)
            {
                PlayerFieldArmy = this;
            }
        }
    }
    public void Start()
    {
        FieldArmyHolder.PlayerFieldArmy.fieldArmy.faction = SessionManager.Instance.HostFaction;

        fieldArmy.USDReserves.Clear();

        foreach (UnitSaveData item in SessionManager.Instance.HostFaction.UnitDataList)
        {
            fieldArmy.AddTroop(item, 5);
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
            var a = SessionManager.Instance.HostFaction.UnitDataList.Find(x => x.name == name);
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
    public void FixedUpdate()
    {
        if (target.x + target.y == 0)
        {
            return;
        }
        var heading = transform.position - new Vector3((target.x - adjustment.x) * modification.x, (target.y - adjustment.y) * modification.y, 0);
        var distance = heading.magnitude;
        if (distance < 5)
        {
            Mapshower.Instance.SelectProvince(target);
            if (LocalProvince != target)
            {
                AddTroop();
            }
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
