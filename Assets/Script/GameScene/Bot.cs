using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System;
using System.Collections.Generic;

public class Bot : NetworkBehaviour
{
    public float moveSpeed = 5f;
    private float attackRange = 5f;

    private float baseAttackCooldown = 2f;
    private float attackCooldown;
    private float attackTimer = 0f;

    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;
    public float arrowSpeed = 15f;
    private Vector3 lastDirection = Vector3.forward;

    private CharacterController controller;
    private Animator animator;

    public bool isDead = false;
    private PlayerVisual playerVisual;
    public NetworkVariable<int> currentHealthNet = new NetworkVariable<int>(
    1000, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private int lavaTilesTouched = 0;
    private float lavaDamageTimer = 0f;
    private float lavaDamageInterval = 0.5f;
    public static bool GameHasStarted = false;

    // AI movement flavor
    private float strafeInterval = 2f;
    private float strafeTimer = 0f;
    private float strafeDirection = 0f;

    private float nextIdlePauseIn = 0f;

    private bool isAttacking = false;
    private Vector3 smoothMoveDirection = Vector3.zero;
    private Vector3 smoothVelocity = Vector3.zero;

    [SerializeField] private AudioClip arrowFireClip;
    [SerializeField] private AudioClip lavaSizzleClip;
    private AudioSource lavaSource;
    [SerializeField] private AudioClip deathClip;

    private AudioSource sfxSource;
    public GameObject spikeballProjectilePrefab;
    private float spikeballCooldown = 8f;
    private float spikeballTimer = 0f;
    public GameObject shieldPrefab;
    private float shieldCooldown = 13f;
    private float shieldTimer = 0f;
    private NetworkObject activeShieldObject;
    private MonoBehaviour targetEnemy;
    [SerializeField] private GameObject fireVFXObject;
    private Coroutine fireVFXCoroutine;
    public NetworkVariable<ulong> BotClientId = new NetworkVariable<ulong>(
    0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isWalkingNet = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    public NetworkVariable<int> animationTriggerNet = new NetworkVariable<int>(
    0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isDeadNet = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private CharacterAudioProfile audioProfile;

    private const int ANIM_NONE = 0;
    private const int ANIM_ATTACK = 1;
    private const int ANIM_DRINK = 2;
    private const int ANIM_TOSS = 3;


    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 1f;
        sfxSource.playOnAwake = false;
        lavaSource = gameObject.AddComponent<AudioSource>();
        lavaSource.clip = lavaSizzleClip;
        lavaSource.loop = true;
        lavaSource.spatialBlend = 0f;
        lavaSource.volume = 0.5f;
        lavaSource.playOnAwake = false;

    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerVisual = GetComponent<PlayerVisual>();

        var playerData = MultiplayerManager.Instance.GetPlayerDataFromClientId(BotClientId.Value);
        playerVisual.SetPlayerModel(playerData.modelId);

        animator = playerVisual.CurrentAnimator;
        audioProfile = playerVisual.CurrentAudioProfile;

        if (animator == null)
        {
            Debug.LogError($"❌ Bot: Animator is null after model assignment! (ClientId: {BotClientId.Value})");
        }

        // ✅ Ensure animator is assigned on all clients, not just server
        isWalkingNet.OnValueChanged += (oldVal, newVal) =>
        {
            if (animator != null)
                animator.SetBool("isWalking", newVal);
        };

        animationTriggerNet.OnValueChanged += (oldVal, newVal) =>
        {
            if (animator == null) return;

            switch (newVal)
            {
                case ANIM_ATTACK:
                    animator.SetTrigger("Attack");
                    break;
                case ANIM_DRINK:
                    animator.SetTrigger("Drink");
                    break;
                case ANIM_TOSS:
                    animator.SetTrigger("Toss");
                    break;
            }
        };

        isDeadNet.OnValueChanged += (oldVal, newVal) =>
        {
            if (newVal)
            {
                // Only trigger animation if not already dead (double protection)
                if (!isDead)
                {
                    isDead = true;
                    if (animator != null) animator.SetTrigger("Die");
                    if (controller != null) controller.enabled = false;
                }
            }
        };


        if (IsServer)
        {
            StartCoroutine(LookForPlayer());
        }
    }



    private void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        attackCooldown = baseAttackCooldown + UnityEngine.Random.Range(-0.3f, 0.3f);
        ScheduleNextPause();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || isDead) return;

        if (other.CompareTag("Lava"))
        {
            lavaTilesTouched++;
            if (lavaTilesTouched == 1)
            {
                TakeDamage(10);
                lavaDamageTimer = 0f;

                PlayLavaSoundClientRpc();
                ShowLavaFireVFXClientRpc(true);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsServer || isDead) return;

        if (other.CompareTag("Lava"))
        {
            lavaTilesTouched--;
            if (lavaTilesTouched <= 0)
            {
                lavaTilesTouched = 0;
                lavaDamageTimer = 0f;

                StopLavaSoundClientRpc();
                ShowLavaFireVFXClientRpc(false);
            }
        }
    }


    [ClientRpc]
    private void PlayLavaSoundClientRpc()
    {
        if (lavaSizzleClip != null && lavaSource != null && !lavaSource.isPlaying)
        {
            lavaSource.Play();
        }
    }

    [ClientRpc]
    private void StopLavaSoundClientRpc()
    {
        if (lavaSource != null && lavaSource.isPlaying)
        {
            lavaSource.Stop();
        }
    }


    public event Action<int> OnBotDamaged;

    public int GetHealth() => currentHealthNet.Value;

    public void TakeDamage(int damage)
    {
        if (isDead || GameOverUI.GameHasEnded)
            return;

        currentHealthNet.Value -= damage;
        currentHealthNet.Value = Mathf.Max(currentHealthNet.Value, 0);
        OnBotDamaged?.Invoke(currentHealthNet.Value);

        if (currentHealthNet.Value <= 0)
        {
            TriggerDeath();
        }
    }

    public void ApplySlowEffect(float duration, float slowMultiplier)
    {
        StartCoroutine(ApplySlowEffectCoroutine(duration, slowMultiplier));
    }

    private IEnumerator ApplySlowEffectCoroutine(float duration, float slowMultiplier)
    {
        float originalSpeed = moveSpeed;
        moveSpeed *= slowMultiplier;

        // ✅ Activate slow trail effect
        TrailRenderer slowTrailEffect = GetComponentInChildren<TrailRenderer>(true);
        if (slowTrailEffect != null)
        {
            slowTrailEffect.gameObject.SetActive(true);
        }

        SkinnedMeshRenderer bodyRenderer = null;
        List<Renderer> headRenderers = new List<Renderer>();

        Material[] originalBodyMaterials = null;
        List<Color[]> originalHeadColors = new List<Color[]>();

        Transform modelRoot = transform.Find("ModelRoot");
        if (modelRoot != null)
        {
            foreach (Transform variant in modelRoot)
            {
                if (!variant.gameObject.activeSelf)
                    continue;

                Transform nested = variant.Find(variant.name);
                if (nested != null)
                {
                    bodyRenderer = nested.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (bodyRenderer != null)
                    {
                        Material[] materials = bodyRenderer.materials;
                        originalBodyMaterials = new Material[materials.Length];
                        for (int i = 0; i < materials.Length; i++)
                        {
                            originalBodyMaterials[i] = new Material(materials[i]);
                            materials[i].color = Color.cyan;
                        }
                        bodyRenderer.materials = materials;
                    }
                }

                string headPath = "RigPelvis/RigSpine1/RigSpine2/RigSpine3/RigRibcage/RigNeck/RigHead/Head";
                Transform head = variant.Find(headPath);
                if (head != null)
                {
                    foreach (Transform child in head)
                    {
                        Renderer r = child.GetComponent<Renderer>();
                        if (r != null)
                        {
                            Material[] headMats = r.materials;
                            Color[] originalColors = new Color[headMats.Length];
                            for (int i = 0; i < headMats.Length; i++)
                            {
                                originalColors[i] = headMats[i].color;
                                headMats[i].color = Color.cyan;
                            }
                            r.materials = headMats;
                            headRenderers.Add(r);
                            originalHeadColors.Add(originalColors);
                        }
                    }
                }

                break;
            }
        }

        yield return new WaitForSeconds(duration);

        moveSpeed = originalSpeed;

        // ✅ Restore trail effect
        if (slowTrailEffect != null)
        {
            slowTrailEffect.gameObject.SetActive(false);
        }

        // ✅ Restore body colors
        if (bodyRenderer != null && originalBodyMaterials != null)
        {
            Material[] currentMaterials = bodyRenderer.materials;
            for (int i = 0; i < currentMaterials.Length; i++)
            {
                currentMaterials[i].color = originalBodyMaterials[i].color;
            }
            bodyRenderer.materials = currentMaterials;
        }

        // ✅ Restore head colors
        for (int i = 0; i < headRenderers.Count; i++)
        {
            Renderer r = headRenderers[i];
            Color[] originalColors = originalHeadColors[i];
            Material[] mats = r.materials;
            for (int j = 0; j < mats.Length; j++)
            {
                mats[j].color = originalColors[j];
            }
            r.materials = mats;
        }
    }


    private IEnumerator LookForPlayer()
    {
        while (targetEnemy == null)
        {
            foreach (var player in FindObjectsOfType<Player>())
            {
                if (player.IsOwner)
                {
                    targetEnemy = player;
                    break;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void ScheduleNextPause()
    {
        nextIdlePauseIn = UnityEngine.Random.Range(3f, 6f);
    }

    private IEnumerator PauseMovementBriefly()
    {
        float pauseDuration = UnityEngine.Random.Range(0.4f, 1.2f);
        yield return new WaitForSeconds(pauseDuration);
    }

    private void Update()
    {
        if (GameOverUI.GameHasEnded && lavaSource != null && lavaSource.isPlaying)
        {
            lavaSource.Stop();
        }
        if (!GameHasStarted && lavaSource != null && lavaSource.isPlaying)
        {
            lavaSource.Stop();
        }

        if (IsServer && GameOverUI.GameHasEnded)
        {
            isWalkingNet.Value = false;
        }

        if (Time.frameCount % 30 == 0)
            targetEnemy = FindNearestEnemy();

        if (!IsServer || isDead || targetEnemy == null || !GameHasStarted || GameOverUI.GameHasEnded)
            return;

        // Check if target is dead
        bool targetDead = false;
        if (targetEnemy is Player p) targetDead = p.isDead;
        if (targetEnemy is Bot b) targetDead = b.isDead || b.currentHealthNet.Value <= 0;

        if (targetDead)
        {
            isWalkingNet.Value = false;
            return;
        }


        spikeballTimer += Time.deltaTime;
        if (spikeballTimer >= spikeballCooldown)
        {
            FireSpikeballAtPlayer();
            spikeballTimer = 0f;
        }

        shieldTimer += Time.deltaTime;
        if (shieldTimer >= shieldCooldown)
        {
            ActivateShield();
            shieldTimer = 0f;
        }

        Vector3 dir = targetEnemy.transform.position - transform.position;
        dir.y = 0f;
        float dist = dir.magnitude;
        Vector3 moveDir = dir.normalized;

        Vector3 finalMove = Vector3.zero;

        if (dist > attackRange)
        {
            // move directly to player
            finalMove = moveDir * moveSpeed;
        }
        else
        {
            // Strafe around player while in range
            strafeTimer += Time.deltaTime;
            if (strafeTimer >= strafeInterval)
            {
                strafeTimer = 0f;
                strafeDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            }

            Vector3 strafe = Vector3.Cross(Vector3.up, moveDir) * strafeDirection;
            finalMove = strafe.normalized * moveSpeed * 0.4f;

            TryAttack(moveDir);
        }

        // Movement and animation
        if (!isAttacking && finalMove != Vector3.zero)
        {
            // Always face the direction you're moving
            Quaternion targetRot = Quaternion.LookRotation(finalMove);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);

            controller.SimpleMove(finalMove);


            isWalkingNet.Value = true;
        }
        else
        {
            isWalkingNet.Value = false;
        }

        // Lava damage
        if (lavaTilesTouched > 0)
        {
            lavaDamageTimer += Time.deltaTime;
            if (lavaDamageTimer >= lavaDamageInterval)
            {
                lavaDamageTimer = 0f;
                TakeDamage(10);
            }
        }

        if (isDead && lavaSource != null && lavaSource.isPlaying)
        {
            lavaSource.Stop();
        }

    }

    private void ActivateShield()
    {
        if (isDead || activeShieldObject != null) return;

        GameObject shieldObj = Instantiate(shieldPrefab, transform.position, Quaternion.identity);
        NetworkObject netObj = shieldObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
            netObj.TrySetParent(GetComponent<NetworkObject>(), true);

            // Set position and scale immediately on server
            shieldObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            shieldObj.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);

            var shieldBlocker = shieldObj.GetComponent<ShieldBlocker>();
            if (shieldBlocker != null)
                shieldBlocker.OwnerClientId = BotClientId.Value;

            activeShieldObject = netObj;

            // Ensure position fix explicitly on clients:
            FixShieldLocalPositionClientRpc(netObj.NetworkObjectId);

            StartCoroutine(DestroyShieldAfterDuration(netObj));
        }

        SoundManager.Instance.PlaySoundByName("shield_activate");

        if (IsServer && animator != null)
            StartCoroutine(SetAnimationTrigger(ANIM_DRINK));
    }

    [ClientRpc]
    private void FixShieldLocalPositionClientRpc(ulong shieldNetworkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(shieldNetworkObjectId, out NetworkObject shieldNetObj))
        {
            shieldNetObj.TrySetParent(this.GetComponent<NetworkObject>(), true);
            shieldNetObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            shieldNetObj.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
        }
    }


    private IEnumerator DestroyShieldAfterDuration(NetworkObject netObj)
    {
        yield return new WaitForSeconds(1f);
        if (netObj != null && netObj.IsSpawned)
        {
            ShieldBlocker blocker = netObj.GetComponent<ShieldBlocker>();
            if (blocker != null)
            {
                blocker.ForceFadeOnly();
                yield return new WaitForSeconds(0.6f);
            }

            if (netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }

            activeShieldObject = null;
        }
    }



    private void FireSpikeballAtPlayer()
    {
        Vector3 direction = (targetEnemy.transform.position - transform.position).normalized;
        Vector3 spawnPosition = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position + Vector3.up;

        GameObject spikeball = Instantiate(spikeballProjectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
        NetworkObject spikeballNetObj = spikeball.GetComponent<NetworkObject>();
        Projectile spikeballScript = spikeball.GetComponent<Projectile>();

        if (spikeballNetObj != null && spikeballScript != null)
        {
            spikeballNetObj.Spawn(true);
            spikeballScript.InitializeProjectile(spawnPosition, direction, BotClientId.Value);
            spikeballScript.TeleportProjectileClientRpc(spawnPosition, direction);

            Collider spikeballCollider = spikeball.GetComponent<Collider>();
            foreach (var botCollider in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(spikeballCollider, botCollider);
            }
        }

        if (IsServer && animator != null)
            StartCoroutine(SetAnimationTrigger(ANIM_TOSS));
    }

    private IEnumerator SetAnimationTrigger(int trigger)
    {
        animationTriggerNet.Value = trigger;
        yield return new WaitForSeconds(0.1f);
        animationTriggerNet.Value = ANIM_NONE;
    }




    private void TryAttack(Vector3 direction)
    {
        if (isAttacking || isDead || targetEnemy == null || !GameHasStarted || GameOverUI.GameHasEnded)
            return;

        bool targetDead = false;
        if (targetEnemy is Player p) targetDead = p.isDead;
        if (targetEnemy is Bot b) targetDead = b.isDead || b.currentHealthNet.Value <= 0;
        if (targetDead)
            return;

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown)
        {
            isAttacking = true; // Set immediately!
            hasFiredThisAttack = false; // Make sure to reset for this attack window

            attackCooldown = baseAttackCooldown + UnityEngine.Random.Range(-0.3f, 0.3f);
            attackTimer = 0f;
            lastDirection = direction.normalized;
            transform.rotation = Quaternion.LookRotation(lastDirection);

            TriggerAttackAnim();
        }
    }




    private bool hasFiredThisAttack = false;

    public void TryFireArrow()
    {
        if (!IsServer) return; // <--- Only let server do this logic
        if (hasFiredThisAttack || isDead || !GameHasStarted || GameOverUI.GameHasEnded || targetEnemy == null)
            return;

        bool targetDead = false;
        if (targetEnemy is Player p) targetDead = p.isDead;
        if (targetEnemy is Bot b) targetDead = b.isDead || b.currentHealthNet.Value <= 0;
        if (targetDead)
            return;

        hasFiredThisAttack = true;

        if (arrowFireClip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(arrowFireClip);
        }

        ShootArrowServerRpc(arrowSpawnPoint.position, lastDirection);
        isAttacking = false;
    }



    private void TriggerAttackAnim()
    {
        if (IsServer && animator != null)
        {
            StartCoroutine(SetAnimationTrigger(ANIM_ATTACK));

            StartCoroutine(AttackTimeoutCoroutine());
        }
    }

    // Safety coroutine
    private IEnumerator AttackTimeoutCoroutine()
    {
        yield return new WaitForSeconds(1.5f); // Match this to your longest attack animation duration
        if (isAttacking)
        {
            Debug.LogWarning("Attack timeout triggered. Resetting isAttacking flag.");
            isAttacking = false;
        }
    }



    [ServerRpc(RequireOwnership = false)]
    private void ShootArrowServerRpc(Vector3 position, Vector3 direction)
    {
        GameObject arrow = Instantiate(arrowPrefab, position, Quaternion.LookRotation(direction));
        Arrow arrowScript = arrow.GetComponent<Arrow>();

        if (arrowScript != null)
        {
            arrowScript.InitializeArrow(position, direction, GetComponent<NetworkObject>(), BotClientId.Value);
        }


        arrow.GetComponent<NetworkObject>().Spawn(true);
        arrowScript.TeleportArrowClientRpc(position, direction);
    }

    public void TriggerDeath()
    {
        if (isDead) return;
        isDead = true;
        isDeadNet.Value = true;

        PlayDeathSoundClientRpc();

        if (lavaSource != null && lavaSource.isPlaying)
        {
            lavaSource.Stop();
        }

        animator.SetTrigger("Die");
        controller.enabled = false;

        DisableAllColliders();

        if (IsServer)
        {
            GameTimer timer = FindObjectOfType<GameTimer>();
            if (timer != null)
            {
                timer.QueueCheckEndGame();
            }
        }
    }

    [ClientRpc]
    private void PlayDeathSoundClientRpc()
    {
        // Play the death sound only if you have the audio profile and source
        if (audioProfile != null && audioProfile.deathClip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(audioProfile.deathClip);
        }
    }


    private void DisableAllColliders()
    {
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        // (Optional) If you want to keep a trigger for VFX or UI, filter here.
    }

    private MonoBehaviour FindNearestEnemy()
    {
        MonoBehaviour nearest = null;
        float nearestDist = float.MaxValue;

        int myTeam = MultiplayerManager.Instance.GetTeamIndexForClient(BotClientId.Value);

        // 1. Check all alive Players (including you)
        foreach (var player in FindObjectsOfType<Player>())
        {
            if (player.isDead) continue;
            int playerTeam = MultiplayerManager.Instance.GetTeamIndexForClient(player.OwnerClientId);
            if (playerTeam == myTeam) continue; // skip self/ally

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < nearestDist)
            {
                nearest = player;
                nearestDist = dist;
            }
        }

        // 2. Check all alive Bots (skip self, and skip ally bots)
        foreach (var bot in FindObjectsOfType<Bot>())
        {
            if (bot == this || bot.isDead) continue;
            int botTeam = MultiplayerManager.Instance.GetTeamIndexForClient(bot.BotClientId.Value);
            if (botTeam == myTeam) continue;

            float dist = Vector3.Distance(transform.position, bot.transform.position);
            if (dist < nearestDist)
            {
                nearest = bot;
                nearestDist = dist;
            }
        }

        return nearest;
    }

    [ClientRpc]
    private void ShowLavaFireVFXClientRpc(bool enable)
    {
        if (fireVFXObject == null) return;

        if (enable)
        {
            if (fireVFXCoroutine != null) StopCoroutine(fireVFXCoroutine);
            fireVFXObject.SetActive(true);
        }
        else
        {
            // Fire stays for 1s after leaving lava
            if (fireVFXCoroutine != null) StopCoroutine(fireVFXCoroutine);
            fireVFXCoroutine = StartCoroutine(DisableFireVFXAfterDelay(0.3f));
        }
    }

    private IEnumerator DisableFireVFXAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (fireVFXObject != null)
            fireVFXObject.SetActive(false);
    }


}
