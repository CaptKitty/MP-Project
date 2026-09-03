using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectX.DeterministicBattle;
using ProjectX.TileBattle;

[System.Serializable]
public sealed class TileBattleNetworkState
{
    public List<SavedTileCampaignBattle> Battles = new List<SavedTileCampaignBattle>();
}

/// <summary>
/// Network identity for a player on the campaign map. The server owns the
/// assignment; the owning client only requests its preferred nation.
/// </summary>
public class CampaignNetworkPlayer : NetworkBehaviour
{
    public static CampaignNetworkPlayer Local { get; private set; }

    public NetworkVariable<FixedString64Bytes> NationName = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private static readonly Dictionary<ulong, string> ServerAssignments = new Dictionary<ulong, string>();
    private float nextSnapshotTime;
    private float nextProvinceSnapshotTime;
    private float nextDetailedSnapshotTime;
    private float nextTileBattleSnapshotTime;
    private float nextArmyOwnershipCheckTime;
    private float nextCampaignChecksumTime;
    private float lastCampaignResyncTime;
    private int nextArmyId;
    private bool battleHooksInstalled;
    private int nextBattleTransferId;
    private int lastDetailedStateSignature = int.MinValue;
    private int lastArmyStateSignature = int.MinValue;
    private int lastHoldingStateSignature = int.MinValue;
    private readonly Dictionary<string, int> lastHoldingRecordSignatures = new Dictionary<string, int>();
    private int lastLevyStateSignature = int.MinValue;
    private readonly Dictionary<string, int> lastLevyRecordSignatures = new Dictionary<string, int>();
    private int lastProvinceStateSignature = int.MinValue;
    private int[] lastProvinceSignatures;
    private int lastQueueStateSignature = int.MinValue;
    private int lastLawStateSignature = int.MinValue;
    private int lastActiveEdictStateSignature = int.MinValue;
    private int lastAllegianceStateSignature = int.MinValue;
    private readonly Dictionary<int, BattleStartTransfer> incomingBattleTransfers = new Dictionary<int, BattleStartTransfer>();
    private readonly Dictionary<int, BattleStartTransfer> incomingTileBattleTransfers = new Dictionary<int, BattleStartTransfer>();
    private bool presenceAnnounced;
    private const float CampaignChecksumIntervalSeconds = 30f;
    private const float CampaignChecksumInitialOffsetSeconds = 13f;
    private const float ProvinceSnapshotIntervalSeconds = 2f;
    private const float DetailedSnapshotIntervalSeconds = 2f;
    private const float DetailedSnapshotInitialOffsetSeconds = 0.85f;

    // These collections are scanned frequently on the authoritative peer. Reusing
    // them avoids a large managed-allocation burst (and WebGL GC pause) every pass.
    private readonly List<CampaignUnitState> unitStateBuffer = new List<CampaignUnitState>();
    private readonly List<CampaignLawState> lawStateBuffer = new List<CampaignLawState>();
    private readonly List<CampaignClassRuleState> classRuleStateBuffer = new List<CampaignClassRuleState>();
    private readonly List<CampaignActiveEdictState> activeEdictStateBuffer = new List<CampaignActiveEdictState>();
    private readonly List<CampaignFactionFlagState> factionFlagStateBuffer = new List<CampaignFactionFlagState>();
    private readonly List<CampaignBuildingState> buildingStateBuffer = new List<CampaignBuildingState>();
    private readonly List<CampaignMercenaryState> mercenaryStateBuffer = new List<CampaignMercenaryState>();
    private readonly List<CampaignLevyState> levyStateBuffer = new List<CampaignLevyState>();
    private readonly List<CampaignHoldingState> holdingStateBuffer = new List<CampaignHoldingState>();
    private readonly List<CampaignAllegianceState> allegianceStateBuffer = new List<CampaignAllegianceState>();
    private readonly List<CampaignRecruitmentOrderState> recruitmentStateBuffer = new List<CampaignRecruitmentOrderState>();
    private readonly List<CampaignConstructionOrderState> constructionStateBuffer = new List<CampaignConstructionOrderState>();
    private readonly List<CampaignHoldingConstructionOrderState> holdingConstructionStateBuffer = new List<CampaignHoldingConstructionOrderState>();
    private readonly List<CampaignArmyState> armyStateBuffer = new List<CampaignArmyState>();
    private readonly List<CampaignProvinceState> provinceStateBuffer = new List<CampaignProvinceState>();
    private readonly HashSet<string> receivedArmyIdBuffer = new HashSet<string>();
    private readonly List<FieldArmyHolder> staleArmyBuffer = new List<FieldArmyHolder>();
    private readonly HashSet<int> regionalFoodRepresentativeBuffer = new HashSet<int>();

    // A complete battle snapshot can be considerably larger than Netcode's
    // maximum RPC writer capacity. Keep each string comfortably below that
    // limit (including the worst-case UTF-8 expansion) and reassemble it on
    // the client.
    private const int BattleStartChunkCharacters = 8192;

    private sealed class BattleStartTransfer
    {
        public readonly string[] Chunks;
        public int ReceivedCount;

        public BattleStartTransfer(int chunkCount)
        {
            Chunks = new string[chunkCount];
        }
    }

    public string AssignedNation => NationName.Value.ToString();
    public bool HasAssignment => !NationName.Value.IsEmpty;

    public static bool IsNationPlayerControlled(string nationName)
    {
        if (string.IsNullOrEmpty(nationName)) return false;
        foreach (string assignment in ServerAssignments.Values)
            if (assignment == nationName) return true;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            foreach (NetworkObject networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                CampaignNetworkPlayer player = networkObject != null ? networkObject.GetComponent<CampaignNetworkPlayer>() : null;
                if (player != null && player.AssignedNation == nationName) return true;
            }
        return false;
    }

