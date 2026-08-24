using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UIRegionalLoyaltyDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Text loyaltyCounter;
    private GameObject tooltipRoot;
    private Text tooltipText;
    private CampaignRegion loadedRegion;

    private void Awake()
    {
        Image background = GetComponent<Image>();
        if (background != null) background.raycastTarget = true;
        EnsureVisuals();
    }

    public void LoadRegion(CampaignRegion region)
    {
        loadedRegion = region;
        EnsureVisuals();
        float loyalty = region != null ? Mathf.Clamp(region.loyalty, 0f, 100f) : 0f;
        loyaltyCounter.text = loyalty.ToString("0.#") + "%";
        tooltipText.text = BuildInfluenceBreakdown(region, loyalty);
    }

    private static string BuildInfluenceBreakdown(CampaignRegion region, float loyalty)
    {
        string regionName = region != null && !string.IsNullOrWhiteSpace(region.name) ? region.name : "No region";
        List<string> lines = new List<string>
        {
            regionName + " loyalty",
            "Current loyalty: " + loyalty.ToString("0.#") + "%"
        };
        if (region != null) lines.AddRange(region.LoyaltyInfluenceLines(region.ControllingNation()));
        lines.Add("Provincial income: " + loyalty.ToString("0.#") + "% of normal");
        lines.Add(loyalty > 50f ? "Levies: available" : "Levies: unavailable (requires >50%)");
        if (loyalty < 25f) lines.Add("Raised levies: repatriating");
        return string.Join("\n", lines);
    }

    private void EnsureVisuals()
    {
        if (loyaltyCounter == null)
        {
            Transform existing = transform.Find("LoyaltyCounter");
            if (existing != null) loyaltyCounter = existing.GetComponent<Text>();
            if (loyaltyCounter == null)
            {
                GameObject counterObject = new GameObject("LoyaltyCounter", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                counterObject.layer = gameObject.layer;
                counterObject.transform.SetParent(transform, false);
                loyaltyCounter = counterObject.GetComponent<Text>();
                loyaltyCounter.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                loyaltyCounter.fontSize = 18;
                loyaltyCounter.fontStyle = FontStyle.Bold;
                loyaltyCounter.color = Color.white;
                loyaltyCounter.alignment = TextAnchor.MiddleCenter;
                loyaltyCounter.raycastTarget = false;
                RectTransform counterRect = loyaltyCounter.rectTransform;
                counterRect.anchorMin = Vector2.zero;
                counterRect.anchorMax = Vector2.one;
                counterRect.offsetMin = new Vector2(4f, 2f);
                counterRect.offsetMax = new Vector2(-4f, -2f);
            }
        }

        if (tooltipRoot != null && tooltipText != null) return;
        Transform existingTooltip = transform.Find("LoyaltyTooltip");
        if (existingTooltip != null)
        {
            tooltipRoot = existingTooltip.gameObject;
            tooltipText = existingTooltip.GetComponentInChildren<Text>(true);
            return;
        }

        tooltipRoot = new GameObject("LoyaltyTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipRoot.layer = gameObject.layer;
        tooltipRoot.transform.SetParent(transform, false);
        RectTransform tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(1f, .5f);
        tooltipRect.pivot = new Vector2(0f, .5f);
        tooltipRect.anchoredPosition = new Vector2(8f, 0f);
        tooltipRect.sizeDelta = new Vector2(260f, 205f);
        Image background = tooltipRoot.GetComponent<Image>();
        background.color = new Color(.06f, .06f, .06f, .96f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("LoyaltyInfluences", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(tooltipRoot.transform, false);
        tooltipText = textObject.GetComponent<Text>();
        tooltipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tooltipText.fontSize = 12;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAnchor.MiddleLeft;
        tooltipText.raycastTarget = false;
        RectTransform textRect = tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 7f);
        textRect.offsetMax = new Vector2(-10f, -7f);
        tooltipRoot.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureVisuals();
        LoadRegion(loadedRegion);
        tooltipRoot.SetActive(true);
        tooltipRoot.transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }
}
