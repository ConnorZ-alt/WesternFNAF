using UnityEngine;

public class TrainBridgeManager : MonoBehaviour
{
    [SerializeField] private TrainCarBridge bridgePrefab;

    [SerializeField] private Transform engineBack;
    [SerializeField] private Transform car2Front;
    [SerializeField] private Transform car2Back;
    [SerializeField] private Transform car3Front;

    private void Awake()
    {
        CreateBridge(engineBack, car2Front);
        CreateBridge(car2Back, car3Front);
    }

    private void CreateBridge(Transform backAnchor, Transform frontAnchor)
    {
        var bridge = Instantiate(bridgePrefab);
        bridge.SetAnchors(backAnchor, frontAnchor);
    }
}