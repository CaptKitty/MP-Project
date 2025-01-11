using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/Base")]
public class BaseTrigger : ScriptableObject
{
    public string triggerdescription;
    public virtual bool CanTrigger()
    {
        return true;
    }
}
