using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AuraStuff : MonoBehaviour
{
    public Modifier auratoadd;
    public void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.GetComponent<CritterHolder>())
        {
            var items = Instantiate(auratoadd);
            items.potato = other.gameObject;
            items.DestroyAura();
            items.LoadAura();
            other.gameObject.GetComponent<CritterHolder>().onDeath += items.DestroyAura;
            other.gameObject.GetComponent<CritterHolder>().modifierlist.Add(items);
        }
    }
    public void OnTriggerExit2D(Collider2D other)
    {
        if(other.gameObject.GetComponent<CritterHolder>())
        {
            var a = Instantiate(auratoadd);
            var b = other.gameObject.GetComponent<CritterHolder>().modifierlist.Find(x => x.name == a.name);
            if(b != null)
            {
                other.gameObject.GetComponent<CritterHolder>().modifierlist.Remove(b);
                b.DestroyThis();
            }
            other.gameObject.GetComponent<CritterHolder>().HandleModifiers();
        }
    }
}
