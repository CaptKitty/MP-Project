using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Diagnostics;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

public class Mapshower : MonoBehaviour
{
    private enum CampaignMapMode { Ownership, Supply, Cultures, Regions }
    [Min(0.01f)] public float CampaignTimeScale = 0.25f;
    public string regionname;
    public int regionnumber;
    public string owner;
    public string culture1;
    public int culture1pop;
    public string culture2;
    public int culture2pop;
    public string culture3;
    public int culture3pop;

    public Nation SelectedNation;
    public Province SelectedProvince;


    public int width;
    public int height;

    public Camera OverheadCamera;

    Color32[] remapArr;
    Color32[] paletteArr;
    Color32[] ownerArr;
    Texture2D paletteTex;
    Texture2D ownerTex;

    Color32 prevColor;
    Color32 prevColorA;
    bool selectAny = false;
    bool selectALL = false;
    private Province highlightedProvince;
    private CampaignMapMode currentMapMode = CampaignMapMode.Ownership;
    public bool potato = true;
    public GameObject banana;
    public static Mapshower Instance;

    [Header("Campaign speed UI")]
    [SerializeField] private Transform speedSettings;
    [SerializeField] private Color activeSpeedButtonColor = new Color(1f, 0.72f, 0.28f, 1f);
    private readonly List<Button> campaignSpeedButtons = new List<Button>();
    private readonly List<ColorBlock> campaignSpeedButtonColors = new List<ColorBlock>();
    private static readonly float[] CampaignSpeedOptions = { 0f, .25f, 1f, 2f, 5f };
    private static readonly string[] CampaignSpeedButtonNames =
        { "0xSpeed", "0.25xSpeed", "1xSpeed", "2xSpeed", "5xSpeed" };
    private float displayedCampaignSpeed = float.NaN;
    [Header("Campaign pause UI")]
    [SerializeField] private GameObject pauseCanvas;
    [SerializeField] private GameObject pauseSign;
    [SerializeField] private Text pauseBlameText;
    private string pauseRequestedBy = string.Empty;

    private Vector3 StartDragPosition;
    private Vector3 dragPressPosition;
    private bool mapPressStartedOutsideUI;
    private bool mapDragExceededClickThreshold;
    private bool suppressMapClickUntilMouseRelease;
    private const float MapClickDragThresholdPixels = 6f;

