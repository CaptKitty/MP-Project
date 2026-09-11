using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectX.SectorBattle
{
    /// <summary>Connects the authored ScenarioScene controls to the real sector battle simulation.</summary>
    public sealed class ScenarioBattleDesigner : MonoBehaviour
    {
        private sealed class Row
        {
            public GameObject Root;
            public Dropdown UnitPicker, GroupPicker, LanePicker;
            public InputField Amount;
            public Image Portrait;
            public UnitSaveData Unit;
            public BattleLane Lane = BattleLane.Centre;
        }
        private sealed class Team
        {
            public int Side;
            public Dropdown TemplatePicker, FactionPicker, TacticPicker;
            public Transform Composition;
            public GameObject Prototype;
            public Button AddButton;
            public FactionArmyTemplate[] Templates;
            public Faction Faction;
            public readonly List<Row> Rows = new List<Row>();
            public Material Material;
        }

        private readonly Team[] teams = { new Team { Side = 0 }, new Team { Side = 1 } };
        private UnitSaveData[] units;
        private Faction[] factions;
        private Material baseMaterial;
        private Canvas canvas;
        private Button startButton;
        private Dropdown mapPicker;
        private GameObject tooltip;
        private Coroutine tooltipDelay;

        private void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            units = Resources.LoadAll<UnitSaveData>("Prefabs/Units/NormieData")
                .Where(unit => unit != null).OrderBy(UnitName).ToArray();
            factions = Resources.LoadAll<Faction>("Prefabs/NationData/Factions").Where(f => f != null)
                .GroupBy(f => f.name).Select(g => g.First()).OrderBy(f => f.name).ToArray();
            baseMaterial = FindUnitMaterial();
            Transform designer = transform.Find("Battle Designer");
            if (designer == null) { Debug.LogError("ScenarioBattleDesigner: Battle Designer was not found."); return; }
            BindTeam(teams[0], designer.Find("TeamA"));
            BindTeam(teams[1], designer.Find("TeamB"), teams[0].Prototype);
            Transform mapLauncher = designer.Find("MapLauncher");
            mapPicker = mapLauncher != null
                ? mapLauncher.Find("SelectedMapType/MapTypeSelector")?.GetComponent<Dropdown>()
                : null;
            if (mapPicker == null)
                mapPicker = GetComponentsInChildren<Dropdown>(true).FirstOrDefault(item =>
                    item.name == "MapTypeSelector" || item.name == "SelectedMapType" || item.name == "SelectedMap");
            SetOptions(mapPicker, new[] { "Open Grassland", "Mountain Pass", "Dense Forest" });
            if (mapPicker != null) { mapPicker.SetValueWithoutNotify(0); mapPicker.RefreshShownValue(); }
            startButton = mapLauncher != null ? mapLauncher.Find("Start Battle")?.GetComponent<Button>() : null;
            if (startButton == null)
                startButton = GetComponentsInChildren<Button>(true).FirstOrDefault(item => item.name == "Start Battle" || item.name == "StartBattle");
            if (startButton != null) { startButton.onClick.RemoveListener(StartBattle); startButton.onClick.AddListener(StartBattle); }
            else Debug.LogError("ScenarioBattleDesigner: Start Battle button was not found.");
            UpdateStartButton();
        }

        private void BindTeam(Team team, Transform root, GameObject sharedPrototype = null)
        {
            if (root == null) { Debug.LogError("ScenarioBattleDesigner: Team " + team.Side + " was not found."); return; }
            team.TemplatePicker = root.Find("Header/Prefab/PrefabPicker")?.GetComponent<Dropdown>();
            team.FactionPicker = root.Find("Header/Faction/FactionPicker")?.GetComponent<Dropdown>();
            team.TacticPicker = root.Find("Header/Tactic")?.GetComponent<Dropdown>();
            team.Composition = root.Find("ArmyComposition");
            if (team.Composition == null || team.Composition.childCount == 0) return;
            GameObject localPrototype = team.Composition.GetChild(0).gameObject;
            localPrototype.SetActive(false);
            team.Prototype = sharedPrototype != null ? sharedPrototype : localPrototype;
            team.AddButton = team.Composition.Find("AddUnit")?.GetComponent<Button>();
            if (team.AddButton != null)
            {
                team.AddButton.onClick.RemoveAllListeners();
                team.AddButton.onClick.AddListener(() => AddRow(team, null, 1, BattleLane.Centre, NextDefaultGroup(team)));
            }
            team.Templates = Resources.LoadAll<FactionArmyTemplate>("FactionArmyTemplates/Scenarios")
                .Where(t => t != null).OrderBy(t => t.name).ToArray();
            SetOptions(team.TemplatePicker, team.Templates.Select(t => t.name));
            SetOptions(team.FactionPicker, factions.Select(f => f.name));
            SetOptions(team.TacticPicker, TacticNames());
            if (team.FactionPicker != null)
            {
                team.FactionPicker.onValueChanged.RemoveAllListeners();
                team.FactionPicker.onValueChanged.AddListener(index => SelectFaction(team, index));
            }
            if (team.TemplatePicker != null)
            {
                team.TemplatePicker.onValueChanged.RemoveAllListeners();
                team.TemplatePicker.onValueChanged.AddListener(index => LoadTemplate(team, index));
            }
            SelectFaction(team, team.FactionPicker != null ? team.FactionPicker.value : 0);
            if (team.Templates.Length > 0) LoadTemplate(team, team.TemplatePicker != null ? team.TemplatePicker.value : 0);
            else AddRow(team, units.FirstOrDefault(), 1, BattleLane.Centre, 1);
        }

        private void LoadTemplate(Team team, int index)
        {
            if (index >= 0 && index < team.Templates.Length) EnsureOtherTeamUsesDifferentTemplate(team, team.Templates[index]);
            ClearRows(team);
            if (index < 0 || index >= team.Templates.Length) return;
            FactionArmyTemplate template = team.Templates[index];
            if (template.faction != null && team.FactionPicker != null)
            {
                int factionIndex = Array.IndexOf(factions, template.faction);
                if (factionIndex < 0) factionIndex = Array.FindIndex(factions, f => f.name == template.faction.name);
                if (factionIndex >= 0) team.FactionPicker.value = factionIndex;
            }
            foreach (SectorCustomFormationSpec spec in template.formations)
                if (spec != null && spec.Unit != null && spec.Count > 0)
                    AddRow(team, spec.Unit, spec.Count, spec.Lane, NextDefaultGroup(team));
            if (team.TacticPicker != null)
                team.TacticPicker.SetValueWithoutNotify((int)template.tactic + 1);
            UpdateStartButton();
        }

        private void EnsureOtherTeamUsesDifferentTemplate(Team selectedTeam, FactionArmyTemplate selectedTemplate)
        {
            Team other = teams.FirstOrDefault(item => item != selectedTeam);
            if (other == null || other.TemplatePicker == null || other.Templates == null || other.Templates.Length < 2) return;
            FactionArmyTemplate otherSelection = SelectedTemplate(other);
            if (otherSelection != selectedTemplate) return;
            int current = Array.IndexOf(other.Templates, selectedTemplate);
            int replacement = (Mathf.Max(0, current) + 1) % other.Templates.Length;
            other.TemplatePicker.SetValueWithoutNotify(replacement);
            LoadTemplate(other, replacement);
        }

        private void AddRow(Team team, UnitSaveData unit, int amount, BattleLane lane, int group)
        {
            GameObject root = Instantiate(team.Prototype, team.Composition);
            root.name = "ArmyUnit - " + UnitName(unit); root.SetActive(true);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 425f - team.Rows.Count * 55f);
            Row row = new Row { Root = root, Unit = unit, Lane = lane };
            row.UnitPicker = root.transform.Find("UnitSelector")?.GetComponent<Dropdown>();
            row.Amount = root.transform.Find("UnitAmount")?.GetComponent<InputField>();
            row.LanePicker = root.transform.Find("LanePosition")?.GetComponent<Dropdown>();
            Transform portraitHost = root.transform.Find("LoadBodySpriteHere");
            row.GroupPicker = root.transform.Find("CommandGroup")?.GetComponent<Dropdown>();
            Button remove = root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.transform.parent == root.transform);
            SetOptions(row.UnitPicker, new[] { "Select Unit" }.Concat(units.Select(UnitName)));
            int unitIndex = Array.IndexOf(units, unit);
            if (row.UnitPicker != null) row.UnitPicker.SetValueWithoutNotify(unitIndex >= 0 ? unitIndex + 1 : 0);
            SetOptions(row.GroupPicker, Enumerable.Range(1, 10).Select(i => i.ToString()));
            if (row.GroupPicker != null) row.GroupPicker.SetValueWithoutNotify(Mathf.Clamp(group, 1, 10) - 1);
            SetOptions(row.LanePicker, LaneNames());
            if (row.LanePicker != null)
            {
                row.LanePicker.SetValueWithoutNotify(Mathf.Clamp((int)lane, 0, 4));
                row.LanePicker.onValueChanged.AddListener(i => row.Lane = (BattleLane)Mathf.Clamp(i, 0, 4));
            }
            if (row.Amount != null) row.Amount.SetTextWithoutNotify(Mathf.Max(1, amount).ToString());
            if (row.UnitPicker != null) row.UnitPicker.onValueChanged.AddListener(i => { row.Unit = ValidUnit(i); RefreshPortrait(team, row); });
            if (row.Amount != null) row.Amount.onEndEdit.AddListener(value => { NormalizeAmount(row, value); UpdateStartButton(); });
            if (remove != null) remove.onClick.AddListener(() => RemoveRow(team, row));
            if (portraitHost != null)
            {
                GameObject artObject = new GameObject("Unit Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayeredBattleUnitVisual), typeof(ScenarioUnitHover));
                artObject.transform.SetParent(portraitHost, false);
                RectTransform artRect = artObject.GetComponent<RectTransform>(); artRect.anchorMin = Vector2.zero; artRect.anchorMax = Vector2.one; artRect.offsetMin = artRect.offsetMax = Vector2.zero;
                row.Portrait = artObject.GetComponent<Image>();
                artObject.GetComponent<ScenarioUnitHover>().Bind(() => BeginHover(team, row), EndHover);
            }
            team.Rows.Add(row); RefreshPortrait(team, row); RefreshRowPositions(team); UpdateStartButton();
        }

        private void SelectFaction(Team team, int index)
        {
            team.Faction = index >= 0 && index < factions.Length ? factions[index] : null;
            if (team.Material != null) Destroy(team.Material);
            team.Material = MakeFactionMaterial(team.Faction, team.Side);
            foreach (Row row in team.Rows) RefreshPortrait(team, row);
        }

        private void RefreshPortrait(Team team, Row row)
        {
            if (row.Portrait == null) return;
            if (row.Unit == null) { row.Portrait.sprite = null; row.Portrait.color = Color.clear; return; }
            row.Portrait.GetComponent<LayeredBattleUnitVisual>().Configure(row.Unit, team.Material);
        }

        private void NormalizeAmount(Row row, string value)
        {
            int amount; if (!int.TryParse(value, out amount)) amount = 1;
            row.Amount.SetTextWithoutNotify(Mathf.Max(1, amount).ToString());
        }

        private void RemoveRow(Team team, Row row)
        { HideTooltip(); team.Rows.Remove(row); Destroy(row.Root); RefreshRowPositions(team); UpdateStartButton(); }

        private void ClearRows(Team team)
        { HideTooltip(); foreach (Row row in team.Rows) if (row.Root != null) Destroy(row.Root); team.Rows.Clear(); RefreshRowPositions(team); }

        private static void RefreshRowPositions(Team team)
        {
            for (int i = 0; i < team.Rows.Count; i++)
            {
                RectTransform row = team.Rows[i].Root != null ? team.Rows[i].Root.GetComponent<RectTransform>() : null;
                if (row != null) row.anchoredPosition = new Vector2(row.anchoredPosition.x, 425f - i * 55f);
            }
            if (team.AddButton != null)
            {
                RectTransform add = team.AddButton.GetComponent<RectTransform>();
                add.anchoredPosition = new Vector2(0f, 425f - team.Rows.Count * 55f);
                team.AddButton.transform.SetAsLastSibling();
            }
        }

        private void StartBattle()
        {
            List<SectorCustomFormationSpec> a = BuildSpecs(teams[0]), b = BuildSpecs(teams[1]);
            int aCount = a.Sum(s => s.Count), bCount = b.Sum(s => s.Count);
            if (aCount <= 0 || bCount <= 0)
            { Debug.LogWarning("Start Battle requires at least one troop on both Team A and Team B."); return; }
            FactionArmyTemplate templateA = SelectedTemplate(teams[0]), templateB = SelectedTemplate(teams[1]);
            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            SectorBattleSimulation simulation = SectorCustomBattleFactory.PrepareGenerated(a, b, seed, 10, 10,
                templateA != null ? templateA.generalName : "Team A General", templateB != null ? templateB.generalName : "Team B General",
                SelectedTactic(teams[0], templateA), SelectedTactic(teams[1], templateB),
                IsPlayerControlled(teams[0]), IsPlayerControlled(teams[1]));
            simulation.ApplyMapType(mapPicker != null
                ? (SectorBattleMapType)Mathf.Clamp(mapPicker.value, 0, 2)
                : SectorBattleMapType.OpenGrassland);
            simulation.StartBattle();
            StartCoroutine(OpenBattleWhenReady(simulation, seed));
        }

        private IEnumerator OpenBattleWhenReady(SectorBattleSimulation simulation, ulong seed)
        {
            while (SectorBattleCampaignManager.Instance == null || SectorBattlePresentation.Instance == null) yield return null;
            SectorCampaignBattle battle = SectorBattleCampaignManager.Instance.RegisterCustomBattle(simulation, "Scenario Battle " + seed,
                teams[0].Faction != null ? teams[0].Faction.name : "Team A", teams[1].Faction != null ? teams[1].Faction.name : "Team B",
                teams[0].Faction, teams[1].Faction);
            SectorBattlePresentation.Instance.OpenViewer(battle);
        }

        private List<SectorCustomFormationSpec> BuildSpecs(Team team)
        {
            List<SectorCustomFormationSpec> result = new List<SectorCustomFormationSpec>();
            foreach (Row row in team.Rows)
            {
                int amount; if (row.Unit == null || row.Amount == null || !int.TryParse(row.Amount.text, out amount) || amount <= 0) continue;
                result.Add(new SectorCustomFormationSpec { Unit = row.Unit, Count = amount, Lane = row.Lane,
                    CommandGroup = row.GroupPicker != null ? row.GroupPicker.value + 1 : 1 });
            }
            return result;
        }

        private void UpdateStartButton()
        {
            if (startButton == null) return;
            startButton.interactable = BuildSpecs(teams[0]).Sum(s => s.Count) > 0 && BuildSpecs(teams[1]).Sum(s => s.Count) > 0;
        }

        private void BeginHover(Team team, Row row)
        { HideTooltip(); tooltipDelay = StartCoroutine(ShowTooltipAfterDelay(team, row)); }
        private void EndHover() { HideTooltip(); }
        private IEnumerator ShowTooltipAfterDelay(Team team, Row row)
        {
            yield return new WaitForSecondsRealtime(1f);
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UnitStatDisplayMenu");
            if (prefab == null || row.Unit == null || canvas == null) yield break;
            tooltip = Instantiate(prefab, canvas.transform, false); tooltip.name = "Scenario Unit Tooltip";
            tooltip.GetComponent<UnitStatDisplayMenu>()?.LoadNewUnit(row.Unit, team.Material);
            RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();
            if (tooltipRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, Input.mousePosition,
                canvas.worldCamera, out Vector2 local)) tooltipRect.anchoredPosition = local + new Vector2(tooltipRect.rect.width * .55f, 0f);
            tooltipDelay = null;
        }
        private void HideTooltip()
        { if (tooltipDelay != null) { StopCoroutine(tooltipDelay); tooltipDelay = null; } if (tooltip != null) { Destroy(tooltip); tooltip = null; } }

        private static void SetOptions(Dropdown dropdown, IEnumerable<string> options)
        { if (dropdown == null) return; dropdown.ClearOptions(); dropdown.AddOptions(options.ToList()); dropdown.RefreshShownValue(); }
        private static IEnumerable<string> TacticNames()
        { yield return "Player"; yield return "Winged Center"; yield return "Focused Center"; yield return "Double Envelopment"; yield return "Single Flank"; }
        private static IEnumerable<string> LaneNames()
        { yield return "Top Flank"; yield return "Upper Wing"; yield return "Centre"; yield return "Lower Wing"; yield return "Bottom Flank"; }
        private static int NextDefaultGroup(Team team) => team.Rows.Count % 10 + 1;
        private static bool IsPlayerControlled(Team team) => team.TacticPicker != null && team.TacticPicker.value == 0;
        private static SectorGeneralTactic SelectedTactic(Team team, FactionArmyTemplate template)
        {
            if (team.TacticPicker == null) return template != null ? template.tactic : SectorGeneralTactic.WingedCenter;
            return team.TacticPicker.value <= 0 ? SectorGeneralTactic.WingedCenter :
                (SectorGeneralTactic)Mathf.Clamp(team.TacticPicker.value - 1, 0, Enum.GetValues(typeof(SectorGeneralTactic)).Length - 1);
        }
        private UnitSaveData ValidUnit(int index) => index > 0 && index <= units.Length ? units[index - 1] : null;
        private static string UnitName(UnitSaveData unit) => unit == null ? "Missing Unit" : string.IsNullOrWhiteSpace(unit.unitname) ? unit.name : unit.unitname;
        private static FactionArmyTemplate SelectedTemplate(Team team) => team.TemplatePicker != null && team.TemplatePicker.value >= 0 && team.TemplatePicker.value < team.Templates.Length ? team.Templates[team.TemplatePicker.value] : null;
        private static Material FindUnitMaterial() => Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m != null && m.name == "New Material 1");
        private Material MakeFactionMaterial(Faction faction, int side)
        {
            if (baseMaterial == null) return null;
            Material material = new Material(baseMaterial) { name = "Scenario Team " + side };
            Color primary = faction != null ? faction.color : side == 0 ? Color.red : Color.blue;
            Color secondary = faction != null ? faction.color2 : Color.yellow;
            Color skin = faction != null ? faction.color3 : new Color(.75f, .49f, .31f);
            if (material.HasProperty("_FactionColor")) material.SetColor("_FactionColor", primary);
            if (material.HasProperty("_FactionColor2")) material.SetColor("_FactionColor2", secondary);
            if (material.HasProperty("_FactionColor3")) material.SetColor("_FactionColor3", skin);
            return material;
        }

    }

    public sealed class ScenarioUnitHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action enter, exit;
        public void Bind(Action onEnter, Action onExit) { enter = onEnter; exit = onExit; }
        public void OnPointerEnter(PointerEventData eventData) { enter?.Invoke(); }
        public void OnPointerExit(PointerEventData eventData) { exit?.Invoke(); }
    }
}
