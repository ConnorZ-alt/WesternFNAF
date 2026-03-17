using System;
using UnityEngine;

/// Singleton that tracks how many ammo pickups remain in the world (on the train)
/// AmmoPickup objects register themselves here on Awake, and de-register when collected
/// Any UI can listen to OnWorldAmmoChanged to update a display

public class WorldAmmoTracker : MonoBehaviour
{
    public static WorldAmmoTracker Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Events
    
    /// Fires whenever the world ammo count changes
    /// int = new world ammo count remaining
    public event Action<int> OnWorldAmmoChanged;

    // State

    private int worldAmmoCount = 0;

    public int WorldAmmoCount => worldAmmoCount;

    // Public API
    
    /// Called by AmmoPickup.Awake() to register itself as available world ammo
    /// Pass the ammoAmount value from the pickup
    public void RegisterPickup(int amount)
    {
        worldAmmoCount += amount;
        Debug.Log("[WorldAmmo] Pickup registered. World ammo = " + worldAmmoCount);
        OnWorldAmmoChanged?.Invoke(worldAmmoCount);
    }
    
    /// Called by AmmoPickup when it is collected by the player
    /// Pass the ammoAmount value from the pickup
    
    public void DeregisterPickup(int amount)
    {
        worldAmmoCount = Mathf.Max(0, worldAmmoCount - amount);
        Debug.Log("[WorldAmmo] Pickup collected. World ammo = " + worldAmmoCount);
        OnWorldAmmoChanged?.Invoke(worldAmmoCount);
    }
}