using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArmyMovement : MonoBehaviour
{
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
    public void Movement()
    {
        var heading  = target - gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;
        if(direction != null)
        {
            gameObject.transform.position += direction * Time.deltaTime * 25f;
        }
        

        if(distance < 0.2f)
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
    public void Combat(string province)
    {
        troops -= 1;
        if(troops < 1)
        {
            Destroy(this.gameObject);
            return;
        }
        Owners.Instance.provincelist.Find(x => x.name == province).troops -= 1;
        if(Owners.Instance.provincelist.Find(x => x.name == province).troops < 1)
        {
            Victory();
        }
    }
    public void Victory()
    {
        Mapshower.Instance.ChangeProvinceOwner(province, nation);
        Owners.Instance.provincelist.Find(x => x.name == province).troops += troops;
        Destroy(this.gameObject);
    }

}
