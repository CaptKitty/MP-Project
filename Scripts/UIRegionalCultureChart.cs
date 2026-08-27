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
            if (province == null || province.holdings == null) continue;
            foreach (ProvinceHolding holding in province.holdings)
            {
                if (holding == null || string.IsNullOrWhiteSpace(holding.cultureName)) continue;
                totals.TryGetValue(holding.cultureName, out Slice slice);
                slice.name = holding.cultureName;
                slice.population++;
                Color32 cultureColor = Owners.Instance != null
                    ? Owners.Instance.CultureColor(holding.cultureName, StableCultureColor(holding.cultureName))
                    : StableCultureColor(holding.cultureName);
                cultureColor.a = 255;
                slice.color = cultureColor;
                totals[holding.cultureName] = slice;
            }
        }
        slices.Clear();
        slices.AddRange(totals.Values.OrderByDescending(slice => slice.population).ThenBy(slice => slice.name));
        EnsureDistinctSliceColors();
        RefreshLegend(region);
        SetVerticesDirty();
    }

    private void EnsureDistinctSliceColors()
    {
        HashSet<uint> used = new HashSet<uint>();
        for (int i = 0; i < slices.Count; i++)
        {
            Slice slice = slices[i];
            uint packed = ((uint)slice.color.r << 16) | ((uint)slice.color.g << 8) | slice.color.b;
            if (used.Contains(packed) || (slice.color.r < 8 && slice.color.g < 8 && slice.color.b < 8))
            {
                slice.color = StableCultureColor(slice.name);
                packed = ((uint)slice.color.r << 16) | ((uint)slice.color.g << 8) | slice.color.b;
                int attempt = 1;
                while (used.Contains(packed))
                {
                    slice.color = PaletteColor(i + attempt++);
                    packed = ((uint)slice.color.r << 16) | ((uint)slice.color.g << 8) | slice.color.b;
                }
                slices[i] = slice;
            }
            used.Add(packed);
        }
    }

    private static Color32 StableCultureColor(string cultureName)
    {
        unchecked
        {
            uint hash = 2166136261;
            string value = cultureName ?? "Unassigned";
            for (int i = 0; i < value.Length; i++) hash = (hash ^ char.ToUpperInvariant(value[i])) * 16777619;
            return PaletteColor((int)(hash % 12));
        }
    }

    private static Color32 PaletteColor(int index)
    {
        Color32[] palette =
        {
            new Color32(202, 69, 55, 255), new Color32(54, 120, 190, 255),
            new Color32(225, 157, 44, 255), new Color32(95, 166, 82, 255),
            new Color32(137, 83, 177, 255), new Color32(42, 164, 157, 255),
            new Color32(205, 91, 147, 255), new Color32(139, 103, 65, 255),
            new Color32(109, 142, 203, 255), new Color32(178, 174, 54, 255),
            new Color32(227, 119, 65, 255), new Color32(100, 100, 100, 255)
        };
        return palette[Mathf.Abs(index) % palette.Length];
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
        List<string> lines = new List<string> { title + " Holding cultures (" + total + " Holdings)" };
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
