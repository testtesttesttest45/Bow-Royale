using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectPlayer : MonoBehaviour {


    [SerializeField] private int playerIndex;
    [SerializeField] private GameObject readyGameObject;
    [SerializeField] private PlayerVisual playerVisual;
    [SerializeField] private Button kickButton;
    [SerializeField] private TextMeshPro playerNameText;
    [SerializeField] private Button swapButton;
    [SerializeField] private GameObject ownerMarker;


    private void Awake()
    {
        kickButton.onClick.AddListener(() => {
            PlayerData playerData = MultiplayerManager.Instance.GetPlayerDataFromPlayerIndex(playerIndex);

            bool isBot = playerData.clientId >= 9000;

            if (isBot)
            {
                LobbyManager.Instance.RemoveBotFromLobbyById(playerData.clientId);
            }
            else
            {
                LobbyManager.Instance.KickPlayer(playerData.playerId.ToString());
                MultiplayerManager.Instance.KickPlayer(playerData.clientId);
            }
        });
    }


    private void Start()
    {
        MultiplayerManager.Instance.OnPlayerDataNetworkListChanged += KitchenGameMultiplayer_OnPlayerDataNetworkListChanged;
        

        UpdatePlayer();
        swapButton.onClick.AddListener(OnSwapClicked);
    }

    private void OnSwapClicked()
    {
        PlayerData playerData = MultiplayerManager.Instance.GetPlayerDataFromPlayerIndex(playerIndex);
        MultiplayerManager.Instance.RequestSwap(playerData.clientId);
    }


    private void CharacterSelectReady_OnReadyChanged(object sender, System.EventArgs e) {
        UpdatePlayer();
    }

    private void KitchenGameMultiplayer_OnPlayerDataNetworkListChanged(object sender, System.EventArgs e) {
        UpdatePlayer();
    }

    private void UpdatePlayer()
    {
        transform.localPosition = CharacterSelectUI.SlotPositions[playerIndex];

        if (MultiplayerManager.Instance.IsPlayerIndexConnected(playerIndex))
        {
            Show();

            PlayerData playerData = MultiplayerManager.Instance.GetPlayerDataFromPlayerIndex(playerIndex);

            readyGameObject.SetActive(playerData.isReady);
            playerNameText.text = playerData.playerName.ToString();
            playerVisual.SetPlayerModel(playerData.modelId);

            bool isSelf = playerData.clientId == NetworkManager.Singleton.LocalClientId;
            bool isBot = playerData.clientId >= 9000;
            bool hasPending = MultiplayerManager.Instance != null &&
                  MultiplayerManager.Instance.IsSwapPopupPendingFor(playerData.clientId);

            swapButton.gameObject.SetActive(!isSelf && !isBot && !hasPending);
            //if (ownerMarker != null)
            //    ownerMarker.SetActive(isSelf);
            // show owner marker for me, not for bots
            if (ownerMarker != null)
            {
                ownerMarker.SetActive(isSelf && !isBot);
            }


            if (NetworkManager.Singleton.IsServer)
            {
                kickButton.gameObject.SetActive(!isSelf);
            }
            else
            {
                kickButton.gameObject.SetActive(false);
            }
            Debug.Log($"Slot {playerIndex}: Now showing {playerData.playerName} (ClientId {playerData.clientId})");

        }
        else
        {
            Hide();
        }
    }

    private void Show() {
        gameObject.SetActive(true);
    }

    private void Hide() {
        gameObject.SetActive(false);
    }

    private void OnDestroy() {
        MultiplayerManager.Instance.OnPlayerDataNetworkListChanged -= KitchenGameMultiplayer_OnPlayerDataNetworkListChanged;
    }

}