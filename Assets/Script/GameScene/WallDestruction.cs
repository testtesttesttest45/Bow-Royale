using Unity.Netcode;
using UnityEngine;

public class WallDestruction : NetworkBehaviour
{
    [Header("Structure")]
    public GameObject fracturedWall;         // The 'Fractured Wall' empty parent (inactive at start)
    public GameObject originalWall;          // The solid wall
    public GameObject crackStage1Group;      // Crack visual group (inactive at start)
    public GameObject crackStage2Group;      // Crack visual group (inactive at start)
    public GameObject fracturedTower;        // The mesh group to explode (inactive at start)

    public int hitThreshold = 3;
    private NetworkVariable<int> syncedHits = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Sound Effects")]
    public AudioClip crackSound;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (fracturedWall != null) fracturedWall.SetActive(false);
        if (crackStage1Group != null) crackStage1Group.SetActive(false);
        if (crackStage2Group != null) crackStage2Group.SetActive(false);
        if (fracturedTower != null) fracturedTower.SetActive(false);
    }

    public void RegisterHit()
    {
        if (!IsServer) return;
        syncedHits.Value++;
        if (syncedHits.Value >= hitThreshold)
        {
            TriggerWallDestructionClientRpc();
        }
        else
        {
            UpdateCrackStageClientRpc(syncedHits.Value);
        }
        if (crackSound != null)
            PlayCrackSoundClientRpc();
    }

    [ClientRpc]
    private void UpdateCrackStageClientRpc(int hits)
    {
        if (hits == 1 && crackStage1Group != null)
            crackStage1Group.SetActive(true);
        else if (hits == 2 && crackStage2Group != null)
            crackStage2Group.SetActive(true);
    }

    [ClientRpc]
    private void TriggerWallDestructionClientRpc()
    {
        // Hide cracks and main wall
        if (crackStage1Group != null) crackStage1Group.SetActive(false);
        if (crackStage2Group != null) crackStage2Group.SetActive(false);
        if (originalWall != null) originalWall.SetActive(false);

        // Activate fractured wall group and fractured tower
        if (fracturedWall != null) fracturedWall.SetActive(true);
        if (fracturedTower != null) fracturedTower.SetActive(true);

        // Explode all pieces in FracturedTower
        foreach (Transform piece in fracturedTower.transform)
        {
            Rigidbody rb = piece.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddExplosionForce(200f, fracturedTower.transform.position, 10f);
            }

            // Remove the collider for "clean up"
            Collider col = piece.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
        }
    }


    [ClientRpc]
    private void PlayCrackSoundClientRpc()
    {
        if (crackSound != null)
            audioSource.PlayOneShot(crackSound, 1f);
    }

    public void SetWallMaterial(Material mat)
    {
        Debug.Log("HERE BRO COME111111");
        if (originalWall != null)
        {
            Debug.Log("HERE BRO COME");
            var origRenderer = originalWall.GetComponent<MeshRenderer>();
            if (origRenderer != null) origRenderer.material = mat;
        }
        // Fractured pieces
        if (fracturedTower != null)
        {
            foreach (var piece in fracturedTower.GetComponentsInChildren<MeshRenderer>())
                piece.material = mat;
        }
    }

}
