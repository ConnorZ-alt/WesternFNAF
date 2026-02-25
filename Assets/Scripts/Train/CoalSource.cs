using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CoalSource : MonoBehaviour
{
    [Header("Who can use this source")]
    [SerializeField] private string playerTag = "Player";
    // Only objects with this tag are allowed to trigger the "in range" logic.

    [Header("Optional: a small UI prompt to show when the player is in range")]
    [SerializeField] private GameObject promptUserInterfaceObject;
    // This can be a world-space UI popup like: "Press E to grab coal".

    [Header("Trigger Setup")]
    [SerializeField] private bool forceKinematicRigidbody = true;
    // Triggers can behave weirdly depending on what colliders you use.
    // A kinematic rigidbody on the trigger object helps make trigger events reliable.
    
    // Other scripts can listen to these events.
    // Example: tutorial steps, sound effects, achievements, UI manager, etc.
    public event Action<GameObject> PlayerEnteredRange;
    public event Action<GameObject> PlayerExitedRange;
    
    // These are the "things we do" when the player enters/leaves.
    // Default: show prompt when entering, hide prompt when leaving.
    private Action<GameObject> onEnterCommand;
    private Action<GameObject> onExitCommand;
    
    // This prevents double toggles and makes behavior more predictable.
    private bool playerInRange;

    // Cached components so we don’t call GetComponent every time.
    private Collider sourceCollider;
    private Rigidbody sourceRigidbody;

    private void Awake()
    {
        // Awake runs when the object loads.
        // We set up components and default behavior here.

        EnsureTriggerSetup();

        // Default commands: show/hide the prompt
        onEnterCommand = DefaultShowPrompt;
        onExitCommand = DefaultHidePrompt;

        // Start with prompt hidden so it doesn’t appear at the wrong time.
        if (promptUserInterfaceObject)
            promptUserInterfaceObject.SetActive(false);

        playerInRange = false;
    }

    private void Reset()
    {
        // Reset runs in the Editor when you add the component or press Reset.
        // This helps you not forget the trigger + rigidbody setup.
        EnsureTriggerSetup();

        if (promptUserInterfaceObject)
            promptUserInterfaceObject.SetActive(false);
    }

    private void OnValidate()
    {
        // OnValidate runs in the Editor when you change Inspector values.
        // We keep it simple and safe. No heavy logic here.

        if (string.IsNullOrWhiteSpace(playerTag))
            playerTag = "Player";
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        // This runs when something enters the trigger area.

        if (!IsValidPlayer(otherCollider))
            return;

        // If we already think the player is inside, don’t run everything again.
        if (playerInRange)
            return;

        playerInRange = true;

        GameObject playerObject = otherCollider.gameObject;

        // Observer: announce that the player entered range.
        PlayerEnteredRange?.Invoke(playerObject);

        // Command: do the "enter" behavior (default is showing UI).
        onEnterCommand?.Invoke(playerObject);

        // Note: PlayerCoalThrower already checks GetComponent<CoalSource>() in its own trigger code,
        // so this object mostly just needs to exist and be a reliable trigger.
    }

    private void OnTriggerExit(Collider otherCollider)
    {
        // This runs when something leaves the trigger area.

        if (!IsValidPlayer(otherCollider))
            return;

        // If we thought the player wasn’t inside, don’t run exit logic.
        if (!playerInRange)
            return;

        playerInRange = false;

        GameObject playerObject = otherCollider.gameObject;

        // Observer: announce that the player exited range.
        PlayerExitedRange?.Invoke(playerObject);

        // Command: do the "exit" behavior (default is hiding UI).
        onExitCommand?.Invoke(playerObject);
    }

    // ----------------------------
    // Setup Helpers
    // ----------------------------

    private void EnsureTriggerSetup()
    {
        // This method makes sure the collider is a trigger and
        // (optionally) makes sure we have a kinematic rigidbody.

        if (sourceCollider == null)
            sourceCollider = GetComponent<Collider>();

        sourceCollider.isTrigger = true;

        if (!forceKinematicRigidbody)
            return;

        if (sourceRigidbody == null)
            sourceRigidbody = GetComponent<Rigidbody>();

        // If there isn't a rigidbody, add one so triggers are more reliable.
        if (sourceRigidbody == null)
            sourceRigidbody = gameObject.AddComponent<Rigidbody>();

        sourceRigidbody.isKinematic = true;
        sourceRigidbody.useGravity = false;
    }

    private bool IsValidPlayer(Collider otherCollider)
    {
        // This helper checks if the object entering/exiting is the player we care about.

        if (otherCollider == null)
            return false;

        return otherCollider.CompareTag(playerTag);
    }

    private void DefaultShowPrompt(GameObject player)
    {
        // This is the default thing we do when the player enters:
        // show the UI prompt (if one exists).
        if (promptUserInterfaceObject)
            promptUserInterfaceObject.SetActive(true);
    }

    private void DefaultHidePrompt(GameObject player)
    {
        // This is the default thing we do when the player leaves:
        // hide the UI prompt (if one exists).
        if (promptUserInterfaceObject)
            promptUserInterfaceObject.SetActive(false);
    }

    // Let other scripts swap what happens on enter/exit without editing this file.
    public void SetEnterCommand(Action<GameObject> command) => onEnterCommand = command;
    public void SetExitCommand(Action<GameObject> command) => onExitCommand = command;

    // ----------------------------
    // Gizmos (Editor Visualization)
    // ----------------------------

    private void OnDrawGizmos()
    {
        // This draws a faint shape in the editor so you can see the trigger area.
        // It doesn't affect gameplay at all.

        Gizmos.color = new Color(0f, 0f, 0f, 0.15f);

        // If this is a box collider, draw a box. Otherwise draw a small sphere.
        if (TryGetComponent(out BoxCollider boxCollider))
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);

            Gizmos.color = new Color(0f, 0f, 0f, 0.35f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
        else
        {
            Gizmos.DrawSphere(transform.position, 0.3f);
        }
    }
}