using UnityEngine;

public class BotAnimationEventRelay : MonoBehaviour
{
    public Bot bot;

    private void Awake()
    {
        Debug.Log($"[Relay] Awake on {gameObject.name} — Found Bot: {(bot != null)}");
    }

    public void PlayerShootArrow2()
    {
        Debug.Log($"[Relay] PlayerShootArrow2 triggered on {gameObject.name}");
        if (bot != null)
        {
            bot.TryFireArrow();
        }
    }
}
