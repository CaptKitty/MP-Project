using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Core;
using Unity.Services.Authentication;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Multiplayer;
using Unity.Collections;

public class TestRelay : MonoBehaviour
{
    private const string CampaignSessionType = "projectx_campaign_v1";
    private const string CampaignModeProperty = "mode";
    private const string PlayerNameProperty = "player_name";
    private const string PlayerFactionProperty = "faction";
    private const string StartCampaignMessage = "projectx_start_campaign";

    public static TestRelay Instance;
    public bool CanThisSpawn = true;
    public string JoinCodeTextStuff = "";
    // Start is called before the first frame update
    public List<GameObject> PlayerObjects = new List<GameObject>();
    public ISession ActiveSession { get; private set; }
    public string LocalLobbyFaction
    {
        get
        {
            if (TryGetLocalLobbyFaction(out string faction))
                return faction;
            return GetSelectedFactionName();
        }
    }

    /// <summary>
    /// Returns the faction explicitly synchronized for this player in the
    /// current multiplayer lobby. This deliberately does not fall back to the
    /// old SessionManager selection, so campaign assignment can distinguish a
    /// real lobby choice from legacy menu state.
    /// </summary>
    public bool TryGetLocalLobbyFaction(out string faction)
    {
        faction = string.Empty;
        if (ActiveSession == null || ActiveSession.CurrentPlayer == null ||
            ActiveSession.CurrentPlayer.Properties == null ||
            !ActiveSession.CurrentPlayer.Properties.TryGetValue(PlayerFactionProperty, out PlayerProperty property) ||
            property == null || string.IsNullOrWhiteSpace(property.Value))
            return false;

        faction = property.Value.Trim();
        return true;
    }

    private Task servicesInitialization;
    private bool isFindingSession;
    private InputField multiplayerPlayerName;
    private Button multiplayerFindLobby;
    private Button multiplayerEnterGame;
    private GameObject multiplayerLobbyTemplate;
    private Transform multiplayerLobbyRoot;
    private readonly List<GameObject> multiplayerLobbyRows = new List<GameObject>();
    private string pendingPlayerName;
    private float playerNameSaveAt = -1f;
    private bool savingPlayerProperties;
    private bool startCampaignHandlerRegistered;

