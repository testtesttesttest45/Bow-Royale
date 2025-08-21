using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CharacterModelSelectSingleUI : MonoBehaviour
{
    [SerializeField] private int modelId;
    [Header("References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private GameObject buttonMask;
    [SerializeField] private Image lockImage;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => {
            if (!isLock)
                MultiplayerManager.Instance.ChangePlayerModel(modelId);
        });
    }

    [Header("State")]
    [SerializeField] private bool isLock;

    private static readonly Color LockedMaskColor = new Color(0, 0, 0, 170f / 255f);


    private void Start()
    {
        MultiplayerManager.Instance.OnPlayerDataNetworkListChanged += OnPlayerDataChanged;
        UpdateVisuals();
    }

    

    private void OnPlayerDataChanged(object sender, System.EventArgs e)
    {
        UpdateVisuals();

    }

    private void UpdateVisuals()
    {
        bool isSelected = MultiplayerManager.Instance.GetPlayerData().modelId == modelId;

        // 🔒 LOCKED
        if (isLock)
        {
            if (lockImage != null) lockImage.gameObject.SetActive(true);
            if (borderImage != null) borderImage.fillCenter = false;
            if (buttonMask != null)
            {
                buttonMask.SetActive(true);
                var maskImg = buttonMask.GetComponent<Image>();
                if (maskImg != null) maskImg.color = LockedMaskColor;
            }
            if (button != null) button.interactable = false;
            return;
        }

        // ✅ UNLOCKED
        if (lockImage != null) lockImage.gameObject.SetActive(false);
        if (borderImage != null) borderImage.fillCenter = isSelected;
        if (buttonMask != null)
        {
            buttonMask.SetActive(!isSelected);
            var maskImg = buttonMask.GetComponent<Image>();
            if (maskImg != null)
            {
                maskImg.color = new Color(1, 1, 1, 107f / 255f);
            }
        }
        if (button != null) button.interactable = true;
    }

    private void OnDestroy()
    {
        MultiplayerManager.Instance.OnPlayerDataNetworkListChanged -= OnPlayerDataChanged;
    }

    public void SetLocked(bool locked)
    {
        isLock = locked;
        UpdateVisuals();
    }
}
