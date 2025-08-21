using UnityEngine;
using UnityEngine.UI;

public class ChargeBarController : MonoBehaviour
{
    public Image chargeFill;
    private float currentCharge = 0f;
    private float maxCharge = 100f;
    private float lerpSpeed = 5f;

    void Update()
    {
        float targetFill = currentCharge / maxCharge;
        chargeFill.fillAmount = Mathf.Lerp(chargeFill.fillAmount, targetFill, Time.deltaTime * lerpSpeed);
    }

    public void SetCharge(float value, bool isLocalOwner)
    {
        currentCharge = Mathf.Clamp(value, 0f, maxCharge);

        // ✅ Always match visual fill to the currentCharge immediately
        // Owner already sets it directly
        chargeFill.fillAmount = currentCharge / maxCharge;
    }


    public void AddCharge(float value, bool isLocalOwner)
    {
        SetCharge(currentCharge + value, isLocalOwner);
    }

    public void ResetCharge()
    {
        currentCharge = 0f;
        chargeFill.fillAmount = 0f;
    }

    public float GetChargePercent()
    {
        return currentCharge / maxCharge;
    }
}