    void Awake()
    {
        Time.timeScale = 1f;
        Instance = this;
        
        var material = GetComponent<Renderer>().material;
        
        return;

        WWW wwwss = new WWW(Application.streamingAssetsPath + "/Basemap.png");
        if(wwwss != null)
        {
            // Texture2D 
            Texture2D texTmp = material.GetTexture("_MainTex") as Texture2D;// = new Texture2D(728, 456);//, TextureFormat.DXT5, false);
            //texTmpss = material.GetTexture("_MainTex") as Texture2D;
            //LoadImageIntoTexture compresses JPGs by DXT1 and PNGs by DXT5     
            wwwss.LoadImageIntoTexture(texTmp);
            //texTmpss = material.GetTexture("_MainTex") as Texture2D;
            texTmp.filterMode = FilterMode.Point;
            material.SetTexture("_MainTex", texTmp);

            width = texTmp.width;//1460;//mainTex.width;//729;
            height = texTmp.height;
        }

        WWW www = new WWW(Application.streamingAssetsPath + "/TerrainMap.png");
        if(www != null)
        {
            Texture2D texTmp = new Texture2D(width, height, TextureFormat.DXT5, false);
            //LoadImageIntoTexture compresses JPGs by DXT1 and PNGs by DXT5     
            www.LoadImageIntoTexture(texTmp);
            
            material.SetTexture("_TerrainTex", texTmp);
        }
        WWW wwws = new WWW(Application.streamingAssetsPath + "/Basemap_RiversAndCities.png");
        if(wwws != null)
        {
            Texture2D texTmp = new Texture2D(width, height, TextureFormat.DXT5, false);
            //LoadImageIntoTexture compresses JPGs by DXT1 and PNGs by DXT5     
            wwws.LoadImageIntoTexture(texTmp);
            material.SetTexture("_RiverTex", texTmp);
        }
        transform.localScale = new Vector3(width, height, 1);



        // GetComponent<Renderer>().

        
        // var TerrainTex = material.GetTexture("_TerrainTex") as Texture2D;

        // TerrainTex = texTmp;

        // UnityEngine.Debug.LogError("Potato2");

        //this.gameObject.SetActive(false);
    }
    void OnEnable()
    {
        Paint();
        //RePaint();
    }
    // Start is called before the first frame update
    void Start()
    {
        if (Owners.Instance != null) Owners.Instance.CampaignSimulationSpeed = CampaignTimeScale;
        BindCampaignSpeedButtons();
        BindPauseUI();
        ApplyPausePresentation(false, string.Empty);
        RefreshCampaignSpeedButtons(CampaignTimeScale);
        var material = GetComponent<Renderer>().material;
        var mainTex = material.GetTexture("_MainTex") as Texture2D;
        var mainArr = mainTex.GetPixels32();

        width = mainTex.width;//1460;//mainTex.width;//729;
        height = mainTex.height;//912;//mainTex.height;//455;

        var main2remap = new Dictionary<Color32, Color32>();
        remapArr = new Color32[mainArr.Length];
        int idx = 0;
        for(int i=0; i<mainArr.Length; i++){
            var mainColor = mainArr[i];
            if(!main2remap.ContainsKey(mainColor)){
                var low = (byte)(idx % 256);
                var high = (byte)(idx / 256);
                main2remap[mainColor] = new Color32(low, high, 0, 255);
                idx++;
            }
            var remapColor = main2remap[mainColor];
            remapArr[i] = remapColor;
        }

        paletteArr = new Color32[256*256];
        for(int i=0; i<paletteArr.Length; i++){
            paletteArr[i] = new Color32(255, 255, 255, 255);
        }
        ownerArr = (Color32[])paletteArr.Clone();

        var remapTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        remapTex.filterMode = FilterMode.Point;
        remapTex.SetPixels32(remapArr);
        remapTex.Apply(false);
        material.SetTexture("_RemapTex", remapTex);

        paletteTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        paletteTex.filterMode = FilterMode.Point;
        paletteTex.SetPixels32(paletteArr);
        paletteTex.Apply(false);
        material.SetTexture("_PaletteTex", paletteTex);

        ownerTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        ownerTex.filterMode = FilterMode.Point;
        ownerTex.SetPixels32(ownerArr);
        ownerTex.Apply(false);
        material.SetTexture("_OwnerTex", ownerTex);

        Paint();
        //UIManager.Instance.Checklist();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("escape"))
        {
            RePaint();
            //Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) SetCampaignSpeed(0f);
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) SetCampaignSpeed(.25f);
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) SetCampaignSpeed(1f);
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) SetCampaignSpeed(2f);
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) SetCampaignSpeed(5f);
        if (Owners.Instance != null)
        {
            float actualSpeed = Owners.Instance.CampaignPaused ? 0f : Owners.Instance.CampaignSimulationSpeed;
            if (!Mathf.Approximately(actualSpeed, displayedCampaignSpeed)) RefreshCampaignSpeedButtons(actualSpeed);
        }
        if (Input.GetKey("1"))
        {
            RePaint();
        }
        if (Input.GetKey("2"))
        {
            CulturePaint();
        }
        if (Input.GetKey("3"))
        {
            SupplyPaint();
        }
        if (Input.GetKey("4"))
        {
            RegionPaint();
        }
        float amount = 1;
        if (Input.GetKey("left shift"))
        {
            amount *= 5;
        }
        if (Input.GetKey("q"))
        {
            if (Camera.main.orthographicSize < 150)
            {
                Camera.main.orthographicSize += amount * 0.5f;
            }
        }
        if (Input.GetKey("e"))
        {
            if (Camera.main.orthographicSize > 50)
            {
                Camera.main.orthographicSize -= amount * 0.5f;
            }
        }
        if (Input.mouseScrollDelta.y != 0)
        {
            if (Camera.main.orthographicSize > 50 && Input.mouseScrollDelta.y > 0 || Camera.main.orthographicSize < 150 && Input.mouseScrollDelta.y < 0)
            {
                Camera.main.orthographicSize += amount * 2f * -Input.mouseScrollDelta.y;
            }
        }
        if (Input.GetKey("d"))
        {
            Camera.main.transform.position = new Vector3(Camera.main.transform.position.x + amount * 0.1f, Camera.main.transform.position.y, -10);
        }
        if (Input.GetKey("a"))
        {
            Camera.main.transform.position = new Vector3(Camera.main.transform.position.x - amount * 0.1f, Camera.main.transform.position.y, -10);
        }
        if (Input.GetKey("w"))
        {
            Camera.main.transform.position = new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y + amount * 0.1f, -10);
        }
        if (Input.GetKey("s"))
        {
            Camera.main.transform.position = new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y - amount * 0.1f, -10);
        }
        if (Input.GetMouseButtonDown(0) && !suppressMapClickUntilMouseRelease &&
            TrySelectArmyUnderPointer())
        {
            // Army markers use 2D colliders while the province map uses its own
            // 3D/map-texture picking. Resolve the army first so a click on a marker
            // cannot fall through and become a province selection.
            ConsumeCurrentMapClick();
        }
        if (Input.GetMouseButtonDown(0) && !suppressMapClickUntilMouseRelease)
        {
            StartDragPosition = Input.mousePosition;
            dragPressPosition = Input.mousePosition;
            mapPressStartedOutsideUI = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            mapDragExceededClickThreshold = false;
        }
        if (Input.GetMouseButton(0) && mapPressStartedOutsideUI)
        {
            if ((Input.mousePosition - dragPressPosition).sqrMagnitude >
                MapClickDragThresholdPixels * MapClickDragThresholdPixels)
                mapDragExceededClickThreshold = true;
            var difference = StartDragPosition - Input.mousePosition;
            var a = Camera.main.orthographicSize / 200;
            difference *= a;
            Camera.main.transform.position = Camera.main.transform.position + difference;
            StartDragPosition = Input.mousePosition;
        }
    }

    private bool TrySelectArmyUnderPointer()
    {
        if (Camera.main == null ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            return false;

        Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));
        FieldArmyHolder selected = null;
        int selectedSortingOrder = int.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            FieldArmyHolder army = hits[i] != null
                ? hits[i].GetComponentInParent<FieldArmyHolder>()
                : null;
            if (army == null || !army.isActiveAndEnabled) continue;

            SpriteRenderer renderer = army.GetComponentInChildren<SpriteRenderer>();
            int sortingOrder = renderer != null ? renderer.sortingOrder : 0;
            if (selected == null || sortingOrder > selectedSortingOrder)
            {
                selected = army;
                selectedSortingOrder = sortingOrder;
            }
        }

        if (selected == null) return false;
        selected.SelectFromMapClick();
        return true;
    }

    public void SetCampaignSpeed(float speed)
    {
        float selected = ClosestCampaignSpeed(speed);
        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsSpawned &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            CampaignNetworkPlayer.Local.RequestCampaignSpeed(selected);
            return;
        }
        string localNation = Owners.Instance != null && Owners.Instance.CallPlayer() != null
            ? Owners.Instance.CallPlayer().name : string.Empty;
        ApplyNetworkCampaignSpeed(selected, localNation);
    }

    public void ApplyNetworkCampaignSpeed(float speed, string requestingNation)
    {
        if (Owners.Instance == null) return;
        float selected = ClosestCampaignSpeed(speed);
        Owners.Instance.CampaignSimulationSpeed = selected;
        Owners.Instance.CampaignPaused = selected <= 0f;
        CampaignTimeScale = selected;
        if (selected <= 0f && !string.IsNullOrWhiteSpace(requestingNation))
            pauseRequestedBy = requestingNation;
        RefreshCampaignSpeedButtons(selected);
        ApplyPausePresentation(selected <= 0f, pauseRequestedBy);
    }

    public void SetCampaignSpeed0() => SetCampaignSpeed(0f);
    public void SetCampaignSpeed025() => SetCampaignSpeed(.25f);
    public void SetCampaignSpeed1() => SetCampaignSpeed(1f);
    public void SetCampaignSpeed2() => SetCampaignSpeed(2f);
    public void SetCampaignSpeed5() => SetCampaignSpeed(5f);

    private void BindCampaignSpeedButtons()
    {
        if (speedSettings == null)
        {
            GameObject host = GameObject.Find("SpeedSettings");
            if (host != null) speedSettings = host.transform;
        }
        campaignSpeedButtons.Clear();
        campaignSpeedButtonColors.Clear();
        for (int i = 0; i < CampaignSpeedOptions.Length; i++)
        {
            Transform child = speedSettings != null ? speedSettings.Find(CampaignSpeedButtonNames[i]) : null;
            Button button = child != null ? child.GetComponent<Button>() : null;
            campaignSpeedButtons.Add(button);
            campaignSpeedButtonColors.Add(button != null ? button.colors : ColorBlock.defaultColorBlock);
            if (button == null) continue;
            float speed = CampaignSpeedOptions[i];
            button.onClick.AddListener(() => SetCampaignSpeed(speed));
        }
    }

    private void RefreshCampaignSpeedButtons(float speed)
    {
        float selected = ClosestCampaignSpeed(speed);
        displayedCampaignSpeed = selected;
        for (int i = 0; i < campaignSpeedButtons.Count; i++)
        {
            Button button = campaignSpeedButtons[i];
            if (button == null || button.targetGraphic == null) continue;
            bool active = Mathf.Approximately(CampaignSpeedOptions[i], selected);
            ColorBlock colors = campaignSpeedButtonColors[i];
            if (active)
            {
                colors.normalColor = activeSpeedButtonColor;
                colors.highlightedColor = activeSpeedButtonColor * 1.08f;
                colors.selectedColor = activeSpeedButtonColor;
            }
            button.colors = colors;
            button.targetGraphic.color = active ? activeSpeedButtonColor : colors.normalColor;
        }
    }

    private static float ClosestCampaignSpeed(float requested)
    {
        float result = CampaignSpeedOptions[0];
        float distance = Mathf.Abs(requested - result);
        for (int i = 1; i < CampaignSpeedOptions.Length; i++)
        {
            float candidateDistance = Mathf.Abs(requested - CampaignSpeedOptions[i]);
            if (candidateDistance >= distance) continue;
            distance = candidateDistance;
            result = CampaignSpeedOptions[i];
        }
        return result;
    }

    private void BindPauseUI()
    {
        if (pauseCanvas == null) pauseCanvas = GameObject.Find("PauseCanvas");
        if (pauseSign == null && pauseCanvas != null)
        {
            Transform sign = pauseCanvas.transform.Find("PauseSign");
            if (sign != null) pauseSign = sign.gameObject;
        }
        if (pauseBlameText == null && pauseSign != null)
        {
            Text[] labels = pauseSign.GetComponentsInChildren<Text>(true);
            foreach (Text label in labels)
                if (label != null && label.gameObject != pauseSign &&
                    (label.text.Contains("<nation>") || label.text.StartsWith("Blame")))
                { pauseBlameText = label; break; }
        }
    }

    private void ApplyPausePresentation(bool paused, string nation)
    {
        if (pauseCanvas == null) BindPauseUI();
        if (pauseBlameText != null)
            pauseBlameText.text = "Blame " + (string.IsNullOrWhiteSpace(nation) ? "Unknown" : nation);
        if (pauseSign != null) pauseSign.SetActive(paused);
        if (pauseCanvas != null)
        {
            if (paused) pauseCanvas.transform.localScale = Vector3.one;
            pauseCanvas.SetActive(paused);
        }
    }
    public void Paint()
    {
        if (!CanPaint()) return;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (!TryGetProvinceRemap(province, out Color32 remapColor) || province.nation == null) continue;
            SetPaletteColor(remapColor, province.OwnershipMapColor);
        }
        UploadPalette();
    }
    public void RePaint()
    {
        if (!CanPaint()) return;
        currentMapMode = CampaignMapMode.Ownership;
        highlightedProvince = null;
        foreach(Province province in Owners.Instance.provincelist)
        {
            if (!TryGetProvinceRemap(province, out Color32 remapColor) || province.nation == null) continue;
            SetPaletteColor(remapColor, province.OwnershipMapColor);
        }
        UploadPalette();
    }
    public void PopPaint()
    {
        if (!CanPaint()) return;
        foreach(Province province in Owners.Instance.provincelist)
        {
            if (!TryGetProvinceRemap(province, out Color32 remapColor)) continue;
            SetPaletteColor(remapColor, PopToColor(province.population));
        }
        UploadPalette();
    }
    public void SupplyPaint()
    {
        if (!CanPaint()) return;
        currentMapMode = CampaignMapMode.Supply;
        foreach(Province province in Owners.Instance.provincelist)
        {
            if (!TryGetProvinceRemap(province, out Color32 remapColor)) continue;
            SetPaletteColor(remapColor, PopToColor(province.supply));
        }
        UploadPalette();
    }
    public void CulturePaint()
    {
        if (!CanPaint()) return;
        currentMapMode = CampaignMapMode.Cultures;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (!TryGetProvinceRemap(province, out Color32 remapColor)) continue;
            Culture primary = province.PrimaryCulture;
            string cultureName = primary != null ? primary.name : province.nation != null && province.nation.culture != null
                ? province.nation.culture.DisplayName : string.Empty;
            Color32 fallback = province.nation != null ? province.nation.ownerIdentity : new Color32(96, 96, 96, 255);
            SetPaletteColor(remapColor, Owners.Instance.CultureColor(cultureName, fallback));
        }
        UploadPalette();
    }
    public void RegionPaint()
    {
        currentMapMode = CampaignMapMode.Regions;
        if (!CanPaint()) return;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (!TryGetProvinceRemap(province, out Color32 remapColor)) continue;
            CampaignRegion region = Owners.Instance.CallRegionByString(province.region);
            SetPaletteColor(remapColor, region != null ? region.identity : new Color32(96, 96, 96, 255));
        }
        UploadPalette();
    }
    public void OnMouseOver()
    {
        // if (Input.GetMouseButtonDown(1))
        // {
        //     var a = Owners.Instance.provincelist[UnityEngine.Random.Range(0, 10)];
        //     FieldArmyHolder.PlayerFieldArmy.SetPositionTo(a);
        //     // banana = null;
        //     // UIManager.Instance.gameObject.transform.GetChild(1).gameObject.SetActive(false);
        //     // UIManager.Instance.gameObject.transform.GetChild(0).gameObject.SetActive(false);
        //     //return;
        // }
        if (1 == 1)
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            var mousePos = Input.mousePosition;
            var ray = Camera.main.ScreenPointToRay(mousePos);
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo))
            {
                var p = hitInfo.point;
                int x = (int)Mathf.Floor(p.x) + width / 2;
                int y = (int)Mathf.Floor(p.y) + height / 2;

                if (Input.GetMouseButtonDown(1))
                {
                    FieldArmyHolder selectedArmy = FieldArmyHolder.SelectedPlayerArmy;
                    if (selectedArmy != null)
                    {
                        Vector3 mapTarget = new Vector3(x, y, 0);
                        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsSpawned)
                        {
                            CampaignNetworkPlayer.Local.RequestArmyMove(selectedArmy.NetworkArmyId, mapTarget);
                        }
                        else
                        {
                            selectedArmy.IsPlayer = true;
                            selectedArmy.IsHumanControlled = true;
                            selectedArmy.SetTarget(mapTarget);
                        }
                    }
                }



                if (!TryGetRemap(x, y, out Color32 remapColor)) return;
                int xp = remapColor[0];
                int yp = remapColor[1];

                // var material = ;
                var mainTex = GrabMaterial().GetTexture("_MainTex") as Texture2D;

                // print(mainTex.GetPixel(x, y));
                // // print(x + " + " + y);
                // print(mainTex.GetPixel(x, y).r*255 + " + " + mainTex.GetPixel(x, y).g*255 + " + " + mainTex.GetPixel(x, y).b*255);
                // print(mainTex.GetPixel(x, y).r*255);

                if (mainTex.GetPixel(x, y) == new Color32(0, 0, 0, 0))
                {
                    if (IsCompletedMapClick())
                    {
                        SelectedProvince = null;
                        UIElement.NothingSelected();
                    }
                    return;
                }

                //changeColors(remapColor, new Color32(50, 0, 0, 255));//new Color32(127, 127, 127, 127));
                int xps = remapColor[0];
                int yps = remapColor[1];

                try
                {
                    Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
                    if (province != highlightedProvince)
                    {
                        highlightedProvince = province;
                        foreach (Province provinces in Owners.Instance.provincelist)
                        {
                            x = (int)provinces.position.x;
                            y = (int)provinces.position.y;
                            if (!TryGetProvinceRemap(provinces, out remapColor)) continue;
                            bool highlighted = currentMapMode == CampaignMapMode.Regions
                                ? province.region == provinces.region
                                : currentMapMode == CampaignMapMode.Cultures
                                    ? province.PrimaryCulture != null && provinces.PrimaryCulture != null &&
                                      string.Equals(province.PrimaryCulture.name, provinces.PrimaryCulture.name,
                                          StringComparison.OrdinalIgnoreCase)
                                    : province.nation == provinces.nation;
                            changeColors(remapColor, highlighted
                                ? new Color32(64, 64, 64, 255)
                                : new Color32(0, 0, 0, 255));
                        }

                        x = (int)Mathf.Floor(p.x) + width / 2;
                        y = (int)Mathf.Floor(p.y) + height / 2;

                        if (!TryGetRemap(x, y, out remapColor)) return;
                        changeColors(remapColor, new Color32(255, 255, 255, 255));

                        UploadOwner();
                    }

                    if (IsCompletedMapClick())
                    {
                        //Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
                        //print(x.ToString() + " " + y.ToString());
                        SelectProvince(province);
                        //province.SetAdjacents();
                        
                        //FieldArmyHolder.PlayerFieldArmy.SetTarget(province);
                    }
                }
                catch
                {

                }
                if (Input.GetMouseButtonDown(1))
                {
                    //AddFileOfPower(new Vector2(x,y),mainTex.GetPixel(x,y));
                }

            }

        }

    }
    public FieldArmyHolder SpawnArmy(Province province, string armyname = "NoName")
    {
        var b = Resources.Load<GameObject>("Prefabs/FieldArmy");
        var c = Instantiate(b, transform.GetChild(0).GetChild(1));

        c.GetComponent<FieldArmyHolder>().SetPositionTo(province);
        c.name = armyname;
        return c.GetComponent<FieldArmyHolder>();
    }
    public Material GrabMaterial()
    {
        return GetComponent<Renderer>().material;
    }
    public void SelectProvince(Province province)
    {
        if (province == null)
        {
            SelectedProvince = null;
            UIElement.NothingSelected();
            return;
        }
        SelectedNation = province.nation;
        if (UIElement.NationHost != null && province.nation != null)
            UIElement.NationHost.UpdateTitle(province.nation.name);

        SelectedProvince = province;
        if (UIElement.ProvinceHost != null)
            UIElement.ProvinceHost.UpdateTitle(province.name, province.supply.ToString());
        UIElement.ProvinceSelected();
    }
    public void SelectProvince(Vector3 spot)
    {
        var ray = Camera.main.ScreenPointToRay(spot);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo))
        {
            var p = hitInfo.point;
            int x = (int)Mathf.Floor(p.x) + width / 2;
            int y = (int)Mathf.Floor(p.y) + height / 2;

            var mainTex = GrabMaterial().GetTexture("_MainTex") as Texture2D;
            Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
            if (province == null)
            {
                SelectedProvince = null;
                UIElement.NothingSelected();
                return;
            }
            SelectProvince(province);
        }
    }
    public Province SelectProvinceFromLocation(Vector3 spot)
    {
        var ray = Camera.main.ScreenPointToRay(spot);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo))
        {
            var p = hitInfo.point;
            int x = (int)Mathf.Floor(p.x) + width / 2;
            int y = (int)Mathf.Floor(p.y) + height / 2;

            var mainTex = GrabMaterial().GetTexture("_MainTex") as Texture2D;
            Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
            return province;
        }
        return null;
    }
    public void PrepBattle()
    {
        print("Pressed button to engage");
        SelectProvince(FieldArmyHolder.PlayerFieldArmy.GrabFieldArmyProvince());
        if (SelectedProvince == null)
        {
            print("Selected province is null");
            return;
        }
        if (SelectedProvince.nation.IsPlayer)
        {
            print("Selected province is yours dumbo");
            return;
        }
        Province province = SelectedProvince;
        if (province == null)
        {
            print("Selected province is somehow null");
            return;
        }
        if (province.name == "")
        {
            SelectProvince(FieldArmyHolder.PlayerFieldArmy.GrabFieldArmyProvince());
        }

            SessionManager.Instance.savedProvince = SelectedProvince;

        if (DeterministicBattleManager.Instance != null &&
            DeterministicBattleManager.Instance.BattleSystemMode == CampaignBattleSystemMode.TileBased &&
            ProjectX.TileBattle.TileBattleCampaignManager.Instance != null)
        {
            ProjectX.TileBattle.TileBattleCampaignManager.Instance.TryStartGarrisonBattle(FieldArmyHolder.PlayerFieldArmy, SelectedProvince);
            return;
        }
        if (DeterministicBattleManager.Instance != null &&
            DeterministicBattleManager.Instance.BattleSystemMode == CampaignBattleSystemMode.Deterministic)
        {
            DeterministicBattleManager.Instance.TryStartGarrisonBattle(FieldArmyHolder.PlayerFieldArmy, SelectedProvince);
            return;
        }
        ArmyBattle(FieldArmyHolder.PlayerFieldArmy, null, SelectedProvince.garrison);

        return;
        //ArmyBattle(FieldArmyHolder.PlayerFieldArmy, SelectedProvince.CreateGarrison());
        //ArmyBattle(FieldArmyHolder.PlayerFieldArmy, province.SallyOut(FieldArmyHolder.PlayerFieldArmy));
        //province.SallyOut(FieldArmyHolder.PlayerFieldArmy);
        return;
        
        SessionManager.Instance.ChangeEnemyFaction(province.nation.name);
        SessionManager.Instance.ClientChangePlayerFaction(province.nation.name);
        SessionManager.Instance.savedProvince = province;
        SessionManager.Instance.LoadCampaign(province.nation.name);
        this.gameObject.SetActive(false);
        SceneManager.LoadScene("FightScene 1", LoadSceneMode.Additive);
    }
    public void ArmyBattle(FieldArmyHolder player, FieldArmyHolder Enemy, FieldArmy EnemyfieldArmy)
    {
        // SelectProvince(FieldArmyHolder.PlayerFieldArmy.LocalProvince);
        // Province province = SelectedProvince;

        // if (Enemy == SessionManager.Instance.savedFieldArmy)
        // {
        //     return;
        // }
        // if (Enemy.fieldArmy.nation == null)
        // {
        //     return;
        // }

        //SessionManager.Instance.ChangeEnemyFaction(Enemy.fieldArmy.nation.faction.name);
        //SessionManager.Instance.ClientChangePlayerFaction(player.fieldArmy.nation.name);
        //SessionManager.Instance.savedProvince = null;
        SessionManager.Instance.savedArmy = Enemy;
        //SessionManager.Instance.savedArmy = Enemy;
        if (SessionManager.Instance.savedArmy == null)
        {
            SessionManager.Instance.savedFieldArmy = EnemyfieldArmy;
        }
        else
        {
            SessionManager.Instance.savedFieldArmy = Enemy.fieldArmy;
        }

        //SessionManager.Instance.LoadCampaign(province.nation.name);

            this.gameObject.SetActive(false);
            OverheadCamera.gameObject.SetActive(false);
            Time.timeScale = 1;

        SceneManager.LoadScene("FightScene 1", LoadSceneMode.Additive);
    }

    void AddFileOfPower(Vector2 position, Color32 color)
    {   
        // Debug.Log(Application.persistentDataPath);
        
        print(Application.persistentDataPath + "/" + regionname + "_" + regionnumber + ".txt");
        //regionnumber++;
        StreamWriter sw = new StreamWriter(Application.persistentDataPath + "/" + regionname + "_" + regionnumber + ".txt");
        UnityEngine.Debug.LogError(Application.persistentDataPath + "/" + regionname + "_" + regionnumber + ".txt");
        sw.WriteLine("Province ={");
        sw.WriteLine("Name ={");
        sw.WriteLine(regionname + "_" + regionnumber);
        sw.WriteLine("}");
        sw.WriteLine("Color ={");
        sw.WriteLine(color.r);
        sw.WriteLine(color.g);
        sw.WriteLine(color.b);
        sw.WriteLine("}");
        sw.WriteLine("Location ={");
        sw.WriteLine(position.x);
        sw.WriteLine(position.y);
        sw.WriteLine("}");
        sw.WriteLine("Population ={");
        sw.WriteLine(culture1pop);
        sw.WriteLine(culture1);
        sw.WriteLine("}");
        if(culture2pop != 0)
        {
            sw.WriteLine("Population ={");
            sw.WriteLine(culture2pop);
            sw.WriteLine(culture2);
            sw.WriteLine("}");
        }
        if(culture3pop != 0)
        {
            sw.WriteLine("Population ={");
            sw.WriteLine(culture3pop);
            sw.WriteLine(culture3);
            sw.WriteLine("}");
        }
        sw.WriteLine("Owner ={");
        sw.WriteLine("Normal ={");
        sw.WriteLine(owner);
        sw.WriteLine("}");
        sw.WriteLine("}");
        sw.Close();
    }

    private bool CanPaint()
    {
        return Owners.Instance != null && Owners.Instance.provincelist != null &&
               remapArr != null && paletteArr != null && paletteTex != null &&
               width > 0 && height > 0 && remapArr.Length == width * height;
    }
    private bool IsCompletedMapClick()
    {
        return Input.GetMouseButtonUp(0) && !suppressMapClickUntilMouseRelease &&
            mapPressStartedOutsideUI && !mapDragExceededClickThreshold;
    }

    public void ConsumeCurrentMapClick()
    {
        suppressMapClickUntilMouseRelease = true;
        mapPressStartedOutsideUI = false;
        mapDragExceededClickThreshold = true;
    }

    private void LateUpdate()
    {
        // Keep an army click consumed through every Update/OnMouse callback and clear it only
        // after the release frame has completely finished.
        if (suppressMapClickUntilMouseRelease && Input.GetMouseButtonUp(0))
            suppressMapClickUntilMouseRelease = false;
    }

    private bool TryGetProvinceRemap(Province province, out Color32 remapColor)
    {
        remapColor = default;
        if (province == null || remapArr == null || width <= 0 || height <= 0) return false;
        int x = (int)province.position.x;
        int y = (int)province.position.y;
        return TryGetRemap(x, y, out remapColor);
    }

    private bool TryGetRemap(int x, int y, out Color32 remapColor)
    {
        remapColor = default;
        if (remapArr == null || width <= 0 || height <= 0) return false;
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
        int index = x + y * width;
        if ((uint)index >= (uint)remapArr.Length) return false;
        remapColor = remapArr[index];
        return true;
    }

    private static int PaletteIndex(Color32 remapColor)
    {
        return remapColor.r + remapColor.g * 256;
    }

    private void SetPaletteColor(Color32 remapColor, Color32 showColor)
    {
        int index = PaletteIndex(remapColor);
        if (paletteArr == null || (uint)index >= (uint)paletteArr.Length) return;
        paletteArr[index] = showColor;
    }

    private void UploadPalette()
    {
        if (paletteTex == null || paletteArr == null || paletteArr.Length != 256 * 256) return;
        paletteTex.SetPixels32(paletteArr);
        paletteTex.Apply(false, false);
    }

    private void UploadOwner()
    {
        if (ownerTex == null || ownerArr == null || ownerArr.Length != 256 * 256) return;
        ownerTex.SetPixels32(ownerArr);
        ownerTex.Apply(false, false);
    }

    void changeColor(Color32 remapColor, Color32 showColor){
        SetPaletteColor(remapColor, showColor);
    }

    void changeColors(Color32 remapColor, Color32 showColor){
        int index = PaletteIndex(remapColor);
        if (ownerArr == null || (uint)index >= (uint)ownerArr.Length) return;

        ownerArr[index] = showColor;
    }
    public Color PopToColor(int population)
    {
        if(population >= 2500)
        {
            return new Color32(0,255,33,1);
        }
        if (population >= 2000)
        {
            return new Color32(76, 255, 0, 1);
        }
        if (population >= 1500)
        {
            return new Color32(182, 255, 0, 1);
        }
        double a = (double)population / 2000f;
        byte b = (byte)(255 * a);
        return new Color32(255, b, 0, 1);

        if (population >= 1000)
        {
            return new Color32(255, 216, 0, 1);
        }
        if(population >= 750)
        {
            return new Color32(255,106,0,1);
        }
        if(population >= 500)
        {
            return new Color32(255,53,0,1);
        }
        if(population >= 250)
        {
            return new Color32(255,0,0,1);
        }
        return new Color32(0,0,0,1);
    }
}
