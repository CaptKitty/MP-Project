using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public GameObject TargetEnemy;
    public float speed = 1;
    public float timer;
    public float duration;

    // Start is called before the first frame update
    void Awake()
    {
        Debug.Log("potato");
        timer = Time.time + duration;
        
    }
    // void Start()
    // {
    //     transform.LookAt( TargetEnemy.transform.position, new Vector3(0,0,0));
    // }

    // Update is called once per frame
    void Update()
    {
        var heading  = TargetEnemy.transform.position - gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;

        transform.position += direction * Time.deltaTime * speed;
        if(distance < 0.05f)
        {
            Destroy(gameObject);
        }
    }
}
