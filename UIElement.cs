using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class UIElement : MonoBehaviour
{
    public static UIElement NationHost;
    public static UIElement ProvinceHost;
    public static UIElement TopBarHost;
    // Start is called before the first frame update
    void Start()
    {
        if(gameObject.name == "NationHost")
        {
            NationHost = this;
        }
        if(gameObject.name == "ProvinceHost")
        {
            ProvinceHost = this;
        }
        if(gameObject.name == "TopBarHost")
        {
            TopBarHost = this;
        }
    }
    public void UpdateTitle(string text)
    {
        transform.GetChild(0).gameObject.GetComponent<Text>().text = text;
    }
    public void UpdateDescription(string text)
    {
        transform.GetChild(1).gameObject.GetComponent<Text>().text = text;
    }
    public void UpdateDescription(Nation nation)//List<ProvinceModifier> provincemodifiers)
    {
        var texty = "Modifiers:";
        texty += "\nMaxTroops: " + nation.GrabMaxTroops().ToString();
        texty += "\nDefenceBonus: " + nation.GrabDefensiveDice().ToString();
        texty += "\nOffenceBonus: " + nation.GrabOffensiveDice().ToString();

        transform.GetChild(1).gameObject.GetComponent<Text>().text = texty;

        // foreach (var modifiers in province.provincemodifiers)
        // {
        // }
    }
    public void UpdateDescription(State state)//List<ProvinceModifier> provincemodifiers)
    {
        var texty = state.name + "\n";
        foreach (var item in state.provincelist)
        {
            texty += "\n" + item.name;
        }
        // texty += "\nMaxTroops: " + province.GrabMaxTroops().ToString();
        // texty += "\nDefenceBonus: " + province.GrabDefensiveDice().ToString();

        transform.GetChild(1).gameObject.GetComponent<Text>().text = texty;

        // foreach (var modifiers in province.provincemodifiers)
        // {
        // }
    }
    public void UpdateDescription(State state, bool potato)//List<ProvinceModifier> provincemodifiers)
    {
        string texty = "\nMaxTroops: " + state.GrabMaxTroops().ToString();

        transform.GetChild(1).gameObject.GetComponent<Text>().text = texty;

        // foreach (var modifiers in province.provincemodifiers)
        // {
        // }
    }
    public void UpdateDescription(Province province)//List<ProvinceModifier> provincemodifiers)
    {
        var texty = "Modifiers:";
        texty += "\nMaxTroops: " + province.GrabMaxTroops().ToString();
        texty += "\nDefenceBonus: " + province.GrabDefensiveDice().ToString();

        transform.GetChild(1).gameObject.GetComponent<Text>().text = texty;

        // foreach (var modifiers in province.provincemodifiers)
        // {
        // }
    }
    public void Updatethird(string text)
    {
        transform.GetChild(2).gameObject.GetComponent<Text>().text = text;
    }
    public void UpdateFourth(string text)
    {
        transform.GetChild(3).gameObject.GetComponent<TextMeshProUGUI>().text = text;
    }
    public void RaiseArmy()
    {
        if(Mapshower.Instance.SelectedProvince != null)
        {
            var a = Owners.Instance.statelist.Find(x => x.name == Mapshower.Instance.SelectedProvince.state).Capitol;
            if(a.nation.IsPlayer)
            {
                foreach (var RPC in TestRelay.Instance.PlayerObjects)
                {
                    if(RPC.GetComponent<NetworkObject>().IsLocalPlayer)
                    {
                        RPC.GetComponent<RpcTest>().SendTroops(a.name, a.name, a.nation.name);
                    }
                    //RPC.GetComponent<RpcTest>().ChangeProvinceOwner(province.name, DraggedProvince.nation.name);
                }
            }
        }
    }
    public void SettleArmy()
    {
        if(Mapshower.Instance.SelectedArmy != null)
        {
            Mapshower.Instance.SelectedArmy.SettleDown();
        }
    }
}
