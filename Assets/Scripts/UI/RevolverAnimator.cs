using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RevolverAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ItemController itemController;
    [SerializeField] private Animator revolverAnimator;

    [Header("Animation Names")]
    [SerializeField] private string idleStateName   = "idle";
    [SerializeField] private string pulloutStateName = "pullout";
    [SerializeField] private string shootStateName   = "shoot";
    [SerializeField] private string reloadStateName  = "reload";

    // Animator parameter names
    private static readonly int TriggerShoot  = Animator.StringToHash("Shoot");
    private static readonly int TriggerReload = Animator.StringToHash("Reload");
    private static readonly int TriggerPullout = Animator.StringToHash("Pullout");

    private int lastKnownCylinder = -1;

    private void Awake()
    {
        // Auto-find if not wired in Inspector
        if (!itemController)
            itemController = GetComponent<ItemController>();
        if (!revolverAnimator)
            revolverAnimator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (itemController != null)
            itemController.OnAmmoChanged += HandleAmmoChanged;
    }

    private void OnDisable()
    {
        if (itemController != null)
            itemController.OnAmmoChanged -= HandleAmmoChanged;
    }

    private void Start()
    {
        // Play pullout animation when the gun first appears
        if (revolverAnimator != null)
            revolverAnimator.SetTrigger(TriggerPullout);

        if (itemController != null)
            lastKnownCylinder = itemController.GetRoundsInCylinder();
    }

    private void HandleAmmoChanged(int cylinderRounds, int reserveRounds)
    {
        if (revolverAnimator == null) return;

        if (cylinderRounds < lastKnownCylinder)
        {
            // Rounds went down = shot fired
            revolverAnimator.SetTrigger(TriggerShoot);
        }
        else if (cylinderRounds > lastKnownCylinder)
        {
            // Rounds went up = reload happened
            revolverAnimator.SetTrigger(TriggerReload);
        }

        lastKnownCylinder = cylinderRounds;
    }
}