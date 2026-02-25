using Unity.VisualScripting;
using UnityEngine;

public class JumpscareHitbox : MonoBehaviour
{
    public SceneManagement sceneManagement;
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            sceneManagement.IsJumpscared = true;
            sceneManagement.OnGameOver();
        }
    }
}
