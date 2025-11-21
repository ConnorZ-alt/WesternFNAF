using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AmmoPickup : MonoBehaviour
{
    [SerializeField] private int ammoAmount = 6;
    [SerializeField] private string playerTag = "Player";

    void Reset()
    {
        Collider pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;

        Rigidbody pickupRigidbody = GetComponent<Rigidbody>();
        if (!pickupRigidbody) pickupRigidbody = gameObject.AddComponent<Rigidbody>();
        pickupRigidbody.isKinematic = true;
        pickupRigidbody.useGravity  = false;
    }

    void Awake()
    {
        // Safety: if this pickup somehow lives under the Player, warn & disable
        if (transform.root.CompareTag(playerTag))
        {
            Debug.LogError("[AmmoPickup] This pickup is inside the Player hierarchy. Move it out into the scene/prefab.");
            enabled = false;
        }
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        // Only react to the player
        if (!otherCollider.CompareTag(playerTag)) return;

        Debug.Log("Pickup trigger hit by: " + otherCollider.name);

        // Find the player's gun
        ItemController itemController = otherCollider.GetComponentInChildren<ItemController>(true);
        if (!itemController)
        {
            Debug.LogWarning("[AmmoPickup] Player has no ItemController under them.");
            return;
        }

        itemController.AddAmmo(ammoAmount);

        // Hide & destroy the pickup (THIS object)
        Collider pickupCollider = GetComponent<Collider>(); 
        if (pickupCollider) pickupCollider.enabled = false;

        foreach (Renderer rendererComponent in GetComponentsInChildren<Renderer>())
            rendererComponent.enabled = false;

        Destroy(gameObject, 0.05f);
    }
}