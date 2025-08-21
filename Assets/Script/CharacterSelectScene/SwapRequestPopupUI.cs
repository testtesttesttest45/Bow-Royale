using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class SwapRequestPopupUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;
    [SerializeField] private Image timerImage;

    private float duration = 5f;
    private float timer;

    private Action onClose;
    private Action onAccept;
    public void Init(string requesterName, Action onClose, Action onAccept)
    {
        messageText.text = $"{requesterName} would like to swap position with you";
        this.onClose = onClose;
        this.onAccept = onAccept;
        timer = duration;
        timerImage.fillAmount = 1f;
        StartCoroutine(RunTimer());
        rejectButton.onClick.AddListener(Close);
        acceptButton.onClick.AddListener(Accept);
    }

    private IEnumerator RunTimer()
    {
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            timerImage.fillAmount = timer / duration;
            yield return null;
        }
        Close();
    }
    private void Close()
    {
        onClose?.Invoke();
        Destroy(gameObject);
    }

    private void Accept()
    {
        Debug.Log("Popup Accept() called!");
        onAccept?.Invoke();
        Close();
    }
}
