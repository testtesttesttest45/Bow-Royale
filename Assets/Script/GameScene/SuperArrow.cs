using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class SuperArrow : NetworkBehaviour
{
    public float speed = 18f;
    public float lifetime = 5f;
    public GameObject hitEffectPrefab;
    public AudioClip hitSound;

    private Vector3 spawnPosition;
    private Vector3 moveDirection;
    private ulong ownerClientId;

    private bool isReady = false;
    private bool hasStarted = false;
    private bool hasHit = false;
    private float lifeTimer = 0f;

    public ulong OwnerClientIdPublic => ownerClientId;
    private int shooterTeam = -1;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            gameObject.SetActive(false); // Prevent clients from moving before sync
        }
    }

    public void InitializeSuperArrow(Vector3 position, Vector3 direction, ulong ownerId)
    {
        spawnPosition = position;
        moveDirection = direction.normalized;
        ownerClientId = ownerId;
        shooterTeam = MultiplayerManager.Instance.GetTeamIndexForClient(ownerClientId);

        transform.position = spawnPosition;
        transform.rotation = Quaternion.LookRotation(moveDirection);

        tag = "SuperArrow"; // ✅ Critical for shield detection

        isReady = true;
        hasStarted = true;
        gameObject.SetActive(true);
    }


    [ClientRpc]
    public void TeleportArrowClientRpc(Vector3 position, Vector3 direction)
    {
        spawnPosition = position;
        moveDirection = direction.normalized;

        transform.position = spawnPosition;
        transform.rotation = Quaternion.LookRotation(moveDirection);

        isReady = true;
        hasStarted = true;
        gameObject.SetActive(true); // 🔥 Activate on clients
    }

    void Update()
    {
        if (!isReady || !hasStarted || hasHit) return;

        transform.position += moveDirection * speed * Time.deltaTime;
        lifeTimer += Time.deltaTime;
        if (IsServer && lifeTimer >= lifetime)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }

    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || hasHit) return;

        // 🛡 Shield block with team check
        if (other.CompareTag("Shield"))
        {
            var blocker = other.GetComponent<ShieldBlocker>();
            if (blocker != null)
            {
                int myTeam = MultiplayerManager.Instance.GetTeamIndexForClient(ownerClientId);
                int shieldTeam = MultiplayerManager.Instance.GetTeamIndexForClient(blocker.OwnerClientId);
                if (blocker.OwnerClientId == ownerClientId || myTeam == shieldTeam)
                {
                    // Ignore self and teammate shields
                    return;
                }
                hasHit = true;
                var netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn(true);
                return;
            }
        }

        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<Player>();
            var bot = other.GetComponent<Bot>();

            if (player != null)
            {
                var playerNetObj = player.GetComponent<NetworkObject>();
                if (playerNetObj != null && playerNetObj.OwnerClientId == ownerClientId)
                    return;

                int myTeam = MultiplayerManager.Instance.GetTeamIndexForClient(ownerClientId);
                int targetTeam = MultiplayerManager.Instance.GetTeamIndexForClient(player.OwnerClientId);
                if (myTeam == targetTeam)
                {
                    // Ignore teammate!
                    return;
                }
                hasHit = true;
                player.TakeDamageServerRpc(1000);
                SpawnHitEffectClientRpc(transform.position);
                var netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn(true);
                return;
            }
            else if (bot != null)
            {
                // **Never skip Bot hit!**
                hasHit = true;
                bot.TakeDamage(1000);
                SpawnHitEffectClientRpc(transform.position);
                var netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn(true);
                return;
            }
        }
        else if (other.CompareTag("Wall"))
        {
            // ✅ Destroy wall instantly with 3 hits
            WallDestruction wall = other.GetComponentInParent<WallDestruction>();
            if (wall != null)
            {
                wall.RegisterHit();
                wall.RegisterHit();
                wall.RegisterHit();
            }

            // ✅ Keep flying! Do NOT despawn or stop
            SpawnHitEffectClientRpc(transform.position);
        }
    }


    [ClientRpc]
    private void SpawnHitEffectClientRpc(Vector3 pos)
    {
        if (hitEffectPrefab != null)
        {
            var fx = Instantiate(hitEffectPrefab, pos, Quaternion.identity);
            Destroy(fx, 2f);
        }

        if (hitSound != null)
        {
            var tempAudio = new GameObject("SuperArrowHitSound");
            var src = tempAudio.AddComponent<AudioSource>();
            src.clip = hitSound;
            src.Play();
            Destroy(tempAudio, hitSound.length);
        }
    }
}
