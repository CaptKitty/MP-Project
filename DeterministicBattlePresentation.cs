using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using ProjectX.DeterministicBattle;

public sealed class CampaignBattleMarker : MonoBehaviour
{
    public CampaignActiveBattle Battle;
    public DeterministicBattlePresentation Presentation;
    private void OnMouseDown() { if (Presentation != null && Battle != null) Presentation.SelectBattle(Battle); }
}

public sealed class BattleVisualLerp : MonoBehaviour
{
    public Vector2 Target;
    public bool Initialized;
    public int EntityId = int.MinValue;
    private Vector2 velocity;
    public void Bind(int id)
    {
        if (EntityId == id) return;
        EntityId = id; Initialized = false; velocity = Vector2.zero;
    }
    private void Update()
    {
        RectTransform rect = (RectTransform)transform;
        if (!Initialized) { rect.anchoredPosition = Target; Initialized = true; }
        else rect.anchoredPosition = Vector2.SmoothDamp(rect.anchoredPosition, Target, ref velocity,
            .18f, Mathf.Infinity, Time.unscaledDeltaTime);
    }
    private void OnDisable() { Initialized = false; velocity = Vector2.zero; }
}
public sealed class FormationVisualSelection : MonoBehaviour
{
    public int FormationId; public DeterministicBattlePresentation Owner;
    public void Select() { if (Owner != null) Owner.SelectFormation(FormationId); }
}
public sealed class DroppedBattleEquipmentVisual : MonoBehaviour
{
    private Vector2 start;
    private float startAngle;
    private float startTime;
    private float direction;

    public void Begin(float horizontalDirection)
    {
        RectTransform rect = (RectTransform)transform;
        start = rect.anchoredPosition;
        startAngle = rect.localEulerAngles.z;
        startTime = Time.unscaledTime;
        direction = horizontalDirection;
    }

    private void Update()
    {
        float progress = Mathf.Clamp01((Time.unscaledTime - startTime) / .55f);
        RectTransform rect = (RectTransform)transform;
        rect.anchoredPosition = start + new Vector2(direction * 14f * progress, -22f * progress) +
            Vector2.up * Mathf.Sin(progress * Mathf.PI) * 10f;
        rect.localRotation = Quaternion.Euler(0f, 0f, startAngle + direction * 100f * progress);
    }
}
public sealed class LayeredBattleUnitVisual : MonoBehaviour
{
    public FormationStatus Status;
    public bool Attacking;
    private Image root;
    private readonly List<Image> layers = new List<Image>();
    private float phase;
    private static Sprite fallbackSprite;
    private Animator legacyAnimator;
    private Transform legacyShieldBone, legacyWeaponBone;
    private Vector3 legacyShieldBasePosition, legacyWeaponBasePosition;
    private Quaternion legacyShieldBaseRotation, legacyWeaponBaseRotation;
    private Vector3 legacyShieldBaseScale = Vector3.one, legacyWeaponBaseScale = Vector3.one;
    private string legacyConfigurationKey;
    private float weaponPresentationAngle;
    private Quaternion presentationFacing = Quaternion.identity;
    private bool hasPresentationFacing;
    private bool equipmentDropped;
    private readonly List<GameObject> droppedEquipment = new List<GameObject>();
    private bool continuousCheer;
    private float nextCheerTime;
    private float presentationFallAngle;
    private bool presentationFallen;
    public bool UsesLegacyAnimator => legacyAnimator != null && legacyAnimator.runtimeAnimatorController != null;

    public void Configure(UnitSaveData unit, Material material)
    {
        if (root == null) root = GetComponent<Image>();
        EnsureLayers(6);
        Sprite[] sprites = new Sprite[7];
        if (unit != null)
        {
            for (int i = 0; i < Mathf.Min(3, unit.bodyparts.Count); i++) sprites[i] = unit.bodyparts[i];
            // Match CritterHolder equipment construction: armor and shield replace the
            // first two art slots; they are not additional torso-centred overlays.
            if (unit.Armor != null && unit.Armor.sprite != null) sprites[0] = unit.Armor.sprite;
            if (unit.Shield != null && unit.Shield.sprite != null) sprites[1] = unit.Shield.sprite;
            Weapon weapon = unit.RangedWeapon != null ? unit.RangedWeapon : unit.MeleeWeapon;
            // CritterHolder equips its weapon into the third original art slot.
            if (weapon != null && weapon.sprite != null) sprites[2] = weapon.sprite;
        }
        root.sprite = sprites[0] != null ? sprites[0] : GetFallbackSprite();
        root.type = sprites[0] != null ? Image.Type.Sliced : Image.Type.Simple;
        root.material = material; root.color = Color.white; root.preserveAspect = true;
        for (int i = 0; i < layers.Count; i++)
        {
            layers[i].sprite = sprites[i + 1]; layers[i].type = Image.Type.Sliced; layers[i].material = material;
            layers[i].color = Color.white; layers[i].gameObject.SetActive(sprites[i + 1] != null);
        }
        // Match the original MenuUnitHolder/TestCritter slot offsets. Slot two carries
        // the shield and the final active layer carries the equipped weapon.
        SetLayerOffset(0, new Vector2(-.072f, -.216f));
        Weapon presentationWeapon = unit != null
            ? (unit.RangedWeapon != null ? unit.RangedWeapon : unit.MeleeWeapon)
            : null;
        Vector2 weaponOffset = presentationWeapon != null && presentationWeapon.OverrideBattleVisualPose
            ? presentationWeapon.BattleVisualOffset
            : new Vector2(.146f, -.082f);
        weaponPresentationAngle = presentationWeapon != null && presentationWeapon.OverrideBattleVisualPose
            ? presentationWeapon.BattleVisualAngle
            : 0f;
        SetLayerOffset(1, weaponOffset);
        if (layers.Count > 1) layers[1].rectTransform.localRotation = Quaternion.Euler(0f, 0f, weaponPresentationAngle);
        ConfigureLegacyAnimator(unit);
    }

