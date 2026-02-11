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
        // Initialize with current ammo state
        if (gun != null)
        {
            lastKnownCylinder = gun.GetRoundsInCylinder();
            lastKnownReserve = gun.GetReserveAmmo();
            
            cylinder.Initialize(gun.GetTotalAmmoCapacity(), lastKnownCylinder);
            reserveStrip.UpdateDisplay(lastKnownReserve);
        }
    }

    private void HandleAmmoChanged(int cylinderRounds, int reserveRounds)
    {
        if (isAnimating) return;
        
        // Detect what changed
        int cylinderDelta = cylinderRounds - lastKnownCylinder;
        int reserveDelta = reserveRounds - lastKnownReserve;
        
        if (cylinderDelta < 0)
        {
            // Bullet was fired
            StartCoroutine(FireSequence(Mathf.Abs(cylinderDelta)));
        }
        else if (cylinderDelta > 0 && reserveDelta < 0)
        {
            // Reload happened
            StartCoroutine(ReloadSequence(cylinderDelta));
        }
        
        lastKnownCylinder = cylinderRounds;
        lastKnownReserve = reserveRounds;
    }

    private IEnumerator FireSequence(int bulletsFired)
    {
        isAnimating = true;
        
        for (int i = 0; i < bulletsFired; i++)
        {
            yield return cylinder.RotateToNext();
            yield return cylinder.RemoveCurrentBullet();
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
            reserveStrip.UpdateDisplay(lastKnownReserve - (i + 1));
            yield return new WaitForSeconds(0.1f);
        }
        
        isAnimating = false;
    }
}