    public override void OnNetworkSpawn()
    {
        nextCampaignChecksumTime = Time.unscaledTime + CampaignChecksumInitialOffsetSeconds;
        nextProvinceSnapshotTime = Time.unscaledTime;
        nextDetailedSnapshotTime = Time.unscaledTime + DetailedSnapshotInitialOffsetSeconds;
        NationName.OnValueChanged += OnNationChanged;
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        if (IsOwner)
        {
            Local = this;
            RequestPreferredNationRpc(GetPreferredNation());
        }
        if (HasAssignment) AnnouncePresence(AssignedNation);
        EnsureBattleHooks();
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        string departingNation = AssignedNation;
        if (presenceAnnounced)
            CampaignConnectionNotifications.Show(departingNation + " player left the campaign.", true);
        NationName.OnValueChanged -= OnNationChanged;
        RemoveBattleHooks();
        incomingBattleTransfers.Clear();
        incomingTileBattleTransfers.Clear();

        if (Local == this)
        {
            Local = null;
        }

        if (IsServer)
        {
            ServerAssignments.Remove(OwnerClientId);
            if (Owners.Instance != null)
            {
                FieldArmyHolder disconnectedArmy = Owners.Instance.armylist.Find(army =>
                    army != null && army.IsHumanControlled && army.NetworkOwnerClientId == OwnerClientId);
                if (disconnectedArmy != null)
                {
                    disconnectedArmy.NetworkOwnerClientId = ulong.MaxValue;
                    disconnectedArmy.IsHumanControlled = false;
                    disconnectedArmy.IsPlayer = false;
                    disconnectedArmy.target = Vector3.zero;
                }
            }
        }
        RefreshPlayerControlledNations();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer && clientId != NetworkManager.Singleton.LocalClientId)
        {
            lastArmyStateSignature = int.MinValue;
            lastDetailedStateSignature = int.MinValue;
            lastHoldingStateSignature = int.MinValue;
            lastHoldingRecordSignatures.Clear();
            lastLevyStateSignature = int.MinValue;
            lastLevyRecordSignatures.Clear();
            lastProvinceStateSignature = int.MinValue;
            lastProvinceSignatures = null;
            lastQueueStateSignature = int.MinValue;
            lastLawStateSignature = int.MinValue;
            lastActiveEdictStateSignature = int.MinValue;
            lastAllegianceStateSignature = int.MinValue;
        }
    }

    private void BroadcastBattleChecksum(BattleChecksumReport report)
    {
        if (IsServer) ReceiveBattleChecksumRpc(new FixedString64Bytes(report.BattleId), report.Tick, report.Hash);
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveBattleChecksumRpc(FixedString64Bytes battleId, int tick, ulong authoritativeHash)
    {
        if (DeterministicBattleManager.Instance == null) return;
        bool matches = DeterministicBattleManager.Instance.ReconcileNetworkChecksum(battleId.ToString(), tick, authoritativeHash, out ulong localHash);
        if (localHash != 0 && !matches)
            Debug.LogError("Battle desync " + battleId + " tick=" + tick + " server=" + authoritativeHash + " local=" + localHash);
    }

    [Rpc(SendTo.Server)]
    private void RequestPreferredNationRpc(FixedString64Bytes preferredNation)
    {
        string requested = preferredNation.ToString();
        string assignment = ChooseUniqueNation(OwnerClientId, requested);

        ServerAssignments[OwnerClientId] = assignment;
        NationName.Value = assignment;
    }

    private static string ChooseUniqueNation(ulong clientId, string requested)
    {
        if (IsAvailable(clientId, requested))
        {
            return requested;
        }

        string[] fallbacks = { "Rome", "Carthage", "Gaul", "Spain", "Galicia", "Germania", "Egypt", "Franks" };
        foreach (string candidate in fallbacks)
        {
            if (IsAvailable(clientId, candidate))
            {
                return candidate;
            }
        }

        return requested;
    }

    private static bool IsAvailable(ulong clientId, string nation)
    {
        if (string.IsNullOrWhiteSpace(nation))
        {
            return false;
        }

        foreach (KeyValuePair<ulong, string> assignment in ServerAssignments)
        {
            if (assignment.Key != clientId && assignment.Value == nation)
            {
                return false;
            }
        }

        return true;
    }

    private static FixedString64Bytes GetPreferredNation()
    {
        if (SessionManager.Instance != null && SessionManager.Instance.HostFaction != null)
        {
            return SessionManager.Instance.HostFaction.name;
        }

        return new FixedString64Bytes("Rome");
    }

    private void OnNationChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        if (current.IsEmpty) return;
        AnnouncePresence(current.ToString());
        RefreshPlayerControlledNations();

        if (IsOwner && SessionManager.Instance != null)
            SessionManager.Instance.ApplyNetworkFaction(current.ToString());
    }

    private void AnnouncePresence(string nation)
    {
        if (presenceAnnounced || string.IsNullOrEmpty(nation)) return;
        presenceAnnounced = true;
        CampaignConnectionNotifications.Show(nation + " player joined the campaign.");
    }

    private static void RefreshPlayerControlledNations()
    {
        if (Owners.Instance == null || Owners.Instance.nationlist == null) return;
        foreach (Nation nation in Owners.Instance.nationlist)
        {
            if (nation == null) continue;
            nation.IsPlayer = IsNationPlayerControlled(nation.name);
            if (nation.IsPlayer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                CancelPendingAIConstruction(nation);
        }
    }

    private static void CancelPendingAIConstruction(Nation nation)
    {
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.nation != nation || province.constructionOrders == null) continue;
            for (int i = province.constructionOrders.Count - 1; i >= 0; i--)
            {
                ProvinceConstructionOrder order = province.constructionOrders[i];
                if (order == null || !order.initiatedByAI) continue;
                nation.Gold += CampaignEconomy.BuildingGoldCost(order.buildingId, order.targetLevel);
                province.constructionOrders.RemoveAt(i);
            }
        }
    }

    private void Update()
    {
        EnsureBattleHooks();
        if (!IsServer || !IsOwner || Owners.Instance == null || Mapshower.Instance == null)
        {
            return;
        }

        if (Time.unscaledTime >= nextArmyOwnershipCheckTime)
        {
            nextArmyOwnershipCheckTime = Time.unscaledTime + 1f;
            EnsureHumanArmiesExist();
        }

        if (Time.unscaledTime >= nextSnapshotTime)
        {
            nextSnapshotTime = Time.unscaledTime + 0.2f;
            BroadcastArmyState();
        }

        if (Time.unscaledTime >= nextProvinceSnapshotTime)
        {
            nextProvinceSnapshotTime = Time.unscaledTime + ProvinceSnapshotIntervalSeconds;
            BroadcastProvinceState();
        }

        if (Time.unscaledTime >= nextDetailedSnapshotTime)
        {
            nextDetailedSnapshotTime = Time.unscaledTime + DetailedSnapshotIntervalSeconds;
            BroadcastDetailedState();
        }

        if (Time.unscaledTime >= nextTileBattleSnapshotTime)
        {
            nextTileBattleSnapshotTime = Time.unscaledTime + 0.5f;
            BroadcastTileBattleState();
        }
        if (Time.unscaledTime >= nextCampaignChecksumTime)
        {
            nextCampaignChecksumTime = Time.unscaledTime + CampaignChecksumIntervalSeconds;
            ReceiveCampaignChecksumRpc(Owners.Instance != null ? Owners.Instance.turncounter : 0,
                CampaignStateChecksum());
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveCampaignChecksumRpc(int campaignTurn, int authoritativeChecksum)
    {
        if (Owners.Instance == null) return;
        if (CampaignStateChecksum() != authoritativeChecksum && Local != null)
            Local.RequestCampaignResyncRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestCampaignResyncRpc()
    {
        if (Time.unscaledTime - lastCampaignResyncTime < 5f) return;
        lastCampaignResyncTime = Time.unscaledTime;
        lastDetailedStateSignature = int.MinValue; lastHoldingStateSignature = int.MinValue;
        lastAllegianceStateSignature = int.MinValue;
        lastLevyStateSignature = int.MinValue; lastProvinceSignatures = null;
        lastHoldingRecordSignatures.Clear(); lastLevyRecordSignatures.Clear();
        BroadcastProvinceState(); BroadcastDetailedState();
    }

    private static int CampaignStateChecksum()
    {
        if (Owners.Instance == null) return 0;
        unchecked
        {
            int hash = 17;
            foreach (Nation nation in Owners.Instance.nationlist)
            {
                if (nation == null) { hash *= 31; continue; }
                hash = hash * 31 + StableTextHash(nation.name); hash = hash * 31 + nation.Gold;
                hash = hash * 31 + Mathf.RoundToInt(nation.Manpower * 1000f); hash = hash * 31 + nation.UpkeepDebt;
                hash = hash * 31 + StableTextHash(nation.TributaryMasterName);
                hash = hash * 31 + StableTextHash(nation.WarNationNames != null
                    ? string.Join("|", nation.WarNationNames) : string.Empty);
            }
            foreach (Province province in Owners.Instance.provincelist)
            {
                if (province == null) { hash *= 31; continue; }
                hash = hash * 31 + StableTextHash(province.nation != null ? province.nation.name : string.Empty);
                hash = hash * 31 + province.supply; hash = hash * 31 + province.urbanization;
                if (province.holdings != null) foreach (ProvinceHolding holding in province.holdings)
                {
                    if (holding == null) continue;
                    hash = hash * 31 + StableTextHash(holding.instanceId); hash = hash * 31 + StableTextHash(holding.HoldingId);
                    hash = hash * 31 + holding.level; hash = hash * 31 + (int)holding.socioEconomicClass;
                }
                if (province.levyEntitlements != null) foreach (ProvinceLevyEntitlement levy in province.levyEntitlements)
                {
                    if (levy == null) continue;
                    hash = hash * 31 + StableTextHash(levy.id); hash = hash * 31 + (int)levy.state;
                    hash = hash * 31 + levy.remainingTicks;
                }
            }
            return hash;
        }
    }

    private static int StableTextHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (value != null) for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash;
        }
    }

    private void BroadcastTileBattleState()
    {
        if (TileBattleCampaignManager.Instance == null) return;
        TileBattleNetworkState state = new TileBattleNetworkState();
        state.Battles.AddRange(TileBattleCampaignManager.Instance.CaptureActiveBattles());
        string json = JsonUtility.ToJson(state); int transferId = ++nextBattleTransferId;
        int chunkCount = Mathf.Max(1, Mathf.CeilToInt(json.Length / (float)BattleStartChunkCharacters));
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int start = chunkIndex * BattleStartChunkCharacters;
            int length = Mathf.Min(BattleStartChunkCharacters, json.Length - start);
            ReceiveTileBattleStateChunkRpc(transferId, chunkIndex, chunkCount, json.Substring(start, length));
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveTileBattleStateChunkRpc(int transferId, int chunkIndex, int chunkCount, string chunk)
    {
        if (chunkCount <= 0 || chunkCount > 4096 || chunkIndex < 0 || chunkIndex >= chunkCount)
        { incomingTileBattleTransfers.Remove(transferId); return; }
        if (!incomingTileBattleTransfers.TryGetValue(transferId, out BattleStartTransfer transfer) || transfer.Chunks.Length != chunkCount)
        { transfer = new BattleStartTransfer(chunkCount); incomingTileBattleTransfers[transferId] = transfer; }
        if (transfer.Chunks[chunkIndex] == null) { transfer.Chunks[chunkIndex] = chunk; transfer.ReceivedCount++; }
        if (transfer.ReceivedCount != chunkCount) return;
        StringBuilder json = new StringBuilder();
        for (int i = 0; i < transfer.Chunks.Length; i++) json.Append(transfer.Chunks[i]);
        incomingTileBattleTransfers.Remove(transferId);
        TileBattleNetworkState state = JsonUtility.FromJson<TileBattleNetworkState>(json.ToString());
        if (state != null && TileBattleCampaignManager.Instance != null)
            TileBattleCampaignManager.Instance.ReceiveNetworkState(state.Battles);
    }

    private void EnsureBattleHooks()
    {
        if (!IsServer || !IsOwner || battleHooksInstalled || DeterministicBattleManager.Instance == null) return;
        DeterministicBattleManager manager = DeterministicBattleManager.Instance;
        manager.ChecksumReady += BroadcastBattleChecksum;
        manager.NetworkBattleStarted += BroadcastBattleStarted;
        manager.NetworkCommandScheduled += BroadcastBattleCommand;
        manager.NetworkBattleFinished += BroadcastBattleFinished;
        battleHooksInstalled = true;
        for (int i = 0; i < manager.ActiveBattles.Count; i++)
        {
            CampaignActiveBattle battle = manager.ActiveBattles[i];
            SavedActiveBattle state = manager.CaptureActiveBattles().Find(item => item.StartState.BattleId == battle.StartState.BattleId);
            if (state != null) BroadcastBattleStarted(state);
        }
    }

    private void RemoveBattleHooks()
    {
        if (!battleHooksInstalled || DeterministicBattleManager.Instance == null) return;
        DeterministicBattleManager manager = DeterministicBattleManager.Instance;
        manager.ChecksumReady -= BroadcastBattleChecksum;
        manager.NetworkBattleStarted -= BroadcastBattleStarted;
        manager.NetworkCommandScheduled -= BroadcastBattleCommand;
        manager.NetworkBattleFinished -= BroadcastBattleFinished;
        battleHooksInstalled = false;
    }

    private void BroadcastBattleStarted(SavedActiveBattle state)
    {
        string json = JsonUtility.ToJson(state);
        int transferId = ++nextBattleTransferId;
        int chunkCount = Mathf.Max(1, Mathf.CeilToInt(json.Length / (float)BattleStartChunkCharacters));

        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int start = chunkIndex * BattleStartChunkCharacters;
            int length = Mathf.Min(BattleStartChunkCharacters, json.Length - start);
            ReceiveBattleStartedChunkRpc(transferId, chunkIndex, chunkCount, json.Substring(start, length));
        }
    }
    private void BroadcastBattleCommand(string battleId, BattleCommandRecord command) =>
        ReceiveBattleCommandRpc(new FixedString64Bytes(battleId), JsonUtility.ToJson(command));
    private void BroadcastBattleFinished(string battleId) => ReceiveBattleFinishedRpc(new FixedString64Bytes(battleId));

    [Rpc(SendTo.NotServer)]
    private void ReceiveBattleStartedChunkRpc(int transferId, int chunkIndex, int chunkCount, string chunk)
    {
        if (chunkCount <= 0 || chunkCount > 4096 || chunkIndex < 0 || chunkIndex >= chunkCount)
        {
            incomingBattleTransfers.Remove(transferId);
            return;
        }

        if (!incomingBattleTransfers.TryGetValue(transferId, out BattleStartTransfer transfer) ||
            transfer.Chunks.Length != chunkCount)
        {
            transfer = new BattleStartTransfer(chunkCount);
            incomingBattleTransfers[transferId] = transfer;
        }

        if (transfer.Chunks[chunkIndex] == null)
        {
            transfer.Chunks[chunkIndex] = chunk;
            transfer.ReceivedCount++;
        }

        if (transfer.ReceivedCount != chunkCount) return;

        int characterCount = 0;
        for (int i = 0; i < transfer.Chunks.Length; i++)
            characterCount += transfer.Chunks[i].Length;

        StringBuilder json = new StringBuilder(characterCount);
        for (int i = 0; i < transfer.Chunks.Length; i++)
            json.Append(transfer.Chunks[i]);

        incomingBattleTransfers.Remove(transferId);
        if (DeterministicBattleManager.Instance != null)
            DeterministicBattleManager.Instance.ReceiveNetworkBattle(JsonUtility.FromJson<SavedActiveBattle>(json.ToString()));
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveBattleCommandRpc(FixedString64Bytes battleId, string json)
    {
        if (DeterministicBattleManager.Instance != null)
            DeterministicBattleManager.Instance.ReceiveNetworkCommand(battleId.ToString(), JsonUtility.FromJson<BattleCommandRecord>(json));
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveBattleFinishedRpc(FixedString64Bytes battleId)
    {
        if (DeterministicBattleManager.Instance != null)
            DeterministicBattleManager.Instance.ReceiveNetworkBattleFinished(battleId.ToString());
    }

    public void RequestBattleOrder(string armyId, int formationId, FormationOrder order, int delayTicks)
    {
        if (IsOwner) RequestBattleOrderRpc(new FixedString64Bytes(armyId), formationId, (byte)order, delayTicks);
    }

    [Rpc(SendTo.Server)]
    private void RequestBattleOrderRpc(FixedString64Bytes armyId, int formationId, byte order, int delayTicks, RpcParams rpcParams = default)
    {
        if (Owners.Instance == null || DeterministicBattleManager.Instance == null) return;
        FieldArmyHolder army = Owners.Instance.armylist.Find(item => item != null && item.NetworkArmyId == armyId.ToString() &&
            item.NetworkOwnerClientId == rpcParams.Receive.SenderClientId);
        if (army != null) DeterministicBattleManager.Instance.SchedulePlayerOrder(army, formationId,
            (FormationOrder)Mathf.Clamp(order, 0, (byte)FormationOrder.Withdraw), Mathf.Clamp(delayTicks, 2, 20));
    }

    public void RequestArmyMove(Vector3 mapTarget)
    {
        RequestArmyMove(string.Empty, mapTarget);
    }

    public void RequestArmyMove(string armyId, Vector3 mapTarget)
    {
        if (!IsOwner || !HasAssignment)
        {
            return;
        }

        RequestArmyMoveRpc(armyId ?? string.Empty, mapTarget.x, mapTarget.y);
    }

    public void RequestRecruit(string unitName, int amount = 1)
    {
        RequestProvinceRecruit(unitName, amount, false, string.Empty, false);
    }

    public void RequestProvinceRecruit(string unitName, int amount, bool mercenary, string sourceProvinceName,
        bool tributary = false)
    {
        if (IsOwner && HasAssignment && amount > 0)
        {
            string armyId = FieldArmyHolder.SelectedPlayerArmy != null
                ? FieldArmyHolder.SelectedPlayerArmy.NetworkArmyId : string.Empty;
            RequestRecruitRpc(unitName, amount, mercenary, tributary, sourceProvinceName ?? string.Empty, armyId ?? string.Empty);
        }
    }

    public void RequestRaiseLevy(string entitlementId, string sourceProvinceName)
    {
        if (!IsOwner || !HasAssignment || string.IsNullOrEmpty(entitlementId)) return;
        string armyId = FieldArmyHolder.SelectedPlayerArmy != null
            ? FieldArmyHolder.SelectedPlayerArmy.NetworkArmyId : string.Empty;
        RequestRaiseLevyRpc(entitlementId, sourceProvinceName ?? string.Empty, armyId ?? string.Empty);
    }

    public void RequestRaiseAllLevies()
    {
        if (!IsOwner || !HasAssignment) return;
        string armyId = FieldArmyHolder.SelectedPlayerArmy != null
            ? FieldArmyHolder.SelectedPlayerArmy.NetworkArmyId : string.Empty;
        RequestRaiseAllLeviesRpc(armyId ?? string.Empty);
    }

    public void BroadcastRecruitmentVisual(string armyId, string unitName, string sourceNationName = null)
    {
        if (!IsServer || string.IsNullOrEmpty(armyId) || string.IsNullOrEmpty(unitName)) return;
        ReceiveRecruitmentVisualRpc(new FixedString64Bytes(armyId), new FixedString64Bytes(unitName),
            new FixedString64Bytes(sourceNationName ?? string.Empty));
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveRecruitmentVisualRpc(FixedString64Bytes armyId, FixedString64Bytes unitName,
        FixedString64Bytes sourceNationName)
    {
        if (Owners.Instance == null) return;
        FieldArmyHolder army = Owners.Instance.armylist.Find(candidate => candidate != null &&
            candidate.NetworkArmyId == armyId.ToString() && candidate.fieldArmy != null);
        if (army == null || army.fieldArmy.nation == null) return;
        UnitSaveData unit = FindUnit(army.fieldArmy.nation, unitName.ToString());
        if (unit != null) CampaignRecruitmentVisual.SpawnLocal(unit, army, sourceNationName.ToString());
    }

    public void RequestDemobilizeAllLevies()
    {
        if (!IsOwner || !HasAssignment) return;
        string armyId = FieldArmyHolder.SelectedPlayerArmy != null
            ? FieldArmyHolder.SelectedPlayerArmy.NetworkArmyId : string.Empty;
        RequestDemobilizeAllLeviesRpc(armyId ?? string.Empty);
    }

    [Rpc(SendTo.Server)]
    private void RequestDemobilizeAllLeviesRpc(FixedString64Bytes armyId, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        if (sender == null || Owners.Instance == null) return;
        FieldArmyHolder army = Owners.Instance.armylist.Find(candidate => candidate != null &&
            candidate.NetworkArmyId == armyId.ToString() && candidate.NetworkOwnerClientId == rpcParams.Receive.SenderClientId &&
            candidate.IsHumanControlled && candidate.fieldArmy != null && candidate.fieldArmy.nation != null &&
            candidate.fieldArmy.nation.name == sender.AssignedNation);
        if (army == null || !army.IsTargetNull()) return;
        army.fieldArmy.DemobilizeAllLevies();
    }

    [Rpc(SendTo.Server)]
    private void RequestRaiseAllLeviesRpc(FixedString64Bytes armyId, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        if (sender == null || Owners.Instance == null) return;
        FieldArmyHolder army = Owners.Instance.armylist.Find(candidate => candidate != null &&
            candidate.NetworkArmyId == armyId.ToString() && candidate.NetworkOwnerClientId == rpcParams.Receive.SenderClientId &&
            candidate.IsHumanControlled && candidate.fieldArmy != null && candidate.fieldArmy.nation != null &&
            candidate.fieldArmy.nation.name == sender.AssignedNation);
        Province current = army != null ? army.GrabNearestProvince() : null;
        if (current == null || current.nation != army.fieldArmy.nation) return;
        if (!army.IsTargetNull()) return;
        current.RaiseAllAvailableLocalAndAdjacentRegionLevies(army, true);
    }

    [Rpc(SendTo.Server)]
    private void RequestRaiseLevyRpc(FixedString128Bytes entitlementId, FixedString64Bytes sourceProvinceName,
        FixedString64Bytes armyId, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        if (sender == null || Owners.Instance == null) return;
        FieldArmyHolder army = Owners.Instance.armylist.Find(candidate => candidate != null &&
            candidate.NetworkArmyId == armyId.ToString() && candidate.NetworkOwnerClientId == rpcParams.Receive.SenderClientId &&
            candidate.fieldArmy != null && candidate.fieldArmy.nation != null && candidate.fieldArmy.nation.name == sender.AssignedNation);
        Province source = Owners.Instance.provincelist.Find(candidate => candidate != null && candidate.name == sourceProvinceName.ToString());
        Province current = army != null ? army.GrabNearestProvince() : null;
        if (army == null || source == null || current == null || source.nation != army.fieldArmy.nation ||
            !current.SharesRegionWith(source)) return;
        source.ReconcileLevyEntitlements();
        if (!army.IsTargetNull()) return;
        source.RaiseLevy(entitlementId.ToString(), army);
    }

    public void RequestEventOption(string eventName, int optionIndex, string targetNation = null)
    {
        if (IsOwner && HasAssignment)
        {
            RequestEventOptionRpc(eventName, optionIndex, targetNation ?? AssignedNation);
        }
    }

    public void RequestCampaignSpeed(float speed)
    {
        if (IsOwner && IsSpawned && HasAssignment)
            RequestCampaignSpeedRpc(speed);
    }

    [Rpc(SendTo.Server)]
    private void RequestCampaignSpeedRpc(float requestedSpeed, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        if (sender == null || !sender.HasAssignment || Mapshower.Instance == null) return;
        float speed = NormalizeCampaignSpeed(requestedSpeed);
        string nation = sender.AssignedNation;
        Mapshower.Instance.ApplyNetworkCampaignSpeed(speed, nation);
        ApplyCampaignSpeedRpc(speed, new FixedString64Bytes(nation));
    }

    [Rpc(SendTo.NotServer)]
    private void ApplyCampaignSpeedRpc(float speed, FixedString64Bytes requestingNation)
    {
        if (Mapshower.Instance != null)
            Mapshower.Instance.ApplyNetworkCampaignSpeed(NormalizeCampaignSpeed(speed), requestingNation.ToString());
    }

    private static float NormalizeCampaignSpeed(float requested)
    {
        float[] options = { 0f, .25f, 1f, 2f, 5f };
        float result = options[0];
        float distance = Mathf.Abs(requested - result);
        for (int i = 1; i < options.Length; i++)
        {
            float candidateDistance = Mathf.Abs(requested - options[i]);
            if (candidateDistance >= distance) continue;
            distance = candidateDistance;
            result = options[i];
        }
        return result;
    }

    [Rpc(SendTo.Server)]
    private void RequestEventOptionRpc(FixedString64Bytes eventName, int optionIndex,
        FixedString64Bytes targetNation, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        BaseEvents source = Resources.Load<BaseEvents>("EventGroup/" + eventName.ToString());
        if (sender == null || source == null || optionIndex < 0 || optionIndex >= source.OptionList.Count)
        {
            return;
        }

        // A player may only resolve events belonging to their assigned nation.
        string resolvedNation = targetNation.IsEmpty ? sender.AssignedNation : targetNation.ToString();
        if (!string.Equals(resolvedNation, sender.AssignedNation, System.StringComparison.OrdinalIgnoreCase)) return;
        EventContext context = EventContext.ForNation(resolvedNation);
        if (context.ResolveNation() == null || !source.Trigger(context)) return;

        Option option = source.OptionList[optionIndex];
        if (option.trigger != null && !option.trigger.CanTrigger(context))
        {
            return;
        }
        foreach (BaseEffect effectSource in option.EffectList)
        {
            if (effectSource == null) continue;
            BaseEffect effect = Instantiate(effectSource);
            effect.GrabRandomTarget(context);
            effect.Execute(context);
        }
    }

    public void RequestFactionUpgrade(string upgrade)
    {
        if (IsOwner && HasAssignment)
        {
            RequestFactionUpgradeRpc(upgrade);
        }
    }

    public void RequestProvinceBuilding(string provinceName, int slotIndex, string buildingId, int targetLevel)
    {
        if (IsOwner && HasAssignment)
            RequestProvinceBuildingRpc(provinceName ?? string.Empty, slotIndex, buildingId ?? string.Empty, targetLevel);
    }

    public void RequestDestroyProvinceBuilding(string provinceName, int slotIndex)
    {
        if (IsOwner && HasAssignment)
            RequestDestroyProvinceBuildingRpc(provinceName ?? string.Empty, slotIndex);
    }

    public void RequestProvinceHolding(string provinceName, int slotIndex, string holdingId, int targetLevel)
    {
        if (IsOwner && HasAssignment)
            RequestProvinceHoldingRpc(provinceName ?? string.Empty, slotIndex, holdingId ?? string.Empty, targetLevel);
    }

    public void RequestRaiseArmy(string provinceName)
    {
        if (IsOwner && HasAssignment) RequestRaiseArmyRpc(provinceName ?? string.Empty);
    }

    [Rpc(SendTo.Server)]
    private void RequestRaiseArmyRpc(FixedString64Bytes provinceName, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        Province province = sender == null || Owners.Instance == null ? null
            : Owners.Instance.provincelist.Find(item => item.name == provinceName.ToString());
        Nation nation = province != null ? province.nation : null;
        if (nation == null || nation.name != sender.AssignedNation || nation.Gold < CampaignEconomy.ArmyCreationCost) return;
        nation.Gold -= CampaignEconomy.ArmyCreationCost;
        nation.ArmyNumber++;
        FieldArmyHolder army = Mapshower.Instance.SpawnArmy(province, nation.ArmyNumber + " Army of " + nation.name);
        if (army == null) { nation.Gold += CampaignEconomy.ArmyCreationCost; return; }
        army.PreserveConfiguredRoster = true;
        army.ConfigureNetworkIdentity(CreateArmyId(nation.name), rpcParams.Receive.SenderClientId, true, nation);
        FieldArmyHolder.SelectedPlayerArmy = army;
        FieldArmyHolder.InspectedArmy = army;
    }

    [Rpc(SendTo.Server)]
    private void RequestDestroyProvinceBuildingRpc(FixedString64Bytes provinceName, int slotIndex,
        RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        Province province = sender == null || Owners.Instance == null ? null
            : Owners.Instance.provincelist.Find(item => item.name == provinceName.ToString());
        if (province == null || province.nation == null || sender == null ||
            province.nation.name != sender.AssignedNation || slotIndex < 0 || slotIndex >= 4) return;
        province.DestroyBuildingInSlot(slotIndex);
    }

    [Rpc(SendTo.Server)]
    private void RequestProvinceBuildingRpc(FixedString64Bytes provinceName, int slotIndex,
        FixedString64Bytes buildingId, int targetLevel, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        Province province = sender == null || Owners.Instance == null ? null
            : Owners.Instance.provincelist.Find(item => item.name == provinceName.ToString());
        if (province == null || province.nation == null || province.nation.name != sender.AssignedNation || slotIndex < 0 || slotIndex >= 4) return;

        string id = buildingId.ToString();
        ProvinceBuilding existing = province.GetBuildingInSlot(slotIndex);
        if (!NationContentResolver.HasBuilding(province.nation, id)) return;
        if (!NationContentResolver.CanConstructBuildingLevel(province.nation, id, targetLevel)) return;
        if (existing == null)
        {
            if (targetLevel != 1 || !NationContentResolver.HasBuilding(province.nation, id)) return;
        }
        else if (!existing.BuildingId.Equals(id, System.StringComparison.OrdinalIgnoreCase) ||
            targetLevel != existing.level + 1 || targetLevel > existing.maxLevel) return;

        int goldCost = CampaignEconomy.BuildingGoldCost(id, targetLevel);
        Nation owner = province.nation;
        if (owner == null || owner.Gold < goldCost) return;
        if (!province.BeginBuildingConstruction(slotIndex, id, targetLevel,
            BuildingDefinition.ConstructionTicks(id, targetLevel))) return;
        owner.Gold -= goldCost;
    }

    [Rpc(SendTo.Server)]
    private void RequestProvinceHoldingRpc(FixedString64Bytes provinceName, int slotIndex,
        FixedString64Bytes holdingId, int targetLevel, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        Province province = sender == null || Owners.Instance == null ? null
            : Owners.Instance.provincelist.Find(item => item.name == provinceName.ToString());
        if (province == null || province.nation == null || province.nation.name != sender.AssignedNation || slotIndex < 0) return;
        HoldingDefinition definition = HoldingDefinition.Find(holdingId.ToString());
        if (definition == null) return;
        ProvinceHolding existing = province.GetHoldingInSlot(slotIndex);
        if (existing == null || existing.HoldingId.Equals(definition.StableId, System.StringComparison.OrdinalIgnoreCase)) return;
        int cost = definition.GoldCostForLevel(1);
        if (province.nation.Gold < cost || !province.BeginHoldingTransformation(existing.instanceId, definition.StableId,
            definition.ConstructionTicksForLevel(1))) return;
        province.nation.Gold -= cost;
    }

    [Rpc(SendTo.Server)]
    private void RequestFactionUpgradeRpc(FixedString64Bytes upgrade, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        Nation nation = sender == null || Owners.Instance == null
            ? null
            : Owners.Instance.nationlist.Find(item => item.name == sender.AssignedNation);
        if (nation == null)
        {
            return;
        }

        string value = upgrade.ToString();
        if (value.Contains("Barracks") && nation.faction.BarracksLevel < nation.faction.BarracksDataList.Count)
        {
            nation.faction.UpgradeBarracks();
        }
        else if (value.Contains("Merc") && nation.faction.MercenaryDataList.Count > 0)
        {
            nation.faction.UpgradeMercenaries();
        }
        else if (value.Contains("Farm"))
        {
            nation.faction.FarmLevel++;
            nation.faction.GrabIncome();
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestRecruitRpc(FixedString64Bytes unitName, int amount, bool mercenary, bool tributary,
        FixedString64Bytes sourceProvinceName, FixedString64Bytes armyId, RpcParams rpcParams = default)
    {
        if (mercenary && !ProvinceMercenaryPool.Enabled) return;
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        if (sender == null || amount < 1 || amount > 10)
        {
            return;
        }

        string requestedArmyId = armyId.ToString();
        FieldArmyHolder army = !string.IsNullOrEmpty(requestedArmyId) && Owners.Instance != null
            ? Owners.Instance.armylist.Find(candidate => candidate != null && candidate.NetworkArmyId == requestedArmyId &&
                candidate.NetworkOwnerClientId == rpcParams.Receive.SenderClientId && candidate.IsHumanControlled &&
                candidate.fieldArmy != null && candidate.fieldArmy.nation != null &&
                candidate.fieldArmy.nation.name == sender.AssignedNation)
            : FindHumanArmy(rpcParams.Receive.SenderClientId, sender.AssignedNation);
        if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null || !army.IsTargetNull())
        {
            return;
        }

        Province currentProvince = army.GrabNearestProvince();
        Nation nation = army.fieldArmy.nation;
        string requestedSource = sourceProvinceName.ToString();
        Province sourceProvince = string.IsNullOrEmpty(requestedSource)
            ? currentProvince
            : Owners.Instance.provincelist.Find(item => item.name == requestedSource);
        if (currentProvince == null || sourceProvince == null)
        {
            return;
        }
        Nation manpowerNation = tributary ? sourceProvince.nation : nation;
        if (tributary && !DiplomacySystem.CanRecruitTributaryRoster(nation, manpowerNation) ||
            !sourceProvince.AllowsRecruitment(manpowerNation)) return;

        ProvinceMercenaryPool requestedMercenaryPool = mercenary ? sourceProvince.FindMercenary(unitName.ToString()) : null;
        UnitSaveData unit = mercenary
            ? requestedMercenaryPool != null ? requestedMercenaryPool.unit : null
            : FindUnit(manpowerNation, unitName.ToString());
        if (unit == null) return;

        int availableSlots = army.fieldArmy.MaxArmySize - army.fieldArmy.GrabArmySize() - army.fieldArmy.GrabQueuedArmySize();
        int recruitAmount = Mathf.Min(amount, availableSlots);
        if (recruitAmount <= 0) return;
        CampaignUnitOrigin recruitmentOrigin = mercenary || tributary
            ? CampaignUnitOrigin.Mercenary : CampaignUnitOrigin.Professional;
        int goldCost = CampaignEconomy.UnitGoldCost(unit, recruitAmount, nation, recruitmentOrigin);
        if (nation.Gold < goldCost) return;

        if (mercenary)
        {
            ProvinceMercenaryPool pool = requestedMercenaryPool;
            if (sourceProvince != currentProvince || pool == null || pool.available < recruitAmount) return;

            int supplyCost = Mathf.Max(1, unit.cost / 50) * recruitAmount;
            if (army.fieldArmy.ArmySupply < supplyCost) return;
            army.fieldArmy.ArmySupply -= supplyCost;
            pool.available -= recruitAmount;
        }
        else if (tributary)
        {
            if (sourceProvince.nation != manpowerNation ||
                !currentProvince.CanAccessRecruitmentSource(sourceProvince, manpowerNation) ||
                !sourceProvince.CanRecruitLocal(unit) || !manpowerNation.TrySpendManpower(sourceProvince, recruitAmount)) return;
        }
        else
        {
            if (currentProvince.nation != nation || sourceProvince.nation != nation ||
                !currentProvince.SharesRegionWith(sourceProvince) || !sourceProvince.CanRecruitLocal(unit)) return;
            if (!nation.TrySpendManpower(sourceProvince, recruitAmount)) return;
        }

        if (!army.fieldArmy.QueueRecruitment(unit, recruitAmount, recruitmentOrigin,
            tributary ? manpowerNation.name : null))
        {
            if (tributary) manpowerNation.RefundManpower(sourceProvince, recruitAmount);
            else if (!mercenary) nation.RefundManpower(sourceProvince, recruitAmount);
            return;
        }
        nation.Gold -= goldCost;
    }

    [Rpc(SendTo.Server)]
    private void RequestArmyMoveRpc(FixedString64Bytes armyId, float x, float y, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        if (sender == null || !sender.HasAssignment || Owners.Instance == null)
        {
            return;
        }

        string requestedArmy = armyId.ToString();
        FieldArmyHolder army = !string.IsNullOrEmpty(requestedArmy)
            ? Owners.Instance.armylist.Find(item => item != null && item.NetworkArmyId == requestedArmy &&
                item.fieldArmy != null && item.fieldArmy.nation != null && item.fieldArmy.nation.name == sender.AssignedNation)
            : FindHumanArmy(rpcParams.Receive.SenderClientId, sender.AssignedNation);
        if (army == null)
        {
            return;
        }

        Vector3 requestedTarget = new Vector3(x, y, 0);
        if (x < 0 || y < 0 || x >= Mapshower.Instance.width || y >= Mapshower.Instance.height)
        {
            return;
        }

        army.IsPlayer = true; army.IsHumanControlled = true;
        army.NetworkOwnerClientId = rpcParams.Receive.SenderClientId;
        army.SetTarget(requestedTarget);
        army.TargetProvince = FindNearestProvince(requestedTarget);
    }

    private void EnsureHumanArmiesExist()
    {
        foreach (NetworkObject playerObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            CampaignNetworkPlayer player = playerObject.GetComponent<CampaignNetworkPlayer>();
            if (player == null || !player.HasAssignment)
            {
                continue;
            }

            FieldArmyHolder army = FindHumanArmy(player.OwnerClientId, player.AssignedNation);
            if (army == null)
            {
                Nation nation = Owners.Instance.nationlist.Find(item => item.name == player.AssignedNation);
                if (nation == null) continue;
                army = nation.armies.Find(candidate => candidate != null && !candidate.IsHumanControlled);
                if (army == null) continue;
                army.ConfigureNetworkIdentity(
                    string.IsNullOrEmpty(army.NetworkArmyId) ? CreateArmyId(nation.name) : army.NetworkArmyId,
                    player.OwnerClientId,
                    true,
                    nation);
            }
            else
            {
                army.ConfigureNetworkIdentity(
                    string.IsNullOrEmpty(army.NetworkArmyId) ? CreateArmyId(army.fieldArmy.nation.name) : army.NetworkArmyId,
                    player.OwnerClientId,
                    true,
                    army.fieldArmy.nation);
            }
        }
    }

    private void BroadcastArmyState()
    {
        List<CampaignArmyState> armies = armyStateBuffer;
        armies.Clear();
        int signature = 17;
        foreach (FieldArmyHolder army in Owners.Instance.armylist)
        {
            if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(army.NetworkArmyId))
            {
                army.NetworkArmyId = CreateArmyId(army.fieldArmy.nation.name);
            }

            CampaignArmyState state = CampaignArmyState.FromArmy(army);
            armies.Add(state);
            unchecked
            {
                signature = signature * 31 + state.ArmyId.GetHashCode();
                signature = signature * 31 + state.DisplayName.GetHashCode();
                signature = signature * 31 + state.NationName.GetHashCode();
                signature = signature * 31 + state.OwnerClientId.GetHashCode();
                signature = signature * 31 + state.MapPosition.GetHashCode();
                signature = signature * 31 + state.MapTarget.GetHashCode();
                signature = signature * 31 + state.Supply;
                signature = signature * 31 + state.UnitCount;
                signature = signature * 31 + (state.InEncounter ? 1 : 0);
            }
        }
        if (signature == lastArmyStateSignature) return;
        lastArmyStateSignature = signature;
        ReceiveArmyStateRpc(armies.ToArray());
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveArmyStateRpc(CampaignArmyState[] armies)
    {
        if (Owners.Instance == null || Mapshower.Instance == null)
        {
            return;
        }

        HashSet<string> receivedIds = receivedArmyIdBuffer;
        receivedIds.Clear();
        foreach (CampaignArmyState state in armies)
        {
            string armyId = state.ArmyId.ToString();
            receivedIds.Add(armyId);
            FieldArmyHolder army = Owners.Instance.armylist.Find(item => item != null && item.NetworkArmyId == armyId);

            bool locallyOwned = state.OwnerClientId == NetworkManager.Singleton.LocalClientId;
            if (locallyOwned && FieldArmyHolder.PlayerFieldArmy != null && army != FieldArmyHolder.PlayerFieldArmy)
            {
                if (army != null && army.IsNetworkReplica)
                {
                    Owners.Instance.armylist.Remove(army);
                    if (army.fieldArmy != null && army.fieldArmy.nation != null)
                        army.fieldArmy.nation.armies.Remove(army);
                    Destroy(army.gameObject);
                }
                army = FieldArmyHolder.PlayerFieldArmy;
            }

            if (army == null)
            {
                Province start = FindNearestProvince(state.MapPosition);
                if (start == null)
                {
                    continue;
                }
                army = Mapshower.Instance.SpawnArmy(start, state.DisplayName.ToString());
            }

            Nation nation = Owners.Instance.nationlist.Find(item => item.name == state.NationName.ToString());
            army.ConfigureNetworkIdentity(armyId, state.OwnerClientId, state.OwnerClientId != ulong.MaxValue, nation);
            army.ApplyNetworkState(state);
        }

        List<FieldArmyHolder> stale = staleArmyBuffer;
        stale.Clear();
        foreach (FieldArmyHolder candidate in Owners.Instance.armylist)
            if (candidate != null && candidate.IsNetworkReplica && !receivedIds.Contains(candidate.NetworkArmyId))
                stale.Add(candidate);
        foreach (FieldArmyHolder army in stale)
        {
            Destroy(army.gameObject);
        }
    }

    private void BroadcastProvinceState()
    {
        long perfStamp = CampaignPerformanceTrace.Stamp();
        int provinceCount = Owners.Instance.provincelist.Count;
        bool fullSnapshot = lastProvinceSignatures == null || lastProvinceSignatures.Length != provinceCount;
        if (fullSnapshot) lastProvinceSignatures = new int[provinceCount];
        List<CampaignProvinceState> changed = provinceStateBuffer;
        changed.Clear();
        regionalFoodRepresentativeBuffer.Clear();
        for (int i = 0; i < Owners.Instance.provincelist.Count; i++)
        {
            Province province = Owners.Instance.provincelist[i];
            int nationIndex = Owners.Instance.nationlist.IndexOf(province.nation);
            CampaignProvinceState state = CampaignProvinceState.FromProvince(i, nationIndex, province);
            CampaignRegion region = Owners.Instance.CallRegionByString(province.region);
            int regionalOwnerKey = ((region != null ? region.GetHashCode() : StableTextHash(province.region)) * 397) ^ nationIndex;
            bool carriesRegionalFoodSignature = regionalFoodRepresentativeBuffer.Add(regionalOwnerKey);
            int signature = 17;
            unchecked
            {
                signature = signature * 31 + state.NationIndex; signature = signature * 31 + state.Supply;
                signature = signature * 31 + state.OccupyingNationIndex;
                signature = signature * 31 + state.Population; signature = signature * 31 + state.Urbanization;
                signature = signature * 31 + state.TerrainProfile;
                // Food storage belongs to a region/owner pair, not every province.
                // One representative carries its change so an economy tick no longer
                // marks every province in the region dirty with duplicate data.
                if (carriesRegionalFoodSignature)
                {
                    signature = signature * 31 + state.RegionalFoodStorage;
                    signature = signature * 31 + state.RegionalFoodStorageCapacity;
                    signature = signature * 31 + state.RegionalFoodShortage;
                    signature = signature * 31 + Mathf.RoundToInt(state.RegionalManpower * 1000f);
                }
            }
            if (fullSnapshot || lastProvinceSignatures[i] != signature) changed.Add(state);
            lastProvinceSignatures[i] = signature;
        }
        if (changed.Count > 0) ReceiveProvinceStateRpc(changed.ToArray());
        double snapshotMs = CampaignPerformanceTrace.MillisecondsSince(perfStamp);
        if (snapshotMs >= 4.0) CampaignPerformanceTrace.Report("Host.ProvinceSnapshot", snapshotMs,
            "changed=" + changed.Count + " total=" + provinceCount);
    }

    private void BroadcastDetailedState()
    {
        long perfStamp = CampaignPerformanceTrace.Stamp();
        List<CampaignUnitState> units = unitStateBuffer;
        units.Clear();
        foreach (FieldArmyHolder army in Owners.Instance.armylist)
        {
            if (army == null || string.IsNullOrEmpty(army.NetworkArmyId) || army.fieldArmy == null)
            {
                continue;
            }
            army.fieldArmy.ReconcileFormationRecords();
            foreach (ArmyFormationRecord record in army.fieldArmy.formationRecords)
            {
                if (record != null && record.unit != null)
                {
                    units.Add(new CampaignUnitState
                    {
                        ArmyId = army.NetworkArmyId,
                        UnitName = record.unit.name,
                        Amount = 1,
                        Origin = (byte)record.origin,
                        EntitlementId = record.entitlementId ?? string.Empty,
                        SourceNationName = record.sourceNationName ?? string.Empty
                    });
                }
            }
        }

        CampaignNationState[] nations = new CampaignNationState[Owners.Instance.nationlist.Count];
        List<CampaignLawState> laws = lawStateBuffer;
        List<CampaignClassRuleState> classRules = classRuleStateBuffer;
        List<CampaignAllegianceState> allegiances = allegianceStateBuffer;
        laws.Clear();
        classRules.Clear();
        allegiances.Clear();
        for (int i = 0; i < Owners.Instance.nationlist.Count; i++)
        {
            Nation nation = Owners.Instance.nationlist[i];
            nation.EnsureDefaultLaws();
            AllegianceSystem.EnsureNationAllegiances(nation);
            nations[i] = new CampaignNationState
            {
                NationIndex = (ushort)i,
                Manpower = nation.Manpower,
                BarracksLevel = nation.faction.BarracksLevel,
                MercenaryLevel = nation.faction.MercLevel,
                FarmLevel = nation.faction.FarmLevel,
                Income = nation.faction.Income,
                Gold = nation.Gold
                ,UpkeepDebt = nation.UpkeepDebt,
                LevyLawPermille = nation.LevyLawPermille
                ,TributaryMasterName = nation.TributaryMasterName ?? string.Empty
                ,PeaceTreatyNationNames = nation.PeaceTreatyNationNames != null
                    ? string.Join("|", nation.PeaceTreatyNationNames) : string.Empty
                ,WarNationNames = nation.WarNationNames != null ? string.Join("|", nation.WarNationNames) : string.Empty
                ,LastWarDeclarationTurn = nation.LastWarDeclarationTurn
                ,PendingPeaceOfferFrom = nation.PendingPeaceOfferFrom ?? string.Empty
                ,PendingPeaceTerms = (byte)nation.PendingPeaceTerms
            };
            foreach (NationalLaw law in nation.laws)
            {
                if (law == null) continue;
                law.EnsureEffectsMigrated();
                foreach (NationalLawEffect effect in law.effects)
                    if (effect != null) laws.Add(new CampaignLawState { NationIndex = (ushort)i,
                        Id = law.id ?? string.Empty, DisplayName = law.displayName ?? string.Empty,
                        AmountPermille = effect.amountPermille, Effect = (byte)effect.type,
                        Operation = (byte)effect.operation, Target = (byte)effect.target,
                        AnySocioEconomicClass = effect.anySocioEconomicClass,
                        SocioEconomicClass = (byte)SocioEconomicClassRules.Normalize(effect.socioEconomicClass), CultureScope = (byte)effect.cultureScope,
                        CultureName = effect.cultureName ?? string.Empty, AnyUnitOrigin = effect.anyUnitOrigin,
                        UnitOrigin = (byte)effect.unitOrigin, AnyAllegiance = effect.anyAllegiance,
                        AllegianceId = effect.allegianceId ?? string.Empty,
                        UseAllegianceFocusedRegions = effect.useAllegianceFocusedRegions });
                if (law.classRules != null) foreach (NationalClassRule rule in law.classRules)
                    if (rule != null) classRules.Add(new CampaignClassRuleState { NationIndex = (ushort)i,
                        LawId = law.id ?? string.Empty, DisplayName = law.displayName ?? string.Empty,
                        Type = (byte)rule.type,
                        AffectedClass = (byte)SocioEconomicClassRules.Normalize(rule.affectedClass),
                        ResultingClass = (byte)SocioEconomicClassRules.Normalize(rule.resultingClass),
                        CultureName = rule.cultureName ?? string.Empty });
            }
            foreach (Allegiance allegiance in nation.allegiances)
                if (allegiance != null) allegiances.Add(new CampaignAllegianceState { NationIndex = (ushort)i,
                    Id = allegiance.id ?? string.Empty, DisplayName = allegiance.displayName ?? string.Empty,
                    Type = (byte)allegiance.type, PrimaryIdentityId = allegiance.primaryIdentityId ?? string.Empty,
                    DynamicIdentityId = allegiance.dynamicIdentityId ?? string.Empty,
                    CurrentInterestRegionIds = JoinAllegianceRegions(allegiance.currentInterestRegionIds),
                    FutureInterestRegionIds = JoinAllegianceRegions(allegiance.futureInterestRegionIds) });
        }
        int lawSignature = LawStateSignature(laws, classRules);
        bool lawsChanged = lawSignature != lastLawStateSignature;
        if (lawsChanged) lastLawStateSignature = lawSignature;
        ReceiveNationStateRpc(nations, lawsChanged ? laws.ToArray() : System.Array.Empty<CampaignLawState>(),
            lawsChanged ? classRules.ToArray() : System.Array.Empty<CampaignClassRuleState>(), lawsChanged);
        int allegianceSignature = AllegianceStateSignature(allegiances);
        if (allegianceSignature != lastAllegianceStateSignature)
        {
            lastAllegianceStateSignature = allegianceSignature;
            ReceiveAllegianceStateRpc(allegiances.ToArray());
        }
        List<CampaignActiveEdictState> activeEdicts = activeEdictStateBuffer;
        activeEdicts.Clear();
        for (int i = 0; i < Owners.Instance.nationlist.Count; i++)
        {
            Nation nation = Owners.Instance.nationlist[i];
            if (nation == null || nation.activeEdicts == null) continue;
            foreach (ActiveNationalEdict active in nation.activeEdicts)
            {
                if (active == null || active.edict == null) continue;
                string target = string.Empty;
                if (active.edict.coreEffects != null)
                {
                    NationalLawEffect targeted = active.edict.coreEffects.Find(effect => effect != null && !effect.anyAllegiance);
                    if (targeted != null) target = targeted.allegianceId ?? string.Empty;
                }
                activeEdicts.Add(new CampaignActiveEdictState { NationIndex = (ushort)i,
                    ExtensionId = active.edict.StableId ?? string.Empty, Title = active.title ?? string.Empty,
                    TargetAllegianceId = target, RemainingTicks = active.remainingTicks,
                    IsAftermath = active.edict.isAftermath });
            }
        }
        int activeEdictSignature = ActiveEdictStateSignature(activeEdicts);
        if (activeEdictSignature != lastActiveEdictStateSignature)
        {
            lastActiveEdictStateSignature = activeEdictSignature;
            ReceiveActiveEdictStateRpc(activeEdicts.ToArray());
        }
        BroadcastQueueState();

        List<CampaignFactionFlagState> flags = factionFlagStateBuffer;
        List<CampaignBuildingState> buildings = buildingStateBuffer;
        List<CampaignMercenaryState> mercenaries = mercenaryStateBuffer;
        List<CampaignLevyState> levies = levyStateBuffer;
        List<CampaignHoldingState> holdings = holdingStateBuffer;
        flags.Clear();
        buildings.Clear();
        mercenaries.Clear();
        levies.Clear();
        holdings.Clear();
        for (int i = 0; i < Owners.Instance.nationlist.Count; i++)
        {
            foreach (string flag in Owners.Instance.nationlist[i].faction.Flaglist)
            {
                flags.Add(new CampaignFactionFlagState { NationIndex = (ushort)i, Flag = flag });
            }
        }
        for (int i = 0; i < Owners.Instance.provincelist.Count; i++)
        {
            Province province = Owners.Instance.provincelist[i];
            foreach (ProvinceBuilding building in province.buildings)
            {
                if (building == null) continue;
                buildings.Add(new CampaignBuildingState
                {
                    ProvinceIndex = (ushort)i, BuildingId = building.BuildingId,
                    Level = building.level, MaxLevel = building.maxLevel, SlotIndex = building.slotIndex
                });
            }
            foreach (ProvinceMercenaryPool pool in province.mercenaryPools)
            {
                if (pool == null || pool.unit == null) continue;
                mercenaries.Add(new CampaignMercenaryState
                {
                    ProvinceIndex = (ushort)i, UnitName = pool.unit.name,
                    Available = pool.available, Capacity = pool.capacity,
                    RegenerationPerTurn = pool.regenerationPerTurn,
                    RegenerationProgress = pool.regenerationProgress
                });
            }
            if (province.holdings != null) foreach (ProvinceHolding holding in province.holdings)
            {
                if (holding == null) continue;
                holdings.Add(new CampaignHoldingState { ProvinceIndex = (ushort)i, InstanceId = holding.instanceId ?? string.Empty,
                    HoldingId = holding.HoldingId,
                    Level = holding.level, SlotIndex = holding.slotIndex, CultureName = holding.cultureName ?? string.Empty,
                    SocioEconomicClass = (byte)SocioEconomicClassRules.Normalize(holding.socioEconomicClass), Allegiance = holding.allegiance ?? string.Empty,
                    LevyEnabled = holding.levyEnabled, AdaptationTargetId = holding.adaptationTargetId ?? string.Empty,
                    AdaptationPressure = holding.adaptationPressure,
                    AdaptationCooldownTicks = holding.adaptationCooldownTicks });
            }
            foreach (ProvinceLevyEntitlement levy in province.levyEntitlements)
                if (levy != null) levies.Add(new CampaignLevyState { ProvinceIndex = (ushort)i,
                    EntitlementId = levy.id ?? string.Empty, RuleId = levy.ruleId ?? string.Empty,
                    UnitName = levy.unitName ?? string.Empty, BuildingSlot = levy.buildingSlot, Ordinal = levy.ordinal,
                    HoldingId = levy.holdingId ?? string.Empty, HoldingInstanceId = levy.holdingInstanceId ?? string.Empty,
                    State = (byte)levy.state, Eligible = levy.eligible, RemainingTicks = levy.remainingTicks,
                    RaisedArmyId = levy.raisedArmyId ?? string.Empty });
        }
        int coreSignature = CoreDetailedStateSignature(units, flags, buildings, mercenaries);
        int holdingSignature = HoldingStateSignature(holdings);
        int levySignature = LevyStateSignature(levies);
        bool coreChanged = coreSignature != lastDetailedStateSignature;
        bool holdingsChanged = holdingSignature != lastHoldingStateSignature;
        bool leviesChanged = levySignature != lastLevyStateSignature;
        if (!coreChanged && !holdingsChanged && !leviesChanged)
        {
            double unchangedMs = CampaignPerformanceTrace.MillisecondsSince(perfStamp);
            if (unchangedMs >= 4.0) CampaignPerformanceTrace.Report("Host.DetailedSnapshot", unchangedMs,
                "unchanged; units=" + units.Count + " holdings=" + holdings.Count + " levies=" + levies.Count);
            return;
        }

        if (coreChanged)
        {
            lastDetailedStateSignature = coreSignature;
            ReceiveDetailedStateRpc(units.ToArray(), flags.ToArray(), buildings.ToArray(), mercenaries.ToArray(),
                System.Array.Empty<CampaignLevyState>());
        }

        // Each large category has its own signature. A levy countdown must not force
        // every holding to be deleted and rebuilt, and a holding transformation must
        // not resend every levy.
        if (holdingsChanged)
        {
            lastHoldingStateSignature = holdingSignature;
            BroadcastHoldingChanges(holdings);
        }

        if (leviesChanged)
        {
            lastLevyStateSignature = levySignature;
            BroadcastLevyChanges(levies);
        }
        double detailedMs = CampaignPerformanceTrace.MillisecondsSince(perfStamp);
        if (detailedMs >= 4.0) CampaignPerformanceTrace.Report("Host.DetailedSnapshot", detailedMs,
            "core=" + coreChanged + " holdings=" + holdingsChanged + " levies=" + leviesChanged +
            " counts[u=" + units.Count + ",h=" + holdings.Count + ",l=" + levies.Count + "]");
    }

    private void BroadcastQueueState()
    {
        List<CampaignRecruitmentOrderState> recruitment = recruitmentStateBuffer;
        recruitment.Clear();
        foreach (FieldArmyHolder army in Owners.Instance.armylist)
        {
            if (army == null || army.fieldArmy == null || string.IsNullOrEmpty(army.NetworkArmyId) ||
                army.fieldArmy.recruitmentOrders == null) continue;
            foreach (ArmyRecruitmentOrder order in army.fieldArmy.recruitmentOrders)
            {
                if (order == null || order.unit == null || order.amount <= 0) continue;
                recruitment.Add(new CampaignRecruitmentOrderState
                {
                    ArmyId = army.NetworkArmyId,
                    UnitName = order.unit.name,
                    Amount = order.amount,
                    RemainingTicks = order.remainingTicks,
                    Origin = (byte)order.origin,
                    SourceNationName = order.sourceNationName ?? string.Empty
                });
            }
        }

        List<CampaignConstructionOrderState> construction = constructionStateBuffer;
        List<CampaignHoldingConstructionOrderState> holdingConstruction = holdingConstructionStateBuffer;
        construction.Clear();
        holdingConstruction.Clear();
        for (int provinceIndex = 0; provinceIndex < Owners.Instance.provincelist.Count; provinceIndex++)
        {
            Province province = Owners.Instance.provincelist[provinceIndex];
            if (province == null || province.constructionOrders == null) continue;
            foreach (ProvinceConstructionOrder order in province.constructionOrders)
            {
                if (order == null || string.IsNullOrEmpty(order.buildingId)) continue;
                construction.Add(new CampaignConstructionOrderState
                {
                    ProvinceIndex = (ushort)provinceIndex,
                    SlotIndex = order.slotIndex,
                    BuildingId = order.buildingId,
                    TargetLevel = order.targetLevel,
                    RemainingTicks = order.remainingTicks
                });
            }
            if (province.holdingConstructionOrders != null)
                foreach (HoldingConstructionOrder order in province.holdingConstructionOrders)
                {
                    if (order == null || string.IsNullOrEmpty(order.holdingId)) continue;
                    holdingConstruction.Add(new CampaignHoldingConstructionOrderState { ProvinceIndex = (ushort)provinceIndex,
                        SlotIndex = order.slotIndex, HoldingInstanceId = order.holdingInstanceId ?? string.Empty,
                        HoldingId = order.holdingId, TargetLevel = order.targetLevel,
                        RemainingTicks = order.remainingTicks });
                }
        }
        int signature = 17;
        unchecked
        {
            foreach (CampaignRecruitmentOrderState state in recruitment)
            {
                signature = signature * 31 + state.ArmyId.GetHashCode();
                signature = signature * 31 + state.UnitName.GetHashCode();
                signature = signature * 31 + state.Amount;
                signature = signature * 31 + state.RemainingTicks;
                signature = signature * 31 + state.Origin;
                signature = signature * 31 + state.SourceNationName.GetHashCode();
            }
            foreach (CampaignConstructionOrderState state in construction)
            {
                signature = signature * 31 + state.ProvinceIndex;
                signature = signature * 31 + state.SlotIndex;
                signature = signature * 31 + state.BuildingId.GetHashCode();
                signature = signature * 31 + state.TargetLevel;
                signature = signature * 31 + state.RemainingTicks;
            }
            foreach (CampaignHoldingConstructionOrderState state in holdingConstruction)
            {
                signature = signature * 31 + state.ProvinceIndex; signature = signature * 31 + state.SlotIndex;
                signature = signature * 31 + state.HoldingInstanceId.GetHashCode();
                signature = signature * 31 + state.HoldingId.GetHashCode(); signature = signature * 31 + state.TargetLevel;
                signature = signature * 31 + state.RemainingTicks;
            }
        }
        if (signature == lastQueueStateSignature) return;
        lastQueueStateSignature = signature;
        ReceiveQueueStateRpc(recruitment.ToArray(), construction.ToArray(), holdingConstruction.ToArray());
    }

    public void BroadcastQueueStateNow()
    {
        if (IsServer && IsOwner && Owners.Instance != null) BroadcastQueueState();
    }

    public void BroadcastLevyQueueStateNow()
    {
        if (!IsServer || !IsOwner || Owners.Instance == null) return;
        List<CampaignLevyState> levies = levyStateBuffer;
        levies.Clear();
        for (int provinceIndex = 0; provinceIndex < Owners.Instance.provincelist.Count; provinceIndex++)
        {
            Province province = Owners.Instance.provincelist[provinceIndex];
            if (province == null || province.levyEntitlements == null) continue;
            foreach (ProvinceLevyEntitlement levy in province.levyEntitlements)
                if (levy != null) levies.Add(new CampaignLevyState { ProvinceIndex = (ushort)provinceIndex,
                    EntitlementId = levy.id ?? string.Empty, RuleId = levy.ruleId ?? string.Empty,
                    UnitName = levy.unitName ?? string.Empty, BuildingSlot = levy.buildingSlot, Ordinal = levy.ordinal,
                    HoldingId = levy.holdingId ?? string.Empty, HoldingInstanceId = levy.holdingInstanceId ?? string.Empty,
                    State = (byte)levy.state, Eligible = levy.eligible, RemainingTicks = levy.remainingTicks,
                    RaisedArmyId = levy.raisedArmyId ?? string.Empty });
        }
        int signature = LevyStateSignature(levies);
        if (signature == lastLevyStateSignature) return;
        lastLevyStateSignature = signature;
        BroadcastLevyChanges(levies);
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveQueueStateRpc(CampaignRecruitmentOrderState[] recruitment,
        CampaignConstructionOrderState[] construction, CampaignHoldingConstructionOrderState[] holdingConstruction)
    {
        if (Owners.Instance == null) return;
        long perfStamp = CampaignPerformanceTrace.Stamp();
        foreach (FieldArmyHolder army in Owners.Instance.armylist)
        {
            if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null) continue;
            if (RecruitmentQueueMatches(army, recruitment)) continue;
            army.fieldArmy.recruitmentOrders.Clear();
            foreach (CampaignRecruitmentOrderState state in recruitment)
            {
                if (state.ArmyId.ToString() != army.NetworkArmyId) continue;
                UnitSaveData unit = FindUnit(army.fieldArmy.nation, state.UnitName.ToString());
                if (unit == null) continue;
                army.fieldArmy.recruitmentOrders.Add(new ArmyRecruitmentOrder
                {
                    unit = unit, amount = state.Amount, remainingTicks = state.RemainingTicks,
                    origin = (CampaignUnitOrigin)Mathf.Clamp(state.Origin, 0, 3),
                    sourceNationName = state.SourceNationName.ToString()
                });
            }
            // Only the currently displayed army can have visible queue content;
            // rebuilding every army's menu caused a large client hitch each tick.
            if (FieldArmyHolder.InspectedArmy == army || FieldArmyHolder.SelectedPlayerArmy == army)
                RecruitmentMenu.RefreshQueueFor(army.fieldArmy);
        }

        for (int provinceIndex = 0; provinceIndex < Owners.Instance.provincelist.Count; provinceIndex++)
        {
            Province province = Owners.Instance.provincelist[provinceIndex];
            if (province == null) continue;
            if (!ConstructionQueueMatches(province, provinceIndex, construction))
            {
                province.constructionOrders.Clear();
                foreach (CampaignConstructionOrderState state in construction)
                    if (state.ProvinceIndex == provinceIndex)
                        province.constructionOrders.Add(new ProvinceConstructionOrder { slotIndex = state.SlotIndex,
                            buildingId = state.BuildingId.ToString(), targetLevel = state.TargetLevel,
                            remainingTicks = state.RemainingTicks });
            }
            if (!HoldingConstructionQueueMatches(province, provinceIndex, holdingConstruction))
            {
                province.holdingConstructionOrders.Clear();
                foreach (CampaignHoldingConstructionOrderState state in holdingConstruction)
                    if (state.ProvinceIndex == provinceIndex)
                        province.holdingConstructionOrders.Add(new HoldingConstructionOrder { slotIndex = state.SlotIndex,
                            holdingInstanceId = state.HoldingInstanceId.ToString(), holdingId = state.HoldingId.ToString(),
                            targetLevel = state.TargetLevel, remainingTicks = state.RemainingTicks });
            }
        }
        double queueStateMs = CampaignPerformanceTrace.MillisecondsSince(perfStamp);
        if (queueStateMs >= 4.0) CampaignPerformanceTrace.Report("Client.QueueState", queueStateMs,
            "recruit=" + recruitment.Length + " build=" + construction.Length + " holdings=" + holdingConstruction.Length);
    }

    private static bool RecruitmentQueueMatches(FieldArmyHolder army, CampaignRecruitmentOrderState[] states)
    {
        int expected = 0; foreach (CampaignRecruitmentOrderState state in states)
            if (state.ArmyId.ToString() == army.NetworkArmyId) expected++;
        if (army.fieldArmy.recruitmentOrders.Count != expected) return false;
        int index = 0;
        foreach (CampaignRecruitmentOrderState state in states)
        {
            if (state.ArmyId.ToString() != army.NetworkArmyId) continue;
            ArmyRecruitmentOrder order = army.fieldArmy.recruitmentOrders[index++];
            if (order == null || order.unit == null || order.unit.name != state.UnitName.ToString() ||
                order.amount != state.Amount || order.remainingTicks != state.RemainingTicks ||
                (byte)order.origin != state.Origin ||
                (order.sourceNationName ?? string.Empty) != state.SourceNationName.ToString()) return false;
        }
        return true;
    }

    private static bool ConstructionQueueMatches(Province province, int provinceIndex, CampaignConstructionOrderState[] states)
    {
        int expected = 0; foreach (CampaignConstructionOrderState state in states) if (state.ProvinceIndex == provinceIndex) expected++;
        if (province.constructionOrders.Count != expected) return false;
        int index = 0; foreach (CampaignConstructionOrderState state in states)
        {
            if (state.ProvinceIndex != provinceIndex) continue;
            ProvinceConstructionOrder order = province.constructionOrders[index++];
            if (order == null || order.slotIndex != state.SlotIndex || order.buildingId != state.BuildingId.ToString() ||
                order.targetLevel != state.TargetLevel || order.remainingTicks != state.RemainingTicks) return false;
        }
        return true;
    }

    private static bool HoldingConstructionQueueMatches(Province province, int provinceIndex,
        CampaignHoldingConstructionOrderState[] states)
    {
        int expected = 0; foreach (CampaignHoldingConstructionOrderState state in states) if (state.ProvinceIndex == provinceIndex) expected++;
        if (province.holdingConstructionOrders.Count != expected) return false;
        int index = 0; foreach (CampaignHoldingConstructionOrderState state in states)
        {
            if (state.ProvinceIndex != provinceIndex) continue;
            HoldingConstructionOrder order = province.holdingConstructionOrders[index++];
            if (order == null || order.slotIndex != state.SlotIndex || order.holdingInstanceId != state.HoldingInstanceId.ToString() ||
                order.holdingId != state.HoldingId.ToString() || order.targetLevel != state.TargetLevel ||
                order.remainingTicks != state.RemainingTicks) return false;
        }
        return true;
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveNationStateRpc(CampaignNationState[] nations, CampaignLawState[] laws,
        CampaignClassRuleState[] classRules, bool replaceLaws)
    {
        if (Owners.Instance == null) return;
        long perfStamp = CampaignPerformanceTrace.Stamp();
        bool diplomacyChanged = false;
        foreach (CampaignNationState state in nations)
        {
            if (state.NationIndex >= Owners.Instance.nationlist.Count) continue;
            Nation nation = Owners.Instance.nationlist[state.NationIndex];
            nation.Manpower = state.Manpower;
            nation.faction.BarracksLevel = state.BarracksLevel;
            nation.faction.MercLevel = state.MercenaryLevel;
            nation.faction.FarmLevel = state.FarmLevel;
            nation.faction.Income = state.Income;
            nation.Gold = state.Gold;
            nation.UpkeepDebt = state.UpkeepDebt;
            nation.LevyLawPermille = Mathf.Clamp(state.LevyLawPermille, 0, 1000);
            string receivedMaster = state.TributaryMasterName.ToString();
            if (nation.TributaryMasterName != receivedMaster) diplomacyChanged = true;
            nation.TributaryMasterName = receivedMaster;
            string peaceNames = state.PeaceTreatyNationNames.ToString();
            nation.PeaceTreatyNationNames = string.IsNullOrWhiteSpace(peaceNames)
                ? new List<string>() : new List<string>(peaceNames.Split('|'));
            string warNames = state.WarNationNames.ToString();
            nation.WarNationNames = string.IsNullOrWhiteSpace(warNames)
                ? new List<string>() : new List<string>(warNames.Split('|'));
            nation.LastWarDeclarationTurn = state.LastWarDeclarationTurn;
            nation.PendingPeaceOfferFrom = state.PendingPeaceOfferFrom.ToString();
            nation.PendingPeaceTerms = (BasicPeaceTerms)Mathf.Clamp(state.PendingPeaceTerms, 0, 3);
        }
        if (diplomacyChanged && Mapshower.Instance != null) Mapshower.Instance.RePaint();
        if (!replaceLaws)
        {
            double nationStateMs = CampaignPerformanceTrace.MillisecondsSince(perfStamp);
            if (nationStateMs >= 4.0) CampaignPerformanceTrace.Report("Client.NationState", nationStateMs,
                "nations=" + nations.Length + " laws=unchanged");
            return;
        }
        foreach (Nation nation in Owners.Instance.nationlist)
            if (nation != null) { nation.laws = new List<NationalLaw>(); nation.ResetLawResolution(); }
        foreach (CampaignLawState state in laws)
        {
            if (state.NationIndex >= Owners.Instance.nationlist.Count) continue;
            Nation nation = Owners.Instance.nationlist[state.NationIndex];
            string lawId = state.Id.ToString();
            NationalLaw law = nation.laws.Find(candidate => candidate != null && candidate.id == lawId);
            if (law == null) { law = new NationalLaw { id = lawId, displayName = state.DisplayName.ToString() }; nation.laws.Add(law); }
            law.effects.Add(new NationalLawEffect { amountPermille = Mathf.Clamp(state.AmountPermille, -5000, 5000),
                type = (NationalLawEffectType)Mathf.Clamp(state.Effect, 0, 7),
                operation = (NationalLawOperation)Mathf.Clamp(state.Operation, 0, 3),
                target = (NationalLawTarget)Mathf.Clamp(state.Target, 0, 3),
                anySocioEconomicClass = state.AnySocioEconomicClass,
                socioEconomicClass = SocioEconomicClassRules.Normalize(
                    (SocioEconomicClass)Mathf.Clamp(state.SocioEconomicClass, 0, 8)),
                cultureScope = (NationalLawCultureScope)Mathf.Clamp(state.CultureScope, 0, 3),
                cultureName = state.CultureName.ToString(), anyUnitOrigin = state.AnyUnitOrigin,
                unitOrigin = (CampaignUnitOrigin)Mathf.Clamp(state.UnitOrigin, 0, 3),
                anyAllegiance = state.AnyAllegiance, allegianceId = state.AllegianceId.ToString(),
                useAllegianceFocusedRegions = state.UseAllegianceFocusedRegions });
        }
        foreach (CampaignClassRuleState state in classRules)
        {
            if (state.NationIndex >= Owners.Instance.nationlist.Count) continue;
            Nation nation = Owners.Instance.nationlist[state.NationIndex];
            string lawId = state.LawId.ToString();
            NationalLaw law = nation.laws.Find(candidate => candidate != null && candidate.id == lawId);
            if (law == null) { law = new NationalLaw { id = lawId, displayName = state.DisplayName.ToString() }; nation.laws.Add(law); }
            law.classRules.Add(new NationalClassRule { type = (NationalClassRuleType)Mathf.Clamp(state.Type, 0, 1),
                affectedClass = SocioEconomicClassRules.Normalize(
                    (SocioEconomicClass)Mathf.Clamp(state.AffectedClass, 0, 8)),
                resultingClass = SocioEconomicClassRules.Normalize(
                    (SocioEconomicClass)Mathf.Clamp(state.ResultingClass, 0, 8)),
                cultureName = state.CultureName.ToString() });
        }
        foreach (Nation nation in Owners.Instance.nationlist) nation.EnsureDefaultLaws();
        double lawStateMs = CampaignPerformanceTrace.MillisecondsSince(perfStamp);
        if (lawStateMs >= 4.0) CampaignPerformanceTrace.Report("Client.NationState", lawStateMs,
            "nations=" + nations.Length + " laws=" + laws.Length + " rules=" + classRules.Length);
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveDetailedStateRpc(CampaignUnitState[] units,
        CampaignFactionFlagState[] flags, CampaignBuildingState[] buildings, CampaignMercenaryState[] mercenaries,
        CampaignLevyState[] levies)
    {
        if (Owners.Instance == null)
        {
            return;
        }

        foreach (Nation nation in Owners.Instance.nationlist)
            if (nation != null && nation.faction != null) nation.faction.Flaglist.Clear();
        foreach (CampaignFactionFlagState flag in flags)
        {
            if (flag.NationIndex < Owners.Instance.nationlist.Count)
            {
                Owners.Instance.nationlist[flag.NationIndex].faction.Flaglist.Add(flag.Flag.ToString());
            }
        }

        Dictionary<int, List<CampaignBuildingState>> buildingsByProvince = new Dictionary<int, List<CampaignBuildingState>>();
        foreach (CampaignBuildingState state in buildings)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count) continue;
            int provinceIndex = state.ProvinceIndex;
            if (!buildingsByProvince.TryGetValue(provinceIndex, out List<CampaignBuildingState> list))
            {
                list = new List<CampaignBuildingState>();
                buildingsByProvince.Add(provinceIndex, list);
            }
            list.Add(state);
        }
        for (int provinceIndex = 0; provinceIndex < Owners.Instance.provincelist.Count; provinceIndex++)
        {
            Province province = Owners.Instance.provincelist[provinceIndex];
            buildingsByProvince.TryGetValue(provinceIndex, out List<CampaignBuildingState> expected);
            if (BuildingStateMatches(province, expected)) continue;
            province.buildings.Clear();
            if (expected != null) foreach (CampaignBuildingState state in expected)
            {
                string buildingId = state.BuildingId.ToString();
                province.buildings.Add(new ProvinceBuilding
                {
                    definition = BuildingDefinition.Find(buildingId), id = buildingId, level = state.Level,
                    maxLevel = Mathf.Max(1, state.MaxLevel),
                    slotIndex = state.SlotIndex
                });
            }
            province.RefreshGarrisonForFort();
        }

        // Mercenaries are currently disabled; avoid rebuilding dormant pools on every detailed update.
        if (ProvinceMercenaryPool.Enabled) ApplyMercenaryState(mercenaries);

        foreach (Province province in Owners.Instance.provincelist) province.levyEntitlements.Clear();
        foreach (CampaignLevyState state in levies)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count) continue;
            Province province = Owners.Instance.provincelist[state.ProvinceIndex];
            province.levyEntitlements.Add(new ProvinceLevyEntitlement { id = state.EntitlementId.ToString(),
                ruleId = state.RuleId.ToString(), unitName = state.UnitName.ToString(), unit = FindUnit(province.nation, state.UnitName.ToString()),
                buildingSlot = state.BuildingSlot, ordinal = state.Ordinal, beneficiaryNation = province.nation != null ? province.nation.name : string.Empty,
                holdingId = state.HoldingId.ToString(), holdingInstanceId = state.HoldingInstanceId.ToString(),
                state = (LevyEntitlementState)Mathf.Clamp(state.State, 0, 3), eligible = state.Eligible,
                remainingTicks = state.RemainingTicks, raisedArmyId = state.RaisedArmyId.ToString() });
        }

        Dictionary<string, List<CampaignUnitState>> byArmy = new Dictionary<string, List<CampaignUnitState>>();
        foreach (CampaignUnitState unit in units)
        {
            string armyId = unit.ArmyId.ToString();
            if (!byArmy.TryGetValue(armyId, out List<CampaignUnitState> list))
            {
                list = new List<CampaignUnitState>();
                byArmy.Add(armyId, list);
            }
            list.Add(unit);
        }

        foreach (FieldArmyHolder army in Owners.Instance.armylist)
        {
            if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null) continue;
            byArmy.TryGetValue(army.NetworkArmyId, out List<CampaignUnitState> expected);
            if (ArmyCompositionMatches(army.fieldArmy, expected)) continue;
            army.fieldArmy.USDReserves.Clear();
            army.fieldArmy.formationRecords.Clear();
            if (expected == null) continue;
            foreach (CampaignUnitState unitState in expected)
            {
                UnitSaveData unit = FindUnit(army.fieldArmy.nation, unitState.UnitName.ToString());
                if (unit != null) army.fieldArmy.AddTroop(unit, unitState.Amount, true,
                    (CampaignUnitOrigin)Mathf.Clamp(unitState.Origin, 0, 3), unitState.EntitlementId.ToString(),
                    unitState.SourceNationName.ToString());
            }
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveActiveEdictStateRpc(CampaignActiveEdictState[] states)
    {
        if (Owners.Instance == null) return;
        foreach (Nation nation in Owners.Instance.nationlist)
            if (nation != null) nation.activeEdicts = new List<ActiveNationalEdict>();
        foreach (CampaignActiveEdictState state in states)
        {
            if (state.NationIndex >= Owners.Instance.nationlist.Count) continue;
            Nation nation = Owners.Instance.nationlist[state.NationIndex];
            nation.EnsureDefaultLaws();
            string id = state.ExtensionId.ToString();
            string sourceId = state.IsAftermath && id.EndsWith("_aftermath", System.StringComparison.OrdinalIgnoreCase)
                ? id.Substring(0, id.Length - "_aftermath".Length) : id;
            NationalEdict template = null;
            foreach (NationalLaw law in nation.laws)
            {
                if (law == null || law.availableExtensions == null) continue;
                template = law.availableExtensions.Find(extension => extension != null &&
                    string.Equals(extension.StableId, sourceId, System.StringComparison.OrdinalIgnoreCase));
                if (template != null) break;
            }
            if (template == null) continue;
            NationalEdict edict = template.Clone();
            if (state.IsAftermath)
            {
                edict.extensionId = id; edict.displayName = state.Title.ToString(); edict.isAftermath = true;
                edict.coreEffects = edict.aftermathEffects;
                edict.aftermathEffects = new List<NationalLawEffect>();
                edict.aftermathType = EdictAftermathType.None;
            }
            string target = state.TargetAllegianceId.ToString();
            if (!string.IsNullOrWhiteSpace(target) && edict.coreEffects != null)
                foreach (NationalLawEffect effect in edict.coreEffects)
                    if (effect != null && !effect.anyAllegiance) effect.allegianceId = target;
            nation.activeEdicts.Add(new ActiveNationalEdict { instanceId = id,
                title = state.Title.ToString(), edict = edict, remainingTicks = Mathf.Max(1, state.RemainingTicks) });
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveHoldingStateRpc(CampaignHoldingState[] holdings, bool reset, bool finalChunk)
    {
        if (Owners.Instance == null) return;
        if (reset)
            foreach (Province province in Owners.Instance.provincelist)
                if (province != null) province.holdings.Clear();

        foreach (CampaignHoldingState state in holdings)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count) continue;
            string holdingId = state.HoldingId.ToString();
            Province province = Owners.Instance.provincelist[state.ProvinceIndex];
            ProvinceHolding holding = new ProvinceHolding {
                instanceId = state.InstanceId.ToString(),
                definition = HoldingDefinition.Find(holdingId), id = holdingId, level = Mathf.Max(1, state.Level),
                slotIndex = state.SlotIndex, cultureName = state.CultureName.ToString(),
                socioEconomicClass = SocioEconomicClassRules.Normalize(
                    (SocioEconomicClass)Mathf.Clamp(state.SocioEconomicClass, 0, 8)),
                allegiance = state.Allegiance.ToString(),
                levyEnabled = state.LevyEnabled, adaptationTargetId = state.AdaptationTargetId.ToString(),
                adaptationPressure = Mathf.Max(0, state.AdaptationPressure),
                adaptationCooldownTicks = Mathf.Max(0, state.AdaptationCooldownTicks) };
            province.nation?.ApplyHoldingClassLaws(holding);
            province.holdings.Add(holding);
        }

        if (finalChunk)
        {
            foreach (Province province in Owners.Instance.provincelist)
                if (province != null) province.RebuildPopulationFromHoldings();
            foreach (Province province in Owners.Instance.provincelist)
                if (province != null) province.ReconcileLevyEntitlements();
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveHoldingDeltaRpc(CampaignHoldingState[] holdings)
    {
        if (Owners.Instance == null) return;
        HashSet<Province> changedProvinces = new HashSet<Province>();
        foreach (CampaignHoldingState state in holdings)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count) continue;
            Province province = Owners.Instance.provincelist[state.ProvinceIndex];
            string instanceId = state.InstanceId.ToString();
            string holdingId = state.HoldingId.ToString();
            ProvinceHolding holding = province.holdings.Find(candidate =>
                candidate != null && candidate.instanceId == instanceId);
            if (holding == null)
            {
                holding = new ProvinceHolding { instanceId = instanceId };
                province.holdings.Add(holding);
            }
            holding.definition = HoldingDefinition.Find(holdingId); holding.id = holdingId;
            holding.level = Mathf.Max(1, state.Level); holding.slotIndex = state.SlotIndex;
            holding.cultureName = state.CultureName.ToString();
            holding.socioEconomicClass = SocioEconomicClassRules.Normalize(
                (SocioEconomicClass)Mathf.Clamp(state.SocioEconomicClass, 0, 8));
            holding.allegiance = state.Allegiance.ToString(); holding.levyEnabled = state.LevyEnabled;
            holding.adaptationTargetId = state.AdaptationTargetId.ToString();
            holding.adaptationPressure = Mathf.Max(0, state.AdaptationPressure);
            holding.adaptationCooldownTicks = Mathf.Max(0, state.AdaptationCooldownTicks);
            province.nation?.ApplyHoldingClassLaws(holding);
            changedProvinces.Add(province);
        }
        foreach (Province province in changedProvinces)
        {
            province.RebuildPopulationFromHoldings();
            province.ReconcileLevyEntitlements();
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveLevyStateRpc(CampaignLevyState[] levies, bool reset, bool finalChunk)
    {
        if (Owners.Instance == null) return;
        if (reset)
            foreach (Province province in Owners.Instance.provincelist)
                if (province != null) province.levyEntitlements.Clear();

        foreach (CampaignLevyState state in levies)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count) continue;
            Province province = Owners.Instance.provincelist[state.ProvinceIndex];
            province.levyEntitlements.Add(new ProvinceLevyEntitlement { id = state.EntitlementId.ToString(),
                ruleId = state.RuleId.ToString(), unitName = state.UnitName.ToString(),
                unit = FindUnit(province.nation, state.UnitName.ToString()), buildingSlot = state.BuildingSlot,
                ordinal = state.Ordinal, beneficiaryNation = province.nation != null ? province.nation.name : string.Empty,
                holdingId = state.HoldingId.ToString(), holdingInstanceId = state.HoldingInstanceId.ToString(),
                state = (LevyEntitlementState)Mathf.Clamp(state.State, 0, 3), eligible = state.Eligible,
                remainingTicks = state.RemainingTicks, raisedArmyId = state.RaisedArmyId.ToString() });
        }

        // The recovery display cache is frame-scoped and refreshes itself after this RPC.
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveLevyDeltaRpc(CampaignLevyState[] levies)
    {
        if (Owners.Instance == null) return;
        foreach (CampaignLevyState state in levies)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count) continue;
            Province province = Owners.Instance.provincelist[state.ProvinceIndex];
            string entitlementId = state.EntitlementId.ToString();
            ProvinceLevyEntitlement levy = province.levyEntitlements.Find(candidate =>
                candidate != null && candidate.id == entitlementId);
            if (levy == null)
            {
                levy = new ProvinceLevyEntitlement { id = entitlementId };
                province.levyEntitlements.Add(levy);
            }
            string unitName = state.UnitName.ToString();
            if (levy.unit == null || levy.unitName != unitName) levy.unit = FindUnit(province.nation, unitName);
            levy.ruleId = state.RuleId.ToString(); levy.unitName = unitName;
            levy.buildingSlot = state.BuildingSlot; levy.ordinal = state.Ordinal;
            levy.beneficiaryNation = province.nation != null ? province.nation.name : string.Empty;
            levy.holdingId = state.HoldingId.ToString(); levy.holdingInstanceId = state.HoldingInstanceId.ToString();
            levy.state = (LevyEntitlementState)Mathf.Clamp(state.State, 0, 3); levy.eligible = state.Eligible;
            levy.remainingTicks = state.RemainingTicks; levy.raisedArmyId = state.RaisedArmyId.ToString();
        }
        FieldArmyHolder visible = FieldArmyHolder.InspectedArmy != null
            ? FieldArmyHolder.InspectedArmy : FieldArmyHolder.SelectedPlayerArmy;
        if (visible != null && visible.fieldArmy != null) RecruitmentMenu.RefreshQueueFor(visible.fieldArmy);
    }

    private static bool BuildingStateMatches(Province province, List<CampaignBuildingState> expected)
    {
        int expectedCount = expected != null ? expected.Count : 0;
        if (province == null || province.buildings == null || province.buildings.Count != expectedCount) return false;
        foreach (CampaignBuildingState state in expected)
        {
            string id = state.BuildingId.ToString();
            ProvinceBuilding match = province.buildings.Find(building => building != null &&
                building.slotIndex == state.SlotIndex && string.Equals(building.BuildingId, id, System.StringComparison.OrdinalIgnoreCase));
            if (match == null || match.level != state.Level || match.maxLevel != state.MaxLevel) return false;
        }
        return true;
    }

    private static bool ArmyCompositionMatches(FieldArmy army, List<CampaignUnitState> expected)
    {
        int expectedCount = expected != null ? expected.Count : 0;
        army.ReconcileFormationRecords();
        if (army.formationRecords.Count != expectedCount) return false;
        if (expected == null) return true;
        List<ArmyFormationRecord> unmatched = new List<ArmyFormationRecord>(army.formationRecords);
        foreach (CampaignUnitState state in expected)
        {
            int index = unmatched.FindIndex(record => record != null && record.unit != null &&
                record.unit.name == state.UnitName.ToString() && (byte)record.origin == state.Origin &&
                (record.entitlementId ?? string.Empty) == state.EntitlementId.ToString() &&
                (record.sourceNationName ?? string.Empty) == state.SourceNationName.ToString());
            if (index < 0) return false;
            unmatched.RemoveAt(index);
        }
        return true;
    }

    private static void ApplyMercenaryState(CampaignMercenaryState[] mercenaries)
    {
        foreach (Province province in Owners.Instance.provincelist) province.mercenaryPools.Clear();
        foreach (CampaignMercenaryState state in mercenaries)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count) continue;
            Province province = Owners.Instance.provincelist[state.ProvinceIndex];
            Nation localNation = province.OriginalNation != null ? province.OriginalNation : province.nation;
            UnitSaveData unit = FindUnit(localNation, state.UnitName.ToString());
            if (unit == null) continue;
            province.mercenaryPools.Add(new ProvinceMercenaryPool
            {
                unit = unit, available = state.Available, capacity = state.Capacity,
                regenerationPerTurn = state.RegenerationPerTurn,
                regenerationProgress = state.RegenerationProgress
            });
        }
    }

    private static FixedString512Bytes JoinAllegianceRegions(List<string> regions)
    {
        string joined = regions != null ? string.Join("|", regions) : string.Empty;
        if (joined.Length > 500) joined = joined.Substring(0, 500);
        return new FixedString512Bytes(joined);
    }

    private static List<string> SplitAllegianceRegions(FixedString512Bytes regions)
    {
        List<string> result = new List<string>();
        foreach (string value in regions.ToString().Split('|'))
            if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value.Trim())) result.Add(value.Trim());
        return result;
    }

    private static int AllegianceStateSignature(List<CampaignAllegianceState> states)
    {
        unchecked
        {
            int hash = 17;
            foreach (CampaignAllegianceState state in states)
            {
                hash = hash * 31 + state.NationIndex; hash = hash * 31 + state.Id.GetHashCode();
                hash = hash * 31 + state.DisplayName.GetHashCode(); hash = hash * 31 + state.Type;
                hash = hash * 31 + state.PrimaryIdentityId.GetHashCode();
                hash = hash * 31 + state.DynamicIdentityId.GetHashCode();
                hash = hash * 31 + state.CurrentInterestRegionIds.GetHashCode();
                hash = hash * 31 + state.FutureInterestRegionIds.GetHashCode();
            }
            return hash;
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveAllegianceStateRpc(CampaignAllegianceState[] states)
    {
        if (Owners.Instance == null) return;
        foreach (Nation nation in Owners.Instance.nationlist) if (nation != null)
        {
            if (nation.allegiances == null) nation.allegiances = new List<Allegiance>();
            else nation.allegiances.Clear();
        }
        foreach (CampaignAllegianceState state in states)
        {
            if (state.NationIndex >= Owners.Instance.nationlist.Count) continue;
            Nation nation = Owners.Instance.nationlist[state.NationIndex];
            nation.allegiances.Add(new Allegiance { id = state.Id.ToString(), displayName = state.DisplayName.ToString(),
                type = (AllegianceType)Mathf.Clamp(state.Type, 0, 1),
                primaryIdentityId = state.PrimaryIdentityId.ToString(), dynamicIdentityId = state.DynamicIdentityId.ToString(),
                currentInterestRegionIds = SplitAllegianceRegions(state.CurrentInterestRegionIds),
                futureInterestRegionIds = SplitAllegianceRegions(state.FutureInterestRegionIds) });
        }
        foreach (Nation nation in Owners.Instance.nationlist) if (nation != null) PoliticalProposalSystem.EnsureGroups(nation);
    }

    private static int LawStateSignature(List<CampaignLawState> laws, List<CampaignClassRuleState> rules)
    {
        unchecked
        {
            int hash = 17;
            foreach (CampaignLawState state in laws)
            {
                hash = hash * 31 + state.NationIndex; hash = hash * 31 + state.Id.GetHashCode();
                hash = hash * 31 + state.AmountPermille; hash = hash * 31 + state.Effect;
                hash = hash * 31 + state.Operation; hash = hash * 31 + state.Target;
                hash = hash * 31 + state.SocioEconomicClass; hash = hash * 31 + state.CultureScope;
                hash = hash * 31 + state.CultureName.GetHashCode(); hash = hash * 31 + state.UnitOrigin;
                hash = hash * 31 + (state.AnyAllegiance ? 1 : 0); hash = hash * 31 + state.AllegianceId.GetHashCode();
                hash = hash * 31 + (state.UseAllegianceFocusedRegions ? 1 : 0);
            }
            foreach (CampaignClassRuleState state in rules)
            {
                hash = hash * 31 + state.NationIndex; hash = hash * 31 + state.LawId.GetHashCode();
                hash = hash * 31 + state.Type; hash = hash * 31 + state.AffectedClass;
                hash = hash * 31 + state.ResultingClass; hash = hash * 31 + state.CultureName.GetHashCode();
            }
            return hash;
        }
    }

    private static int ActiveEdictStateSignature(List<CampaignActiveEdictState> states)
    {
        unchecked
        {
            int hash = 17;
            foreach (CampaignActiveEdictState state in states)
            {
                hash = hash * 31 + state.NationIndex;
                hash = hash * 31 + state.ExtensionId.GetHashCode();
                hash = hash * 31 + state.Title.GetHashCode();
                hash = hash * 31 + state.TargetAllegianceId.GetHashCode();
                hash = hash * 31 + state.RemainingTicks;
                hash = hash * 31 + (state.IsAftermath ? 1 : 0);
            }
            return hash;
        }
    }

    private static int CoreDetailedStateSignature(List<CampaignUnitState> units,
        List<CampaignFactionFlagState> flags, List<CampaignBuildingState> buildings,
        List<CampaignMercenaryState> mercenaries)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + units.Count;
            foreach (CampaignUnitState state in units)
            {
                hash = hash * 31 + state.ArmyId.GetHashCode();
                hash = hash * 31 + state.UnitName.GetHashCode();
                hash = hash * 31 + state.Amount;
                hash = hash * 31 + state.Origin;
                hash = hash * 31 + state.EntitlementId.GetHashCode();
            }
            hash = hash * 31 + flags.Count;
            foreach (CampaignFactionFlagState state in flags)
            {
                hash = hash * 31 + state.NationIndex;
                hash = hash * 31 + state.Flag.GetHashCode();
            }
            hash = hash * 31 + buildings.Count;
            foreach (CampaignBuildingState state in buildings)
            {
                hash = hash * 31 + state.ProvinceIndex;
                hash = hash * 31 + state.BuildingId.GetHashCode();
                hash = hash * 31 + state.Level;
                hash = hash * 31 + state.MaxLevel;
                hash = hash * 31 + state.SlotIndex;
            }
            hash = hash * 31 + mercenaries.Count;
            foreach (CampaignMercenaryState state in mercenaries)
            {
                hash = hash * 31 + state.ProvinceIndex;
                hash = hash * 31 + state.UnitName.GetHashCode();
                hash = hash * 31 + state.Available;
                hash = hash * 31 + state.Capacity;
                hash = hash * 31 + state.RegenerationPerTurn.GetHashCode();
                hash = hash * 31 + state.RegenerationProgress.GetHashCode();
            }
            return hash;
        }
    }

    private static int HoldingStateSignature(List<CampaignHoldingState> holdings)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + holdings.Count;
            foreach (CampaignHoldingState state in holdings)
            {
                hash = hash * 31 + state.ProvinceIndex; hash = hash * 31 + state.HoldingId.GetHashCode();
                hash = hash * 31 + state.InstanceId.GetHashCode(); hash = hash * 31 + state.Level;
                hash = hash * 31 + state.SlotIndex; hash = hash * 31 + state.CultureName.GetHashCode();
                hash = hash * 31 + state.SocioEconomicClass; hash = hash * 31 + state.Allegiance.GetHashCode();
                hash = hash * 31 + state.LevyEnabled.GetHashCode();
            }
            return hash;
        }
    }

    private static int LevyStateSignature(List<CampaignLevyState> levies)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + levies.Count;
            foreach (CampaignLevyState state in levies)
            {
                hash = hash * 31 + state.ProvinceIndex; hash = hash * 31 + state.EntitlementId.GetHashCode();
                hash = hash * 31 + state.State; hash = hash * 31 + state.Eligible.GetHashCode();
                hash = hash * 31 + state.RemainingTicks; hash = hash * 31 + state.RaisedArmyId.GetHashCode();
            }
            return hash;
        }
    }

    private void BroadcastHoldingChanges(List<CampaignHoldingState> holdings)
    {
        Dictionary<string, int> current = new Dictionary<string, int>(holdings.Count);
        List<CampaignHoldingState> changed = new List<CampaignHoldingState>();
        bool full = lastHoldingRecordSignatures.Count != holdings.Count;
        foreach (CampaignHoldingState state in holdings)
        {
            string key = state.ProvinceIndex + "|" + state.InstanceId.ToString();
            int signature = HoldingRecordSignature(state);
            current[key] = signature;
            if (!lastHoldingRecordSignatures.TryGetValue(key, out int previous) || previous != signature)
                changed.Add(state);
        }
        if (!full)
            foreach (string key in lastHoldingRecordSignatures.Keys)
                if (!current.ContainsKey(key)) { full = true; break; }
        lastHoldingRecordSignatures.Clear();
        foreach (KeyValuePair<string, int> pair in current) lastHoldingRecordSignatures[pair.Key] = pair.Value;

        if (full)
        {
            const int chunkSize = 48;
            if (holdings.Count == 0) ReceiveHoldingStateRpc(System.Array.Empty<CampaignHoldingState>(), true, true);
            else for (int offset = 0; offset < holdings.Count; offset += chunkSize)
            {
                int count = Mathf.Min(chunkSize, holdings.Count - offset);
                ReceiveHoldingStateRpc(holdings.GetRange(offset, count).ToArray(), offset == 0,
                    offset + count >= holdings.Count);
            }
        }
        else if (changed.Count > 0)
        {
            const int chunkSize = 48;
            for (int offset = 0; offset < changed.Count; offset += chunkSize)
            {
                int count = Mathf.Min(chunkSize, changed.Count - offset);
                ReceiveHoldingDeltaRpc(changed.GetRange(offset, count).ToArray());
            }
        }
    }

    private static int HoldingRecordSignature(CampaignHoldingState state)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + state.HoldingId.GetHashCode(); hash = hash * 31 + state.Level;
            hash = hash * 31 + state.SlotIndex; hash = hash * 31 + state.CultureName.GetHashCode();
            hash = hash * 31 + state.SocioEconomicClass; hash = hash * 31 + state.Allegiance.GetHashCode();
            hash = hash * 31 + state.LevyEnabled.GetHashCode(); return hash;
        }
    }

    private void BroadcastLevyChanges(List<CampaignLevyState> levies)
    {
        Dictionary<string, int> current = new Dictionary<string, int>(levies.Count);
        List<CampaignLevyState> changed = new List<CampaignLevyState>();
        bool full = lastLevyRecordSignatures.Count != levies.Count;
        foreach (CampaignLevyState state in levies)
        {
            string key = state.ProvinceIndex + "|" + state.EntitlementId.ToString();
            int signature = LevyRecordSignature(state);
            current[key] = signature;
            if (!lastLevyRecordSignatures.TryGetValue(key, out int previous) || previous != signature)
                changed.Add(state);
        }
        if (!full)
            foreach (string key in lastLevyRecordSignatures.Keys)
                if (!current.ContainsKey(key)) { full = true; break; }

        lastLevyRecordSignatures.Clear();
        foreach (KeyValuePair<string, int> pair in current) lastLevyRecordSignatures[pair.Key] = pair.Value;

        if (full)
        {
            const int chunkSize = 32;
            if (levies.Count == 0) ReceiveLevyStateRpc(System.Array.Empty<CampaignLevyState>(), true, true);
            else for (int offset = 0; offset < levies.Count; offset += chunkSize)
            {
                int count = Mathf.Min(chunkSize, levies.Count - offset);
                ReceiveLevyStateRpc(levies.GetRange(offset, count).ToArray(), offset == 0,
                    offset + count >= levies.Count);
            }
        }
        else if (changed.Count > 0)
        {
            const int chunkSize = 32;
            for (int offset = 0; offset < changed.Count; offset += chunkSize)
            {
                int count = Mathf.Min(chunkSize, changed.Count - offset);
                ReceiveLevyDeltaRpc(changed.GetRange(offset, count).ToArray());
            }
        }
    }

    private static int LevyRecordSignature(CampaignLevyState state)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + state.UnitName.GetHashCode(); hash = hash * 31 + state.State;
            hash = hash * 31 + state.Eligible.GetHashCode(); hash = hash * 31 + state.RemainingTicks;
            hash = hash * 31 + state.RaisedArmyId.GetHashCode(); hash = hash * 31 + state.HoldingId.GetHashCode();
            hash = hash * 31 + state.HoldingInstanceId.GetHashCode(); return hash;
        }
    }

    private static UnitSaveData FindUnit(Nation nation, string unitName)
    {
        UnitSaveData unit = NationContentResolver.ResolveUnits(nation)
            .ConvertAll(entry => entry != null ? entry.unit : null)
            .Find(item => item != null && item.name == unitName);
        if (unit != null)
        {
            return unit;
        }
        UnitSaveData[] allUnits = Resources.LoadAll<UnitSaveData>("Prefabs/Units");
        return System.Array.Find(allUnits, item => item != null && item.name == unitName);
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveProvinceStateRpc(CampaignProvinceState[] provinces)
    {
        if (Owners.Instance == null || Mapshower.Instance == null)
        {
            return;
        }
        long perfStamp = CampaignPerformanceTrace.Stamp();

        bool ownershipChanged = false;
        foreach (CampaignProvinceState state in provinces)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count ||
                state.NationIndex >= Owners.Instance.nationlist.Count)
            {
                continue;
            }

            Province province = Owners.Instance.provincelist[state.ProvinceIndex];
            Nation receivedNation = Owners.Instance.nationlist[state.NationIndex];
            if (province.nation != receivedNation)
            {
                province.nation = receivedNation;
                ownershipChanged = true;
            }
            Nation receivedOccupier = state.OccupyingNationIndex < Owners.Instance.nationlist.Count
                ? Owners.Instance.nationlist[state.OccupyingNationIndex] : null;
            if (province.OccupyingNation != receivedOccupier)
            {
                province.OccupyingNation = receivedOccupier;
                if (province.IsOccupied) province.garrison = null;
                else if (province.garrison == null) province.CreateGarrison();
                ownershipChanged = true;
            }
            province.supply = state.Supply;
            province.urbanization = Mathf.Clamp(state.Urbanization, -100, province.MaximumDevelopment);
            province.terrainProfile = (CampaignTerrainProfile)state.TerrainProfile;
            CampaignRegion region = Owners.Instance.CallRegionByString(province.region);
            if (region != null)
            {
                RegionalLoyaltyShare foodShare = region.GetLoyaltyShare(receivedNation, true);
                foodShare.foodStorageCapacity = Mathf.Max(1, state.RegionalFoodStorageCapacity);
                foodShare.foodStorage = Mathf.Clamp(state.RegionalFoodStorage, 0, foodShare.foodStorageCapacity);
                foodShare.lastFoodShortage = Mathf.Max(0, state.RegionalFoodShortage);
                RegionalManpowerShare manpowerShare = region.GetManpowerShare(receivedNation, true);
                manpowerShare.current = Mathf.Clamp(state.RegionalManpower, 0f, region.GetManpowerCapacity(receivedNation));
                manpowerShare.initialized = true;
            }
        }
        if (ownershipChanged) Mapshower.Instance.RePaint();
        double provinceStateMs = CampaignPerformanceTrace.MillisecondsSince(perfStamp);
        if (provinceStateMs >= 4.0) CampaignPerformanceTrace.Report("Client.ProvinceState", provinceStateMs,
            "changed=" + provinces.Length + " repaint=" + ownershipChanged);
    }

    private string CreateArmyId(string nation)
    {
        nextArmyId++;
        return nation + "_" + nextArmyId;
    }

    private static CampaignNetworkPlayer FindPlayer(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        foreach (NetworkObject networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            CampaignNetworkPlayer player = networkObject.GetComponent<CampaignNetworkPlayer>();
            if (player != null && player.OwnerClientId == clientId)
            {
                return player;
            }
        }
        return null;
    }

    private static FieldArmyHolder FindHumanArmy(ulong ownerClientId, string nationName)
    {
        if (Owners.Instance == null)
        {
            return null;
        }

        return Owners.Instance.armylist.Find(army => army != null &&
            ((army.NetworkOwnerClientId == ownerClientId && army.IsHumanControlled) ||
             (army.IsPlayer && army.fieldArmy != null && army.fieldArmy.nation != null && army.fieldArmy.nation.name == nationName)));
    }

    private static Province FindStartingProvince(Nation nation)
    {
        if (nation == null || Owners.Instance == null)
        {
            return null;
        }

        return Owners.Instance.provincelist.Find(province => province.nation == nation);
    }

    private static Province FindNearestProvince(Vector3 mapPosition)
    {
        if (Owners.Instance == null || Owners.Instance.provincelist.Count == 0)
        {
            return null;
        }

        Province nearest = Owners.Instance.provincelist[0];
        float nearestDistance = Vector2.Distance(nearest.position, mapPosition);
        foreach (Province province in Owners.Instance.provincelist)
        {
            float distance = Vector2.Distance(province.position, mapPosition);
            if (distance < nearestDistance)
            {
                nearest = province;
                nearestDistance = distance;
            }
        }
        return nearest;
    }
}
