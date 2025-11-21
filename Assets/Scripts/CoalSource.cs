using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CoalSource : MonoBehaviour
{
    [Header("Who can use this source")]
    [SerializeField] private string playerTag = "Player";

    [Header("Optional: a small UI prompt to show when the player is in range")]
    [SerializeField] private GameObject promptUserInterfaceObject;

    private Collider sourceCollider;
    private Rigidbody sourceRigidbody;

    private void Reset()
    {
        // Ensure trigger collider + kinematic rigidbody (so triggers work with CharacterController players)
        sourceCollider = GetComponent<Collider>();
        sourceCollider.isTrigger = true;

        sourceRigidbody = GetComponent<Rigidbody>();
        if (!sourceRigidbody) sourceRigidbody = gameObject.AddComponent<Rigidbody>();
        sourceRigidbody.isKinematic = true;
        sourceRigidbody.useGravity = false;
    }

    private void Awake()
    {
        // Re-enforce at runtime in case something changed in the Inspector
        sourceCollider = GetComponent<Collider>();
        sourceCollider.isTrigger = true;

        sourceRigidbody = GetComponent<Rigidbody>();
        if (!sourceRigidbody) sourceRigidbody = gameObject.AddComponent<Rigidbody>();
        sourceRigidbody.isKinematic = true;
        sourceRigidbody.useGravity = false;

        if (promptUserInterfaceObject) promptUserInterfaceObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        if (!otherCollider.CompareTag(playerTag)) return;
        if (promptUserInterfaceObject) promptUserInterfaceObject.SetActive(true);
        // PlayerCoalThrower already checks GetComponent<CoalSource>() in its OnTriggerEnter,
        // so this object just needs to exist as a tagged trigger. No extra calls needed here.
    }

    private void OnTriggerExit(Collider otherCollider)
    {
        if (!otherCollider.CompareTag(playerTag)) return;
        if (promptUserInterfaceObject) promptUserInterfaceObject.SetActive(false);
    }

    // Nice to have: visualize the trigger area in the editor
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0f, 0f, 0.15f);
        var boxCollider = GetComponent<Collider>() as BoxCollider;
        if (boxCollider)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            Gizmos.color = new Color(0f, 0f, 0f, 0.35f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
        else
        {
            Gizmos.DrawSphere(transform.position, 0.3f);
        }
    }
}
