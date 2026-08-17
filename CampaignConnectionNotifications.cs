using System.Collections.Generic;
using UnityEngine;

public sealed class CampaignConnectionNotifications : MonoBehaviour
{
    private sealed class Notice
    {
        public string Text;
        public float ExpiresAt;
        public bool Warning;
    }

    private static CampaignConnectionNotifications instance;
    private readonly List<Notice> notices = new List<Notice>();
    private GUIStyle normalStyle;
    private GUIStyle warningStyle;

    public static void Show(string message, bool warning = false)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (instance == null)
        {
            GameObject root = new GameObject("Campaign Connection Notifications");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<CampaignConnectionNotifications>();
        }

        instance.notices.Add(new Notice
        {
            Text = message,
            Warning = warning,
            ExpiresAt = Time.unscaledTime + 6f
        });
        if (warning) Debug.LogWarning(message); else Debug.Log(message);
    }

    private void Update()
    {
        notices.RemoveAll(notice => notice == null || Time.unscaledTime >= notice.ExpiresAt);
    }

    private void OnGUI()
    {
        if (notices.Count == 0) return;
        EnsureStyles();
        float width = Mathf.Min(620f, Screen.width - 24f);
        float x = (Screen.width - width) * .5f;
        for (int i = 0; i < notices.Count; i++)
            GUI.Label(new Rect(x, 18f + i * 42f, width, 36f), notices[i].Text,
                notices[i].Warning ? warningStyle : normalStyle);
    }

    private void EnsureStyles()
    {
        if (normalStyle != null) return;
        normalStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        warningStyle = new GUIStyle(normalStyle);
        warningStyle.normal.textColor = new Color(1f, .72f, .35f);
    }
}