    private async void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        EnsureStartCampaignHandler();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindMultiplayerSceneUI();
        await EnsureServicesReady();

    }

    private void Update()
    {
        EnsureStartCampaignHandler();
        if (playerNameSaveAt >= 0f && Time.unscaledTime >= playerNameSaveAt)
        {
            playerNameSaveAt = -1f;
            SaveLocalPlayerProperties();
        }
    }

    private void EnsureStartCampaignHandler()
    {
        if (startCampaignHandlerRegistered || NetworkManager.Singleton == null ||
            NetworkManager.Singleton.CustomMessagingManager == null) return;

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            StartCampaignMessage, OnStartCampaignMessage);
        startCampaignHandlerRegistered = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindMultiplayerSceneUI();
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
                if (candidate.name == objectName) return candidate.gameObject;
        }
        return null;
    }

    private void BindMultiplayerSceneUI()
    {
        if (SceneManager.GetActiveScene().name != "MultiplayerScene") return;

        GameObject playerNameObject = FindSceneObject("PlayerName");
        GameObject findLobbyObject = FindSceneObject("Find Lobby");
        GameObject enterGameObject = FindSceneObject("Enter Game");
        GameObject currentLobbyObject = FindSceneObject("CurrentLobby");
        if (playerNameObject == null || findLobbyObject == null || enterGameObject == null || currentLobbyObject == null)
        {
            Debug.LogWarning("MultiplayerScene lobby UI is incomplete.");
            return;
        }

        multiplayerPlayerName = playerNameObject.GetComponent<InputField>();
        multiplayerFindLobby = findLobbyObject.GetComponent<Button>();
        multiplayerEnterGame = enterGameObject.GetComponent<Button>();
        multiplayerLobbyRoot = currentLobbyObject.transform;
        multiplayerLobbyTemplate = FindSceneObject("PlayerTemplate");
        if (multiplayerLobbyTemplate == null && multiplayerLobbyRoot.childCount > 0)
            multiplayerLobbyTemplate = multiplayerLobbyRoot.GetChild(0).gameObject;

        if (multiplayerPlayerName != null)
        {
            string savedName = PlayerPrefs.GetString(PlayerNameProperty, "Player");
            if (string.IsNullOrWhiteSpace(multiplayerPlayerName.text))
                multiplayerPlayerName.SetTextWithoutNotify(savedName);
            pendingPlayerName = SanitizePlayerName(multiplayerPlayerName.text);
            multiplayerPlayerName.onValueChanged.RemoveListener(OnPlayerNameChanged);
            multiplayerPlayerName.onValueChanged.AddListener(OnPlayerNameChanged);
        }

        if (multiplayerFindLobby != null)
        {
            multiplayerFindLobby.onClick.RemoveListener(FindOrHostPublicSession);
            multiplayerFindLobby.onClick.AddListener(FindOrHostPublicSession);
            multiplayerFindLobby.interactable = ActiveSession == null && !isFindingSession;
        }
        if (multiplayerEnterGame != null)
        {
            multiplayerEnterGame.onClick.RemoveListener(EnterGame);
            multiplayerEnterGame.onClick.AddListener(EnterGame);
            multiplayerEnterGame.interactable = ActiveSession != null;
        }

        if (multiplayerLobbyTemplate != null)
        {
            multiplayerLobbyTemplate.name = "PlayerTemplate";
            multiplayerLobbyTemplate.SetActive(false);
        }
        RefreshLobbyRows();
    }

    private void OnPlayerNameChanged(string value)
    {
        pendingPlayerName = SanitizePlayerName(value);
        PlayerPrefs.SetString(PlayerNameProperty, pendingPlayerName);
        playerNameSaveAt = Time.unscaledTime + 0.25f;
    }

    private static string SanitizePlayerName(string value)
    {
        string cleaned = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        return cleaned.Length <= 24 ? cleaned : cleaned.Substring(0, 24);
    }

    public async void FindOrHostPublicSession()
    {
        if (isFindingSession || ActiveSession != null) return;
        isFindingSession = true;
        if (multiplayerFindLobby != null) multiplayerFindLobby.interactable = false;
        try
        {
            await EnsureServicesReady();
            ConfigureTransportForPlatform();

            string faction = GetSelectedFactionName();
            QuickJoinOptions quickJoin = new QuickJoinOptions
            {
                Timeout = TimeSpan.FromSeconds(3),
                CreateSession = true,
                Filters = new List<FilterOption>
                {
                    new FilterOption(FilterField.StringIndex1, CampaignSessionType, FilterOperation.Equal),
                    new FilterOption(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual)
                }
            };
            SessionOptions sessionOptions = CreateCampaignSessionOptions();
            sessionOptions.PlayerProperties = new Dictionary<string, PlayerProperty>
            {
                { PlayerNameProperty, new PlayerProperty(SanitizePlayerName(pendingPlayerName), VisibilityPropertyOptions.Public) },
                { PlayerFactionProperty, new PlayerProperty(faction, VisibilityPropertyOptions.Public) }
            };

            ActiveSession = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, sessionOptions);
            SubscribeToSession(ActiveSession);
            JoinCodeTextStuff = ActiveSession.Code;
            if (JoinCodeStuff.Instance != null && JoinCodeStuff.Instance.Texty != null)
                JoinCodeStuff.Instance.Texty.text = ActiveSession.Code;

            Debug.Log(ActiveSession.IsHost
                ? "No campaign was available; created and hosted session " + ActiveSession.Code
                : "Joined active campaign session " + ActiveSession.Code);
            if (multiplayerEnterGame != null) multiplayerEnterGame.interactable = true;
            RefreshLobbyRows();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ActiveSession = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }
        finally
        {
            isFindingSession = false;
            if (multiplayerFindLobby != null) multiplayerFindLobby.interactable = ActiveSession == null;
            if (multiplayerEnterGame != null) multiplayerEnterGame.interactable = ActiveSession != null;
        }
    }

    private static SessionOptions CreateCampaignSessionOptions()
    {
        return new SessionOptions
        {
            Name = "Project X Campaign",
            Type = CampaignSessionType,
            MaxPlayers = 4,
            IsPrivate = false,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { CampaignModeProperty, new SessionProperty(CampaignSessionType, VisibilityPropertyOptions.Public, PropertyIndex.String1) }
            }
        }.WithRelayNetwork();
    }

    private static void ConfigureTransportForPlatform()
    {
        if (NetworkManager.Singleton == null) throw new InvalidOperationException("NetworkManager is unavailable.");
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) throw new InvalidOperationException("UnityTransport is unavailable.");
#if UNITY_WEBGL
        transport.UseWebSockets = true;
#else
        transport.UseWebSockets = false;
