using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "UpgradeModule/Basic")]
public class UpgradeModule : ScriptableObject
{
    public string name;
    public Modifier modifier;
    public Weapon NewRangedWeapon;
    public Weapon NewMeleeWeapon;
    public Weapon NewArmors;
    public Weapon NewShields;
    public Sprite NewArmor;
    public Sprite NewShield;
    public UpgradeModule GrabUpgradeModule()
    {
        var potato = new UpgradeModule();
        potato.name = name;
        potato.modifier = modifier;
        potato.NewRangedWeapon = NewRangedWeapon;
        potato.NewMeleeWeapon = NewMeleeWeapon;
        potato.NewArmors = NewArmors;
        potato.NewShields = NewShields;
        
        potato.NewArmor = NewArmor;
        potato.NewShield = NewShield;
        return potato;
    }
    public bool CanUpgrade()
    {
        return true;
    }
}
