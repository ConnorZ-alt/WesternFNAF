using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CylinderUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform cogTransform;
    [SerializeField] private ChamberSlot[] chambers;
    
    [Header("Animation Settings")]
    [SerializeField] private float rotationDuration = 0.25f;
    [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private int currentChamberIndex;
    private float degreesPerChamber;

    public void Initialize(int chamberCount, int startingBullets)
    {
        degreesPerChamber = 360f / chamberCount;
        currentChamberIndex = 0;
    
        // Reset rotation
        cogTransform.localEulerAngles = Vector3.zero;
    
        // Clear ALL chambers first, then load the correct amount
        for (int i = 0; i < chambers.Length; i++)
        {
            chambers[i].Clear();
        }
    
        // Now load starting bullets
        for (int i = 0; i < Mathf.Min(startingBullets, chambers.Length); i++)
        {
            chambers[i].LoadBullet();
        }
    }

    public IEnumerator RotateToNext()
    {
        float startRotation = cogTransform.localEulerAngles.z;
        float endRotation = startRotation - degreesPerChamber;
        
        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = rotationCurve.Evaluate(elapsed / rotationDuration);
            float angle = Mathf.Lerp(startRotation, endRotation, t);
            cogTransform.localEulerAngles = new Vector3(0, 0, angle);
            yield return null;
        }
        
        cogTransform.localEulerAngles = new Vector3(0, 0, endRotation);
        currentChamberIndex = (currentChamberIndex + 1) % chambers.Length;
    }

    public IEnumerator RemoveCurrentBullet()
    {
        yield return chambers[currentChamberIndex].EjectBullet();
    }

    public IEnumerator LoadBulletFromPosition(Vector3 worldStartPos)
    {
        int emptyIndex = FindEmptyChamber();
        if (emptyIndex < 0) yield break;
        
        yield return chambers[emptyIndex].LoadBulletAnimated(worldStartPos);
    }

    private int FindEmptyChamber()
    {
        for (int i = 0; i < chambers.Length; i++)
        {
            if (chambers[i].IsEmpty) return i;
        }
        return -1;
    }
}