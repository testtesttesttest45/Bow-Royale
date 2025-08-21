using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using Unity.Netcode;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private Transform winnerModelHolder;
    public List<GameObject> modelPrefabs;
    private GameObject currentModel;
    [SerializeField] private TextMeshProUGUI winnerLabelText;
    public static bool GameHasEnded = false;
    [SerializeField] private Button rematchButton;
    [SerializeField] private TextMeshProUGUI rematchButtonText;
    [SerializeField] private GameObject rematchSuccessIcon;
    [SerializeField] private GameObject rematchAlertIcon;
    [SerializeField] private GameObject rematchFightIcon;
    [SerializeField] private ToastNotification toastNotification;
    private bool selfRequested = false;
    public static bool RematchInProgress = false;
    public static GameOverUI Instance;

    private void Awake()
    {
        GameHasEnded = false;
        Instance = this;
        mainMenuButton.onClick.AddListener(() => {
            StartCoroutine(ReturnToMainMenu());
        });
    }

    private IEnumerator ReturnToMainMenu()
    {
        SessionManager.CleanUpSession();
        yield return null; // Allow one frame for destruction
        Loader.Load(Loader.Scene.MainMenuScene);
    }



    public void UpdateRematchProgress(int readyCount, int totalPlayers)
    {
        if (rematchButtonText)
            rematchButtonText.text = $"REQUEST REMATCH {readyCount}/{totalPlayers}";
    }

    private void Start()
    {
        gameOverPanel.SetActive(false);
        GameHasEnded = false;
        RematchInProgress = false;
    }

    public void ShowGameOver(string winnerName, int modelId)
    {
        DestroyAllHostFreezeDetectors(); // 🧹 Clean up any freeze detectors
        if (RematchInProgress)
        {
            Debug.Log("[GameOverUI] Ignoring GameOver popup: rematch in progress.");
            return;
        }

        SoundManager.Instance?.StopBGM(); // 🔇 Stop background music
        GamePauseUI.ResetState();
        gameOverPanel.SetActive(true);
        selfRequested = false;
        rematchClicked = false;
        ResetRematchButton();

        // ✅ Hide GamePauseUI
        if (GamePauseUI.InstanceShown)
        {
            GamePauseUI.Instance?.HidePause();
            GamePauseUI.Instance = null;
        }

        // 🧹 Hide disconnect UI
        HostDisconnectUI disconnectUI = FindObjectOfType<HostDisconnectUI>(true);
        if (disconnectUI != null)
        {
            disconnectUI.Hide();
        }

        // 🧹 Clear old model
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }

        bool isSpecialDisconnect = winnerName == "The other player has disconnected";
        bool isDraw = string.IsNullOrEmpty(winnerName) && !isSpecialDisconnect;

        winnerLabelText.gameObject.SetActive(!(isDraw || isSpecialDisconnect));

        // 🔊 Play correct sound
        if (isSpecialDisconnect)
        {
            winnerNameText.text = "The other player has disconnected";
            SoundManager.Instance?.PlayGlobalSound(SoundManager.Instance.drawClip);
        }
        else if (isDraw)
        {
            winnerNameText.text = "Draw!";
            SoundManager.Instance?.PlayGlobalSound(SoundManager.Instance.drawClip);
        }
        else
        {
            
            winnerNameText.text = winnerName;

            // ✅ Check if local player won
            string localPlayerName = MultiplayerManager.Instance.GetPlayerName();
            Debug.Log($"[GameOverUI] winnerName: {winnerName}, localPlayerName: {localPlayerName}");
            Debug.Log($"[GameOverUI] victoryClip: {SoundManager.Instance?.victoryClip}, defeatClip: {SoundManager.Instance?.defeatClip}");

            if (winnerName == localPlayerName)
            {
                SoundManager.Instance?.PlayGlobalSound(SoundManager.Instance.victoryClip);
            }
            else
            {
                SoundManager.Instance?.PlayGlobalSound(SoundManager.Instance.defeatClip);
            }

            // 🧍 Show model
            if (modelId >= 0 && modelId < modelPrefabs.Count)
            {
                currentModel = Instantiate(modelPrefabs[modelId], winnerModelHolder);
                currentModel.transform.localPosition = new Vector3(-100, 0, -100);
                // in 2v2, second model can be placed at 85, 0, -100
                currentModel.transform.localRotation = Quaternion.Euler(0, 180, 0);
                currentModel.transform.localScale = Vector3.one * 142f;
            }
        }

        mainMenuButton.Select();
        if (rematchButton != null)
        {
            rematchButton.gameObject.SetActive(!isSpecialDisconnect); // Hide if DC
        }
    }

    private void OnEnable()
    {
        if (MultiplayerManager.Instance != null)
            MultiplayerManager.Instance.OnRematchRequest += HandleRematchRequest;
        if (rematchButton != null)
            rematchButton.onClick.AddListener(OnRematchButtonClicked);

        ResetRematchButton();
    }
    private void OnDisable()
    {
        if (MultiplayerManager.Instance != null)
            MultiplayerManager.Instance.OnRematchRequest -= HandleRematchRequest;
        if (rematchButton != null)
            rematchButton.onClick.RemoveListener(OnRematchButtonClicked);
    }

    private void ResetRematchButton()
    {
        if (rematchButtonText) rematchButtonText.text = "REQUEST REMATCH";
        if (rematchFightIcon) rematchFightIcon.SetActive(true);
        if (rematchSuccessIcon) rematchSuccessIcon.SetActive(false);
        if (rematchAlertIcon) rematchAlertIcon.SetActive(false);
        rematchClicked = false;
        selfRequested = false;
    }

    private bool rematchClicked = false;

    private void OnRematchButtonClicked()
    {
        if (rematchClicked) return;

        rematchClicked = true;
        MultiplayerManager.Instance.RequestRematch();

        // if (rematchButtonText) rematchButtonText.text = "Waiting for others...";
        if (rematchSuccessIcon) rematchSuccessIcon.SetActive(true);
        if (rematchAlertIcon) rematchAlertIcon.SetActive(false);
        if (rematchFightIcon) rematchFightIcon.SetActive(false);

        // if (toastNotification) ToastNotification.Show("Rematch request sent!", "Success");
    }




    private void HandleRematchRequest(bool receivedFromOther)
    {
        if (receivedFromOther)
        {

            if (rematchButtonText) rematchButtonText.text = "Opponent would like a rematch!";
            if (rematchSuccessIcon) rematchSuccessIcon.SetActive(false);
            if (rematchAlertIcon) rematchAlertIcon.SetActive(true);
            if (rematchFightIcon) rematchFightIcon.SetActive(false);

            // if (toastNotification) ToastNotification.Show("Opponent would like a rematch!", "Alert");

            // If we've already clicked (both agreed), start rematch!
            if (selfRequested)
            {
                StartCoroutine(StartRematchAfterDelay());
            }
        }
    }

    private IEnumerator StartRematchAfterDelay()
    {
        yield return new WaitForSeconds(0.7f);

        RematchInProgress = true;   // <---- mark as in progress!
        GameHasEnded = false;       // <---- clear game over flag (optional, for clarity)
        Bot.GameHasStarted = false;

        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
        else
        {
            MultiplayerManager.Instance.SendStartRematchRequestToHost();
        }
    }


    public static void DestroyAllHostFreezeDetectors()
    {
        var detectors = GameObject.FindObjectsOfType<HostFreezeDetector>(true);
        foreach (var detector in detectors)
        {
            GameObject.Destroy(detector);
        }
    }

    public void ShowTeamGameOver(string winnerNames, int model1, int model2)
    {
        DestroyAllHostFreezeDetectors();
        SoundManager.Instance?.StopBGM();
        GamePauseUI.ResetState();
        gameOverPanel.SetActive(true);

        winnerNameText.text = winnerNames;

        // Play sound for 1v1 (and 2v2, if you want per-player sound)
        string localPlayerName = MultiplayerManager.Instance.GetPlayerName();
        if (!string.IsNullOrEmpty(localPlayerName) && winnerNames.Contains(localPlayerName))
        {
            SoundManager.Instance?.PlayGlobalSound(SoundManager.Instance.victoryClip);
        }
        else
        {
            SoundManager.Instance?.PlayGlobalSound(SoundManager.Instance.defeatClip);
        }

        // Clear previous
        foreach (Transform child in winnerModelHolder)
            Destroy(child.gameObject);

        // Show 2 models if needed
        if (model1 >= 0 && model1 < modelPrefabs.Count)
        {
            var m1 = Instantiate(modelPrefabs[model1], winnerModelHolder);
            m1.transform.localPosition = new Vector3(-100, 0, -100);
            m1.transform.localRotation = Quaternion.Euler(0, 180, 0);
            m1.transform.localScale = Vector3.one * 142f;
        }
        if (model2 >= 0 && model2 < modelPrefabs.Count)
        {
            var m2 = Instantiate(modelPrefabs[model2], winnerModelHolder);
            m2.transform.localPosition = new Vector3(85, 0, -100);
            m2.transform.localRotation = Quaternion.Euler(0, 180, 0);
            m2.transform.localScale = Vector3.one * 142f;
        }
    }





}
