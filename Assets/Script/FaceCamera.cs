using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    void Update()
    {
        if (Camera.main != null)
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
    }
}
