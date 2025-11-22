using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SneakCover : MonoBehaviour
{
    [Header("Slot Discovery")]
    [Tooltip("If empty, slots are auto-found in children by component SneakCoverSlot.")]
    [SerializeField] private List<SneakCoverSlot> slots = new List<SneakCoverSlot>();

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color freeColor = new Color(0.2f, 1f, 0.3f, 0.25f);
    [SerializeField] private Color reservedColor = new Color(1f, 0.3f, 0.2f, 0.35f);
    [SerializeField] private float gizmoRadius = 0.18f;

    private void Awake()
    {
        if (slots == null || slots.Count == 0)
        {
            slots = new List<SneakCoverSlot>(GetComponentsInChildren<SneakCoverSlot>(true));
            if (slots.Count == 0)
                Debug.LogWarning($"[SneakCover] No SneakCoverSlot children under {name}. Add a few empties with SneakCoverSlot.");
        }
    }

    /// <summary>Return the nearest FREE slot to a world position. Returns null if none available.</summary>
    public SneakCoverSlot GetNearestFreeSlot(Vector3 fromWorld)
    {
        SneakCoverSlot nearestSlot = null;
        float bestDistanceSqr = float.PositiveInfinity;

        foreach (var slotIter in slots)
        {
            if (!slotIter || slotIter.isReserved) continue;

            float distanceSqr = (slotIter.WorldPosition - fromWorld).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                nearestSlot = slotIter;
                bestDistanceSqr = distanceSqr;
            }
        }

        return nearestSlot;
    }

    /// <summary>Return any free slot (random). Null if none.</summary>
    public SneakCoverSlot GetRandomFreeSlot()
    {
        var freeList = new List<SneakCoverSlot>();
        foreach (var slotIter in slots)
        {
            if (slotIter && !slotIter.isReserved) freeList.Add(slotIter);
        }

        if (freeList.Count == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, freeList.Count);
        return freeList[randomIndex];
    }

    /// <summary>Try to reserve a slot; returns true on success.</summary>
    public bool TryReserveSlot(SneakCoverSlot slot)
    {
        if (!slot || slot.isReserved) return false;
        slot.isReserved = true;
        return true;
    }

    /// <summary>Reserve the nearest free slot to a position; returns the slot or null.</summary>
    public SneakCoverSlot ReserveNearest(Vector3 fromWorld)
    {
        var nearestSlot = GetNearestFreeSlot(fromWorld);
        if (nearestSlot != null) nearestSlot.isReserved = true;
        return nearestSlot;
    }

    /// <summary>Release a previously reserved slot.</summary>
    public void ReleaseSlot(SneakCoverSlot slot)
    {
        if (slot) slot.isReserved = false;
    }

    /// <summary>Convenience: release all (e.g., on scene reload/debug).</summary>
    public void ReleaseAll()
    {
        foreach (var slotIter in slots)
        {
            if (slotIter) slotIter.isReserved = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        var foundSlots = (slots != null && slots.Count > 0)
            ? slots
            : new List<SneakCoverSlot>(GetComponentsInChildren<SneakCoverSlot>(true));

        foreach (var slot in foundSlots)
        {
            if (!slot) continue;

            Gizmos.color = slot.isReserved ? reservedColor : freeColor;
            Gizmos.DrawSphere(slot.WorldPosition, gizmoRadius);
            Gizmos.DrawLine(slot.WorldPosition, slot.WorldPosition + slot.WorldForward * 0.6f);
        }

        // draw bench bounds if there's a collider (purely visual)
        var colliderComponent = GetComponent<Collider>();
        if (colliderComponent is BoxCollider boxCollider)
        {
            Gizmos.color = new Color(0, 0, 0, 0.08f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);

            Gizmos.color = new Color(0, 0, 0, 0.25f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}