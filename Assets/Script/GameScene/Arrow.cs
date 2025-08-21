using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Arrow : NetworkBehaviour
{
    public float moveSpeed = 15f;
    public float maxDistance = 5f;
    public AudioClip arrowHitClip;
    public GameObject hitEffectPrefab;

    private Vector3 spawnPosition;
    private Vector3 moveDirection;
    private ulong ownerClientId;
    private bool isReady = false;
    private bool hasStarted = false;
    private bool hasHit = false;
    public ulong OwnerClientIdPublic => ownerClientId;
    private int shooterTeam = -1;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            gameObject.SetActive(false);
        }
    }

    public void SetOwnerClientId(ulong id)
    {
        ownerClientId = id;
    }

    private NetworkObject ownerNetObject;
    public void InitializeArrow(Vector3 position, Vector3 direction, NetworkObject ownerObj, ulong explicitOwnerClientId)
    {
        spawnPosition = position;
        moveDirection = direction.normalized;
        ownerNetObject = ownerObj;
        ownerClientId = explicitOwnerClientId;
        shooterTeam = MultiplayerManager.Instance.GetTeamIndexForClient(ownerClientId);

        transform.position = spawnPosition;
        transform.rotation = Quaternion.LookRotation(moveDirection);
        isReady = true;
        hasStarted = true;

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
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (!isReady || !hasStarted) return;

        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        if (IsServer && Vector3.Distance(spawnPosition, transform.position) >= maxDistance)
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
        Debug.Log("[Arrow] OnTriggerEnter: " + other.gameObject.name + " tag=" + other.tag);
        if (other.CompareTag("Shield"))
        {
            var blocker = other.GetComponent<ShieldBlocker>();
            int shieldTeam = MultiplayerManager.Instance.GetTeamIndexForClient(blocker.OwnerClientId);
            if (blocker != null)
            {
                if (shieldTeam == shooterTeam)
                {
                    Debug.Log("[Arrow] Ignore teammate shield!");
                    return;
                }
                if (blocker.OwnerClientId != ownerClientId)
                {
                    hasHit = true;
                    blocker.ForcePlayDeflect();
                    var netObj = GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn(true);
                    }
                    return;
                }
            }
        }



        if (other.CompareTag("Player"))
        {
            var playerNetObj = other.GetComponent<NetworkObject>();
            NetworkObject ownerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(ownerClientId);
            if (ownerNetObject != null && other.gameObject == ownerNetObject.gameObject)
            {
                return; // ignore self-hit
            }



            // 💥 Support both Player and Bot
            var player = other.GetComponent<Player>();
            var bot = other.GetComponent<Bot>();

            if (player != null)
            {
                int targetTeam = MultiplayerManager.Instance.GetTeamIndexForClient(player.OwnerClientId);
                if (targetTeam == shooterTeam) return; // teammate, ignore!
                hasHit = true;
                player.TakeDamageServerRpc(100);
            }
            else if (bot != null)
            {
                int targetTeam = MultiplayerManager.Instance.GetTeamIndexForClient(bot.BotClientId.Value);
                if (targetTeam == shooterTeam) return; // teammate, ignore!
                hasHit = true;
                bot.TakeDamage(100);
            }


            SpawnHitEffectClientRpc(transform.position);
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }

        }

        else if (other.CompareTag("Wall"))
        {
            hasHit = true;
            other.GetComponentInParent<WallDestruction>()?.RegisterHit();
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }

        }
    }

    [ClientRpc]
    private void SpawnHitEffectClientRpc(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            var fx = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(fx, 2f);
        }

        if (arrowHitClip != null)
        {
            var tempAudio = new GameObject("ArrowHitSound");
            var src = tempAudio.AddComponent<AudioSource>();
            src.clip = arrowHitClip;
            src.Play();
            Destroy(tempAudio, arrowHitClip.length);
        }
    }
}
