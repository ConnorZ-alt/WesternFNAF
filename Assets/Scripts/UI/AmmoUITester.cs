using UnityEngine;

public class AmmoUITester : MonoBehaviour
{
    [SerializeField] private CylinderUI cylinder;
    [SerializeField] private ReserveStripUI reserveStrip;
    
    [Header("Test Settings")]
    [SerializeField] private int testChamberCount = 6;
    [SerializeField] private int testReserveAmmo = 18;

    private void Start()
    {
        cylinder.Initialize(testChamberCount, testChamberCount); // Full cylinder
        reserveStrip.UpdateDisplay(testReserveAmmo);
    }

    private void Update()
    {
        // Press F to simulate firing
        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(TestFire());
        }
        
        // Press R to simulate reload
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(TestReload());
        }
    }

    private System.Collections.IEnumerator TestFire()
    {
        Debug.Log("Testing fire...");
        yield return cylinder.RotateToNext();
        yield return cylinder.RemoveCurrentBullet();
    }

    private System.Collections.IEnumerator TestReload()
    {
        Debug.Log("Testing reload...");
        Vector3 startPos = reserveStrip.GetNextBulletPosition();
        yield return cylinder.LoadBulletFromPosition(startPos);
        testReserveAmmo--;
        reserveStrip.UpdateDisplay(testReserveAmmo);
    }
}