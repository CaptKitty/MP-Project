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
    private float nextTileBattleSnapshotTime;
    private float nextArmyOwnershipCheckTime;
    private int nextArmyId;
    private bool battleHooksInstalled;
    private int nextBattleTransferId;
    private readonly Dictionary<int, BattleStartTransfer> incomingBattleTransfers = new Dictionary<int, BattleStartTransfer>();
    private readonly Dictionary<int, BattleStartTransfer> incomingTileBattleTransfers = new Dictionary<int, BattleStartTransfer>();
    private bool presenceAnnounced;

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
        NationName.OnValueChanged += OnNationChanged;

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
            if (nation != null) nation.IsPlayer = IsNationPlayerControlled(nation.name);
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
            nextProvinceSnapshotTime = Time.unscaledTime + 2f;
            BroadcastProvinceState();
            BroadcastDetailedState();
        }

        if (Time.unscaledTime >= nextTileBattleSnapshotTime)
        {
            nextTileBattleSnapshotTime = Time.unscaledTime + 0.5f;
            BroadcastTileBattleState();
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
        RequestProvinceRecruit(unitName, amount, false, string.Empty);
    }

    public void RequestProvinceRecruit(string unitName, int amount, bool mercenary, string sourceProvinceName)
    {
        if (IsOwner && HasAssignment && amount > 0)
        {
            string armyId = FieldArmyHolder.SelectedPlayerArmy != null
                ? FieldArmyHolder.SelectedPlayerArmy.NetworkArmyId : string.Empty;
            RequestRecruitRpc(unitName, amount, mercenary, sourceProvinceName ?? string.Empty, armyId ?? string.Empty);
        }
    }

    public void RequestEventOption(string eventName, int optionIndex)
    {
        if (IsOwner && HasAssignment)
        {
            RequestEventOptionRpc(eventName, optionIndex);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestEventOptionRpc(FixedString64Bytes eventName, int optionIndex, RpcParams rpcParams = default)
    {
        CampaignNetworkPlayer sender = FindPlayer(rpcParams.Receive.SenderClientId);
        BaseEvents source = Resources.Load<BaseEvents>("EventGroup/" + eventName.ToString());
        if (sender == null || source == null || optionIndex < 0 || optionIndex >= source.OptionList.Count)
        {
            return;
        }

        Option option = source.OptionList[optionIndex];
        if (option.trigger != null && !option.trigger.CanTrigger())
        {
            return;
        }
        foreach (BaseEffect effectSource in option.EffectList)
        {
            if (effectSource == null) continue;
            BaseEffect effect = Instantiate(effectSource);
            effect.nation = sender.AssignedNation;
            effect.GrabRandomTarget();
            effect.Execute();
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
    private void RequestRecruitRpc(FixedString64Bytes unitName, int amount, bool mercenary,
        FixedString64Bytes sourceProvinceName, FixedString64Bytes armyId, RpcParams rpcParams = default)
    {
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
        if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null)
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

        ProvinceMercenaryPool requestedMercenaryPool = mercenary ? sourceProvince.FindMercenary(unitName.ToString()) : null;
        UnitSaveData unit = mercenary
            ? requestedMercenaryPool != null ? requestedMercenaryPool.unit : null
            : FindUnit(nation, unitName.ToString());
        if (unit == null) return;

        int availableSlots = army.fieldArmy.MaxArmySize - army.fieldArmy.GrabArmySize() - army.fieldArmy.GrabQueuedArmySize();
        int recruitAmount = Mathf.Min(amount, availableSlots);
        if (recruitAmount <= 0) return;
        int goldCost = CampaignEconomy.UnitGoldCost(unit, recruitAmount);
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
        else
        {
            if (sourceProvince != currentProvince || currentProvince.nation != nation || !sourceProvince.CanRecruitLocal(unit)) return;
            int manpowerCost = Mathf.Max(1, unit.cost / 100) * recruitAmount;
            if (nation.Manpower < manpowerCost) return;
            nation.Manpower -= manpowerCost;
        }

        if (!army.fieldArmy.QueueRecruitment(unit, recruitAmount)) return;
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
        List<CampaignArmyState> armies = new List<CampaignArmyState>();
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

            armies.Add(CampaignArmyState.FromArmy(army));
        }

        ReceiveArmyStateRpc(armies.ToArray());
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveArmyStateRpc(CampaignArmyState[] armies)
    {
        if (Owners.Instance == null || Mapshower.Instance == null)
        {
            return;
        }

        HashSet<string> receivedIds = new HashSet<string>();
        foreach (CampaignArmyState state in armies)
        {
            string armyId = state.ArmyId.ToString();
            receivedIds.Add(armyId);
            FieldArmyHolder army = Owners.Instance.armylist.Find(item => item != null && item.NetworkArmyId == armyId);

            if (army == null && state.OwnerClientId == NetworkManager.Singleton.LocalClientId && FieldArmyHolder.PlayerFieldArmy != null)
            {
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

        List<FieldArmyHolder> stale = Owners.Instance.armylist.FindAll(item =>
            item != null && item.IsNetworkReplica && !receivedIds.Contains(item.NetworkArmyId));
        foreach (FieldArmyHolder army in stale)
        {
            Destroy(army.gameObject);
        }
    }

    private void BroadcastProvinceState()
    {
        CampaignProvinceState[] provinces = new CampaignProvinceState[Owners.Instance.provincelist.Count];
        for (int i = 0; i < Owners.Instance.provincelist.Count; i++)
        {
            Province province = Owners.Instance.provincelist[i];
            int nationIndex = Owners.Instance.nationlist.IndexOf(province.nation);
            provinces[i] = CampaignProvinceState.FromProvince(i, nationIndex, province);
        }
        ReceiveProvinceStateRpc(provinces);
    }

    private void BroadcastDetailedState()
    {
        List<CampaignUnitState> units = new List<CampaignUnitState>();
        foreach (FieldArmyHolder army in Owners.Instance.armylist)
        {
            if (army == null || string.IsNullOrEmpty(army.NetworkArmyId) || army.fieldArmy == null)
            {
                continue;
            }
            foreach (ArmyReserves reserve in army.fieldArmy.USDReserves)
            {
                if (reserve != null && reserve.USD != null && reserve.amount > 0)
                {
                    units.Add(new CampaignUnitState
                    {
                        ArmyId = army.NetworkArmyId,
                        UnitName = reserve.USD.name,
                        Amount = reserve.amount
                    });
                }
            }
        }

        CampaignNationState[] nations = new CampaignNationState[Owners.Instance.nationlist.Count];
        for (int i = 0; i < Owners.Instance.nationlist.Count; i++)
        {
            Nation nation = Owners.Instance.nationlist[i];
            nations[i] = new CampaignNationState
            {
                NationIndex = (ushort)i,
                Manpower = nation.Manpower,
                BarracksLevel = nation.faction.BarracksLevel,
                MercenaryLevel = nation.faction.MercLevel,
                FarmLevel = nation.faction.FarmLevel,
                Income = nation.faction.Income,
                Gold = nation.Gold
            };
        }
        List<CampaignFactionFlagState> flags = new List<CampaignFactionFlagState>();
        List<CampaignBuildingState> buildings = new List<CampaignBuildingState>();
        List<CampaignMercenaryState> mercenaries = new List<CampaignMercenaryState>();
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
        }
        ReceiveDetailedStateRpc(units.ToArray(), nations, flags.ToArray(), buildings.ToArray(), mercenaries.ToArray());
    }

    [Rpc(SendTo.NotServer)]
    private void ReceiveDetailedStateRpc(CampaignUnitState[] units, CampaignNationState[] nations,
        CampaignFactionFlagState[] flags, CampaignBuildingState[] buildings, CampaignMercenaryState[] mercenaries)
    {
        if (Owners.Instance == null)
        {
            return;
        }

        foreach (CampaignNationState state in nations)
        {
            if (state.NationIndex >= Owners.Instance.nationlist.Count)
            {
                continue;
            }
            Nation nation = Owners.Instance.nationlist[state.NationIndex];
            nation.Manpower = state.Manpower;
            nation.faction.BarracksLevel = state.BarracksLevel;
            nation.faction.MercLevel = state.MercenaryLevel;
            nation.faction.FarmLevel = state.FarmLevel;
            nation.faction.Income = state.Income;
            nation.Gold = state.Gold;
            nation.faction.Flaglist.Clear();
        }
        foreach (CampaignFactionFlagState flag in flags)
        {
            if (flag.NationIndex < Owners.Instance.nationlist.Count)
            {
                Owners.Instance.nationlist[flag.NationIndex].faction.Flaglist.Add(flag.Flag.ToString());
            }
        }

        foreach (Province province in Owners.Instance.provincelist)
        {
            province.buildings.Clear();
            province.mercenaryPools.Clear();
        }
        foreach (CampaignBuildingState state in buildings)
        {
            if (state.ProvinceIndex >= Owners.Instance.provincelist.Count) continue;
            Owners.Instance.provincelist[state.ProvinceIndex].buildings.Add(new ProvinceBuilding
            {
                definition = BuildingDefinition.Find(state.BuildingId.ToString()),
                id = state.BuildingId.ToString(), level = state.Level,
                maxLevel = Mathf.Max(state.MaxLevel, ProvinceBuilding.MaximumLevelFor(state.BuildingId.ToString())),
                slotIndex = state.SlotIndex
            });
        }
        foreach (Province province in Owners.Instance.provincelist)
            province.RefreshGarrisonForFort();
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

        foreach (FieldArmyHolder knownArmy in Owners.Instance.armylist)
        {
            if (knownArmy != null && knownArmy.fieldArmy != null)
            {
                knownArmy.fieldArmy.USDReserves.Clear();
            }
        }

        foreach (KeyValuePair<string, List<CampaignUnitState>> entry in byArmy)
        {
            FieldArmyHolder army = Owners.Instance.armylist.Find(item => item != null && item.NetworkArmyId == entry.Key);
            if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null)
            {
                continue;
            }
            foreach (CampaignUnitState unitState in entry.Value)
            {
                UnitSaveData unit = FindUnit(army.fieldArmy.nation, unitState.UnitName.ToString());
                if (unit != null)
                {
                    army.fieldArmy.AddTroop(unit, unitState.Amount, true);
                }
            }
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
            province.supply = state.Supply;
            province.population = state.Population;
            province.terrainProfile = (CampaignTerrainProfile)state.TerrainProfile;
        }
        if (ownershipChanged) Mapshower.Instance.RePaint();
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
