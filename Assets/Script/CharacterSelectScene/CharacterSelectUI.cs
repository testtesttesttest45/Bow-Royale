using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectUI : MonoBehaviour
{


    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private Button addBotButton;
    [SerializeField] private SwapRequestPopupUI swapRequestPopupPrefab;
    [SerializeField] private GameObject lobbyStatusObject;
    private SwapRequestPopupUI currentPopup;

    public static readonly Vector3[] SlotPositions = {
    new Vector3(-1.52f, 0, 0.08f), // inner left. in game scene, will spawn at 5,0,0
    new Vector3(1.34f, 0, 0.08f), // outer left. in game scene, will spawn at 10,0,0
    new Vector3(-2.84f, 0, 0.08f), // inner right. in game scene, will spawn at 5,0,5
    new Vector3(2.66f, 0, 0.08f) // outer right. in game scene, will spawn at 10,0,5
    };

    public static CharacterSelectUI Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (!LobbyManager.Instance.IsLobbyHost())
            addBotButton.gameObject.SetActive(false);

        addBotButton.onClick.AddListener(() => {
            LobbyManager.Instance.AddBotToLobby();
        });
        mainMenuButton.onClick.AddListener(() => {
            if (LobbyManager.Instance.IsLobbyHost())
                LobbyManager.Instance.DeleteLobby();
            else
                LobbyManager.Instance.LeaveLobby();

            SessionManager.CleanUpSession();
            Loader.Load(Loader.Scene.MainMenuScene);
        });


        readyButton.onClick.AddListener(() => {
            CharacterSelectReady.Instance.ToggleReady();
        });

    }

    public void OnClick_SelectModel(int modelId)
    {
        MultiplayerManager.Instance.ChangePlayerModel(modelId);
        
    }


    private void Start()
    {
        Lobby lobby = LobbyManager.Instance.GetLobby();

        lobbyNameText.text = "Lobby Name: " + lobby.Name;
        lobbyCodeText.text = "Lobby Code: " + lobby.LobbyCode;
        MultiplayerManager.Instance.OnPlayerDataNetworkListChanged += UpdateReadyButtonState;
        UpdateReadyButtonState(this, System.EventArgs.Empty);
    }

    private void UpdateReadyButtonState(object sender, System.EventArgs e)
    {
        int total = MultiplayerManager.Instance.playerDataNetworkList.Count;
        int ready = MultiplayerManager.Instance.GetReadyPlayerCount();

        addBotButton.interactable = total < MultiplayerManager.MAX_PLAYER_AMOUNT;

        // Ready button always interactable, always shows "Ready X/Y"
        readyButton.interactable = true;
        readyButtonText.text = $"Ready {ready}/{total}";

        // Show warning if team count is imbalanced (1 or 3)
        bool imbalance = (total == 1 || total == 3);
        if (lobbyStatusObject != null)
            lobbyStatusObject.SetActive(imbalance);
    }




    private void OnDestroy()
    {
        if (MultiplayerManager.Instance != null)
            MultiplayerManager.Instance.OnPlayerDataNetworkListChanged -= UpdateReadyButtonState;
    }


    public void ShowSwapRequestPopup(string requesterName, ulong requesterClientId, ulong requestedClientId)
    {
        if (currentPopup != null) Destroy(currentPopup.gameObject);

        currentPopup = Instantiate(swapRequestPopupPrefab, transform);
        Debug.Log("Instantiating swap popup");
        currentPopup.Init(requesterName, () => {
            MultiplayerManager.Instance.ClearSwapPopupPendingServerRpc(requestedClientId);
            currentPopup = null;
        }, () => {
            Debug.Log($"Accept pressed: sending AcceptSwapRequestServerRpc({requesterClientId}, {requestedClientId}) from client {NetworkManager.Singleton.LocalClientId}");
            MultiplayerManager.Instance.AcceptSwapRequestServerRpc(requesterClientId, requestedClientId);
        }
        );
    }






}