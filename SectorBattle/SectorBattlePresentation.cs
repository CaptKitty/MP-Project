using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectX.DeterministicBattle;

namespace ProjectX.SectorBattle
{
    public sealed class SectorCampaignBattleMarker : MonoBehaviour
    {
        public SectorCampaignBattle Battle;
        public SectorBattlePresentation Presentation;
        private void OnMouseDown()
        {
            if (Mapshower.Instance != null) Mapshower.Instance.ConsumeCurrentMapClick();
            if (Presentation != null && Battle != null) Presentation.SelectBattlePopup(Battle);
        }
    }

    public sealed class SectorView : MonoBehaviour, IPointerClickHandler
    {
        public SectorCoord Coordinate;
        public Text DebugLabel;
        public SectorBattlePresentation Owner;
        public void Bind(SectorDebugState state, bool showDebug)
        {
            Coordinate = state.Coordinate;
            if (DebugLabel != null) DebugLabel.gameObject.SetActive(true);
        }
        public void OnPointerClick(PointerEventData eventData) { if (Owner != null) Owner.OnSectorClicked(Coordinate); }
    }

    public sealed class SectorBattleCameraController : MonoBehaviour
    {
        public RectTransform Battlefield;
        public float MinimumZoom = .72f, MaximumZoom = 1.55f;
        private Vector2 previousMouse;
        private void Update()
        {
            if (Battlefield == null) return;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .01f)
            {
                float scale = Mathf.Clamp(Battlefield.localScale.x * (1f + wheel * .08f), MinimumZoom, MaximumZoom);
                Battlefield.localScale = Vector3.one * scale;
            }
            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButton(2)) Battlefield.anchoredPosition += mouse - previousMouse;
            previousMouse = mouse;
        }
    }

    public sealed class SectorFormationView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public int FormationId = -1;
        public Vector2 Target;
        public SectorBattlePresentation Owner;
        private Vector2 velocity;
        private bool initialized;

        public void Bind(int id, SectorBattlePresentation owner)
        { if (FormationId != id) { FormationId = id; initialized = false; velocity = Vector2.zero; } Owner = owner; }

        public void TriggerAttack(bool faceLeft)
        {
            LayeredBattleUnitVisual art = GetComponent<LayeredBattleUnitVisual>();
            if (art == null) return;
            art.SetHorizontalFacing(faceLeft); art.Attacking = true; art.TriggerLegacyAttack();
        }

        private void Update()
        {
            RectTransform rect = (RectTransform)transform;
            if (!initialized) { rect.anchoredPosition = Target; initialized = true; }
            else rect.anchoredPosition = Vector2.SmoothDamp(rect.anchoredPosition, Target, ref velocity,
                .25f, Mathf.Infinity, Time.unscaledDeltaTime);
        }
        public void OnPointerEnter(PointerEventData eventData) { if (Owner != null) Owner.Inspect(FormationId, false); }
        public void OnPointerExit(PointerEventData eventData) { if (Owner != null) Owner.ClearHover(FormationId); }
        public void OnPointerClick(PointerEventData eventData) { if (Owner != null) { Owner.SelectGroupForFormation(FormationId); Owner.Inspect(FormationId, true); } }
        private void OnDisable() { initialized = false; velocity = Vector2.zero; }
    }

    public sealed class SectorCommandCardView : MonoBehaviour, IPointerClickHandler
    {
        public int GroupId;
        public SectorBattlePresentation Owner;
        public Text Header, Status, Summary, Members;
        public Image StrengthBar, MoraleBar;
        public LayeredBattleUnitVisual Portrait;
        public Outline Selection;
        public void OnPointerClick(PointerEventData eventData) { Owner?.SelectGroupFromCard(GroupId); }
    }

    /// <summary>Tactical UI for SectorBattleSimulation. Commands and explicit manual ticks use simulation APIs.</summary>
    public sealed class SectorBattlePresentation : MonoBehaviour
    {
        public static SectorBattlePresentation Instance { get; private set; }
        public static bool BlocksWorldInput => Instance != null && Instance.viewerRoot != null && Instance.viewerRoot.activeInHierarchy;
        private const float FieldWidth = 1250f, FieldHeight = 500f, CellWidth = 246f, CellHeight = 96f;
        private SectorBattleCampaignManager manager;
        private SectorCampaignBattle selected;
        private Canvas canvas;
        private GameObject accessRoot, viewerRoot, overlayRoot, inspectorRoot, battlePopupRoot, groupRoot;
        private GameObject topHeaderRoot, commandDockRoot, cardContainer, footerRoot;
        private GameObject demoRoot;
        private Text accessText, tickTimerText, inspectorText, battlePopupText, groupText;
        private Text leftFactionText, rightFactionText, battleTitleText, dockTitleText, footerText, movementText;
        private Button endTurnButton, autoTurnButton;
        private Text autoTurnButtonText;
        private RectTransform field, arrowRoot;
        private readonly Dictionary<SectorCoord, RectTransform> sectorRects = new Dictionary<SectorCoord, RectTransform>();
        private readonly Dictionary<SectorCoord, Text> sectorLabels = new Dictionary<SectorCoord, Text>();
        private readonly Dictionary<int, Image> formationViews = new Dictionary<int, Image>();
        private readonly Dictionary<int, SectorCommandCardView> commandCards = new Dictionary<int, SectorCommandCardView>();
        private readonly Dictionary<int, Text> groupBanners = new Dictionary<int, Text>();
        private readonly Dictionary<SectorCampaignBattle, GameObject> markers = new Dictionary<SectorCampaignBattle, GameObject>();
        private readonly List<Text> arrows = new List<Text>();
        private readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private Material baseUnitMaterial;
        private float nextRefresh;
        private int consumedEvents;
        private int hoveredId = -1, pinnedId = -1;
        private int selectedGroupId = -1;
        private int replayIndex;
        private bool replayMode, replayPlaying;
        private float nextReplayFrame;
        private bool autoAdvanceTurns;
        private float nextAutoTurn;
        private bool showDebug;
        private bool ownsCampaignPause;
        private float campaignSpeedBeforeBattle = .25f;
        private static Sprite markerSprite;

        public void Initialize(SectorBattleCampaignManager owner)
        {
            Instance = this;
            manager = owner; baseUnitMaterial = FindUnitMaterial();
            canvas = CreateCanvas(); CreateAccessButton();
            if (!SceneManager.GetActiveScene().name.StartsWith("MapScene", StringComparison.OrdinalIgnoreCase) &&
                SceneManager.GetActiveScene().name != "SampleScene" &&
                SceneManager.GetActiveScene().name != "ScenarioScene")
                CreateDemoButton();
            CreateBattlePopup(); CreateViewer();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && viewerRoot != null && viewerRoot.activeSelf) CloseViewer();
            if (autoAdvanceTurns && selected != null && viewerRoot != null && viewerRoot.activeSelf && !replayMode &&
                selected.Simulation != null && !selected.Simulation.IsResolved && Time.unscaledTime >= nextAutoTurn)
            { nextAutoTurn = Time.unscaledTime + 1f; EndTurn(); }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .08f; RefreshAccess();
            SynchronizeMarkers(); DetectMarkerClick();
            if (selected != null && battlePopupRoot != null && battlePopupRoot.activeSelf) RefreshBattlePopup();
            if (selected != null && viewerRoot.activeSelf)
            {
                if (replayMode && replayPlaying && Time.unscaledTime >= nextReplayFrame)
                { replayIndex = Mathf.Min(replayIndex + 1, Mathf.Max(0, selected.Replay.Count - 1)); replayPlaying = replayIndex < selected.Replay.Count - 1; nextReplayFrame = Time.unscaledTime + .18f; }
                RefreshViewer();
            }
        }

        public void OpenViewer(SectorCampaignBattle battle)
        {
            if (battle == null || battle.Simulation is not SectorBattleSimulation) return;
            if (selected != null && selected != battle) manager?.SetManualTickMode(selected, false);
            selected = battle; consumedEvents = 0; pinnedId = hoveredId = -1;
            selectedGroupId = -1; replayMode = replayPlaying = false; replayIndex = 0;
            SetAutoAdvance(false);
            manager?.SetManualTickMode(battle, true);
            PauseCampaignForBattle();
            if (battlePopupRoot != null) battlePopupRoot.SetActive(false);
            Mapshower.Instance?.ConsumeCurrentMapClick();
            viewerRoot.SetActive(true); viewerRoot.transform.SetAsLastSibling(); RefreshViewer();
        }
        public void Inspect(int id, bool pin) { hoveredId = id; if (pin) pinnedId = pinnedId == id ? -1 : id; RefreshInspector(); }
        public void ClearHover(int id) { if (hoveredId == id) hoveredId = -1; RefreshInspector(); }
        public void SelectGroupForFormation(int formationId)
        {
            if (selected?.Simulation is not SectorBattleSimulation simulation) return;
            SectorCommandGroup group = simulation.Commands.GroupForFormation(formationId);
            if (group == null || !group.PlayerControlled) return;
            selectedGroupId = group.GroupId; RefreshViewer();
        }
        public void SelectGroupFromCard(int groupId)
        {
            if (selected?.Simulation is not SectorBattleSimulation simulation || replayMode) return;
            SectorCommandGroup group = null;
            for (int i = 0; i < simulation.CommandGroups.Count; i++) if (simulation.CommandGroups[i].GroupId == groupId) { group = simulation.CommandGroups[i]; break; }
            if (group == null || !group.PlayerControlled) return;
            selectedGroupId = groupId; pinnedId = hoveredId = -1; RefreshViewer();
        }
        public void OnSectorClicked(SectorCoord coordinate)
        {
            if (selectedGroupId < 0 || selected?.Simulation is not SectorBattleSimulation simulation) return;
            simulation.IssueGroupMove(selectedGroupId, coordinate); RefreshViewer();
        }

        private void RefreshAccess()
        {
            int count = manager != null ? manager.ActiveBattles.Count : 0;
            if (accessRoot != null) accessRoot.SetActive(count > 0);
            if (count > 0 && accessText != null) accessText.text = "BATTLES: " + count + "\nClick to watch";
            if (selected != null && manager != null && !manager.ActiveBattles.Contains(selected)) CloseViewer();
        }

        public void OpenRelevantBattle()
        {
            if (manager == null || manager.ActiveBattles.Count == 0) return;
            SectorCampaignBattle battle = manager.ActiveBattles.Find(item => item.ArmyA != null && item.ArmyA.IsFriendlyToLocalPlayer());
            OpenViewer(battle ?? manager.ActiveBattles[0]);
        }

        public int ActiveBattleCount => manager != null ? manager.ActiveBattles.Count : 0;
        public bool IsViewingBattle(SectorCampaignBattle battle) => battle != null && selected == battle &&
            viewerRoot != null && viewerRoot.activeInHierarchy;

        private void CloseViewer()
        { SectorCampaignBattle closing = selected; Mapshower.Instance?.ConsumeCurrentMapClick(); manager?.SetManualTickMode(closing, false); RestoreCampaignAfterBattle(); SetAutoAdvance(false); selected = null; selectedGroupId = -1; replayMode = replayPlaying = false; viewerRoot.SetActive(false); inspectorRoot.SetActive(false); if (groupRoot != null) groupRoot.SetActive(false); if (battlePopupRoot != null) battlePopupRoot.SetActive(false); manager?.DismissBattle(closing); }

        private void PauseCampaignForBattle()
        {
            if (Owners.Instance == null || Owners.Instance.CampaignPaused || ownsCampaignPause) return;
            campaignSpeedBeforeBattle = Mathf.Max(.25f, Owners.Instance.CampaignSimulationSpeed);
            ownsCampaignPause = true;
            if (Mapshower.Instance != null) Mapshower.Instance.SetCampaignSpeed(0f);
            else Owners.Instance.CampaignPaused = true;
        }

        private void RestoreCampaignAfterBattle()
        {
            if (!ownsCampaignPause) return;
            ownsCampaignPause = false;
            if (Mapshower.Instance != null) Mapshower.Instance.SetCampaignSpeed(campaignSpeedBeforeBattle);
            else if (Owners.Instance != null)
            { Owners.Instance.CampaignSimulationSpeed = campaignSpeedBeforeBattle; Owners.Instance.CampaignPaused = false; }
        }

        private void EndTurn()
        {
            if (selected == null || replayMode || selected.Simulation == null || selected.Simulation.IsResolved) return;
            manager?.AdvanceManualBattleOneTick(selected); RefreshViewer();
        }

        private void ToggleAutoAdvance()
        {
            SetAutoAdvance(!autoAdvanceTurns);
            if (autoAdvanceTurns) nextAutoTurn = Time.unscaledTime + 1f;
        }

        private void SetAutoAdvance(bool enabled)
        {
            autoAdvanceTurns = enabled;
            if (autoTurnButtonText != null) autoTurnButtonText.text = enabled ? "STOP AUTO" : "AUTO 1/S";
        }

        private void SynchronizeMarkers()
        {
            if (manager == null) return;
            List<SectorCampaignBattle> removed = new List<SectorCampaignBattle>();
            foreach (KeyValuePair<SectorCampaignBattle, GameObject> pair in markers)
                if (!manager.ActiveBattles.Contains(pair.Key) || pair.Key == null || pair.Key.Simulation == null || pair.Key.Simulation.IsResolved)
                    removed.Add(pair.Key);
            for (int i = 0; i < removed.Count; i++)
            { if (markers[removed[i]] != null) Destroy(markers[removed[i]]); markers.Remove(removed[i]); }
            for (int i = 0; i < manager.ActiveBattles.Count; i++)
            {
                SectorCampaignBattle battle = manager.ActiveBattles[i];
                if (battle.DebugScenario || battle.Simulation == null || battle.Simulation.IsResolved) continue;
                if (!markers.TryGetValue(battle, out GameObject marker))
                {
                    marker = new GameObject("Sector Battle Marker " + battle.BattleId, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(SectorCampaignBattleMarker));
                    marker.transform.SetParent(transform, true); marker.transform.localScale = Vector3.one * .02f;
                    CircleCollider2D collider = marker.GetComponent<CircleCollider2D>(); collider.radius = .5f; collider.isTrigger = true;
                    SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>(); renderer.sprite = GetMarkerSprite();
                    renderer.color = new Color(.88f, .17f, .06f, .96f); renderer.sortingOrder = 100;
                    SectorCampaignBattleMarker link = marker.GetComponent<SectorCampaignBattleMarker>(); link.Battle = battle; link.Presentation = this;
                    markers.Add(battle, marker);
                }
                Vector3 position = battle.ArmyA != null && battle.ArmyB != null
                    ? (battle.ArmyA.transform.position + battle.ArmyB.transform.position) * .5f : battle.MapPosition;
                marker.transform.position = new Vector3(position.x, position.y, -2f);
            }
        }

        private void DetectMarkerClick()
        {
            if (!Input.GetMouseButtonDown(0) || Camera.main == null || viewerRoot.activeSelf) return;
            SectorCampaignBattle closest = null; float closestPixels = 16f;
            foreach (KeyValuePair<SectorCampaignBattle, GameObject> pair in markers)
            {
                if (pair.Value == null) continue;
                Vector3 screen = Camera.main.WorldToScreenPoint(pair.Value.transform.position);
                float distance = Vector2.Distance(Input.mousePosition, screen);
                if (screen.z >= 0f && distance < closestPixels) { closest = pair.Key; closestPixels = distance; }
            }
            if (closest != null) SelectBattlePopup(closest);
        }

        public void SelectBattlePopup(SectorCampaignBattle battle)
        {
            if (battle == null) return;
            selected = battle; battlePopupRoot.SetActive(true); battlePopupRoot.transform.SetAsLastSibling(); RefreshBattlePopup();
        }

        private void RefreshBattlePopup()
        {
            if (selected == null || manager == null || !manager.ActiveBattles.Contains(selected))
            { battlePopupRoot.SetActive(false); selected = null; return; }
            StringBuilder text = new StringBuilder(800).AppendLine("ARMIES ENGAGED")
                .Append("Sector battle — Tick ").AppendLine(selected.Simulation.Tick.ToString()).AppendLine();
            AppendEngagedSide(text, "SIDE A", selected.SideA, null);
            AppendEngagedSide(text, "SIDE B", selected.SideB, selected.Garrison);
            battlePopupText.text = text.ToString();
        }

        private static void AppendEngagedSide(StringBuilder text, string heading, List<FieldArmyHolder> holders, FieldArmy garrison)
        {
            text.AppendLine(heading);
            if (holders != null) for (int i = 0; i < holders.Count; i++)
            {
                FieldArmyHolder holder = holders[i]; if (holder == null || holder.fieldArmy == null) continue;
                AppendArmy(text, holder.name, holder.fieldArmy);
            }
            if (garrison != null) AppendArmy(text, "Provincial Garrison", garrison);
            text.AppendLine();
        }

        private static void AppendArmy(StringBuilder text, string name, FieldArmy army)
        {
            text.Append("• ").Append(string.IsNullOrWhiteSpace(name) ? army.name : name).Append(" — ");
            bool any = false;
            for (int i = 0; i < army.USDReserves.Count; i++)
            {
                ArmyReserves reserve = army.USDReserves[i]; if (reserve == null || reserve.USD == null || reserve.amount <= 0) continue;
                if (any) text.Append(", "); any = true; text.Append(reserve.amount).Append('×').Append(reserve.USD.unitname);
            }
            text.AppendLine(any ? string.Empty : "No active formations");
        }

        private void RefreshViewer()
        {
            SectorBattleSimulation simulation = selected != null ? selected.Simulation as SectorBattleSimulation : null;
            if (simulation == null) return;
            if (simulation.IsResolved && manager != null && manager.IsManualTickMode(selected))
            {
                manager.SetManualTickMode(selected, false);
                RestoreCampaignAfterBattle();
            }
            SectorReplayFrame frame = replayMode && selected.Replay.Count > 0 ? selected.Replay[Mathf.Clamp(replayIndex, 0, selected.Replay.Count - 1)] : null;
            int shownTick = frame != null ? frame.Tick : simulation.Tick;
            bool manual = manager != null && manager.IsManualTickMode(selected);
            string stateLabel = replayMode ? "REPLAY " + (replayIndex + 1) + "/" + selected.Replay.Count : simulation.IsResolved ? "FINISHED" : manual ? "AWAITING ORDERS" : "LIVE";
            battleTitleText.text = selected.BattleId.ToUpperInvariant() + "\n<color=#cbb987>Tick " + shownTick + "  •  " + stateLabel + "</color>";
            leftFactionText.text = FactionName(selected, 0).ToUpperInvariant() + "\n<color=#c7b9a2>SIDE A — ADVANCING RIGHT</color>";
            rightFactionText.text = FactionName(selected, 1).ToUpperInvariant() + "\n<color=#c7b9a2>SIDE B — ADVANCING LEFT</color>";
            if (tickTimerText != null)
            {
                tickTimerText.text = replayMode ? "REPLAY" : simulation.IsResolved ? "BATTLE ENDED" :
                    selected.StartDelaySectorTicksRemaining > 0 ? "DEPLOYMENT: " + selected.StartDelaySectorTicksRemaining + " SECTOR TICKS" :
                    manual ? "NEXT SECTOR TICK: " + manager.CampaignStepsUntilNextSectorTick + " STEPS" :
                    manager == null || manager.TickClockPaused ? "NEXT TICK: PAUSED" : "NEXT TICK: " + manager.SecondsUntilNextTick.ToString("0.0") + "s";
            }
            if (endTurnButton != null) endTurnButton.interactable = manual && !replayMode && !simulation.IsResolved;
            if (simulation.IsResolved || replayMode || !manual) SetAutoAdvance(false);
            if (autoTurnButton != null) autoTurnButton.interactable = manual && !replayMode && !simulation.IsResolved;
            List<SectorDebugState> sectors = frame != null ? frame.Sectors : simulation.GetDebugState();
            for (int i = 0; i < sectors.Count; i++) RefreshSector(sectors[i]);
            List<SectorFormationPresentationState> formations = frame != null ? frame.Formations : simulation.GetPresentationState();
            HashSet<int> visible = new HashSet<int>();
            for (int i = 0; i < formations.Count; i++)
            {
                SectorFormationPresentationState state = formations[i];
                if (state.State == SectorFormationState.Withdrawn) continue;
                visible.Add(state.FormationId);
                if (state.State == SectorFormationState.Destroyed || state.Strength <= 0) RefreshCorpse(state);
                else RefreshFormation(state, formations, sectors);
            }
            foreach (KeyValuePair<int, Image> pair in formationViews) pair.Value.gameObject.SetActive(visible.Contains(pair.Key));
            ApplyFormationDrawOrder();
            RefreshArrows(formations); RefreshGroupBanners(simulation, formations); RefreshCommandCards(simulation, formations);
            if (!replayMode) ConsumeEvents(simulation); RefreshInspector();
            if (!replayMode) { RefreshGroupPanel(simulation); RefreshSectorHighlights(simulation); }
            else { groupRoot.SetActive(false); ClearSectorHighlights(); }
        }

        private void RefreshSector(SectorDebugState state)
        {
            RectTransform rect = sectorRects[state.Coordinate]; Image image = rect.GetComponent<Image>();
            rect.GetComponent<SectorView>().Bind(state, showDebug);
            Color terrain = TerrainColor(state.Terrain);
            if (state.Control == SectorControl.Contested) terrain = Color.Lerp(terrain, new Color(.72f, .20f, .16f, .5f), .38f);
            else if (state.Control == SectorControl.SideA) terrain = Color.Lerp(terrain, new Color(.18f, .45f, .85f, .45f), .18f);
            else if (state.Control == SectorControl.SideB) terrain = Color.Lerp(terrain, new Color(.85f, .25f, .18f, .45f), .18f);
            image.color = terrain;
            StringBuilder text = new StringBuilder().Append(TerrainName(state.Terrain));
            if (showDebug) text.Append("\n").Append(state.Coordinate).Append("  ").Append(state.Control)
                .Append("\nBase frontage: ").Append(state.BaseFrontage)
                .Append("  Active: ").Append(state.ActiveA.Count + state.ActiveB.Count)
                .Append("\nSupport A/B: ").Append(state.ReserveA.Count).Append('/').Append(state.ReserveB.Count)
                .Append("  Flank A/B: +").Append(state.FlankFrontageA).Append("/+").Append(state.FlankFrontageB);
            sectorLabels[state.Coordinate].text = text.ToString();
        }

        private void RefreshFormation(SectorFormationPresentationState state,
            List<SectorFormationPresentationState> all, List<SectorDebugState> sectors)
        {
            Image image = AcquireFormation(state.FormationId); image.gameObject.SetActive(true);
            LayeredBattleUnitVisual art = image.GetComponent<LayeredBattleUnitVisual>();
            art.RestoreEquipment(); art.Configure(state.Unit, GetFactionMaterial(state));
            art.SetPresentationFallen(false, 0f); art.SetContinuousCheer(false);
            art.Status = state.Role == SectorFormationRole.Moving ? FormationStatus.Advancing :
                state.Role == SectorFormationRole.Routing ? FormationStatus.Routing :
                state.Role == SectorFormationRole.BaseFrontage || state.Role == SectorFormationRole.FlankAttacker ||
                state.Role == SectorFormationRole.RearFlankAttacker
                    ? FormationStatus.Engaged : FormationStatus.Advancing;
            bool faceLeft = state.Side == 1;
            if (!state.TargetSector.Equals(state.Sector)) faceLeft = (int)state.TargetSector.Depth < (int)state.Sector.Depth;
            art.SetHorizontalFacing(faceLeft);
            SectorFormationView view = image.GetComponent<SectorFormationView>();
            view.Bind(state.FormationId, this); view.Target = FormationPosition(state, all, sectors);
            float visualSize = FormationVisualSize(state, all);
            float unitScale = state.Unit != null && state.Unit.Huge ? 2f :
                state.Unit != null && state.Unit.Big ? 1.2f : 1f;
            image.rectTransform.sizeDelta = Vector2.one * visualSize * unitScale;
            image.color = state.Role == SectorFormationRole.Supporting ? new Color(1f, 1f, 1f, .72f) : Color.white;
            Outline marker = image.GetComponent<Outline>();
            if (marker != null) marker.enabled = false;
        }

        private void RefreshCorpse(SectorFormationPresentationState state)
        {
            Image image = AcquireFormation(state.FormationId); image.gameObject.SetActive(true); image.raycastTarget = false;
            LayeredBattleUnitVisual art = image.GetComponent<LayeredBattleUnitVisual>();
            art.Configure(state.Unit, GetFactionMaterial(state)); art.Status = FormationStatus.Destroyed; art.Attacking = false;
            art.SetHorizontalFacing(state.Side == 1);
            art.SetPresentationFallen(true, state.FormationId % 2 == 0 ? 90f : -90f);
            art.SetContinuousCheer(false); art.DropEquipment(state.FormationId % 2 == 0 ? 1f : -1f);
            SectorFormationView view = image.GetComponent<SectorFormationView>(); view.Bind(state.FormationId, this);
            int seed = Math.Abs(state.FormationId * 37 + 17);
            view.Target = SectorCentre(state.Sector) + new Vector2(seed % 141 - 70f, (seed / 11) % 65 - 42f);
            float corpseScale = state.Unit != null && state.Unit.Huge ? 2f :
                state.Unit != null && state.Unit.Big ? 1.2f : 1f;
            image.rectTransform.sizeDelta = Vector2.one * 45f * corpseScale;
            image.color = new Color(.72f, .72f, .68f, .9f);
            Outline marker = image.GetComponent<Outline>();
            if (marker != null) marker.enabled = false;
        }

        private Vector2 FormationPosition(SectorFormationPresentationState state,
            List<SectorFormationPresentationState> all, List<SectorDebugState> debug)
        {
            Vector2 source = SectorCentre(state.Sector) + SlotOffset(state, all);
            if (state.Role != SectorFormationRole.Moving) return source;
            Vector2 destination = SectorCentre(state.TargetSector) + GridOffset(state, all, state.TargetSector, true);
            return Vector2.Lerp(source, destination, Mathf.Clamp01(state.MovementProgress));
        }

        private Vector2 SlotOffset(SectorFormationPresentationState state, List<SectorFormationPresentationState> all)
            => GridOffset(state, all, state.Sector, false);

        private Vector2 GridOffset(SectorFormationPresentationState state, List<SectorFormationPresentationState> all, SectorCoord coordinate, bool targets)
        {
            SectorBattleSimulation simulation = selected != null ? selected.Simulation as SectorBattleSimulation : null;
            int visualColumn = VisualColumn(state);
            List<SectorFormationPresentationState> peers = all.FindAll(item =>
                (targets ? item.TargetSector.Equals(coordinate) && item.Role == SectorFormationRole.Moving : item.Sector.Equals(coordinate)) &&
                item.State != SectorFormationState.Destroyed && item.State != SectorFormationState.Withdrawn &&
                (targets || VisualColumn(item) == visualColumn));
            peers.Sort((a, b) =>
            {
                int side = a.Side.CompareTo(b.Side); if (side != 0) return side;
                SectorCommandGroup ga = simulation?.Commands.GroupForFormation(a.FormationId), gb = simulation?.Commands.GroupForFormation(b.FormationId);
                int group = (ga?.GroupId ?? 0).CompareTo(gb?.GroupId ?? 0); return group != 0 ? group : a.FormationId.CompareTo(b.FormationId);
            });
            int index = Mathf.Max(0, peers.FindIndex(item => item.FormationId == state.FormationId));
            int row = index;
            float x = targets ? (state.Side == 0 ? -26f : 26f) :
                visualColumn == 0 ? -82f : visualColumn == 1 ? -27f : visualColumn == 2 ? 27f : 82f;
            // Allocate occupation slots from the back/top of the sector downward.
            // This keeps a stable marching order and makes lower figures naturally
            // cover the feet of figures behind them instead of covering their heads.
            float y = peers.Count <= 1 ? 6f : Mathf.Lerp(26f, -20f, row / (float)(peers.Count - 1));
            return new Vector2(x, y);
        }

        private void ApplyFormationDrawOrder()
        {
            List<Image> visible = formationViews.Values
                .Where(image => image != null && image.gameObject.activeSelf)
                .OrderByDescending(image =>
                {
                    SectorFormationView view = image.GetComponent<SectorFormationView>();
                    return view != null ? view.Target.y : image.rectTransform.anchoredPosition.y;
                })
                .ThenBy(image => image.GetComponent<SectorFormationView>()?.FormationId ?? 0)
                .ToList();

            // Unity UI renders later siblings on top. High figures are placed first,
            // so progressively lower figures render in front of them.
            for (int i = 0; i < visible.Count; i++)
                visible[i].transform.SetAsLastSibling();
        }

        private static int VisualColumn(SectorFormationPresentationState state)
        {
            bool fighting = state.Role == SectorFormationRole.BaseFrontage || state.Role == SectorFormationRole.FlankAttacker ||
                state.Role == SectorFormationRole.RearFlankAttacker;
            if (state.Side == 0) return fighting ? 1 : 0; // reserve A, frontage A
            return fighting ? 2 : 3;                     // frontage B, reserve B
        }

        private static float FormationVisualSize(SectorFormationPresentationState state, List<SectorFormationPresentationState> all)
        {
            int column = VisualColumn(state);
            int count = all.FindAll(item => item.Sector.Equals(state.Sector) && VisualColumn(item) == column &&
                item.State != SectorFormationState.Destroyed && item.State != SectorFormationState.Withdrawn).Count;
            float vertical = count > 1 ? 46f / (count - 1) : 44f;
            return Mathf.Clamp(vertical + 8f, 24f, 44f);
        }

        private static float SlotX(int index, int count)
        { count = Mathf.Max(1, count); return (index - (count - 1) * .5f) * Mathf.Min(42f, 164f / count); }

        private void RefreshArrows(List<SectorFormationPresentationState> formations)
        {
            for (int i = 0; i < arrows.Count; i++) arrows[i].gameObject.SetActive(false);
            Dictionary<string, int> grouped = new Dictionary<string, int>();
            for (int i = 0; i < formations.Count; i++) if (formations[i].Role == SectorFormationRole.FlankAttacker ||
                formations[i].Role == SectorFormationRole.RearFlankAttacker)
            {
                SectorFormationPresentationState f = formations[i]; string key = f.Side + ":" + f.Sector + ":" + f.TargetSector;
                grouped[key] = grouped.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            int arrowIndex = 0;
            foreach (KeyValuePair<string, int> pair in grouped)
            {
                string[] pieces = pair.Key.Split(':'); SectorFormationPresentationState sample = formations.Find(f =>
                    (f.Role == SectorFormationRole.FlankAttacker || f.Role == SectorFormationRole.RearFlankAttacker) &&
                    f.Side.ToString() == pieces[0] && f.Sector.ToString() == pieces[1]);
                if (sample == null) continue;
                Text arrow = AcquireArrow(arrowIndex++); Vector2 from = SectorCentre(sample.Sector), to = SectorCentre(sample.TargetSector);
                arrow.rectTransform.anchoredPosition = Vector2.Lerp(from, to, .5f);
                arrow.text = DirectionArrow(from, to) + "  FLANK +" + pair.Value;
                arrow.color = sample.Side == 0 ? new Color(.55f, .78f, 1f) : new Color(1f, .58f, .45f);
                arrow.gameObject.SetActive(true);
            }
        }

        private void RefreshGroupBanners(SectorBattleSimulation simulation, List<SectorFormationPresentationState> formations)
        {
            HashSet<int> visible = new HashSet<int>();
            for (int i = 0; i < simulation.CommandGroups.Count; i++)
            {
                SectorCommandGroup group = simulation.CommandGroups[i]; Vector2 centre = Vector2.zero; int count = 0;
                for (int m = 0; m < group.MemberFormationIds.Count; m++)
                {
                    SectorFormationPresentationState state = formations.Find(item => item.FormationId == group.MemberFormationIds[m] && item.Strength > 0 && item.State != SectorFormationState.Destroyed && item.State != SectorFormationState.Withdrawn);
                    if (state == null) continue; centre += FormationPosition(state, formations, null); count++;
                }
                if (count == 0) continue; centre /= count; visible.Add(group.GroupId);
                if (!groupBanners.TryGetValue(group.GroupId, out Text banner))
                {
                    banner = CreateText("Command Group Banner " + group.GroupId, field, 11, TextAnchor.MiddleCenter, Color.white);
                    banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    banner.rectTransform.sizeDelta = new Vector2(190f, 28f); banner.raycastTarget = false;
                    Outline outline = banner.gameObject.AddComponent<Outline>(); outline.effectDistance = new Vector2(1f, -1f);
                    groupBanners[group.GroupId] = banner;
                }
                banner.text = "⚑  " + group.DisplayName.ToUpperInvariant() + " [" + count + "]";
                banner.rectTransform.anchoredPosition = centre + new Vector2(0f, 42f);
                banner.color = group.GroupId == selectedGroupId ? new Color(1f, .86f, .28f) : group.Side == 0 ? new Color(.95f, .55f, .45f) : new Color(.48f, .72f, 1f);
                banner.fontStyle = group.GroupId == selectedGroupId ? FontStyle.Bold : FontStyle.Normal;
                banner.gameObject.SetActive(true);
            }
            foreach (KeyValuePair<int, Text> pair in groupBanners) if (!visible.Contains(pair.Key)) pair.Value.gameObject.SetActive(false);
            RefreshSelectedMovement(simulation);
        }

        private void RefreshSelectedMovement(SectorBattleSimulation simulation)
        {
            SectorCommandGroup group = null;
            for (int i = 0; i < simulation.CommandGroups.Count; i++) if (simulation.CommandGroups[i].GroupId == selectedGroupId) { group = simulation.CommandGroups[i]; break; }
            bool show = group != null && (group.Moving || !group.DestinationSector.Equals(group.CurrentSector));
            movementText.gameObject.SetActive(show);
            if (!show) return;
            Vector2 from = SectorCentre(group.CurrentSector), to = SectorCentre(group.DestinationSector);
            movementText.rectTransform.anchoredPosition = Vector2.Lerp(from, to, .5f);
            movementText.text = "- - -  " + DirectionArrow(from, to) + "  " + LaneName(group.DestinationSector.Lane) +
                " / " + DepthName(group.DestinationSector.Depth);
        }

        private void RefreshCommandCards(SectorBattleSimulation simulation, List<SectorFormationPresentationState> formations)
        {
            int side = 0;
            SectorCommandGroup selectedGroup = null;
            for (int i = 0; i < simulation.CommandGroups.Count; i++) if (simulation.CommandGroups[i].GroupId == selectedGroupId) selectedGroup = simulation.CommandGroups[i];
            if (selectedGroup != null && selectedGroup.PlayerControlled) side = selectedGroup.Side;
            else if (!simulation.CommandGroups.Any(item => item.Side == 0 && item.PlayerControlled) && simulation.CommandGroups.Any(item => item.Side == 1 && item.PlayerControlled)) side = 1;
            List<SectorCommandGroup> groups = new List<SectorCommandGroup>();
            for (int i = 0; i < simulation.CommandGroups.Count; i++) if (simulation.CommandGroups[i].Side == side && simulation.CommandGroups[i].PlayerControlled) groups.Add(simulation.CommandGroups[i]);
            groups.Sort((a, b) => a.GroupId.CompareTo(b.GroupId));
            dockTitleText.text = FactionName(selected, side).ToUpperInvariant() + " COMMAND GROUPS     " + groups.Count + " / " + simulation.Commands.Capacity(side);
            HashSet<int> visible = new HashSet<int>();
            for (int i = 0; i < groups.Count; i++)
            {
                SectorCommandGroup group = groups[i]; visible.Add(group.GroupId);
                SectorCommandCardView card = AcquireCommandCard(group.GroupId); card.gameObject.SetActive(true);
                RectTransform rect = (RectTransform)card.transform; float gap = .006f, width = (1f - gap * (groups.Count + 1)) / Mathf.Max(1, groups.Count);
                Anchor(rect, new Vector2(gap + i * (width + gap), .02f), new Vector2(gap + i * (width + gap) + width, .98f));
                List<SectorFormationPresentationState> members = new List<SectorFormationPresentationState>();
                for (int m = 0; m < group.MemberFormationIds.Count; m++) { SectorFormationPresentationState state = formations.Find(item => item.FormationId == group.MemberFormationIds[m]); if (state != null) members.Add(state); }
                int strength = Average(members, item => Percent(item.Strength, item.MaximumStrength));
                int morale = Average(members, item => item.Morale / 10), exhaustion = Average(members, item => item.Exhaustion / 10);
                bool engaged = members.Exists(item => item.Role == SectorFormationRole.BaseFrontage ||
                    item.Role == SectorFormationRole.FlankAttacker || item.Role == SectorFormationRole.RearFlankAttacker);
                int impaired = members.FindAll(item => item.State == SectorFormationState.Routing || item.Morale < 350 || item.Exhaustion > 700).Count;
                card.Header.text = group.DisplayName.ToUpperInvariant() + " [" + members.Count + "]";
                card.Status.text = LaneName(group.CurrentSector.Lane) + " / " + DepthName(group.CurrentSector.Depth) + "  •  " +
                    (engaged ? "ENGAGED / " : string.Empty) + group.CurrentOrder.ToString().ToUpperInvariant();
                card.Summary.text = DominantUnitName(members) + "\nStrength  " + strength + "%\nMorale    " + morale + "%\nExhaust.  " + exhaustion + "%" + (impaired > 0 ? "\n<color=#e8a04b>⚠ " + impaired + " impaired</color>" : string.Empty);
                card.Members.text = MemberDots(members.Count);
                if (members.Count > 0) card.Portrait.Configure(members[0].Unit, GetFactionMaterial(members[0]));
                card.StrengthBar.fillAmount = strength / 100f; card.MoraleBar.fillAmount = morale / 100f;
                bool selectedCard = group.GroupId == selectedGroupId; card.Selection.effectColor = selectedCard ? new Color(1f, .78f, .18f, 1f) : new Color(.42f, .34f, .22f, .65f);
                card.Selection.effectDistance = selectedCard ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
            }
            foreach (KeyValuePair<int, SectorCommandCardView> pair in commandCards) if (!visible.Contains(pair.Key)) pair.Value.gameObject.SetActive(false);
        }

        private SectorCommandCardView AcquireCommandCard(int groupId)
        {
            if (commandCards.TryGetValue(groupId, out SectorCommandCardView existing)) return existing;
            GameObject root = Panel("Command Group Card " + groupId, cardContainer.transform, Vector2.zero, Vector2.one, new Color(.055f, .065f, .06f, 1f));
            SectorCommandCardView card = root.AddComponent<SectorCommandCardView>(); card.GroupId = groupId; card.Owner = this;
            card.Selection = root.AddComponent<Outline>();
            card.Header = CreateText("Group Name", root.transform, 13, TextAnchor.MiddleLeft, new Color(.97f, .90f, .75f)); Anchor(card.Header.rectTransform, new Vector2(.04f, .80f), new Vector2(.97f, .98f));
            card.Status = CreateText("Group Status", root.transform, 10, TextAnchor.MiddleLeft, new Color(.78f, .74f, .65f)); Anchor(card.Status.rectTransform, new Vector2(.04f, .66f), new Vector2(.97f, .80f));
            GameObject portrait = new GameObject("Representative Unit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayeredBattleUnitVisual)); portrait.transform.SetParent(root.transform, false);
            RectTransform portraitRect = portrait.GetComponent<RectTransform>(); Anchor(portraitRect, new Vector2(.04f, .25f), new Vector2(.38f, .64f));
            Image portraitImage = portrait.GetComponent<Image>(); portraitImage.preserveAspect = true; portraitImage.raycastTarget = false; card.Portrait = portrait.GetComponent<LayeredBattleUnitVisual>();
            card.Summary = CreateText("Group Summary", root.transform, 11, TextAnchor.UpperLeft, Color.white); card.Summary.supportRichText = true; Anchor(card.Summary.rectTransform, new Vector2(.40f, .20f), new Vector2(.96f, .64f));
            card.Members = CreateText("Member Markers", root.transform, 12, TextAnchor.MiddleCenter, new Color(.88f, .74f, .48f)); Anchor(card.Members.rectTransform, new Vector2(.04f, .02f), new Vector2(.96f, .18f));
            card.StrengthBar = CreateBar("Strength Bar", root.transform, new Vector2(.46f, .49f), new Vector2(.92f, .54f), new Color(.35f, .72f, .40f));
            card.MoraleBar = CreateBar("Morale Bar", root.transform, new Vector2(.46f, .37f), new Vector2(.92f, .42f), new Color(.88f, .66f, .20f));
            commandCards[groupId] = card; return card;
        }

        private void ConsumeEvents(SectorBattleSimulation simulation)
        {
            if (consumedEvents > simulation.Events.Count) consumedEvents = 0;
            for (int i = consumedEvents; i < simulation.Events.Count; i++)
            {
                SectorCombatEvent battleEvent = simulation.Events[i];
                if (battleEvent.Type != SectorPresentationEventType.Attack || !formationViews.TryGetValue(battleEvent.FormationId, out Image image)) continue;
                SectorFormationPresentationState state = simulation.GetPresentationState().Find(item => item.FormationId == battleEvent.FormationId);
                if (state != null) image.GetComponent<SectorFormationView>().TriggerAttack(
                    (int)battleEvent.Sector.Depth < (int)state.Sector.Depth || battleEvent.Sector.Depth == state.Sector.Depth && state.Side == 1);
            }
            consumedEvents = simulation.Events.Count;
        }

        private void RefreshInspector()
        {
            int id = pinnedId >= 0 ? pinnedId : hoveredId;
            if (id < 0 || selected == null || selected.Simulation is not SectorBattleSimulation simulation)
            { inspectorRoot.SetActive(false); return; }
            SectorFormationPresentationState state = simulation.GetPresentationState().Find(item => item.FormationId == id);
            if (state == null) { inspectorRoot.SetActive(false); return; }
            inspectorRoot.SetActive(true); inspectorText.text = state.DisplayName + "\nSector: " + LaneName(state.Sector.Lane) + " / " + DepthName(state.Sector.Depth) +
                "\nStrength: " + Percent(state.Strength, state.MaximumStrength) + "%\nMorale: " + state.Morale / 10 + "%\nExhaustion: " +
                state.Exhaustion / 10 + "%\nRole: " + RoleName(state.Role) + (pinnedId == id ? "\n[CLICKED — click again to unpin]" : string.Empty);
        }

        private void RefreshGroupPanel(SectorBattleSimulation simulation)
        {
            SectorCommandGroup group = null;
            for (int i = 0; i < simulation.CommandGroups.Count; i++) if (simulation.CommandGroups[i].GroupId == selectedGroupId) { group = simulation.CommandGroups[i]; break; }
            if (group == null) { groupRoot.SetActive(false); return; }
            groupRoot.SetActive(true); StringBuilder text = new StringBuilder(700)
                .Append(group.DisplayName.ToUpperInvariant()).Append(" [").Append(group.MemberFormationIds.Count).AppendLine("]")
                .Append(LaneName(group.CurrentSector.Lane)).Append(" / ").Append(DepthName(group.CurrentSector.Depth)).Append("  |  ")
                .Append(group.CurrentOrder.ToString().ToUpperInvariant()).AppendLine()
                .Append("Commander: ").AppendLine(string.IsNullOrEmpty(group.GeneralName) ? "Unknown" : group.GeneralName);
            List<SectorFormationPresentationState> states = simulation.GetPresentationState();
            for (int i = 0; i < group.MemberFormationIds.Count; i++)
            {
                SectorFormationPresentationState unit = states.Find(candidate => candidate.FormationId == group.MemberFormationIds[i]);
                if (unit == null) continue;
                text.Append("- ").Append(unit.DisplayName).Append("  ")
                    .Append(Percent(unit.Strength, unit.MaximumStrength)).Append("% / M ")
                    .Append(unit.Morale / 10).Append("% / E ").Append(unit.Exhaustion / 10).AppendLine("%");
            }
            groupText.text = text.ToString();
        }

        private void IssueSelectedOrder(SectorGroupOrder order)
        {
            if (selectedGroupId < 0 || selected?.Simulation is not SectorBattleSimulation simulation) return;
            simulation.IssueGroupOrder(selectedGroupId, order); RefreshViewer();
        }

        private void RefreshSectorHighlights(SectorBattleSimulation simulation)
        {
            SectorCommandGroup selectedGroup = null;
            for (int i = 0; i < simulation.CommandGroups.Count; i++) if (simulation.CommandGroups[i].GroupId == selectedGroupId) { selectedGroup = simulation.CommandGroups[i]; break; }
            foreach (KeyValuePair<SectorCoord, RectTransform> pair in sectorRects)
            {
                Outline outline = pair.Value.GetComponent<Outline>();
                bool valid = selectedGroup != null && Math.Abs((int)selectedGroup.CurrentSector.Lane - (int)pair.Key.Lane) +
                    Math.Abs((int)selectedGroup.CurrentSector.Depth - (int)pair.Key.Depth) == 1;
                bool current = selectedGroup != null && selectedGroup.CurrentSector.Equals(pair.Key);
                bool destination = selectedGroup != null && selectedGroup.DestinationSector.Equals(pair.Key) && !current;
                outline.effectColor = destination ? new Color(1f, .78f, .12f, 1f) : current ? new Color(1f, .91f, .48f, .82f) :
                    valid ? new Color(.92f, .86f, .62f, .62f) : new Color(1f, 1f, 1f, showDebug ? .34f : .10f);
                outline.effectDistance = destination ? new Vector2(4f, -4f) : current || valid ? new Vector2(2.5f, -2.5f) : new Vector2(1f, -1f);
            }
        }

        private Image AcquireFormation(int id)
        {
            if (formationViews.TryGetValue(id, out Image existing)) return existing;
            GameObject root = new GameObject("Sector Formation " + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Outline), typeof(LayeredBattleUnitVisual), typeof(SectorFormationView)); root.transform.SetParent(field, false);
            Image image = root.GetComponent<Image>(); image.raycastTarget = true; image.preserveAspect = true;
            Outline marker = root.GetComponent<Outline>(); marker.enabled = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            formationViews[id] = image; return image;
        }

        private Text AcquireArrow(int index)
        {
            if (index < arrows.Count) return arrows[index];
            Text text = CreateText("Flank Arrow", arrowRoot, 18, TextAnchor.MiddleCenter, Color.white);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f); text.rectTransform.sizeDelta = new Vector2(160, 34);
            arrows.Add(text); return text;
        }

        private void CreateViewer()
        {
            viewerRoot = Panel("Sector Battle Viewer", canvas.transform, Vector2.zero, Vector2.one, new Color(.025f, .035f, .03f, .98f));
            CanvasGroup inputShield = viewerRoot.AddComponent<CanvasGroup>();
            inputShield.interactable = true; inputShield.blocksRaycasts = true; inputShield.ignoreParentGroups = false;
            viewerRoot.GetComponent<Image>().raycastTarget = true;
            topHeaderRoot = Panel("Top Battle Header", viewerRoot.transform, new Vector2(0f, .92f), Vector2.one, new Color(.055f, .06f, .055f, .985f));
            leftFactionText = CreateText("Player Faction Header", topHeaderRoot.transform, 20, TextAnchor.MiddleLeft, new Color(.91f, .84f, .69f));
            Anchor(leftFactionText.rectTransform, new Vector2(.025f, .08f), new Vector2(.31f, .92f));
            battleTitleText = CreateText("Battle Title", topHeaderRoot.transform, 19, TextAnchor.MiddleCenter, new Color(.96f, .91f, .79f));
            battleTitleText.supportRichText = true; Anchor(battleTitleText.rectTransform, new Vector2(.31f, .08f), new Vector2(.69f, .92f));
            rightFactionText = CreateText("Enemy Faction Header", topHeaderRoot.transform, 20, TextAnchor.MiddleRight, new Color(.91f, .84f, .69f));
            Anchor(rightFactionText.rectTransform, new Vector2(.69f, .08f), new Vector2(.965f, .92f));
            tickTimerText = CreateText("Tick Cooldown", viewerRoot.transform, 14, TextAnchor.MiddleCenter, new Color(1f, .88f, .45f));
            Anchor(tickTimerText.rectTransform, new Vector2(.80f, .935f), new Vector2(.90f, .985f));
            CreateButton("Close", viewerRoot.transform, new Vector2(.955f, .935f), new Vector2(.993f, .988f), "X", CloseViewer);
            CreateButton("Debug", viewerRoot.transform, new Vector2(.905f, .935f), new Vector2(.953f, .988f), "Debug", ToggleDebug);
            CreateButton("Replay", viewerRoot.transform, new Vector2(.735f, .935f), new Vector2(.798f, .988f), "Replay", ToggleReplay);
            CreateButton("Replay Previous", viewerRoot.transform, new Vector2(.675f, .935f), new Vector2(.704f, .988f), "<", () => StepReplay(-1));
            CreateButton("Replay Next", viewerRoot.transform, new Vector2(.705f, .935f), new Vector2(.734f, .988f), ">", () => StepReplay(1));
            GameObject fieldObject = Panel("Battlefield Presentation", viewerRoot.transform, new Vector2(.045f, .34f), new Vector2(.995f, .92f), new Color(.12f, .20f, .10f, 1f));
            field = new GameObject("Sector Overlay", typeof(RectTransform)).GetComponent<RectTransform>(); field.SetParent(fieldObject.transform, false);
            field.anchorMin = field.anchorMax = new Vector2(.5f, .5f); field.sizeDelta = new Vector2(FieldWidth, FieldHeight);
            SectorBattleCameraController cameraController = viewerRoot.AddComponent<SectorBattleCameraController>(); cameraController.Battlefield = field;
            CreateSectors();
            CreateBattlefieldOrientationLabels(fieldObject.transform);
            arrowRoot = new GameObject("Flank Indicators", typeof(RectTransform)).GetComponent<RectTransform>(); arrowRoot.SetParent(field, false);
            arrowRoot.anchorMin = Vector2.zero; arrowRoot.anchorMax = Vector2.one; arrowRoot.offsetMin = arrowRoot.offsetMax = Vector2.zero;
            overlayRoot = new GameObject("Sector Debug Overlay"); overlayRoot.transform.SetParent(field, false);
            inspectorRoot = Panel("Formation Inspector", fieldObject.transform, new Vector2(.77f, .56f), new Vector2(.985f, .94f), new Color(.035f, .04f, .035f, .92f));
            inspectorText = CreateText("Inspection", inspectorRoot.transform, 17, TextAnchor.UpperLeft, Color.white);
            Anchor(inspectorText.rectTransform, new Vector2(.06f, .06f), new Vector2(.94f, .94f)); inspectorRoot.SetActive(false);
            commandDockRoot = Panel("Command Dock", viewerRoot.transform, new Vector2(0f, .035f), new Vector2(1f, .34f), new Color(.025f, .03f, .028f, .995f));
            Outline dockBorder = commandDockRoot.AddComponent<Outline>(); dockBorder.effectColor = new Color(.53f, .42f, .25f, .72f); dockBorder.effectDistance = new Vector2(0f, 2f);
            dockTitleText = CreateText("Command Dock Header", commandDockRoot.transform, 14, TextAnchor.MiddleLeft, new Color(.88f, .80f, .65f));
            Anchor(dockTitleText.rectTransform, new Vector2(.012f, .87f), new Vector2(.77f, .99f));
            cardContainer = Panel("Command Group Card Container", commandDockRoot.transform, new Vector2(.008f, .04f), new Vector2(.775f, .86f), new Color(0f, 0f, 0f, 0f));
            cardContainer.GetComponent<Image>().raycastTarget = false;
            groupRoot = Panel("Selected Group Inspector", commandDockRoot.transform, new Vector2(.782f, .04f), new Vector2(.993f, .97f), new Color(.045f, .052f, .048f, 1f));
            groupText = CreateText("Selected Group Data", groupRoot.transform, 13, TextAnchor.UpperLeft, Color.white);
            Anchor(groupText.rectTransform, new Vector2(.04f, .24f), new Vector2(.96f, .94f));
            CreateButton("Hold", groupRoot.transform, new Vector2(.03f, .04f), new Vector2(.22f, .19f), "Hold", () => IssueSelectedOrder(SectorGroupOrder.Hold));
            CreateButton("Advance", groupRoot.transform, new Vector2(.25f, .04f), new Vector2(.49f, .19f), "Advance", () => IssueSelectedOrder(SectorGroupOrder.Advance));
            CreateButton("Withdraw", groupRoot.transform, new Vector2(.52f, .04f), new Vector2(.76f, .19f), "Withdraw", () => IssueSelectedOrder(SectorGroupOrder.Withdraw));
            groupRoot.SetActive(false);
            autoTurnButton = CreateButton("Auto Turn", commandDockRoot.transform, new Vector2(.78f, .045f), new Vector2(.885f, .20f), "AUTO 1/S", ToggleAutoAdvance);
            autoTurnButtonText = autoTurnButton.GetComponentInChildren<Text>();
            autoTurnButton.GetComponent<Image>().color = new Color(.20f, .25f, .12f, 1f);
            endTurnButton = CreateButton("End Turn", commandDockRoot.transform, new Vector2(.89f, .045f), new Vector2(.992f, .20f), "END TURN", EndTurn);
            endTurnButton.GetComponent<Image>().color = new Color(.34f, .17f, .055f, 1f);
            footerRoot = Panel("Footer Status", viewerRoot.transform, Vector2.zero, new Vector2(1f, .035f), new Color(.018f, .021f, .019f, 1f));
            footerText = CreateText("Footer Text", footerRoot.transform, 12, TextAnchor.MiddleLeft, new Color(.72f, .68f, .58f));
            footerText.text = "One mechanical unit. One battlefield exemplar. Command groups issue shared orders.";
            Anchor(footerText.rectTransform, new Vector2(.012f, 0f), new Vector2(.99f, 1f));
            movementText = CreateText("Selected Movement Path", field, 17, TextAnchor.MiddleCenter, new Color(1f, .82f, .22f));
            movementText.rectTransform.anchorMin = movementText.rectTransform.anchorMax = new Vector2(.5f, .5f); movementText.rectTransform.sizeDelta = new Vector2(250f, 36f); movementText.gameObject.SetActive(false);
            viewerRoot.SetActive(false);
        }

        private void CreateSectors()
        {
            for (int lane = 0; lane < 5; lane++) for (int depth = 0; depth < 5; depth++)
            {
                SectorCoord coordinate = new SectorCoord((BattleLane)lane, (BattleDepth)depth);
                GameObject root = Panel(coordinate.ToString(), field, Vector2.zero, Vector2.zero, TerrainColor(SectorTerrain.OpenPlain));
                SectorView sectorView = root.AddComponent<SectorView>(); sectorView.Coordinate = coordinate; sectorView.Owner = this;
                RectTransform rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(CellWidth - 4f, CellHeight - 4f); rect.anchoredPosition = SectorCentre(coordinate);
                Outline outline = root.AddComponent<Outline>(); outline.effectColor = new Color(1f, 1f, 1f, .10f); outline.effectDistance = new Vector2(1f, -1f);
                Text label = CreateText("Terrain Label", root.transform, 12, TextAnchor.LowerCenter, new Color(1f, 1f, 1f, .82f));
                Anchor(label.rectTransform, new Vector2(.03f, .02f), new Vector2(.97f, .38f)); label.raycastTarget = false;
                sectorView.DebugLabel = label;
                sectorRects[coordinate] = rect; sectorLabels[coordinate] = label;
            }
        }

        private void CreateAccessButton()
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate != null && candidate.scene.IsValid() && candidate.name == "Tile Battles") return;
            accessRoot = Panel("Sector Battles", canvas.transform, new Vector2(.005f, .44f), new Vector2(.11f, .56f), new Color(.18f, .07f, .035f, .95f));
            Button button = accessRoot.AddComponent<Button>(); button.onClick.AddListener(OpenRelevantBattle);
            accessText = CreateText("Label", accessRoot.transform, 16, TextAnchor.MiddleCenter, Color.white);
            Anchor(accessText.rectTransform, Vector2.zero, Vector2.one); accessRoot.SetActive(false);
        }

        private void CreateBattlePopup()
        {
            battlePopupRoot = Panel("Engaged Armies", canvas.transform, new Vector2(.35f, .28f), new Vector2(.65f, .72f), new Color(.055f, .065f, .05f, .985f));
            battlePopupText = CreateText("Engaged Armies Text", battlePopupRoot.transform, 16, TextAnchor.UpperLeft, Color.white);
            Anchor(battlePopupText.rectTransform, new Vector2(.06f, .20f), new Vector2(.94f, .94f));
            CreateButton("View Battlefield", battlePopupRoot.transform, new Vector2(.08f, .05f), new Vector2(.62f, .16f), "View Battlefield", () => OpenViewer(selected));
            CreateButton("Close", battlePopupRoot.transform, new Vector2(.66f, .05f), new Vector2(.92f, .16f), "Close", () => { battlePopupRoot.SetActive(false); selected = null; });
            battlePopupRoot.SetActive(false);
        }

        private void CreateDemoButton()
        {
            demoRoot = CreateButton("Custom Sector Battle", canvas.transform, new Vector2(.005f, .36f), new Vector2(.11f, .43f),
                "CUSTOM SECTOR BATTLE", () => manager?.CustomBattleLauncher?.Show()).gameObject;
            demoRoot.SetActive(Debug.isDebugBuild || Application.isEditor);
        }

        private void ToggleDebug()
        {
            showDebug = !showDebug;
            RefreshViewer();
        }

        private void ToggleReplay()
        {
            if (selected == null || selected.Replay.Count == 0) return;
            if (!replayMode) { replayMode = true; replayIndex = 0; replayPlaying = true; nextReplayFrame = Time.unscaledTime + .18f; selectedGroupId = -1; }
            else replayPlaying = !replayPlaying;
        }

        private void StepReplay(int direction)
        {
            if (selected == null || selected.Replay.Count == 0) return;
            replayMode = true; replayPlaying = false;
            replayIndex = Mathf.Clamp(replayIndex + direction, 0, selected.Replay.Count - 1);
        }

        private void ClearSectorHighlights()
        {
            foreach (RectTransform rect in sectorRects.Values)
            { Outline outline = rect.GetComponent<Outline>(); outline.effectColor = new Color(1f, 1f, 1f, showDebug ? .34f : .10f); outline.effectDistance = new Vector2(1f, -1f); }
        }

        private Vector2 SectorCentre(SectorCoord coordinate) => new Vector2(((int)coordinate.Depth - 2) * CellWidth, (2 - (int)coordinate.Lane) * CellHeight);
        private static string LaneName(BattleLane lane) => lane == BattleLane.TopFlank ? "TOP FLANK" :
            lane == BattleLane.UpperWing ? "UPPER WING" : lane == BattleLane.LowerWing ? "LOWER WING" :
            lane == BattleLane.BottomFlank ? "BOTTOM FLANK" : "CENTRE";
        private static string DepthName(BattleDepth depth) => depth == BattleDepth.SideAReserve ? "SIDE A RESERVE" :
            depth == BattleDepth.SideALine ? "SIDE A LINE" : depth == BattleDepth.SideBLine ? "SIDE B LINE" :
            depth == BattleDepth.SideBReserve ? "SIDE B RESERVE" : "CENTRAL GROUND";
        private static string RoleName(SectorFormationRole role) => role == SectorFormationRole.BaseFrontage ? "Base frontage" :
            role == SectorFormationRole.FlankAttacker ? "Flank attacker" : role == SectorFormationRole.RearFlankAttacker ? "Rear attacker" :
            role == SectorFormationRole.RangedSupport ? "Ranged support" : role.ToString();
        private static int Percent(int value, int maximum) => Mathf.Clamp(Mathf.RoundToInt(value * 100f / Mathf.Max(1, maximum)), 0, 100);
        private static int Average(List<SectorFormationPresentationState> states, Func<SectorFormationPresentationState, int> selector)
        {
            if (states == null || states.Count == 0) return 0;
            int total = 0; for (int i = 0; i < states.Count; i++) total += selector(states[i]);
            return Mathf.RoundToInt(total / (float)states.Count);
        }
        private static string DominantUnitName(List<SectorFormationPresentationState> states)
        {
            if (states == null || states.Count == 0) return "No active members";
            return states.GroupBy(item => string.IsNullOrEmpty(item.DisplayName) ? "Unit" : item.DisplayName)
                .OrderByDescending(group => group.Count()).ThenBy(group => group.Key).First().Key;
        }
        private static string MemberDots(int count)
        {
            if (count <= 0) return "-";
            return string.Join(" ", Enumerable.Repeat("◆", Mathf.Min(12, count))) + (count > 12 ? " +" + (count - 12) : string.Empty);
        }
        private static string DirectionArrow(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) return delta.x >= 0f ? "→" : "←";
            return delta.y >= 0f ? "↑" : "↓";
        }
        private static string TerrainName(SectorTerrain terrain)
        {
            switch (terrain)
            {
                case SectorTerrain.Forest: return "Forest";
                case SectorTerrain.Scrubland: return "Scrubland";
                case SectorTerrain.RockyGround: return "Rocky Ground";
                case SectorTerrain.Hill: return "Gentle Hill";
                case SectorTerrain.ShallowRiver: return "Shallow River";
                case SectorTerrain.DryPlain: return "Dry Plain";
                case SectorTerrain.Mountain: return "Mountain";
                default: return "Open Plain";
            }
        }
        private string FactionName(SectorCampaignBattle battle, int side)
        {
            if (battle == null) return side == 0 ? "Side A" : "Side B";
            string displayName = side == 0 ? battle.DisplayFactionA : battle.DisplayFactionB;
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            Faction displayFaction = side == 0 ? battle.DisplayFactionDefinitionA : battle.DisplayFactionDefinitionB;
            if (displayFaction != null && !string.IsNullOrEmpty(displayFaction.name)) return displayFaction.name;
            FieldArmy army = side == 0 ? battle.ArmyA?.fieldArmy : battle.ArmyB != null ? battle.ArmyB.fieldArmy : battle.Garrison;
            Nation nation = army != null ? army.nation : null;
            if (nation != null && nation.faction != null && !string.IsNullOrEmpty(nation.faction.name)) return nation.faction.name;
            return BattleName(battle, side);
        }
        private void CreateBattlefieldOrientationLabels(Transform parent)
        {
            string[] depths = { "SIDE A RESERVE", "SIDE A LINE", "CENTRAL GROUND", "SIDE B LINE", "SIDE B RESERVE" };
            for (int i = 0; i < depths.Length; i++)
            {
                Text label = CreateText(depths[i], field, 13, TextAnchor.MiddleCenter, new Color(.94f, .89f, .75f));
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
                label.rectTransform.sizeDelta = new Vector2(CellWidth, 26f);
                label.rectTransform.anchoredPosition = new Vector2((i - 2) * CellWidth, FieldHeight * .5f + 20f);
                label.raycastTarget = false;
            }
            string[] lanes = { "TOP FLANK", "UPPER WING", "CENTRE", "LOWER WING", "BOTTOM FLANK" };
            for (int i = 0; i < lanes.Length; i++)
            {
                Text label = CreateText(lanes[i], field, 13, TextAnchor.MiddleRight, new Color(.94f, .89f, .75f));
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
                label.rectTransform.sizeDelta = new Vector2(125f, CellHeight);
                label.rectTransform.anchoredPosition = new Vector2(-FieldWidth * .5f - 14f, (2 - i) * CellHeight);
                label.raycastTarget = false;
            }
        }
        private static Image CreateBar(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject background = Panel(name + " Background", parent, min, max, new Color(.03f, .035f, .03f, .95f));
            GameObject fill = Panel(name, background.transform, new Vector2(.02f, .16f), new Vector2(.98f, .84f), color);
            Image image = fill.GetComponent<Image>(); image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0;
            return image;
        }
        private static Color TerrainColor(SectorTerrain terrain)
        {
            switch (terrain) { case SectorTerrain.Forest: return new Color(.12f, .31f, .12f, .88f); case SectorTerrain.Scrubland: return new Color(.34f, .36f, .16f, .88f);
                case SectorTerrain.RockyGround: case SectorTerrain.Mountain: return new Color(.31f, .30f, .28f, .9f); case SectorTerrain.Hill: return new Color(.39f, .47f, .22f, .9f);
                case SectorTerrain.ShallowRiver: return new Color(.17f, .39f, .52f, .9f); case SectorTerrain.DryPlain: return new Color(.49f, .42f, .22f, .9f); default: return new Color(.24f, .48f, .20f, .88f); }
        }

        private string BattleName(SectorCampaignBattle battle, int side)
        {
            FieldArmy army = side == 0 ? battle.ArmyA != null ? battle.ArmyA.fieldArmy : null : battle.ArmyB != null ? battle.ArmyB.fieldArmy : battle.Garrison;
            return army != null && !string.IsNullOrEmpty(army.name) ? army.name : side == 0 ? "Side A" : "Side B";
        }

        private Material GetFactionMaterial(SectorFormationPresentationState state)
        {
            Nation nation = null;
            if (selected.ArmySources.TryGetValue(state.FormationId, out FieldArmyHolder holder) && holder != null && holder.fieldArmy != null) nation = holder.fieldArmy.nation;
            if (nation == null && state.Side == 1 && selected.Garrison != null) nation = selected.Garrison.nation;
            Faction faction = nation != null ? nation.faction : state.Side == 0
                ? selected.DisplayFactionDefinitionA : selected.DisplayFactionDefinitionB;
            string key = faction != null ? faction.name : "side" + state.Side;
            if (materialCache.TryGetValue(key, out Material cached)) return cached;
            if (baseUnitMaterial == null) return null;
            Material material = new Material(baseUnitMaterial) { name = "Sector Battle " + key };
            if (faction != null) { if (material.HasProperty("_FactionColor")) material.SetColor("_FactionColor", faction.color);
                if (material.HasProperty("_FactionColor2")) material.SetColor("_FactionColor2", faction.color2);
                if (material.HasProperty("_FactionColor3")) material.SetColor("_FactionColor3", faction.color3); }
            else
            {
                Color primary = state.Side == 0 ? new Color(.68f, .12f, .08f) : new Color(.08f, .22f, .62f);
                if (material.HasProperty("_FactionColor")) material.SetColor("_FactionColor", primary);
                if (material.HasProperty("_FactionColor2")) material.SetColor("_FactionColor2", new Color(.84f, .65f, .18f));
                if (material.HasProperty("_FactionColor3")) material.SetColor("_FactionColor3", new Color(.75f, .49f, .31f));
            }
            materialCache[key] = material; return material;
        }

        private static Material FindUnitMaterial()
        {
            SectorBattleVisualSettings settings = Resources.Load<SectorBattleVisualSettings>("SectorBattleVisualSettings");
            if (settings != null && settings.UnitMaterial != null) return settings.UnitMaterial;
            Material[] all = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == "New Material 1") return all[i];
            Debug.LogError("Sector Battle could not load its unit material. Assign New Material 1 in Resources/SectorBattleVisualSettings.");
            return null;
        }
        private static Sprite GetMarkerSprite()
        {
            if (markerSprite == null) markerSprite = Resources.Load<Sprite>("Map/buttony_square_stale");
            return markerSprite;
        }
        private static Canvas CreateCanvas()
        {
            GameObject root = new GameObject("Sector Battle Presentation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 175;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            return canvas;
        }
        private static GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        { GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); root.transform.SetParent(parent, false); RectTransform rect = root.GetComponent<RectTransform>(); Anchor(rect, min, max); root.GetComponent<Image>().color = color; return root; }
        private static Text CreateText(string name, Transform parent, int size, TextAnchor alignment, Color color)
        { GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); root.transform.SetParent(parent, false); Text text = root.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.alignment = alignment; text.color = color; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; return text; }
        private static Button CreateButton(string name, Transform parent, Vector2 min, Vector2 max, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject root = Panel(name, parent, min, max, new Color(.28f, .09f, .045f, .96f));
            Button button = root.AddComponent<Button>(); button.onClick.AddListener(action);
            Text text = CreateText("Label", root.transform, 17, TextAnchor.MiddleCenter, Color.white);
            text.text = label;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 8; text.resizeTextMaxSize = 17;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one); text.rectTransform.offsetMin = new Vector2(3f, 2f); text.rectTransform.offsetMax = new Vector2(-3f, -2f);
            text.transform.SetAsLastSibling();
            return button;
        }
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
