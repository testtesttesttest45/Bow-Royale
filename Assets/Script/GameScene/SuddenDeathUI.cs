using System.Collections;
using UnityEngine;

public class SuddenDeathUI : MonoBehaviour
{
    public GameObject suddenDeathBG;
    public GameObject suddenDeathText;

    private CanvasGroup bgGroup;
    private CanvasGroup textGroup;

    private void Awake()
    {
        // Ensure CanvasGroups exist
        bgGroup = suddenDeathBG.GetComponent<CanvasGroup>();
        if (bgGroup == null) bgGroup = suddenDeathBG.AddComponent<CanvasGroup>();

        textGroup = suddenDeathText.GetComponent<CanvasGroup>();
        if (textGroup == null) textGroup = suddenDeathText.AddComponent<CanvasGroup>();

        // Start fully hidden
        bgGroup.alpha = 0f;
        textGroup.alpha = 0f;
        suddenDeathBG.SetActive(false);
        suddenDeathText.SetActive(false);
        gameObject.SetActive(false);
    }

    public void TriggerSuddenDeathSequence()
    {
        gameObject.SetActive(true);
        suddenDeathBG.SetActive(true);
        suddenDeathText.SetActive(true);

        // Start the fade in + out animation
        StartCoroutine(FadeInAndOutRoutine());
    }

    private IEnumerator FadeInAndOutRoutine()
    {
        float fadeInDuration = 0.5f;
        float holdDuration = 2f;
        float fadeOutDuration = 1f;

        float t = 0f;

        // Fade In
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / fadeInDuration);
            bgGroup.alpha = alpha;
            textGroup.alpha = alpha;
            yield return null;
        }

        // Hold fully visible
        bgGroup.alpha = 1f;
        textGroup.alpha = 1f;
        yield return new WaitForSeconds(holdDuration);

        // Fade Out
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            bgGroup.alpha = alpha;
            textGroup.alpha = alpha;
            yield return null;
        }

        // Fully hidden again
        bgGroup.alpha = 0f;
        textGroup.alpha = 0f;
        suddenDeathBG.SetActive(false);
        suddenDeathText.SetActive(false);
        gameObject.SetActive(false);
    }
}
