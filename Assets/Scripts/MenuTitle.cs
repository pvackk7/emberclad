using UnityEngine;
using UnityEngine.UI;

/// The hand-lettered EMBERCLAD title on the main menu, sized to keep the art's aspect.
public class MenuTitle : MonoBehaviour
{
    void Awake()
    {
        var imageObject = new GameObject("Title artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var imageRect = (RectTransform)imageObject.transform;
        imageRect.SetParent(transform, false);
        imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
        var logo = imageObject.GetComponent<RawImage>();
        logo.texture = Resources.Load<Texture2D>("Menu/Title");
        logo.raycastTarget = false;
        var rt = (RectTransform)transform;
        if (logo.texture != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.x * logo.texture.height / logo.texture.width);
    }
}
