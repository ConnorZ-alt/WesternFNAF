using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CoalReceiver : MonoBehaviour
{
    [SerializeField] private TrainController train;      // drag the Train (with TrainController) here
    [SerializeField] private float coalValueOverride = -1f; // -1 => use train’s GetCoalPerPiece()

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // make sure it's a trigger
    }

    private void OnTriggerEnter(Collider other)
    {
        // must be a thrown rigidbody (your Coal prefab has a Rigidbody)
        if (!other.attachedRigidbody) return;

        // quick filter: tag preferred (set your Coal prefab tag to "Coal")
        if (!other.CompareTag("Coal"))
        {
            // fallback: name contains "coal"
            if (!other.name.ToLower().Contains("coal")) return;
        }

        if (!train)
        {
            Debug.LogWarning("CoalReceiver: Train reference not set.");
            return;
        }

        float pieceValue = (coalValueOverride > 0f) ? coalValueOverride : train.GetCoalAmount();
        train.AddCoal(pieceValue);

        Destroy(other.gameObject); // consume the coal
    }
}