using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CoalSource : MonoBehaviour
{
    [Header("Who can use this source")]
    [SerializeField] private string playerTag = "Player";

    [Header("Optional: a small UI prompt to show when the player is in range")]
    [SerializeField] private GameObject promptUI;

    private Collider col;
    private Rigidbody rb;

    private void Reset()
    {
        // Ensure trigger collider + kinematic rigidbody (so triggers work with CharacterController players)
        col = GetComponent<Collider>();
        col.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Awake()
    {
        // Re-enforce at runtime in case something changed in the Inspector
        col = GetComponent<Collider>();
        col.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (promptUI) promptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (promptUI) promptUI.SetActive(true);
        // PlayerCoalThrower already checks GetComponent<CoalSource>() in its OnTriggerEnter,
        // so this object just needs to exist as a tagged trigger. No extra calls needed here.
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (promptUI) promptUI.SetActive(false);
    }

    // Nice to have: visualize the trigger area in the editor
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0f, 0f, 0.15f);
        var box = GetComponent<Collider>() as BoxCollider;
        if (box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0f, 0f, 0f, 0.35f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else
        {
            Gizmos.DrawSphere(transform.position, 0.3f);
        }
    }
}
