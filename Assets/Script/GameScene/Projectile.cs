using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Projectile : NetworkBehaviour
{
    public GameObject hitEffectPrefab;
    public AudioClip hitSound;

    private Vector3 startPosition;
    private Vector3 moveDirection;
    private ulong ownerClientId;

    private readonly float maxTravelDistance = 6.5f;
    private readonly float speed = 12f;
    private readonly float rotationSpeed = 360f;

    private bool hasHit = false;
    private bool isReady = false;

    private readonly NetworkVariable<bool> shouldStop = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<Vector3> stopPosition = new NetworkVariable<Vector3>(
    Vector3.zero,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );

    public ulong OwnerClientIdPublic => ownerClientId;
    private int shooterTeam = -1;

    void Start()
    {
        IgnoreOwnerCollision();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            gameObject.SetActive(false);
        }
    }

    public void InitializeProjectile(Vector3 position, Vector3 direction, ulong ownerId)
    {
        startPosition = position;
        moveDirection = direction.normalized;
        ownerClientId = ownerId;
        shooterTeam = MultiplayerManager.Instance.GetTeamIndexForClient(ownerClientId);

        transform.position = startPosition;
        transform.rotation = Quaternion.LookRotation(moveDirection);
        isReady = true;
        gameObject.SetActive(true);

        Collider myCollider = GetComponent<Collider>();

        foreach (var player in FindObjectsOfType<Player>())
        {
            if (MultiplayerManager.Instance.GetTeamIndexForClient(player.OwnerClientId) == shooterTeam)
            {
                foreach (Collider col in player.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(myCollider, col);
                }
            }
        }

        foreach (var bot in FindObjectsOfType<Bot>())
        {
            if (MultiplayerManager.Instance.GetTeamIndexForClient(bot.BotClientId.Value) == shooterTeam)
            {
                foreach (Collider col in bot.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(myCollider, col);
                }
            }
        }
    }


    [ClientRpc]
    public void TeleportProjectileClientRpc(Vector3 position, Vector3 direction)
    {
        startPosition = position;
        moveDirection = direction.normalized;

        transform.position = startPosition;
        transform.rotation = Quaternion.LookRotation(moveDirection);

        isReady = true;
        gameObject.SetActive(true);
    }
    void Update()
    {
        if (!isReady) return;

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        if (!shouldStop.Value)
        {
            transform.position += moveDirection * speed * Time.deltaTime;

            if (IsServer && Vector3.Distance(startPosition, transform.position) >= maxTravelDistance)
            {
                stopPosition.Value = transform.position;
                shouldStop.Value = true;
                StartCoroutine(DespawnAfterDelay());
            }
        }
        else
        {
            transform.position = stopPosition.Value;
        }

    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        if (IsServer && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
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

        // ❌ Skip other projectiles (just in case)
        if (other.CompareTag("Projectile"))
        {
            return;
        }

        // 👤 Hit player or bot, using Player tag for both
        if (other.CompareTag("Player"))
        {
            NetworkObject ownerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(ownerClientId);

            // Ignore self-hit (ownerNetObj can be null for bots!)
            if (ownerNetObj != null && other.gameObject == ownerNetObj.gameObject)
                return;

            var player = other.GetComponent<Player>();
            var bot = other.GetComponent<Bot>();

            if (player != null)
            {
                int myTeam = MultiplayerManager.Instance.GetTeamIndexForClient(ownerClientId);
                int targetTeam = MultiplayerManager.Instance.GetTeamIndexForClient(player.OwnerClientId);
                if (myTeam == targetTeam) return;
                hasHit = true;
                player.TakeDamageServerRpc(150);
                player.ApplySlowEffectServerRpc(2.5f, 0.4f);

                SpawnHitEffectClientRpc(transform.position);
                if (NetworkObject != null && NetworkObject.IsSpawned)
                    NetworkObject.Despawn(true);
            }
            else if (bot != null)
            {
                int myTeam = MultiplayerManager.Instance.GetTeamIndexForClient(ownerClientId);
                int targetTeam = MultiplayerManager.Instance.GetTeamIndexForClient(bot.BotClientId.Value);
                if (myTeam == targetTeam) return; // Ignore teammate!
                hasHit = true;
                bot.TakeDamage(150);
                bot.ApplySlowEffect(2.5f, 0.4f);

                SpawnHitEffectClientRpc(transform.position);
                if (NetworkObject != null && NetworkObject.IsSpawned)
                    NetworkObject.Despawn(true);
            }
            return;
        }

        // 🧱 Hit wall or anything else
        hasHit = true;
        stopPosition.Value = transform.position;
        shouldStop.Value = true;
        StartCoroutine(DespawnAfterDelay());
    }



    public void SetOwnerClientId(ulong clientId)
    {
        ownerClientId = clientId;
    }

    public ulong GetOwnerClientId()
    {
        return ownerClientId;
    }
    private void IgnoreOwnerCollision()
    {
        GameObject owner = GetOwnerPlayer();
        if (owner == null) return;

        Collider myCollider = GetComponent<Collider>();

        // Ignore body collision
        Collider ownerBody = owner.GetComponent<Collider>();
        if (ownerBody != null && myCollider != null)
        {
            Physics.IgnoreCollision(myCollider, ownerBody);
        }

        // Ignore shield collision
        ShieldBlocker[] shields = FindObjectsOfType<ShieldBlocker>();
        foreach (var shield in shields)
        {
            if (shield.OwnerClientId == ownerClientId)
            {
                Collider shieldCollider = shield.GetComponent<Collider>();
                if (shieldCollider != null && myCollider != null)
                {
                    Physics.IgnoreCollision(myCollider, shieldCollider);
                }
            }
        }
    }

    private GameObject GetOwnerPlayer()
    {
        foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == ownerClientId)
                return player;
        }
        return null;
    }

    [ClientRpc]
    private void SpawnHitEffectClientRpc(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            GameObject fx = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(fx, 2f);
        }

        if (hitSound != null)
        {
            GameObject tempAudio = new GameObject("ProjectileHitSound");
            AudioSource src = tempAudio.AddComponent<AudioSource>();
            src.clip = hitSound;
            src.volume = 1f;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(tempAudio, hitSound.length);
        }
    }
}
