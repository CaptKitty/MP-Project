using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CritterMovement : MonoBehaviour
{
    public bool online = false;
    [SerializeField]
    public GameObject TargetEnemy;
    public float combatdistance = 0.25f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if(online == true)
        {
            
        }
    }
    void Attack(float distance)
    {
        if(distance < combatdistance)
        {
            // Destroy(TargetEnemy);
            TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(1);
        }
    }
    public void FindTarget()
    {
        List<GameObject> enemylists = new List<GameObject>();
        foreach (var item in BattleManager1.Instance.enemylist)
        {
            if(item == null)
            {
                continue;
            }
            if(item.GetComponent<CritterHolder>().IsthisAI != this.gameObject.GetComponent<CritterHolder>().IsthisAI)
            {
                enemylists.Add(item);
            }
        }
        if(enemylists.Count > 0)
        {
            TargetEnemy = enemylists[0];
            foreach (var item in enemylists)
            {
                var heading  = item.transform.position - transform.position;
                var distance = heading.magnitude;

                var heading2  = TargetEnemy.transform.position - transform.position;
                var distance2 = heading2.magnitude;
                
                if(distance < distance2)
                {
                    TargetEnemy = item;
                }
            }
        }
    }
}
