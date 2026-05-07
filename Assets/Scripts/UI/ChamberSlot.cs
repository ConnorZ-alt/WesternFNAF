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
        // FIX: Reset position and scale so previous animation state doesn't bleed in
        bulletImage.rectTransform.localPosition = Vector3.zero;
        bulletImage.transform.localScale = Vector3.one;
        bulletImage.color = Color.white;
        bulletImage.enabled = true;
    }

    public void Clear()
    {
        bulletImage.enabled = false;
        // FIX: Also reset transform state so the slot is clean for next use
        bulletImage.rectTransform.localPosition = Vector3.zero;
        bulletImage.transform.localScale = Vector3.one;
    }

    public IEnumerator EjectBullet()
    {
        float elapsed = 0f;
        Vector3 startScale = Vector3.one;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;

            bulletImage.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            bulletImage.color = new Color(1, 1, 1, 1 - t);
            yield return null;
        }

        bulletImage.enabled = false;
        // Reset after eject so slot is clean
        bulletImage.transform.localScale = Vector3.one;
    }

    public IEnumerator LoadBulletAnimated(Vector3 screenStartPos)
    {
        // FIX: Convert the screen-space position from ReserveStripUI into the local space
        // of this bullet image's parent RectTransform, so the animation starts
        // at the correct position on screen.
        RectTransform rt = bulletImage.rectTransform;
        RectTransform parentRt = rt.parent as RectTransform;

        // Cache the end position (where this chamber slot's bullet should sit)
        Vector3 localEndPos = Vector3.zero; // bullet always ends at center of its slot
        Vector3 localStartPos;

        // Convert screen position → local position inside this slot's parent
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRt,
                screenStartPos,
                null, // null = Screen Space Overlay canvas (no camera needed)
                out Vector2 localPoint))
        {
            localStartPos = localPoint;
        }
        else
        {
            // Fallback: just pop in at the end position if conversion fails
            localStartPos = localEndPos;
        }

        bulletImage.enabled = true;
        bulletImage.color = new Color(1, 1, 1, 0);
        bulletImage.transform.localScale = Vector3.one;
        rt.localPosition = localStartPos;

        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = t * t * (3f - 2f * t);

            rt.localPosition = Vector3.Lerp(localStartPos, localEndPos, smoothT);
            bulletImage.color = new Color(1, 1, 1, t);
            yield return null;
        }

        rt.localPosition = localEndPos;
        bulletImage.color = Color.white;
    }
}