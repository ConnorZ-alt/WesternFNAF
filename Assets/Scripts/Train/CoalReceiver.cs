using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CoalReceiver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController trainController;
    // Drag the Train (the object with TrainController) here.

    [Header("Coal Value")]
    [SerializeField] private float coalValueOverride = -1f;
    // If this is > 0, we use it as the coal value.
    // If it's -1 (or 0 or less), we ask the train for its normal coal amount.
    
    // Other scripts can listen to this event.
    // Example: achievements ("delivered 10 coal"), sounds, UI popups, etc.
    public event Action<CoalPiece, float> CoalReceived;
    
    // This is the main "thing we do" when coal enters the receiver.
    // Default is: add coal to train, then consume the coal piece.
    private Action<CoalPiece> onCoalEnteredCommand;

    private Collider receiverCollider;

    // ----------------------------
    // Public Helpers (used by other scripts)
    // ----------------------------

    public TrainController GetTrain() => trainController;

    public float GetCoalValue()
    {
        // This method tells other scripts what coal is worth right now.
        // We prefer the override if it's set, otherwise we ask the train.
        if (coalValueOverride > 0f)
            return coalValueOverride;

        if (trainController != null)
            return trainController.GetCoalAmount();

        // Backup value if there is no train assigned (prevents returning weird values).
        return 0.2f;
    }

    private void Awake()
    {
        // Grab the collider once so we don’t call GetComponent repeatedly.
        receiverCollider = GetComponent<Collider>();

        // Make sure this collider acts like a trigger (so coal passes in instead of bouncing off).
        receiverCollider.isTrigger = true;

        // Set up the default command.
        onCoalEnteredCommand = DefaultAddCoalAndConsume;
    }

    private void Reset()
    {
        // Reset runs in the Editor when you add the component or press Reset.
        // It sets safe defaults so you don’t forget to configure something.

        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnValidate()
    {
        // Runs in the Editor when inspector values change.
        // Keeps the override from being confusing (negative is allowed to mean "disabled").
        // No heavy logic here—just small safety checks.

        // If someone accidentally sets a tiny positive like 0.0001, it will still count as override.
        // That’s fine, just something to be aware of.
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        // This runs when something enters the trigger collider.

        // 1) Must have a Rigidbody attached, so random trigger overlaps don’t count.
        if (otherCollider == null || otherCollider.attachedRigidbody == null)
            return;

        // 2) Check if the thing entering is a coal piece (or is inside a coal object).
        if (!TryGetCoalPiece(otherCollider, out CoalPiece coalPiece))
            return;

        // 3) Figure out how much coal this is worth.
        float coalAmountToAdd = GetCoalValue();
        
        // 4) Observer: announce that we received coal.
        // This is how achievements/audio/UI can react without being hard-coded in here.
        CoalReceived?.Invoke(coalPiece, coalAmountToAdd);

        // 5) Command: do the main behavior (credit + consume by default).
        onCoalEnteredCommand?.Invoke(coalPiece);
    }

    // ----------------------------
    // Helper Methods
    // ----------------------------

    private static bool TryGetCoalPiece(Collider otherCollider, out CoalPiece coalPiece)
    {
        // This helper keeps OnTriggerEnter clean and easy to read.
        // We search upward because the collider might be on a child object.
        coalPiece = otherCollider.GetComponentInParent<CoalPiece>();
        return coalPiece != null;
    }

    private void DefaultAddCoalAndConsume(CoalPiece coalPiece)
    {
        // This is the normal behavior when coal enters the receiver:
        // - Add coal to the train (if train exists)
        // - Consume the coal so it despawns and can’t count twice

        if (coalPiece == null)
            return;

        if (trainController == null)
        {
            Debug.LogWarning(
                $"[{nameof(CoalReceiver)}] Train reference not set on {name}. " +
                "Coal will be consumed but no coal will be added.",
                this
            );

            // Still clean up the coal so it doesn’t sit around forever.
            coalPiece.Consume();
            return;
        }

        float coalAmountToAdd = GetCoalValue();
        trainController.AddCoal(coalAmountToAdd);

        // Despawn the coal here (marks it consumed so its collision code won’t fire later).
        coalPiece.Consume();
    }

    // ----------------------------
    // Optional: Let Other Scripts Swap the Command
    // ----------------------------

    public void SetOnCoalEnteredCommand(Action<CoalPiece> command)
    {
        // This lets you replace the default behavior without editing this script.
        // Example: during a tutorial, you might accept coal but not add it yet.
        onCoalEnteredCommand = command;
    }
}
