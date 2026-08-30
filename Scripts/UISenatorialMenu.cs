using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UISenatorialMenu : MonoBehaviour
{
    private GameObject opener;
    private RectTransform lawTemplate;
    private readonly List<GameObject> generatedRows = new List<GameObject>();
    private RectTransform allegianceTemplate;
    private Text allegianceHeader;
    private readonly List<GameObject> generatedAllegiances = new List<GameObject>();
    private Text edictDetails;
    private GameObject edictVoteYes;
    private GameObject edictVoteNo;
    private GameObject edictPropose;
    private int lastRenderedTurn = int.MinValue;

    public void Configure(GameObject openControl)
    {
        opener = openControl;
        lawTemplate = FindDescendant(transform, "LawsHolderObject") as RectTransform;
        if (lawTemplate == null) lawTemplate = FindDescendant(transform, "LawHolder") as RectTransform;
        if (lawTemplate != null) lawTemplate.gameObject.SetActive(false);
        allegianceTemplate = FindDescendant(transform, "AllegianceHolderObject") as RectTransform;
        if (allegianceTemplate != null) allegianceTemplate.gameObject.SetActive(false);
        allegianceHeader = ComponentInDescendant<Text>(transform, "AllegianceHeaderName");
        edictDetails = ComponentInDescendant<Text>(transform, "EdictDetailsText");
        edictVoteYes = ObjectInDescendant(transform, "EdictVoteYes");
        edictVoteNo = ObjectInDescendant(transform, "EdictVoteNo");
        edictPropose = ObjectInDescendant(transform, "EdictPropose");
        ConfigureButton(edictVoteYes, VoteYes);
        ConfigureButton(edictVoteNo, VoteNo);
        ConfigureButton(edictPropose, ProposeEdict);

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
        RebuildAll();
    }

    private void Update()
    {
        int turn = Owners.Instance != null ? Owners.Instance.turncounter : 0;
        if (turn != lastRenderedTurn) RebuildAll();
    }

    public void Open()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        else RebuildAll();
    }

    public void Close() => gameObject.SetActive(false);

    private void RebuildAll()
    {
        lastRenderedTurn = Owners.Instance != null ? Owners.Instance.turncounter : 0;
        RebuildLawRows();
        RebuildAllegiances();
        RefreshEdicts();
    }

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

    private void RebuildAllegiances()
    {
        foreach (GameObject row in generatedAllegiances)
            if (row != null) { row.SetActive(false); Destroy(row); }
        generatedAllegiances.Clear();
        Nation nation = LocalNation();
        if (nation == null || allegianceTemplate == null) return;
        PoliticalProposalSystem.EnsureGroups(nation);
        string allegianceType = NationContentResolver.ResolveAllegianceType(nation);
        if (allegianceHeader != null) allegianceHeader.text = allegianceType;
        int groupCount = Mathf.Max(1, nation.politicalGroups.Count);
        float scale = Mathf.Clamp(5f / groupCount, .65f, 1f);
        float spacing = Mathf.Max(10f, (allegianceTemplate.rect.width + 10f) * scale);
        float firstX = groupCount <= 5 ? allegianceTemplate.anchoredPosition.x : -spacing * (groupCount - 1) * .5f;
        for (int i = 0; i < nation.politicalGroups.Count; i++)
        {
            PoliticalGroup group = nation.politicalGroups[i];
            if (group == null) continue;
            RectTransform row = Instantiate(allegianceTemplate, allegianceTemplate.parent);
            row.name = "Allegiance_" + group.id;
            row.anchoredPosition = new Vector2(firstX + spacing * i, allegianceTemplate.anchoredPosition.y);
            row.localScale = Vector3.one * scale;
            Text header = ComponentInDescendant<Text>(row, "AllegianceDataHeader");
            if (header != null) header.text = group.representsUnalignedHoldings
                ? group.displayName + ":" : allegianceType + " " + group.displayName + ":";
            Text data = ComponentInDescendant<Text>(row, "AllegianceData");
            if (data != null) data.text = "Focus: Undetermined\n\nPower: " + HoldingPower(nation, group) + " holdings";
            Image icon = ComponentInDescendant<Image>(row, "AllegianceIcon");
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            row.gameObject.SetActive(true);
            generatedAllegiances.Add(row.gameObject);
        }
    }

    private void RefreshEdicts()
    {
        Nation nation = LocalNation();
        PoliticalProposal proposal = PoliticalProposalSystem.CurrentEdict(nation);
        if (edictDetails != null)
        {
            if (proposal != null)
                edictDetails.text = proposal.title + "\n\n" + PoliticalProposalSystem.DescribeEdict(proposal.edict) +
                    "\n\nDebate remaining: " + proposal.remainingDebateTicks + " turns" +
                    (proposal.playerVoteCast ? "\nYour vote: " + (proposal.playerSupports ? "Support" : "Oppose") : string.Empty);
            else if (nation != null && !string.IsNullOrWhiteSpace(nation.latestPassedEdict))
                edictDetails.text = "Latest passed edict\n\n" + nation.latestPassedEdict;
            else edictDetails.text = "No edict under consideration";
        }
        bool canVote = proposal != null && !proposal.playerVoteCast;
        if (edictVoteYes != null) edictVoteYes.SetActive(canVote);
        if (edictVoteNo != null) edictVoteNo.SetActive(canVote);
        if (edictPropose != null) edictPropose.SetActive(proposal == null);
    }

    private void VoteYes() => Vote(true);
    private void VoteNo() => Vote(false);
    private void Vote(bool supports)
    {
        Nation nation = LocalNation();
        PoliticalProposal proposal = PoliticalProposalSystem.CurrentEdict(nation);
        if (proposal != null) PoliticalProposalSystem.CastPlayerVote(nation, proposal.id, supports);
        RefreshEdicts();
    }

    private void ProposeEdict()
    {
        PoliticalProposalSystem.ProposeDefaultPlayerEdict(LocalNation());
        RebuildAll();
    }

    private static int HoldingPower(Nation nation, PoliticalGroup group)
    {
        if (nation == null || group == null || Owners.Instance == null) return 0;
        int result = 0;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.nation != nation || province.holdings == null) continue;
            foreach (ProvinceHolding holding in province.holdings)
            {
                if (holding == null) continue;
                if (group.representsUnalignedHoldings)
                {
                    bool unaligned = string.IsNullOrWhiteSpace(holding.allegiance) ||
                        string.Equals(holding.allegiance, "Unaligned", System.StringComparison.OrdinalIgnoreCase);
                    if (unaligned && SocioEconomicClassRules.Normalize(holding.socioEconomicClass) ==
                        SocioEconomicClassRules.Normalize(group.representedClass)) result++;
                }
                else if (string.Equals(holding.allegiance, group.id, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(holding.allegiance, group.displayName, System.StringComparison.OrdinalIgnoreCase)) result++;
            }
        }
        return result;
    }

    private static void ConfigureButton(GameObject target, UnityEngine.Events.UnityAction action)
    {
        if (target == null || !target.TryGetComponent(out Button button)) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static T ComponentInDescendant<T>(Transform root, string objectName) where T : Component
    {
        Transform child = FindDescendant(root, objectName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static GameObject ObjectInDescendant(Transform root, string objectName)
    {
        Transform child = FindDescendant(root, objectName);
        return child != null ? child.gameObject : null;
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
