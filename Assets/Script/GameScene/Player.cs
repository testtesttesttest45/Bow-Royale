using UnityEngine;
using System.Collections;
using Unity.Netcode;
using UnityEngine.UI;
using Unity.Netcode.Components;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class Player : NetworkBehaviour
{
    public float moveSpeed = 5f;
    private float baseMoveSpeed;
    public MovementJoystick movementJoystick;
    public Animator animator;
    private CharacterController controller;

    private AudioSource footstepSource;
    public AudioSource lavaSource;

    public AudioClip footstepClip;
    public AudioClip lavaSizzleClip;

    private bool isWalking = false;
    // public int lavaTilesTouched = 0;

    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;
    public float arrowSpeed = 15f;
    public AudioClip arrowFireClip;

    public float lavaDamageTimer = 0f;
    private float lavaDamageInterval = 0.5f;
    public HealthBarController healthBarController;

    public AudioClip deathClip;
    private AudioSource deathSource;

    public GameTimer gameTimer;

    public SpikeballAbility spikeballAbility;

    [HideInInspector] public bool isAiming = false;
    public bool isDead { get; private set; } = false;

    public float attackSpeed = 10f;

    public TumbleAbility tumbleAbility;
    public ShieldAbility shieldAbility;
    public SupershotAbility supershotAbility;

    public bool isGameStarted = false;

    public GameObject movementJoystickPrefab;
    public GameObject abilityPanelPrefab;
    public GameObject spikeballIndicatorPrefab;
    public GameObject tumbleIndicatorPrefab;
    public GameObject swipePanelPrefab;
    public GameObject basicAttackIndicatorRootPrefab;

    private NetworkAnimator netAnimator;
    private bool animatorReady = false;

    public NetworkVariable<bool> networkGameStarted = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isWalkingNetwork = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    public NetworkVariable<bool> isWalkingNet = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<float> networkSupershotChargeAmount = new NetworkVariable<float>(
    0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private List<Vector3> spawnPositionList;

    public GameObject spikeballProjectilePrefab;

    private AudioSource abilitySource;

    private Vector3 storedAttackDirection = Vector3.forward;
    private bool isArrowSpawnPointReady = false;

    public ChargeBarController worldChargeBarController;
    private float lastSyncedCharge = 0f;
    private float smoothCharge = 0f;
    private float chargeLerpSpeed = 5f;

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
    1000,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> isDeadNet = new NetworkVariable<bool>(
    false,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );

    private bool attackMovementLock = false;

    public TrailRenderer slowTrailEffect;

    [SerializeField] private GameObject fireVFXObject;
    private Coroutine fireVFXCoroutine;
    private CharacterAudioProfile audioProfile;
    public GameObject ownerMarker;

    private void Awake()
    {
        netAnimator = GetComponent<NetworkAnimator>();

        if (netAnimator != null && netAnimator.Animator == null)
        {
            animator = GetComponent<Animator>();
            netAnimator.Animator = animator;
        }
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        networkGameStarted.OnValueChanged += (oldVal, newVal) => { };

        PlayerData data = MultiplayerManager.Instance.GetPlayerDataFromClientId(OwnerClientId);
        var visual = GetComponentInChildren<PlayerVisual>();
        visual.SetPlayerModel(data.modelId);
        audioProfile = visual.CurrentAudioProfile;

        animator = visual.CurrentAnimator;

        if (netAnimator != null && animator != null)
        {
            netAnimator.Animator = animator;
            animatorReady = true;
        }
        else
        {
            Debug.LogError("❌ Failed to assign animator to NetworkAnimator!");
        }

        if (IsServer)
        {
            int index = MultiplayerManager.Instance.GetPlayerDataIndexFromClientId(OwnerClientId);
            if (index >= 0 && index < spawnPositionList.Count)
            {
                transform.position = spawnPositionList[index];

                if (index == 1)
                {
                    transform.rotation = Quaternion.Euler(0f, 165f, 0f);
                }
            }
            else
            {
                Debug.LogWarning($"❓ Spawn index {index} out of range! Defaulting to (0,0,0).");
                transform.position = Vector3.zero;
            }
        }

        StartCoroutine(DelayedSetModelAndAbilities());
        Transform barHolder = transform.Find("HealthBarHolder/HealthCanvas/ChargeBarBG");
        if (barHolder != null)
        {
            worldChargeBarController = barHolder.GetComponent<ChargeBarController>();
            if (worldChargeBarController == null)
                Debug.LogError("❌ ChargeBarController not found on ChargeBar object!");
        }
        else
        {
            Debug.LogError("❌ ChargeBar transform not found!");
        }

        isDeadNet.OnValueChanged += (oldVal, newVal) =>
        {
            if (newVal && !isDead)
            {
                isDead = true;
                TriggerDeathLocal();
            }
        };

        if (isDeadNet.Value && !isDead)
        {
            isDead = true;
            TriggerDeathLocal();
        }
    }

    void LateUpdate()
    {
        if (worldChargeBarController != null)
        {
            if (IsOwner)
            {
                lastSyncedCharge = networkSupershotChargeAmount.Value;
                smoothCharge = Mathf.Lerp(smoothCharge, lastSyncedCharge, Time.deltaTime * chargeLerpSpeed);
                worldChargeBarController.SetCharge(smoothCharge, true);
            }
            else
            {
                worldChargeBarController.SetCharge(networkSupershotChargeAmount.Value, false);
            }
        }
    }

    private IEnumerator DelayedSetModelAndAbilities()
    {
        yield return null;

        PlayerData data = MultiplayerManager.Instance.GetPlayerDataFromClientId(OwnerClientId);
        var visual = GetComponentInChildren<PlayerVisual>();
        visual.SetPlayerModel(data.modelId);

        animator = visual.CurrentAnimator;

        if (IsOwner)
        {
            arrowSpawnPoint = transform.Find("ArrowSpawnPoint");
            if (arrowSpawnPoint == null)
            {
                Debug.LogError("❌ ArrowSpawnPoint NOT found after model was assigned.");
            }
            else
            {
                int waitFrames = 0;
                while (arrowSpawnPoint.position == Vector3.zero && waitFrames < 30)
                {
                    waitFrames++;
                    yield return null;
                }

                if (arrowSpawnPoint.position != Vector3.zero)
                {
                    isArrowSpawnPointReady = true;
                    Debug.Log($"✅ ArrowSpawnPoint confirmed after {waitFrames} frame(s)");
                }
                else
                {
                    Debug.LogError("❌ ArrowSpawnPoint remained at origin after waiting.");
                }
            }

            spikeballAbility.AssignAnimator(animator);
            tumbleAbility.AssignAnimator(animator);
            shieldAbility.AssignAnimator(animator);
            supershotAbility.AssignAnimator(animator);
            spikeballAbility.Initialize();
            tumbleAbility.Initialize();
        }

    }

    void Start()
    {
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 1;

        controller = GetComponent<CharacterController>();
        baseMoveSpeed = moveSpeed;
        GameManager.Instance.OnStateChanged += GameManager_OnStateChanged;
        gameTimer = FindObjectOfType<GameTimer>();

        var canvas = GameObject.Find("Screen Canvas").transform;
        var worldCanvas = GameObject.Find("World Canvas").transform;

        Transform countdownUITransform = canvas.Find("GameStartCountdownUI");
        int insertIndex = countdownUITransform != null ? countdownUITransform.GetSiblingIndex() : canvas.childCount;

        // Instantiate AbilityPanel for ALL players, initially inactive if not owner
        GameObject abilityPanelObj = Instantiate(abilityPanelPrefab, canvas);
        abilityPanelObj.transform.SetSiblingIndex(insertIndex++);
        abilityPanelObj.SetActive(IsOwner); // Only owner can interact

        // Get ability scripts from panel
        spikeballAbility = abilityPanelObj.GetComponentInChildren<SpikeballAbility>(true);
        tumbleAbility = abilityPanelObj.GetComponentInChildren<TumbleAbility>(true);
        supershotAbility = abilityPanelObj.GetComponentInChildren<SupershotAbility>(true);
        shieldAbility = abilityPanelObj.GetComponentInChildren<ShieldAbility>(true);

        // Link common references
        spikeballAbility.player = this;
        tumbleAbility.player = this;
        supershotAbility.player = this;
        shieldAbility.player = this;

        spikeballAbility.playerTransform = transform;
        tumbleAbility.playerTransform = transform;
        shieldAbility.playerTransform = transform;

        // assign healthbar

        var abilityController = GetComponentInChildren<AbilityController>();
        if (abilityController != null)
        {
            spikeballAbility.abilityController = abilityController;
            tumbleAbility.abilityController = abilityController;
            shieldAbility.abilityController = abilityController;
            supershotAbility.abilityController = abilityController;

            abilityController.spikeball = spikeballAbility;
            abilityController.shield = shieldAbility;
            abilityController.tumble = tumbleAbility;
            abilityController.supershot = supershotAbility;
            abilityController.gameTimer = gameTimer;

            abilityController.InitializeAbilities();
        }
        else
        {
            Debug.LogError("❌ AbilityController not found in Player prefab!");
        }

        if (arrowSpawnPoint == null)
        {
            arrowSpawnPoint = transform.Find("ArrowSpawnPoint");
            isArrowSpawnPointReady = arrowSpawnPoint != null && arrowSpawnPoint.position != Vector3.zero;
        }

        if (supershotAbility != null && arrowSpawnPoint != null)
        {
            supershotAbility.arrowSpawnPoint = arrowSpawnPoint;
        }
        else
        {
            Debug.LogError("❌ Failed to assign Supershot.arrowSpawnPoint properly!");
        }

        // Additional Owner-only setup
        if (IsOwner)
        {
            var countdownUIComponent = GameStartCountdownUI.Instance;
            if (countdownUIComponent != null)
                countdownUIComponent.InjectDependencies(gameTimer, this);
            else
                Debug.LogError("❌ Couldn't find GameStartCountdownUI during Start()");

            GameObject joystickObj = Instantiate(movementJoystickPrefab, canvas);
            joystickObj.transform.SetSiblingIndex(insertIndex++);
            joystickObj.SetActive(true);
            movementJoystick = joystickObj.GetComponent<MovementJoystick>();

            GameObject swipePanelObj = Instantiate(swipePanelPrefab, canvas);
            swipePanelObj.transform.SetSiblingIndex(insertIndex++);
            swipePanelObj.SetActive(true);
            var swipeAttack = swipePanelObj.GetComponent<SwipeAttack>();
            swipeAttack.player = this;

            GameObject basicAttackIndicatorRoot = Instantiate(basicAttackIndicatorRootPrefab, worldCanvas);
            Transform indicatorCanvas = basicAttackIndicatorRoot.transform.Find("Canvas");
            swipeAttack.indicator = indicatorCanvas.Find("BasicAttackIndicator");
            swipeAttack.RedLineHolder = indicatorCanvas.Find("RedLineHolder/BasicAttackDirection");

            swipeAttack.cancelAreaGroup = swipePanelObj.transform.Find("AbilityCancel").gameObject;
            swipeAttack.cancelArea = swipeAttack.cancelAreaGroup.transform.Find("CancelArea").gameObject;
            swipeAttack.cancelAreaBorder = swipePanelObj.transform.Find("AbilityCancel/CancelAreaFrame").gameObject;

            swipeAttack.attackJoystickBG = swipePanelObj.transform.Find("Attackstick Background").gameObject;
            swipeAttack.attackJoystickHandle = swipePanelObj.transform.Find("Attackstick Background/Attackstick").gameObject;

            abilityPanelObj.SetActive(true);
            abilityPanelObj.transform.SetSiblingIndex(insertIndex++);

            GameObject spikeballIndicatorObj = Instantiate(spikeballIndicatorPrefab, worldCanvas);
            var spikeballImage = spikeballIndicatorObj.GetComponentInChildren<Image>();
            spikeballAbility.indicator = spikeballImage.transform;
            spikeballAbility.spellIndicator = spikeballImage.gameObject;

            spikeballAbility.abilityButton.onClick.RemoveAllListeners();
            spikeballAbility.abilityButton.onClick.AddListener(() =>
            {
                spikeballAbility.OnHoldStart(null);
            });

            GameObject tumbleIndicatorObj = Instantiate(tumbleIndicatorPrefab, worldCanvas);
            var tumbleImage = tumbleIndicatorObj.GetComponentInChildren<Image>();
            tumbleAbility.indicator = tumbleImage.transform;
            tumbleAbility.spellIndicator = tumbleImage.gameObject;

            // UI-only references
            supershotAbility.chargeBarController = worldChargeBarController;
            shieldAbility.playerTransform = transform;
            shieldAbility.player = this;

            Transform suddenDeathTransform = canvas.Find("SuddenDeathUI");

            if (suddenDeathTransform != null)
            {
                SuddenDeathUI suddenDeath = suddenDeathTransform.GetComponent<SuddenDeathUI>();
                if (abilityController != null)
                {
                    abilityController.suddenDeathUI = suddenDeath;
                }
            }
            else
            {
                Debug.LogWarning("⚠ SuddenDeathUI not found under Screen Canvas.");
            }

        }

        // Audio setup
        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.clip = footstepClip;
        footstepSource.loop = true;
        footstepSource.volume = 0.1f;
        footstepSource.playOnAwake = false;

        abilitySource = gameObject.AddComponent<AudioSource>();
        abilitySource.spatialBlend = 0f;
        abilitySource.volume = 1f;

        lavaSource = gameObject.AddComponent<AudioSource>();
        lavaSource.clip = lavaSizzleClip;
        lavaSource.loop = true;
        lavaSource.volume = 0.2f;
        lavaSource.playOnAwake = false;

        deathSource = gameObject.AddComponent<AudioSource>();
        deathSource.playOnAwake = false;
        deathSource.volume = 1f;
    }

    private void GameManager_OnStateChanged(object sender, EventArgs e)
    {
        if (GameManager.Instance.IsGamePlaying())
        {
            if (IsServer)
            {
                networkGameStarted.Value = true;
                gameTimer.StartTimer();
            }
            isGameStarted = true;
        }
    }

    private bool _wasInLavaLastFrame = false;
    private float _lavaCheckRadius = 0.2f;

    void Update()
    {
        if (!animatorReady || animator == null) return;
        if (!isGameStarted || isDead) return;

        Vector3 inputDir = new Vector3(movementJoystick.joystickVec.x, 0f, movementJoystick.joystickVec.y);
        Vector3 moveDirection = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f) * inputDir;

        bool isCurrentlyAttacking = attackMovementLock || IsInAttackAnimation();

        if (supershotAbility != null && supershotAbility.IsCharging())
        {
            // If player tries to move while charging, cancel the charge
            if (movementJoystick.joystickVec.magnitude > 0.15f)
            {
                supershotAbility.OnHoldRelease(null); // simulate release
            }

            return; // block movement while charging
        }

        if (!attackMovementLock && moveDirection.magnitude > 0.1f)
        {
            controller.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);

            if (!isWalking)
            {
                footstepSource.Play();
                isWalking = true;

                if (IsOwner)
                    isWalkingNet.Value = true;
            }

            Quaternion toRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, 720 * Time.deltaTime);
        }
        else
        {
            if (isWalking)
            {
                footstepSource.Stop();
                isWalking = false;

                if (IsOwner)
                    isWalkingNet.Value = false;
            }
        }


        UpdateWalkingAnimation();

        if (IsServer && !isDead && !GameOverUI.GameHasEnded)
        {
            // Are we in lava right now?
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, _lavaCheckRadius, LayerMask.GetMask("Lava"));
            bool inLava = false;
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Lava") || hit.gameObject.layer == LayerMask.NameToLayer("Lava"))
                {
                    inLava = true;
                    break;
                }
            }

            if (inLava)
            {
                // Just entered lava
                if (!_wasInLavaLastFrame)
                {
                    PlayLavaBurnClientRpc(OwnerClientId);
                    TakeDamageServerRpc(50);
                    lavaDamageTimer = 0f;

                    ShowLavaFireVFXClientRpc(true);
                }

                // Take damage every interval
                lavaDamageTimer += Time.deltaTime;
                if (lavaDamageTimer >= lavaDamageInterval)
                {
                    lavaDamageTimer = 0f;
                    TakeDamageServerRpc(50);
                }
            }
            else
            {
                // Just exited lava
                if (_wasInLavaLastFrame)
                {
                    lavaDamageTimer = 0f;
                    if (lavaSource != null && lavaSource.isPlaying)
                        lavaSource.Stop();
                    StopLavaBurnClientRpc();

                    ShowLavaFireVFXClientRpc(false);
                }
            }

            _wasInLavaLastFrame = inLava;
        }

        // Clamp Y
        Vector3 pos = transform.position;
        pos.y = 0f;
        transform.position = pos;

       

    }
    private void UpdateWalkingAnimation()
    {
        if (animator == null) return;

        bool shouldWalk = isWalkingNet.Value;

        animator.SetBool("isWalking", shouldWalk);
    }

    public bool IsInAttackAnimation()
    {
        if (animator == null) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);

        bool currentIsAttack = stateInfo.IsTag("Attack") || stateInfo.IsName("Attack");
        bool nextIsAttack = nextState.IsTag("Attack") || nextState.IsName("Attack");

        return currentIsAttack || nextIsAttack;
    }

    public void TriggerAttackAnim()
    {
        if (!isDead && netAnimator != null)
        {
            netAnimator.SetTrigger("Attack");
            StartCoroutine(UnlockMovementAfterAttack());
        }
    }

    private IEnumerator UnlockMovementAfterAttack()
    {
        yield return new WaitForSeconds(0.5f);
        attackMovementLock = false;
    }

    public void TriggerDeath()
    {
        if (isDead) return;

        isDead = true;

        if (IsServer)
            isDeadNet.Value = true;
        TriggerDeathLocal();
    }

    private void TriggerDeathLocal()
    {
        if (animator == null || controller == null) return;

        animator.SetTrigger("Die");
        controller.enabled = false;

        if (audioProfile != null && audioProfile.deathClip != null)
            deathSource.PlayOneShot(audioProfile.deathClip);

        footstepSource.Stop();

        if (lavaSource.isPlaying)
            lavaSource.Stop();
        StopSoundGlobalClientRpc("lava_burn");

        if (spikeballAbility != null) spikeballAbility.DisableAbilityOnDeath();
        if (tumbleAbility != null) tumbleAbility.DisableAbilityOnDeath();
        if (shieldAbility != null) shieldAbility.DisableAbilityOnDeath();

        //if (IsOwner && gameTimer != null)
        //    gameTimer.StopTimer();

        DisableAllColliders();
    }


    private void DisableAllColliders()
    {
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
    }

    

    [ClientRpc]
    private void PlayLavaBurnClientRpc(ulong steppingClientId, ClientRpcParams clientRpcParams = default)
    {
        // Called on all clients. Each will check if they are the one stepping on lava.
        bool isSteppingPlayer = NetworkManager.Singleton.LocalClientId == steppingClientId;
        float volume = isSteppingPlayer ? 1.0f : 0.3f;
        SoundManager.Instance.PlayLavaBurn(volume);
    }

    [ClientRpc]
    private void StopLavaBurnClientRpc()
    {
        SoundManager.Instance.StopSoundByName("lava_burn");
    }


    public void TrySwipeAttack(Vector3 direction)
    {
        if (!isGameStarted || isDead) return;
        if (IsInAttackAnimation()) return; // block if already attacking
        if (movementJoystick != null)
            movementJoystick.joystickVec = Vector2.zero;

        if (IsOwner)
        {
            attackMovementLock = true;
            StartCoroutine(PerformSwipeAttack(direction));
        }
    }

    IEnumerator PerformSwipeAttack(Vector3 direction)
    {
        transform.rotation = Quaternion.LookRotation(direction);
        storedAttackDirection = direction;

        TriggerAttackAnim();
        yield break;
    }

    public void PlayerShootArrow()
    {
        if (isDead || !IsOwner || arrowSpawnPoint == null || !isArrowSpawnPointReady) return;

        if (supershotAbility != null && supershotAbility.IsFullyCharged()) return;

        Vector3 pos = arrowSpawnPoint.position;
        Vector3 dir = storedAttackDirection;

        ShootArrowServerRpc(pos, dir);
    }

    [ServerRpc]
    public void ShootArrowServerRpc(Vector3 position, Vector3 direction, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        GameObject arrowObj = Instantiate(arrowPrefab, position, Quaternion.LookRotation(direction));
        Arrow arrow = arrowObj.GetComponent<Arrow>();
        arrow.InitializeArrow(position, direction, GetComponent<NetworkObject>(), clientId);

        NetworkObject netObj = arrowObj.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(clientId);

        arrow.TeleportArrowClientRpc(position, direction);

        // 🔊 Always play arrow fire sound
        PlayArrowFireClientRpc();

        // 🐔 30% chance to play a grunt from the player's assigned audio profile
        if (audioProfile != null && audioProfile.gruntClips != null && audioProfile.gruntClips.Length > 0 && UnityEngine.Random.value < 0.3f)
        {
            int randomIndex = UnityEngine.Random.Range(0, audioProfile.gruntClips.Length);
            PlayGruntClientRpc(randomIndex);
        }
    }

    [ClientRpc]
    private void PlayArrowFireClientRpc()
    {
        SoundManager.Instance.GetAbilitySource().PlayOneShot(SoundManager.Instance.arrowFireClip);
    }

    [ClientRpc]
    private void PlayGruntClientRpc(int index)
    {
        if (audioProfile?.gruntClips != null && index >= 0 && index < audioProfile.gruntClips.Length)
        {
            AudioClip clip = audioProfile.gruntClips[index];
            if (clip != null)
            {
                SoundManager.Instance.GetAbilitySource().PlayOneShot(clip);
            }
        }
    }

    public void FireSpikeball(Vector3 direction)
    {
        FireSpikeballServerRpc(direction);
    }

    [ServerRpc]
    private void FireSpikeballServerRpc(Vector3 direction)
    {
        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position + transform.forward;

        GameObject proj = Instantiate(spikeballProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));
        var netObj = proj.GetComponent<NetworkObject>();
        var projectile = proj.GetComponent<Projectile>();

        if (netObj != null && projectile != null)
        {
            netObj.SpawnWithOwnership(OwnerClientId);

            projectile.InitializeProjectile(spawnPos, direction, OwnerClientId);
            projectile.TeleportProjectileClientRpc(spawnPos, direction);
        }

        PlaySoundGlobalClientRpc("spikeball_throw");
    }

    public void ActivateShield()
    {
        if (isDead) return;
        if (!IsOwner) return;

        if (IsServer)
        {
            SpawnShieldServerRpc(OwnerClientId);
        }
        else
        {
            ActivateShieldServerRpc();
        }
    }

    [ServerRpc]
    private void ActivateShieldServerRpc(ServerRpcParams rpcParams = default)
    {
        SpawnShieldServerRpc(OwnerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnShieldServerRpc(ulong ownerClientId)
    {
        if (!IsServer) return;

        GameObject shieldObj = Instantiate(shieldAbility.shieldPrefab, transform.position, Quaternion.identity);
        NetworkObject networkObj = shieldObj.GetComponent<NetworkObject>();

        if (networkObj != null)
        {
            networkObj.SpawnWithOwnership(ownerClientId);

            // 🟢 Set ShieldBlocker owner field here:
            var shieldBlocker = shieldObj.GetComponent<ShieldBlocker>();
            if (shieldBlocker != null)
            {
                shieldBlocker.OwnerClientId = ownerClientId;
                shieldBlocker.audioProfile = this.audioProfile;  // <-- add this line
            }

            networkObj.TrySetParent(this.GetComponent<NetworkObject>(), true);

            shieldObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            shieldObj.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);

            FixShieldLocalPositionClientRpc(networkObj.NetworkObjectId);
            AssignShieldAudioProfileClientRpc(networkObj.NetworkObjectId);
        }


        PlaySoundGlobalClientRpc("shield_activate");

        StartCoroutine(DestroyShieldAfterDuration(networkObj));
    }

    [ClientRpc]
    private void AssignShieldAudioProfileClientRpc(ulong shieldNetworkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(shieldNetworkObjectId, out NetworkObject shieldNetObj))
        {
            ShieldBlocker blocker = shieldNetObj.GetComponent<ShieldBlocker>();
            if (blocker != null)
            {
                // This 'audioProfile' is from Player.cs and is set by OnNetworkSpawn, so it's valid
                blocker.SetAudioProfile(this.audioProfile);
            }
        }
    }


    [ClientRpc]
    private void FixShieldLocalPositionClientRpc(ulong shieldNetworkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(shieldNetworkObjectId, out NetworkObject shieldNetObj))
        {
            shieldNetObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            shieldNetObj.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
        }
    }
    private IEnumerator DestroyShieldAfterDuration(NetworkObject netObj)
    {
        yield return new WaitForSeconds(shieldAbility.shieldDuration);

        if (netObj != null && netObj.IsSpawned)
        {
            var blocker = netObj.GetComponent<ShieldBlocker>();
            if (blocker != null)
            {
                blocker.ForceFadeOnly(); // just fade, no sound
                yield return new WaitForSeconds(0.6f);
            }


            if (netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }
    }

    public void TriggerDrinkAnim()
    {
        if (!isDead && netAnimator != null)
        {
            netAnimator.SetTrigger("Drink");
        }
    }

    public void ResetJoystick()
    {
        if (movementJoystick != null)
        {
            movementJoystick.ForceRelease();
        }
    }

    private ulong previewArrowId;
    private ulong chargeVFXId;

    [ServerRpc(RequireOwnership = false)]
    public void SpawnChargeVFXServerRpc()
    {
        Transform spawnPoint = supershotAbility.arrowSpawnPoint;

        var vfx = Instantiate(supershotAbility.chargeVFXPrefab, spawnPoint.position, spawnPoint.rotation);
        var netObj = vfx.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
            netObj.TrySetParent(spawnPoint.GetComponentInParent<NetworkObject>(), true);
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localRotation = Quaternion.identity;

            chargeVFXId = netObj.NetworkObjectId;

            AssignChargeVFXClientRpc(netObj.NetworkObjectId);
        }
    }

    [ClientRpc]
    private void AssignChargeVFXClientRpc(ulong netId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out NetworkObject obj))
        {
            if (NetworkManager.Singleton.LocalClientId == OwnerClientId)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    supershotAbility.AssignActiveChargeVFX(ps);
                }
            }
        }

        // ✅ Everyone plays charge sound, but only once
        if (!SoundManager.Instance.IsSoundPlaying("supershot_charge"))
        {
            PlaySoundGlobalClientRpc("supershot_charge");
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void SpawnPreviewArrowServerRpc()
    {
        Transform spawnPoint = supershotAbility.arrowSpawnPoint;
        var preview = Instantiate(supershotAbility.superArrowPreviewPrefab, spawnPoint.position + spawnPoint.forward * 1.8f, spawnPoint.rotation);
        var netObj = preview.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
            netObj.TrySetParent(spawnPoint.GetComponentInParent<NetworkObject>(), true);

            previewArrowId = netObj.NetworkObjectId;

            AssignPreviewArrowClientRpc(netObj.NetworkObjectId);
        }
    }

    [ClientRpc]
    private void AssignPreviewArrowClientRpc(ulong netId)
    {
        if (NetworkManager.Singleton.LocalClientId != OwnerClientId) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out NetworkObject obj))
        {
            supershotAbility.AssignCurrentPreviewArrow(obj.gameObject);
        }
    }

    [ServerRpc]
    public void DespawnChargeVFXServerRpc()
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(chargeVFXId, out var obj))
        {
            obj.Despawn(true);
        }
        ClearChargeVFXClientRpc();
    }

    [ClientRpc]
    private void ClearChargeVFXClientRpc()
    {
        if (NetworkManager.Singleton.LocalClientId != OwnerClientId) return;
        Debug.Log("✅ Charge VFX cleared on client: " + NetworkManager.Singleton.LocalClientId);
        supershotAbility.ClearActiveChargeVFX();
    }

    [ServerRpc]
    public void FireSuperArrowServerRpc(Vector3 position, Vector3 direction, ServerRpcParams rpcParams = default)
    {
        Vector3 offsetPos = position + direction.normalized * 1.25f;

        GameObject arrow = Instantiate(supershotAbility.superArrowPrefab, offsetPos, Quaternion.LookRotation(direction));
        var netObj = arrow.GetComponent<NetworkObject>();
        var arrowScript = arrow.GetComponent<SuperArrow>();

        if (netObj != null)
        {
            netObj.SpawnWithOwnership(OwnerClientId);

            if (arrowScript != null)
            {
                arrowScript.InitializeSuperArrow(offsetPos, direction, rpcParams.Receive.SenderClientId);
                arrowScript.TeleportArrowClientRpc(offsetPos, direction);
            }
        }
        PlaySoundGlobalClientRpc("supershot_release");
        PlaySuperGruntClientRpc();
    }

    [ClientRpc]
    private void PlaySuperGruntClientRpc()
    {
        if (audioProfile != null && audioProfile.supershotGrunt != null && abilitySource != null)
        {
            abilitySource.PlayOneShot(audioProfile.supershotGrunt);
        }
    }

    [ServerRpc]
    public void DespawnPreviewArrowServerRpc()
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(previewArrowId, out var obj))
        {
            obj.Despawn(true);
        }
        ClearPreviewArrowClientRpc();
    }

    [ClientRpc]
    private void ClearPreviewArrowClientRpc()
    {
        if (NetworkManager.Singleton.LocalClientId != OwnerClientId) return;
        supershotAbility.currentPreviewArrow = null;
    }

    [ServerRpc]
    public void UpdateSupershotChargeServerRpc(float charge)
    {
        networkSupershotChargeAmount.Value = charge;
    }



    [ServerRpc]
    public void ResetSupershotChargeServerRpc()
    {
        networkSupershotChargeAmount.Value = 0f;
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        if (GameOverUI.GameHasEnded || isDead) return;

        currentHealth.Value -= damage;

        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;
            isDeadNet.Value = true;

            GameTimer timer = FindObjectOfType<GameTimer>();
            if (timer != null)
            {
                timer.QueueCheckEndGame();
            }
        }

    }


    [ServerRpc(RequireOwnership = false)]
    public void ApplySlowEffectServerRpc(float duration, float slowMultiplier)
    {
        ApplySlowEffectClientRpc(duration, slowMultiplier);
    }

    [ClientRpc]
    private void ApplySlowEffectClientRpc(float duration, float slowMultiplier)
    {
        StartCoroutine(ApplySlowEffectCoroutine(duration, slowMultiplier));
    }

    private IEnumerator ApplySlowEffectCoroutine(float duration, float slowMultiplier)
    {
        moveSpeed = baseMoveSpeed * slowMultiplier;

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

                // ✅ Go through all renderers in active model variant
                SkinnedMeshRenderer[] renderers = variant.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var renderer in renderers)
                {
                    string lowerName = renderer.gameObject.name.ToLower();

                    if (lowerName.Contains("head"))
                    {
                        // 🎯 Head part
                        Material[] headMats = renderer.materials;
                        Color[] originalColors = new Color[headMats.Length];
                        for (int i = 0; i < headMats.Length; i++)
                        {
                            originalColors[i] = headMats[i].color;
                            headMats[i].color = Color.cyan;
                        }
                        renderer.materials = headMats;
                        headRenderers.Add(renderer);
                        originalHeadColors.Add(originalColors);
                    }
                    else if (bodyRenderer == null)
                    {
                        // 🧍‍♂️ First non-head renderer is considered body
                        bodyRenderer = renderer;
                        Material[] mats = bodyRenderer.materials;
                        originalBodyMaterials = new Material[mats.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            originalBodyMaterials[i] = new Material(mats[i]);
                            mats[i].color = Color.cyan;
                        }
                        bodyRenderer.materials = mats;
                    }
                }

                break; // ✅ Stop after active model variant
            }
        }

        yield return new WaitForSeconds(duration);

        moveSpeed = baseMoveSpeed;

        // 🔁 Restore body materials
        if (bodyRenderer != null && originalBodyMaterials != null)
        {
            Material[] currentMaterials = bodyRenderer.materials;
            for (int i = 0; i < currentMaterials.Length; i++)
            {
                currentMaterials[i].color = originalBodyMaterials[i].color;
            }
            bodyRenderer.materials = currentMaterials;
        }

        if (slowTrailEffect != null)
        {
            slowTrailEffect.gameObject.SetActive(false);
        }

        // 🔁 Restore head colors
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


    public void PlaySupershotChargeSound()
    {
        PlaySupershotChargeSoundServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlaySupershotChargeSoundServerRpc()
    {
        PlaySoundGlobalClientRpc("supershot_charge");
    }


    [ClientRpc]
    public void PlaySoundGlobalClientRpc(string soundName)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySoundByName(soundName);
        }
    }

    public void PlayTumbleSound()
    {
        PlayTumbleSoundServerRpc();
    }

    [ServerRpc]
    private void PlayTumbleSoundServerRpc()
    {
        PlaySoundGlobalClientRpc("tumble");
    }

    [ClientRpc]
    public void StopSoundGlobalClientRpc(string soundName)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopSoundByName(soundName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void StopSupershotChargeSoundServerRpc()
    {
        StopSoundGlobalClientRpc("supershot_charge");
    }

    public void TriggerPostCountdownLock()
    {
        float lockDuration = 1f;
        if (spikeballAbility != null)
            spikeballAbility.TriggerCooldownVisual(lockDuration);
        if (tumbleAbility != null)
            tumbleAbility.TriggerCooldownVisual(lockDuration);
        if (shieldAbility != null)
            shieldAbility.TriggerCooldownVisual(lockDuration);
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

    public void ShowOwnerMarker(bool show)
    {
        if (ownerMarker != null)
            ownerMarker.SetActive(show);
    }

}