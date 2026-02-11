using UnityEngine;
using System.Collections;

public class AmmoUITester : MonoBehaviour
{
    [SerializeField] private CylinderUI cylinder;
    [SerializeField] private ReserveStripUI reserveStrip;
    
    [Header("Test Settings")]
    [SerializeField] private int testChamberCount = 6;
    [SerializeField] private int testReserveAmmo = 18;

    private int currentCylinderAmmo;
    private bool isAnimating;

    private void Start()
    {
        currentCylinderAmmo = testChamberCount; // Start full
        cylinder.Initialize(testChamberCount, currentCylinderAmmo);
        reserveStrip.UpdateDisplay(testReserveAmmo);
    }

    private void Update()
    {
        if (isAnimating) return;
        
        // Press F to simulate firing
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (currentCylinderAmmo > 0)
            {
                StartCoroutine(TestFire());
            }
            else
            {
                Debug.Log("Cylinder empty — can't fire");
            }
        }
        
        // Press R to simulate reload
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (currentCylinderAmmo >= testChamberCount)
            {
                Debug.Log("Cylinder already full — can't reload");
            }
            else if (testReserveAmmo <= 0)
            {
                Debug.Log("No reserve ammo — can't reload");
            }
            else
            {
                StartCoroutine(TestReload());
            }
        }
    }

    private IEnumerator TestFire()
    {
        isAnimating = true;
        Debug.Log($"Firing... Cylinder: {currentCylinderAmmo} → {currentCylinderAmmo - 1}");
        
        yield return cylinder.RemoveCurrentBullet();
        yield return cylinder.RotateToNext();
        
        currentCylinderAmmo--;
        isAnimating = false;
    }

    private IEnumerator TestReload()
    {
        isAnimating = true;
        
        int bulletsToLoad = Mathf.Min(testChamberCount - currentCylinderAmmo, testReserveAmmo);
        Debug.Log($"Reloading {bulletsToLoad} bullets...");
        
        for (int i = 0; i < bulletsToLoad; i++)
        {
            Vector3 startPos = reserveStrip.GetNextBulletPosition();
            yield return cylinder.LoadBulletFromPosition(startPos);
            
            currentCylinderAmmo++;
            testReserveAmmo--;
            reserveStrip.UpdateDisplay(testReserveAmmo);
            
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.Log($"Reload complete. Cylinder: {currentCylinderAmmo}, Reserve: {testReserveAmmo}");
        isAnimating = false;
    }
}