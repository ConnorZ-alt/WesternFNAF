using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class AmmoPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private int ammoAmount = 6;
    [SerializeField] private string playerTag = "Player";

    // Cache components so we don't keep calling GetComponent() a bunch.
    private Collider pickupCollider;
    private Rigidbody pickupRigidbody;

    private bool collected; // simple "state" so we don't collect twice by accident

    private void Reset()
    {
        // Reset runs in the Unity Editor when you add the component or click Reset.
        // This sets up the pickup so it behaves like a trigger pickup.
        EnsureTriggerSetup();
    }

    private void Awake()
    {
        // Awake runs when the game starts and this object is created.
        // We grab references and also double-check the setup at runtime.

        pickupCollider = GetComponent<Collider>();
        pickupRigidbody = GetComponent<Rigidbody>();

        // Safety: if this pickup is somehow placed under the Player, disable it.
        if (transform.root.CompareTag(playerTag))
        {
            Debug.LogError("[AmmoPickup] This pickup is inside the Player hierarchy. Move it out into the scene/prefab.");
            enabled = false;
            return;
        }

        // Make sure collider/rigidbody settings are correct even if Inspector was changed.
        EnsureTriggerSetup();

        // --- NEW: Register this pickup with the world ammo tracker ---
        // We do this in Awake so the counter reflects all pickups in the level on game start.
        if (WorldAmmoTracker.Instance != null)
            WorldAmmoTracker.Instance.RegisterPickup(ammoAmount);
        else
            Debug.LogWarning("[AmmoPickup] No WorldAmmoTracker found. Add a WorldAmmoTracker to the scene.");
    }

    private void EnsureTriggerSetup()
    {
        // This method makes sure the pickup collider is a trigger,
        // and ensures the pickup has a kinematic rigidbody (Unity likes this for triggers).

        pickupCollider = GetComponent<Collider>();
        if (pickupCollider) pickupCollider.isTrigger = true;

        pickupRigidbody = GetComponent<Rigidbody>();
        if (!pickupRigidbody) pickupRigidbody = gameObject.AddComponent<Rigidbody>();

        pickupRigidbody.isKinematic = true;
        pickupRigidbody.useGravity = false;
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        // OnTriggerEnter runs when something enters this trigger.
        // We only want to react to the Player.

        if (collected) return; // already picked up
        if (!otherCollider.CompareTag(playerTag)) return;

        // Try to find an ItemController on the player.
        ItemController gun = otherCollider.GetComponentInChildren<ItemController>(true);

        if (gun == null)
        {
            Debug.LogWarning("[AmmoPickup] Player entered pickup but has no ItemController in children.");
            return;
        }

        // Tell the gun to add ammo.
        gun.AddAmmo(ammoAmount);

        // --- NEW: Deregister from world tracker (bullet is no longer "in the world") ---
        if (WorldAmmoTracker.Instance != null)
            WorldAmmoTracker.Instance.DeregisterPickup(ammoAmount);

        // Mark as collected so this can't run twice.
        collected = true;

        // Hide the pickup right away, then destroy it.
        HidePickupVisualsAndCollider();
        Destroy(gameObject, 0.05f);
    }

    private void HidePickupVisualsAndCollider()
    {
        // This method makes the pickup disappear instantly.

        if (!pickupCollider) pickupCollider = GetComponent<Collider>();
        if (pickupCollider) pickupCollider.enabled = false;

        // Turn off every renderer so the object is invisible.
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
    }
}