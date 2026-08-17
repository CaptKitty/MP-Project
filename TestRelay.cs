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

public class TestRelay : MonoBehaviour
{
    private const string CampaignSessionType = "projectx_campaign_v1";
    private const string CampaignModeProperty = "mode";

    public static TestRelay Instance;
    public bool CanThisSpawn = true;
    public string JoinCodeTextStuff = "";
    // Start is called before the first frame update
    public List<GameObject> PlayerObjects = new List<GameObject>();
    public ISession ActiveSession { get; private set; }

    private Task servicesInitialization;
    private bool isFindingSession;

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

        await EnsureServicesReady();

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
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
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