    public void SetHorizontalFacing(bool faceLeft)
    {
        presentationFacing = Quaternion.Euler(0f, faceLeft ? 180f : 0f, 0f);
        hasPresentationFacing = true; transform.localRotation = presentationFacing;
    }

    public void SetPresentedWeapon(Weapon weapon)
    {
        if (layers.Count < 2) return;
        Image weaponLayer = layers[1];
        weaponLayer.sprite = weapon != null ? weapon.sprite : null;
        weaponLayer.gameObject.SetActive(weaponLayer.sprite != null);
        Vector2 offset = weapon != null && weapon.OverrideBattleVisualPose
            ? weapon.BattleVisualOffset : new Vector2(.146f, -.082f);
        weaponPresentationAngle = weapon != null && weapon.OverrideBattleVisualPose
            ? weapon.BattleVisualAngle : 0f;
        SetLayerOffset(1, offset);
        weaponLayer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, weaponPresentationAngle);
        string style = weapon != null ? weapon.BattleAnimationType ?? string.Empty : string.Empty;
        SetWeaponBool("Sword", style); SetWeaponBool("Spear", style); SetWeaponBool("Javelin", style);
        SetWeaponBool("Slinger", style); SetWeaponBool("Axe", style); SetWeaponBool("BasicBow", style);
    }

    public void TriggerLegacyAttack()
    {
        if (UsesLegacyAnimator && HasParameter(legacyAnimator, "Attack", AnimatorControllerParameterType.Trigger))
            legacyAnimator.SetTrigger("Attack");
    }

    public void TriggerLegacyHurt()
    {
        if (UsesLegacyAnimator && HasParameter(legacyAnimator, "Hurt", AnimatorControllerParameterType.Trigger))
            legacyAnimator.SetTrigger("Hurt");
    }

    public void TriggerLegacyCheer()
    {
        if (UsesLegacyAnimator && HasParameter(legacyAnimator, "Cheer", AnimatorControllerParameterType.Trigger))
            legacyAnimator.SetTrigger("Cheer");
    }

    public void SetContinuousCheer(bool active)
    {
        continuousCheer = active;
        if (!active) nextCheerTime = 0f;
    }

    public void SetPresentationFallen(bool fallen, float angle)
    {
        if (presentationFallen != fallen && legacyAnimator != null)
        {
            legacyAnimator.enabled = !fallen;
            if (!fallen) { legacyAnimator.Rebind(); legacyAnimator.Update(0f); }
        }
        presentationFallen = fallen;
        presentationFallAngle = angle;
    }

    public void DropEquipment(float horizontalDirection)
    {
        if (!equipmentDropped)
        {
            equipmentDropped = true;
            RectTransform unitRect = (RectTransform)transform;
            for (int i = 0; i < Mathf.Min(2, layers.Count); i++)
            {
                Image source = layers[i];
                if (source == null || source.sprite == null || !source.gameObject.activeSelf) continue;
                GameObject dropped = new GameObject(i == 0 ? "Dropped Shield" : "Dropped Weapon",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(DroppedBattleEquipmentVisual));
                dropped.transform.SetParent(transform.parent, false);
                RectTransform rect = (RectTransform)dropped.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = unitRect.sizeDelta;
                rect.anchoredPosition = unitRect.anchoredPosition;
                Image image = dropped.GetComponent<Image>();
                image.sprite = source.sprite; image.material = source.material; image.color = source.color;
                image.type = Image.Type.Sliced; image.raycastTarget = false;
                dropped.GetComponent<DroppedBattleEquipmentVisual>().Begin(horizontalDirection * (i == 0 ? .8f : 1.2f));
                droppedEquipment.Add(dropped);
            }
        }
        for (int i = 0; i < Mathf.Min(2, layers.Count); i++) layers[i].gameObject.SetActive(false);
    }

    public void RestoreEquipment()
    {
        if (!equipmentDropped) return;
        equipmentDropped = false;
        for (int i = 0; i < droppedEquipment.Count; i++) if (droppedEquipment[i] != null) Destroy(droppedEquipment[i]);
        droppedEquipment.Clear();
    }

    private void ConfigureLegacyAnimator(UnitSaveData unit)
    {
        string key = unit != null ? unit.name : string.Empty;
        if (legacyConfigurationKey == key) return;
        legacyConfigurationKey = key;
        if (legacyAnimator != null) Destroy(legacyAnimator.gameObject);
        legacyAnimator = null; legacyShieldBone = legacyWeaponBone = null;
        if (unit == null) return;
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Units/Normies/" + unit.name);
        if (prefab == null && !string.IsNullOrEmpty(unit.unitname))
            prefab = Resources.Load<GameObject>("Prefabs/Units/Normies/" + unit.unitname);
        if (prefab == null) prefab = Resources.Load<GameObject>("Prefabs/Units/Normies/" +
            (unit.Big ? "BlankCritterPrefab_Large" : "BlankCritterPrefab"));
        Animator sourceAnimator = prefab != null ? prefab.GetComponent<Animator>() : null;
        if (sourceAnimator == null || sourceAnimator.runtimeAnimatorController == null) return;

        GameObject driver = new GameObject("Legacy Animator Driver"); driver.transform.SetParent(transform, false);
        driver.hideFlags = HideFlags.HideInHierarchy;
        CopyDriverBone(prefab.transform, driver.transform, "bone_1", out Transform unused, out _, out _, out _);
        CopyDriverBone(prefab.transform, driver.transform, "bone_2", out legacyShieldBone,
            out legacyShieldBasePosition, out legacyShieldBaseRotation, out legacyShieldBaseScale);
        CopyDriverBone(prefab.transform, driver.transform, "bone_3", out legacyWeaponBone,
            out legacyWeaponBasePosition, out legacyWeaponBaseRotation, out legacyWeaponBaseScale);
        legacyAnimator = driver.AddComponent<Animator>(); legacyAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
        legacyAnimator.updateMode = AnimatorUpdateMode.UnscaledTime; legacyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        legacyAnimator.Rebind(); legacyAnimator.Update(0f);
        legacyAnimator.enabled = !presentationFallen;
        Weapon presentationWeapon = unit.RangedWeapon != null ? unit.RangedWeapon : unit.MeleeWeapon;
        string style = presentationWeapon != null ? presentationWeapon.BattleAnimationType ?? string.Empty : string.Empty;
        SetWeaponBool("Sword", style); SetWeaponBool("Spear", style); SetWeaponBool("Javelin", style);
        SetWeaponBool("Slinger", style); SetWeaponBool("Axe", style); SetWeaponBool("BasicBow", style);
        if (HasParameter(legacyAnimator, "Attack Speed", AnimatorControllerParameterType.Float)) legacyAnimator.SetFloat("Attack Speed", 1f);
    }

    private static void CopyDriverBone(Transform prefabRoot, Transform driverRoot, string name, out Transform bone,
        out Vector3 basePosition, out Quaternion baseRotation, out Vector3 baseScale)
    {
        Transform source = prefabRoot.Find(name); GameObject copy = new GameObject(name); bone = copy.transform; bone.SetParent(driverRoot, false);
        basePosition = source != null ? source.localPosition : Vector3.zero;
        baseRotation = source != null ? source.localRotation : Quaternion.identity;
        baseScale = source != null ? source.localScale : Vector3.one;
        bone.localPosition = basePosition; bone.localRotation = baseRotation; bone.localScale = baseScale;
        SpriteRenderer renderer = copy.AddComponent<SpriteRenderer>(); renderer.enabled = false;
        SpriteRenderer sourceRenderer = source != null ? source.GetComponent<SpriteRenderer>() : null;
        if (sourceRenderer != null) renderer.sprite = sourceRenderer.sprite;
    }

    private void SetWeaponBool(string parameter, string style)
    {
        if (HasParameter(legacyAnimator, parameter, AnimatorControllerParameterType.Bool))
            legacyAnimator.SetBool(parameter, style.IndexOf(parameter, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool HasParameter(Animator animator, string name, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++) if (parameters[i].name == name && parameters[i].type == type) return true;
        return false;
    }

    private static void ApplyLegacyBone(Transform bone, Vector3 basePosition, Quaternion baseRotation, Vector3 baseScale,
        Image image, float presentationAngle = 0f)
    {
        if (bone == null || image == null || !image.gameObject.activeSelf) return;
        RectTransform rect = image.rectTransform; float scale = ((RectTransform)image.transform.parent).rect.width;
        Vector3 delta = bone.localPosition - basePosition; rect.anchoredPosition = new Vector2(delta.x, delta.y) * scale;
        rect.localRotation = Quaternion.Euler(0f, 0f, presentationAngle) * bone.localRotation * Quaternion.Inverse(baseRotation);
        rect.localScale = new Vector3(baseScale.x != 0f ? bone.localScale.x / baseScale.x : 1f,
            baseScale.y != 0f ? bone.localScale.y / baseScale.y : 1f, 1f);
    }

    private void SetLayerOffset(int index, Vector2 relativeOffset)
    {
        if (index < 0 || index >= layers.Count) return;
        RectTransform rect = layers[index].rectTransform;
        rect.anchorMin = relativeOffset;
        rect.anchorMax = Vector2.one + relativeOffset;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null) return fallbackSprite;
        Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        texture.name = "Fallback unit silhouette"; texture.filterMode = FilterMode.Point;
        Color[] pixels = new Color[24 * 24];
        for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++)
        {
            float dx = x - 11.5f, dy = y - 16.5f;
            bool head = dx * dx + dy * dy < 15f;
            bool body = y >= 3 && y <= 14 && Mathf.Abs(dx) < 5f - Mathf.Abs(y - 8f) * .12f;
            pixels[y * 24 + x] = head || body ? Color.white : Color.clear;
        }
        texture.SetPixels(pixels); texture.Apply();
        fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 24, 24), new Vector2(.5f, .5f), 24f);
        return fallbackSprite;
    }

    private void EnsureLayers(int totalLayers)
    {
        while (layers.Count < totalLayers - 1)
        {
            GameObject child = new GameObject("Unit Art Layer " + (layers.Count + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(transform, false); RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = child.GetComponent<Image>(); image.raycastTarget = false; layers.Add(image);
        }
    }

    private void LateUpdate()
    {
        if (continuousCheer && Time.unscaledTime >= nextCheerTime)
        {
            TriggerLegacyCheer();
            nextCheerTime = Time.unscaledTime + .85f;
        }
        phase += Time.unscaledDeltaTime * (Status == FormationStatus.Charging ? 10f : 5f);
        float bob = !presentationFallen && (Status == FormationStatus.Advancing || Status == FormationStatus.Charging)
            ? Mathf.Sin(phase) * 0.025f : 0f;
        if (!presentationFallen && continuousCheer && !UsesLegacyAnimator) bob += Mathf.Abs(Mathf.Sin(phase * 1.4f)) * .08f;
        float lunge = !presentationFallen && Attacking && !UsesLegacyAnimator
            ? (0.025f + Mathf.Abs(Mathf.Sin(phase * 1.5f)) * 0.045f) : 0f;
        float routeTilt = !presentationFallen && Status == FormationStatus.Routing ? Mathf.Sin(phase * 0.6f) * 12f : 0f;
        float totalTilt = routeTilt + presentationFallAngle;
        if (hasPresentationFacing) transform.localRotation = presentationFacing * Quaternion.Euler(0f, 0f, totalTilt);
        else transform.localRotation = Quaternion.Euler(0f, 0f, totalTilt);
        transform.localScale = new Vector3(1f + lunge, 1f + bob, 1f);
        if (UsesLegacyAnimator)
        {
            if (layers.Count > 0) ApplyLegacyBone(legacyShieldBone, legacyShieldBasePosition, legacyShieldBaseRotation, legacyShieldBaseScale, layers[0]);
            if (layers.Count > 1) ApplyLegacyBone(legacyWeaponBone, legacyWeaponBasePosition, legacyWeaponBaseRotation,
                legacyWeaponBaseScale, layers[1], weaponPresentationAngle);
        }
    }
}

public sealed class DeterministicBattlePresentation : MonoBehaviour
{
    private readonly Dictionary<CampaignActiveBattle, CampaignBattleMarker> markers = new Dictionary<CampaignActiveBattle, CampaignBattleMarker>();
    private DeterministicBattleManager manager;
    private CampaignActiveBattle selected;
    private Canvas canvas;
    private GameObject inspectionRoot, viewerRoot;
    private GameObject battleAccessRoot;
    private Text battleAccessLabel;
    private Text inspectionText, viewerHeader;
    private RectTransform viewerField;
    private RectTransform viewerContent;
    private readonly List<Image> formationPool = new List<Image>();
    private readonly List<Image> combatantPool = new List<Image>();
    private readonly List<Image> projectilePool = new List<Image>();
    private readonly List<Image> terrainPool = new List<Image>();
    private readonly Dictionary<string, Material> factionMaterialCache = new Dictionary<string, Material>();
    private float nextRefresh;
    private int selectedFormationId = -1;
    private readonly List<BattleSnapshot> history = new List<BattleSnapshot>();
    private int historyOffset;
    private bool presentationPaused;
    private float viewerZoom = 3f;
    private Vector2 viewerPan;
    private static Sprite markerSprite;
    private static Sprite formationRingSprite;
    private static Sprite projectileFallbackSprite;
    private static readonly Dictionary<BattleTerrainKind, Sprite> terrainSprites = new Dictionary<BattleTerrainKind, Sprite>();
    private Material unitMaterial;

    public void Initialize(DeterministicBattleManager owner)
    {
        manager = owner;
        canvas = CreateCanvas();
        CreateInspectionPanel();
        CreateViewer();
        CreateBattleAccessButton();
        unitMaterial = FindUnitMaterial();
    }

    private void Update()
    {
        DetectMarkerClick();
        if (viewerRoot != null && viewerRoot.activeSelf)
        {
            float pan = 220f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) viewerPan.x += pan;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) viewerPan.x -= pan;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) viewerPan.y -= pan;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) viewerPan.y += pan;
        }
        if (manager == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        SynchronizeMarkers();
        RefreshBattleAccessButton();
        if (selected != null && manager.ActiveBattles.Contains(selected))
        {
            RefreshInspection();
            if (viewerRoot.activeSelf)
            {
                BattleSnapshot live = selected.Simulation.CreateSnapshot();
                if (history.Count == 0 || history[history.Count - 1].Tick != live.Tick) { history.Add(live); if (history.Count > 120) history.RemoveAt(0); }
                if (!presentationPaused) historyOffset = 0;
                if (history.Count > 0) RefreshViewer(history[Mathf.Clamp(history.Count - 1 - historyOffset, 0, history.Count - 1)]);
            }
        }
        else if (selected != null) CloseInspection();
    }

    private void DetectMarkerClick()
    {
        if (!Input.GetMouseButtonDown(0) || Camera.main == null || markers.Count == 0) return;
        CampaignBattleMarker closest = null; float closestPixels = 14f;
        foreach (CampaignBattleMarker marker in markers.Values)
        {
            if (marker == null) continue;
            Vector3 screen = Camera.main.WorldToScreenPoint(marker.transform.position);
            if (screen.z < 0f) continue;
            float distance = Vector2.Distance(Input.mousePosition, screen);
            if (distance < closestPixels) { closest = marker; closestPixels = distance; }
        }
        if (closest != null) SelectBattle(closest.Battle);
    }

    private void SynchronizeMarkers()
    {
        List<CampaignActiveBattle> removed = new List<CampaignActiveBattle>();
        foreach (KeyValuePair<CampaignActiveBattle, CampaignBattleMarker> pair in markers)
            if (!manager.ActiveBattles.Contains(pair.Key)) removed.Add(pair.Key);
        for (int i = 0; i < removed.Count; i++)
        {
            if (markers[removed[i]] != null) Destroy(markers[removed[i]].gameObject);
            markers.Remove(removed[i]);
        }
        for (int i = 0; i < manager.ActiveBattles.Count; i++)
        {
            CampaignActiveBattle battle = manager.ActiveBattles[i];
            if (!markers.TryGetValue(battle, out CampaignBattleMarker marker)) marker = CreateMarker(battle);
            RemoveMarkerText(marker);
            if (battle.ArmyA != null && battle.ArmyB != null)
            {
                Vector3 position = (battle.ArmyA.transform.position + battle.ArmyB.transform.position) * 0.5f;
                marker.transform.position = new Vector3(position.x, position.y, -2f);
            }
            else if (battle.ArmyA != null)
                marker.transform.position = new Vector3(battle.ArmyA.transform.position.x, battle.ArmyA.transform.position.y, -2f);
        }
    }

    private CampaignBattleMarker CreateMarker(CampaignActiveBattle battle)
    {
        GameObject root = new GameObject("Battle Marker " + battle.StartState.BattleId, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(CampaignBattleMarker));
        root.transform.SetParent(transform, true);
        root.transform.localScale = Vector3.one * 0.02f;
        CircleCollider2D markerCollider = root.GetComponent<CircleCollider2D>();
        markerCollider.radius = 0.5f;
        markerCollider.isTrigger = true;
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        renderer.sprite = GetMarkerSprite(); renderer.color = new Color(0.85f, 0.16f, 0.08f, 0.95f);
        renderer.sortingOrder = 100;
        CampaignBattleMarker marker = root.GetComponent<CampaignBattleMarker>();
        marker.Battle = battle; marker.Presentation = this;
        RemoveMarkerText(marker);
        markers.Add(battle, marker);
        return marker;
    }

    private static void RemoveMarkerText(CampaignBattleMarker marker)
    {
        if (marker == null) return;
        Component[] components = marker.GetComponentsInChildren<Component>(true);
        HashSet<GameObject> childrenToRemove = new HashSet<GameObject>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null) continue;
            bool isText = component is Text || component is TextMesh ||
                component.GetType().Name.IndexOf("TextMeshPro", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isText) continue;
            if (component.gameObject != marker.gameObject) childrenToRemove.Add(component.gameObject);
            else Destroy(component);
        }
        foreach (GameObject child in childrenToRemove) if (child != null) Destroy(child);
    }

    private void CreateBattleAccessButton()
    {
        battleAccessRoot = Panel("Active Battles", canvas.transform, new Vector2(0.78f, 0.88f), new Vector2(0.98f, 0.97f), new Color(0.5f, 0.08f, 0.04f, 0.95f));
        Button button = battleAccessRoot.AddComponent<Button>(); button.onClick.AddListener(OpenRelevantBattle);
        battleAccessLabel = Label("Label", battleAccessRoot.transform, 10, TextAnchor.MiddleCenter);
        battleAccessLabel.resizeTextForBestFit = true;
        battleAccessLabel.resizeTextMinSize = 7;
        battleAccessLabel.resizeTextMaxSize = 10;
        battleAccessLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        battleAccessLabel.verticalOverflow = VerticalWrapMode.Truncate;
        battleAccessLabel.rectTransform.anchorMin = Vector2.zero; battleAccessLabel.rectTransform.anchorMax = Vector2.one;
        battleAccessLabel.rectTransform.offsetMin = battleAccessLabel.rectTransform.offsetMax = Vector2.zero;
        battleAccessRoot.SetActive(false);
    }

    private void RefreshBattleAccessButton()
    {
        bool active = manager != null && manager.ActiveBattles.Count > 0;
        if (battleAccessRoot != null) battleAccessRoot.SetActive(active);
        if (active) battleAccessLabel.text = "BATTLES: " + manager.ActiveBattles.Count + "\nClick to view";
    }

    private void OpenRelevantBattle()
    {
        if (manager == null || manager.ActiveBattles.Count == 0) return;
        CampaignActiveBattle battle = manager.ActiveBattles.Find(item =>
            item.ArmyA != null && item.ArmyA.IsFriendlyToLocalPlayer() || item.ArmyB != null && item.ArmyB.IsFriendlyToLocalPlayer());
        SelectBattle(battle ?? manager.ActiveBattles[0]);
        OpenViewer();
    }

    public void SelectBattle(CampaignActiveBattle battle)
    {
        selected = battle;
        inspectionRoot.SetActive(true);
        RefreshInspection();
    }
    public void SelectFormation(int formationId) { selectedFormationId = formationId; RefreshInspection(); }

    private void RefreshInspection()
    {
        ActiveBattleSummary summary = selected.GetSummary();
        StringBuilder text = new StringBuilder(1024);
        text.AppendLine(summary.ArmyA + "  vs  " + summary.ArmyB);
        text.AppendLine("Tick " + summary.Tick + "   " + summary.Phase + "   Hash " + summary.StateHash);
        text.AppendLine("Province: " + summary.EncounterProvince + "   Battlefield: " + summary.TerrainArchetype +
            "   Terrain areas: " + summary.TerrainAreas);
        text.AppendLine("Casualties: " + summary.SideACasualties + " / " + summary.SideBCasualties +
            "   Advantage: " + summary.Advantage + "   Reinforcements: " + summary.Reinforcements);
        text.AppendLine("Morale " + summary.AverageMorale + "   Cohesion " + summary.AverageCohesion +
            "   Routing " + summary.RoutingFormations + "   Effects " + summary.ActiveEffects);
        text.AppendLine("Ranged: " + summary.ProjectilesLaunched + " thrown, " + summary.ProjectileHits +
            " hits, " + summary.ActiveProjectiles + " airborne, " + summary.RemainingAmmunition + " ammo left");
        if (summary.GeneralDecisions != null) for (int i = 0; i < summary.GeneralDecisions.Count; i++) text.AppendLine(summary.GeneralDecisions[i]);
        text.AppendLine();
        for (int i = 0; i < summary.FormationDetails.Count; i++)
        {
            FormationBattleSummary f = summary.FormationDetails[i];
            text.Append('#').Append(f.FormationId).Append(" S").Append(f.Side).Append(' ').Append(f.UnitName)
                .Append("  ").Append(f.Order).Append('/').Append(f.Status).Append("  M").Append(f.Morale)
                .Append(" C").Append(f.Cohesion).Append("  ").Append(f.Living).Append(" alive  ")
                .Append(f.Terrain).AppendLine();
        }
        inspectionText.text = text.ToString();
    }

    public void OpenViewer() { if (selected != null) { history.Clear(); historyOffset = 0; viewerRoot.SetActive(true); RefreshViewer(selected.Simulation.CreateSnapshot()); } }
    public void CloseViewer() { viewerRoot.SetActive(false); DeactivatePool(formationPool); DeactivatePool(combatantPool); DeactivatePool(projectilePool); DeactivatePool(terrainPool); }
    public void CloseInspection() { selected = null; inspectionRoot.SetActive(false); CloseViewer(); }

    private void RefreshViewer(BattleSnapshot snapshot)
    {
        viewerHeader.text = snapshot.BattleId + "   tick " + snapshot.Tick + "   hash " + snapshot.StateHash + (presentationPaused ? " [VIEW PAUSED]" : "");
        viewerContent.localScale = Vector3.one * viewerZoom; viewerContent.anchoredPosition = viewerPan;
        DeactivatePool(formationPool); DeactivatePool(combatantPool); DeactivatePool(projectilePool); DeactivatePool(terrainPool);
        for (int i = 0; i < snapshot.Terrain.Count; i++)
        {
            TerrainSnapshot t = snapshot.Terrain[i]; Image image = Acquire(terrainPool, "Terrain");
            image.sprite = GetTerrainSprite(t.Kind); image.type = Image.Type.Simple;
            image.color = TerrainColor(t.Kind); image.preserveAspect = true;
            SetBattlePosition(image.rectTransform, t.Center, Mathf.Max(18f, t.RadiusMilli * 0.018f), t.Id);
        }
        for (int i = 0; i < snapshot.Combatants.Count; i++)
        {
            CombatantSnapshot c = snapshot.Combatants[i]; if (!c.Alive) continue;
            Image image = Acquire(combatantPool, "Combatant");
            UnitSaveData unit = FindUnitData(c.DefinitionId);
            LayeredBattleUnitVisual visual = image.GetComponent<LayeredBattleUnitVisual>();
            if (visual == null) visual = image.gameObject.AddComponent<LayeredBattleUnitVisual>();
            FormationSnapshot owner = snapshot.Formations.Find(item => item.Id == c.FormationId);
            int side = owner != null ? owner.Side : 0;
            visual.Configure(unit, GetFactionMaterial(side, unit));
            Int2 visualPosition = c.Position;
            if (owner != null)
            {
                Int2 offset = c.Position - owner.Position;
                // Presentation favours legibility over exact member coordinates.
                visualPosition = owner.Position + new Int2(offset.X * 7 / 4, offset.Y * 7 / 4);
            }
            SetBattlePosition(image.rectTransform, visualPosition, unit != null && unit.Big ? 22f : 14f, c.Id);
            if (owner != null)
            {
                visual.Status = owner.Status; visual.Attacking = c.NextAttackTick > snapshot.Tick;
            }
        }
        for (int i = 0; i < snapshot.Formations.Count; i++)
        {
            FormationSnapshot f = snapshot.Formations[i]; Image image = Acquire(formationPool, "Formation");
            image.sprite = GetFormationRingSprite(); image.type = Image.Type.Simple; image.preserveAspect = true;
            // Keep an invisible hit target for selection without drawing a circle
            // through the centre of every formation.
            image.color = Color.clear;
            SetBattlePosition(image.rectTransform, f.Position, 28f, f.Id);
            image.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(f.Facing.Y, f.Facing.X) * Mathf.Rad2Deg - 90f);
            FormationVisualSelection select = image.GetComponent<FormationVisualSelection>();
            if (select == null) { select = image.gameObject.AddComponent<FormationVisualSelection>(); Button button = image.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None; button.onClick.AddListener(select.Select); }
            select.FormationId = f.Id; select.Owner = this; image.raycastTarget = true; image.transform.localScale = f.Id == selectedFormationId ? Vector3.one * 1.5f : Vector3.one;
        }
        for (int i = 0; i < snapshot.Projectiles.Count; i++)
        {
            ProjectileSnapshot p = snapshot.Projectiles[i]; if (!p.Active) continue;
            Image image = Acquire(projectilePool, "Projectile");
            image.sprite = FindProjectileSprite(snapshot, p) ?? GetProjectileFallbackSprite();
            image.type = Image.Type.Simple; image.preserveAspect = true; image.color = Color.white;
            SetBattlePosition(image.rectTransform, p.Position, 9f, p.Id);
        }
    }

    private Color FormationColor(BattleSnapshot snapshot, int formationId)
    {
        FormationSnapshot formation = snapshot.Formations.Find(item => item.Id == formationId);
        return formation != null && formation.Side == 0 ? new Color(0.25f, 0.8f, 1f, 0.8f) : new Color(1f, 0.35f, 0.2f, 0.8f);
    }

    private Image Acquire(List<Image> pool, string objectName)
    {
        for (int i = 0; i < pool.Count; i++) if (!pool[i].gameObject.activeSelf) { pool[i].gameObject.SetActive(true); return pool[i]; }
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(viewerContent, false); Image image = child.GetComponent<Image>(); image.raycastTarget = false;
        pool.Add(image); return image;
    }

    private void SetBattlePosition(RectTransform rect, Int2 position, float size, int entityId)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        BattleVisualLerp interpolation = rect.GetComponent<BattleVisualLerp>();
        if (interpolation == null) interpolation = rect.gameObject.AddComponent<BattleVisualLerp>();
        interpolation.Bind(entityId);
        interpolation.Target = new Vector2(position.X / 100f, position.Y / 100f);
        rect.sizeDelta = Vector2.one * size;
    }

    private static void DeactivatePool(List<Image> pool) { for (int i = 0; i < pool.Count; i++) pool[i].gameObject.SetActive(false); }

    private void CreateInspectionPanel()
    {
        inspectionRoot = Panel("BattleInspection", canvas.transform, new Vector2(0f, 0.05f), new Vector2(0.42f, 0.95f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
        inspectionText = Label("Battle Details", inspectionRoot.transform, 14, TextAnchor.UpperLeft);
        RectTransform textRect = inspectionText.rectTransform; textRect.anchorMin = new Vector2(0.03f, 0.12f); textRect.anchorMax = new Vector2(0.97f, 0.97f); textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        Button("View Battle", inspectionRoot.transform, new Vector2(0.04f, 0.02f), new Vector2(0.31f, 0.1f), OpenViewer);
        Button("Hold", inspectionRoot.transform, new Vector2(0.33f, 0.02f), new Vector2(0.49f, 0.1f), () => SchedulePlayerOrder(FormationOrder.Hold));
        Button("Advance", inspectionRoot.transform, new Vector2(0.51f, 0.02f), new Vector2(0.68f, 0.1f), () => SchedulePlayerOrder(FormationOrder.Advance));
        Button("Withdraw", inspectionRoot.transform, new Vector2(0.7f, 0.02f), new Vector2(0.84f, 0.1f), () => SchedulePlayerOrder(FormationOrder.Withdraw));
        Button("X", inspectionRoot.transform, new Vector2(0.86f, 0.02f), new Vector2(0.96f, 0.1f), CloseInspection);
        inspectionRoot.SetActive(false);
    }

    private void SchedulePlayerOrder(FormationOrder order)
    {
        if (selected == null || manager == null) return;
        FieldArmyHolder army = selected.ArmyA != null && (selected.ArmyA.IsPlayer || selected.ArmyA.IsHumanControlled)
            ? selected.ArmyA : selected.ArmyB;
        if (army == null) return;
        int side = army == selected.ArmyA ? 0 : 1;
        SimFormation formation = selected.Simulation.Formations.Find(item => item.Id == selectedFormationId && item.Side == side && item.Status != FormationStatus.Destroyed);
        if (formation != null) manager.SchedulePlayerOrder(army, formation.Id, order);
    }

    private void CreateViewer()
    {
        viewerRoot = Panel("BattleViewer", canvas.transform, new Vector2(0.18f, 0.12f), new Vector2(0.95f, 0.92f), new Color(0.025f, 0.03f, 0.035f, 0.98f));
        viewerHeader = Label("Battle Viewer", viewerRoot.transform, 16, TextAnchor.MiddleLeft);
        viewerHeader.rectTransform.anchorMin = new Vector2(0.02f, 0.92f); viewerHeader.rectTransform.anchorMax = new Vector2(0.8f, 0.99f); viewerHeader.rectTransform.offsetMin = viewerHeader.rectTransform.offsetMax = Vector2.zero;
        GameObject field = Panel("Field", viewerRoot.transform, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.91f), new Color(0.435f, 0.608f, 0.333f, 1f));
        viewerField = field.GetComponent<RectTransform>();
        GameObject content = new GameObject("Battle Content", typeof(RectTransform)); content.transform.SetParent(viewerField, false); viewerContent = content.GetComponent<RectTransform>();
        viewerContent.anchorMin = viewerContent.anchorMax = new Vector2(.5f,.5f); viewerContent.sizeDelta = new Vector2(800,600);
        Button("Pause", viewerRoot.transform, new Vector2(0.58f, 0.93f), new Vector2(0.68f, 0.99f), () => presentationPaused = !presentationPaused);
        Button("<", viewerRoot.transform, new Vector2(0.69f, 0.93f), new Vector2(0.73f, 0.99f), () => { presentationPaused = true; historyOffset = Mathf.Min(history.Count - 1, historyOffset + 1); });
        Button(">", viewerRoot.transform, new Vector2(0.74f, 0.93f), new Vector2(0.78f, 0.99f), () => historyOffset = Mathf.Max(0, historyOffset - 1));
        Button("+", viewerRoot.transform, new Vector2(0.79f, 0.93f), new Vector2(0.83f, 0.99f), () => viewerZoom = Mathf.Min(6f, viewerZoom + .25f));
        Button("-", viewerRoot.transform, new Vector2(0.84f, 0.93f), new Vector2(0.88f, 0.99f), () => viewerZoom = Mathf.Max(.4f, viewerZoom - .2f));
        Button("Center", viewerRoot.transform, new Vector2(0.89f, 0.93f), new Vector2(0.95f, 0.99f), CenterViewer);
        Button("X", viewerRoot.transform, new Vector2(0.96f, 0.93f), new Vector2(0.99f, 0.99f), CloseViewer);
        viewerRoot.SetActive(false);
    }

    private void CenterViewer()
    {
        viewerPan = Vector2.zero;
        if (selected == null) return;
        SimFormation f = selected.Simulation.Formations.Find(item => item.Id == selectedFormationId);
        if (f != null) viewerPan = new Vector2(-f.Position.X / 100f, -f.Position.Y / 100f) * viewerZoom;
    }

    private UnitSaveData FindUnitData(int definitionId)
    {
        if (selected == null) return null;
        BattleUnitDefinition definition = selected.StartState.Definitions.Find(item => item.DefinitionId == definitionId);
        if (definition == null) return null;
        FieldArmyHolder[] armies = { selected.ArmyA, selected.ArmyB };
        for (int a = 0; a < armies.Length; a++) if (armies[a] != null)
            for (int i = 0; i < armies[a].fieldArmy.USDReserves.Count; i++)
                if (armies[a].fieldArmy.USDReserves[i].USD != null && armies[a].fieldArmy.USDReserves[i].USD.name == definition.UnitName)
                    return armies[a].fieldArmy.USDReserves[i].USD;
        if (selected.GarrisonArmy != null)
            for (int i = 0; i < selected.GarrisonArmy.USDReserves.Count; i++)
                if (selected.GarrisonArmy.USDReserves[i].USD != null && selected.GarrisonArmy.USDReserves[i].USD.name == definition.UnitName)
                    return selected.GarrisonArmy.USDReserves[i].USD;
        return null;
    }

    private Faction GetFaction(int side)
    {
        if (selected == null) return null;
        FieldArmyHolder army = side == 0 ? selected.ArmyA : selected.ArmyB;
        if (army != null && army.fieldArmy != null && army.fieldArmy.nation != null) return army.fieldArmy.nation.faction;
        return side == 1 && selected.GarrisonArmy != null && selected.GarrisonArmy.nation != null
            ? selected.GarrisonArmy.nation.faction : null;
    }

    private Color GetFactionColor(int side)
    {
        Faction faction = GetFaction(side);
        if (faction != null)
        {
            Color color = faction.color;
            color.a = 1f;
            return color;
        }
        return side == 0 ? new Color(.15f, .8f, 1f) : new Color(1f, .3f, .18f);
    }

    private Material GetFactionMaterial(int side, UnitSaveData unit)
    {
        Faction faction = GetFaction(side);
        Material baseMaterial = unitMaterial != null ? unitMaterial : FindArmyUnitMaterial(side);
        if (baseMaterial == null || faction == null) return baseMaterial;
        Color third = unit != null && unit.Mercenary ? unit.nativeSkintone : faction.color3;
        string key = faction.GetInstanceID() + ":" + (unit != null && unit.Mercenary ? ColorUtility.ToHtmlStringRGBA(third) : "faction");
        Material material;
        if (factionMaterialCache.TryGetValue(key, out material) && material != null) return material;
        material = new Material(baseMaterial); material.name = baseMaterial.name + " - " + faction.name;
        if (material.HasProperty("_FactionColor")) material.SetColor("_FactionColor", faction.color);
        if (material.HasProperty("_FactionColor2")) material.SetColor("_FactionColor2", faction.color2);
        if (material.HasProperty("_FactionColor3")) material.SetColor("_FactionColor3", third);
        factionMaterialCache[key] = material;
        return material;
    }

    private Material FindArmyUnitMaterial(int side)
    {
        if (selected == null) return null;
        FieldArmyHolder army = side == 0 ? selected.ArmyA : selected.ArmyB;
        if (army == null) return null;
        SpriteRenderer[] renderers = army.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material candidate = renderers[i].sharedMaterial;
            if (candidate != null && candidate.HasProperty("_FactionColor")) return candidate;
        }
        return null;
    }

    private Sprite FindProjectileSprite(BattleSnapshot snapshot, ProjectileSnapshot projectile)
    {
        CombatantSnapshot source = snapshot.Combatants.Find(item => item.Id == projectile.SourceCombatantId);
        UnitSaveData unit = source != null ? FindUnitData(source.DefinitionId) : null;
        Weapon weapon = unit != null ? (unit.RangedWeapon != null ? unit.RangedWeapon : unit.MeleeWeapon) : null;
        if (weapon == null) return null;
        if (weapon.BattleProjectileSprite != null) return weapon.BattleProjectileSprite;
        if (weapon.Throwable != null)
        {
            SpriteRenderer renderer = weapon.Throwable.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null) return renderer.sprite;
        }
        return null;
    }

    private static Sprite GetFormationRingSprite()
    {
        if (formationRingSprite == null)
            formationRingSprite = CreateRuntimeSprite("Formation selection ring", (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                return d > 12f && d < 15f ? Color.white : Color.clear;
            });
        return formationRingSprite;
    }

    private static Sprite GetProjectileFallbackSprite()
    {
        if (projectileFallbackSprite == null)
            projectileFallbackSprite = CreateRuntimeSprite("Projectile arrow", (x, y) =>
            {
                bool shaft = Mathf.Abs(y - 15.5f) < 1.6f && x > 4 && x < 27;
                bool head = x >= 22 && Mathf.Abs(y - 15.5f) <= (29 - x) * .65f;
                bool feather = x < 9 && Mathf.Abs(y - 15.5f) <= (9 - x) * .55f;
                return shaft || head || feather ? Color.white : Color.clear;
            });
        return projectileFallbackSprite;
    }

    private static Sprite GetTerrainSprite(BattleTerrainKind kind)
    {
        Sprite sprite;
        if (terrainSprites.TryGetValue(kind, out sprite)) return sprite;
        sprite = CreateRuntimeSprite("Terrain " + kind, (x, y) =>
        {
            float nx = x - 15.5f, ny = y - 15.5f;
            bool inside = nx * nx + ny * ny < 225f;
            if (!inside) return Color.clear;
            switch (kind)
            {
                case BattleTerrainKind.Forest:
                    return ((x + y * 3) % 11 < 4 || (x * 3 + y) % 13 < 4) ? Color.white : new Color(1, 1, 1, .18f);
                case BattleTerrainKind.Hill:
                    return Mathf.Abs(ny + 7f - Mathf.Abs(nx) * .55f) < 2f || Mathf.Abs(ny - 2f - Mathf.Abs(nx) * .35f) < 1.5f ? Color.white : new Color(1, 1, 1, .16f);
                case BattleTerrainKind.River:
                    return Mathf.Abs(nx - Mathf.Sin(y * .35f) * 4f) < 4f ? Color.white : Color.clear;
                case BattleTerrainKind.Road:
                    return Mathf.Abs(ny - nx * .2f) < 3f ? Color.white : Color.clear;
                case BattleTerrainKind.Impassable:
                    return Mathf.Abs(ny + 8f - Mathf.Abs(nx) * .65f) < 3f || ny < -8f + Mathf.Abs(nx) * .65f ? Color.white : Color.clear;
                default:
                    return ((x + y) % 8 < 2) ? new Color(1, 1, 1, .55f) : new Color(1, 1, 1, .1f);
            }
        });
        terrainSprites[kind] = sprite;
        return sprite;
    }

    private static Sprite CreateRuntimeSprite(string name, System.Func<int, int, Color> pixel)
    {
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.name = name; texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) colors[y * 32 + x] = pixel(x, y);
        texture.SetPixels(colors); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32f);
    }

    private static Material FindUnitMaterial()
    {
        Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
        for (int i = 0; i < materials.Length; i++) if (materials[i] != null && materials[i].name == "New Material 1") return materials[i];
        return null;
    }

    private static GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = color; return root;
    }

    private static Text Label(string name, Transform parent, int size, TextAnchor alignment)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); root.transform.SetParent(parent, false);
        Text text = root.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.alignment = alignment; text.color = Color.white;
        return text;
    }

    private static Button Button(string caption, Transform parent, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        GameObject root = Panel(caption, parent, min, max, new Color(0.2f, 0.25f, 0.32f, 1f)); Button button = root.AddComponent<Button>(); button.onClick.AddListener(action);
        Text label = Label("Label", root.transform, 14, TextAnchor.MiddleCenter); label.text = caption; label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static Canvas FindOverlayCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++) if (canvases[i].isRootCanvas && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay) return canvases[i];
        return canvases.Length > 0 ? canvases[0] : null;
    }

    private static Canvas CreateCanvas()
    {
        GameObject root = new GameObject("Deterministic Battle Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true; canvas.sortingOrder = 2000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static Sprite GetMarkerSprite()
    {
        if (markerSprite != null) return markerSprite;
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); texture.name = "Runtime Battle Marker";
        Color[] pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) pixels[y * 32 + x] = (x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f) < 220f ? Color.white : Color.clear;
        texture.SetPixels(pixels); texture.Apply(); markerSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f); return markerSprite;
    }

    private static Color TerrainColor(BattleTerrainKind kind)
    {
        switch (kind)
        {
            case BattleTerrainKind.Hill: return new Color(0.5f, 0.4f, 0.22f, 0.45f);
            case BattleTerrainKind.Forest: return new Color(0.05f, 0.3f, 0.08f, 0.55f);
            case BattleTerrainKind.River: return new Color(0.1f, 0.4f, 0.8f, 0.55f);
            case BattleTerrainKind.Road: return new Color(0.55f, 0.45f, 0.3f, 0.4f);
            case BattleTerrainKind.Impassable: return new Color(0.1f, 0.1f, 0.1f, 0.75f);
            default: return new Color(0.35f, 0.32f, 0.2f, 0.35f);
        }
    }
}
