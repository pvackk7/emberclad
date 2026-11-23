using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(205)]
public sealed class ForestScenery : MonoBehaviour
{
    readonly List<Object> owned = new List<Object>();
    readonly List<MovingPlane> planes = new List<MovingPlane>();
    readonly List<Lamp> lamps = new List<Lamp>();
    readonly List<Mote> motes = new List<Mote>();
    Material paper, bark, bankPaint, stone, path, moonPaint;
    Sprite lanternSprite;
    Transform sky, forest, near, pines;
    EndCampfire campfire;
    static readonly int Wick = Shader.PropertyToID("_ForestWick");
    static readonly int Pool = Shader.PropertyToID("_ForestPool");

    struct MovingPlane { public Transform root; public float parallax, mist; public bool sky; }
    static ForestScenery active;
    sealed class Lamp { public SpriteRenderer halo, core; public float glassScale; public Material glass; public float ignition = 0; public Transform wick; public Mesh flame; public float phase; }   // ignition 0: lit from the start (it was -1, waiting for an ember cast)
    struct Mote { public SpriteRenderer sprite; public Vector3 home; public float phase; }
    readonly Vector3[] flamePoints = new Vector3[14];

    static Color Ink(byte r, byte g, byte b, float a = 1)
    {
        Color c = new Color32(r, g, b, 255);
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.linear;
        c.a = a;
        return c;
    }

    T Keep<T>(T asset) where T : Object { owned.Add(asset); return asset; }

    Material Paint(string resource = null, bool receivesLight = false)
    {
        var paint = Keep(new Material(Resources.Load<Shader>("Shaders/ForestInk")));
        paint.SetColor("_RendererColor", Color.white);
        paint.SetFloat("_Lamplit", receivesLight ? 1 : 0);
        if (resource != null) paint.mainTexture = Resources.Load<Texture2D>(resource);
        return paint;
    }

    public static void Build(Transform parent)
    {
        var root = new GameObject("Ancient forest path");
        root.transform.SetParent(parent, false);
        active = root.AddComponent<ForestScenery>();
        active.Illustrate();
    }

    Transform Plane(string name, float parallax, bool night = false, float mist = 0)
    {
        var root = new GameObject(name).transform;
        root.SetParent(transform, false);
        planes.Add(new MovingPlane { root = root, parallax = parallax, sky = night, mist = mist });
        return root;
    }

    SpriteRenderer Place(string name, Sprite sprite, Transform plane, float x, float y, float height,
        int order, Color color, bool facingLeft = false, string layer = "Background")
    {
        var obj = new GameObject(name);
        var t = obj.transform;
        t.SetParent(plane, false);
        t.localPosition = World.Project(new Vector2(x,y));
        float scale = height / sprite.bounds.size.y;
        t.localScale = new Vector3(facingLeft ? -scale : scale, scale, 1);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.sharedMaterial = bark; sr.sortingLayerName = layer; sr.sortingOrder = order; sr.color = color;
        return sr;
    }

