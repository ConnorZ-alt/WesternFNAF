using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CylinderUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform cogTransform;
    [SerializeField] private ChamberSlot[] chambers;
    [SerializeField] private GameObject bulletPrefab;
    
    [Header("Animation Settings")]
    [SerializeField] private float rotationDuration = 0.25f;
    [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private int currentChamberIndex;
    private float degreesPerChamber;

    public void Initialize(int chamberCount)
    {
        degreesPerChamber = 360f / chamberCount;
        currentChamberIndex = 0;
        
        // Fill all chambers
        foreach (var chamber in chambers)
        {
            chamber.LoadBullet();
        }
    }

    public IEnumerator RotateToNext()
    {
        float startRotation = cogTransform.localEulerAngles.z;
        float endRotation = startRotation - degreesPerChamber; // Negative for clockwise
        
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
        // Find first empty chamber
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