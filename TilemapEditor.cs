using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapEditor : MonoBehaviour
{
    public Tilemap inputImage;
    public Tilemap outputImage;
    public TileBase basetile;
    public int MaxNumberOfTries = 1;
    public List<Inputs> ComboList = new List<Inputs>();
    
    public void GrabInputs()
    {
        ComboList.Clear();
        foreach (Vector3Int position in inputImage.cellBounds.allPositionsWithin)
        {
            Inputs potato = new Inputs();
            
            for (int j = -1; j < 2; j++)
            {
                for (int i = -1; i < 2; i++)
                {
                    if(inputImage.GetTile(new Vector3Int(position.x + i, position.y + j, 0)) != null)
                    {
                        potato.Tilearray.Add(inputImage.GetTile(new Vector3Int(position.x + i, position.y + j, 0)));
                    }
                }
            }
            if(potato.Tilearray.Count == 9)
            {
                // bool canaddpotato = true;
                // int numberofequals = 0;
                // foreach (var item in ComboList)
                // {
                //     //if(potato.Tilearray[0] == item.Tilearray[0] && potato.Tilearray[1] == item.Tilearray[1] && potato.Tilearray[2] == item.Tilearray[2] && potato.Tilearray[3] == item.Tilearray[3])
                //     for (int i = 0; i < 9; i++)
                //     {
                //         if(potato.Tilearray[i] == item.Tilearray[i])
                //         {
                //             numberofequals++;
                //             canaddpotato = false;
                //         }
                //     }
                // }
                // if(numberofequals != 9)//canaddpotato)
                // {
                    ComboList.Add(potato);
                //}
            }
        }
    }
    public void GenerateTerrain()
    {
        for (int i = 0; i < MaxNumberOfTries; i++)
        {
            
            if(GenerateTerrains())
            {
                return;
            }
        }
    }
    public void Update()
    {
        if(Input.GetKeyDown("k"))
        {
            GenerateTerrain();
        }
    }

    public bool GenerateTerrains()
    {
        Debug.Log("Potato");
        foreach (Vector3Int position in inputImage.cellBounds.allPositionsWithin)
        {
            outputImage.SetTile(position, basetile);
        }
        foreach (Vector3Int position in inputImage.cellBounds.allPositionsWithin)
        {
            Inputs potato = new Inputs();
            for (int j = -1; j < 2; j++)
            {
                for (int i = -1; i < 2; i++)
                {
                    if(outputImage.GetTile(new Vector3Int(position.x + i, position.y + j, 0)) != null)
                    {
                        potato.Tilearray.Add(outputImage.GetTile(new Vector3Int(position.x + i, position.y + j, 0)));
                    }
                }
            }
            List<Inputs> ViableComboList = new List<Inputs>();
            foreach (var item in ComboList)
            {
                if(potato.Tilearray.Count < 9)
                {
                    continue;
                }
                if(potato.Tilearray[0] == item.Tilearray[0] || potato.Tilearray[0].name == "_Water")
                {
                    if(potato.Tilearray[1] == item.Tilearray[1] || potato.Tilearray[1].name == "_Water")
                    {
                        if(potato.Tilearray[2] == item.Tilearray[2] || potato.Tilearray[2].name == "_Water")
                        {
                            if(potato.Tilearray[3] == item.Tilearray[3] || potato.Tilearray[3].name == "_Water")
                            {
                                if(potato.Tilearray[4] == item.Tilearray[4] || potato.Tilearray[4].name == "_Water")
                                {
                                    if(potato.Tilearray[5] == item.Tilearray[5] || potato.Tilearray[5].name == "_Water")
                                    {
                                        if(potato.Tilearray[6] == item.Tilearray[6] || potato.Tilearray[6].name == "_Water")
                                        {
                                            if(potato.Tilearray[7] == item.Tilearray[7] || potato.Tilearray[7].name == "_Water")
                                            {
                                                if(potato.Tilearray[8] == item.Tilearray[8] || potato.Tilearray[8].name == "_Water")
                                                {
                                                    ViableComboList.Add(item);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if(ViableComboList.Count > 0)
            {
                Inputs a = ViableComboList[Random.Range(0,ViableComboList.Count)];
                int x = 0;
                for (int j = -1; j < 2; j++)
                {
                    for (int i = -1; i < 2; i++)
                    {
                        //Debug.LogError(i*2+j);
                        outputImage.SetTile(new Vector3Int(position.x + i, position.y + j, 0), a.Tilearray[x]);
                        x++;
                    }
                }
            }
        }
        foreach (Vector3Int position in outputImage.cellBounds.allPositionsWithin)
        {
            if(outputImage.GetTile(position) == basetile)
            {
                Debug.Log("Potatos");
                return false;
            }
        }
        Debug.Log("Potata");
        return true;
    }
}
[System.Serializable]
public class Inputs
{
    public List<TileBase> Tilearray = new List<TileBase>();
}