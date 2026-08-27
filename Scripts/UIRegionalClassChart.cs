using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UIRegionalClassChart : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler
{
    private struct Slice
    {
        public SocioEconomicClass socialClass;
        public Color32 color;
        public int holdings;
    }

    private readonly List<Slice> slices = new List<Slice>();
    private GameObject tooltipRoot;
    private Text legend;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = true;
        EnsureLegend();
    }

    public void LoadRegion(CampaignRegion region, Province fallbackProvince)
    {
        Dictionary<SocioEconomicClass, int> totals = new Dictionary<SocioEconomicClass, int>();
        IEnumerable<Province> provinces = region != null && region.provincelist != null
            ? region.provincelist : new[] { fallbackProvince };
        foreach (Province province in provinces)
        {
            if (province == null) continue;
            foreach (KeyValuePair<SocioEconomicClass, int> entry in province.GetSocioEconomicComposition())
                totals[entry.Key] = totals.TryGetValue(entry.Key, out int current) ? current + entry.Value : entry.Value;
        }

        slices.Clear();
        foreach (KeyValuePair<SocioEconomicClass, int> entry in totals)
            slices.Add(new Slice { socialClass = entry.Key, holdings = entry.Value, color = ClassColor(entry.Key) });
        slices.Sort((left, right) =>
        {
            int count = right.holdings.CompareTo(left.holdings);
            return count != 0 ? count : left.socialClass.CompareTo(right.socialClass);
        });
        RefreshLegend(region);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        int total = slices.Sum(slice => slice.holdings);
        if (total <= 0) return;
        Rect chartRect = rectTransform.rect;
        Vector2 center = chartRect.center;
        float radius = Mathf.Min(chartRect.width, chartRect.height) * .48f;
        float angle = 90f;
        foreach (Slice slice in slices)
        {
            float sweep = 360f * slice.holdings / total;
            int steps = Mathf.Max(2, Mathf.CeilToInt(sweep / 8f));
            int centerIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(center, slice.color, Vector2.zero);
            for (int step = 0; step <= steps; step++)
            {
                float radians = (angle - sweep * step / steps) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
                vertexHelper.AddVert(point, slice.color, Vector2.zero);
                if (step > 0) vertexHelper.AddTriangle(centerIndex, centerIndex + step, centerIndex + step + 1);
            }
            angle -= sweep;
        }
    }

    private void EnsureLegend()
    {
        if (legend != null && tooltipRoot != null) return;
        Transform existing = transform.Find("ClassTooltip");
        if (existing != null)
        {
            tooltipRoot = existing.gameObject;
            legend = existing.GetComponentInChildren<Text>(true);
            return;
        }
        tooltipRoot = new GameObject("ClassTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipRoot.layer = gameObject.layer;
        tooltipRoot.transform.SetParent(transform, false);
        RectTransform tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(1f, .5f);
        tooltipRect.pivot = new Vector2(0f, .5f);
        tooltipRect.anchoredPosition = new Vector2(8f, 0f);
        tooltipRect.sizeDelta = new Vector2(185f, 155f);
        Image background = tooltipRoot.GetComponent<Image>();
        background.color = new Color(.06f, .06f, .06f, .96f);
        background.raycastTarget = false;

        GameObject legendObject = new GameObject("ClassLegend", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        legendObject.layer = gameObject.layer;
        legendObject.transform.SetParent(tooltipRoot.transform, false);
        legend = legendObject.GetComponent<Text>();
        legend.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        legend.fontSize = 11;
        legend.color = Color.white;
        legend.alignment = TextAnchor.MiddleLeft;
        legend.horizontalOverflow = HorizontalWrapMode.Overflow;
        legend.verticalOverflow = VerticalWrapMode.Overflow;
        legend.raycastTarget = false;
        RectTransform legendRect = legend.rectTransform;
        legendRect.anchorMin = Vector2.zero; legendRect.anchorMax = Vector2.one;
        legendRect.offsetMin = new Vector2(8f, 6f); legendRect.offsetMax = new Vector2(-8f, -6f);
        tooltipRoot.SetActive(false);
    }

    private void RefreshLegend(CampaignRegion region)
    {
        EnsureLegend();
        int total = slices.Sum(slice => slice.holdings);
        string title = region != null ? region.name : "Province";
        List<string> lines = new List<string> { title + " social classes (" + total + " Holdings)" };
        foreach (Slice slice in slices)
        {
            float percentage = total > 0 ? slice.holdings * 100f / total : 0f;
            lines.Add("• " + slice.socialClass + " " + slice.holdings + " (" + percentage.ToString("0.#") + "%)");
        }
        legend.text = string.Join("\n", lines);
    }

    private static Color32 ClassColor(SocioEconomicClass socialClass)
    {
        switch (socialClass)
        {
            case SocioEconomicClass.Subsistence: return new Color32(112, 91, 65, 255);
            case SocioEconomicClass.Laborers: return new Color32(133, 133, 133, 255);
            case SocioEconomicClass.Freemen: return new Color32(102, 153, 72, 255);
            case SocioEconomicClass.Burghers: return new Color32(218, 156, 55, 255);
            case SocioEconomicClass.Clergy: return new Color32(230, 224, 190, 255);
            case SocioEconomicClass.Aristocracy: return new Color32(132, 76, 173, 255);
            case SocioEconomicClass.Citizen: return new Color32(62, 139, 204, 255);
            case SocioEconomicClass.Elite: return new Color32(196, 62, 62, 255);
            case SocioEconomicClass.Enslaved: return new Color32(70, 70, 70, 255);
            default: return new Color32(160, 160, 160, 255);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureLegend();
        tooltipRoot.SetActive(true);
        tooltipRoot.transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }
}
