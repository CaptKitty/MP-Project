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

    public static ToolTipManager _instance;
    // public static ToolTipManager _instance2;
    // public static ToolTipManager _instance3;


    private void Awake()
    {
        try
        {
            Imagebasesize = this.transform.GetChild(0).GetComponent<RectTransform>().sizeDelta;
            Textbasesize = this.transform.GetChild(0).GetChild(0).GetComponent<RectTransform>().sizeDelta; 
        }
        catch{}

        OnEnable();
        return;
    }
    void SetSize()
    {
        try
        {
            this.transform.GetChild(0).GetComponent<RectTransform>().sizeDelta = Imagebasesize;
            this.transform.GetChild(0).GetChild(0).GetComponent<RectTransform>().sizeDelta = Textbasesize;
        }
        catch{}
    }
    // Start is called before the first frame update
    void Start()
    {
        // Awake();
        // Cursor.visible = true;
        GetComponent<Canvas>().enabled = !GetComponent<Canvas>().enabled;
        // gameObject.SetActive(false);
    }
    void OnEnable()
    {
        _instance = this;
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
    }

    public void SetAndShowToolTip(string message, Vector3 position)
    {
        SetSize();
        gameObject.SetActive(true);
        GetComponent<Canvas>().enabled = true;

        transform.GetChild(0).transform.localPosition = position;//Input.mousePosition;
        if(textComponent != null && textComponent.text != null)
        {
            textComponent.text = message;
        }
        if(text != null && text.text != null)
        {
            text.text = message;;
        }
    }

    // public void SetAndShowToolTip(string message, Vector3 position, bool potato)
    // {
    //     gameObject.SetActive(true);

    //     transform.position = position;//Input.mousePosition;
    //     textComponent.text = message;
    // }

    public void SetAndShowToolTip(string message, Vector3 position, bool potato, Vector2 size = new Vector2())
    {
        SetSize();
        gameObject.SetActive(true);
        GetComponent<Canvas>().enabled = true;

        transform.position = position;
        if(textComponent != null)
        {
            textComponent.text = message;
        }
        if(text != null)
        {
            text.text = message;
        }

        if(size.x != 0)
        {
            this.transform.GetChild(0).GetComponent<RectTransform>().sizeDelta = size;
            this.transform.GetChild(0).GetChild(0).GetComponent<RectTransform>().sizeDelta = new Vector2(size.x*0.8f, size.y*0.8f);
        }
        
        // text.text = message;
        //this.transform.GetChild(1).GetChild(0).GetComponent<Text>().text = message;
    }

    public void HideToolTip()
    {
        // gameObject.SetActive(false);
        try
        {
            GetComponent<Canvas>().enabled = false;
            textComponent.text = string.Empty;
        }
        catch{}

    }
}