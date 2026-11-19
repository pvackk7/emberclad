using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuFocusButton : Button
{
    public static bool KeyboardFocus;
    MenuPlateGraphic plate;
    Text caption, marker;
    bool hovered;
    float emphasis;
    public Color idleColor = new Color(0.98f, 0.95f, 0.86f), focusColor = new Color(1, 0.86f, 0.53f);
    public float focusScale = 1.025f;

    public void Configure(MenuPlateGraphic background, Text label, Text arrow)
    {
        plate = background;
        caption = label;
        marker = arrow;
        transition = Transition.None;
        Refresh(0);
    }

    public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); KeyboardFocus = false; hovered = true; }
    public override void OnPointerExit(PointerEventData e) { base.OnPointerExit(e); hovered = false; }
    protected override void OnDisable() { hovered = false; base.OnDisable(); }

    void Update()
    {
        bool selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        emphasis = Mathf.MoveTowards(emphasis, IsInteractable() && (KeyboardFocus ? selected : hovered) ? 1 : 0, Time.unscaledDeltaTime * 9);
        Refresh(emphasis);
    }

    void Refresh(float t)
    {
        if (plate == null) return;
        if (!Mathf.Approximately(plate.focus, t)) { plate.focus = t; plate.SetVerticesDirty(); }
        caption.color = Color.Lerp(idleColor, focusColor, t);
        marker.color = new Color(1, 0.78f, 0.35f, t);
        transform.localScale = Vector3.one * Mathf.Lerp(1, focusScale, t);
    }
}
