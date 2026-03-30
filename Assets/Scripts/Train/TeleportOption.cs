using UnityEngine;

[System.Serializable]
public class TeleportOption
{
    public Transform position;
    public JumpscareHitbox hitbox;
    public int carIndex;
    public bool isAbovePlayer;
}