using System.Collections;
using System.Collections.Generic;
using UnityEngine; 

[System.Serializable]
[CreateAssetMenu(fileName = "Effects/SpawnTroops")]
public class SpawnTroopEffect : BaseEffect
{
    public int TroopsToSpawn = 1;
    public string province = "";
    public override void Execute()
    {
        if(province != "")
        {
            Owners.Instance.provincelist.Find(x => x.name == province).AddTroops(TroopsToSpawn);
        }
    }
}
