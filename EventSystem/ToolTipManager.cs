using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class ToolTipManager : MonoBehaviour
{

    public TextMeshProUGUI textComponent;
    public Text text;
    public Vector2 Imagebasesize;
    public Vector2 Textbasesize;
    [Tooltip("Additional space between tooltip text and the sprite's 9-slice borders.")]
    public Vector2 contentMargin = new Vector2(12f, 12f);
    private float baseTmpFontSize;
    private int baseTextFontSize;
    private bool baseTextBestFit;

    public static ToolTipManager _instance;
    // public static ToolTipManager _instance2;
    // public static ToolTipManager _instance3;


    private void Awake()
    {
        try
        {
            Imagebasesize = this.transform.GetChild(0).GetComponent<RectTransform>().sizeDelta;
            Textbasesize = this.transform.GetChild(0).GetChild(0).GetComponent<RectTransform>().sizeDelta; 
            if (textComponent != null) baseTmpFontSize = textComponent.fontSize;
            if (text != null) baseTextFontSize = text.fontSize;
            if (text != null) baseTextBestFit = text.resizeTextForBestFit;
        }
        catch{}

        OnEnable();
        Startie();
        return;
    }
    void SetSize()
    {
        try
        {
            this.transform.GetChild(0).GetComponent<RectTransform>().sizeDelta = Imagebasesize;
            this.transform.GetChild(0).GetChild(0).GetComponent<RectTransform>().sizeDelta = Textbasesize;
            if (textComponent != null && baseTmpFontSize > 0f) textComponent.fontSize = baseTmpFontSize;
            if (text != null && baseTextFontSize > 0) text.fontSize = baseTextFontSize;
            if (text != null) text.resizeTextForBestFit = baseTextBestFit;
        }
        catch{}
    }
    // Start is called before the first frame update
    void Startie()
    {
        // Awake();
        // Cursor.visible = true;
        GetComponent<Canvas>().enabled = false;//!GetComponent<Canvas>().enabled;
        // gameObject.SetActive(false);
    }
    public void OnEnable()
    {
        _instance = this;
        try
        {
            try
            {
                transform.GetChild(0).GetChild(1).GetComponent<Image>().sprite = SessionManager.Instance.HostFaction.factionTheme.TooltipBird;
            }
            catch
            {
                transform.GetChild(0).GetChild(1).GetComponent<Image>().sprite = Owners.Instance.CallPlayer().faction.factionTheme.TooltipBird;
            }
        }
        catch
        {
            print("no bird available");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // transform.position = Input.mousePosition;
    }

    public void SetAndShowToolTip(string message)
    {
        //print(gameObject.name);
        gameObject.SetActive(true);
        SetSize();

        transform.position = Input.mousePosition;
        if(textComponent.text != null)
        {
            textComponent.text = message;
        }
        if(text != null && text.text != null)
        {
            text.text = message;;
        }
        ApplyContentLayout(message, Vector2.zero);
    }

    public void SetAndShowToolTip(string message, Vector3 position)
    {
        SetSize();
        gameObject.SetActive(true);
        GetComponent<Canvas>().enabled = true;

        transform.GetChild(0).transform.localPosition = position;//Input.mousePosition;
        if (textComponent != null && textComponent.text != null)
        {
            textComponent.text = message;
        }
        if (text != null && text.text != null)
        {
            text.text = message; ;
        }
        ApplyContentLayout(message, Vector2.zero);
        if (transform.GetChild(0).position.x > 1600)
        {
            transform.GetChild(0).transform.localPosition = new Vector3(position.x - 300, position.y, position.z);
        }
    }

    // public void SetAndShowToolTip(string message, Vector3 position, bool potato)
    // {
    //     gameObject.SetActive(true);

    //     transform.position = position;//Input.mousePosition;
    //     textComponent.text = message;
    // }

    public void SetAndShowToolTip(string message, Vector3 position, bool potato = false,
        Vector2 size = new Vector2(), int requestedFontSize = 0)
    {
        SetSize();
        gameObject.SetActive(true);
        GetComponent<Canvas>().enabled = true;

        //transform.position = position;
        this.transform.GetChild(0).position = position;
        if(textComponent != null)
        {
            textComponent.text = message;
        }
        if(text != null)
        {
            text.text = message;
        }
        if (transform.GetChild(0).position.x > 1600)
        {
            transform.GetChild(0).position = new Vector3(position.x - 400, position.y, position.z);
        }
        if (requestedFontSize > 0)
        {
            if (textComponent != null) textComponent.fontSize = requestedFontSize;
            if (text != null) { text.fontSize = requestedFontSize; text.resizeTextForBestFit = false; }
        }
        ApplyContentLayout(message, size);
        
        // text.text = message;
        //this.transform.GetChild(1).GetChild(0).GetComponent<Text>().text = message;
    }

    private void ApplyContentLayout(string message, Vector2 requestedMaximum)
    {
        if (transform.childCount == 0) return;
        RectTransform background = transform.GetChild(0) as RectTransform;
        if (background == null || background.childCount == 0) return;
        RectTransform textRect = background.GetChild(0) as RectTransform;
        if (textRect == null) return;

        Vector4 border = GetBackgroundBorderInUiUnits(background);
        float horizontalPadding = border.x + border.z + contentMargin.x * 2f;
        float verticalPadding = border.y + border.w + contentMargin.y * 2f;
        float maximumWidth = requestedMaximum.x > 0f ? requestedMaximum.x : 900f;
        float maximumHeight = requestedMaximum.y > 0f ? requestedMaximum.y : 700f;
        float borderMinimumWidth = horizontalPadding + 100f;
        float borderMinimumHeight = verticalPadding + 50f;
        float minimumWidth = Mathf.Max(borderMinimumWidth,
            Imagebasesize.x > 0f ? Mathf.Min(Imagebasesize.x, maximumWidth) : 280f);
        float minimumHeight = Mathf.Max(borderMinimumHeight,
            Imagebasesize.y > 0f ? Mathf.Min(Imagebasesize.y, maximumHeight) : 100f);
        maximumWidth = Mathf.Max(maximumWidth, minimumWidth);
        maximumHeight = Mathf.Max(maximumHeight, minimumHeight);
        float availableMaximumWidth = Mathf.Max(100f, maximumWidth - horizontalPadding);

        Vector2 preferred = Vector2.zero;
        if (textComponent != null)
            preferred = textComponent.GetPreferredValues(message ?? string.Empty, availableMaximumWidth, 0f);
        else if (text != null)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(availableMaximumWidth, maximumHeight));
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            TextGenerator generator = text.cachedTextGeneratorForLayout;
            preferred.y = generator.GetPreferredHeight(message ?? string.Empty, settings) / text.pixelsPerUnit;
            float widestLine = 0f;
            string[] lines = (message ?? string.Empty).Split('\n');
            TextGenerationSettings widthSettings = text.GetGenerationSettings(new Vector2(maximumWidth * 4f, maximumHeight));
            widthSettings.resizeTextForBestFit = false;
            widthSettings.horizontalOverflow = HorizontalWrapMode.Overflow;
            foreach (string line in lines)
                widestLine = Mathf.Max(widestLine, generator.GetPreferredWidth(line, widthSettings) / text.pixelsPerUnit);
            preferred.x = Mathf.Min(widestLine, availableMaximumWidth);
        }

        float panelWidth = Mathf.Clamp(preferred.x + horizontalPadding, minimumWidth, maximumWidth);
        float textWidth = Mathf.Max(100f, panelWidth - horizontalPadding);
        if (text != null)
        {
            TextGenerationSettings heightSettings = text.GetGenerationSettings(new Vector2(textWidth, maximumHeight));
            heightSettings.resizeTextForBestFit = false;
            heightSettings.horizontalOverflow = HorizontalWrapMode.Wrap;
            heightSettings.verticalOverflow = VerticalWrapMode.Overflow;
            preferred.y = text.cachedTextGeneratorForLayout.GetPreferredHeight(message ?? string.Empty,
                heightSettings) / text.pixelsPerUnit;
        }
        else if (textComponent != null)
            preferred.y = textComponent.GetPreferredValues(message ?? string.Empty, textWidth, 0f).y;

        float panelHeight = Mathf.Clamp(preferred.y + verticalPadding, minimumHeight, maximumHeight);
        background.sizeDelta = new Vector2(panelWidth, panelHeight);
        textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, .5f);
        textRect.pivot = new Vector2(.5f, .5f);
        // Centre the text in the sprite's inner (stretchable) rectangle, rather than
        // in the full image. This matters for frames with asymmetric decoration.
        textRect.anchoredPosition = new Vector2(
            (border.x - border.z) * .5f,
            (border.y - border.w) * .5f);
        textRect.sizeDelta = new Vector2(textWidth, Mathf.Max(50f, panelHeight - verticalPadding));

        // The decorative icon is the second child. Its centre sits on the panel's top edge.
        if (background.childCount > 1 && background.GetChild(1) is RectTransform icon)
        {
            icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f);
            icon.pivot = new Vector2(.5f, .5f);
            icon.anchoredPosition = new Vector2(0f, panelHeight * .5f + 50f);
        }
    }

    private static Vector4 GetBackgroundBorderInUiUnits(RectTransform background)
    {
        Image image = background.GetComponent<Image>();
        if (image == null || image.sprite == null)
            return Vector4.zero;

        Vector4 spriteBorder = image.sprite.border;
        float pixelsPerUnit = image.pixelsPerUnit;
        if (pixelsPerUnit <= 0f)
            pixelsPerUnit = 1f;

        return spriteBorder / pixelsPerUnit;
    }

    public void HideToolTip()
    {
        // gameObject.SetActive(false);
        try
        {
            GetComponent<Canvas>().enabled = false;
            //textComponent.text = string.Empty;
        }
        catch{}

    }
}
