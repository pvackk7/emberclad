using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(205)]
public sealed class ForestScenery : MonoBehaviour
{
    readonly List<Object> owned = new List<Object>();
    readonly List<MovingPlane> planes = new List<MovingPlane>();
    readonly List<MovingSprig> sprigs = new List<MovingSprig>();
    readonly List<Lamp> lamps = new List<Lamp>();
    readonly List<Mote> motes = new List<Mote>();
    Material paper, bark, treePaint, stone, path, grass, moonPaint;
    Sprite[] trees, details;
    Transform sky, forest, near, front;
    EndCampfire campfire;
    static readonly int Wick = Shader.PropertyToID("_ForestWick");
    static readonly int Pool = Shader.PropertyToID("_ForestPool");

    struct MovingPlane { public Transform root; public float parallax, mist; public bool sky; }
    struct MovingSprig { public Transform root; public float angle, speed, phase, amplitude; }
    struct Lamp { public Transform wick; public Mesh flame; public float phase; }
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
        root.AddComponent<ForestScenery>().Illustrate();
    }

    Transform Plane(string name, float parallax, bool night = false, float mist = 0)
    {
        var root = new GameObject(name).transform;
        root.SetParent(transform, false);
        planes.Add(new MovingPlane { root = root, parallax = parallax, sky = night, mist = mist });
        return root;
    }

    Sprite[] CutSheet(string name, RectInt[] cells)
    {
        var tex = Resources.Load<Texture2D>("Forest/" + name);
        var result = new Sprite[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            var rect = new Rect(c.x * tex.width / 1536f, (1024 - c.yMax) * tex.height / 1024f,
                c.width * tex.width / 1536f, c.height * tex.height / 1024f);
            Vector2 pivot = name == "TrailDetails" && i == 0 ? new Vector2(.035f,.52f) : new Vector2(.5f,0);
            result[i] = Keep(Sprite.Create(tex, rect, pivot, 100, 0, SpriteMeshType.FullRect));
            result[i].name = name + " " + i;
        }
        return result;
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
        treePaint = Paint("Forest/AncientTrees", true);
        stone = Paint("Forest/CanyonWall");
        path = Paint("Forest/ForestFloor", true);
        grass = Paint("Forest/ForestFloor", true);
        grass.SetFloat("_GrassOnly", 1);
        moonPaint = Paint("Menu/TitleBackground"); moonPaint.SetFloat("_Moon", 1);
        trees = CutSheet("AncientTrees", new[] {
            new RectInt(2,2,507,496),new RectInt(515,1,507,493),new RectInt(1027,6,507,489),
            new RectInt(2,512,508,509),new RectInt(514,512,507,506) });
        details = CutSheet("TrailDetails", new[] {
            new RectInt(9,108,503,294),new RectInt(532,146,477,329),new RectInt(1149,6,305,493),
            new RectInt(8,752,498,238),new RectInt(575,515,343,484),new RectInt(1025,686,506,297) });
        sky = Plane("Night beyond the canopy", .994f, true);
        forest = Plane("Wooded canyon wall", .135f);
        near = Plane("Roots and ruins", 0);
        front = Plane("Near lip of the trail", -.018f);
        Night();
        Ribbon(GroundPart.Cliff);
        Ribbon(GroundPart.Bank);
        Ribbon(GroundPart.Trail);
        Opening();
    }

    void Night()
    {
        Surface("Blue hour",sky,paper,-100,new[]{new Vector3(-100,-8),new Vector3(100,-8),new Vector3(100,8),new Vector3(-100,8)},
            null,new[]{Ink(43,73,86),Ink(43,73,86),Ink(12,17,37),Ink(12,17,37)},new[]{0,1,2,0,2,3});
        var vertices = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
        var random = new System.Random(2815);
        for (int i = 0; i < 380; i++)
        {
            float x = (float)random.NextDouble() * 92 - 45, y = (float)random.NextDouble() * 7.5f - 1;
            float r = .007f + (float)random.NextDouble() * .011f;
            Color c = Ink(164,204,216,.28f + (float)random.NextDouble() * .39f);
            int k = vertices.Count;
            vertices.Add(new Vector2(x-r,y)); vertices.Add(new Vector2(x,y+r*1.22f));
            vertices.Add(new Vector2(x+r,y)); vertices.Add(new Vector2(x,y-r*1.22f));
            for(int j=0;j<4;j++) colors.Add(c);
            indices.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
        }
        Surface("Distant stars",sky,paper,-99,vertices.ToArray(),null,colors.ToArray(),indices.ToArray());
        Vector2 center = new Vector2(2.42f,2.64f);
        Disc("Moonlit air",center,new[]{.05f,.83f,1.12f,1.52f,2.0f},new[]{.25f,.20f,.09f,.024f,0},false);
        Disc("Cratered moon",center,new[]{.025f,.79f,.875f,.916f,.95f},new[]{1f,1f,1f,.78f,0},true);
    }

    void Disc(string name, Vector2 center, float[] rings, float[] alpha, bool lunar)
    {
        const int slices = 64;
        var v = new Vector3[rings.Length*slices+1]; var uv = new Vector2[v.Length]; var c = new Color[v.Length];
        var tri = new List<int>(); var texture = moonPaint.mainTexture;
        Vector2 texel = new Vector2(1f/texture.width,1f/texture.height);
        Vector2 lunarCenter = new Vector2(1303f/1672f,702f/941f);
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

    enum GroundPart { Cliff, Bank, Trail }

    void Ribbon(GroundPart part)
    {
        bool cliff = part == GroundPart.Cliff;
        bool bank = part == GroundPart.Bank;
        float x = cliff ? -25.5f : bank ? -22.8f : -16.2f;
        float[] widths = cliff ? new[] { 32f, 32f, 38f, 29f, 35f } : new[] { 22.7f, 26.4f, 24.6f, 29.8f, 23.3f };
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
                float top = cliff ? 5.52f + .64f * Mathf.Sin(px * .103f) :
                    bank ? .74f + .17f * Mathf.Sin(px * .38f) : -.12f + .095f * Mathf.Sin(px * .46f);
                float bottom = cliff ? -3.3f : bank ? -3.15f : -6.35f;
                int k = j * 2;
                float u = forward ? t : 1 - t;
                vertices[k] = new Vector3(px, soil + bottom);
                vertices[k + 1] = new Vector3(px, soil + top);
                uv[k] = new Vector2(u, bank ? .52f : 0);
                uv[k + 1] = new Vector2(u, 1);
                colors[k] = colors[k + 1] = cliff ? Ink(176,201,217) : bank ? Ink(133,157,151) : Ink(225,234,239);
                if (j < steps) Quad(triangles, j * 6, k, k + 2, k + 3, k + 1);
            }
            var mesh = Surface(part.ToString(), cliff ? forest : near, cliff ? stone : path, cliff ? -84 : bank ? -68 : -63,
                vertices, uv, colors, triangles);
            if (part == GroundPart.Trail)
            {
                var tufts = new GameObject("Grass at the root line");
                tufts.transform.SetParent(near, false);
                tufts.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = tufts.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = grass;
                renderer.sortingLayerName = "Background";
                renderer.sortingOrder = -48;
            }
            x += width;
            section++;
        }
    }

    static void Quad(int[] indices, int start, int a, int b, int c, int d)
    {
        indices[start] = a; indices[start + 1] = b; indices[start + 2] = c;
        indices[start + 3] = a; indices[start + 4] = c; indices[start + 5] = d;
    }

    void Tree(int kind, float x, float y, float height, bool turn = false, bool withBough = false)
    {
        var sprite = trees[kind];
        Rect rect = sprite.rect;
        float width = height * rect.width / rect.height;
        float baseY = y - .71f;
        const int columns = 18;
        float[] levels = { 0, .10f, .28f, .55f, 1 };
        var vertices = new Vector3[(columns + 1) * levels.Length];
        var colors = new Color[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[columns * (levels.Length - 1) * 6];
        for (int row = 0; row < levels.Length; row++)
        for (int column = 0; column <= columns; column++)
        {
            float u = column / (float)columns;
            float v = levels[row];
            float px = x + (u - .5f) * width;
            float rooted = 1 - Mathf.SmoothStep(0, 1, v / .34f);
            float ground = Mathf.Lerp(World.Height(x), World.Height(px), rooted);
            int k = row * (columns + 1) + column;
            vertices[k] = new Vector3(px, ground + baseY + v * height);
            uv[k] = new Vector2((rect.x + (turn ? 1 - u : u) * rect.width) / sprite.texture.width,
                (rect.y + v * rect.height) / sprite.texture.height);
            colors[k] = Color.Lerp(Ink(181,194,188), Color.white, Mathf.SmoothStep(0,1,v / .20f));
            if (row + 1 < levels.Length && column < columns)
                Quad(triangles, (row * columns + column) * 6, k, k + 1, k + columns + 2, k + columns + 1);
        }
        Surface(sprite.name, near, treePaint, -52, vertices, uv, colors, triangles);
        if (!withBough) return;
        float offset = turn ? .87f : -.87f;
        var twig = Place("Oak bough", details[0], near, x + offset, baseY + height * .48f, height * .17f, -53,
            new Color(.64f,.74f,.70f), turn);
        sprigs.Add(new MovingSprig { root = twig.transform, angle = turn ? 12 : -9, speed = .63f, phase = x, amplitude = .37f });
    }

    void Fern(float x,float y,float height,bool flank=false,bool flip=false)
    {
        var sr=Place("Bracken",details[1],flank?front:near,x,y,height,flank?4:-42,
            flank?new Color(.34f,.46f,.49f):new Color(.58f,.71f,.70f),flip,flank?"Foreground":"Background");
        sprigs.Add(new MovingSprig { root=sr.transform, angle=flip?-5:3,speed=.73f,phase=x*.37f,amplitude=.56f });
    }

    void Lantern(float x,float y,float size)
    {
        var post=Place("Wayside lantern", details[2],near,x,y,size,-40,Color.white);
        var anchor=new GameObject("Candle wick").transform; anchor.SetParent(post.transform,false);
        anchor.localPosition=new Vector2(.96f,3.095f);
        var flame=Surface("Living flame",anchor,paper,-39,new Vector3[14],null,
            new[]{Ink(248,144,42),Ink(248,144,42),Ink(255,175,54),Ink(255,175,54),Ink(255,175,54),Ink(248,144,42),Ink(248,144,42),
            Ink(255,224,144),Ink(255,224,144),Ink(255,240,173),Ink(255,240,173),Ink(255,240,173),Ink(255,224,144),Ink(255,224,144)},
            new[]{0,1,2,0,2,3,0,3,4,0,4,5,0,5,6,7,8,9,7,9,10,7,10,11,7,11,12,7,12,13});
        flame.MarkDynamic(); flame.bounds=new Bounds(new Vector3(0,.14f,0),new Vector3(.55f,.7f,.02f));
        lamps.Add(new Lamp{wick=anchor,flame=flame,phase=x*.53f});
    }

    void Mist(float x,float y,float width,float height,float tilt)
    {
        var root=Plane("Mist caught behind the trees",.09f,false,x*.36f+1);
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
        Tree(0,-2.3f,-1.52f,9.7f,false,true);
        Tree(2,8.8f,-1.58f,7.85f);
        Tree(1,13.1f,-1.34f,9.35f,false,true);
        Tree(4,25.7f,-1.59f,10.6f);
        Fern(12.2f,-1.99f,.54f,false,true);
        Fern(-1.4f,-1.72f,.70f); Fern(8.7f,-5.05f,1.22f,true,true); Fern(24.8f,-1.68f,.69f);
        Lantern(5.85f,-1.64f,2.24f);
        Fireflies(11.3f,-.42f,2); Fireflies(22.9f,.03f,1);
        Mist(3,.18f,10.2f,.49f,.12f); Mist(22,.57f,13,.61f,-.23f);
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
        foreach(var s in sprigs)
            if(Mathf.Abs(s.root.position.x-camera.x)<reach)
                s.root.localRotation=Quaternion.Euler(0,0,s.angle+s.amplitude*(Mathf.Sin(time*s.speed+s.phase)+.32f*Mathf.Sin(time*1.3f+s.phase)));
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
            if(distance>reach+2) continue;
            float t=time+lamp.phase;
            AnimateFlame(lamp.flame,t);
            if(distance>=nearest) continue;
            nearest=distance; light=new Vector4(wick.x,wick.y,.87f+.075f*Mathf.Sin(t*6)+.045f*Mathf.Sin(t*11.7f),0);
            pool=new Vector4(wick.x+.22f,World.Height(wick.x)-2.0f,0,0);
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
