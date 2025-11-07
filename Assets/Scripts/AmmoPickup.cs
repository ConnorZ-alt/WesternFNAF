using UnityEngine;

public class AmmoPickup : MonoBehaviour
{
    [SerializeField] private int amount = 6;
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Pickup trigger hit by: " + other.name); // TEMP: helps you see if it fires

        if (!other.CompareTag(playerTag)) return;

        var gun = other.GetComponentInChildren<ItemController>();
        if (!gun)
        {
            Debug.LogWarning("AmmoPickup: No ItemController found under Player.");
            return;
        }

        gun.AddAmmo(amount);
        Destroy(gameObject);
    }
}