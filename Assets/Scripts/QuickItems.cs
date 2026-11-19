using UnityEngine;
using UnityEngine.InputSystem;

public static class QuickItems
{
    public static int Selected { get; private set; }
    public static int Hovered { get; private set; } = -1;
    public static bool Selecting { get; private set; }
    // Holding the hotbar key no longer freezes the knight: the selected item stays
    // usable while the wheel is open. Only the release frame is swallowed, so the
    // click that confirms a slot doesn't also swing or drink.
    public static bool BlockActions => released;
    static bool released, waitForRelease;
    static Vector2 motion, center;

    // Screen directions of the four wells as the HUD draws them: top, right,
    // bottom, left. Only the first three hold anything (sword, potion, embers),
    // and the empty one points left -- straight at the corner the wheel itself is
    // drawn in. Aiming that way used to resolve to an empty well and then do
    // nothing on release, which read as the hotbar simply going dead whenever
    // the mouse drifted toward it. Only filled slots are aimable.
    static readonly Vector2[] slotDirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    /// Three slots once Embers are found, two before: an empty well you can aim at
    /// is the hotbar going dead under your hand.
    public static int Filled { get { return Game.I != null && Game.I.HasEmbers ? 3 : 2; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Selected = 0; Hovered = -1; Selecting = released = waitForRelease = false;
        motion = Vector2.zero; center = Vector2.zero;
    }

    // Which filled slot is the mouse pointing most directly at? Every angle maps
    // to a real item, so there is no direction that quietly does nothing.
    public static int Direction(Vector2 delta) { return Direction(delta, -1); }

    public static int Direction(Vector2 delta, int current)
    {
        if (delta.magnitude < 20) return -1;
        Vector2 dir = delta.normalized;
        int best = -1; float bestDot = -2;
        for (int i = 0; i < Filled; i++)
        {
            float d = Vector2.Dot(dir, slotDirs[i]);
            if (d > bestDot) { bestDot = d; best = i; }
        }
        // Stickiness: once a slot is lit, the mouse has to lean clearly toward a
        // different one to steal it. Without this the diagonal between two slots
        // flickers back and forth while the hand is mid-sweep.
        if (current >= 0 && current < Filled && best != current &&
            bestDot < Vector2.Dot(dir, slotDirs[current]) + 0.15f) return current;
        return best;
    }

    public static void Tick(Game game)
    {
        released = false;
        bool held = MenuSettings.Held(11);
        if (!game.CanUseItems || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            Selecting = false; Hovered = -1; waitForRelease = held; return;
        }
        if (waitForRelease) { waitForRelease = held; return; }
        if (held)
        {
            if (!Selecting)
            {
                Selecting = true; motion = Vector2.zero; Hovered = -1;
                // The cursor's spot at the moment the hotbar key goes down becomes the wheel's
                // center, like a joystick snapped to wherever the hand already was,
                // so a small circle anywhere on screen sweeps the whole wheel.
                if (Mouse.current != null) center = Mouse.current.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                motion = Vector2.ClampMagnitude(Mouse.current.position.ReadValue() - center, 140);
                int dir = Direction(motion, Hovered);
                // Latch the last slot aimed at instead of falling back to nothing
                // when the mouse comes to rest inside the dead zone.
                if (dir >= 0) Hovered = dir;
            }
        }
        else if (Selecting)
        {
            if (Hovered >= 0 && Hovered < Filled) Selected = Hovered;
            Selecting = false; released = true; Hovered = -1;
        }
    }
}
