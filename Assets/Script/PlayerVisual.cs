using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerVisual : MonoBehaviour
{

    public Animator CurrentAnimator { get; private set; }

    private int currentModelId = -1;

    [SerializeField] private NetworkAnimator networkAnimator;

    [System.Serializable]
    public class ModelVariant
    {
        public GameObject modelObject;
        public CharacterAudioProfile audioProfile;
    }
    [SerializeField] private List<ModelVariant> modelVariants;

    private NetworkAnimator netAnim;
    public CharacterAudioProfile CurrentAudioProfile { get; private set; }

    private void Awake()
    {
        netAnim = GetComponent<NetworkAnimator>();
        if (netAnim != null)
        {
            netAnim.enabled = false;
        }

        foreach (var variant in modelVariants)
        {
            if (variant.modelObject != null)
                variant.modelObject.SetActive(false); // Hide all initially
        }
    }


    public void SetPlayerModel(int modelId)
    {
        if (modelId == currentModelId) return;

        for (int i = 0; i < modelVariants.Count; i++)
        {
            bool isActive = i == modelId;
            modelVariants[i].modelObject.SetActive(isActive);

            if (isActive)
            {
                CurrentAnimator = modelVariants[i].modelObject.GetComponent<Animator>();
                CurrentAudioProfile = modelVariants[i].audioProfile;

                var relay = modelVariants[i].modelObject.GetComponent<AnimationEventRelay>();
                if (relay != null)
                {
                    var player = GetComponentInParent<Player>();
                    var bot = GetComponentInParent<Bot>();

                    if (player != null) relay.player = player;
                    if (bot != null) relay.bot = bot;
                }

                CurrentAnimator.Rebind();
                CurrentAnimator.Update(0f);
            }
        }

        currentModelId = modelId;
    }


}
