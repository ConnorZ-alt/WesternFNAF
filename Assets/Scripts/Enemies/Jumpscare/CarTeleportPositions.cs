using UnityEngine;
[System.Serializable]
public class CarTeleportPositions
{
    public Transform top;    // when enemy is BELOW player (comes from above)
    public Transform bottom; // when enemy is ABOVE player (comes from below)
}
