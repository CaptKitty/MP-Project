using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRegionalCultureChart : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler
{
    private struct Slice
    {
        public string name;
        public Color32 color;
        public int population;
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
        Dictionary<string, Slice> totals = new Dictionary<string, Slice>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<Province> provinces = region != null && region.provincelist != null
            ? region.provincelist : new[] { fallbackProvince };
        foreach (Province province in provinces)
        {
            if (province == null || province.cultures == null) continue;
            foreach (Culture culture in province.cultures)
            {
                if (culture == null || string.IsNullOrWhiteSpace(culture.name) || culture.population <= 0) continue;
                totals.TryGetValue(culture.name, out Slice slice);
                slice.name = culture.name;
                slice.population += culture.population;
                Color32 cultureColor = culture.ownerIdentity;
                cultureColor.a = 255;
                slice.color = cultureColor;
                totals[culture.name] = slice;
            }
        }
        slices.Clear();
        slices.AddRange(totals.Values.OrderByDescending(slice => slice.population).ThenBy(slice => slice.name));
        RefreshLegend(region);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        int total = slices.Sum(slice => slice.population);
        if (total <= 0) return;
        Rect chartRect = rectTransform.rect;
        Vector2 center = chartRect.center;
        float radius = Mathf.Min(chartRect.width, chartRect.height) * .48f;
        float angle = 90f;
        foreach (Slice slice in slices)
        {
            float sweep = 360f * slice.population / total;
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
        Transform existing = transform.Find("CultureTooltip");
        if (existing != null)
        {
            tooltipRoot = existing.gameObject;
            legend = existing.GetComponentInChildren<Text>(true);
            return;
        }
        tooltipRoot = new GameObject("CultureTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipRoot.layer = gameObject.layer;
        tooltipRoot.transform.SetParent(transform, false);
        RectTransform tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(1f, .5f);
        tooltipRect.pivot = new Vector2(0f, .5f);
        tooltipRect.anchoredPosition = new Vector2(8f, 0f);
        tooltipRect.sizeDelta = new Vector2(170f, 120f);
        Image background = tooltipRoot.GetComponent<Image>();
        background.color = new Color(.06f, .06f, .06f, .96f);
        background.raycastTarget = false;

        GameObject legendObject = new GameObject("CultureLegend", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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
        legendRect.anchorMin = Vector2.zero;
        legendRect.anchorMax = Vector2.one;
        legendRect.offsetMin = new Vector2(8f, 6f);
        legendRect.offsetMax = new Vector2(-8f, -6f);
        tooltipRoot.SetActive(false);
    }

    private void RefreshLegend(CampaignRegion region)
    {
        EnsureLegend();
        int total = slices.Sum(slice => slice.population);
        string title = region != null ? region.name : "Province";
        List<string> lines = new List<string> { title + " cultures" };
        foreach (Slice slice in slices)
        {
            float percentage = total > 0 ? slice.population * 100f / total : 0f;
            lines.Add("• " + slice.name + " " + percentage.ToString("0.#") + "%");
        }
        legend.text = string.Join("\n", lines);
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
