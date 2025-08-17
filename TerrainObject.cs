using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainObject : MonoBehaviour
{
    public Modifier modifier;
    public string immunityflag;
    public string EffectFlag = "";
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.GetComponent<CritterHolder>() != null)
        {
            //if (EffectFlag == "")
            //{
                foreach (var item in collision.gameObject.GetComponent<CritterHolder>().flaglist)
                {
                    if (item == immunityflag)
                    {
                        return;
                    }
                }

                collision.gameObject.GetComponent<CritterHolder>().modifierlist.Add(modifier);
                foreach (var items in collision.gameObject.GetComponent<CritterHolder>().modifierlist)
                {
                    items.potato = collision.gameObject;
                    items.DestroyAura();
                    items.LoadAura();
                    collision.gameObject.GetComponent<CritterHolder>().onDeath += items.DestroyAura;
                }
            // }
            // else
            // {
                // foreach (var item in collision.gameObject.GetComponent<CritterHolder>().flaglist)
                // {
                //     if (IsEffect(item, collision))
                //     {
                //         collision.gameObject.GetComponent<CritterHolder>().modifierlist.Add(modifier);
                //         foreach (var items in collision.gameObject.GetComponent<CritterHolder>().modifierlist)
                //         {
                //             items.potato = collision.gameObject;
                //             items.DestroyAura();
                //             items.LoadAura();
                //             collision.gameObject.GetComponent<CritterHolder>().onDeath += items.DestroyAura;
                //         }
                //         return;
                //     }
                // }
            //}
            
        }
    }
    public bool IsEffect(string item, Collider2D collision)
    {
        if (EffectFlag == item)
        {
            return true;
        }
        if (EffectFlag == "Ranged")
        {
            if (collision.gameObject.GetComponent<CritterHolder>().RangedWeapon.ammo > 0)
            {
                return true;
            }
        }
        return false;
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.GetComponent<CritterHolder>() != null)
        {
            collision.gameObject.GetComponent<CritterHolder>().modifierlist.Remove(modifier);
            foreach (var items in collision.gameObject.GetComponent<CritterHolder>().modifierlist)
            {
                items.potato = collision.gameObject;
                items.DestroyAura();
                items.LoadAura();
                collision.gameObject.GetComponent<CritterHolder>().onDeath += items.DestroyAura;
            }
        }
    }
}