    Mesh Surface(string name, Transform plane, Material mat, int order, Vector3[] v, Vector2[] uv, Color[] colors, int[] triangles,
        string layer = "Background")
    {
        var mesh = Keep(new Mesh { name = name });
        mesh.vertices = v; mesh.colors = colors;
        if (uv != null) mesh.uv = uv;
        mesh.triangles = triangles; mesh.RecalculateBounds();
        var go = new GameObject(name); go.transform.SetParent(plane,false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var sr = go.AddComponent<MeshRenderer>(); sr.sharedMaterial = mat; sr.sortingLayerName = layer; sr.sortingOrder = order;
        return mesh;
    }

    void Illustrate()
    {
        paper = Paint();
        bark = Paint(null, true);
        bankPaint = Paint("Forest/PineBank", true);
        stone = Paint("Forest/CanyonWall");
        stone.SetFloat("_DistanceMist", 0f);   // the misty pine art already carries its own haze
        stone.SetFloat("_Lamplit", .7f);   // the canyon rock catches a little of a nearby lantern
        path = Paint("Forest/ForestFloor", true);
        moonPaint = Paint("Menu/TitleBackground"); moonPaint.SetFloat("_Moon", 1);
        var lanternTexture = Resources.Load<Texture2D>("Forest/TrailLantern");
        // If the lantern art is missing (Forest/TrailLantern), the level simply has no lanterns instead of failing to load.
        lanternSprite = lanternTexture == null ? null : Keep(Sprite.Create(lanternTexture, new Rect(0, 0, lanternTexture.width, lanternTexture.height),
            new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect));   // standalone lantern: pivot at the middle of its base
        sky = Plane("Night beyond the canopy", .994f, true);
        forest = Plane("Wooded canyon wall", .62f);   // far misty pines drift slowly (were .135, which read as too quick)
        pines = Plane("Misty pines behind the trail", .38f);   // big misty pines: slower than the ground, quicker than the far row
        near = Plane("Roots and ruins", 0);
        Night();
        Ribbon(GroundPart.Cliff);
        Ribbon(GroundPart.Trail);
        Opening();
    }

    void Night()
    {
        // The same blue night-haze painting as the main menu (Menu/MenuBackground), hung on the
        // sky plane so it stays behind everything and barely drifts. 20 x 11.25 units keeps the
        // painting's own 16:9 shape and covers the view (7.2 units tall) even on wide screens.
        var haze = Paint("Menu/MenuBackground");
        const float w = 10f, h = 5.625f;
        Surface("Menu night haze", sky, haze, -100,
            new[]{new Vector3(-w,-h),new Vector3(w,-h),new Vector3(w,h),new Vector3(-w,h)},
            new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)},
            new[]{Color.white,Color.white,Color.white,Color.white},new[]{0,1,2,0,2,3});
        // Stars and moon over the haze, as before.
        var vertices = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
        var random = new System.Random(2815);
        Vector2 moon = new Vector2(2.42f,2.15f);
        // Evenly spread: one star per cell of a grid over the visible sky, jittered inside its cell,
        // and drawn over the moon's glow (only the moon disc itself stays clear).
        const int cols = 26, rows = 9; const float sx0 = -10f, sx1 = 10f, sy0 = -.6f, sy1 = 5.4f;
        for (int i = 0; i < cols * rows; i++)
        {
            float x = sx0 + (i % cols + (float)random.NextDouble()) * (sx1 - sx0) / cols;
            float y = sy0 + (i / cols + (float)random.NextDouble()) * (sy1 - sy0) / rows;
            if ((new Vector2(x, y) - moon).magnitude < 1.0f) continue;
            float r = .007f + (float)random.NextDouble() * .011f;
            Color c = Ink(164,204,216,.28f + (float)random.NextDouble() * .39f);
            int k = vertices.Count;
            vertices.Add(new Vector2(x-r,y)); vertices.Add(new Vector2(x,y+r*1.22f));
            vertices.Add(new Vector2(x+r,y)); vertices.Add(new Vector2(x,y-r*1.22f));
            for(int j=0;j<4;j++) colors.Add(c);
            indices.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
        }
        Surface("Distant stars",sky,paper,-95,vertices.ToArray(),null,colors.ToArray(),indices.ToArray());
        Vector2 center = new Vector2(2.42f,2.15f); // lowered with the closer camera (half-height 3.6) so the moon stays in frame
        Disc("Moonlit air",center,new[]{.05f,.83f,1.12f,1.52f,2.0f},new[]{.25f,.20f,.09f,.024f,0},false);
        Disc("Cratered moon",center,new[]{.025f,.79f,.875f,.916f,.95f},new[]{1f,1f,1f,.78f,0},true);
    }

    void Disc(string name, Vector2 center, float[] rings, float[] alpha, bool lunar)
    {
        const int slices = 64;
        var v = new Vector3[rings.Length*slices+1]; var uv = new Vector2[v.Length]; var c = new Color[v.Length];
        var tri = new List<int>();
        // The moon is cut out of the 1672 x 941 painting by pixel position. Use the painting's own size,
        // not the imported texture's: the web build caps textures (this one came in at 1024 x 576), and
        // 1/texture.width then cut out a region 1.6x too big -- the stretched, blocky moon on the website.
        const float PaintingW = 1672f, PaintingH = 941f;
        Vector2 texel = new Vector2(1f/PaintingW,1f/PaintingH);
        Vector2 lunarCenter = new Vector2(1303f/PaintingW,702f/PaintingH);
        v[0] = center; uv[0] = lunarCenter; c[0] = lunar ? Color.white : Ink(79,157,185,alpha[0]);
        for (int j = 0; j < rings.Length; j++)
        for (int i = 0; i < slices; i++)
        {
            float a = i * (2*Mathf.PI/slices); Vector2 d = new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            int k=1+j*slices+i, next=1+j*slices+(i+1)%slices;
            v[k] = center+d*rings[j]; uv[k] = lunarCenter + Vector2.Scale(texel,d) * (rings[j]/.916f*34);
            c[k] = lunar ? new Color(1,1,1,alpha[j]) : Ink(79,157,185,alpha[j]);
            if(j==0) tri.AddRange(new[]{0,k,next});
            else tri.AddRange(new[]{k-slices,k,next,k-slices,next,next-slices});
        }
        Surface(name,sky,lunar?moonPaint:paper,lunar?-96:-97,v,uv,c,tri.ToArray());
    }

    enum GroundPart { Cliff, Trail }

    void Ribbon(GroundPart part)
    {
        bool cliff = part == GroundPart.Cliff;
        float x = cliff ? -25.5f : -16.2f;
        float[] widths = cliff ? new[] { 9.74f } :   // misty pines: one width so the art keeps its own proportions
            new[] { 22.7f, 26.4f, 24.6f, 29.8f, 23.3f };
        int section = 0;
        while (x < World.MaxX + 33)
        {
            float width = widths[section % widths.Length];
            int steps = Mathf.CeilToInt(width / .48f);
            var vertices = new Vector3[(steps + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[steps * 6];
            bool forward = cliff ? section % 2 == 1 : section % 2 == 0;
            for (int j = 0; j <= steps; j++)
            {
                float t = j / (float)steps;
                float px = x + t * width;
                float soil = World.Height(px);
                // The trail is now a wide field: its painted horizon sits just behind the back
                // of the lane and its cliff edge just below the front (see FieldTop/FieldBottom).
                float top = cliff ? 2.81f : FieldTop;   // flat: a wavy top stretched the tree layer unevenly and made trees lean
                float bankCover = 0;
                top = Mathf.Lerp(top, -1.88f, bankCover);
                float bottom = cliff ? -.44f : FieldBottom;   // misty pines: fog line (row 660) just behind the cobble horizon
                int k = j * 2;
                float u = forward ? t : 1 - t;
                vertices[k] = new Vector3(px, soil + bottom);
                vertices[k + 1] = new Vector3(px, soil + top);
                uv[k] = new Vector2(u, 0);
                uv[k + 1] = new Vector2(u, Mathf.Lerp(1, .64f, bankCover));
                colors[k] = colors[k + 1] = cliff ? Ink(255,255,255) : Ink(225,234,239);
                if (j < steps) Quad(triangles, j * 6, k, k + 2, k + 3, k + 1);
            }
            Surface(part.ToString(), cliff ? forest : near, cliff ? stone : path, cliff ? -84 : -63,
                vertices, uv, colors, triangles);
            if (cliff) FogSkirt("Fog under the far pines", forest, -84, x, width, -.33f, Ink(86,105,106), null);
            x += width;
            section++;
        }
    }

    // The misty pine art ends in a hard line just under its solid fog, so any dip in the cobble
    // edge showed sky below the trees. This hangs a plain band of the same fog colour under each
    // section, from just inside the art down well below the horizon.
    void FogSkirt(string name, Transform plane, int order, float x0, float width, float top, Color fog,
        System.Func<float, float> edgeAlpha, float lift = float.NaN)
    {
        const int columns = 24; const float depth = 2.6f;
        var v = new Vector3[(columns + 1) * 2]; var c = new Color[v.Length]; var tri = new int[columns * 6];
        for (int j = 0; j <= columns; j++)
        {
            float t = j / (float)columns, x = x0 + width * t;
            float baseY = float.IsNaN(lift) ? World.Height(x) : lift;   // follow the ground like the section above it
            int k = j * 2;
            v[k] = new Vector3(x, baseY + top - depth); v[k + 1] = new Vector3(x, baseY + top);
            var col = fog; col.a = edgeAlpha == null ? 1 : edgeAlpha(t);
            c[k] = c[k + 1] = col;
            if (j < columns) Quad(tri, j * 6, k, k + 2, k + 3, k + 1);
        }
        Surface(name, plane, paper, order, v, null, c, tri);
    }

    static void Quad(int[] indices, int start, int a, int b, int c, int d)
    {
        indices[start] = a; indices[start + 1] = b; indices[start + 2] = c;
        indices[start + 3] = a; indices[start + 4] = c; indices[start + 5] = d;
    }

    const float BankStart = -5.8f, BankWidth = 21.3f;

    // Floor art: the soil starts .72-.74 of the way up the texture, the grass verge
    // is painted above that. Mirrors the trail geometry built in Ribbon().
    const float SoilLip = .73f;
    // Field art (Forest/ForestFloor): the grass horizon is at row ~268 of 724 and the cliff
    // lip at row ~585. Mapped so the horizon lands .65 behind the back of the lane (World.MaxY)
    // and the lip .3 in front of the front (World.MinY), giving a deep, open walking field.
    public const float FieldTop = 3.23f, FieldBottom = -5.90f;
    public static float TrailEdge(float x) { return World.MaxY; }

    public static float BankCoverage(float x)
    {
        float edge = Mathf.Min(x - BankStart, BankStart + BankWidth - x);
        return Mathf.SmoothStep(0, 1, edge / 1.8f);
    }

    void PineBank()
    {
        // Big misty pines (Forest/PineBank, same art as layer 2 but larger) standing just behind the
        // cobble horizon along the whole level. Each section keeps the art's own 3:1 proportions and is
        // lifted by one terrain height for the whole section, so the trees stay upright instead of
        // leaning with the slope. Section edges fade out so the steps between sections read as mist.
        const float width = 14.4f, height = 4.8f, bottom = -1.2f, fade = .06f;
        const int columns = 24;
        int section = 0;
        for (float x0 = -9f; x0 < World.MaxX + 12; x0 += width, section++)
        {
            float lift = World.Height(x0 + width * .5f);
            bool flip = section % 2 == 1;
            var vertices = new Vector3[(columns + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[columns * 6];
            for (int c = 0; c <= columns; c++)
            {
                float t = c / (float)columns, x = x0 + width * t;
                float edge = Mathf.SmoothStep(0, 1, Mathf.Min(t, 1 - t) / fade);
                int k = c * 2;
                vertices[k] = new Vector3(x, lift + bottom);
                vertices[k + 1] = new Vector3(x, lift + bottom + height);
                uv[k] = new Vector2(flip ? 1 - t : t, 0); uv[k + 1] = new Vector2(flip ? 1 - t : t, 1);
                colors[k] = colors[k + 1] = Ink(235, 240, 242, edge);
                if (c < columns) Quad(triangles, c * 6, k, k + 2, k + 3, k + 1);
            }
            Surface("Misty pines behind the trail", pines, bankPaint, -65, vertices, uv, colors, triangles);
            FogSkirt("Fog under the big pines", pines, -65, x0, width, -1.03f, Ink(80,98,99), t => Mathf.SmoothStep(0, 1, Mathf.Min(t, 1 - t) / fade), lift);
        }
    }

    void Lantern(float x,float y,float size)
    {
        if (lanternSprite == null) return;   // no lantern art in the project
        int order = Art.YOrder(y);
        var shadow = Art.New("fx_soft", near, World.Project(new Vector2(x, y + .04f)), order - 1);
        var bounds = shadow.sprite.bounds.size;
        shadow.transform.localScale = new Vector3(.62f / bounds.x, .13f / bounds.y, 1);
        shadow.color = new Color(.07f, .08f, .09f, .64f);
        var post=Place("Wayside lantern", lanternSprite,near,x,y,size,order,new Color(.62f,.56f,.52f),false,"Default");   // dimmed: the panes smoulder rather than shine
        var anchor=new GameObject("Candle wick").transform; anchor.SetParent(post.transform,false);
        anchor.localPosition=new Vector2(0, post.sprite.bounds.size.y * .33f);   // centre of the lantern's glowing panes
        var glass = Keep(new Material(bark));
        glass.SetFloat("_UnlitGlass", 1);
        post.sharedMaterial = glass;
        anchor.localScale = Vector3.zero;
        var flame=Surface("Living flame",anchor,paper,order + 1,new Vector3[14],null,
            new[]{Ink(248,144,42),Ink(248,144,42),Ink(255,175,54),Ink(255,175,54),Ink(255,175,54),Ink(248,144,42),Ink(248,144,42),
            Ink(255,224,144),Ink(255,224,144),Ink(255,240,173),Ink(255,240,173),Ink(255,240,173),Ink(255,224,144),Ink(255,224,144)},
            new[]{0,1,2,0,2,3,0,3,4,0,4,5,0,5,6,7,8,9,7,9,10,7,10,11,7,11,12,7,12,13},"Default");
        flame.MarkDynamic(); flame.bounds=new Bounds(new Vector3(0,.14f,0),new Vector3(.55f,.7f,.02f));
        // A small glow hugging the glass and a hot pale core, so the lantern itself stays the
        // brightest point. The light it throws is done in the shaders (ForestInk, SpriteFlash):
        // the surroundings get lit, not the air. Placed in the trail plane (not under the
        // wick, whose scale animates) and faded in when the lantern catches.
        var halo = Art.New("fx_glow", near, Vector3.zero, order + 2, "Default", true);
        halo.transform.position = anchor.position; halo.transform.localScale = Vector3.one * .8f;
        halo.color = new Color(1f, .44f, .10f, 0);
        var core = Art.New("fx_glow", near, Vector3.zero, order + 3, "Default", true);
        core.transform.position = anchor.position + new Vector3(0, .05f, 0); core.transform.localScale = Vector3.one * .38f;
        core.color = new Color(1f, .93f, .64f, 0);
        var glowPool = Art.New("fx_soft", near, World.Project(new Vector2(x, y + .02f)), order - 2, "Default", true);
        var poolBounds = glowPool.sprite.bounds.size;
        glowPool.transform.localScale = new Vector3(1.5f / poolBounds.x, .32f / poolBounds.y, 1);
        glowPool.color = new Color(1f, .48f, .14f, .09f);   // faint, steady warmth on the stone under the lantern
        lamps.Add(new Lamp{glassScale=.05f / post.transform.localScale.x / .31f,glass=glass,wick=anchor,flame=flame,phase=x*.53f,halo=halo,core=core});
    }

    void Mist(float x,float y,float width,float height,float tilt)
    {
        var root=Plane("Mist caught behind the trees",.40f,false,x*.36f+1);   // drifts with the big pines (was .09)
        const int steps=20; var v=new Vector3[(steps+1)*3]; var colors=new Color[v.Length]; var tri=new int[steps*12];
        for(int i=0;i<=steps;i++)
        {
            float t=i/(float)steps, px=x+(t-.5f)*width, weight=Mathf.Sin(t*Mathf.PI);
            float mid=World.Height(px)+y+tilt*t+.09f*Mathf.Sin(t*7);
            int k=i*3; v[k]=new Vector2(px,mid-height*weight); v[k+1]=new Vector2(px,mid); v[k+2]=new Vector2(px,mid+height*weight*.72f);
            colors[k]=colors[k+2]=Ink(105,155,164,0); colors[k+1]=Ink(105,155,164,.049f*weight);
            if(i==steps) continue;
            int q=i*12; for(int j=0;j<2;j++){int n=k+j;tri[q++]=n;tri[q++]=n+3;tri[q++]=n+4;tri[q++]=n;tri[q++]=n+4;tri[q++]=n+1;}
        }
        Surface("Low mist",root,paper,-71,v,null,colors,tri);
    }

    void Fireflies(float x,float y,int count)
    {
        for(int i=0;i<count;i++)
        {
            float phase=x*.86f+i*2.71f;
            Vector2 home=World.Project(new Vector2(x+i*.81f,y+.18f*Mathf.Sin(phase)));
            var sr=Art.New("fx_soft",near,home,-45,"Background",true);
            sr.transform.localScale=Vector3.one*(.069f+i*.012f);
            motes.Add(new Mote{sprite=sr,home=home,phase=phase});
        }
    }

    void Opening()
    {
        PineBank();
        // Lit box lanterns along both edges of the cobblestone platform: in front of the lane and at its back.
        // Sparse and uneven on purpose: long dark stretches between them.
        float[] frontX = { 6.5f, 19.0f, 27.5f, 49.0f, 63.5f, 88.0f, 101.5f, 127.0f };
        for (int i = 0; i < frontX.Length; i++) Lantern(frontX[i], -3.55f, 1.05f + .06f * Mathf.Sin(i * 2.3f));   // set in on the stone, not on the lip
        float[] backX = { 12.0f, 38.5f, 56.0f, 76.5f, 115.0f, 134.0f };
        for (int i = 0; i < backX.Length; i++) Lantern(backX[i], -.74f, .98f + .05f * Mathf.Sin(i * 1.7f));
        Fireflies(11.3f,.73f,2); Fireflies(22.9f,1.18f,1);
        Mist(3,1.33f,10.2f,.49f,.12f); Mist(22,1.72f,13,.61f,-.23f);
    }

    public static void IgniteVisibleLanterns()
    {
        if (active == null || CameraRig.I == null) return;
        foreach (var lamp in active.lamps)
        {
            Vector3 view = CameraRig.I.cam.WorldToViewportPoint(lamp.wick.position);
            if (lamp.ignition < 0 && view.z > 0 && view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1)
                lamp.ignition = Time.time;
        }
    }

    void LateUpdate()
    {
        if (CameraRig.I==null) return;
        var camera=CameraRig.I.transform.position; float time=Time.time;
        foreach(var p in planes)
        {
            float dx=camera.x*p.parallax;
            float y=p.sky?camera.y:World.Height(camera.x)-World.Height(camera.x-dx);
            float drift=p.mist==0?0:Mathf.Sin(time*.12f+p.mist)*.34f;
            p.root.localPosition=new Vector3(dx+drift,y,0);
        }
        float reach=CameraRig.I.HalfW+4;
        foreach(var mote in motes)
        {
            if(Mathf.Abs(mote.home.x-camera.x)>reach) continue;
            float t=time*.53f+mote.phase;
            mote.sprite.transform.localPosition=mote.home+new Vector3(.31f*Mathf.Sin(t)+.12f*Mathf.Sin(t*1.57f),.19f*Mathf.Cos(t*.83f));
            float pulse=Mathf.Clamp01((Mathf.Sin(t*.83f)+.28f)*1.12f);
            mote.sprite.color=new Color(.67f,.87f,.64f,pulse*.68f);
        }
        Vector4 light=Vector4.zero, pool=Vector4.zero; float nearest=reach+2;
        foreach(var lamp in lamps)
        {
            var wick=lamp.wick.position; float distance=Mathf.Abs(wick.x-camera.x);
            if(lamp.ignition < 0) continue;   // every lamp burns all the time, near or far
            float catchLight = Mathf.SmoothStep(0, 1, (time - lamp.ignition) / .65f);
            lamp.wick.localScale = Vector3.one * (lamp.glassScale * catchLight);
            lamp.glass.SetFloat("_UnlitGlass", 1 - catchLight);
            float t=time+lamp.phase;
            AnimateFlame(lamp.flame,t);
            // Candle flicker: two layers of noise (a slow breathing and a quicker shiver), and now
            // and then a brief dip as if a draught caught the flame. Drives glow and thrown light together.
            float slow = Mathf.PerlinNoise(t * 1.7f, lamp.phase), quick = Mathf.PerlinNoise(t * 7.3f, lamp.phase + 4.1f);
            float draught = Mathf.Pow(Mathf.PerlinNoise(t * .55f, lamp.phase + 9.3f), 5f);
            float flicker = .84f + .12f * slow + .08f * quick - .22f * draught;
            lamp.halo.color = new Color(1f, .42f + .05f * quick, .10f, .13f * catchLight * flicker);   // dark fantasy: a weak, guttering glow
            lamp.halo.transform.localScale = Vector3.one * (.6f + .05f * slow);
            lamp.core.color = new Color(1f, .82f, .52f, .32f * catchLight * flicker);
            if (catchLight > .5f && distance < reach && Random.value < Time.deltaTime * 1.2f)
                Fx.Emit("fx_soft", World.Unproject(wick) + new Vector2(Random.Range(-.08f,.08f), .15f), new Vector2(Random.Range(-.25f,.25f), Random.Range(.5f,1.1f)),
                    Random.Range(.8f,1.4f), .09f, .02f, new Color(1f,.8f,.3f), new Color(1f,.3f,.1f,0), 0, 0, 0, true);
            // No shader light from lanterns: the shader can only light from one source (the nearest),
            // which made each lantern and the knight brighten as you walked up. Each lantern instead
            // carries its own small, steady glow on the stone (see Lantern), the same near or far.
        }
        if (campfire==null && World.shrine!=null) campfire=World.shrine.GetComponent<EndCampfire>();
        if (campfire!=null && campfire.IsLit && Mathf.Abs(World.CampX-camera.x)<nearest)
        {
            Vector2 basePos=World.Project(new Vector2(World.CampX,-1.55f));
            light=new Vector4(basePos.x,basePos.y+.57f,1.35f+.10f*Mathf.Sin(time*6.4f),0);
            pool=new Vector4(basePos.x,basePos.y-.33f,0,0);
        }
        Shader.SetGlobalVector(Wick,light); Shader.SetGlobalVector(Pool,pool);
    }

    void AnimateFlame(Mesh mesh,float t)
    {
        float lean=.037f*Mathf.Sin(t*6.2f)+.023f*Mathf.Sin(t*12.4f+.7f);
        float h=.31f+.045f*Mathf.Sin(t*8.6f)+.02f*Mathf.Sin(t*14.7f);
        for(int j=0;j<2;j++)
        {
            int k=j*7; float size=j==0?1:.56f; float shift=j*.012f;
            flamePoints[k]=new Vector2(shift,.028f);
            flamePoints[k+1]=new Vector2(-.072f*size,.078f*size);
            flamePoints[k+2]=new Vector2(-.052f*size+lean*.2f,h*.64f*size);
            flamePoints[k+3]=new Vector2((lean+.021f)*size,h*size);
            flamePoints[k+4]=new Vector2(.035f*size+lean*.43f,h*.53f*size);
            flamePoints[k+5]=new Vector2(.072f*size,.104f*size);
            flamePoints[k+6]=new Vector2(.052f*size,.042f*size);
        }
        mesh.vertices=flamePoints;
    }

    void OnDestroy()
    {
        Shader.SetGlobalVector(Wick, Vector4.zero);
        foreach(var asset in owned) if(asset!=null) Destroy(asset);
    }
}
