using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class ShieldBlocker : NetworkBehaviour
{
    public new ulong OwnerClientId;
    private bool hasBlocked = false;
    public AudioClip deflectSound;
    public CharacterAudioProfile audioProfile;
    private AudioSource abilitySource;
    private int shieldTeam = -1;

    private void Start()
    {
        shieldTeam = MultiplayerManager.Instance.GetTeamIndexForClient(OwnerClientId);

        Collider shieldCollider = GetComponent<Collider>();

        // Ignore existing friendly projectiles immediately
        foreach (var projectile in FindObjectsOfType<Projectile>())
        {
            if (projectile != null && MultiplayerManager.Instance.GetTeamIndexForClient(projectile.OwnerClientIdPublic) == shieldTeam)
            {
                Physics.IgnoreCollision(shieldCollider, projectile.GetComponent<Collider>());
            }
        }

        foreach (var arrow in FindObjectsOfType<Arrow>())
        {
            if (arrow != null && MultiplayerManager.Instance.GetTeamIndexForClient(arrow.OwnerClientIdPublic) == shieldTeam)
            {
                Physics.IgnoreCollision(shieldCollider, arrow.GetComponent<Collider>());
            }
        }
    }


    public void SetAudioProfile(CharacterAudioProfile profile)
    {
        audioProfile = profile;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || hasBlocked) return;

        if (other.CompareTag("Arrow") || other.CompareTag("Projectile") || other.CompareTag("SuperArrow"))
        {
            ulong arrowOwnerId = 99999;
            int projectileTeam = -1;
            int myTeam = MultiplayerManager.Instance.GetTeamIndexForClient(OwnerClientId);

            // Get projectile owner and team
            if (other.TryGetComponent<Arrow>(out var arrow))
            {
                arrowOwnerId = arrow.OwnerClientIdPublic;
                projectileTeam = MultiplayerManager.Instance.GetTeamIndexForClient(arrowOwnerId);
            }
            else if (other.TryGetComponent<Projectile>(out var cleaver))
            {
                arrowOwnerId = cleaver.GetOwnerClientId();
                projectileTeam = MultiplayerManager.Instance.GetTeamIndexForClient(arrowOwnerId);
            }
            else if (other.TryGetComponent<SuperArrow>(out var super))
            {
                arrowOwnerId = super.OwnerClientIdPublic;
                projectileTeam = MultiplayerManager.Instance.GetTeamIndexForClient(arrowOwnerId);
            }
            else
            {
                Debug.LogWarning("Projectile type not recognized by ShieldBlocker");
                return;
            }

            Debug.Log($"[ShieldBlocker] OnTriggerEnter: MyOwnerId={OwnerClientId} | MyTeam={myTeam} | ProjectileOwner={arrowOwnerId} | ProjectileTeam={projectileTeam} | Other={other.name}");

            // Defensive: Early out if team info is not valid
            if (arrowOwnerId == 99999 || projectileTeam == -1 || myTeam == -1)
            {
                Debug.LogWarning($"[ShieldBlocker] Invalid team or owner info! OwnerId={OwnerClientId}, ProjectileOwner={arrowOwnerId}, ShieldTeam={myTeam}, ProjectileTeam={projectileTeam}");
                return;
            }

            // Block only if ENEMY
            if (arrowOwnerId == OwnerClientId)
            {
                Debug.Log($"[ShieldBlocker] Ignoring self-owned projectile.");
                return;
            }

            if (projectileTeam == myTeam)
            {
                Debug.Log($"[ShieldBlocker] Ignoring teammate projectile: {projectileTeam} == {myTeam}");
                return;
            }

            Debug.Log($"[ShieldBlocker] BLOCKED projectile from ENEMY team: {projectileTeam} vs {myTeam} | Other={other.name}");

            hasBlocked = true;
            var netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }

            PlayDeflectSoundClientRpc();
            PlayBlockFadeClientRpc();
        }
    }




    [ClientRpc]
    private void PlayDeflectSoundClientRpc()
    {
        if (deflectSound != null)
        {
            GameObject tempAudio = new GameObject("DeflectSound");
            AudioSource src = tempAudio.AddComponent<AudioSource>();
            src.clip = deflectSound;
            src.volume = 1f;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(tempAudio, deflectSound.length);
        }
        if (audioProfile != null && audioProfile.shieldDeflect != null)
        {
            GameObject tempVO = new GameObject("ShieldDeflectVO");
            AudioSource voSrc = tempVO.AddComponent<AudioSource>();
            voSrc.clip = audioProfile.shieldDeflect;
            voSrc.volume = 1f;
            voSrc.spatialBlend = 0f;
            voSrc.Play();
            Destroy(tempVO, audioProfile.shieldDeflect.length);
        }
    }

    [ClientRpc]
    private void PlayBlockFadeClientRpc()
    {
        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        Renderer shieldRenderer = GetComponentInChildren<Renderer>();
        if (shieldRenderer == null) yield break;

        Material mat = shieldRenderer.material;

        float startFill = mat.GetFloat("_Fill");
        float endFill = -0.75f;
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            float currentFill = Mathf.Lerp(startFill, endFill, t);
            mat.SetFloat("_Fill", currentFill);
            yield return null;
        }

        if (IsServer)
        {
            GetComponent<NetworkObject>()?.Despawn(true);
        }
    }

    public void ForcePlayDeflect()
    {
        if (hasBlocked) return;

        hasBlocked = true;
        PlayDeflectSoundClientRpc();
        PlayBlockFadeClientRpc();
    }

    public void ForceFadeOnly() // 🔕 no deflect sound
    {
        if (hasBlocked) return;

        hasBlocked = true;
        PlayBlockFadeClientRpc();
    }
}
