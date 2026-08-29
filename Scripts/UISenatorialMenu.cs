using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UISenatorialMenu : MonoBehaviour
{
    private GameObject opener;
    private RectTransform lawTemplate;
    private readonly List<GameObject> generatedRows = new List<GameObject>();

    public void Configure(GameObject openControl)
    {
        opener = openControl;
        lawTemplate = FindDescendant(transform, "LawsHolderObject") as RectTransform;
        if (lawTemplate == null) lawTemplate = FindDescendant(transform, "LawHolder") as RectTransform;
        if (lawTemplate != null) lawTemplate.gameObject.SetActive(false);

        if (opener != null)
            foreach (Button button in opener.GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveListener(Open);
                button.onClick.AddListener(Open);
            }

        Transform close = FindDescendant(transform, "CloseSenateButton");
        if (close != null && close.TryGetComponent(out Button closeButton))
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnEnable()
    {
        if (lawTemplate != null) RebuildLawRows();
    }

    public void Open()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        else RebuildLawRows();
    }

    public void Close() => gameObject.SetActive(false);

    private void RebuildLawRows()
    {
        if (lawTemplate == null) return;
        foreach (GameObject row in generatedRows)
            if (row != null) { row.SetActive(false); Destroy(row); }
        generatedRows.Clear();

        Nation nation = LocalNation();
        if (nation != null) nation.EnsureDefaultLaws();
        Transform titleObject = FindDescendant(transform, "SenateName");
        if (titleObject != null && titleObject.TryGetComponent(out Text title))
            title.text = NationContentResolver.ResolveAssemblyName(nation);
        List<NationalLaw> activeLaws = nation != null && nation.laws != null
            ? nation.laws.FindAll(law => law != null && law.effects != null &&
                (law.effects.Exists(effect => effect != null && effect.amountPermille != 0) ||
                 law.classRules != null && law.classRules.Exists(rule => rule != null)))
            : new List<NationalLaw>();

        List<PoliticalProposal> proposals = nation != null && nation.politicalProposals != null
            ? nation.politicalProposals.FindAll(proposal => proposal != null) : new List<PoliticalProposal>();

        int rowCount = Mathf.Max(1, activeLaws.Count + proposals.Count);
        float spacing = Mathf.Max(10f, lawTemplate.rect.height + 10f);
        for (int i = 0; i < rowCount; i++)
        {
            RectTransform row = Instantiate(lawTemplate, lawTemplate.parent);
            bool activeLaw = i < activeLaws.Count;
            int proposalIndex = i - activeLaws.Count;
            row.name = activeLaw ? "Law_" + activeLaws[i].id : proposalIndex >= 0 && proposalIndex < proposals.Count
                ? "Proposal_" + proposals[proposalIndex].id : "Law_None";
            row.anchoredPosition = lawTemplate.anchoredPosition + Vector2.down * spacing * i;
            row.gameObject.SetActive(true);
            Text label = row.GetComponentInChildren<Text>(true);
            if (label != null) label.text = activeLaw ? activeLaws[i].DescribeWithName() :
                proposalIndex >= 0 && proposalIndex < proposals.Count
                    ? "PROPOSAL: " + proposals[proposalIndex].title + " — vote in " +
                        proposals[proposalIndex].remainingDebateTicks + " turns"
                    : "No active national laws.";
            generatedRows.Add(row.gameObject);
        }
    }

    private static Nation LocalNation()
    {
        if (Owners.Instance == null || Owners.Instance.nationlist == null) return null;
        string nationName = CampaignNetworkPlayer.Local != null ? CampaignNetworkPlayer.Local.AssignedNation : string.Empty;
        if (string.IsNullOrEmpty(nationName) && SessionManager.Instance != null && SessionManager.Instance.HostFaction != null)
            nationName = SessionManager.Instance.HostFaction.name;
        return !string.IsNullOrEmpty(nationName)
            ? Owners.Instance.nationlist.Find(nation => nation != null && nation.name == nationName)
            : null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child;
        return null;
    }
}

public static class UISenatorialMenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameObject opener = FindSceneObject(scene, "OpenSenate");
        if (opener == null) return;
        GameObject menu = FindSceneObject(scene, "SenatorialMenu");
        if (menu == null)
        {
            GameObject laws = FindSceneObject(scene, "Laws");
            if (laws != null && laws.transform.parent != null) menu = laws.transform.parent.gameObject;
        }
        if (menu == null) return;
        UISenatorialMenu controller = menu.GetComponent<UISenatorialMenu>();
        if (controller == null) controller = menu.AddComponent<UISenatorialMenu>();
        controller.Configure(opener);
        menu.SetActive(false);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            if (candidate != null && candidate.scene == scene && candidate.name == objectName) return candidate;
        return null;
    }
}
