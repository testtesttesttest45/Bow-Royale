using UnityEngine;

public class RiverScroll : MonoBehaviour
{
    public Material riverMaterial;
    public Vector2 scrollSpeed = new Vector2(0.1f, 0f); // Adjust to your flow direction/speed

    private Vector2 uvOffset = Vector2.zero;

    void Update()
    {
        uvOffset += scrollSpeed * Time.deltaTime;
        riverMaterial.SetTextureOffset("_BaseMap", uvOffset);
    }
}
