using UnityEngine;
using UnityEngine.EventSystems;

public class ModelDragRotator : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public Transform targetToRotate;
    private float lastX;
    private bool isDragging = false;
    public float rotationSpeed = 0.5f;

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        lastX = eventData.position.x;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || targetToRotate == null) return;

        float deltaX = eventData.position.x - lastX;
        lastX = eventData.position.x;

        // Rotate the entire parent (Player 1) around Y axis
        targetToRotate.Rotate(Vector3.up, -deltaX * rotationSpeed, Space.World);
    }
}
