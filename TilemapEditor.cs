using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapEditor : MonoBehaviour
{
    public Tilemap inputImage;
    public Tilemap outputImage;
    public List<Inputs> ComboList = new List<Inputs>();
    
    public void GrabInputs()
    {
        ComboList.Clear();
        foreach (Vector3Int position in inputImage.cellBounds.allPositionsWithin)
        {
            Inputs potato = new Inputs();
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    if(inputImage.GetTile(new Vector3Int(position.x + i, position.y + j, 0)) != null)
                    {
                        potato.Tilearray.Add(inputImage.GetTile(new Vector3Int(position.x + i, position.y + j, 0)));
                    }
                }
            }
            if(potato.Tilearray.Count == 4)
            {
                bool canaddpotato = true;
                foreach (var item in ComboList)
                {
                    if(potato.Tilearray[0] == item.Tilearray[0] && potato.Tilearray[1] == item.Tilearray[1] && potato.Tilearray[2] == item.Tilearray[2] && potato.Tilearray[3] == item.Tilearray[3])
                    {
                        canaddpotato = false;
                    }
                }
                if(canaddpotato)
                {
                    ComboList.Add(potato);
                }
            }
        }
    }

    public void GenerateTerrain()
    {
        foreach (Vector3Int position in inputImage.cellBounds.allPositionsWithin)
        {
            Inputs potato = new Inputs();
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
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
                if(potato.Tilearray.Count < 4)
                {
                    continue;
                }
                if(potato.Tilearray[0] == item.Tilearray[0] || potato.Tilearray[0].name == "_Water")
                {
                    if(potato.Tilearray[1] == item.Tilearray[1] || potato.Tilearray[1].name == "_Water")
                    {
                        if(potato.Tilearray[2] == item.Tilearray[2] || potato.Tilearray[2].name == "_Water")
                        {
                            ViableComboList.Add(item);
                        }
                    }
                }
            }
            if(ViableComboList.Count > 0)
            {
                Inputs a = ViableComboList[Random.Range(0,ViableComboList.Count)];
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        //Debug.LogError(i*2+j);
                        outputImage.SetTile(new Vector3Int(position.x + i, position.y + j, 0), a.Tilearray[i*2+j]);
                    }
                }
            }
            
        }
    }
}
[System.Serializable]
public class Inputs
{
    public List<TileBase> Tilearray = new List<TileBase>();
}