using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using System.Diagnostics;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Linq;

using UnityEngine.Tilemaps;

public class Mapshower : MonoBehaviour
{
    public string regionname;
    public int regionnumber;
    public string statename;
    public string owner;
    public string culture1;
    public int culture1pop;
    public string culture2;
    public int culture2pop;
    public string culture3;
    public int culture3pop;

    public Nation SelectedNation;
    public Province SelectedProvince;
    public ArmyMovement SelectedArmy;


    public int width;
    public int height;
    public Vector2 Offset = new Vector2(0,0);
    private Province DraggedProvince = new Province();
    private Province OldProvince = new Province();

    Color32[] remapArr;
    Texture2D paletteTex;
    Texture2D ownerTex;

    Color32 prevColor;
    Color32 prevColorA;
    bool selectAny = false;
    bool selectALL = false;
    public bool potato = true;
    public GameObject banana;
    public static Mapshower Instance;

    void Awake()
    {
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

        
    }
    public Color32[] GrabremapArr()
    {
        return remapArr;
    }
    void OnEnable()
    {
        //Paint();
        //RePaint();
    }
    // Start is called before the first frame update
    void Start()
    {
        
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

        var paletteArr = new Color32[256*256];
        for(int i=0; i<paletteArr.Length; i++){
            paletteArr[i] = new Color32(255, 255, 255, 255);
        }

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
        ownerTex.SetPixels32(paletteArr);
        ownerTex.Apply(false);
        material.SetTexture("_OwnerTex", ownerTex);

        Paint();
        
        gameObject.SetActive(false);
    }
    public void Potato()
    {
        Tile tomato = Resources.Load<Tile>("Tiles/Hexes/BaseHex");
        var mainTex = GetComponent<Renderer>().material.GetTexture("_MainTex") as Texture2D;
        var areaTex = GetComponent<Renderer>().material.GetTexture("_AreaTex") as Texture2D;
        foreach (Vector3Int position in banana.GetComponent<Tilemap>().cellBounds.allPositionsWithin)
        {
            Vector3 potato = banana.GetComponent<Tilemap>().CellToWorld(position);
            int x = (int)Mathf.Floor(potato.x) + width / 2;
            int y = (int)Mathf.Floor(potato.y) + height / 2;
            
            if(banana.GetComponent<Tilemap>().HasTile(position))
            {
                var a = new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0);
                if(mainTex.GetPixel(x, y).a != 0)
                {
                    tomato = Instantiate(tomato);
                    //TileBase tomato = banana.GetComponent<Tilemap>().GetTile(position);
                    
                    tomato.color =  areaTex.GetPixel(x, y);

                    Province corn = Owners.Instance.CallProvinceByColor(a);

                    
                    //print(mainTex.GetPixel(x, y));
                    //tomato.color = corn.nation.ownerIdentity;//new Color(corn.nation.ownerIdentity.r, corn.nation.ownerIdentity.g, corn.nation.ownerIdentity.b, 255);

                    print(areaTex.GetPixel(x, y));
                    print(tomato.color);
                    //print(corn);
                    //print(corn.nation.ownerIdentity);
                    tomato.color = new Color(tomato.color.r, tomato.color.g, tomato.color.b, 0);
                    
                    banana.GetComponent<Tilemap>().SetTile(position,tomato);
                    corn.ProvincialTileList.Add(position);
                }
                else
                {
                    tomato = Instantiate(tomato);
                    tomato.color = new Color(0,0,0,0);
                    banana.GetComponent<Tilemap>().SetTile(position,tomato);
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("1"))
        {
            RePaint();
        }
        if (Input.GetKeyDown("2"))
        {
            CulturePaint();
        }
        if (Input.GetKeyDown("3"))
        {
            PopPaint();
        }
        if (Input.GetKeyDown("t"))
        {
            TestTime();
        }
        if (Input.GetKeyDown("escape"))
        {
            SceneManager.LoadScene("SampleScene");
            //PopPaint();
            ////Application.Quit();
        }
        if (Input.GetKeyDown("space"))
        {
            foreach(var x in Owners.Instance.nationlist)
            {
                print(x.ownerIdentity);
            }
            foreach (var item in Owners.Instance.provincelist)
            {
                item.SetAdjacencies();
            }
        }
        var potato = 0.1f;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            potato = potato * 10;
        }
        if (Input.GetKey("q"))
        {
            Camera.main.orthographicSize += potato;
            float a = Camera.main.orthographicSize/300;
            foreach (var item in Owners.Instance.armylist)
            {
                item.GetComponent<RectTransform>().localScale = new Vector2(a,a);
            }
            foreach (var item in Owners.Instance.provincelist)
            {
                if(item.Drafty != null)
                {
                    item.Drafty.GetComponent<RectTransform>().localScale = new Vector2(a,a);
                }
            }
        }
        if (Input.GetKey("e"))
        {
            Camera.main.orthographicSize -= potato;
            float a = Camera.main.orthographicSize/300;
            foreach (var item in Owners.Instance.armylist)
            {
                item.GetComponent<RectTransform>().localScale = new Vector2(a,a);
            }
            foreach (var item in Owners.Instance.provincelist)
            {
                if(item.Drafty != null)
                {
                    item.Drafty.GetComponent<RectTransform>().localScale = new Vector2(a,a);
                }
            }
        }
        if (Input.GetKey("w"))
        {
            Camera.main.transform.position = Camera.main.transform.position + new Vector3(0,potato,0);
        }
        if (Input.GetKey("s"))
        {
            Camera.main.transform.position = Camera.main.transform.position + new Vector3(0,-potato,0);
        }
        if (Input.GetKey("d"))
        {
            Camera.main.transform.position = Camera.main.transform.position + new Vector3(potato,0,0);
        }
        if (Input.GetKey("a"))
        {
            Camera.main.transform.position = Camera.main.transform.position + new Vector3(-potato,0,0);
        }
    }
    public void Paint()
    {
        int i = 0;
        
        foreach(Province province in Owners.Instance.provincelist)
        {
            i = i+1;
            int x = (int)province.position.x;
            int y = (int)province.position.y;

            var remapColor = remapArr[x + y * width];
            int xp = remapColor[0];
            int yp = remapColor[1];

            if(!selectAny || !prevColor.Equals(remapColor)){
                selectAny = true;
                prevColor = remapColor;
                paletteTex.SetPixel(xp, yp, province.nation.ownerIdentity);
                paletteTex.Apply(false);
                ownerTex.Apply(false);
            }
        }
    }
    public void RePaint()
    {
        //print(Owners.Instance.provincelist);
        foreach(Province province in Owners.Instance.provincelist)
        {
            int x = (int)province.position.x;
            int y = (int)province.position.y;

            var remapColor = remapArr[x + y * width];
            int xp = remapColor[0];
            int yp = remapColor[1];

            if(!selectAny || !prevColor.Equals(remapColor)){
                selectAny = true;
                prevColor = remapColor;
                paletteTex.SetPixel(xp, yp, province.nation.ownerIdentity);
                paletteTex.Apply(false);
                ownerTex.Apply(false);
            }
        }
    }

