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
        if (gun == null)
            gun = FindObjectOfType<ItemController>();
        
        if (gun == null)
        {
            Debug.LogError("[AmmoUIController] No ItemController found!");
            return;
        }
        
        gun.OnAmmoChanged += HandleAmmoChanged;
        
        lastKnownCylinder = gun.GetRoundsInCylinder();
        lastKnownReserve = gun.GetReserveAmmo();
        
        cylinder.Initialize(gun.GetTotalAmmoCapacity(), lastKnownCylinder);
        reserveStrip.UpdateDisplay(lastKnownReserve);
    }

    private void HandleAmmoChanged(int cylinderRounds, int reserveRounds)
    {
        int cylinderDelta = cylinderRounds - lastKnownCylinder;
        int reserveDelta = reserveRounds - lastKnownReserve;
        
        // Capture old reserve before updating
        int oldReserve = lastKnownReserve;
        
        // Update tracked state first
        lastKnownCylinder = cylinderRounds;
        lastKnownReserve = reserveRounds;
        
        if (currentAnimation != null && isAnimating)
        {
            StopCoroutine(currentAnimation);
            isAnimating = false;
        }
        
        if (cylinderDelta < 0)
        {
            currentAnimation = StartCoroutine(FireSequence(Mathf.Abs(cylinderDelta)));
        }
        else if (cylinderDelta > 0 && reserveDelta < 0)
        {
            // Pass the old reserve value to the coroutine
            currentAnimation = StartCoroutine(ReloadSequence(cylinderDelta, oldReserve));
        }
        else if (reserveDelta > 0)
        {
            reserveStrip.UpdateDisplay(reserveRounds);
        }
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

    private IEnumerator ReloadSequence(int bulletsToLoad, int startingReserve)
    {
        isAnimating = true;
        
        for (int i = 0; i < bulletsToLoad; i++)
        {
            Vector3 startPos = reserveStrip.GetNextBulletPosition();
            yield return cylinder.LoadBulletFromPosition(startPos);
            
            // Use the captured starting value
            reserveStrip.UpdateDisplay(startingReserve - (i + 1));
            
            yield return new WaitForSeconds(0.1f);
        }
        
        isAnimating = false;
    }
}