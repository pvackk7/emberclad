using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class BuildForestPrototype
{
    const string ArtDir = "Assets/Art/Sprites";
    const string AnimDir = "Assets/Art/Animations";

    static Sprite ImportAsSprite(string path, Vector2 pivot, float ppu = 100f)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new Exception("No importer at " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static AnimationClip MakeClip(string name, Sprite[] frames, float fps, bool loop)
    {
        if (!AssetDatabase.IsValidFolder(AnimDir))
            AssetDatabase.CreateFolder("Assets/Art", "Animations");
        var clip = new AnimationClip { frameRate = fps };
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        string path = $"{AnimDir}/{name}.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static GameObject MakeSprite(string name, Sprite sprite, Vector3 pos, string sortLayer, int order, float scale = 1f, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = sortLayer;
        sr.sortingOrder = order;
        return go;
    }

    static void EnsureSortingLayer(string name)
    {
        var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = so.FindProperty("m_SortingLayers");
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return;
        int idx = layers.arraySize;
        layers.InsertArrayElementAtIndex(idx);
        var el = layers.GetArrayElementAtIndex(idx);
        el.FindPropertyRelative("name").stringValue = name;
        el.FindPropertyRelative("uniqueID").intValue = UnityEngine.Random.Range(100000, int.MaxValue);
        so.ApplyModifiedProperties();
    }

    public static string All()
    {
        AssetDatabase.Refresh();

        EnsureSortingLayer("Background");

        // ---- Import sprites ----
        var sky = ImportAsSprite($"{ArtDir}/sky_backdrop.png", new Vector2(0.5f, 0.5f));
        var ground = ImportAsSprite($"{ArtDir}/ground_grass.png", new Vector2(0.5f, 0.5f));
        var path = ImportAsSprite($"{ArtDir}/ground_path.png", new Vector2(0.5f, 0.5f));
        var treePine = ImportAsSprite($"{ArtDir}/tree_pine.png", new Vector2(0.5f, 0.04f));
        var treeRound = ImportAsSprite($"{ArtDir}/tree_round.png", new Vector2(0.5f, 0.04f));
        var treeSmall = ImportAsSprite($"{ArtDir}/tree_small.png", new Vector2(0.5f, 0.04f));
        var bush = ImportAsSprite($"{ArtDir}/bush.png", new Vector2(0.5f, 0.08f));
        var idle0 = ImportAsSprite($"{ArtDir}/player_idle_0.png", new Vector2(0.5f, 0.06f), 140f);
        var idle1 = ImportAsSprite($"{ArtDir}/player_idle_1.png", new Vector2(0.5f, 0.06f), 140f);
        var walk0 = ImportAsSprite($"{ArtDir}/player_walk_0.png", new Vector2(0.5f, 0.06f), 140f);
        var walk1 = ImportAsSprite($"{ArtDir}/player_walk_1.png", new Vector2(0.5f, 0.06f), 140f);
        var walk2 = ImportAsSprite($"{ArtDir}/player_walk_2.png", new Vector2(0.5f, 0.06f), 140f);
        var walk3 = ImportAsSprite($"{ArtDir}/player_walk_3.png", new Vector2(0.5f, 0.06f), 140f);

        // ---- Animations + Controller ----
        var idleClip = MakeClip("Idle", new[] { idle0, idle1 }, 2.5f, true);
        var walkClip = MakeClip("Walk", new[] { walk0, walk1, walk2, walk3 }, 9f, true);

        string ctrlPath = $"{AnimDir}/PlayerAnimator.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var rootSM = controller.layers[0].stateMachine;
        var idleState = rootSM.AddState("Idle");
        idleState.motion = idleClip;
        var walkState = rootSM.AddState("Walk");
        walkState.motion = walkClip;
        rootSM.defaultState = idleState;
        var t1 = idleState.AddTransition(walkState);
        t1.hasExitTime = false; t1.duration = 0.05f;
        t1.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
        var t2 = walkState.AddTransition(idleState);
        t2.hasExitTime = false; t2.duration = 0.05f;
        t2.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");

        // ---- Clear any sample scene default objects we don't want ----
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Main Camera" || root.name == "Global Light 2D") continue;
            UnityEngine.Object.DestroyImmediate(root);
        }

        // ---- Background & ground ----
        var backgroundRoot = new GameObject("World");
        MakeSprite("Sky", sky, new Vector3(0, 1.2f, 0), "Background", -100, 1.1f, backgroundRoot.transform);
        var groundGo = MakeSprite("Ground", ground, new Vector3(0, -1.6f, 0), "Background", -50, 1f, backgroundRoot.transform);
        groundGo.transform.localScale = new Vector3(5.2f, 2.0f, 1f);
        MakeSprite("ClearingPath", path, new Vector3(0, -0.6f, 0), "Background", -40, 1.6f, backgroundRoot.transform);

        // ---- Trees & bushes scattered for depth testing ----
        var scenery = new GameObject("Scenery");
        var treeSprites = new[] { treePine, treeRound, treeSmall };
        var rnd = new System.Random(42);
        float NextF(float a, float b) => (float)(a + rnd.NextDouble() * (b - a));
        int created = 0;
        for (int i = 0; i < 9; i++)
        {
            float x = NextF(-7.8f, 7.8f);
            float y = NextF(0.1f, 1.1f); // far row (behind player's lane)
            var s = treeSprites[rnd.Next(treeSprites.Length)];
            MakeSprite($"Tree_Far_{i}", s, new Vector3(x, y, 0), "Default", 0, NextF(0.9f, 1.3f), scenery.transform);
            created++;
        }
        for (int i = 0; i < 5; i++)
        {
            float x = NextF(-7.5f, 7.5f);
            float y = NextF(-2.6f, -1.9f); // near row (in front of player's lane)
            var s = treeSprites[rnd.Next(treeSprites.Length)];
            MakeSprite($"Tree_Near_{i}", s, new Vector3(x, y, 0), "Default", 0, NextF(1.0f, 1.4f), scenery.transform);
            created++;
        }
        for (int i = 0; i < 6; i++)
        {
            float x = NextF(-7.5f, 7.5f);
            float y = NextF(-1.8f, 0.2f);
            MakeSprite($"Bush_{i}", bush, new Vector3(x, y, 0), "Default", 0, NextF(0.8f, 1.2f), scenery.transform);
            created++;
        }

        // ---- Player ----
        var player = MakeSprite("Player", idle0, new Vector3(0, -1.2f, 0), "Default", 0, 1f, null);
        var anim = player.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        var pc = player.AddComponent<PlayerController>();
        pc.minY = -3.0f; pc.maxY = 1.0f; pc.minX = -8.2f; pc.maxX = 8.2f;

        // ---- Camera ----
        var camGo = GameObject.Find("Main Camera");
        if (camGo == null) { camGo = new GameObject("Main Camera"); camGo.AddComponent<Camera>(); camGo.tag = "MainCamera"; }
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4.2f;
        camGo.transform.position = new Vector3(0, -0.6f, -10f);
        var follow = camGo.GetComponent<CameraFollow>();
        if (follow == null) follow = camGo.AddComponent<CameraFollow>();
        follow.target = player.transform;
        follow.minBounds = new Vector2(-4.0f, -1.6f);
        follow.maxBounds = new Vector2(4.0f, 0.6f);
        follow.yOffset = 0.5f;

        // ---- Global 2D light (keep if present, ensure visible) ----
        var light = GameObject.Find("Global Light 2D");
        if (light != null) light.SetActive(true);

        // ---- Transparency sort by world Y (depth-based sprite rendering) ----
        var gfx = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
        var gso = new SerializedObject(gfx);
        var modeProp = gso.FindProperty("m_TransparencySortMode");
        var axisProp = gso.FindProperty("m_TransparencySortAxis");
        if (modeProp != null) modeProp.enumValueIndex = 1; // Custom Axis
        if (axisProp != null) axisProp.vector3Value = new Vector3(0, 1, 0);
        gso.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        return $"OK: scenery={created}, idleFrames=2, walkFrames=4, sceneSaved={scene.path}";
    }
}