    public void PopPaint()
    {
        foreach(Province province in Owners.Instance.provincelist)
        {
            int x = (int)province.position.x;
            int y = (int)province.position.y;

            var remapColor = remapArr[x + y * width];
            int xp = remapColor[0];
            int yp = remapColor[1];

            if(!selectAny || !prevColor.Equals(remapColor)){
                selectAny = true;
                prevColor = remapColor;
                paletteTex.SetPixel(xp, yp, GrabPopulation(province.cultures));//PopToColor(province.troops));
                paletteTex.Apply(false);
                ownerTex.Apply(false);
            }
        }
    }
    public void CulturePaint()
    {
        foreach(Province province in Owners.Instance.provincelist)
        {
            int x = (int)province.position.x;
            int y = (int)province.position.y;

            var remapColor = remapArr[x + y * width];
            int xp = remapColor[0];
            int yp = remapColor[1];

            if(!selectAny || !prevColor.Equals(remapColor)){
                selectAny = true;
                prevColor = remapColor;
                paletteTex.SetPixel(xp, yp, GrabCulture(province.cultures));
                paletteTex.Apply(false);
                ownerTex.Apply(false);
            }
        }
    }
    public void OnMouseOver()
    {
        if (Input.GetMouseButtonDown(1))
        {
            // banana = null;
            // UIManager.Instance.gameObject.transform.GetChild(1).gameObject.SetActive(false);
            // UIManager.Instance.gameObject.transform.GetChild(0).gameObject.SetActive(false);
            //return;
        }

        if(1==1)
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            var mousePos = Input.mousePosition;
            var ray = Camera.main.ScreenPointToRay(mousePos);
            RaycastHit hitInfo;
            if(Physics.Raycast(ray, out hitInfo)){
                var p = hitInfo.point;
                int x = (int)Mathf.Floor(p.x) + width / 2;
                int y = (int)Mathf.Floor(p.y) + height / 2;

                var remapColor = remapArr[x + y * width];
                // print(remapColor.r + " " + x.ToString() + " " + y.ToString());
                int xp = remapColor[0];
                int yp = remapColor[1];

                var material = GetComponent<Renderer>().material;
                var mainTex = material.GetTexture("_MainTex") as Texture2D;
                


                // print(mainTex.GetPixel(x, y));
                // // print(x + " + " + y);
                // print(mainTex.GetPixel(x, y).r*255 + " + " + mainTex.GetPixel(x, y).g*255 + " + " + mainTex.GetPixel(x, y).b*255);
                // print(mainTex.GetPixel(x, y).r*255);

                if(mainTex.GetPixel(x,y) == new Color32(0,0,0,0))
                {
                    return;
                }

                if(selectALL){
                    changeColors(prevColorA, new Color32(255, 255, 255, 255));
                }
                selectALL = true;
                prevColorA = remapColor;
                
                //changeColors(remapColor, new Color32(50, 0, 0, 255));//new Color32(127, 127, 127, 127));
                int xps = remapColor[0];
                int yps = remapColor[1];

                if(Input.GetMouseButtonDown(1))
                {
                    AddFileOfPower(new Vector2(x,y),mainTex.GetPixel(x,y));
                }

                Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
                if(Input.GetMouseButtonDown(0))
                {
                    //province.SetAdjacencies();
                    foreach (var item in province.GrabAdjacentProvinces())
                    {
                        UnityEngine.Debug.LogError(item.name);
                    }
                }
                if(Input.GetMouseButtonDown(1))
                {
                    if(SelectedArmy != null)
                    {
                        if(province.nation.GrabDiplomaticStatus(SelectedArmy.nation) != "peace")
                        {
                            var location = province.position;
                            location = new Vector2(location.x-Offset.x+20,location.y-Offset.y);
                            SelectedArmy.target = location;
                            SelectedArmy.province = province.name;
                        }
                    }
                    ///print(new Vector2(x,y));
                    //AddFileOfPower(new Vector2(x,y),mainTex.GetPixel(x,y));
                }

                // UIElement.NationHost.UpdateTitle(province.nation.name);
                // UIElement.ProvinceHost.UpdateTitle(province.name);

                    if(2==2)
                    {
                        
                        if(Input.GetMouseButtonDown(0))
                        {
                            DraggedProvince = province;
                            material.SetFloat("_ProvinceView", 0f);
                            Owners.Instance.statelist.Find(x => x.name == DraggedProvince.state).GrabPopulationPieCharts();
                            //print(mainTex.GetPixel(x, y) + " " + x + " " + y);
                        }
                        // if(Input.GetMouseButtonUp(0))
                        // {
                        //     if(DraggedProvince != province)
                        //     {
                        //         if(DraggedProvince != null && DraggedProvince.nation != null)
                        //         {
                        //             if(DraggedProvince.nation.IsPlayer)
                        //             {
                        //                 //UnityEngine.Debug.Log(DraggedProvince.nation.GrabDiplomaticStatus(province.nation.name));
                        //                 //string diplostatus = GrabDiplomaticStatus();
                        //                 if(DraggedProvince.nation.CanIDoThis(province.nation.name))
                        //                 {
                        //                     var a = Owners.Instance.statelist.Find(x => x.name == DraggedProvince.state).Capitol;

                        //                     foreach (var RPC in TestRelay.Instance.PlayerObjects)
                        //                     {
                        //                         if(RPC.GetComponent<NetworkObject>().IsLocalPlayer)
                        //                         {
                        //                             RPC.GetComponent<RpcTest>().SendTroops(a.name, province.name, DraggedProvince.nation.name);
                        //                         }
                        //                         //RPC.GetComponent<RpcTest>().ChangeProvinceOwner(province.name, DraggedProvince.nation.name);
                        //                     }
                        //                 }
                        //             }
                        //         }
                        //     }
                        //     else//if(DraggedProvince == province)
                        //     {
                        //         // material.SetFloat("_ProvinceView", 1f);
                        //         // // PaintSettlementsInProvince(province);
                        //         // //     x = (int)province.position.x;
                        //         // //     y = (int)province.position.y;

                        //         // //         remapColor = remapArr[x + y * width];
                        //         // //         xp = remapColor[0];
                        //         // //         yp = remapColor[1];

                        //         // //         //var state = Owners.Instance.statelist.Find(x => x.name == province.state);

                        //         // //         // if(province.nation == provinces.nation)
                        //         // //         // {
                        //         // //         //     changeColors(remapColor, new Color32(64, 64, 64, 255));//state.stateIdentity);
                        //         // //         // }
                        //         // //         // else
                        //         // //         // {
                        //         // //             print(x + " " + y + " " + remapColor);
                        //         // //             changeColors(remapColor, new Color32(64, 64, 64, 255));
                                
                        //         // // ChangeProvinceOwner(DraggedProvince.name, DraggedProvince.nation.name, true);
                        //     }
                        // }
                    }
                
                foreach(Province provinces in Owners.Instance.provincelist)
                {
                    x = (int)provinces.position.x;
                    y = (int)provinces.position.y;

                    remapColor = remapArr[x + y * width];
                    xp = remapColor[0];
                    yp = remapColor[1];

                    //var state = Owners.Instance.statelist.Find(x => x.name == province.state);

                    // if(province.nation == provinces.nation)
                    // {
                    //     changeColors(remapColor, new Color32(64, 64, 64, 255));//state.stateIdentity);
                    // }
                    // else
                    // {
                        changeColors(remapColor, new Color32(255, 255, 255, 255));
                    //}
                }

                foreach(Province provinces in Owners.Instance.provincelist)
                {
                    if(provinces.state == province.state)
                    {
                        x = (int)provinces.position.x;
                        y = (int)provinces.position.y;

                        remapColor = remapArr[x + y * width];
                        xp = remapColor[0];
                        yp = remapColor[1];

                        //var state = Owners.Instance.statelist.Find(x => x.name == province.state);

                        // if(province.nation == provinces.nation)
                        // {
                        //     changeColors(remapColor, new Color32(64, 64, 64, 255));//state.stateIdentity);
                        // }
                        // else
                        // {
                            changeColors(remapColor, new Color32(64, 64, 64, 255));
                        //}
                    }
                }

                // x = (int)Mathf.Floor(p.x) + width / 2;
                // y = (int)Mathf.Floor(p.y) + height / 2;

                // remapColor = remapArr[x + y * width];
                
                // changeColors(remapColor, new Color32(64, 64, 64, 255));

                //ownerTex.SetPixel(xps, yps, province.nation.ownerIdentity);
                


                ownerTex.Apply(true);
                paletteTex.Apply(true);
                
                if(Input.GetMouseButtonDown(0))
                {
                    //Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
                    SelectedNation = province.nation;
                    //province.nation.name);
                    UIElement.NationHost.UpdateTitle(province.nation.name);
                    //State stat = ;
                    UIElement.NationHost.UpdateDescription(Owners.Instance.statelist.Find(x => x.name == province.state));
                    UIElement.NationHost.Updatethird(Owners.Instance.CallPlayer().GrabDiplomaticStatus(province.nation.name));
                    UIElement.NationHost.UpdateFourth(GrabStateStuff(province.state));

                    SelectedProvince = province;
                    BuildingTab.Instance.GrabBuildings(SelectedProvince);
                    UIElement.ProvinceHost.UpdateTitle(province.name);
                    UIElement.ProvinceHost.UpdateDescription(Owners.Instance.statelist.Find(x => x.name == province.state),false);
                    UIElement.ProvinceHost.Updatethird(Owners.Instance.statelist.Find(x => x.name == province.state).Capitol.troops.ToString());
                    
                    // if(!province.nation.IsPlayer)
                    // {
                    //     PrepBattle(province);
                    // }
                }
                
                
                // // UIManager.Instance.ChangeText(province);
                // // print(Owners.Instance.CallProvinceByString(province.name).identity);
                // //print(province.name);
                // // if(banana != null)
                // // {
                // //     //UIManager.Instance.gameObject.transform.GetChild(1).gameObject.SetActive(true);
                // //     //UIManager.Instance.gameObject.transform.GetChild(0).gameObject.SetActive(false);
                // //     if(banana.GetComponent<CampaignArmyController>().general.nation.IsPlayer)
                // //     {
                // //         banana.GetComponent<CampaignArmyController>().TryToMove(province);
                //         // RePaint();
                // //     }
                // // }
                // // else
                // // {
                // //     UIManager.Instance.gameObject.transform.GetChild(1).gameObject.SetActive(false);
                // //     UIManager.Instance.gameObject.transform.GetChild(0).gameObject.SetActive(true);
                // // }
                
                // // 
            }
            
        }

    }
    public void SendTroops(string origin, string target, string owner, int numero, int count, bool SpawnNewArmy = false)
    {
        foreach (var item in Owners.Instance.armylist)
        {
            if(item == null)
            {
                continue;
            }
            if(item.GetComponent<ArmyMovement>().province == target && item.GetComponent<ArmyMovement>().nation == owner && item.GetComponent<ArmyMovement>().origin == origin)
            {
                Vector3 OriginLocation = Owners.Instance.provincelist.Find(x => x.name == origin).position;
                OriginLocation = new Vector2(OriginLocation.x-Offset.x,OriginLocation.y-Offset.y);

                //var targetLocation = Owners.Instance.provincelist.Find(x => x.name == target).position;
                //targetLocation = new Vector2(targetLocation.x-366+20,targetLocation.y-218);
                
                var heading  = OriginLocation - item.transform.position;
                var distance = heading.magnitude;

                if(distance < 100)
                {
                    item.GetComponent<ArmyMovement>().troops += count;
                    item.GetComponent<ArmyMovement>().SetTroopsMarker();

                    if(Owners.Instance.nationlist.Find(x => x.name == owner).IsPlayer)
                    {
                        Owners.Instance.provincelist.Find(x => x.name == origin).AddTroops(-count);
                    }
                    return;
                }
            }
        }

        if(count < 1)
        {
            return;
        }
        GameObject potato = Resources.Load<GameObject>("Prefabs/Map_Farmer");
        GameObject tomato = Instantiate(potato, transform.GetChild(2));//
        Vector2 location = Owners.Instance.provincelist.Find(x => x.name == origin).position;
        location = new Vector2(location.x-Offset.x,location.y-Offset.y);
        tomato.transform.position = location;
        location = Owners.Instance.provincelist.Find(x => x.name == target).position;
        location = new Vector2(location.x-Offset.x+20,location.y-Offset.y);
        tomato.GetComponent<ArmyMovement>().origin = origin;
        tomato.GetComponent<ArmyMovement>().target = location;
        tomato.GetComponent<ArmyMovement>().province = target;
        tomato.GetComponent<ArmyMovement>().nation = owner;
        tomato.GetComponent<ArmyMovement>().troops = count;
        tomato.GetComponent<ArmyMovement>().name = numero.ToString();
        var nation = Owners.Instance.nationlist.Find(x => x.name == owner);
        tomato.transform.GetComponent<Image>().color = new Color32(nation.ownerIdentity.r, nation.ownerIdentity.g, nation.ownerIdentity.b, 255);
        tomato.GetComponent<ArmyMovement>().SetTroopsMarker();
        tomato.name = numero.ToString();
        //Owners.Instance.provincelist.Find(x => x.name == origin).troops -=;
        if(!SpawnNewArmy)
        {
            if(Owners.Instance.nationlist.Find(x => x.name == owner).IsPlayer)
            {
                Owners.Instance.provincelist.Find(x => x.name == origin).AddTroops(-count);
            }
        }
        
    }
    // public void ChangeProvinceOwner(string province, string owner, bool tempy = false)
    // {
        
    //     if(tempy == true)
    //     {
    //         Tile tomato = Resources.Load<Tile>("Tiles/Hexes/BaseHex");
    //         var corn = OldProvince;
            
    //         foreach (var item in OldProvince.ProvincialTileList)
    //         {
    //             tomato = Instantiate(tomato);
    //             Tile a = (Tile)banana.GetComponent<Tilemap>().GetTile(item);
    //             print(a.color);
    //             //tomato.color = a.color;
    //             tomato.color = new Color((float)a.color.r, (float)a.color.g, (float)a.color.b, 0);//new Color(corn.nation.ownerIdentity.r, corn.nation.ownerIdentity.g, corn.nation.ownerIdentity.b, 0);
    //             //print(tomato.color);
    //             banana.GetComponent<Tilemap>().SetTile(item,tomato);
    //         }
    //     }
    //     OldProvince = Owners.Instance.provincelist.Find(x => x.name == province);

    //     if(1==1)//tempy == true)
    //     {
    //         Tile tomato = Resources.Load<Tile>("Tiles/Hexes/BaseHex");
    //         var corn = Owners.Instance.provincelist.Find(x => x.name == province);
    //         //tomato = Instantiate(tomato);
    //         foreach (var item in Owners.Instance.provincelist.Find(x => x.name == province).ProvincialTileList)
    //         {
    //             tomato = Instantiate(tomato);
    //             Tile a = (Tile)banana.GetComponent<Tilemap>().GetTile(item);
    //             tomato.color = new Color((float)a.color.r, (float)a.color.g, (float)a.color.b, 255);//corn.nation.ownerIdentity;//new Color(corn.nation.ownerIdentity.r, corn.nation.ownerIdentity.g, corn.nation.ownerIdentity.b, 255);
    //             banana.GetComponent<Tilemap>().SetTile(item,tomato);
    //         }
    //         return;
    //     }
    //     // if(Owners.Instance.provincelist.Find(x => x.name == province).nation != Owners.Instance.nationlist.Find(x => x.name == owner))
    //     // {
    //     //     Tile tomato = Resources.Load<Tile>("Tiles/Hexes/BaseHex");
    //     //     var corn = Owners.Instance.provincelist.Find(x => x.name == province);
    //     //     tomato = Instantiate(tomato);
    //     //     foreach (var item in Owners.Instance.provincelist.Find(x => x.name == province).ProvincialTileList)
    //     //     {
    //     //         Tile a = (Tile)banana.GetComponent<Tilemap>().GetTile(item);
    //     //         tomato.color = new Color(corn.nation.ownerIdentity.r, corn.nation.ownerIdentity.g, corn.nation.ownerIdentity.b, 0);//Owners.Instance.nationlist.Find(x => x.name == owner).ownerIdentity;
    //     //         if(tempy)
    //     //         {
    //     //             tomato.color = new Color(corn.nation.ownerIdentity.r, corn.nation.ownerIdentity.g, corn.nation.ownerIdentity.b, 255);
    //     //         }
    //     //         banana.GetComponent<Tilemap>().SetTile(item,tomato);
    //     //     }
    //     //     UnityEngine.Debug.LogError("Done");
    //     // }
    // }
    public void ChangeProvinceOwner(string province, string owner)
    {
        if(Owners.Instance.provincelist.Find(x => x.name == province).nation != Owners.Instance.nationlist.Find(x => x.name == owner))
        {
            var thisprovince = Owners.Instance.provincelist.Find(x => x.name == province);
            thisprovince.KillPop();
            if(Owners.Instance.statelist.Find(x => x.name == thisprovince.state) != null)
            {
                if(Owners.Instance.statelist.Find(x => x.name == thisprovince.state).Capitol == thisprovince && Owners.Instance.statelist.Find(x => x.name == thisprovince.state).provincelist.Count > 1)
                {
                    Owners.Instance.statelist.Find(x => x.name == thisprovince.state).Capitol = Owners.Instance.statelist.Find(x => x.name == thisprovince.state).provincelist[1];
                }
            }

            Owners.Instance.statelist.Find(x => x.name == thisprovince.state).provincelist.Remove(thisprovince);


            Owners.Instance.provincelist.Find(x => x.name == province).nation = Owners.Instance.nationlist.Find(x => x.name == owner);
            if(Owners.Instance.provincelist.Find(x => x.name == province).Drafty != null)
            {
                Owners.Instance.provincelist.Find(x => x.name == province).Drafty.transform.GetComponent<Image>().color = new Color32(Owners.Instance.nationlist.Find(x => x.name == owner).ownerIdentity.r, Owners.Instance.nationlist.Find(x => x.name == owner).ownerIdentity.g, Owners.Instance.nationlist.Find(x => x.name == owner).ownerIdentity.b, 255);
            }
            var a = new List<State>();
            foreach (var item in Owners.Instance.statelist)
            {
                if(item.nation.name == owner)
                {
                    a.Add(item);
                }
            }
            Owners.Instance.provincelist.Find(x => x.name == province).state = a[0].name;
            var distances = (a[0].Capitol.position - Owners.Instance.provincelist.Find(x => x.name == province).position).magnitude;
            //print(distances);
            foreach (var item in a)
            {
                var heading  = item.Capitol.position - Owners.Instance.provincelist.Find(x => x.name == province).position;
                var distance = heading.magnitude;
                //print(distance + " vs " + distances);
                if(distance < distances)
                {
                    Owners.Instance.provincelist.Find(x => x.name == province).state = item.name;
                    distances = distance;
                }   
            }
            if(distances > 100)
            {
                State state = new State();
                state.name = thisprovince.name + " State";
                state.nation = thisprovince.nation;
                state.stateIdentity = thisprovince.identity;
                state.provincelist = new List<Province>();
                state.provincelist.Add(thisprovince);
                state.Capitol = thisprovince;
                thisprovince.state = state.name;
                Owners.Instance.statelist.Add(state);
            }
            else
            {
                Owners.Instance.statelist.Find(x => x.name == Owners.Instance.provincelist.Find(x => x.name == province).state).provincelist.Add(Owners.Instance.provincelist.Find(x => x.name == province));
            }
            //Potato();
            RePaint();
        }
    }
    public void TestTime()
    {
        // foreach (var item in Owners.Instance.provincelist)
        // {
        //     item.ResetJobs();
        //     var texty = "";
        //     texty += item.name + "\n";
        //     foreach (var items in item.GrabProvincialOutput())
        //     {
        //         texty += items.amount + " " + items.resource.name;
        //     }
        //     print(texty);
        // }
        foreach (var item in Owners.Instance.statelist)
        {
            // item.();
            // item.ResetJobs();
            var texty = "";
            texty += item.name + "\n";
            foreach (var items in item.GrabStateOutput())
            {
                texty += items.amount + " :" + items.resource.name + ":\n";
            }
            print(texty);
        }
    }
    public string GrabStateStuff(string namey)
    {
        var a = Owners.Instance.statelist.Find(x => x.name == namey);
        var textoid = "";
        textoid += a.name + "\n";
        foreach (var items in a.GrabStateOutput())
        {
            items.amount = (float)Math.Round(items.amount, 2);
            textoid += items.amount + " <sprite name=\"" + items.resource.name + "\">\n";
        }
        return textoid;
        // foreach (var item in Owners.Instance.provincelist)
        // {
        //     item.ResetJobs();
        //     var texty = "";
        //     texty += item.name + "\n";
        //     foreach (var items in item.GrabProvincialOutput())
        //     {
        //         texty += items.amount + " " + items.resource.name;
        //     }
        //     print(texty);
        // }
        // foreach (var item in Owners.Instance.statelist)
        // {
        //     // item.();
        //     // item.ResetJobs();
        //     var texty = "";
        //     texty += item.name + "\n";
        //     foreach (var items in item.GrabStateOutput())
        //     {
        //         texty += items.amount + " :" + items.resource.name + ":\n";
        //     }
        //     print(texty);
        // }
    }
    public void DevProvince()
    {
        if(SelectedProvince == null)
        {
            return;
        }
        if(SelectedProvince.nation.IsPlayer)
        {
            return;
        }
        SelectedProvince.population += 1;
        var potato = GameObject.Find("Text (Legacy) (1)");
        potato.GetComponent<Text>().text = SelectedProvince.population.ToString();
    }
    public void OpenBuildingMenu()
    {
        FactionUpgrade.Instance.gameObject.SetActive(true);
    }
    public void PrepBattle()
    {
        if(SelectedProvince == null)
        {
            return;
        }
        if(SelectedProvince.nation.IsPlayer)
        {
            return;
        }
        Province province = SelectedProvince;
        SessionManager.Instance.ChangeEnemyFaction(province.nation.name);
        SessionManager.Instance.ClientChangePlayerFaction(province.nation.name);
        SessionManager.Instance.savedProvince = province;
        SessionManager.Instance.LoadCampaign(province.nation.name);
        this.gameObject.SetActive(false);
        SceneManager.LoadScene("FightScene 1", LoadSceneMode.Additive);
    }

    void AddFileOfPower(Vector2 position, Color32 color)
    {   
        // Debug.Log(Application.persistentDataPath);
        
        print(Application.persistentDataPath + "/" + regionname + "_" + regionnumber + ".txt");
        regionnumber++;
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
        sw.WriteLine("State ={");
        sw.WriteLine(statename);
        sw.WriteLine("}");
        sw.Close();
    }

    void changeColor(Color32 remapColor, Color32 showColor){
        int xp = remapColor[0];
        int yp = remapColor[1];

        paletteTex.SetPixel(xp, yp, showColor);
    }

    void changeColors(Color32 remapColor, Color32 showColor){
        int xp = remapColor[0];
        int yp = remapColor[1];

        ownerTex.SetPixel(xp, yp, showColor);
    }
    public Color PopToColor(int population)
    {
        if(population >= 50)
        {
            return new Color32(0,255,33,1);
        }
        if(population >= 40)
        {
            return new Color32(76,255,0,1);
        }
        if(population >= 30)
        {
            return new Color32(182,255,0,1);
        }
        if(population >= 20)
        {
            return new Color32(255,216,0,1);
        }
        if(population >= 15)
        {
            return new Color32(255,106,0,1);
        }
        if(population >= 10)
        {
            return new Color32(255,53,0,1);
        }
        if(population >= 5)
        {
            return new Color32(255,0,0,1);
        }
        return new Color32(0,0,0,1);
    }
    public Color GrabPopulation(List<Culture> culturelist, int MaxPopulation = 7)
    {
        float a = 0;
        foreach (var item in culturelist)
        {
            a += item.population;
        }
        float c = ((1-a) / ((float)MaxPopulation-a))*2;
        if(c >= 1)
        {
            byte e = (byte)(255-(255*c));
            var b = new Color32(e, 255, 0, 255);
            return b;
        }
        else
        {
            byte e = (byte)(255-(255*c));
            var b = new Color32(255, e, 0, 255);
            return b;
        }
        var d = new Color32(0, 0, 0, 255);
        return d;
    }
    public Color GrabCulture(List<Culture> culturelist)
    {
        var a = 0;
        var b = new Color(0,0,0,0);
        foreach (var item in culturelist)
        {
            if(item.population > a)
            {
                a = item.population;
                b = item.ownerIdentity;
            }
        }
        return b;
    }
}
