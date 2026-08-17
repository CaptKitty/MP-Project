using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UIElement : MonoBehaviour
{
    public static UIElement NationHost;
    public static UIElement ProvinceHost;
    public static UIElement ArmyHost;
    private Text currencyText;
    private Text armyNameText;
    private Text armyCompositionText;
    private Text armyCommanderText;
    private string currencyTemplate;
    private string armyNameTemplate;
    private string armyCompositionTemplate;
    private string armyCommanderTemplate;
    private int lastGold = int.MinValue;
    private FieldArmyHolder lastArmy;

    private void Awake()
    {
        if (gameObject.name == "NationHost")
        {
            NationHost = this;
        }
        if (gameObject.name == "ProvinceHost")
        {
            ProvinceHost = this;
        }
        if (gameObject.name == "ArmyHost")
        {
            ResolveArmyPanel();
            // MapScene still contains an older ArmyHost. Prefer the one carrying
            // the named campaign army-panel fields.
            if (ArmyHost == null || currencyText != null) ArmyHost = this;
        }
    }

    private void Start()
    {
        if (gameObject.name == "ArmyHost") RefreshArmyPanel(true);
    }

    private void Update()
    {
        if (ArmyHost != this) return;
        FieldArmyHolder selected = SelectedArmy();
        int gold = LocalNation() != null ? LocalNation().Gold : 0;
        if (selected != lastArmy || gold != lastGold) RefreshArmyPanel(true);
    }

    private void ResolveArmyPanel()
    {
        currencyText = FindNamedText("Currency");
        armyNameText = FindNamedText("Armyname");
        armyCompositionText = FindNamedText("ArmyComposition");
        armyCommanderText = FindNamedText("ArmyCommander") ?? FindNamedText("ArmyGeneral");
        if (currencyText != null) currencyTemplate = currencyText.text;
        if (armyNameText != null) armyNameTemplate = armyNameText.text;
        if (armyCompositionText != null) armyCompositionTemplate = armyCompositionText.text;
        if (armyCommanderText != null) armyCommanderTemplate = armyCommanderText.text;
    }

    private Text FindNamedText(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
            if (descendants[i].name == objectName) return descendants[i].GetComponent<Text>();
        return null;
    }

    private static FieldArmyHolder SelectedArmy()
    {
        return FieldArmyHolder.InspectedArmy != null
            ? FieldArmyHolder.InspectedArmy
            : FieldArmyHolder.SelectedPlayerArmy != null
            ? FieldArmyHolder.SelectedPlayerArmy
            : FieldArmyHolder.PlayerFieldArmy;
    }

    private static Nation LocalNation()
    {
        string nationName = CampaignNetworkPlayer.Local != null
            ? CampaignNetworkPlayer.Local.AssignedNation
            : string.Empty;
        if (string.IsNullOrEmpty(nationName) && SessionManager.Instance != null && SessionManager.Instance.HostFaction != null)
            nationName = SessionManager.Instance.HostFaction.name;
        if (Owners.Instance != null && !string.IsNullOrEmpty(nationName))
            return Owners.Instance.nationlist.Find(nation => nation != null && nation.name == nationName);
        FieldArmyHolder army = SelectedArmy();
        return army != null && army.fieldArmy != null ? army.fieldArmy.nation : null;
    }

    public void RefreshArmyPanel(bool includeComposition)
    {
        if (currencyText == null && armyNameText == null) ResolveArmyPanel();
        Nation localNation = LocalNation();
        FieldArmyHolder army = SelectedArmy();
        lastGold = localNation != null ? localNation.Gold : 0;
        lastArmy = army;

        SetTemplateText(currencyText, currencyTemplate, localNation != null ? localNation.Gold.ToString() : "0");
        SetTemplateText(armyNameText, armyNameTemplate, army != null ? army.gameObject.name : "No army selected");
        if (includeComposition) SetTemplateText(armyCompositionText, armyCompositionTemplate, CompositionOf(army));
        SetTemplateText(armyCommanderText, armyCommanderTemplate, CommanderOf(army));
    }

    private static void SetTemplateText(Text target, string template, string value)
    {
        if (target == null) return;
        target.text = !string.IsNullOrEmpty(template) && template.Contains("<X>")
            ? template.Replace("<X>", value ?? string.Empty)
            : value ?? string.Empty;
    }

    private static string CompositionOf(FieldArmyHolder army)
    {
        if (army == null || army.fieldArmy == null) return "None";
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < army.fieldArmy.USDReserves.Count; i++)
        {
            ArmyReserves reserve = army.fieldArmy.USDReserves[i];
            if (reserve == null || reserve.USD == null || reserve.amount <= 0) continue;
            if (text.Length > 0) text.Append('\n');
            text.Append(reserve.amount).Append("X : ").Append(reserve.USD.name);
        }
        return text.Length > 0 ? text.ToString() : "None";
    }

    private static string CommanderOf(FieldArmyHolder army)
    {
        if (army == null) return "None";
        ProjectX.TileBattle.TileGeneralPersonality personality =
            ProjectX.TileBattle.TileBattleCampaignAdapter.CreatePersonality(army);
        StringBuilder text = new StringBuilder(personality.Name);
        AppendTrait(text, "Aggressive", personality.Aggressive);
        AppendTrait(text, "Defensive", personality.Defensive);
        AppendTrait(text, "Cautious", personality.Cautious);
        AppendTrait(text, "Opportunistic", personality.Opportunistic);
        AppendTrait(text, "Cavalry Commander", personality.CavalryMinded);
        AppendTrait(text, "Methodical", personality.Methodical);
        AppendTrait(text, "Bold", personality.Bold);
        AppendTrait(text, "Patient", personality.Patient);
        AppendTrait(text, "Stubborn", personality.Stubborn);
        return text.ToString();
    }

    private static void AppendTrait(StringBuilder text, string name, int value)
    {
        if (value > 0) text.Append('\n').Append(name);
    }
    public void UpdateTitle(string text, string supply = "")
    {
        if (this == ArmyHost && armyNameText != null)
            SetTemplateText(armyNameText, armyNameTemplate, text);
        else if (transform.childCount > 0)
            transform.GetChild(0).gameObject.GetComponent<Text>().text = text;
    }
    public void UpdateSecond(string text, string supply = "")
    {
        if (this != ArmyHost && transform.childCount > 1)
            transform.GetChild(1).gameObject.GetComponent<Text>().text = supply + "\n Supply Available";
    }
    public void UpdateThree(string text, string supply = "")
    {
        if (this == ArmyHost && armyCompositionText != null)
            SetTemplateText(armyCompositionText, armyCompositionTemplate, text.TrimEnd());
        else if (transform.childCount > 2)
            transform.GetChild(2).gameObject.GetComponent<Text>().text = text;
    }
}
