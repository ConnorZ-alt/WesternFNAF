using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CoalCatchSurface : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private CoalReceiver coalReceiver; 
    // Drag the parent CoalReceiver here.
    // If you forget, this script will try to find it in the parents automatically.

    [Header("Settings")]
    [SerializeField] private string trainFloorLayerName = "TrainFloor";
    // This is the layer we want this surface to be on (so the project stays organized).

    [SerializeField] private float destroyDelaySeconds = 0f;
    // Usually we destroy the coal instantly. If you ever want a tiny delay (effects), change this.
    
    // Other scripts can "listen" to this event without this script needing to know about them.
    // Example listeners: Achievements, Audio, UI, VFX, Analytics, etc.
    public event Action<CoalPiece, float> CoalCaught;
    
    // This is the "thing we do" when coal is caught.
    // Default: credit coal to the train and destroy the coal piece.
    private Action<CoalPiece> onCoalCaughtCommand;

    private Collider surfaceCollider;

    private void Awake()
    {
        // Grab the collider once so we don’t keep calling GetComponent over and over.
        surfaceCollider = GetComponent<Collider>();

        // If the reference wasn’t set in the Inspector, try to find it in the parents.
        if (coalReceiver == null)
            coalReceiver = GetComponentInParent<CoalReceiver>();

        // Set up the default command.
        // We keep this in one place so later you can swap it out if you need to.
        onCoalCaughtCommand = DefaultCreditAndDestroy;
    }

    private void Reset()
    {
        // Reset runs in the Editor when you add the component or hit Reset.
        // It helps set safe default settings so you don’t forget something important.

        var col = GetComponent<Collider>();

        // We want real collisions (physical stop), not trigger events.
        col.isTrigger = false;

        // Put this surface on the TrainFloor layer (if it exists).
        int layer = LayerMask.NameToLayer(trainFloorLayerName);
        if (layer != -1)
            gameObject.layer = layer;

        // Auto-find the receiver on the parent so setup is faster.
        if (!coalReceiver)
            coalReceiver = GetComponentInParent<CoalReceiver>();
    }

    private void OnValidate()
    {
        // OnValidate runs in the Editor when you change values in the Inspector.
        // This is a safe place to warn about bad setup.

        if (destroyDelaySeconds < 0f)
            destroyDelaySeconds = 0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // This method runs when something physically hits this surface.

        // 1) First, check if the thing that hit us is coal (or is part of a coal object).
        if (!TryGetCoalPiece(collision, out CoalPiece coalPiece))
            return;

        // 2) If we don’t have a receiver, we can’t do anything useful, so we stop here.
        if (coalReceiver == null)
        {
            Debug.LogWarning(
                $"[{nameof(CoalCatchSurface)}] No CoalReceiver found for {name}. " +
                "Set it in the Inspector or make sure it exists in a parent object.",
                this
            );
            return;
        }

        // 3) Decide how much coal this piece is worth.
        // (Right now your CoalReceiver is the one that knows the value.)
        float coalAmount = coalReceiver.GetCoalValue();

        // 4) Observer: announce that coal was caught.
        // Any other system can listen to this and react (like achievements).
        CoalCaught?.Invoke(coalPiece, coalAmount);

        // 5) Command: perform the main behavior (credit + destroy by default).
        onCoalCaughtCommand?.Invoke(coalPiece);
    }

    // ----------------------------
    // Helpers
    // ----------------------------

    private static bool TryGetCoalPiece(Collision collision, out CoalPiece coalPiece)
    {
        // This helper keeps the collision method clean and easy to read.
        // We look for a CoalPiece on the collider or somewhere above it in the hierarchy.

        coalPiece = null;

        if (collision == null || collision.collider == null)
            return false;

        coalPiece = collision.collider.GetComponentInParent<CoalPiece>();
        return coalPiece != null;
    }

    private void DefaultCreditAndDestroy(CoalPiece coalPiece)
    {
        // This is the default "what happens when coal hits the surface":
        // - find the train controller
        // - add coal
        // - destroy the coal object

        if (coalPiece == null || coalReceiver == null)
            return;

        var trainController = coalReceiver.GetTrain();
        if (trainController != null)
        {
            float coalAmount = coalReceiver.GetCoalValue();
            trainController.AddCoal(coalAmount);
        }

        // Destroy the coal piece so it can’t be counted twice.
        if (destroyDelaySeconds <= 0f)
            Destroy(coalPiece.gameObject);
        else
            Destroy(coalPiece.gameObject, destroyDelaySeconds);
    }

    // ----------------------------
    // Optional: Let Other Scripts Swap the Command
    // ----------------------------

    public void SetCoalCaughtCommand(Action<CoalPiece> command)
    {
        // This lets you replace the default behavior without editing this script.
        // Example: a tutorial might "catch coal" but NOT destroy it yet.
        onCoalCaughtCommand = command;
    }
}
