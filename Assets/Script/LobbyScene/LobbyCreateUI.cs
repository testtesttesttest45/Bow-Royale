using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyCreateUI : MonoBehaviour {


    [SerializeField] private Button closeButton;
    [SerializeField] private Button createPublicButton;
    [SerializeField] private Button createPrivateButton;
    [SerializeField] private TMP_InputField lobbyNameInputField;
    [SerializeField] private TMP_InputField playerNameInputField;



    private void Awake() {
        createPublicButton.onClick.AddListener(() => {
            LobbyManager.Instance.CreateLobby(lobbyNameInputField.text, false);
        });
        createPrivateButton.onClick.AddListener(() => {
            LobbyManager.Instance.CreateLobby(lobbyNameInputField.text, true);
        });
        closeButton.onClick.AddListener(() => {
            Hide();
        });
    }

    private void Start() {
        Hide();
    }

    public void Show()
    {
        gameObject.SetActive(true);

        void SyncLobbyName(string name)
        {
            lobbyNameInputField.text = !string.IsNullOrWhiteSpace(name) ? $"{name}'s Lobby" : "Lobby Name";
        }

        // Initial set
        SyncLobbyName(playerNameInputField.text);

        // Remove previous listeners to avoid stacking them
        playerNameInputField.onValueChanged.RemoveAllListeners();
        playerNameInputField.onValueChanged.AddListener(SyncLobbyName);

        createPublicButton.Select();
    }


    private void Hide() {
        gameObject.SetActive(false);
    }

}