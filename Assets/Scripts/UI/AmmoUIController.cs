using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AmmoUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CylinderUI cylinder;
    [SerializeField] private ReserveStripUI reserveStrip;
    
    [Header("Ammo Settings")]
    [SerializeField] private int magazineSize = 6;
    [SerializeField] private int startingReserve = 6;
    
    private int currentAmmo;
    private int reserveAmmo;
    private bool isAnimating;

    private void Start()
    {
        currentAmmo = magazineSize;
        reserveAmmo = startingReserve;
        
        cylinder.Initialize(magazineSize);
        reserveStrip.UpdateDisplay(reserveAmmo);
    }

    public bool TryFire()
    {
        if (isAnimating || currentAmmo <= 0) return false;
        
        StartCoroutine(FireSequence());
        return true;
    }

    public bool TryReload()
    {
        if (isAnimating || currentAmmo >= magazineSize || reserveAmmo <= 0) return false;
        
        StartCoroutine(ReloadSequence());
        return true;
    }

    private IEnumerator FireSequence()
    {
        isAnimating = true;
        
        // Rotate cylinder first, then remove bullet
        yield return cylinder.RotateToNext();
        yield return cylinder.RemoveCurrentBullet();
        
        currentAmmo--;
        isAnimating = false;
    }

    private IEnumerator ReloadSequence()
    {
        isAnimating = true;
        
        int bulletsToLoad = Mathf.Min(magazineSize - currentAmmo, reserveAmmo);
        
        for (int i = 0; i < bulletsToLoad; i++)
        {
            // Get world position of reserve bullet
            Vector3 startPos = reserveStrip.GetNextBulletPosition();
            
            // Animate bullet traveling to cylinder
            yield return cylinder.LoadBulletFromPosition(startPos);
            
            reserveAmmo--;
            currentAmmo++;
            reserveStrip.UpdateDisplay(reserveAmmo);
            
            yield return new WaitForSeconds(0.1f); // Bullet delay
        }
        
        isAnimating = false;
    }
}


/*
In weapon/player script:
[SerializeField] private AmmoUIController ammoUI;

void Update()
{
    if (Input.GetButtonDown("Fire1"))
    {
        if (ammoUI.TryFire())
        {
            // Actually fire the weapon
            FireWeapon();
        }
    }
    
    if (Input.GetKeyDown(KeyCode.R))
    {
        ammoUI.TryReload();
    }
}
*/