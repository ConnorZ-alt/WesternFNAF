using UnityEngine;

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider))]
public class CoalPiece : MonoBehaviour
{
    private bool consumed;

    /// Call when the coal is successfully delivered (receiver caught it).
    public void Consume()
    {
        if (consumed) return;
        consumed = true;
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision c)
    {
        // If it hits anything solid before the receiver, just despawn (no meter gain)
        if (consumed) return; // receiver might have already consumed this frame
        consumed = true;
        Destroy(gameObject);
    }
}