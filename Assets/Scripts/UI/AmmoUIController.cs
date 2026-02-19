using UnityEngine;
using System.Collections;

public class AmmoUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ItemController gun;
    [SerializeField] private CylinderUI cylinder;
    [SerializeField] private ReserveStripUI reserveStrip;
    
    private int lastKnownCylinder;
    private int lastKnownReserve;
    private bool isAnimating;
    private Coroutine currentAnimation;

    private void OnEnable()
    {
        if (gun != null)
            gun.OnAmmoChanged += HandleAmmoChanged;
    }

    private void OnDisable()
    {
        if (gun != null)
            gun.OnAmmoChanged -= HandleAmmoChanged;
    }

    private void Start()
    {
        // Find gun automatically if not assigned
        if (gun == null)
            gun = FindObjectOfType<ItemController>();
        
        if (gun == null)
        {
            Debug.LogError("[AmmoUIController] No ItemController found!");
            return;
        }
        
        // Subscribe if we found it in Start
        gun.OnAmmoChanged += HandleAmmoChanged;
        
        // Initialize with current ammo state
        lastKnownCylinder = gun.GetRoundsInCylinder();
        lastKnownReserve = gun.GetReserveAmmo();
        
        cylinder.Initialize(gun.GetTotalAmmoCapacity(), lastKnownCylinder);
        reserveStrip.UpdateDisplay(lastKnownReserve);
    }

    private void HandleAmmoChanged(int cylinderRounds, int reserveRounds)
    {
        // Calculate what changed
        int cylinderDelta = cylinderRounds - lastKnownCylinder;
        int reserveDelta = reserveRounds - lastKnownReserve;
        
        // Stop any in-progress animation if ammo changes rapidly
        if (currentAnimation != null && isAnimating)
        {
            StopCoroutine(currentAnimation);
            isAnimating = false;
        }
        
        if (cylinderDelta < 0)
        {
            // Bullets were fired
            currentAnimation = StartCoroutine(FireSequence(Mathf.Abs(cylinderDelta)));
        }
        else if (cylinderDelta > 0 && reserveDelta < 0)
        {
            // Reload happened (cylinder gained, reserve lost)
            currentAnimation = StartCoroutine(ReloadSequence(cylinderDelta));
        }
        else if (reserveDelta > 0)
        {
            // Picked up ammo (just update reserve display)
            reserveStrip.UpdateDisplay(reserveRounds);
        }
        
        // Update tracked state
        lastKnownCylinder = cylinderRounds;
        lastKnownReserve = reserveRounds;
    }

    private IEnumerator FireSequence(int bulletsFired)
    {
        isAnimating = true;
        
        for (int i = 0; i < bulletsFired; i++)
        {
            yield return cylinder.RemoveCurrentBullet();
            yield return cylinder.RotateToNext();
        }
        
        isAnimating = false;
    }

    private IEnumerator ReloadSequence(int bulletsToLoad)
    {
        isAnimating = true;
        
        for (int i = 0; i < bulletsToLoad; i++)
        {
            Vector3 startPos = reserveStrip.GetNextBulletPosition();
            yield return cylinder.LoadBulletFromPosition(startPos);
            
            // Update reserve display progressively
            reserveStrip.UpdateDisplay(lastKnownReserve - (i + 1));
            
            yield return new WaitForSeconds(0.1f);
        }
        
        isAnimating = false;
    }
}