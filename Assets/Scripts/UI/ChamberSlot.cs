using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ChamberSlot : MonoBehaviour
{
    [SerializeField] private Image bulletImage;
    [SerializeField] private float animationDuration = 0.2f;
    
    public bool IsEmpty => !bulletImage.enabled;

    public void LoadBullet()
    {
        bulletImage.enabled = true;
        bulletImage.color = Color.white;
        bulletImage.transform.localScale = Vector3.one;
    }

    public IEnumerator EjectBullet()
    {
        // Shrink and fade out
        float elapsed = 0f;
        Vector3 startScale = Vector3.one;
        Color startColor = bulletImage.color;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            
            bulletImage.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            bulletImage.color = new Color(startColor.r, startColor.g, startColor.b, 1 - t);
            yield return null;
        }
        
        bulletImage.enabled = false;
    }

    public IEnumerator LoadBulletAnimated(Vector3 worldStartPos)
    {
        bulletImage.enabled = true;
        bulletImage.color = new Color(1, 1, 1, 0);
        
        RectTransform rt = bulletImage.rectTransform;
        Vector3 localEndPos = rt.localPosition;
        
        // Convert world start to local space
        Vector3 localStartPos = rt.parent.InverseTransformPoint(worldStartPos);
        
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Smooth step for position
            float smoothT = t * t * (3f - 2f * t);
            
            rt.localPosition = Vector3.Lerp(localStartPos, localEndPos, smoothT);
            bulletImage.color = new Color(1, 1, 1, t);
            yield return null;
        }
        
        rt.localPosition = localEndPos;
        bulletImage.color = Color.white;
    }
}
