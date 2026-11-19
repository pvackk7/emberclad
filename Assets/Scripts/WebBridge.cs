using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// What the game needs from the web page on phones (Plugins/WebGL/EmbercladSite.jslib):
/// touch detection, a native text field for the nickname, and fullscreen. Outside a
/// WebGL build every call is a harmless no-op.
public static class WebBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int EmbercladIsTouch();
    [DllImport("__Internal")] static extern void EmbercladTextOpen(float x, float y, float w, float h, int maxLen);
    [DllImport("__Internal")] static extern void EmbercladTextPlace(float x, float y, float w, float h);
    [DllImport("__Internal")] static extern void EmbercladTextClose();
    [DllImport("__Internal")] static extern int EmbercladTextVersion();
    [DllImport("__Internal")] static extern string EmbercladTextValue();
    [DllImport("__Internal")] static extern void EmbercladTextSet(string value);
    [DllImport("__Internal")] static extern int EmbercladTextTakeSubmit();
    [DllImport("__Internal")] static extern int EmbercladTextFocused();
    [DllImport("__Internal")] static extern int EmbercladFullscreenState();
    [DllImport("__Internal")] static extern void EmbercladFullscreenToggle();
    public const bool Web = true;
#else
    public const bool Web = false;
#endif

    /// Phone or tablet. In the editor, set EditorPrefs "Emberclad.ForceTouch" to try the
    /// touch layout with the Device Simulator.
    public static bool IsTouchDevice()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { return EmbercladIsTouch() != 0; } catch { return false; }
#elif UNITY_EDITOR
        return UnityEditor.EditorPrefs.GetBool("Emberclad.ForceTouch", false);
#else
        return Application.isMobilePlatform;
#endif
    }

    // ---- native text field --------------------------------------------------------

    /// rect: the field in screen pixels (Unity's bottom-left origin).
    public static void TextOpen(Rect rect, int maxLen)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var n = Normalise(rect); EmbercladTextOpen(n.x, n.y, n.width, n.height, maxLen);
#endif
    }

    public static void TextPlace(Rect rect)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var n = Normalise(rect); EmbercladTextPlace(n.x, n.y, n.width, n.height);
#endif
    }

    public static void TextClose()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        EmbercladTextClose();
#endif
    }

    public static int TextVersion()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return EmbercladTextVersion();
#else
        return 0;
#endif
    }

    public static string TextValue()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return EmbercladTextValue() ?? "";
#else
        return "";
#endif
    }

    public static void TextSet(string value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        EmbercladTextSet(value ?? "");
#endif
    }

    public static bool TextTakeSubmit()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return EmbercladTextTakeSubmit() != 0;
#else
        return false;
#endif
    }

    public static bool TextFocused()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return EmbercladTextFocused() != 0;
#else
        return false;
#endif
    }

    /// Pixel rect (bottom-left origin) to fractions of the canvas from its top-left.
    static Rect Normalise(Rect r)
    {
        float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
        return new Rect(r.xMin / sw, 1f - r.yMax / sh, r.width / sw, r.height / sh);
    }

    // ---- fullscreen ---------------------------------------------------------------

    public enum Fullscreen { Off = 0, On = 1, Unsupported = 2, HomeScreenApp = 3 }

    public static Fullscreen FullscreenState()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { return (Fullscreen)EmbercladFullscreenState(); } catch { return Fullscreen.Off; }
#else
        return Screen.fullScreen ? Fullscreen.On : Fullscreen.Off;
#endif
    }

    public static void ToggleFullscreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        EmbercladFullscreenToggle();
#else
        Screen.fullScreen = !Screen.fullScreen;
#endif
    }
}
