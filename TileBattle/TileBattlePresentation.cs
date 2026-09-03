using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectX.TileBattle
{
    public sealed class TileBattleVisualMotion : MonoBehaviour
    {
        public Vector2 Target;
        public int UnitId = int.MinValue;
        private Vector2 displayed, velocity, attackDirection;
        private float attackStart = -10f;
        private bool rangedAttack;
        private string attackStyle;
        private bool initialized;
        private bool pendingEntry;
        private bool fallen;
        private float fallStarted;
        private float fallDirection = 1f;

        public void Bind(int unitId)
        {
            if (UnitId == unitId) return;
            UnitId = unitId; initialized = false; pendingEntry = true; velocity = Vector2.zero; attackStart = -10f;
            fallen = false; fallStarted = -10f;
        }

        public void SetTargetWithEntry(Vector2 target, int side, float fieldWidth)
        {
            Target = target;
            if (!pendingEntry) return;
            displayed = new Vector2(side == 0 ? -fieldWidth * .56f : fieldWidth * .56f, target.y);
            initialized = true; velocity = Vector2.zero; pendingEntry = false;
        }

        public void TriggerAttack(Vector2 direction, bool ranged, string style)
        {
            attackDirection = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right;
            rangedAttack = ranged; attackStyle = style ?? string.Empty; attackStart = Time.unscaledTime;
        }

        public void RetreatOffField(int side, float fieldWidth)
        {
            Target = new Vector2(side == 0 ? -fieldWidth * .62f : fieldWidth * .62f, Target.y);
        }

        public void SetFallen(bool active, int side)
        {
            if (fallen == active) return;
            fallen = active; fallStarted = Time.unscaledTime;
            // Stable pseudo-random direction: varied corpses without replay/network visual disagreement.
            unchecked { fallDirection = ((UnitId * 1103515245 + 12345) & 1) == 0 ? -1f : 1f; }
            velocity = Vector2.zero; attackStart = -10f;
        }

        private void Update()
        {
            if (!initialized) { displayed = Target; initialized = true; }
            else displayed = Vector2.SmoothDamp(displayed, Target, ref velocity, .40f, Mathf.Infinity, Time.unscaledDeltaTime);
            float elapsed = Time.unscaledTime - attackStart;
            float progress = Mathf.Clamp01(elapsed / .5f);
            LayeredBattleUnitVisual layered = GetComponent<LayeredBattleUnitVisual>();
            bool legacyAnimation = layered != null && layered.UsesLegacyAnimator;
            float lunge = !legacyAnimation && elapsed >= 0f && elapsed < .5f ? Mathf.Sin(progress * Mathf.PI) * (rangedAttack ? -7f : 16f) : 0f;
            Vector2 perpendicular = new Vector2(-attackDirection.y, attackDirection.x);
            float swing = !legacyAnimation && !rangedAttack && !string.IsNullOrEmpty(attackStyle) &&
                attackStyle.IndexOf("swing", System.StringComparison.OrdinalIgnoreCase) >= 0 && elapsed >= 0f && elapsed < .5f
                ? Mathf.Sin(progress * Mathf.PI * 2f) * 7f : 0f;
            float moving = !fallen && Vector2.Distance(displayed, Target) > 1f ? Mathf.Sin(Time.unscaledTime * 12f) * .65f : 0f;
            float fallProgress = fallen ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - fallStarted) / .45f)) : 0f;
            ((RectTransform)transform).anchoredPosition = displayed + attackDirection * lunge + perpendicular * swing +
                Vector2.up * (moving - fallProgress * 13f);
            if (layered != null) layered.SetPresentationFallen(fallen, fallDirection * 90f * fallProgress);
        }

        private void OnDisable()
        {
            initialized = false; pendingEntry = true; velocity = Vector2.zero; attackStart = -10f;
            fallen = false; fallStarted = -10f;
            LayeredBattleUnitVisual layered = GetComponent<LayeredBattleUnitVisual>();
            if (layered != null) layered.SetPresentationFallen(false, 0f);
        }
    }

    public sealed class TileProjectileVisual : MonoBehaviour
    {
        public Vector2 StartPosition, EndPosition;
        public float Duration = .9f;
        public float VisualAngleOffset;
        private float startTime;
        public void Launch(Vector2 start, Vector2 end)
        {
            StartPosition = start; EndPosition = end; startTime = Time.unscaledTime;
            Vector2 direction = end - start;
            ApplyRotation(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            gameObject.SetActive(true);
        }
        private void Update()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - startTime) / Mathf.Max(.05f, Duration));
            Vector2 position = Vector2.Lerp(StartPosition, EndPosition, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * 18f;
            ((RectTransform)transform).anchoredPosition = position;
            Vector2 tangent = EndPosition - StartPosition;
            tangent.y += Mathf.Cos(progress * Mathf.PI) * 18f * Mathf.PI;
            if (tangent.sqrMagnitude > .001f)
                ApplyRotation(Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
            if (progress >= 1f) gameObject.SetActive(false);
        }

        private void ApplyRotation(float trajectoryAngle)
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, trajectoryAngle + VisualAngleOffset);
        }
    }

    public sealed class TileBattlePresentation : MonoBehaviour
    {
        [Header("Prototype casualty presentation")]
        [Tooltip("When enabled, destroyed formations remain on the battlefield as fallen visuals. Disable to restore pooled deletion.")]
        public bool LeaveFallenUnitsOnField = true;
        [Tooltip("Detach the displayed shield and weapon when a formation falls.")]
        public bool DropEquipmentOnDeath = true;
        private TileBattleCampaignManager manager;
        private readonly Dictionary<TileCampaignBattle, GameObject> markers = new Dictionary<TileCampaignBattle, GameObject>();
        private readonly List<Image> unitPool = new List<Image>();
        private readonly List<Image> projectilePool = new List<Image>();
        private readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private TileCampaignBattle selected;
        private Canvas canvas;
        private GameObject accessRoot, viewerRoot, summaryRoot;
        private Text accessText, headerText, debugText, summaryText;
        private Button summaryButton;
        private Text playbackText;
        private RectTransform field;
        private RectTransform gridRoot;
        private int displayedGridWidth = -1, displayedGridHeight = -1;
        private float nextRefresh;
        private int consumedEventCount;
        private int displayedProjectileCount;
        private readonly HashSet<int> cheeringUnits = new HashSet<int>();
        private int historyIndex;
        private bool followLive = true;
        private bool playbackPaused;
        private float playbackSpeed = 1f;
        private float nextPlaybackAdvance;
        private static Sprite squareSprite, markerSprite;
        private Material baseUnitMaterial;

        public void Initialize(TileBattleCampaignManager owner)
        {
            manager = owner; canvas = CreateCanvas(); baseUnitMaterial = FindUnitMaterial();
            BindSceneAccessButton(); CreateViewer();
        }

        private void Update()
        {
            DetectMarkerClick();
            if (Input.GetKeyDown(KeyCode.Escape) && viewerRoot != null && viewerRoot.activeSelf) CloseViewer();
            AdvancePlayback();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            SynchronizeMarkers(); RefreshAccess();
            if (selected != null && viewerRoot != null && viewerRoot.activeSelf) RefreshViewer();
        }

        private void SynchronizeMarkers()
        {
            if (manager == null) return;
            List<TileCampaignBattle> removed = new List<TileCampaignBattle>();
            foreach (KeyValuePair<TileCampaignBattle, GameObject> pair in markers)
                if (!manager.ActiveBattles.Contains(pair.Key)) removed.Add(pair.Key);
            for (int i = 0; i < removed.Count; i++)
            { if (markers[removed[i]] != null) Destroy(markers[removed[i]]); markers.Remove(removed[i]); }
            for (int i = 0; i < manager.ActiveBattles.Count; i++)
            {
                TileCampaignBattle battle = manager.ActiveBattles[i];
                if (!markers.TryGetValue(battle, out GameObject marker))
                {
                    marker = new GameObject("Tile Battle Marker " + battle.BattleId, typeof(SpriteRenderer));
                    marker.transform.SetParent(transform, true); marker.transform.localScale = Vector3.one * 0.02f;
                    SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>(); renderer.sprite = GetMarkerSprite();
                    renderer.color = new Color(.85f, .15f, .06f, .95f); renderer.sortingOrder = 100;
                    markers.Add(battle, marker);
                }
                Vector3 position = battle.ArmyA != null && battle.ArmyB != null
                    ? (battle.ArmyA.transform.position + battle.ArmyB.transform.position) * .5f : battle.MapPosition;
                marker.transform.position = new Vector3(position.x, position.y, -2f);
            }
        }

        private void DetectMarkerClick()
        {
            if (!Input.GetMouseButtonDown(0) || Camera.main == null) return;
            TileCampaignBattle closest = null; float distance = 16f;
            foreach (KeyValuePair<TileCampaignBattle, GameObject> pair in markers)
            {
                if (pair.Value == null) continue;
                Vector3 screen = Camera.main.WorldToScreenPoint(pair.Value.transform.position);
                float candidate = Vector2.Distance(Input.mousePosition, screen);
                if (screen.z >= 0f && candidate < distance) { closest = pair.Key; distance = candidate; }
            }
            if (closest != null) OpenViewer(closest);
        }

        private void BindSceneAccessButton()
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate != null && candidate.scene.IsValid() && candidate.name == "Tile Battles")
                { accessRoot = candidate; break; }
            if (accessRoot == null)
            {
                Debug.LogError("MapScene is missing the scene-authored 'Tile Battles' button.");
                return;
            }
            Button button = accessRoot.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("The scene-authored 'Tile Battles' object requires a Button component.");
                return;
            }
            button.onClick.RemoveListener(OpenRelevantBattle);
            button.onClick.AddListener(OpenRelevantBattle);
            accessText = accessRoot.GetComponentInChildren<Text>(true);
            if (accessText != null)
            {
                accessText.resizeTextForBestFit = true;
                accessText.resizeTextMinSize = 8;
                accessText.resizeTextMaxSize = 12;
            }
            accessRoot.SetActive(false);
        }

        private void CreateViewer()
        {
            viewerRoot = Panel("Tile Battle Viewer", canvas.transform, new Vector2(.06f, .06f), new Vector2(.94f, .94f), new Color(.08f, .09f, .07f, .98f));
            headerText = Label("Header", viewerRoot.transform, 16, TextAnchor.MiddleLeft);
            RectTransform header = headerText.rectTransform; header.anchorMin = new Vector2(.02f, .92f); header.anchorMax = new Vector2(.80f, .99f); header.offsetMin = header.offsetMax = Vector2.zero;
            Button close = ButtonWithText("Close", viewerRoot.transform, new Vector2(.89f, .93f), new Vector2(.98f, .985f), "X"); close.onClick.AddListener(CloseViewer);
            Button replay = ButtonWithText("Replay", viewerRoot.transform, new Vector2(.52f, .93f), new Vector2(.60f, .985f), "Replay"); replay.onClick.AddListener(StartReplay);
            Button previous = ButtonWithText("Previous", viewerRoot.transform, new Vector2(.61f, .93f), new Vector2(.66f, .985f), "<"); previous.onClick.AddListener(PreviousFrame);
            Button pause = ButtonWithText("Pause", viewerRoot.transform, new Vector2(.67f, .93f), new Vector2(.74f, .985f), "Pause");
            playbackText = pause.GetComponentInChildren<Text>(); pause.onClick.AddListener(TogglePlayback);
            Button next = ButtonWithText("Next", viewerRoot.transform, new Vector2(.75f, .93f), new Vector2(.80f, .985f), ">"); next.onClick.AddListener(NextFrame);
            Button speed = ButtonWithText("Speed", viewerRoot.transform, new Vector2(.81f, .93f), new Vector2(.88f, .985f), "1x"); speed.onClick.AddListener(() => CycleSpeed(speed.GetComponentInChildren<Text>()));
            summaryButton = ButtonWithText("Summary", viewerRoot.transform, new Vector2(.42f, .93f), new Vector2(.51f, .985f), "Summary");
            summaryButton.onClick.AddListener(OpenSummary); summaryButton.gameObject.SetActive(false);
            GameObject fieldObject = Panel("Battlefield", viewerRoot.transform, new Vector2(.03f, .06f), new Vector2(.76f, .91f), new Color(.35f, .56f, .25f, 1f));
            fieldObject.AddComponent<RectMask2D>();
            field = fieldObject.GetComponent<RectTransform>();
            GameObject grid = new GameObject("Tile Grid", typeof(RectTransform)); grid.transform.SetParent(field, false);
            gridRoot = grid.GetComponent<RectTransform>(); Stretch(gridRoot);
            debugText = Label("Debug", viewerRoot.transform, 11, TextAnchor.UpperLeft);
            RectTransform debug = debugText.rectTransform; debug.anchorMin = new Vector2(.78f, .07f); debug.anchorMax = new Vector2(.98f, .91f); debug.offsetMin = debug.offsetMax = Vector2.zero;
            debugText.horizontalOverflow = HorizontalWrapMode.Wrap; debugText.verticalOverflow = VerticalWrapMode.Overflow;
            summaryRoot = Panel("Battle Summary", viewerRoot.transform, new Vector2(.12f, .10f), new Vector2(.88f, .90f), new Color(.10f, .11f, .08f, .99f));
            summaryText = Label("Summary Text", summaryRoot.transform, 14, TextAnchor.UpperLeft);
            RectTransform summaryRect = summaryText.rectTransform; summaryRect.anchorMin = new Vector2(.04f, .05f);
            summaryRect.anchorMax = new Vector2(.96f, .92f); summaryRect.offsetMin = summaryRect.offsetMax = Vector2.zero;
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap; summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            Button closeSummary = ButtonWithText("Close Summary", summaryRoot.transform, new Vector2(.82f, .93f), new Vector2(.97f, .99f), "Close");
            closeSummary.onClick.AddListener(() => summaryRoot.SetActive(false)); summaryRoot.SetActive(false);
            viewerRoot.SetActive(false);
        }

        private void OpenRelevantBattle()
        {
            if (manager == null || manager.ActiveBattles.Count == 0) return;
            TileCampaignBattle battle = manager.ActiveBattles.Find(item => item.ArmyA != null && item.ArmyA.IsFriendlyToLocalPlayer() ||
                item.ArmyB != null && item.ArmyB.IsFriendlyToLocalPlayer());
            OpenViewer(battle ?? manager.ActiveBattles[0]);
        }

        public void OpenViewer(TileCampaignBattle battle)
        {
            selected = battle;
            followLive = true; playbackPaused = false; historyIndex = battle != null ? Mathf.Max(0, battle.Simulation.History.Count - 1) : 0;
            consumedEventCount = battle != null ? EventCountBeforeSnapshot(battle.Simulation, historyIndex) : 0;
            displayedProjectileCount = 0;
            cheeringUnits.Clear();
            nextPlaybackAdvance = Time.unscaledTime;
            viewerRoot.SetActive(battle != null); if (battle != null) RefreshViewer();
        }

        public void CloseViewer()
        {
            selected = null; if (viewerRoot != null) viewerRoot.SetActive(false);
            if (summaryRoot != null) summaryRoot.SetActive(false);
            for (int i = 0; i < unitPool.Count; i++) if (unitPool[i] != null)
            {
                LayeredBattleUnitVisual art = unitPool[i].GetComponent<LayeredBattleUnitVisual>();
                if (art != null) art.RestoreEquipment();
                unitPool[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < projectilePool.Count; i++) if (projectilePool[i] != null) projectilePool[i].gameObject.SetActive(false);
        }

        private void RefreshAccess()
        {
            bool active = manager != null && manager.ActiveBattles.Count > 0;
            if (accessRoot != null) accessRoot.SetActive(active);
            if (active && accessText != null) accessText.text = "TILE BATTLES: " + manager.ActiveBattles.Count + "\nClick to watch";
        }

        private void EnsureGrid(int width, int height)
        {
            if (gridRoot == null || width <= 0 || height <= 0 ||
                displayedGridWidth == width && displayedGridHeight == height) return;
            for (int i = gridRoot.childCount - 1; i >= 0; i--) Destroy(gridRoot.GetChild(i).gameObject);
            displayedGridWidth = width; displayedGridHeight = height;
            Color regular = new Color(.08f, .13f, .06f, .20f);
            Color centre = new Color(.06f, .09f, .04f, .38f);
            for (int x = 0; x <= width; x++)
                CreateGridLine("Column " + x, new Vector2((float)x / width, 0f), new Vector2((float)x / width, 1f),
                    x == width / 2 ? centre : regular, x == width / 2 ? 2f : 1f, true);
            for (int y = 0; y <= height; y++)
                CreateGridLine("Row " + y, new Vector2(0f, (float)y / height), new Vector2(1f, (float)y / height),
                    y == height / 2 ? centre : regular, y == height / 2 ? 2f : 1f, false);
            gridRoot.SetAsFirstSibling();
        }

        private void CreateGridLine(string name, Vector2 anchorMin, Vector2 anchorMax, Color color, float thickness, bool vertical)
        {
            GameObject line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(gridRoot, false);
            RectTransform rect = line.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = vertical ? new Vector2(-thickness * .5f, 0f) : new Vector2(0f, -thickness * .5f);
            rect.offsetMax = vertical ? new Vector2(thickness * .5f, 0f) : new Vector2(0f, thickness * .5f);
            Image image = line.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        }

        private void RefreshViewer()
        {
            if (manager != null)
            {
                LeaveFallenUnitsOnField = manager.LeaveFallenUnitsOnField;
                DropEquipmentOnDeath = manager.DropEquipmentOnDeath;
            }
            TileBattleSimulation simulation = selected.Simulation;
            if (simulation.History.Count == 0) return;
            historyIndex = Mathf.Clamp(historyIndex, 0, simulation.History.Count - 1);
            TileBattleRoundSnapshot snapshot = simulation.History[historyIndex];
            EnsureGrid(simulation.Grid.Width, simulation.Grid.Height);
            string left = !string.IsNullOrEmpty(selected.LeftDisplayName) ? selected.LeftDisplayName : "Left";
            string right = !string.IsNullOrEmpty(selected.RightDisplayName) ? selected.RightDisplayName : "Right";
            headerText.text = left + "     vs     " + right + "     Round " + snapshot.CommandRound + " / Tick " + snapshot.ResolutionTick +
                "     " + snapshot.Phase + (followLive ? "  [LIVE]" : playbackPaused ? "  [PAUSED]" : "  [REPLAY " + playbackSpeed + "x]") +
                (Owners.Instance != null && Owners.Instance.CampaignPaused ? "  [CAMPAIGN PAUSED — SPACE]" : string.Empty);
            HashSet<int> visibleUnitIds = new HashSet<int>();
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                TileBattleUnitViewState unit = snapshot.Units[i];
                bool destroyed = unit.Strength <= 0 || unit.State == TileUnitState.Destroyed;
                if (!unit.Deployed || unit.State == TileUnitState.Withdrawn || destroyed && !LeaveFallenUnitsOnField) continue;
                visibleUnitIds.Add(unit.Id);
                Image image = AcquireUnit(unit.Id); UnitSaveData data = FindUnitData(selected, unit.Id);
                LayeredBattleUnitVisual art = image.GetComponent<LayeredBattleUnitVisual>();
                if (art == null) art = image.gameObject.AddComponent<LayeredBattleUnitVisual>();
                art.Configure(data, GetFactionMaterial(selected, unit.Side, unit.Id, data)); art.Attacking = false;
                ApplySnapshotWeapon(art, data, unit);
                art.SetHorizontalFacing(unit.Facing == TileFacing.West || unit.Facing != TileFacing.East && unit.Side == 1);
                RectTransform rect = image.rectTransform;
                float x = (unit.Position.X + .5f) / simulation.Grid.Width;
                float y = (unit.Position.Y + .5f) / simulation.Grid.Height;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = data != null && data.Big ? new Vector2(96, 96) : new Vector2(72, 72);
                TileBattleVisualMotion motion = image.GetComponent<TileBattleVisualMotion>(); if (motion == null) motion = image.gameObject.AddComponent<TileBattleVisualMotion>();
                motion.Bind(unit.Id); motion.SetTargetWithEntry(new Vector2((x - .5f) * field.rect.width,
                    (y - .5f) * field.rect.height), unit.Side, field.rect.width);
                motion.SetFallen(destroyed, unit.Side);
                if (destroyed)
                {
                    art.SetContinuousCheer(false);
                    if (DropEquipmentOnDeath) art.DropEquipment(unit.Side == 0 ? -1f : 1f);
                    else art.RestoreEquipment();
                    image.color = new Color(.72f, .72f, .72f, .82f);
                }
                else
                {
                    art.RestoreEquipment();
                    image.color = unit.State == TileUnitState.Routing ? new Color(1f, 1f, 1f, .55f) : Color.white;
                }
            }
            for (int i = 0; i < unitPool.Count; i++)
            {
                TileBattleVisualMotion motion = unitPool[i].GetComponent<TileBattleVisualMotion>();
                if (motion != null && !visibleUnitIds.Contains(motion.UnitId))
                {
                    LayeredBattleUnitVisual art = unitPool[i].GetComponent<LayeredBattleUnitVisual>();
                    if (art != null) art.RestoreEquipment();
                    unitPool[i].gameObject.SetActive(false);
                }
            }
            ConsumeVisualEvents(simulation, snapshot.EventCount);
            // The projectile event is allowed to hold the ranged weapon for its attack frame.
            // On the next viewer refresh ApplySnapshotWeapon selects melee when ammo is empty.
            ApplyFinishedBattlePresentation(simulation, snapshot);
            RefreshDebug(simulation);
            if (summaryButton != null) summaryButton.gameObject.SetActive(simulation.Result.Finished);
        }

        private static void ApplySnapshotWeapon(LayeredBattleUnitVisual art, UnitSaveData data,
            TileBattleUnitViewState unit)
        {
            if (art == null || data == null || unit == null) return;
            Weapon weapon = unit.Ammunition > 0 && data.RangedWeapon != null
                ? data.RangedWeapon : data.MeleeWeapon;
            art.SetPresentedWeapon(weapon);
        }

        private void ApplyFinishedBattlePresentation(TileBattleSimulation simulation, TileBattleRoundSnapshot snapshot)
        {
            bool finalFrame = simulation.Result.Finished && historyIndex == simulation.History.Count - 1;
            if (!finalFrame)
            {
                cheeringUnits.Clear();
                for (int i = 0; i < unitPool.Count; i++)
                {
                    LayeredBattleUnitVisual art = unitPool[i] != null ? unitPool[i].GetComponent<LayeredBattleUnitVisual>() : null;
                    if (art == null) continue;
                    art.SetContinuousCheer(false);
                }
                return;
            }
            int winner = simulation.Result.WinningSide;
            if (winner < 0) return;
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                TileBattleUnitViewState unit = snapshot.Units[i];
                if (!unit.Deployed || unit.Strength <= 0 || unit.State == TileUnitState.Destroyed || unit.State == TileUnitState.Withdrawn) continue;
                TileBattleVisualMotion motion = FindMotion(unit.Id);
                if (motion == null) continue;
                LayeredBattleUnitVisual art = motion.GetComponent<LayeredBattleUnitVisual>();
                if (unit.Side != winner)
                {
                    if (art != null)
                    {
                        art.SetContinuousCheer(false);
                        art.DropEquipment(unit.Side == 0 ? -1f : 1f);
                    }
                    motion.RetreatOffField(unit.Side, field.rect.width);
                    if (art != null) art.SetHorizontalFacing(unit.Side == 0);
                }
                else if (art != null)
                {
                    cheeringUnits.Add(unit.Id);
                    art.RestoreEquipment();
                    art.SetContinuousCheer(true);
                }
            }
        }

        private void ConsumeVisualEvents(TileBattleSimulation simulation, int eventLimit)
        {
            eventLimit = Mathf.Clamp(eventLimit, 0, simulation.Events.Count);
            if (consumedEventCount > eventLimit) { consumedEventCount = eventLimit; return; }
            consumedEventCount = Mathf.Clamp(consumedEventCount, 0, eventLimit);
            for (int i = consumedEventCount; i < eventLimit; i++)
            {
                TileBattleEvent battleEvent = simulation.Events[i];
                if (battleEvent.Type == TileBattleEventType.ProjectileLaunched)
                {
                    SpawnProjectile(battleEvent); continue;
                }
                if (battleEvent.Type == TileBattleEventType.UnitDamaged)
                {
                    TileBattleVisualMotion damagedMotion = FindMotion(battleEvent.UnitId);
                    LayeredBattleUnitVisual damagedArt = damagedMotion != null ? damagedMotion.GetComponent<LayeredBattleUnitVisual>() : null;
                    if (damagedArt != null) damagedArt.TriggerLegacyHurt();
                    continue;
                }
                if (battleEvent.Type != TileBattleEventType.UnitAttacked) continue;
                TileBattleVisualMotion motion = FindMotion(battleEvent.UnitId);
                if (motion == null) continue;
                Vector2 direction = new Vector2(battleEvent.To.X - battleEvent.From.X, battleEvent.To.Y - battleEvent.From.Y);
                UnitSaveData data = FindUnitData(selected, battleEvent.UnitId);
                bool ranged = battleEvent.RangedAttack;
                Weapon weapon = data != null ? (ranged ? data.RangedWeapon : data.MeleeWeapon) : null;
                motion.TriggerAttack(direction, ranged, weapon != null ? weapon.BattleAnimationType : string.Empty);
                LayeredBattleUnitVisual art = motion.GetComponent<LayeredBattleUnitVisual>();
                if (art != null) { art.SetPresentedWeapon(weapon); art.Attacking = true; art.TriggerLegacyAttack(); }
            }
            consumedEventCount = eventLimit;
        }

        private void AdvancePlayback()
        {
            if (selected == null || playbackPaused || selected.Simulation.History.Count == 0 || Time.unscaledTime < nextPlaybackAdvance) return;
            if (followLive && Owners.Instance != null && Owners.Instance.CampaignPaused) return;
            if (historyIndex < selected.Simulation.History.Count - 1)
            {
                TileBattleRoundSnapshot from = selected.Simulation.History[historyIndex++];
                TileBattleRoundSnapshot to = selected.Simulation.History[historyIndex];
                int tickDistance = to.CommandRound == from.CommandRound
                    ? Mathf.Max(1, to.ResolutionTick - from.ResolutionTick) : Mathf.Max(1, to.ResolutionTick);
                float campaignMultiplier = followLive && Owners.Instance != null
                    ? Mathf.Max(.01f, Owners.Instance.CampaignSimulationSpeed) : 1f;
                nextPlaybackAdvance = Time.unscaledTime + tickDistance * .08f / (Mathf.Max(.25f, playbackSpeed) * campaignMultiplier);
            }
            else if (!followLive) playbackPaused = true;
        }

        private void StartReplay()
        {
            if (selected == null || selected.Simulation.History.Count == 0) return;
            followLive = false; playbackPaused = false; historyIndex = 0;
            consumedEventCount = 0; displayedProjectileCount = 0; cheeringUnits.Clear(); nextPlaybackAdvance = Time.unscaledTime;
            for (int i = 0; i < unitPool.Count; i++)
            {
                LayeredBattleUnitVisual art = unitPool[i] != null ? unitPool[i].GetComponent<LayeredBattleUnitVisual>() : null;
                if (art != null) art.RestoreEquipment();
            }
            for (int i = 0; i < projectilePool.Count; i++) projectilePool[i].gameObject.SetActive(false);
        }

        private static int EventCountBeforeSnapshot(TileBattleSimulation simulation, int snapshotIndex)
        {
            return simulation != null && snapshotIndex > 0 && snapshotIndex <= simulation.History.Count
                ? simulation.History[snapshotIndex - 1].EventCount : 0;
        }

        private void PreviousFrame()
        {
            if (selected == null) return; followLive = false; playbackPaused = true;
            historyIndex = Mathf.Max(0, historyIndex - 1);
            if (selected.Simulation.History.Count > 0) consumedEventCount = selected.Simulation.History[historyIndex].EventCount;
        }

        private void NextFrame()
        {
            if (selected == null) return; followLive = false; playbackPaused = true;
            historyIndex = Mathf.Min(selected.Simulation.History.Count - 1, historyIndex + 1);
        }

        private void TogglePlayback()
        {
            if (selected == null) return;
            if (followLive) { followLive = false; playbackPaused = true; }
            else playbackPaused = !playbackPaused;
            if (playbackText != null) playbackText.text = playbackPaused ? "Play" : "Pause";
        }

        private void CycleSpeed(Text label)
        {
            playbackSpeed = playbackSpeed < .75f ? 1f : playbackSpeed < 1.5f ? 2f : playbackSpeed < 3f ? 4f : .5f;
            if (label != null) label.text = playbackSpeed + "x";
            followLive = false; playbackPaused = false; nextPlaybackAdvance = Time.unscaledTime;
        }

        private void OpenSummary()
        {
            if (selected == null || !selected.Simulation.Result.Finished || summaryRoot == null) return;
            TileBattleSimulation simulation = selected.Simulation;
            string left = GeneralName(simulation.LeftGeneral, "Left Army");
            string right = GeneralName(simulation.RightGeneral, "Right Army");
            string winner = simulation.Result.WinningSide == 0 ? left : simulation.Result.WinningSide == 1 ? right : "Draw";
            StringBuilder text = new StringBuilder(1800);
            text.AppendLine("BATTLE SUMMARY").Append("Winner: ").AppendLine(winner)
                .Append("Duration: ").Append(simulation.Result.CommandRounds).AppendLine(" command rounds")
                .Append("Ended because: ").AppendLine(string.IsNullOrEmpty(simulation.Result.EndReason)
                    ? "No termination reason was recorded" : simulation.Result.EndReason).AppendLine();
            AppendSummaryGeneral(text, "LEFT", simulation.LeftGeneral);
            AppendSummaryGeneral(text, "RIGHT", simulation.RightGeneral);
            AppendCasualtySummary(text, "LEFT ARMY CASUALTIES", simulation, 0);
            AppendCasualtySummary(text, "RIGHT ARMY CASUALTIES", simulation, 1);
            summaryText.text = text.ToString(); summaryRoot.SetActive(true); summaryRoot.transform.SetAsLastSibling();
        }

        private static string GeneralName(ITileBattleGeneral general, string fallback)
        {
            return general != null && general.DebugState != null && !string.IsNullOrEmpty(general.DebugState.GeneralName)
                ? general.DebugState.GeneralName : fallback;
        }

        private static void AppendSummaryGeneral(StringBuilder text, string side, ITileBattleGeneral general)
        {
            if (general == null) { text.Append(side).AppendLine(" GENERAL: none").AppendLine(); return; }
            TileGeneralDebugState state = general.DebugState;
            text.Append(side).Append(" GENERAL: ").AppendLine(GeneralName(general, side))
                .Append("Plan: ").AppendLine(state.CurrentPlan.ToString())
                .Append("Plan policy: ").AppendLine(state.ChangeReason ?? "No plan explanation recorded").AppendLine();
        }

        private static void AppendCasualtySummary(StringBuilder text, string heading, TileBattleSimulation simulation, int side)
        {
            Dictionary<string, int[]> totals = new Dictionary<string, int[]>();
            int armyStart = 0, armyRemaining = 0;
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                TileBattleUnit unit = simulation.Units[i]; if (unit.Side != side) continue;
                string name = unit.Definition != null && !string.IsNullOrEmpty(unit.Definition.DisplayName)
                    ? unit.Definition.DisplayName : "Unknown formation";
                if (!totals.TryGetValue(name, out int[] values)) { values = new int[3]; totals.Add(name, values); }
                int starting = unit.Definition != null ? unit.Definition.Strength : unit.Strength;
                int remaining = simulation.Result.RemainingStrength.TryGetValue(unit.Id, out int saved) ? saved : Mathf.Max(0, unit.Strength);
                values[0]++; values[1] += starting; values[2] += remaining;
                armyStart += starting; armyRemaining += remaining;
            }
            text.AppendLine(heading).Append("Total: ").Append(armyStart - armyRemaining).Append(" / ").Append(armyStart)
                .Append(" strength lost; ").Append(armyRemaining).AppendLine(" remaining");
            List<string> names = new List<string>(totals.Keys); names.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
            {
                int[] values = totals[names[i]];
                text.Append("  ").Append(names[i]).Append(" x").Append(values[0]).Append(": ")
                    .Append(values[1] - values[2]).Append(" / ").Append(values[1]).Append(" lost; ")
                    .Append(values[2]).AppendLine(" remaining");
            }
            Dictionary<string, int> recovered = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                TileBattleUnit unit = simulation.Units[i];
                if (unit.Side != side || !simulation.Result.RecoveredFormations.TryGetValue(unit.Id, out int amount) || amount <= 0)
                    continue;
                string name = unit.Definition != null && !string.IsNullOrEmpty(unit.Definition.DisplayName)
                    ? unit.Definition.DisplayName : "Unknown formation";
                recovered[name] = recovered.TryGetValue(name, out int existing) ? existing + amount : amount;
            }
            if (recovered.Count > 0)
            {
                int recoveredTotal = 0;
                foreach (int amount in recovered.Values) recoveredTotal += amount;
                text.Append("Recovered after victory: ").Append(recoveredTotal).AppendLine(" formation(s)");
                List<string> recoveredNames = new List<string>(recovered.Keys);
                recoveredNames.Sort(System.StringComparer.Ordinal);
                for (int i = 0; i < recoveredNames.Count; i++)
                    text.Append("  + ").Append(recoveredNames[i]).Append(" x").AppendLine(recovered[recoveredNames[i]].ToString());
            }
            else text.AppendLine("Recovered after victory: none");

            Dictionary<string, int> lost = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                TileBattleUnit unit = simulation.Units[i];
                if (unit.Side != side) continue;
                int remaining = simulation.Result.RemainingStrength.TryGetValue(unit.Id, out int saved)
                    ? saved : Mathf.Max(0, unit.Strength);
                if (remaining > 0) continue;
                int recoveredAmount = simulation.Result.RecoveredFormations.TryGetValue(unit.Id, out int restored)
                    ? Mathf.Max(0, restored) : 0;
                int lostAmount = Mathf.Max(0, 1 - recoveredAmount);
                if (lostAmount == 0) continue;
                string name = unit.Definition != null && !string.IsNullOrEmpty(unit.Definition.DisplayName)
                    ? unit.Definition.DisplayName : "Unknown formation";
                lost[name] = lost.TryGetValue(name, out int existing) ? existing + lostAmount : lostAmount;
            }
            if (lost.Count > 0)
            {
                int lostTotal = 0;
                foreach (int amount in lost.Values) lostTotal += amount;
                text.Append("Lost units: ").Append(lostTotal).AppendLine(" formation(s)");
                List<string> lostNames = new List<string>(lost.Keys);
                lostNames.Sort(System.StringComparer.Ordinal);
                for (int i = 0; i < lostNames.Count; i++)
                    text.Append("  - ").Append(lostNames[i]).Append(" x").AppendLine(lost[lostNames[i]].ToString());
            }
            else text.AppendLine("Lost units: none");
            text.AppendLine();
        }

        private void SpawnProjectile(TileBattleEvent battleEvent)
        {
            Image image = AcquireProjectile(); UnitSaveData data = FindUnitData(selected, battleEvent.UnitId);
            Weapon weapon = data != null ? data.RangedWeapon : null;
            image.sprite = ProjectileSprite(data, weapon); image.type = Image.Type.Simple; image.preserveAspect = true;
            // Throwable art should retain its source colours. The faction recolour shader is intended
            // for layered unit sprites and can make thin projectile sprites effectively transparent.
            image.material = null; image.color = Color.white;
            RectTransform rect = image.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(190f, 90f);
            Transform obsoleteStreak = image.transform.Find("Visibility Streak");
            if (obsoleteStreak != null) obsoleteStreak.gameObject.SetActive(false);
            Vector2 start = BoardPosition(battleEvent.From); Vector2 end = BoardPosition(battleEvent.To);
            TileProjectileVisual projectile = image.GetComponent<TileProjectileVisual>();
            if (projectile == null) projectile = image.gameObject.AddComponent<TileProjectileVisual>();
            projectile.Duration = Mathf.Max(.9f, .16f / Mathf.Max(.25f, playbackSpeed));
            projectile.VisualAngleOffset = weapon != null ? weapon.BattleProjectileAngleOffset : 0f;
            image.transform.SetAsLastSibling();
            projectile.Launch(start, end);
            displayedProjectileCount++;
        }

        private Vector2 BoardPosition(TileCoord coordinate)
        {
            if (selected == null) return Vector2.zero;
            return new Vector2(((coordinate.X + .5f) / selected.Simulation.Grid.Width - .5f) * field.rect.width,
                ((coordinate.Y + .5f) / selected.Simulation.Grid.Height - .5f) * field.rect.height);
        }

        private Image AcquireProjectile()
        {
            for (int i = 0; i < projectilePool.Count; i++) if (!projectilePool[i].gameObject.activeSelf)
            { projectilePool[i].gameObject.SetActive(true); return projectilePool[i]; }
            GameObject root = new GameObject("Tile Projectile", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(field, false); Image image = root.GetComponent<Image>(); image.raycastTarget = false;
            projectilePool.Add(image); return image;
        }

        private static Sprite ProjectileSprite(UnitSaveData unit, Weapon weapon)
        {
            if (weapon != null && weapon.BattleProjectileSprite != null) return weapon.BattleProjectileSprite;
            if (weapon != null && weapon.Throwable != null)
            {
                SpriteRenderer renderer = weapon.Throwable.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null && renderer.sprite != null) return renderer.sprite;
            }
            // weapon.sprite is the held/equipped weapon art, not the launched projectile.
            // Never render it in flight when a throwable-specific sprite is unavailable.
            return GetSquareSprite();
        }

        private TileBattleVisualMotion FindMotion(int unitId)
        {
            for (int i = 0; i < unitPool.Count; i++)
            {
                if (!unitPool[i].gameObject.activeSelf) continue;
                TileBattleVisualMotion motion = unitPool[i].GetComponent<TileBattleVisualMotion>();
                if (motion != null && motion.UnitId == unitId) return motion;
            }
            return null;
        }

        private void RefreshDebug(TileBattleSimulation simulation)
        {
            StringBuilder text = new StringBuilder(1200);
            AppendGeneral(text, "LEFT", simulation.LeftGeneral);
            AppendGeneral(text, "RIGHT", simulation.RightGeneral);
            int rangedAttacks = simulation.Events.FindAll(item => item.Type == TileBattleEventType.UnitAttacked && item.RangedAttack).Count;
            int projectileEvents = simulation.Events.FindAll(item => item.Type == TileBattleEventType.ProjectileLaunched).Count;
            int activeProjectiles = projectilePool.FindAll(item => item != null && item.gameObject.activeSelf).Count;
            text.AppendLine().Append("Active formations: ").Append(simulation.Units.FindAll(item => item.Active).Count)
                .Append("\nEvents: ").Append(simulation.Events.Count)
                .Append("\nRanged attacks: ").Append(rangedAttacks)
                .Append("\nProjectile events: ").Append(projectileEvents)
                .Append("\nProjectiles displayed: ").Append(displayedProjectileCount)
                .Append("\nProjectiles visible now: ").Append(activeProjectiles)
                .AppendLine("\n\nRecent events:");
            int start = Mathf.Max(0, simulation.Events.Count - 12);
            for (int i = start; i < simulation.Events.Count; i++) text.AppendLine(simulation.Events[i].ToString());
            debugText.text = text.ToString();
        }

        private static void AppendGeneral(StringBuilder text, string side, ITileBattleGeneral general)
        {
            if (general == null) return; TileGeneralDebugState debug = general.DebugState;
            text.Append(side).Append(": ").Append(debug.GeneralName).Append("\nPlan: ").Append(debug.CurrentPlan)
                .Append("\nReason: ").Append(debug.ChangeReason).AppendLine();
            for (int i = 0; i < debug.PlansConsidered.Count; i++)
            { TilePlanScore score = debug.PlansConsidered[i]; text.Append("  ").Append(score.Plan).Append(": ").Append(score.Total).AppendLine(); }
            text.AppendLine();
        }

        private Image AcquireUnit(int unitId)
        {
            for (int i = 0; i < unitPool.Count; i++)
            {
                TileBattleVisualMotion existing = unitPool[i].GetComponent<TileBattleVisualMotion>();
                if (existing != null && existing.UnitId == unitId)
                { if (!unitPool[i].gameObject.activeSelf) unitPool[i].gameObject.SetActive(true); return unitPool[i]; }
            }
            for (int i = 0; i < unitPool.Count; i++) if (!unitPool[i].gameObject.activeSelf)
            {
                unitPool[i].gameObject.SetActive(true);
                LayeredBattleUnitVisual art = unitPool[i].GetComponent<LayeredBattleUnitVisual>();
                if (art != null) art.RestoreEquipment();
                TileBattleVisualMotion recycled = unitPool[i].GetComponent<TileBattleVisualMotion>();
                if (recycled != null) recycled.Bind(unitId);
                return unitPool[i];
            }
            GameObject root = new GameObject("Tile Formation", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(field, false); Image image = root.GetComponent<Image>(); image.raycastTarget = false;
            TileBattleVisualMotion motion = root.AddComponent<TileBattleVisualMotion>(); motion.Bind(unitId);
            unitPool.Add(image); return image;
        }

        private static UnitSaveData FindUnitData(TileCampaignBattle battle, int unitId)
        {
            if (battle.UnitSources.TryGetValue(unitId, out UnitSaveData cached) && cached != null) return cached;
            if (!battle.UnitSourceNames.TryGetValue(unitId, out string name)) return null;
            FieldArmy army = unitId >= 10000 ? battle.ArmyB != null ? battle.ArmyB.fieldArmy : battle.Garrison : battle.ArmyA != null ? battle.ArmyA.fieldArmy : null;
            if (army != null)
            {
                ArmyReserves reserve = army.USDReserves.Find(item => item != null && item.USD != null && item.USD.name == name);
                if (reserve != null) return reserve.USD;
            }
            UnitSaveData[] all = Resources.LoadAll<UnitSaveData>("Prefabs/Units");
            return System.Array.Find(all, item => item != null && item.name == name);
        }

        private Material GetFactionMaterial(TileCampaignBattle battle, int side, int unitId, UnitSaveData unit)
        {
            FieldArmy army = side == 0 ? battle.ArmyA != null ? battle.ArmyA.fieldArmy : null : battle.ArmyB != null ? battle.ArmyB.fieldArmy : battle.Garrison;
            Faction faction = army != null && army.nation != null ? army.nation.faction : null;
            ArmyFormationRecord record = null;
            battle.UnitFormationSources.TryGetValue(unitId, out record);
            bool mercenary = record != null ? record.origin == CampaignUnitOrigin.Mercenary : unit != null && unit.Mercenary;
            Color mercenarySkin = unit != null ? unit.nativeSkintone : Color.white;
            if (mercenary && record != null && !string.IsNullOrWhiteSpace(record.sourceNationName) && Owners.Instance != null)
            {
                Nation sourceNation = Owners.Instance.nationlist.Find(candidate => candidate != null &&
                    string.Equals(candidate.name, record.sourceNationName, System.StringComparison.OrdinalIgnoreCase));
                if (sourceNation != null && sourceNation.faction != null) mercenarySkin = sourceNation.faction.color3;
            }
            string key = (faction != null ? faction.name : "side" + side) +
                (mercenary ? ":mercenary:" + ColorUtility.ToHtmlStringRGBA(mercenarySkin) : string.Empty);
            if (materialCache.TryGetValue(key, out Material cached)) return cached;
            if (baseUnitMaterial == null) return null;
            Material material = new Material(baseUnitMaterial) { name = "Tile Battle " + key };
            if (faction != null)
            {
                if (material.HasProperty("_FactionColor")) material.SetColor("_FactionColor", faction.color);
                if (material.HasProperty("_FactionColor2")) material.SetColor("_FactionColor2", faction.color2);
                if (material.HasProperty("_FactionColor3"))
                    material.SetColor("_FactionColor3", mercenary ? mercenarySkin : faction.color3);
            }
            materialCache[key] = material; return material;
        }

        private static Material FindUnitMaterial()
        {
            Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < materials.Length; i++) if (materials[i] != null && materials[i].name == "New Material 1") return materials[i];
            return null;
        }

        private static Canvas CreateCanvas()
        {
            GameObject root = new GameObject("Tile Battle UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas result = root.GetComponent<Canvas>(); result.renderMode = RenderMode.ScreenSpaceOverlay; result.sortingOrder = 950;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            return result;
        }

        private static GameObject Panel(string name, Transform parent, Vector2 minimum, Vector2 maximum, Color color)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>(); rect.anchorMin = minimum; rect.anchorMax = maximum; rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = color; return root;
        }

        private static Text Label(string name, Transform parent, int size, TextAnchor alignment)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); root.transform.SetParent(parent, false);
            Text text = root.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size;
            text.alignment = alignment; text.color = Color.white; return text;
        }

        private static Button ButtonWithText(string name, Transform parent, Vector2 min, Vector2 max, string caption)
        {
            GameObject root = Panel(name, parent, min, max, new Color(.25f, .08f, .05f, 1f)); Button button = root.AddComponent<Button>();
            Text label = Label("Label", root.transform, 18, TextAnchor.MiddleCenter); label.text = caption; Stretch(label.rectTransform); return button;
        }

        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private static Sprite GetMarkerSprite()
        {
            if (markerSprite != null) return markerSprite;
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            { float dx = x - 7.5f, dy = y - 7.5f; pixels[y * 16 + x] = dx * dx + dy * dy <= 50f ? Color.white : Color.clear; }
            texture.SetPixels(pixels); texture.Apply(); markerSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 16f); return markerSprite;
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite != null) return squareSprite;
            Texture2D texture = new Texture2D(4, 2, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[8]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels); texture.Apply(); squareSprite = Sprite.Create(texture, new Rect(0, 0, 4, 2), new Vector2(.5f, .5f), 4f); return squareSprite;
        }
    }
}
