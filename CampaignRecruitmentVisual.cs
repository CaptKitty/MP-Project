using System.Collections.Generic;
using UnityEngine;

/// <summary>Presentation-only map reinforcement. It has no collider, input or campaign state.</summary>
public sealed class CampaignRecruitmentVisual : MonoBehaviour
{
    private const float TravelSeconds = 1.75f;
    private static readonly List<CampaignRecruitmentVisual> active = new List<CampaignRecruitmentVisual>();
    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private FieldArmyHolder target;
    private Vector3 start;
    private Vector3 referenceScale;
    private float elapsed;
    private Material visualMaterial;

    public static void Present(UnitSaveData unit, FieldArmyHolder army, string sourceNationName = null, bool broadcast = true)
    {
        if (unit == null || army == null || !army.gameObject.activeInHierarchy) return;
        SpawnLocal(unit, army, sourceNationName);
        if (broadcast && CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsServer)
            CampaignNetworkPlayer.Local.BroadcastRecruitmentVisual(army.NetworkArmyId, unit.name, sourceNationName);
    }

    public static void SpawnLocal(UnitSaveData unit, FieldArmyHolder army, string sourceNationName = null)
    {
        if (unit == null || army == null || Owners.Instance == null) return;
        // A very large simultaneous levy call-up remains legible without leaving hundreds
        // of short-lived renderers behind on low-memory WebGL clients.
        active.RemoveAll(item => item == null);
        while (active.Count >= 80)
        {
            CampaignRecruitmentVisual oldest = active[0];
            active.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }

        Province current = army.GrabNearestProvince();
        List<Province> sources = current != null
            ? current.GetOccupiedRegionProvinces(army.fieldArmy != null ? army.fieldArmy.nation : null)
            : new List<Province>();
        Province source = sources.Count > 0 ? sources[Random.Range(0, sources.Count)] : current;
        Vector3 sourcePosition = source != null
            ? new Vector3(source.position.x - army.offset.x, source.position.y - army.offset.y, army.transform.position.z - .05f)
            : army.transform.position + (Vector3)Random.insideUnitCircle * 2f;
        sourcePosition += (Vector3)Random.insideUnitCircle * .35f;

        GameObject root = new GameObject("Recruitment Visual - " + unit.name);
        CampaignRecruitmentVisual effect = root.AddComponent<CampaignRecruitmentVisual>();
        effect.target = army; effect.start = sourcePosition; effect.referenceScale = army.transform.lossyScale;
        root.transform.position = sourcePosition;
        effect.BuildArt(unit, army, sourceNationName);
        active.Add(effect);
    }

    private void BuildArt(UnitSaveData unit, FieldArmyHolder army, string sourceNationName)
    {
        int count = Mathf.Min(3, unit.bodyparts != null ? unit.bodyparts.Count : 0);
        for (int i = 0; i < count; i++)
        {
            GameObject layer = new GameObject("Bodypart " + (i + 1));
            layer.transform.SetParent(transform, false);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = unit.bodyparts[i];
            renderer.drawMode = SpriteDrawMode.Sliced;
            if (i < army.transform.childCount)
            {
                SpriteRenderer reference = army.transform.GetChild(i).GetComponent<SpriteRenderer>();
                if (reference != null)
                {
                    renderer.sharedMaterial = reference.sharedMaterial;
                    renderer.sortingLayerID = reference.sortingLayerID;
                    renderer.sortingOrder = reference.sortingOrder + 2;
                    renderer.size = reference.size;
                    layer.transform.localPosition = army.transform.GetChild(i).localPosition;
                    layer.transform.localRotation = army.transform.GetChild(i).localRotation;
                    layer.transform.localScale = army.transform.GetChild(i).localScale;
                }
            }
            renderers.Add(renderer);
        }
        if (!string.IsNullOrWhiteSpace(sourceNationName) && Owners.Instance != null)
        {
            Nation sourceNation = Owners.Instance.nationlist.Find(candidate => candidate != null &&
                string.Equals(candidate.name, sourceNationName, System.StringComparison.OrdinalIgnoreCase));
            Material reference = renderers.Count > 0 ? renderers[0].sharedMaterial : null;
            if (sourceNation != null && sourceNation.faction != null && reference != null)
            {
                visualMaterial = new Material(reference);
                if (visualMaterial.HasProperty("_FactionColor3"))
                    visualMaterial.SetColor("_FactionColor3", sourceNation.faction.color3);
                foreach (SpriteRenderer renderer in renderers) if (renderer != null) renderer.sharedMaterial = visualMaterial;
            }
        }
    }

    private void Update()
    {
        if (target == null) { Destroy(gameObject); return; }
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / TravelSeconds);
        float smooth = progress * progress * (3f - 2f * progress);
        Vector3 destination = target.transform.position + new Vector3(0f, .12f, -.05f);
        transform.position = Vector3.Lerp(start, destination, smooth) + Vector3.up * Mathf.Sin(progress * Mathf.PI) * .2f;
        transform.localScale = referenceScale * Mathf.Lerp(.1f, .5f, smooth);
        float alpha = progress < .78f ? 1f : 1f - Mathf.InverseLerp(.78f, 1f, progress);
        foreach (SpriteRenderer renderer in renderers)
            if (renderer != null) { Color color = renderer.color; color.a = alpha; renderer.color = color; }
        if (progress >= 1f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        active.Remove(this);
        if (visualMaterial != null) Destroy(visualMaterial);
    }
}
