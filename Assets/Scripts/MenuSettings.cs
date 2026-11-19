using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// Key bindings and volume levels, saved in PlayerPrefs.
/// Values are cached in memory: input is polled many times a frame, and reading
/// PlayerPrefs (plus building "Binding.X" strings) on every check was pure waste.
public static class MenuSettings
{
    public static readonly string[] Actions = { "Left", "Right", "Up", "Down", "Attack", "Heavy", "Magic", "Jump", "Dodge", "Help", "Restart", "Items", "Interact", "Shield" };
    static readonly Key[] Defaults = { Key.A, Key.D, Key.W, Key.S, Key.J, Key.K, Key.L, Key.Space, Key.F, Key.Tab, Key.R, Key.G, Key.E, Key.LeftShift };
    static Key[] bindings;
    static readonly Dictionary<string, float> volumes = new Dictionary<string, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { bindings = null; volumes.Clear(); }

    static void EnsureLoaded()
    {
        if (bindings != null) return;
        RepairBindings();
        bindings = new Key[Actions.Length];
        for (int i = 0; i < Actions.Length; i++)
        {
            var k = (Key)PlayerPrefs.GetInt("Binding." + Actions[i], (int)Defaults[i]);
            bindings[i] = IsUsable(k) ? k : Defaults[i];
        }
    }

    // A saved key the keyboard can't index (None, or a junk value) would throw on
    // every input check and freeze all controls, so fall back to the default.
    static bool IsUsable(Key k) { return k > Key.None && k <= Key.OEM5; }

    /// One-time fixes for bindings saved by older builds.
    static void RepairBindings()
    {
        // Two legacy swaps seen in the wild: Right<->Jump and Right<->Dodge.
        if (PlayerPrefs.GetInt("Bindings.RightJumpRepair", 0) == 0)
        {
            if (PlayerPrefs.GetInt("Binding.Right", (int)Key.D) == (int)Key.Space && PlayerPrefs.GetInt("Binding.Jump", (int)Key.Space) == (int)Key.D)
            { PlayerPrefs.SetInt("Binding.Right", (int)Key.D); PlayerPrefs.SetInt("Binding.Jump", (int)Key.Space); }
            PlayerPrefs.SetInt("Bindings.RightJumpRepair", 1);
        }
        if (PlayerPrefs.GetInt("Bindings.RightDodgeRepair", 0) == 0)
        {
            if (PlayerPrefs.GetInt("Binding.Right", (int)Key.D) == (int)Key.LeftShift && PlayerPrefs.GetInt("Binding.Dodge", (int)Key.LeftShift) == (int)Key.D)
            { PlayerPrefs.SetInt("Binding.Right", (int)Key.D); PlayerPrefs.SetInt("Binding.Dodge", (int)Key.LeftShift); }
            PlayerPrefs.SetInt("Bindings.RightDodgeRepair", 1);
        }
        // Roll moved from Shift to F, the hotbar from F to G, and Shift became the shield.
        // Anyone still on the old defaults comes along; custom layouts are kept.
        if (PlayerPrefs.GetInt("Bindings.ShieldRemap", 0) == 0)
        {
            if (PlayerPrefs.GetInt("Binding.Dodge", (int)Key.LeftShift) == (int)Key.LeftShift) PlayerPrefs.SetInt("Binding.Dodge", (int)Key.F);
            if (PlayerPrefs.GetInt("Binding.Items", (int)Key.F) == (int)Key.F) PlayerPrefs.SetInt("Binding.Items", (int)Key.G);
            PlayerPrefs.SetInt("Bindings.ShieldRemap", 1);
        }
        PlayerPrefs.Save();
    }

    public static Key Binding(int i) { EnsureLoaded(); return bindings[i]; }

    static KeyControl Control(int i) { var kb = Keyboard.current; return kb == null ? null : kb[Binding(i)]; }
    public static bool Held(int i) { var c = Control(i); return c != null && c.isPressed; }
    public static bool Pressed(int i) { var c = Control(i); return c != null && c.wasPressedThisFrame; }

    /// Binds action i to key; whichever action already had that key takes i's old one.
    public static void Bind(int i, Key key)
    {
        if (!IsUsable(key)) return;
        EnsureLoaded();
        Key old = bindings[i];
        for (int n = 0; n < Actions.Length; n++)
            if (n != i && bindings[n] == key) { bindings[n] = old; PlayerPrefs.SetInt("Binding." + Actions[n], (int)old); }
        bindings[i] = key;
        PlayerPrefs.SetInt("Binding." + Actions[i], (int)key);
        PlayerPrefs.Save();
    }

    public static float Volume(string name)
    {
        if (!volumes.TryGetValue(name, out var v)) { v = PlayerPrefs.GetFloat("Volume." + name, 1f); volumes[name] = v; }
        return v;
    }

    public static void SetVolume(string name, float v)
    {
        volumes[name] = v;
        PlayerPrefs.SetFloat("Volume." + name, v);
    }
}
