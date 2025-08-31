using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainExplainer : MonoBehaviour
{
    public static TerrainExplainer Instance;
    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
        this.enabled = false;
    }
    public void DisEnable()
    {
        gameObject.SetActive(!this.enabled);
        this.enabled = !this.enabled;
    }
    public void Enable()
    {
        gameObject.SetActive(true);
        this.enabled = true;
    }
    public void Disable()
    {
        gameObject.SetActive(false);
        this.enabled = false;
    }
}
