using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UISenatorialMenu : MonoBehaviour
{
    private GameObject opener;
    private GameObject overlayRoot;
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
    private bool choosingExtension;
    private int selectedExtensionIndex;
    private readonly List<ExtensionChoice> extensionChoices = new List<ExtensionChoice>();

    private sealed class ExtensionChoice
    {
        public string lawId;
        public NationalEdict edict;
        public string targetAllegianceId;
        public string targetAllegianceName;
    }

    public void Configure(GameObject openControl)
    {
        opener = openControl;
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        overlayRoot = parentCanvas != null && parentCanvas.gameObject != gameObject
            ? parentCanvas.gameObject : gameObject;
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
        if (overlayRoot != null)
        {
            overlayRoot.transform.localScale = Vector3.one;
            overlayRoot.SetActive(true);
        }
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        else RebuildAll();
    }

    public void Close()
    {
        choosingExtension = false;
        gameObject.SetActive(false);
        if (overlayRoot != null && overlayRoot != gameObject) overlayRoot.SetActive(false);
    }

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

        List<ActiveNationalEdict> activeEdicts = nation != null && nation.activeEdicts != null
            ? nation.activeEdicts.FindAll(active => active != null && active.edict != null)
            : new List<ActiveNationalEdict>();

        int rowCount = Mathf.Max(1, activeLaws.Count + activeEdicts.Count + proposals.Count);
        float spacing = Mathf.Max(10f, lawTemplate.rect.height + 10f);
        for (int i = 0; i < rowCount; i++)
        {
            RectTransform row = Instantiate(lawTemplate, lawTemplate.parent);
            bool activeLaw = i < activeLaws.Count;
            int activeEdictIndex = i - activeLaws.Count;
            bool activeEdict = activeEdictIndex >= 0 && activeEdictIndex < activeEdicts.Count;
            int proposalIndex = activeEdictIndex - activeEdicts.Count;
            row.name = activeLaw ? "Law_" + activeLaws[i].id : proposalIndex >= 0 && proposalIndex < proposals.Count
                ? "Proposal_" + proposals[proposalIndex].id : activeEdict
                    ? "ActiveEdict_" + activeEdicts[activeEdictIndex].instanceId : "Law_None";
            row.anchoredPosition = lawTemplate.anchoredPosition + Vector2.down * spacing * i;
            row.gameObject.SetActive(true);
            Text label = row.GetComponentInChildren<Text>(true);
            if (label != null) label.text = activeLaw ? activeLaws[i].displayName +
                "\nNORMAL\n" + activeLaws[i].Describe() + "\n\nAVAILABLE EXTENSIONS\n" +
                activeLaws[i].DescribeExtensions() :
                activeEdict ? "ACTIVE EDICT: " + activeEdicts[activeEdictIndex].title +
                    "\n" + PoliticalProposalSystem.DescribeEdict(activeEdicts[activeEdictIndex].edict) +
                    "\nRemaining: " + activeEdicts[activeEdictIndex].remainingTicks + " ticks" :
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
        if (allegianceHeader != null) allegianceHeader.text = "Allegiances";
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
            Allegiance allegiance = !group.representsUnalignedHoldings ? AllegianceSystem.Find(nation,
                !string.IsNullOrEmpty(group.allegianceId) ? group.allegianceId : group.id) : null;
            if (header != null) header.text = group.representsUnalignedHoldings
                ? group.displayName + ":" : (allegiance != null ? allegiance.type.ToString() : allegianceType) + " " + group.displayName + ":";
            Text data = ComponentInDescendant<Text>(row, "AllegianceData");
            if (data != null) data.text = AllegianceDetails(nation, group, allegiance);
            Image icon = ComponentInDescendant<Image>(row, "AllegianceIcon");
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            row.gameObject.SetActive(true);
            generatedAllegiances.Add(row.gameObject);
        }
    }

    private static string AllegianceDetails(Nation nation, PoliticalGroup group, Allegiance allegiance)
    {
        int power = HoldingPower(nation, group);
        if (allegiance == null) return "Focus: Unaligned " + SocioEconomicClassRules.DisplayName(group.representedClass) +
            "\n\nPower: " + power + " holdings";
        string primary = allegiance.PrimaryIdentity != null ? allegiance.PrimaryIdentity.DisplayName : "Undetermined";
        string dynamicIdentity = allegiance.DynamicIdentity != null ? allegiance.DynamicIdentity.DisplayName : "Undetermined";
        string current = allegiance.currentInterestRegionIds != null && allegiance.currentInterestRegionIds.Count > 0
            ? string.Join(", ", allegiance.currentInterestRegionIds) : "None";
        string future = allegiance.futureInterestRegionIds != null && allegiance.futureInterestRegionIds.Count > 0
            ? string.Join(", ", allegiance.futureInterestRegionIds) : "None";
        return "Primary: " + primary + "\nDynamic: " + dynamicIdentity + "\nCurrent interests: " + current +
            "\nFuture interests: " + future + "\n\nPower: " + power + " holdings";
    }

    private void RefreshEdicts()
    {
        Nation nation = LocalNation();
        PoliticalProposal proposal = PoliticalProposalSystem.CurrentEdict(nation);
        if (choosingExtension && proposal == null)
        {
            RebuildExtensionChoices(nation);
            if (edictDetails != null) edictDetails.text = ExtensionChoiceDescription();
            bool hasChoices = extensionChoices.Count > 0;
            if (edictVoteYes != null) edictVoteYes.SetActive(hasChoices);
            if (edictVoteNo != null) edictVoteNo.SetActive(hasChoices);
            if (edictPropose != null) edictPropose.SetActive(true);
            SetButtonLabel(edictVoteYes, "Previous");
            SetButtonLabel(edictVoteNo, "Next");
            SetButtonLabel(edictPropose, hasChoices ? "Propose Selected" : "Back");
            return;
        }
        if (proposal != null) choosingExtension = false;
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
        SetButtonLabel(edictVoteYes, "Yes");
        SetButtonLabel(edictVoteNo, "No");
        SetButtonLabel(edictPropose, "Propose Edict");
    }

    private void VoteYes()
    {
        if (choosingExtension) { CycleExtension(-1); return; }
        Vote(true);
    }
    private void VoteNo()
    {
        if (choosingExtension) { CycleExtension(1); return; }
        Vote(false);
    }
    private void Vote(bool supports)
    {
        Nation nation = LocalNation();
        PoliticalProposal proposal = PoliticalProposalSystem.CurrentEdict(nation);
        if (proposal != null) PoliticalProposalSystem.CastPlayerVote(nation, proposal.id, supports);
        RefreshEdicts();
    }

    private void ProposeEdict()
    {
        Nation nation = LocalNation();
        if (!choosingExtension)
        {
            choosingExtension = true;
            selectedExtensionIndex = 0;
            RefreshEdicts();
            return;
        }
        RebuildExtensionChoices(nation);
        if (extensionChoices.Count == 0) { choosingExtension = false; RefreshEdicts(); return; }
        selectedExtensionIndex = Mathf.Clamp(selectedExtensionIndex, 0, extensionChoices.Count - 1);
        ExtensionChoice choice = extensionChoices[selectedExtensionIndex];
        if (PoliticalProposalSystem.ProposeExtension(nation, choice.lawId, choice.edict.StableId,
            choice.targetAllegianceId)) choosingExtension = false;
        RebuildAll();
    }

    private void RebuildExtensionChoices(Nation nation)
    {
        string selectedKey = extensionChoices.Count > 0 && selectedExtensionIndex >= 0 &&
            selectedExtensionIndex < extensionChoices.Count ? ChoiceKey(extensionChoices[selectedExtensionIndex]) : string.Empty;
        extensionChoices.Clear();
        if (nation == null) return;
        nation.EnsureDefaultLaws();
        AllegianceSystem.EnsureNationAllegiances(nation);
        foreach (NationalLaw law in nation.laws)
        {
            if (law == null || law.availableExtensions == null) continue;
            foreach (NationalEdict template in law.availableExtensions)
            {
                if (template == null) continue;
                bool requiresAllegiance = template.coreEffects != null && template.coreEffects.Exists(effect =>
                    effect != null && !effect.anyAllegiance);
                if (requiresAllegiance)
                {
                    foreach (Allegiance allegiance in nation.allegiances)
                    {
                        if (allegiance == null) continue;
                        NationalEdict targeted = template.Clone();
                        foreach (NationalLawEffect effect in targeted.coreEffects)
                            if (effect != null && !effect.anyAllegiance) effect.allegianceId = allegiance.id;
                        if (PoliticalProposalSystem.CanActivateExtension(nation, targeted)) extensionChoices.Add(new ExtensionChoice
                            { lawId = law.id, edict = targeted, targetAllegianceId = allegiance.id,
                                targetAllegianceName = allegiance.displayName });
                    }
                }
                else if (PoliticalProposalSystem.CanActivateExtension(nation, template)) extensionChoices.Add(new ExtensionChoice
                    { lawId = law.id, edict = template });
            }
        }
        int restored = extensionChoices.FindIndex(choice => ChoiceKey(choice) == selectedKey);
        selectedExtensionIndex = restored >= 0 ? restored : Mathf.Clamp(selectedExtensionIndex, 0,
            Mathf.Max(0, extensionChoices.Count - 1));
    }

    private string ExtensionChoiceDescription()
    {
        if (extensionChoices.Count == 0)
            return "PROPOSE EXTENSION\n\nNo extensions are currently available.\n\nAn extension may require an active law or may already be active.";
        ExtensionChoice choice = extensionChoices[Mathf.Clamp(selectedExtensionIndex, 0, extensionChoices.Count - 1)];
        string position = "Extension " + (selectedExtensionIndex + 1) + " / " + extensionChoices.Count;
        string target = !string.IsNullOrWhiteSpace(choice.targetAllegianceName)
            ? "\nSelected Allegiance: " + choice.targetAllegianceName : string.Empty;
        return "PROPOSE EXTENSION\n" + position + "\n\n" + choice.edict.DisplayName + target + "\n\n" +
            PoliticalProposalSystem.DescribeEdict(choice.edict) + "\n\nRequired law: " + choice.lawId;
    }

    private void CycleExtension(int direction)
    {
        if (extensionChoices.Count == 0) return;
        selectedExtensionIndex = (selectedExtensionIndex + direction + extensionChoices.Count) % extensionChoices.Count;
        RefreshEdicts();
    }

    private static string ChoiceKey(ExtensionChoice choice) => choice != null && choice.edict != null
        ? choice.lawId + "|" + choice.edict.StableId + "|" + choice.targetAllegianceId : string.Empty;

    private static void SetButtonLabel(GameObject buttonObject, string value)
    {
        if (buttonObject == null) return;
        Text label = buttonObject.GetComponentInChildren<Text>(true);
        if (label != null) label.text = value;
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
