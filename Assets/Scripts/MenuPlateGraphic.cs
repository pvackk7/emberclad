using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class MenuPlateGraphic : MaskableGraphic
{
    public float focus;

    void Quad(VertexHelper vh, Rect rect, Color tint)
    {
        int n = vh.currentVertCount;
        vh.AddVert(new Vector2(rect.xMin, rect.yMin), tint, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMin, rect.yMax), tint, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMax, rect.yMax), tint, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMax, rect.yMin), tint, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
        vh.AddTriangle(n, n + 2, n + 3);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Quad(vh, r, Color.Lerp(new Color(.39f, .32f, .24f, .7f), new Color(.79f, .61f, .38f, .9f), focus));
        Quad(vh, new Rect(r.xMin + 1, r.yMin + 1, r.width - 2, r.height - 2), new Color(.045f, .035f, .055f, .9f));
    }
}
