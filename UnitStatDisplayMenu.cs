using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UnitStatDisplayMenu : MonoBehaviour
{
    public static UnitStatDisplayMenu Instance;
    public Text TextUnitName, TextHealthName, TextCost, TextPrimary, TextSecondary, TextPrimaryStats, TextSecondaryStats, TextMArmor, TextPArmor;
    public Image Bone1, Bone2, Bone3;
    public GameObject TerrainHolder;
    public List<GameObject> templist;
    public TestCritter testCritter;
    public CritterHolder critter;

    public void Awake()
    {
        Instance = this;
        AddTextTooltip(TextUnitName, "The unit type represented by this formation.");
        AddTextTooltip(TextHealthName, "The formation's current remaining strength.");
        AddTextTooltip(TextCost, "The gold required to recruit this unit.");
        AddTextTooltip(TextPrimary, "The unit's primary ranged weapon. It is used while ammunition remains and a target is in range.");
        AddTextTooltip(TextPrimaryStats, "The damage, range, ammunition and other properties of the ranged weapon.");
        AddTextTooltip(TextSecondary, "The unit's backup melee weapon, used in close combat or when ranged attacks are unavailable.");
        AddTextTooltip(TextSecondaryStats, "The damage and other properties of the melee weapon.");
        AddTextTooltip(TextMArmor, "Body armour always reduces incoming damage. Shield armour is added according to the direction of the attack.");
        AddTextTooltip(TextPArmor, "Shield coverage controls how much shield armour applies from each direction. Actions are attempts per command round; lower initiative acts sooner; speed controls movement.");
    }

    private void OnDisable()
    {
        // A child may still own the shared tooltip when this complete panel is closed.
        if (ToolTipManager._instance != null) ToolTipManager._instance.HideToolTip();
    }
    public void LoadNewUnit(CritterHolder newcritter)
    {
        critter = newcritter;
        testCritter = newcritter.gameObject.GetComponent<TestCritter>();
        ResetUnit();
        LoadUnit();
    }
    public void LoadNewUnit(UnitSaveData unit, Material artworkMaterial = null)
    {
        if (unit == null) return;
        ResetUnit();
        ClearTerrainTraits();

        TextUnitName.text = !string.IsNullOrWhiteSpace(unit.unitname) ? unit.unitname : unit.name;
        TextHealthName.text = "Health: " + unit.health;
        TextCost.text = string.Empty;
        LoadWeapon(TextPrimary, TextPrimaryStats, "Ranged", unit.RangedWeapon);
        LoadWeapon(TextSecondary, TextSecondaryStats, "Melee", unit.MeleeWeapon);

        int bodyArmor = ArmorValue(unit.Armor);
        int shieldArmor = ArmorValue(unit.Shield);
        TextMArmor.text = "Body armor: " + bodyArmor + "%\nShield armor: " + shieldArmor + "%";
        int shieldFront = unit.Shield != null ? unit.Shield.shieldFrontEffectiveness : 0;
        int shieldSide = unit.Shield != null ? unit.Shield.shieldSideEffectiveness : 0;
        TextPArmor.text = "Shield coverage: " + shieldFront + "% front / " +
            shieldSide + "% side\nActions: " + unit.actions + "\nReaction Time: " + unit.ReactionTime;

        SetBone(Bone1, unit.bodyparts, 0, artworkMaterial);
        SetBone(Bone2, unit.bodyparts, 2, artworkMaterial);
        SetBone(Bone3, unit.bodyparts, 1, artworkMaterial);
        LoadTerrainTraits(unit.flaglist);
    }
    public void ResetUnit()
    {
        TextUnitName.text = "";
        TextHealthName.text = "";
        TextCost.text = "";
        TextPrimary.text = "";
        TextPrimaryStats.text = "";
        TextSecondary.text = "";
        TextSecondaryStats.text = "";
    }
    public void LoadUnit()
    {
        ClearTerrainTraits();

        TextUnitName.text = critter.name;
        TextHealthName.text = critter.population + " Health";
        TextCost.text = "Cost: " + critter.cost.amount.ToString();
        if (critter.RangedWeapon != null)
        {
            TextPrimary.text = critter.RangedWeapon.name;
            TextPrimaryStats.text = critter.RangedWeapon.GrabWeaponInformation();
        }
        if (critter.MeleeWeapon != null)
        {
            TextSecondary.text = critter.MeleeWeapon.name;
            TextSecondaryStats.text = critter.MeleeWeapon.GrabWeaponInformation();
        }
        TextMArmor.text = "Body armor: " + ArmorValue(critter.Armor) +
            "%\nShield armor: " + ArmorValue(critter.Shield) + "%";
        TextPArmor.text = "Shield coverage: directional values unavailable";


        Bone1.sprite = testCritter.listy[0].GetComponent<SpriteRenderer>().sprite;
        Bone2.enabled = true;
        Bone2.sprite = testCritter.listy[2].GetComponent<SpriteRenderer>().sprite;

        if (Bone2.sprite == null)
        {
            Bone2.enabled = false;
        }
        Bone3.enabled = true;
        Bone3.sprite = testCritter.listy[1].GetComponent<SpriteRenderer>().sprite;
        if (Bone3.sprite == null)
        {
            Bone3.enabled = false;
        }

        Bone1.material = testCritter.material;
        Bone2.material = testCritter.material;
        Bone3.material = testCritter.material;
        
        LoadTerrainTraits(critter.flaglist);
    }

    private void ClearTerrainTraits()
    {
        foreach (var item in templist) Destroy(item);
        templist.Clear();
    }

    private void LoadTerrainTraits(IEnumerable<string> flags)
    {
        if (flags == null) return;
        foreach (var item in flags)
        {
            TerrainFlagModifier tfm = Resources.Load<TerrainFlagModifier>("Modifiers/Terrain/TerrainFlags/" + item);
            if (tfm != null)
            {
                var tfmo = Instantiate(Resources.Load<GameObject>("Prefabs/TerrainHolder"), TerrainHolder.transform);
                RectTransform traitRect = tfmo.GetComponent<RectTransform>();
                if (traitRect != null) traitRect.sizeDelta = new Vector2(32f, 32f);
                tfmo.transform.localPosition = new Vector3(-82 + 34 * templist.Count, 0, 0);
                Tooltip traitTooltip = tfmo.GetComponent<Tooltip>();
                traitTooltip.message = tfm.flag + "\n" + tfm.tooltip;
                // The shared tooltip is roughly 300px wide, so move its centre beyond the icon.
                traitTooltip.positions = new Vector3(230f, 0f, 0f);
                if (tfm.icon != null)
                {
                    tfmo.GetComponent<Image>().sprite = tfm.icon;
                }
                templist.Add(tfmo);
            }
        }
    }

    private static void LoadWeapon(Text nameText, Text statsText, string slotName, Weapon weapon)
    {
        if (weapon == null)
        {
            nameText.text = slotName + ": None";
            statsText.text = string.Empty;
            return;
        }
        nameText.text = slotName + ": " + weapon.name;
        statsText.text = weapon.GrabWeaponInformation();
    }

    private static int ArmorValue(Weapon equipment)
    {
        if (equipment == null || equipment.armor == null) return 0;
        return Mathf.Max(equipment.armor.armor, equipment.armor.rangedarmor);
    }

    private static void AddTextTooltip(Text text, string message)
    {
        if (text == null) return;
        Tooltip tooltip = text.GetComponent<Tooltip>();
        if (tooltip == null) tooltip = text.gameObject.AddComponent<Tooltip>();
        tooltip.message = message;
    }

    private static void SetBone(Image image, List<Sprite> sprites, int index, Material material)
    {
        if (image == null) return;
        Sprite sprite = sprites != null && index >= 0 && index < sprites.Count ? sprites[index] : null;
        image.enabled = sprite != null;
        image.sprite = sprite;
        image.material = material;
        image.type = Image.Type.Sliced;
        image.preserveAspect = true;
    }
}
