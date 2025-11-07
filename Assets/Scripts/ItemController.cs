using UnityEngine;
using System;
using UnityEngine.Serialization;

public class ItemController : MonoBehaviour
{
    [Header("Links")]
    [Tooltip("Player camera used for aiming/shooting raycasts")]
    [SerializeField] private Camera playerCamera;
    
    [Header("Revolver")]
    [Tooltip("Capacity of the cylinder")]
    [SerializeField] private int totalAmmoCapacity = 6;
    public int GetTotalAmmoCapacity() => totalAmmoCapacity;
    [Tooltip("Max shoot distance for raycast (debug only)")]
    [SerializeField] private float shotRange = 100f;
    [Tooltip("LayerMask for what the debug raycast can hit")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Input")]
    [SerializeField] private bool holdToAim = true;
    [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;   // RMB
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse0; // LMB
    [SerializeField] private KeyCode reloadKey = KeyCode.R;

    [Header("Aiming Visual (optional)")]
    [SerializeField] private bool useAimFov = true;
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float aimFov = 45f;
    [SerializeField] private float fovLerpSpeed = 10f;

    [Header("Ammo State (read-only at runtime)")]
    [SerializeField] private int roundsInCylinder = 0; // starts empty
    [SerializeField] private int reserveAmmo = 0; // filled by pickups

    [Header("Bullet Visual")] 
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletRecoilKick = 0.03f;
    
    [SerializeField] private bool debugForceAim = false;
    private bool lastAiming = false;

    [SerializeField] private bool externalShootBlock = false;

    public void SetExternalShootBlock(bool value)
    {
        externalShootBlock = value;
    }
    
    // Events (for HUD later)
    public event Action<int, int> OnAmmoChanged; // (roundsInCylinder, reserveAmmo)

    // Internals
    [SerializeField] private bool isAiming = false;
    public bool IsAiming => isAiming;

    void Awake()
    {
        if (!playerCamera)
        {
            playerCamera = GetComponentInParent<Camera>();
            if (!playerCamera) playerCamera = Camera.main;
        }

        // Ensure this never behaves like a loose physics item
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
    }

    void Start()
    {
        if (playerCamera && useAimFov) playerCamera.fieldOfView = normalFov;
        RaiseAmmoChanged();
    }

    void Update()
    {
        HandleAimInput();
        HandleReloadInput();
        HandleShootInput();
        UpdateAimFov();
    }

    // ---------- Input ----------
    private void HandleAimInput()
    {
        if (!playerCamera) return;

        // Show what input we're getting this frame
        bool aimKeyDown = Input.GetKeyDown(aimKey);
        bool aimKeyHeld = Input.GetKey(aimKey);

        // This is just logging so we can see in the Console during Play
        if (aimKeyDown)
        {
            Debug.Log("[GUN DEBUG] aimKeyDown fired for " + aimKey + " this frame");
        }

        if (holdToAim)
        {
            // hold-to-aim mode: stay aiming only while holding button
            isAiming = aimKeyHeld;
        }
        else
        {
            // toggle-to-aim mode: flip aiming when we press the button
            if (aimKeyDown)
            {
                isAiming = !isAiming;
                Debug.Log("[GUN DEBUG] Toggled isAiming to: " + isAiming);
            }
        }

        // safety override: if debugForceAim is checked in the Inspector,
        // we force aiming on no matter what
        if (debugForceAim)
        {
            isAiming = true;
        }

        // only print when the aim state actually changes, to reduce spam
        if (isAiming != lastAiming)
        {
            Debug.Log("[GUN] Aim state now = " + (isAiming ? "AIMING" : "NOT AIMING"));
            lastAiming = isAiming;
        }
    }

    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(reloadKey))
            Reload();
    }

    private void HandleShootInput()
    {
        if (Input.GetKeyDown(shootKey))
            Shoot();
    }

    private void UpdateAimFov()
    {
        if (!playerCamera || !useAimFov) return;
        bool aimingForFov = debugForceAim ? true : isAiming;
        float target = aimingForFov ? aimFov : normalFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, Time.deltaTime * fovLerpSpeed);
    }

    // ---------- Public API ----------
    public void AddAmmo(int amount)
    {
        if (amount <= 0) return;
        reserveAmmo += amount;
        Debug.Log("[GUN] Picked up ammo. Reserve now = " + reserveAmmo);
        RaiseAmmoChanged();
    }

    public void Reload()
    {
        int space = totalAmmoCapacity - roundsInCylinder;
        if (space <= 0)
        {
            Debug.Log("[GUN] Reload: cylinder already full.");
            return;
        }
        if (reserveAmmo <= 0)
        {
            Debug.Log("[GUN] Reload: no reserve ammo.");
            return;
        }

        int toLoad = Mathf.Min(space, reserveAmmo);
        roundsInCylinder += toLoad;
        reserveAmmo -= toLoad;

        Debug.Log("[GUN] Reloaded " + toLoad + " rounds. Cylinder = " 
                  + roundsInCylinder + "/" + totalAmmoCapacity 
                  + " | Reserve = " + reserveAmmo);

        RaiseAmmoChanged();
    }


    public bool CanShoot() => roundsInCylinder > 0 && isAiming && !externalShootBlock;

    public void Shoot()
    {
        if (!CanShoot())
        {
            Debug.Log("[GUN] Tried to shoot but can't (no aim or no rounds).");
            return;
        }

        roundsInCylinder--;
        Debug.Log("[GUN] BANG. Cylinder now = " + roundsInCylinder + "/" + totalAmmoCapacity);

        RaiseAmmoChanged();

        // Visual for Bullet Projectile
        if (muzzlePoint && bulletPrefab)
        {
            GameObject bullet = Instantiate(bulletPrefab, muzzlePoint.position, muzzlePoint.rotation);
        }
        else
        {
            Debug.LogWarning("[GUN] No muzzlePoint or bulletPrefab assigned, can't spawn bullets.");
        }
        
        // Debug raycast
        if (playerCamera)
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, shotRange, hitMask, QueryTriggerInteraction.Ignore))
            {
                Debug.Log("[GUN] Hit " + hit.collider.name);
                Debug.DrawLine(ray.origin, hit.point, Color.red, 0.25f);
            }
            else
            {
                Debug.Log("[GUN] Missed (raycast hit nothing).");
                Debug.DrawRay(ray.origin, ray.direction * 10f, Color.gray, 0.25f);
            }
        }
        
        // Tiny recoil kick for the camera to shake (adding slight realism)
        if (playerCamera)
        {
            playerCamera.transform.localPosition += -playerCamera.transform.forward * bulletRecoilKick;
        }

        if (roundsInCylinder == 0)
        {
            isAiming = false;
            Debug.Log("[GUN] Cylinder empty. Aim auto-canceled.");
        }
    }
    
    public int GetRoundsInCylinder() => roundsInCylinder;
    public int GetReserveAmmo() => reserveAmmo;

    // ---------- Helpers ----------
    private void RaiseAmmoChanged() => OnAmmoChanged?.Invoke(roundsInCylinder, reserveAmmo);
}
