using System.Collections;
using UnityEngine;

public class PlayerCoalThrower : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint;      // empty under Main Camera (e.g., X=0.2, Y=-0.15, Z=0.6)
    [SerializeField] private GameObject coalPrefab;    // your Coal prefab
    [SerializeField] private ItemController gun;       // drag your Revolver (ItemController) here

    [Header("Input")]
    [SerializeField] private KeyCode pickKey  = KeyCode.F;         // <- F to pick up
    [SerializeField] private KeyCode throwKey = KeyCode.Mouse0;    // LMB to throw
    [SerializeField] private KeyCode aimKey   = KeyCode.Mouse1;    // RMB to aim (same as gun)

    [Header("Throw Tuning")]
    [SerializeField] private float forwardForce = 12f;   // how far the coal goes
    [SerializeField] private float upwardBoost  = 4f;    // arc height
    [SerializeField] private float maxHoldDist  = 2f;    // not used for ray-pick; we “spawn” coal

    [Header("UX")]
    [SerializeField] private bool requireAimToThrow = true; // only throw while aiming

    private bool insideCoalSource = false;
    private GameObject heldCoal;

    void Awake()
    {
        if (!playerCamera)
        {
            // Try to auto-find the camera
            if (gun) playerCamera = gun.GetComponentInParent<Camera>();
            if (!playerCamera) playerCamera = Camera.main;
        }
    }

    void Update()
    {
        // Pick up coal (infinite) when standing in CoalSource
        if (insideCoalSource && heldCoal == null && Input.GetKeyDown(pickKey))
        {
            PickupCoal();
        }

        // Throw if holding coal
        if (heldCoal != null && Input.GetKeyDown(throwKey))
        {
            // If we require aim, check both the gun's aim (preferred) or RMB held
            bool aiming = gun ? gun.IsAiming : Input.GetKey(aimKey);
            if (!requireAimToThrow || aiming)
            {
                ThrowCoal();

                // Block gun shooting for this click so LMB doesn't also fire the gun
                if (gun)
                {
                    gun.SetExternalShootBlock(true);
                    StartCoroutine(ClearShootBlockNextFrame());
                }
            }
        }

        // Keep the held coal positioned at the hold point (no physics while held)
        if (heldCoal)
        {
            heldCoal.transform.position = holdPoint.position;
            heldCoal.transform.rotation = holdPoint.rotation;
        }
    }

    private void PickupCoal()
    {
        heldCoal = Instantiate(coalPrefab, holdPoint.position, holdPoint.rotation);
        var rb = heldCoal.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true; // freeze while held
    }

    private void ThrowCoal()
    {
        var rb = heldCoal.GetComponent<Rigidbody>();
        rb.isKinematic = false;

        // Arc: forward + upward
        Vector3 v = playerCamera.transform.forward * forwardForce + Vector3.up * upwardBoost;
        rb.linearVelocity = v;

        heldCoal = null;
    }

    private IEnumerator ClearShootBlockNextFrame()
    {
        // Wait a frame so ItemController won’t see this LMB
        yield return null;
        if (gun) gun.SetExternalShootBlock(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CoalSource>()) insideCoalSource = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<CoalSource>()) insideCoalSource = false;
    }
}