#endif
    }

    private string GetSelectedFactionName()
    {
        if (SessionManager.Instance != null && SessionManager.Instance.HostFaction != null)
            return SessionManager.Instance.HostFaction.name;
        Faction[] factions = Resources.LoadAll<Faction>("Prefabs/NationData/Factions");
        return factions.Length > 0 ? factions.OrderBy(item => item.name).First().name : "Rome";
    }

    private async void SaveLocalPlayerProperties()
    {
        if (savingPlayerProperties || ActiveSession == null || ActiveSession.CurrentPlayer == null) return;
        savingPlayerProperties = true;
        try
        {
            ActiveSession.CurrentPlayer.SetProperties(new Dictionary<string, PlayerProperty>
            {
                { PlayerNameProperty, new PlayerProperty(SanitizePlayerName(pendingPlayerName), VisibilityPropertyOptions.Public) },
                { PlayerFactionProperty, new PlayerProperty(GetSelectedFactionName(), VisibilityPropertyOptions.Public) }
            });
            await ActiveSession.SaveCurrentPlayerDataAsync();
            CampaignNetworkPlayer.Local?.RequestLobbyNation(GetSelectedFactionName());
            RefreshLobbyRows();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            savingPlayerProperties = false;
        }
    }

    private void OnLocalFactionChanged(int option)
    {
        Dropdown selector = multiplayerLobbyRows
            .Select(row => row != null ? row.GetComponentInChildren<Dropdown>(true) : null)
            .FirstOrDefault(dropdown => dropdown != null && dropdown.gameObject.activeInHierarchy);
        if (selector == null || option < 0 || option >= selector.options.Count) return;
        string factionName = selector.options[option].text;
        if (SessionManager.Instance != null) SessionManager.Instance.ChangePlayerFaction(factionName);
        CampaignNetworkPlayer.Local?.RequestLobbyNation(factionName);
        SaveLocalPlayerProperties();
    }

    private static string GetPlayerProperty(IReadOnlyPlayer player, string key, string fallback)
    {
        if (player != null && player.Properties != null && player.Properties.TryGetValue(key, out PlayerProperty property) &&
            property != null && !string.IsNullOrWhiteSpace(property.Value)) return property.Value;
        return fallback;
    }

    private void RefreshLobbyRows()
    {
        foreach (GameObject row in multiplayerLobbyRows)
            if (row != null) Destroy(row);
        multiplayerLobbyRows.Clear();
        if (multiplayerLobbyRoot == null || multiplayerLobbyTemplate == null || ActiveSession == null) return;

        string localId = ActiveSession.CurrentPlayer != null ? ActiveSession.CurrentPlayer.Id : AuthenticationService.Instance.PlayerId;
        HashSet<string> claimedFactions = new HashSet<string>(ActiveSession.Players
            .Where(player => player.Id != localId)
            .Select(player => GetPlayerProperty(player, PlayerFactionProperty, string.Empty))
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        for (int index = 0; index < ActiveSession.Players.Count; index++)
        {
            IReadOnlyPlayer player = ActiveSession.Players[index];
            bool isLocal = player.Id == localId;
            GameObject row = Instantiate(multiplayerLobbyTemplate, multiplayerLobbyRoot);
            row.name = "LobbyPlayer_" + player.Id;
            row.SetActive(true);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            if (rowRect != null) rowRect.anchoredPosition = new Vector2(rowRect.anchoredPosition.x, 330f - index * 110f);

            Text header = row.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.transform.parent == row.transform);
            if (header != null)
            {
                bool isSessionHost = index == 0;
                string role = isSessionHost ? "Host" : "Client";
                header.text = role + " " + GetPlayerProperty(player, PlayerNameProperty, "Player");
            }

            Transform factionRoot = row.transform.Find("Faction");
            Dropdown selector = factionRoot != null ? factionRoot.GetComponentInChildren<Dropdown>(true) : null;
            Transform playerFaction = factionRoot != null ? factionRoot.Find("PlayerFaction") : null;
            if (selector != null) selector.gameObject.SetActive(isLocal);
            if (playerFaction != null) playerFaction.gameObject.SetActive(!isLocal);

            string selectedFaction = GetPlayerProperty(player, PlayerFactionProperty, GetSelectedFactionName());
            if (isLocal && selector != null)
            {
                List<string> factionNames = Resources.LoadAll<Faction>("Prefabs/NationData/Factions")
                    .Select(faction => faction.name)
                    .Where(name => name == selectedFaction || !claimedFactions.Contains(name))
                    .Distinct().OrderBy(name => name).ToList();
                selector.ClearOptions();
                selector.AddOptions(factionNames);
                selector.SetValueWithoutNotify(Mathf.Max(0, factionNames.IndexOf(selectedFaction)));
                selector.onValueChanged.RemoveAllListeners();
                selector.onValueChanged.AddListener(OnLocalFactionChanged);
                if (SessionManager.Instance != null && SessionManager.Instance.HostFaction != null &&
                    SessionManager.Instance.HostFaction.name != selectedFaction)
                    SessionManager.Instance.ChangePlayerFaction(selectedFaction);
            }
            else if (playerFaction != null)
            {
                Text factionText = playerFaction.GetComponentInChildren<Text>(true);
                if (factionText != null) factionText.text = selectedFaction;
            }
            multiplayerLobbyRows.Add(row);
        }
    }

    public void EnterGame()
    {
        if (ActiveSession == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
        string selectedFaction = LocalLobbyFaction;
        if (SessionManager.Instance != null && !string.IsNullOrWhiteSpace(selectedFaction) &&
            (SessionManager.Instance.HostFaction == null || SessionManager.Instance.HostFaction.name != selectedFaction))
            SessionManager.Instance.ChangePlayerFaction(selectedFaction);
        if (NetworkManager.Singleton.IsServer)
        {
            GimmeMap();
            return;
        }
        EnsureStartCampaignHandler();
        if (NetworkManager.Singleton.CustomMessagingManager == null)
        {
            Debug.LogWarning("Cannot enter campaign yet: the multiplayer connection is still initializing.");
            return;
        }
        using (FastBufferWriter writer = new FastBufferWriter(1, Allocator.Temp))
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(StartCampaignMessage, NetworkManager.ServerClientId, writer);
    }

    private void OnStartCampaignMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && ActiveSession != null)
            GimmeMap();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsServer || clientId != manager.LocalClientId) return;
        string reason = manager.DisconnectReason;
        CampaignConnectionNotifications.Show(string.IsNullOrWhiteSpace(reason)
            ? "Disconnected from the campaign host."
            : "Disconnected from the campaign host: " + reason, true);
    }
    public async void CreateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            string JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId); 
            //Debug.LogError(JoinCode);
            JoinCodeTextStuff = JoinCode;
            JoinCodeStuff.Instance.Texty.text = JoinCode;

            // NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
            //     allocation.RelayServer.IpV4,
            //     (ushort) allocation.RelayServer.Port,
            //     allocation.AllocationIdBytes,
            //     allocation.Key,
            //     allocation.ConnectionData
            // );
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData("dtls"));

            NetworkManager.Singleton.StartHost();
        }
        catch(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
    public async void CreateLocal()
    {
        // MultiplayerScene has separate Find Lobby and Enter Game controls. Its
        // older serialized buttons may still reference this legacy combined
        // action while the scene is open in the editor, so never let that stale
        // callback enter the campaign from this menu.
        if (SceneManager.GetActiveScene().name == "MultiplayerScene")
        {
            return;
        }

        if (isFindingSession)
        {
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                GimmeMap();
            }
            return;
        }

        isFindingSession = true;
        try
        {
            await EnsureServicesReady();

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
#if UNITY_WEBGL
            // Multiplayer Services supplies WSS Relay data for WebGL. The transport
            // interface must match it or NetworkDriver rejects the configuration.
            transport.UseWebSockets = true;
#else
            transport.UseWebSockets = false;
#endif

            QuickJoinOptions quickJoin = new QuickJoinOptions
            {
                Timeout = TimeSpan.FromSeconds(3),
                CreateSession = true,
                Filters = new List<FilterOption>
                {
                    new FilterOption(FilterField.StringIndex1, CampaignSessionType, FilterOperation.Equal),
                    new FilterOption(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual)
                }
            };

            SessionOptions sessionOptions = new SessionOptions
            {
                Name = "Project X Campaign",
                Type = CampaignSessionType,
                MaxPlayers = 4,
                IsPrivate = false,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    {
                        CampaignModeProperty,
                        new SessionProperty(
                            CampaignSessionType,
                            VisibilityPropertyOptions.Public,
                            PropertyIndex.String1)
                    }
                }
            }.WithRelayNetwork();

            ActiveSession = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, sessionOptions);
            SubscribeToSession(ActiveSession);
            JoinCodeTextStuff = ActiveSession.Code;

            if (JoinCodeStuff.Instance != null && JoinCodeStuff.Instance.Texty != null)
            {
                JoinCodeStuff.Instance.Texty.text = ActiveSession.Code;
            }

            Debug.Log(ActiveSession.IsHost
                ? "No campaign was available; created and hosted session " + ActiveSession.Code
                : "Joined active campaign session " + ActiveSession.Code);

            if (ActiveSession.IsHost)
            {
                GimmeMap();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        finally
        {
            isFindingSession = false;
        }
    }

    private Task EnsureServicesReady()
    {
        if (servicesInitialization == null)
        {
            servicesInitialization = InitializeServices();
        }
        return servicesInitialization;
    }

    private async Task InitializeServices()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        Debug.Log("Signed In " + AuthenticationService.Instance.PlayerId);
    }

    private void SubscribeToSession(ISession session)
    {
        session.Changed -= RefreshLobbyRows;
        session.Changed += RefreshLobbyRows;
        session.PlayerPropertiesChanged -= RefreshLobbyRows;
        session.PlayerPropertiesChanged += RefreshLobbyRows;
        session.RemovedFromSession -= OnRemovedFromSession;
        session.RemovedFromSession += OnRemovedFromSession;
        session.Deleted -= OnSessionDeleted;
        session.Deleted += OnSessionDeleted;
        session.SessionHostChanged -= OnSessionHostChanged;
        session.SessionHostChanged += OnSessionHostChanged;
    }

    private void UnsubscribeFromSession(ISession session)
    {
        if (session == null) return;
        session.Changed -= RefreshLobbyRows;
        session.PlayerPropertiesChanged -= RefreshLobbyRows;
        session.RemovedFromSession -= OnRemovedFromSession;
        session.Deleted -= OnSessionDeleted;
        session.SessionHostChanged -= OnSessionHostChanged;
    }

    private void OnRemovedFromSession()
    {
        HandleSessionEnded("Removed from campaign session");
    }

    private void OnSessionDeleted()
    {
        HandleSessionEnded("Campaign session ended");
    }

    private async void OnSessionHostChanged(string newHostPlayerId)
    {
        Debug.Log("Campaign lobby host changed to " + newHostPlayerId);
        if (ActiveSession == null || !ActiveSession.IsHost)
        {
            return;
        }

        try
        {
            await ActiveSession.ReconnectAsync();
            CampaignPersistence persistence = FindFirstObjectByType<CampaignPersistence>();
            if (persistence != null) persistence.LoadNow();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void HandleSessionEnded(string reason)
    {
        Debug.LogWarning(reason);
        CampaignConnectionNotifications.Show(reason, true);
        UnsubscribeFromSession(ActiveSession);
        ActiveSession = null;
        if (multiplayerEnterGame != null) multiplayerEnterGame.interactable = false;
        if (multiplayerFindLobby != null) multiplayerFindLobby.interactable = true;
        RefreshLobbyRows();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    public async void LeaveCampaign()
    {
        if (ActiveSession == null) return;
        ISession session = ActiveSession;
        UnsubscribeFromSession(session);
        ActiveSession = null;
        try
        {
            await session.LeaveAsync();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            if (startCampaignHandlerRegistered && NetworkManager.Singleton.CustomMessagingManager != null)
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(StartCampaignMessage);
        }
        UnsubscribeFromSession(ActiveSession);
    }
    public async void JoinRelay(Text JoinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(JoinCode.text);

            // NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
            //     joinAllocation.RelayServer.IpV4,
            //     (ushort) joinAllocation.RelayServer.Port,
            //     joinAllocation.AllocationIdBytes,
            //     joinAllocation.Key,
            //     joinAllocation.ConnectionData,
            //     joinAllocation.HostConnectionData
            // );
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));

            NetworkManager.Singleton.StartClient();

            
        }
        catch(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
    public void GimmeMap()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("Only the host can start the network campaign.");
                return;
            }

            NetworkManager.Singleton.SceneManager.LoadScene("MapScene", LoadSceneMode.Single);
            return;
        }

        SceneManager.LoadScene("MapScene");
        // if(BattleManager1.Instance == null && CanThisSpawn)
        // {
        //     CanThisSpawn = false;
        //     SceneManager.LoadScene("FightScene 1");//, LoadSceneMode.Additive);
        // }
    }
    public void GimmeBattlefield()
    {
        if(BattleManager1.Instance == null && CanThisSpawn)
        {
            CanThisSpawn = false;
            SceneManager.LoadScene("FightScene 1");//, LoadSceneMode.Additive);
        }
    }
}
