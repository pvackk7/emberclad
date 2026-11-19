using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// Emberclad > Recording View: puts the editor back on the Default layout and sets the
/// Game view up for screen recording -- Free Aspect (fills whatever space it has, scale 1x)
/// and Play Maximized, so pressing Play fills the whole Unity window with the game.
public static class RecordingView
{
    [MenuItem("Emberclad/Recording View")]
    public static void Apply()
    {
        EditorApplication.ExecuteMenuItem("Window/Layouts/Default");
        EditorApplication.delayCall += ConfigureGameView;
    }

    static void ConfigureGameView()
    {
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType == null) { Debug.LogWarning("Recording View: GameView type not found"); return; }
        var view = EditorWindow.GetWindow(gameViewType, false, "Game", true);
        const BindingFlags all = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // Free Aspect is entry 0 of the size list.
        var select = gameViewType.GetMethod("SizeSelectionCallback", all);
        if (select != null) select.Invoke(view, new object[] { 0, null });
        else { var idx = gameViewType.GetProperty("selectedSizeIndex", all); if (idx != null) idx.SetValue(view, 0); }

        // Play Maximized instead of Play Focused.
        foreach (var t in new[] { gameViewType, gameViewType.BaseType })
        {
            var p = t?.GetProperty("enterPlayModeBehavior", all);
            if (p == null) continue;
            p.SetValue(view, Enum.Parse(p.PropertyType, "PlayMaximized"));
            break;
        }

        // Scale back to fit (1x on Free Aspect) and full resolution, not low-res.
        var lowRes = gameViewType.GetProperty("lowResolutionForAspectRatios", all);
        if (lowRes != null && lowRes.CanWrite) lowRes.SetValue(view, false);
        var fit = gameViewType.GetMethod("SnapZoom", all, null, new[] { typeof(float) }, null);
        if (fit != null) fit.Invoke(view, new object[] { 1f });
        view.Repaint();
        Debug.Log("Recording View: Default layout, Game view on Free Aspect, Play Maximized.");
    }
}
