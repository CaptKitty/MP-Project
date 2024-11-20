using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeButton : MonoBehaviour
{
    public TestCritter testy = new TestCritter();
    public static UpgradeButton Instance;
    public void Awake()
    {
        // if(UpgradeButton.Instance != null)
        // {
        //     Destroy(UpgradeButton.Instance.gameObject);
        // }
        // Instance = this;
    }
    public void OnMouseDown()
    {
        //testy.UpgradeTroop(testy.Upgrade);
        Destroy(this.gameObject);
    }
    public void OnMouseExit()
    {
        Destroy(this.gameObject);
    }
}
