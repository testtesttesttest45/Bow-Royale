using UnityEngine;

public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        // Always face camera
        transform.forward = Camera.main.transform.forward;
    }
}
