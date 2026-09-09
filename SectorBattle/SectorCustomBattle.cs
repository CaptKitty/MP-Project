using System;
using System.Collections.Generic;
using ProjectX.CombatBackend;
using UnityEngine;

namespace ProjectX.SectorBattle
{
    public static class SectorCustomBattleLaunchRequest
    {
        private static bool requested;
        public static void Request() { requested = true; }
        public static bool Consume() { bool value = requested; requested = false; return value; }
    }

    public enum SectorBattlePreset : byte { Basic10v10, CentreConcentration, FlankingTest, CounterFlankTest, CommandCapacity20v20 }

    public static class SectorCustomBattleFactory
    {
        public static SectorBattleSimulation Prepare(SectorBattlePreset preset, ulong seed, int capacityA, int capacityB,
            bool playerA, bool playerB)
        {
            BattleSimulationRequest request = new BattleSimulationRequest { BattleId = "custom-" + preset + "-" + seed, Seed = seed };
            request.Commanders.Add(new BattleSideCommandConfig { Side = 0, GeneralName = "Roman Test General", CommandGroupCapacity = Mathf.Clamp(capacityA, 4, 8), PlayerControlled = playerA });
            request.Commanders.Add(new BattleSideCommandConfig { Side = 1, GeneralName = "Carthaginian Test General", CommandGroupCapacity = Mathf.Clamp(capacityB, 4, 8), PlayerControlled = playerB });
            bool large = preset == SectorBattlePreset.CommandCapacity20v20;
            int id = 1;
            Add(request, ref id, 0, "LegionaryLevy", large ? 10 : 4, BattleLane.Centre);
            Add(request, ref id, 0, "Velite", large ? 6 : 2, BattleLane.LowerWing);
            Add(request, ref id, 0, "Cavalry_Light", large ? 4 : 2, BattleLane.UpperWing);
            if (!large) Add(request, ref id, 0, "LegionaryLight", 2, BattleLane.Centre);
            id = 1001;
            Add(request, ref id, 1, "Phoenician Spear", large ? 8 : 3, BattleLane.Centre);
            Add(request, ref id, 1, "Iberian Light", large ? 0 : 2, BattleLane.UpperWing);
            Add(request, ref id, 1, "Iberian_SpearChucker", large ? 6 : 2, BattleLane.LowerWing);
            Add(request, ref id, 1, "Cavalry_Shock", large ? 4 : 2, preset == SectorBattlePreset.CounterFlankTest ? BattleLane.LowerWing : BattleLane.UpperWing);
            Add(request, ref id, 1, "Numidian_Charioteer_Spear", large ? 2 : 1, BattleLane.LowerWing);
            SectorBattleSimulation simulation = new SectorBattleSimulation(); simulation.Initialize(request);
            if (preset == SectorBattlePreset.FlankingTest || preset == SectorBattlePreset.CounterFlankTest)
                simulation.SetTerrain(new SectorCoord(BattleLane.UpperWing, BattleDepth.CentralGround), SectorTerrain.DryPlain);
            else if (preset == SectorBattlePreset.CentreConcentration)
                simulation.SetTerrain(new SectorCoord(BattleLane.Centre, BattleDepth.CentralGround), SectorTerrain.Hill);
            return simulation;
        }

        private static void Add(BattleSimulationRequest request, ref int id, int side, string resource, int count, BattleLane column)
        {
            if (count <= 0) return;
            UnitSaveData unit = Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/" + resource);
            if (unit == null) throw new InvalidOperationException("Missing custom battle unit: " + resource);
            for (int i = 0; i < count; i++) request.Formations.Add(new BattleFormationInput { FormationId = id++, Side = side,
                Unit = unit, Strength = unit.health, DeploymentColumn = (int)column,
                DeploymentRow = side == 0 ? (int)BattleDepth.SideAReserve : (int)BattleDepth.SideBReserve });
        }
    }

