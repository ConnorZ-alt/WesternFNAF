using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CoalCatchSurface : MonoBehaviour
{
    [SerializeField] private CoalReceiver coalReceiver; // drag parent CoalReceiver

    private void Reset()
    {
        var surfaceCollider = GetComponent<Collider>();
        surfaceCollider.isTrigger = false; // physical stop
        gameObject.layer = LayerMask.NameToLayer("TrainFloor");

        if (!coalReceiver)
            coalReceiver = GetComponentInParent<CoalReceiver>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Handle only actual coal pieces
        var coalPiece = collision.collider.GetComponentInParent<CoalPiece>();
        if (!coalPiece || coalReceiver == null) return;

        // Credit coal and destroy the piece
        var trainController = coalReceiver.GetTrain();   // helper on CoalReceiver
        float coalAmount    = coalReceiver.GetCoalValue(); // helper on CoalReceiver
        if (trainController) trainController.AddCoal(coalAmount);

        Destroy(coalPiece.gameObject);
    }
}