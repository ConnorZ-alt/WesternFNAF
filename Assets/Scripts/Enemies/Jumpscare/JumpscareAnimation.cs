using UnityEngine;

public class JumpscareAnimation : MonoBehaviour
{
    public Animator animator;
    public Transform jumpscareAnimation;
    public Transform jumpscareModel;

    public void StartCrouchingAnimation()
    {
        ResetTransform(jumpscareAnimation);
        animator.Play("JumpscareLungeState", 0, 0f); // 0f = start at first frame
        animator.Update(0f); // forces it to apply immediately
        Stop();
    }
    
    public void StartJumpscareLungeAnimation()
    {
        animator.speed = 1.5f;
        PlayState(animator, "JumpscareLungeState", 0);
    }
    
    public void Stop()
    {
        animator.speed = 0f;
    }

    public void FlipAnimation()
    {
        Vector3 rot = jumpscareModel.eulerAngles;
        jumpscareModel.eulerAngles = new Vector3(rot.x, rot.y + 180f, rot.z);
    }
    
    private void PlayState(Animator anim, string stateName, int layer = 0)
    {
        anim.Play(stateName, layer, 0f); 
        anim.Update(0f); 
    }

    private void ResetTransform(Transform t)
    {
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
    }
}
