using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class JumpscareHitbox : MonoBehaviour
{
    public SceneManagement sceneManagement;
    
    public float timeToJumpscare;
    
    private Coroutine jumpscareTimer;
    private bool isJumpscareTimerRunning = false;
    public void OnTriggerEnter(Collider other) 
    {
        if (other.CompareTag("Player"))
        {
            if (timeToJumpscare == 0) // jumpscare player if there is no time before a jumpscare
            {
                JumpscarePlayer();
            } else if (!isJumpscareTimerRunning) // starts jumpscare timmer if one is not running
            {
                jumpscareTimer = StartCoroutine(WaitToJumpscare(timeToJumpscare));
            }
        }
    }
    
    public void OnTriggerExit(Collider other)
    {
        StopJumpscareTimer();
    }
    
    protected IEnumerator WaitToJumpscare(float time)
    {
        isJumpscareTimerRunning = true;
        yield return new WaitForSeconds(time);
        JumpscarePlayer();
    }

    private void JumpscarePlayer()
    {
        isJumpscareTimerRunning = false;
        sceneManagement.IsJumpscared = true;
        sceneManagement.OnGameOver();
    }

    public void StopJumpscareTimer()
    {
        if (isJumpscareTimerRunning)
        {
            StopCoroutine(jumpscareTimer);
        } 
    }
}

