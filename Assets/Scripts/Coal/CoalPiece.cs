using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CoalPiece : MonoBehaviour
{
    // This coal piece can only be in one of two states:
    // 1) Not consumed yet (normal)
    // 2) Consumed (we're done, don't do anything else)
    private bool consumed;

    [Header("Despawn Settings")]
    [SerializeField] private float destroyDelaySeconds = 0f;
    // Usually we destroy instantly. If you want effects (sound/particles), a tiny delay can help.
    
    // Other scripts can listen for when this coal gets consumed.
    // Example: play a sound, spawn particles, count stats, etc.
    // This script does NOT need to know about those systems.
    public event Action<CoalPiece, ConsumeReason> Consumed;

    // Why did it get consumed?
    // This helps other systems know the difference between:
    // - being successfully delivered
    // - getting destroyed because it hit something else
    public enum ConsumeReason
    {
        DeliveredToReceiver,
        HitSomethingElse
    }

    private void OnValidate()
    {
        // This runs in the editor when you change values in the inspector.
        // We keep the delay from going negative because that makes no sense.
        if (destroyDelaySeconds < 0f)
            destroyDelaySeconds = 0f;
    }

    /// <summary>
    /// Call this when the coal is successfully delivered (the receiver caught it).
    /// </summary>
    public void Consume()
    {
        // "Consume()" is the public way to tell the coal piece:
        // "You're done. You were delivered successfully."
        ConsumeInternal(ConsumeReason.DeliveredToReceiver);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // This runs when the coal physically hits something.
        // If it hits something solid before the receiver, it should despawn
        // and NOT give the player any coal meter gain.

        ConsumeInternal(ConsumeReason.HitSomethingElse);
    }
    
    private void ConsumeInternal(ConsumeReason reason)
    {
        // If we already got consumed earlier (maybe in the same frame),
        // we do nothing. This prevents double-destroy bugs.
        if (consumed)
            return;

        consumed = true;

        // Observer: tell anyone listening that this coal is being consumed.
        Consumed?.Invoke(this, reason);

        // Destroy the object so it can’t collide again or get counted twice.
        if (destroyDelaySeconds <= 0f)
            Destroy(gameObject);
        else
            Destroy(gameObject, destroyDelaySeconds);
    }

    // Optional helper if other scripts want to check state safely.
    public bool IsConsumed => consumed;
}
