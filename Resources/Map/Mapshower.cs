using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using System.Diagnostics;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class Mapshower : MonoBehaviour
{
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
    private Province DraggedProvince = new Province();

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
        if (Input.GetKey("q"))
        {
            Camera.main.orthographicSize += 0.1f;
        }
        if (Input.GetKey("e"))
        {
            Camera.main.orthographicSize -= 0.1f;
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
        print(Owners.Instance.provincelist);
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
                paletteTex.SetPixel(xp, yp, PopToColor(province.population));
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
                paletteTex.SetPixel(xp, yp, province.cultures[0].ownerIdentity);
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

                Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));

                //

                // UIElement.NationHost.UpdateTitle(province.nation.name);
                // UIElement.ProvinceHost.UpdateTitle(province.name);

                    if(2==2)
                    {
                        
                        if(Input.GetMouseButtonDown(0))
                        {
                            DraggedProvince = province;
                        }
                        if(Input.GetMouseButtonUp(0))
                        {
                            if(DraggedProvince != province)
                            {
                                if(DraggedProvince != null && DraggedProvince.nation != null)
                                {
                                    if(DraggedProvince.nation.IsPlayer)
                                    {
                                        foreach (var RPC in TestRelay.Instance.PlayerObjects)
                                        {
                                            RPC.GetComponent<RpcTest>().SendTroops(DraggedProvince.name, province.name, DraggedProvince.nation.name);
                                            //RPC.GetComponent<RpcTest>().ChangeProvinceOwner(province.name, DraggedProvince.nation.name);
                                        }
                                    }
                                }
                            }
                        }
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

                x = (int)Mathf.Floor(p.x) + width / 2;
                y = (int)Mathf.Floor(p.y) + height / 2;

                remapColor = remapArr[x + y * width];
                changeColors(remapColor, new Color32(64, 64, 64, 255));

                //ownerTex.SetPixel(xps, yps, province.nation.ownerIdentity);
                


                ownerTex.Apply(true);
                paletteTex.Apply(true);
                
                if(Input.GetMouseButtonDown(0))
                {
                    //Province province = Owners.Instance.CallProvinceByColor(new Color(mainTex.GetPixel(x, y).r, mainTex.GetPixel(x, y).g, (mainTex.GetPixel(x, y).b), 0));
                    SelectedNation = province.nation;
                    UIElement.NationHost.UpdateTitle(province.troops.ToString());//province.nation.name);

                    SelectedProvince = province;
                    UIElement.ProvinceHost.UpdateTitle(province.name);
                    
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
                // if(Input.GetMouseButtonDown(1))
                // {
                //     AddFileOfPower(new Vector2(x,y),mainTex.GetPixel(x,y));
                // }
                // // 
            }
            
        }

    }
    public void SendTroops(string origin, string target, string owner)
    {
        GameObject potato = Resources.Load<GameObject>("Prefabs/Map_Farmer");
        GameObject tomato = Instantiate(potato, transform.GetChild(2));//
        Vector2 location = Owners.Instance.provincelist.Find(x => x.name == origin).position;
        location = new Vector2(location.x-366,location.y-218);
        tomato.transform.position = location;
        location = Owners.Instance.provincelist.Find(x => x.name == target).position;
        location = new Vector2(location.x-366,location.y-218);
        tomato.GetComponent<ArmyMovement>().target = location;
        tomato.GetComponent<ArmyMovement>().province = target;
        tomato.GetComponent<ArmyMovement>().nation = owner;
        tomato.GetComponent<ArmyMovement>().troops = Owners.Instance.provincelist.Find(x => x.name == origin).troops/2;
        Owners.Instance.provincelist.Find(x => x.name == origin).troops -= Owners.Instance.provincelist.Find(x => x.name == origin).troops/2;
        
    }
    public void ChangeProvinceOwner(string province, string owner)
    {
        Owners.Instance.provincelist.Find(x => x.name == province).nation = Owners.Instance.nationlist.Find(x => x.name == owner);
        RePaint();
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
        if(population >= 2500)
        {
            return new Color32(0,255,33,1);
        }
        if(population >= 2000)
        {
            return new Color32(76,255,0,1);
        }
        if(population >= 1500)
        {
            return new Color32(182,255,0,1);
        }
        if(population >= 1000)
        {
            return new Color32(255,216,0,1);
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