    /// <summary>Prototype IMGUI harness. It constructs the same simulation used by campaign battles.</summary>
    public sealed class SectorCustomBattleLauncher : MonoBehaviour
    {
        private SectorBattleCampaignManager manager; private SectorBattlePresentation presentation;
        private SectorBattleSimulation prepared; private bool visible; private Rect window = new Rect(80, 70, 620, 720);
        private string seedText = "1"; private ulong lastSeed = 1; private int preset, capacityA = 6, capacityB = 6;
        private bool playerA = true, playerB; private Vector2 scroll;
        private GUIStyle readableButton;
        public void Initialize(SectorBattleCampaignManager owner, SectorBattlePresentation view) { manager = owner; presentation = view; }
        public void Show() { visible = true; }
        private void Update() { if (Input.GetKeyDown(KeyCode.F8)) visible = !visible; }
        private void OnGUI()
        {
            if (!visible) return;
            if (readableButton == null)
            {
                readableButton = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                readableButton.normal.textColor = readableButton.hover.textColor = readableButton.active.textColor = Color.white;
                readableButton.focused.textColor = readableButton.onNormal.textColor = readableButton.onHover.textColor = Color.white;
                readableButton.onActive.textColor = readableButton.onFocused.textColor = Color.white;
            }
            window = GUI.Window(746214, window, DrawWindow, "Custom Sector Battle");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Preset"); preset = GUILayout.SelectionGrid(preset, Enum.GetNames(typeof(SectorBattlePreset)), 1, readableButton);
            GUILayout.BeginHorizontal(); GUILayout.Label("Seed", GUILayout.Width(60)); seedText = GUILayout.TextField(seedText, GUILayout.Width(180));
            if (GUILayout.Button("Random seed", readableButton)) seedText = ((ulong)DateTime.UtcNow.Ticks).ToString(); GUILayout.EndHorizontal();
            capacityA = Capacity("Side A command capacity", capacityA); capacityB = Capacity("Side B command capacity", capacityB);
            bool nextPlayerA = GUILayout.Toggle(playerA, "Side A: Player controlled");
            bool nextPlayerB = GUILayout.Toggle(playerB, "Side B: Player controlled");
            if (nextPlayerA != playerA) { playerA = nextPlayerA; prepared?.Commands.SetPlayerControlled(0, playerA); }
            if (nextPlayerB != playerB) { playerB = nextPlayerB; prepared?.Commands.SetPlayerControlled(1, playerB); }
            if (GUILayout.Button("Prepare battle", readableButton)) Prepare();
            if (prepared != null)
            {
                GUILayout.Label("Command Groups (membership editable until Start)"); scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(330));
                for (int i = 0; i < prepared.CommandGroups.Count; i++) DrawGroup(prepared.CommandGroups[i]);
                GUILayout.EndScrollView();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Start battle", readableButton)) StartPrepared();
                if (GUILayout.Button("Restart same seed", readableButton)) { Prepare(lastSeed); StartPrepared(); }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("F8 toggles this window. Formations deploy in their reserve across the centre and two wings.");
            if (GUILayout.Button("Close", readableButton)) visible = false; GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private int Capacity(string label, int value)
        { GUILayout.BeginHorizontal(); GUILayout.Label(label); if (GUILayout.Button("-", readableButton, GUILayout.Width(28))) value--; GUILayout.Label(value.ToString(), GUILayout.Width(24)); if (GUILayout.Button("+", readableButton, GUILayout.Width(28))) value++; GUILayout.EndHorizontal(); return Mathf.Clamp(value, 4, 8); }
        private void Prepare() { if (!ulong.TryParse(seedText, out lastSeed)) lastSeed = 1; Prepare(lastSeed); }
        private void Prepare(ulong seed) { lastSeed = seed; seedText = seed.ToString(); prepared = SectorCustomBattleFactory.Prepare((SectorBattlePreset)preset, seed, capacityA, capacityB, playerA, playerB); }
        private void DrawGroup(SectorCommandGroup group)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label("S" + group.Side + "  " + group.DisplayName + " [" + group.MemberFormationIds.Count + "]", GUILayout.Width(300));
            if (group.MemberFormationIds.Count > 1 && GUILayout.Button("Split", readableButton, GUILayout.Width(70))) prepared.Commands.CreateGroup(group.Side, group.MemberFormationIds[group.MemberFormationIds.Count - 1]);
            SectorCommandGroup compatible = null; for (int i = 0; i < prepared.CommandGroups.Count; i++) if (prepared.CommandGroups[i] != group && prepared.CommandGroups[i].Side == group.Side && SectorCommandController.Compatible(prepared.CommandGroups[i].Role, group.Role)) { compatible = prepared.CommandGroups[i]; break; }
            GUI.enabled = compatible != null;
            if (GUILayout.Button("Move 1", readableButton, GUILayout.Width(70))) prepared.Commands.MoveMember(group.MemberFormationIds[group.MemberFormationIds.Count - 1], compatible.GroupId);
            if (GUILayout.Button("Merge", readableButton, GUILayout.Width(70))) prepared.Commands.Merge(group.GroupId, compatible.GroupId);
            GUI.enabled = true; GUILayout.EndHorizontal();
        }
        private void StartPrepared()
        {
            if (prepared == null || manager == null) return; prepared.StartBattle();
            SectorCampaignBattle battle = manager.RegisterCustomBattle(prepared, "Custom " + (SectorBattlePreset)preset + " seed " + lastSeed);
            visible = false; presentation.OpenViewer(battle); prepared = null;
        }
    }
}
