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

public class TestRelay : MonoBehaviour
{
    public static TestRelay Instance;
    public bool CanThisSpawn = true;
    public string JoinCodeTextStuff = "";
    // Start is called before the first frame update
    public List<GameObject> PlayerObjects = new List<GameObject>();

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () => {
            Debug.Log("Signed In " + AuthenticationService.Instance.PlayerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        if(this.gameObject.name == "SimpleRelayManager")
        {
            Instance = this;
        }
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
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "wss"));

            NetworkManager.Singleton.StartHost();
        }
        catch{}
    }
    public async void CreateLocal()
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

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "wss"));

            NetworkManager.Singleton.StartHost();
        }
        catch{}
        
        GimmeMap();
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
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "wss"));

            NetworkManager.Singleton.StartClient();

            
        }
        catch{}
    }
    public void GimmeMap()
    {
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
