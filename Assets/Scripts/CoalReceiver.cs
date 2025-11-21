using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CoalReceiver : MonoBehaviour
{
    [SerializeField] private TrainController trainController;      // drag the Train (with TrainController) here
    [SerializeField] private float coalValueOverride = -1f;        // -1 => use train’s GetCoalAmount()

    public TrainController GetTrain() => trainController;
    
    private void Reset()
    {
        var receiverCollider = GetComponent<Collider>();
        receiverCollider.isTrigger = true; // make sure it's a trigger
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        // Must be a thrown coal with a rigidbody
        if (!otherCollider.attachedRigidbody) return;

        // Quick filter (tag optional)
        // if (!otherCollider.CompareTag("Coal")) return;

        var coalPiece = otherCollider.GetComponentInParent<CoalPiece>();
        if (!coalPiece) return;

        if (!trainController)
        {
            Debug.LogWarning("CoalReceiver: Train reference not set.");
            coalPiece.Consume(); // still clean up so it doesn’t linger
            return;
        }

        float coalAmountToAdd = (coalValueOverride > 0f) ? coalValueOverride : trainController.GetCoalAmount();
        trainController.AddCoal(coalAmountToAdd);

        // Despawn the coal here (marks it consumed so its OnCollisionEnter won’t run later)
        coalPiece.Consume();
    }

    public float GetCoalValue()
    {
        return (coalValueOverride > 0f)
            ? coalValueOverride
            : (trainController ? trainController.GetCoalAmount() : 0.2f);
    }
}