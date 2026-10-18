using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = DeliveryDash.EditorTools.DeliveryDashBuilder;

namespace DeliveryDash.EditorTools
{
    /// <summary>
    /// Builds every prefab: houses, shops, street furniture, cars, people, the dog, the cyclist and
    /// the newspaper. Houses are built for the right-hand side of the street (+X away from the
    /// road, the street running along +Z); the left side uses the same prefabs turned round.
    /// </summary>
    public static class Prefabs
    {
        public const float HouseWidth = 5.5f;
        public const float GardenDepth = 4.0f;
        public const float DoorZ = 3.95f;

        public static GameObject[] Houses, Shops, Trees, Bins, Cars, People;
        public static GameObject Garage, LampPost, PostBox, Bench, Bollard, Cone, Dog, ReversingCar, Pothole, Cyclist, Newspaper, Railing, Beacon, StartArch, FinishArch, BackTree;

        // ------------------------------------------------------------------ helpers

        static GameObject Part(string name, Transform parent, MeshKit kit, string meshName, int layer = 0, bool shadows = true)
        {
            var (mesh, mats) = kit.Build(meshName);
            mesh = B.SaveMesh(mesh, meshName);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.layer = layer;
            return go;
        }

        static GameObject Pivot(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go;
        }

        static BoxCollider Box(Transform parent, string name, Vector3 centre, Vector3 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            var c = go.AddComponent<BoxCollider>();
            c.center = centre;
            c.size = size;
            return c;
        }

        static GameObject Save(GameObject go, string name)
        {
            string path = B.PrefabDir + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static GameObject Warning(Transform parent, float height)
        {
            var w = Pivot("Warning", parent, new Vector3(0, height, 0));
            var sign = w.AddComponent<WarningSign>();
            var kit = new MeshKit();
            kit.Plane(Mats.Warning, Vector3.zero, new Vector2(0.9f, 0.9f), Vector3.back, Vector3.up);
            var vis = Part("Sign", w.transform, kit, "WarningSign", 0, false);
            sign.Visual = vis.transform;
            return w;
        }

        public static void MakeAll()
        {
            Newspaper = MakeNewspaper();
            Houses = new GameObject[10];
            for (int i = 0; i < Houses.Length; i++) Houses[i] = MakeHouse(i);
            Garage = MakeGarage();
            Shops = new[] { MakeShop(0, "PATEL'S NEWS", Mats.Awning1), MakeShop(1, "THE DAILY GRIND", Mats.Awning2), MakeShop(2, "SUDS LAUNDERETTE", Mats.Awning3) };
            LampPost = MakeLampPost();
            Trees = new[] { MakeTree(0, Mats.Leaves, true), MakeTree(1, Mats.Leaves, true), MakeTree(2, Mats.LeavesAutumn, true) };
            BackTree = MakeTree(3, Mats.Leaves, false, 1.6f);
            Bins = new[] { MakeBin(0, Mats.Lit("BinGreen", new Color(0.18f, 0.45f, 0.26f), null, 0.35f)), MakeBin(1, Mats.Lit("BinBlack", new Color(0.12f, 0.12f, 0.14f), null, 0.35f)), MakeBin(2, Mats.Lit("BinBlue", new Color(0.2f, 0.36f, 0.7f), null, 0.35f)) };
            PostBox = MakePostBox();
            Bench = MakeBench();
            Bollard = MakeBollard();
            Cone = MakeCone();
            Cars = new GameObject[Mats.CarPaints.Length];
            for (int i = 0; i < Cars.Length; i++) Cars[i] = MakeCar(i, false);
            ReversingCar = MakeCar(1, true);
            Dog = MakeDog();
            People = new[]
            {
                MakePerson(0, Mats.Cloth1, Mats.Cloth5, Mats.Skin1, Mats.Hair1, false), MakePerson(1, Mats.Cloth2, Mats.Trousers, Mats.Skin2, Mats.Hair2, true),
                MakePerson(2, Mats.Cloth3, Mats.Cloth5, Mats.Skin3, Mats.Hair1, false), MakePerson(3, Mats.Cloth4, Mats.Trousers, Mats.Skin1, Mats.Hair3, true),
            };
            Pothole = MakePothole();
            Railing = MakeRailing();
            Beacon = MakeBeacon();
            StartArch = MakeArch("Start", "START");
            FinishArch = MakeArch("Finish", "FINISH");
            Cyclist = MakeCyclist();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ newspaper

        static GameObject MakeNewspaper()
        {
            var root = new GameObject("Newspaper");
            var kit = new MeshKit();
            kit.Cylinder(Mats.Newsprint, new Vector3(-0.17f, 0, 0), new Vector3(0.17f, 0, 0), 0.065f, 10);
            kit.Cylinder(Mats.NewsBand, new Vector3(-0.03f, 0, 0), new Vector3(0.03f, 0, 0), 0.068f, 10);
            Part("Roll", root.transform, kit, "Newspaper", B.Paper);
            root.layer = B.Paper;
            var p = root.AddComponent<Newspaper>();
            p.HitMask = (1 << B.Ground) | (1 << B.Building) | (1 << B.Window) | (1 << B.LetterboxLayer) | (1 << B.Obstacle) | (1 << B.Character);
            return Save(root, "Newspaper");
        }

        // ------------------------------------------------------------------ terraced house

        static GameObject MakeHouse(int v)
        {
            float W = HouseWidth;
            var wall = Mats.WallSet[v % Mats.WallSet.Length];
            var door = Mats.Doors[(v * 3) % Mats.Doors.Length];
            var roof = v % 4 == 3 ? Mats.Tile : v % 2 == 0 ? Mats.Slate : Mats.SlateDark;
            bool bay = v % 3 != 2;
            bool brickWall = wall == Mats.BrickRed || wall == Mats.BrickYellow || wall == Mats.BrickBrown;
            float brickUv = 1f / 1.2f;

            var root = new GameObject($"House {v}");
            var house = root.AddComponent<House>();

            // ---- body: garden, walls, facade, roof
            var k = new MeshKit();
            k.Box(Mats.Grass, new Vector3(2.15f, 0.075f, W / 2), new Vector3(3.7f, 0.15f, W), 0.6f);
            k.Box(Mats.Path, new Vector3(2.15f, 0.08f, DoorZ), new Vector3(3.7f, 0.16f, 0.95f), 1f / 1.2f);
            // low front wall either side of the gate, with coping and gate pillars
            var frontWall = brickWall ? wall : Mats.BrickRed;
            k.Box(frontWall, new Vector3(0.15f, 0.35f, (DoorZ - 0.55f) / 2), new Vector3(0.3f, 0.7f, DoorZ - 0.55f), brickUv);
            k.Box(frontWall, new Vector3(0.15f, 0.35f, (DoorZ + 0.55f + W) / 2), new Vector3(0.3f, 0.7f, W - DoorZ - 0.55f), brickUv);
            k.Box(Mats.Coping, new Vector3(0.15f, 0.73f, (DoorZ - 0.55f) / 2), new Vector3(0.38f, 0.06f, DoorZ - 0.55f));
            k.Box(Mats.Coping, new Vector3(0.15f, 0.73f, (DoorZ + 0.55f + W) / 2), new Vector3(0.38f, 0.06f, W - DoorZ - 0.55f));
            foreach (float pz in new[] { DoorZ - 0.6f, DoorZ + 0.6f })
            {
                k.Box(frontWall, new Vector3(0.15f, 0.45f, pz), new Vector3(0.4f, 0.9f, 0.3f), brickUv);
                k.Box(Mats.Coping, new Vector3(0.15f, 0.93f, pz), new Vector3(0.46f, 0.06f, 0.36f));
            }
            // facade and house body
            k.Box(wall, new Vector3(8f, 3.1f, W / 2), new Vector3(8f, 6.2f, W), brickUv);
            // plinth and string course
            k.Box(Mats.Stone, new Vector3(3.98f, 0.25f, W / 2), new Vector3(0.06f, 0.5f, W));
            k.Box(Mats.Trim, new Vector3(3.97f, 3.0f, W / 2), new Vector3(0.08f, 0.12f, W));
            // eaves and gutter
            k.Box(Mats.Trim, new Vector3(3.9f, 6.15f, W / 2), new Vector3(0.3f, 0.22f, W + 0.02f));
            k.Box(Mats.Black, new Vector3(3.75f, 6.05f, W / 2), new Vector3(0.12f, 0.1f, W + 0.02f));
            k.Box(Mats.Black, new Vector3(3.92f, 3.1f, 0.15f), new Vector3(0.08f, 6.0f, 0.08f));   // downpipe
            k.GableRoof(roof, wall, 4f, 12f, 0f, W, 6.2f, 2.4f, 0.35f);
            // bay window
            if (bay)
            {
                k.Box(wall, new Vector3(3.6f, 1.4f, 1.85f), new Vector3(0.75f, 2.5f, 2.3f), brickUv);
                k.Box(Mats.Trim, new Vector3(3.6f, 2.7f, 1.85f), new Vector3(0.85f, 0.14f, 2.4f));
                k.Box(roof, new Vector3(3.6f, 2.86f, 1.85f), new Vector3(0.95f, 0.18f, 2.5f), 1f);
                k.Box(Mats.Sill, new Vector3(3.2f, 0.88f, 1.85f), new Vector3(0.12f, 0.08f, 2.0f));
                FrameBox(k, new Vector3(3.22f, 1.6f, 1.85f), 1.8f, 1.35f, true, 3);
            }
            else
            {
                k.Box(Mats.Sill, new Vector3(3.92f, 0.88f, 1.85f), new Vector3(0.16f, 0.08f, 1.8f));
                FrameBox(k, new Vector3(3.97f, 1.6f, 1.85f), 1.6f, 1.35f, true, 2);
            }
            // upstairs windows
            foreach (float wz in new[] { 1.75f, 4.15f })
            {
                k.Box(Mats.Sill, new Vector3(3.92f, 3.42f, wz), new Vector3(0.16f, 0.08f, 1.15f));
                k.Box(Mats.Stone, new Vector3(3.97f, 4.95f, wz), new Vector3(0.06f, 0.16f, 1.2f));
                FrameBox(k, new Vector3(3.97f, 4.15f, wz), 1.0f, 1.4f, false, 2);
            }
            // front door, frame, fanlight, step
            k.Box(door, new Vector3(3.96f, 1.38f, DoorZ), new Vector3(0.06f, 2.05f, 0.95f));
            k.Box(Mats.Trim, new Vector3(3.95f, 2.48f, DoorZ), new Vector3(0.1f, 0.1f, 1.15f));
            k.Box(Mats.Trim, new Vector3(3.95f, 1.45f, DoorZ - 0.53f), new Vector3(0.1f, 2.2f, 0.1f));
            k.Box(Mats.Trim, new Vector3(3.95f, 1.45f, DoorZ + 0.53f), new Vector3(0.1f, 2.2f, 0.1f));
            k.Box(Mats.Glass, new Vector3(3.97f, 2.68f, DoorZ), new Vector3(0.04f, 0.3f, 0.95f));
            k.Box(Mats.Trim, new Vector3(3.95f, 2.86f, DoorZ), new Vector3(0.1f, 0.08f, 1.15f));
            k.Box(Mats.Brass, new Vector3(3.92f, 1.05f, DoorZ), new Vector3(0.03f, 0.07f, 0.3f));   // letterbox plate
            k.Box(Mats.Brass, new Vector3(3.92f, 1.55f, DoorZ - 0.33f), new Vector3(0.04f, 0.06f, 0.06f)); // knob
            k.Box(Mats.Stone, new Vector3(3.78f, 0.24f, DoorZ), new Vector3(0.45f, 0.18f, 1.2f), 1f / 1.2f);
            Part("Body", root.transform, k, $"HouseBody{v}", B.Building);

            // ---- details (culled at a distance): hedge, flowers, gate, chimney
            var d = new MeshKit();
            d.Box(Mats.Hedge, new Vector3(2.0f, 0.38f, 0.18f), new Vector3(3.3f, 0.76f, 0.34f), 1.2f);
            for (int i = 0; i < 5; i++) d.Sphere(Mats.Hedge, new Vector3(0.6f + i * 0.72f, 0.78f, 0.18f), new Vector3(0.42f, 0.18f, 0.22f), 8, 5);
            var flowerMats = new[] { Mats.Flower1, Mats.Flower2, Mats.Flower3, Mats.Flower4 };
            var rnd = new System.Random(100 + v);
            d.Box(Mats.Soil, new Vector3(0.62f, 0.17f, 1.9f), new Vector3(0.55f, 0.06f, 2.6f));
            for (int i = 0; i < 9; i++)
            {
                float fz = 0.8f + i * 0.28f, fx = 0.5f + (float)rnd.NextDouble() * 0.25f;
                d.Sphere(Mats.Hedge, new Vector3(fx, 0.3f, fz), new Vector3(0.15f, 0.13f, 0.15f), 6, 4);
                d.Sphere(flowerMats[(i + v) % 4], new Vector3(fx, 0.42f, fz), new Vector3(0.08f, 0.07f, 0.08f), 6, 4);
            }
            d.Sphere(Mats.Hedge, new Vector3(3.2f, 0.55f, 5.0f), new Vector3(0.45f, 0.42f, 0.4f), 10, 6);
            // open gate
            d.BoxRotated(v % 2 == 0 ? Mats.Black : Mats.WoodPale, new Vector3(0.55f, 0.45f, DoorZ - 0.6f), new Vector3(0.75f, 0.7f, 0.05f), Quaternion.Euler(0, 75f, 0));
            // chimney on the party wall
            d.Box(wall, new Vector3(8f, 8.3f, 0f), new Vector3(1.0f, 1.8f, 0.7f), brickUv);
            d.Box(Mats.Coping, new Vector3(8f, 9.25f, 0f), new Vector3(1.1f, 0.12f, 0.8f));
            d.Cylinder(Mats.Tile, new Vector3(7.75f, 9.3f, 0f), new Vector3(7.75f, 9.7f, 0f), 0.11f, 8);
            d.Cylinder(Mats.Tile, new Vector3(8.25f, 9.3f, 0f), new Vector3(8.25f, 9.65f, 0f), 0.11f, 8);
            if (v % 2 == 1) for (int i = 0; i < 4; i++) d.Sphere(flowerMats[(i + 1) % 4], new Vector3(3.82f, 3.55f, 3.75f + i * 0.25f), new Vector3(0.09f, 0.08f, 0.09f), 6, 4);
            var details = Part("Details", root.transform, d, $"HouseDetails{v}", B.Building, false);

            // ---- glass (breakable)
            var windows = new List<Renderer>();
            void Pane(string name, Vector3 c, float w, float h)
            {
                var gk = new MeshKit();
                gk.Box(Mats.Glass, c, new Vector3(0.03f, h, w));
                var g = Part(name, root.transform, gk, $"HouseGlass{v}_{name}", B.Window, false);
                var col = g.AddComponent<BoxCollider>();
                col.center = c; col.size = new Vector3(0.12f, h, w);
                var bw = g.AddComponent<BreakableWindow>();
                bw.Glass = g.GetComponent<MeshRenderer>();
                bw.BrokenMaterial = Mats.GlassBroken;
                windows.Add(bw.Glass);
            }
            if (bay) Pane("BayGlass", new Vector3(3.21f, 1.6f, 1.85f), 1.8f, 1.35f);
            else Pane("FrontGlass", new Vector3(3.96f, 1.6f, 1.85f), 1.6f, 1.35f);
            Pane("UpGlassA", new Vector3(3.96f, 4.15f, 1.75f), 1.0f, 1.4f);
            Pane("UpGlassB", new Vector3(3.96f, 4.15f, 4.15f), 1.0f, 1.4f);

            // ---- colliders
            Box(root.transform, "GardenGround", new Vector3(2.15f, 0.075f, W / 2), new Vector3(3.7f, 0.15f, W), B.Ground);
            Box(root.transform, "Doorstep", new Vector3(3.78f, 0.24f, DoorZ), new Vector3(0.45f, 0.18f, 1.2f), B.Ground);
            Box(root.transform, "Facade", new Vector3(8f, 3.1f, W / 2), new Vector3(8f, 6.2f, W), B.Building);
            Box(root.transform, "Roof", new Vector3(8f, 7.4f, W / 2), new Vector3(8.4f, 2.4f, W), B.Building);
            Box(root.transform, "FrontWallA", new Vector3(0.15f, 0.36f, (DoorZ - 0.55f) / 2), new Vector3(0.36f, 0.76f, DoorZ - 0.55f), B.Building);
            Box(root.transform, "FrontWallB", new Vector3(0.15f, 0.36f, (DoorZ + 0.55f + W) / 2), new Vector3(0.36f, 0.76f, W - DoorZ - 0.55f), B.Building);
            Box(root.transform, "Hedge", new Vector3(2.0f, 0.4f, 0.18f), new Vector3(3.3f, 0.8f, 0.34f), B.Building);
            if (bay) Box(root.transform, "Bay", new Vector3(3.6f, 1.4f, 1.85f), new Vector3(0.75f, 2.5f, 2.3f), B.Building);
            var lb = Box(root.transform, "Letterbox", new Vector3(3.86f, 1.05f, DoorZ), new Vector3(0.14f, 0.36f, 0.6f), B.LetterboxLayer);
            lb.gameObject.AddComponent<Letterbox>().House = house;

            // ---- delivery marker (switched on for subscribers by the street builder)
            var mk = new MeshKit();
            // a big rolled paper with a bright band, floating over the gate: easy to spot from down the street
            mk.Cylinder(Mats.Newsprint, new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.5f), 0.22f, 14);
            mk.Cylinder(Mats.Marker, new Vector3(0, 0, -0.13f), new Vector3(0, 0, 0.13f), 0.235f, 14);
            mk.Sphere(Mats.Marker, new Vector3(0, -0.5f, 0), new Vector3(0.12f, 0.16f, 0.12f), 10, 6);
            var markerRoot = Pivot("Marker", root.transform, new Vector3(1.2f, 2.9f, DoorZ));
            Part("MarkerIcon", markerRoot.transform, mk, "MarkerIcon", 0, false);
            markerRoot.SetActive(false);
            var rk = new MeshKit();
            rk.Plane(Mats.MarkerRing, Vector3.zero, new Vector2(2.2f, 2.2f), Vector3.up, Vector3.forward);
            var ring = Part("MarkerRing", root.transform, rk, "MarkerRing", 0, false);
            ring.transform.localPosition = new Vector3(3.3f, 0.18f, DoorZ);
            ring.SetActive(false);

            var step = Pivot("Doorstep", root.transform, new Vector3(3.6f, 0.33f, DoorZ));
            house.Doorstep = step.transform;
            house.Marker = markerRoot.transform;
            house.MarkerRing = ring.GetComponent<MeshRenderer>();

            // LODs: everything up close, drop the small details further away
            var lod = root.AddComponent<LODGroup>();
            var bodyR = root.transform.Find("Body").GetComponent<Renderer>();
            var near = new List<Renderer> { bodyR, details.GetComponent<Renderer>() };
            near.AddRange(windows);
            var far = new List<Renderer> { bodyR };
            far.AddRange(windows);
            lod.SetLODs(new[] { new LOD(0.18f, near.ToArray()), new LOD(0.015f, far.ToArray()) });
            lod.RecalculateBounds();
            return Save(root, $"House_{v}");
        }

        /// <summary>A white window frame with glazing bars (the glass itself is a separate breakable pane).</summary>
        static void FrameBox(MeshKit k, Vector3 c, float w, float h, bool bayFront, int bars)
        {
            float t = 0.07f;
            k.Box(Mats.Trim, c + new Vector3(0, h / 2, 0), new Vector3(0.1f, t, w + t));
            k.Box(Mats.Trim, c - new Vector3(0, h / 2, 0), new Vector3(0.1f, t, w + t));
            k.Box(Mats.Trim, c + new Vector3(0, 0, w / 2), new Vector3(0.1f, h, t));
            k.Box(Mats.Trim, c - new Vector3(0, 0, w / 2), new Vector3(0.1f, h, t));
            for (int i = 1; i < bars; i++) k.Box(Mats.Trim, c + new Vector3(0, 0, -w / 2 + w * i / bars), new Vector3(0.09f, h, 0.05f));
            k.Box(Mats.Trim, c + new Vector3(0, h * 0.18f, 0), new Vector3(0.09f, 0.05f, w));
        }

        static GameObject MakeGarage()
        {
            float W = HouseWidth;
            var root = new GameObject("Driveway");
            var k = new MeshKit();
            k.Box(Mats.Path, new Vector3(4.5f, 0.07f, W / 2), new Vector3(9f, 0.14f, W), 1f / 1.2f);
            foreach (float z in new[] { 0.3f, W - 0.3f })
            {
                k.Box(Mats.BrickRed, new Vector3(0.2f, 0.45f, z), new Vector3(0.4f, 0.9f, 0.4f), 1f / 1.2f);
                k.Box(Mats.Coping, new Vector3(0.2f, 0.93f, z), new Vector3(0.46f, 0.06f, 0.46f));
            }
            k.Box(Mats.BrickRed, new Vector3(11f, 1.4f, W / 2), new Vector3(4f, 2.8f, W - 0.4f), 1f / 1.2f);
            k.Box(Mats.Trim, new Vector3(9.02f, 1.2f, W / 2), new Vector3(0.06f, 2.2f, 3.0f));
            for (int i = 0; i < 6; i++) k.Box(Mats.Coping, new Vector3(8.98f, 0.3f + i * 0.36f, W / 2), new Vector3(0.04f, 0.03f, 2.9f));
            k.Box(Mats.SlateDark, new Vector3(11f, 2.9f, W / 2), new Vector3(4.3f, 0.2f, W));
            Part("Body", root.transform, k, "Driveway", B.Building);
            Box(root.transform, "Ground", new Vector3(4.5f, 0.07f, W / 2), new Vector3(9f, 0.14f, W), B.Ground);
            Box(root.transform, "Garage", new Vector3(11f, 1.4f, W / 2), new Vector3(4f, 2.8f, W - 0.4f), B.Building);
            return Save(root, "Driveway");
        }

        // ------------------------------------------------------------------ shops

        static GameObject MakeShop(int v, string name, Material awning)
        {
            float W = HouseWidth;
            var wall = new[] { Mats.BrickYellow, Mats.RenderCream, Mats.BrickRed }[v];
            var root = new GameObject("Shop " + name);
            var k = new MeshKit();
            k.Box(Mats.Pavement, new Vector3(0.1f, 0.065f, W / 2), new Vector3(0.2f, 0.13f, W), 1f / 1.2f);
            k.Box(wall, new Vector3(4.2f, 3.6f, W / 2), new Vector3(8f, 7.2f, W), 1f / 1.2f);
            k.Box(Mats.Sign, new Vector3(0.12f, 2.95f, W / 2), new Vector3(0.12f, 0.6f, W - 0.2f));      // fascia
            k.Box(Mats.Trim, new Vector3(0.12f, 0.35f, W / 2), new Vector3(0.14f, 0.7f, W - 0.2f));      // stall riser
            k.Box(Mats.Trim, new Vector3(0.12f, 1.55f, 0.15f), new Vector3(0.14f, 2.5f, 0.3f));
            k.Box(Mats.Trim, new Vector3(0.12f, 1.55f, W - 0.15f), new Vector3(0.14f, 2.5f, 0.3f));
            k.Box(Mats.Doors[v + 1], new Vector3(0.16f, 1.25f, W - 0.85f), new Vector3(0.06f, 2.1f, 0.95f));
            // awning sloping out over the pavement
            k.BoxRotated(awning, new Vector3(-0.55f, 2.55f, W / 2), new Vector3(1.4f, 0.06f, W - 0.3f), Quaternion.Euler(0, 0, -16f));
            k.Box(Mats.White, new Vector3(-1.2f, 2.35f, W / 2), new Vector3(0.05f, 0.2f, W - 0.3f));
            // upstairs
            foreach (float wz in new[] { 1.5f, 4.0f })
            {
                k.Box(Mats.Sill, new Vector3(0.12f, 4.08f, wz), new Vector3(0.16f, 0.08f, 1.15f));
                FrameBox(k, new Vector3(0.18f, 4.8f, wz), 1.0f, 1.4f, false, 2);
                k.Box(Mats.Glass, new Vector3(0.19f, 4.8f, wz), new Vector3(0.03f, 1.4f, 1.0f));
            }
            k.Box(Mats.Coping, new Vector3(4.2f, 7.3f, W / 2), new Vector3(8.1f, 0.2f, W + 0.02f));
            k.Box(wall, new Vector3(6f, 8.0f, 0.3f), new Vector3(0.9f, 1.4f, 0.6f), 1f / 1.2f);
            Part("Body", root.transform, k, $"Shop{v}", B.Building);

            // shop window (breakable)
            var gk = new MeshKit();
            gk.Box(Mats.ShopWindow, new Vector3(0.18f, 1.6f, (W - 1.4f) / 2 + 0.2f), new Vector3(0.04f, 1.75f, W - 1.8f));
            var glass = Part("ShopGlass", root.transform, gk, $"ShopGlass{v}", B.Window, false);
            var col = glass.AddComponent<BoxCollider>();
            col.center = new Vector3(0.18f, 1.6f, (W - 1.4f) / 2 + 0.2f); col.size = new Vector3(0.12f, 1.75f, W - 1.8f);
            var bw = glass.AddComponent<BreakableWindow>();
            bw.Glass = glass.GetComponent<MeshRenderer>();
            bw.BrokenMaterial = Mats.GlassBroken;

            // sign lettering
            var textGo = Pivot("SignText", root.transform, new Vector3(0.05f, 2.95f, W / 2));
            textGo.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = name;
            tm.font = AssetDatabase.LoadAssetAtPath<Font>(B.Root + "Resources/Fonts/LilitaOne.ttf");
            tm.fontSize = 64;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.92f, 0.7f);
            textGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            Box(root.transform, "Front", new Vector3(4.2f, 3.6f, W / 2), new Vector3(8f, 7.2f, W), B.Building);
            return Save(root, $"Shop_{v}");
        }

        // ------------------------------------------------------------------ street furniture

        static GameObject MakeLampPost()
        {
            var root = new GameObject("LampPost");
            var k = new MeshKit();
            var green = Mats.Railing;
            k.Cylinder(green, new Vector3(0, 0, 0), new Vector3(0, 0.6f, 0), 0.14f, 10, true, 0.09f);
            k.Cylinder(green, new Vector3(0, 0.6f, 0), new Vector3(0, 5.2f, 0), 0.07f, 10);
            k.Cylinder(green, new Vector3(0, 5.2f, 0), new Vector3(-0.9f, 5.5f, 0), 0.05f, 8);
            k.Box(green, new Vector3(-1.0f, 5.45f, 0), new Vector3(0.5f, 0.12f, 0.3f));
            k.Box(Mats.LampGlow, new Vector3(-1.0f, 5.33f, 0), new Vector3(0.42f, 0.08f, 0.24f));
            Part("Lamp", root.transform, k, "LampPost", B.Obstacle);
            var c = root.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0, 1.2f, 0); c.radius = 0.16f; c.height = 2.4f;
            root.layer = B.Obstacle;
            return Save(root, "LampPost");
        }

        static GameObject MakeTree(int v, Material leaves, bool collider, float scale = 1f)
        {
            var root = new GameObject("Tree " + v);
            var k = new MeshKit();
            var rnd = new System.Random(500 + v);
            float h = (3.2f + (float)rnd.NextDouble()) * scale;
            k.Cylinder(Mats.Bark, Vector3.zero, new Vector3(0.1f, h, 0.05f), 0.16f * scale, 9, false, 0.1f * scale);
            k.Cylinder(Mats.Bark, new Vector3(0.05f, h * 0.6f, 0), new Vector3(0.7f * scale, h * 0.85f, 0.3f), 0.07f * scale, 6, false, 0.04f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f + (float)rnd.NextDouble();
                var c = new Vector3(Mathf.Cos(a) * 0.7f, h + (float)rnd.NextDouble() * 1.0f - 0.2f, Mathf.Sin(a) * 0.7f) * 1f;
                c.x *= scale; c.z *= scale;
                float r = (0.9f + (float)rnd.NextDouble() * 0.5f) * scale;
                k.Sphere(leaves, c, new Vector3(r, r * 0.85f, r), 10, 7);
            }
            k.Sphere(leaves, new Vector3(0, h + 1.1f * scale, 0), Vector3.one * 1.1f * scale, 10, 7);
            if (collider) k.Box(Mats.Soil, new Vector3(0, 0.005f, 0), new Vector3(1.0f, 0.01f, 1.0f));
            Part("Tree", root.transform, k, "Tree" + v, collider ? B.Obstacle : 0);
            if (collider)
            {
                var c = root.AddComponent<CapsuleCollider>();
                c.center = new Vector3(0, 1.2f, 0); c.radius = 0.22f; c.height = 2.4f;
                root.layer = B.Obstacle;
            }
            return Save(root, collider ? "Tree_" + v : "BackTree");
        }

        static GameObject MakeBin(int v, Material body)
        {
            var root = new GameObject("WheelieBin " + v);
            var k = new MeshKit();
            k.Box(body, new Vector3(0, 0.55f, 0), new Vector3(0.58f, 0.95f, 0.7f));
            k.Box(body, new Vector3(0, 1.06f, 0.02f), new Vector3(0.64f, 0.07f, 0.78f));
            k.Box(Mats.Black, new Vector3(0, 0.95f, -0.38f), new Vector3(0.5f, 0.05f, 0.06f));
            foreach (float x in new[] { -0.24f, 0.24f }) k.Cylinder(Mats.Rubber, new Vector3(x - 0.04f, 0.1f, -0.3f), new Vector3(x + 0.04f, 0.1f, -0.3f), 0.1f, 10);
            Part("Bin", root.transform, k, "Bin" + v, B.Obstacle);
            var c = root.AddComponent<BoxCollider>();
            c.center = new Vector3(0, 0.55f, 0); c.size = new Vector3(0.62f, 1.1f, 0.78f);
            root.layer = B.Obstacle;
            return Save(root, "Bin_" + v);
        }

        static GameObject MakePostBox()
        {
            var root = new GameObject("PostBox");
            var red = Mats.Lit("PostRed", new Color(0.82f, 0.1f, 0.1f), null, 0.6f);
            var k = new MeshKit();
            k.Cylinder(Mats.Black, Vector3.zero, new Vector3(0, 0.12f, 0), 0.3f, 14);
            k.Cylinder(red, new Vector3(0, 0.12f, 0), new Vector3(0, 1.35f, 0), 0.26f, 14);
            k.Cylinder(red, new Vector3(0, 1.35f, 0), new Vector3(0, 1.45f, 0), 0.3f, 14);
            k.Sphere(red, new Vector3(0, 1.45f, 0), new Vector3(0.28f, 0.14f, 0.28f), 14, 6);
            k.Box(Mats.Black, new Vector3(0, 1.15f, -0.25f), new Vector3(0.24f, 0.04f, 0.06f));
            Part("Box", root.transform, k, "PostBox", B.Obstacle);
            var c = root.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0, 0.75f, 0); c.radius = 0.3f; c.height = 1.5f;
            root.layer = B.Obstacle;
            return Save(root, "PostBox");
        }

        static GameObject MakeBench()
        {
            var root = new GameObject("Bench");
            var k = new MeshKit();
            for (int i = 0; i < 3; i++) k.Box(Mats.Wood, new Vector3(0, 0.45f, -0.12f + i * 0.14f), new Vector3(1.6f, 0.05f, 0.11f));
            for (int i = 0; i < 2; i++) k.Box(Mats.Wood, new Vector3(0, 0.7f + i * 0.16f, 0.26f), new Vector3(1.6f, 0.1f, 0.05f));
            foreach (float x in new[] { -0.7f, 0.7f }) { k.Box(Mats.Railing, new Vector3(x, 0.22f, 0.05f), new Vector3(0.06f, 0.45f, 0.5f)); k.Box(Mats.Railing, new Vector3(x, 0.65f, 0.27f), new Vector3(0.06f, 0.5f, 0.06f)); }
            Part("Bench", root.transform, k, "Bench", B.Obstacle);
            var c = root.AddComponent<BoxCollider>(); c.center = new Vector3(0, 0.45f, 0.05f); c.size = new Vector3(1.7f, 0.9f, 0.6f);
            root.layer = B.Obstacle;
            return Save(root, "Bench");
        }

        static GameObject MakeBollard()
        {
            var root = new GameObject("Bollard");
            var k = new MeshKit();
            k.Cylinder(Mats.Bollard, Vector3.zero, new Vector3(0, 0.9f, 0), 0.1f, 10);
            k.Sphere(Mats.Bollard, new Vector3(0, 0.9f, 0), new Vector3(0.11f, 0.08f, 0.11f), 10, 5);
            k.Cylinder(Mats.White, new Vector3(0, 0.72f, 0), new Vector3(0, 0.8f, 0), 0.102f, 10, false);
            Part("Bollard", root.transform, k, "Bollard", B.Obstacle);
            var c = root.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, 0.5f, 0); c.radius = 0.13f; c.height = 1f;
            root.layer = B.Obstacle;
            return Save(root, "Bollard");
        }

        static GameObject MakeCone()
        {
            var root = new GameObject("Cone");
            var k = new MeshKit();
            k.Box(Mats.Cone, new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.04f, 0.4f));
            k.Cylinder(Mats.Cone, new Vector3(0, 0.04f, 0), new Vector3(0, 0.7f, 0), 0.16f, 12, true, 0.03f);
            k.Cylinder(Mats.White, new Vector3(0, 0.32f, 0), new Vector3(0, 0.44f, 0), 0.115f, 12, false, 0.095f);
            Part("Cone", root.transform, k, "Cone", B.Obstacle);
            var c = root.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, 0.35f, 0); c.radius = 0.18f; c.height = 0.7f;
            root.layer = B.Obstacle;
            return Save(root, "Cone");
        }

        static GameObject MakeRailing()
        {
            var root = new GameObject("Railing");
            var k = new MeshKit();
            for (int i = 0; i < 12; i++)
            {
                float z = i * 0.18f;
                k.Cylinder(Mats.Railing, new Vector3(0, 0, z), new Vector3(0, 1.05f, z), 0.018f, 5, false);
                k.Sphere(Mats.Railing, new Vector3(0, 1.08f, z), new Vector3(0.03f, 0.05f, 0.03f), 5, 3);
            }
            k.Box(Mats.Railing, new Vector3(0, 0.95f, 1.0f), new Vector3(0.04f, 0.04f, 2.15f));
            k.Box(Mats.Railing, new Vector3(0, 0.15f, 1.0f), new Vector3(0.04f, 0.04f, 2.15f));
            k.Box(Mats.Coping, new Vector3(0, 0.05f, 1.0f), new Vector3(0.3f, 0.1f, 2.2f));
            Part("Railing", root.transform, k, "Railing", B.Building);
            Box(root.transform, "Col", new Vector3(0, 0.55f, 1.0f), new Vector3(0.2f, 1.1f, 2.2f), B.Building);
            return Save(root, "Railing");
        }

        static GameObject MakeBeacon()
        {
            var root = new GameObject("BelishaBeacon");
            var k = new MeshKit();
            for (int i = 0; i < 9; i++) k.Cylinder(i % 2 == 0 ? Mats.Black : Mats.White, new Vector3(0, i * 0.3f, 0), new Vector3(0, (i + 1) * 0.3f, 0), 0.06f, 8, false);
            Part("Pole", root.transform, k, "BeaconPole", B.Obstacle);
            var gk = new MeshKit();
            gk.Sphere(Mats.BeaconOn, new Vector3(0, 2.85f, 0), Vector3.one * 0.2f, 12, 8);
            var globe = Part("Globe", root.transform, gk, "BeaconGlobe", 0, false);
            var blink = globe.AddComponent<Blinker>();
            blink.Target = globe.GetComponent<Renderer>();
            blink.On = Mats.BeaconOn;
            blink.Off = Mats.IndicatorOff;
            var c = root.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, 1.2f, 0); c.radius = 0.1f; c.height = 2.4f;
            root.layer = B.Obstacle;
            return Save(root, "BelishaBeacon");
        }

        static GameObject MakeArch(string name, string text)
        {
            var root = new GameObject(name + " Arch");
            var k = new MeshKit();
            foreach (float x in new[] { -5.6f, 5.6f })
            {
                k.Cylinder(Mats.White, new Vector3(x, 0, 0), new Vector3(x, 4.6f, 0), 0.16f, 10);
                k.Sphere(Mats.Cone, new Vector3(x, 4.7f, 0), Vector3.one * 0.25f, 10, 6);
            }
            k.Box(name == "Start" ? Mats.Awning3 : Mats.Awning1, new Vector3(0, 4.1f, 0), new Vector3(11.2f, 0.9f, 0.15f));
            k.Plane(Mats.Chequer, new Vector3(0, 0.012f, 0), new Vector2(7f, 0.9f), Vector3.up, Vector3.forward, false);
            for (int i = 0; i < 10; i++) k.Sphere(i % 2 == 0 ? Mats.Flower2 : Mats.Flower1, new Vector3(-4.5f + i, 3.55f, -0.08f), new Vector3(0.15f, 0.22f, 0.1f), 8, 5); // bunting
            Part("Arch", root.transform, k, name + "Arch", 0);
            var t = Pivot("Text", root.transform, new Vector3(0, 4.1f, -0.1f));
            var tm = t.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = AssetDatabase.LoadAssetAtPath<Font>(B.Root + "Resources/Fonts/LilitaOne.ttf");
            tm.fontSize = 96; tm.characterSize = 0.07f; tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
            t.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            return Save(root, name + "Arch");
        }

        static GameObject MakePothole()
        {
            var root = new GameObject("Pothole");
            var k = new MeshKit();
            var dark = Mats.Lit("PotholeDark", new Color(0.1f, 0.1f, 0.11f), null, 0.6f);
            k.Sphere(dark, new Vector3(0, 0.004f, 0), new Vector3(0.6f, 0.005f, 0.45f), 14, 3);
            k.Sphere(Mats.Tarmac, new Vector3(0.1f, 0.01f, 0.05f), new Vector3(0.68f, 0.008f, 0.52f), 14, 3);
            // council spray-paint ring so it reads from a distance
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                k.Box(Mats.PaintYellow, new Vector3(Mathf.Cos(a) * 0.82f, 0.012f, Mathf.Sin(a) * 0.64f), new Vector3(0.18f, 0.01f, 0.06f));
            }
            Part("Hole", root.transform, k, "Pothole", 0, false);
            root.AddComponent<Pothole>().Radius = 0.6f;
            return Save(root, "Pothole");
        }

        // ------------------------------------------------------------------ cars

        static void CarBody(MeshKit k, Material paint)
        {
            k.Box(paint, new Vector3(0, 0.62f, 0), new Vector3(1.78f, 0.6f, 3.9f));
            k.Box(paint, new Vector3(0, 0.98f, 1.55f), new Vector3(1.74f, 0.12f, 0.8f));   // bonnet top
            k.Box(paint, new Vector3(0, 1.22f, -0.25f), new Vector3(1.6f, 0.55f, 2.1f));   // cabin
            k.Box(Mats.Glass, new Vector3(0, 1.22f, 0.83f), new Vector3(1.5f, 0.45f, 0.06f));
            k.Box(Mats.Glass, new Vector3(0, 1.22f, -1.32f), new Vector3(1.45f, 0.42f, 0.06f));
            k.Box(Mats.Glass, new Vector3(0.81f, 1.24f, -0.25f), new Vector3(0.03f, 0.38f, 1.9f));
            k.Box(Mats.Glass, new Vector3(-0.81f, 1.24f, -0.25f), new Vector3(0.03f, 0.38f, 1.9f));
            k.Box(Mats.Rubber, new Vector3(0, 0.38f, 1.97f), new Vector3(1.8f, 0.2f, 0.12f));
            k.Box(Mats.Rubber, new Vector3(0, 0.38f, -1.97f), new Vector3(1.8f, 0.2f, 0.12f));
            k.Box(Mats.Headlight, new Vector3(0.6f, 0.72f, 1.96f), new Vector3(0.36f, 0.16f, 0.04f));
            k.Box(Mats.Headlight, new Vector3(-0.6f, 0.72f, 1.96f), new Vector3(0.36f, 0.16f, 0.04f));
            k.Box(Mats.White, new Vector3(0, 0.45f, 2.04f), new Vector3(0.5f, 0.12f, 0.02f));
            k.Box(Mats.PaintYellow, new Vector3(0, 0.45f, -2.04f), new Vector3(0.5f, 0.12f, 0.02f));
            k.Box(Mats.Black, new Vector3(0.95f, 1.0f, 0.75f), new Vector3(0.12f, 0.08f, 0.16f));   // mirrors
            k.Box(Mats.Black, new Vector3(-0.95f, 1.0f, 0.75f), new Vector3(0.12f, 0.08f, 0.16f));
        }

        static GameObject MakeCar(int paint, bool reversing)
        {
            var root = new GameObject(reversing ? "ReversingCar" : "Car " + paint);
            var k = new MeshKit();
            CarBody(k, Mats.CarPaints[paint]);
            Part("Body", root.transform, k, reversing ? "CarBodyReversing" : "CarBody" + paint, B.Obstacle);
            var wheels = new List<Transform>();
            foreach (float x in new[] { -0.82f, 0.82f })
                foreach (float z in new[] { -1.25f, 1.25f })
                {
                    var wk = new MeshKit();
                    wk.Cylinder(Mats.Rubber, new Vector3(-0.12f, 0, 0), new Vector3(0.12f, 0, 0), 0.33f, 14);
                    wk.Cylinder(Mats.Chrome, new Vector3(x > 0 ? 0.121f : -0.121f, 0, 0), new Vector3(x > 0 ? 0.125f : -0.125f, 0, 0), 0.18f, 10);
                    var w = Part("Wheel", root.transform, wk, $"CarWheel{(x > 0 ? "R" : "L")}", B.Obstacle);
                    w.transform.localPosition = new Vector3(x, 0.33f, z);
                    wheels.Add(w.transform);
                }
            // tail lights / indicators as separate renderers so the reversing car can light them
            Renderer Light(string name, Vector3 c, Vector3 size, Material m)
            {
                var lk = new MeshKit(); lk.Box(m, c, size);
                return Part(name, root.transform, lk, (reversing ? "Rev" : "Car") + name, B.Obstacle, false).GetComponent<Renderer>();
            }
            var indicators = new[]
            {
                Light("IndFL", new Vector3(0.82f, 0.72f, 1.96f), new Vector3(0.12f, 0.1f, 0.05f), Mats.IndicatorOff),
                Light("IndFR", new Vector3(-0.82f, 0.72f, 1.96f), new Vector3(0.12f, 0.1f, 0.05f), Mats.IndicatorOff),
                Light("IndBL", new Vector3(0.82f, 0.75f, -1.96f), new Vector3(0.14f, 0.12f, 0.05f), Mats.IndicatorOff),
                Light("IndBR", new Vector3(-0.82f, 0.75f, -1.96f), new Vector3(0.14f, 0.12f, 0.05f), Mats.IndicatorOff),
            };
            Light("TailL", new Vector3(0.6f, 0.75f, -1.96f), new Vector3(0.3f, 0.14f, 0.05f), Mats.Brake);
            Light("TailR", new Vector3(-0.6f, 0.75f, -1.96f), new Vector3(0.3f, 0.14f, 0.05f), Mats.Brake);
            var rev = new[]
            {
                Light("RevL", new Vector3(0.35f, 0.75f, -1.96f), new Vector3(0.14f, 0.1f, 0.05f), Mats.ReverseOff),
                Light("RevR", new Vector3(-0.35f, 0.75f, -1.96f), new Vector3(0.14f, 0.1f, 0.05f), Mats.ReverseOff),
            };
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.8f, 0); col.size = new Vector3(1.85f, 1.6f, 4.05f);
            root.layer = B.Obstacle;
            if (reversing)
            {
                var rc = root.AddComponent<ReversingCar>();
                rc.Indicators = indicators;
                rc.ReverseLights = rev;
                rc.LightOn = Mats.Indicator;
                rc.IndicatorOff = Mats.IndicatorOff;
                rc.ReverseOff = Mats.ReverseOff;
                rc.Wheels = wheels.ToArray();
                rc.WarnDistance = 46f;
                rc.ActDistance = 36f;
                rc.Warning = Warning(root.transform, 2.6f).GetComponent<WarningSign>();
            }
            return Save(root, reversing ? "ReversingCar" : "Car_" + paint);
        }

        // ------------------------------------------------------------------ dog and people

        static GameObject MakeDog()
        {
            var root = new GameObject("Dog");
            var body = Pivot("Body", root.transform, Vector3.zero);
            var k = new MeshKit();
            k.Sphere(Mats.DogBrown, new Vector3(0, 0.5f, 0), new Vector3(0.26f, 0.24f, 0.45f), 12, 8);
            k.Sphere(Mats.DogWhite, new Vector3(0, 0.44f, 0.12f), new Vector3(0.2f, 0.18f, 0.3f), 10, 6);
            k.Sphere(Mats.DogBrown, new Vector3(0, 0.78f, 0.45f), new Vector3(0.2f, 0.2f, 0.21f), 12, 8);
            k.Sphere(Mats.DogWhite, new Vector3(0, 0.72f, 0.64f), new Vector3(0.11f, 0.09f, 0.13f), 10, 6);
            k.Sphere(Mats.Black, new Vector3(0, 0.76f, 0.76f), new Vector3(0.045f, 0.035f, 0.035f), 8, 5);
            k.Sphere(Mats.Black, new Vector3(0.08f, 0.85f, 0.62f), Vector3.one * 0.03f, 6, 4);
            k.Sphere(Mats.Black, new Vector3(-0.08f, 0.85f, 0.62f), Vector3.one * 0.03f, 6, 4);
            k.Sphere(Mats.DogBrown, new Vector3(0.17f, 0.88f, 0.4f), new Vector3(0.05f, 0.15f, 0.1f), 8, 5);
            k.Sphere(Mats.DogBrown, new Vector3(-0.17f, 0.88f, 0.4f), new Vector3(0.05f, 0.15f, 0.1f), 8, 5);
            k.Cylinder(Mats.DogCollar, new Vector3(0, 0.63f, 0.33f), new Vector3(0, 0.66f, 0.37f), 0.16f, 12);
            Part("Mesh", body.transform, k, "DogBody", B.Character);
            var tail = Pivot("Tail", body.transform, new Vector3(0, 0.6f, -0.42f));
            var tk = new MeshKit(); tk.Cylinder(Mats.DogBrown, Vector3.zero, new Vector3(0, 0.05f, -0.3f), 0.04f, 6, true, 0.02f);
            Part("TailMesh", tail.transform, tk, "DogTail", B.Character);
            var legs = new Transform[4];
            int n = 0;
            foreach (float z in new[] { 0.28f, -0.28f })
                foreach (float x in new[] { -0.14f, 0.14f })
                {
                    var leg = Pivot("Leg", body.transform, new Vector3(x, 0.42f, z));
                    var lk = new MeshKit();
                    lk.Cylinder(Mats.DogBrown, Vector3.zero, new Vector3(0, -0.36f, 0), 0.06f, 6);
                    lk.Sphere(Mats.DogWhite, new Vector3(0, -0.38f, 0.03f), new Vector3(0.07f, 0.04f, 0.09f), 6, 4);
                    Part("LegMesh", leg.transform, lk, "DogLeg", B.Character);
                    legs[n++] = leg.transform;
                }
            var dog = root.AddComponent<Dog>();
            dog.Body = body.transform; dog.Tail = tail.transform;
            dog.LegFL = legs[0]; dog.LegFR = legs[1]; dog.LegBL = legs[2]; dog.LegBR = legs[3];
            dog.WarnDistance = 34f; dog.ActDistance = 24f;
            dog.Warning = Warning(root.transform, 1.6f).GetComponent<WarningSign>();
            var c = root.AddComponent<CapsuleCollider>();
            c.direction = 2; c.center = new Vector3(0, 0.5f, 0.1f); c.radius = 0.3f; c.height = 1.1f;
            c.isTrigger = true;
            root.layer = B.Character;
            return Save(root, "Dog");
        }

        static GameObject MakePerson(int v, Material top, Material legsMat, Material skin, Material hair, bool hat)
        {
            var root = new GameObject("Person " + v);
            var body = Pivot("Body", root.transform, Vector3.zero);
            var k = new MeshKit();
            k.Sphere(top, new Vector3(0, 1.25f, 0), new Vector3(0.27f, 0.42f, 0.2f), 12, 8);
            k.Sphere(skin, new Vector3(0, 1.82f, 0), new Vector3(0.2f, 0.22f, 0.2f), 12, 8);
            k.Sphere(hair, new Vector3(0, 1.9f, -0.03f), new Vector3(0.21f, 0.17f, 0.21f), 12, 6);
            k.Sphere(skin, new Vector3(0, 1.8f, 0.19f), new Vector3(0.04f, 0.05f, 0.04f), 6, 4);
            k.Sphere(Mats.Black, new Vector3(0.07f, 1.86f, 0.17f), Vector3.one * 0.025f, 6, 4);
            k.Sphere(Mats.Black, new Vector3(-0.07f, 1.86f, 0.17f), Vector3.one * 0.025f, 6, 4);
            if (hat) { k.Cylinder(Mats.Cap, new Vector3(0, 1.98f, 0), new Vector3(0, 2.12f, 0), 0.18f, 12); k.Cylinder(Mats.Cap, new Vector3(0, 1.97f, 0), new Vector3(0, 1.99f, 0), 0.28f, 12); }
            if (v % 2 == 0) k.Box(Mats.Bag, new Vector3(0.32f, 0.85f, 0), new Vector3(0.1f, 0.3f, 0.32f));
            Part("Mesh", body.transform, k, "PersonBody" + v, B.Character);
            Transform Limb(string name, Vector3 at, Material m, float len, float r)
            {
                var p = Pivot(name, body.transform, at);
                var lk = new MeshKit();
                lk.Cylinder(m, Vector3.zero, new Vector3(0, -len, 0), r, 8);
                lk.Sphere(name.StartsWith("Leg") ? Mats.Black : skin, new Vector3(0, -len, name.StartsWith("Leg") ? 0.05f : 0), new Vector3(r * 1.2f, r * 0.9f, r * (name.StartsWith("Leg") ? 1.8f : 1.2f)), 8, 5);
                Part(name + "Mesh", p.transform, lk, "Person" + name + v, B.Character);
                return p.transform;
            }
            var person = root.AddComponent<Pedestrian>();
            person.Body = body.transform;
            person.LegL = Limb("LegL", new Vector3(-0.1f, 0.88f, 0), legsMat, 0.8f, 0.09f);
            person.LegR = Limb("LegR", new Vector3(0.1f, 0.88f, 0), legsMat, 0.8f, 0.09f);
            person.ArmL = Limb("ArmL", new Vector3(-0.29f, 1.5f, 0), top, 0.55f, 0.07f);
            person.ArmR = Limb("ArmR", new Vector3(0.29f, 1.5f, 0), top, 0.55f, 0.07f);
            person.Warning = Warning(root.transform, 2.5f).GetComponent<WarningSign>();
            var c = root.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0, 0.95f, 0); c.radius = 0.32f; c.height = 1.9f;
            c.isTrigger = true;
            root.layer = B.Character;
            return Save(root, "Person_" + v);
        }

        // ------------------------------------------------------------------ the cyclist

        static GameObject MakeCyclist()
        {
            var root = new GameObject("Cyclist");
            root.layer = B.Rider;
            var lean = Pivot("LeanRoot", root.transform, Vector3.zero);
            const float wheelR = 0.34f, wheelZ = 0.52f;

            // frame, basket of papers, rack and satchel
            var f = new MeshKit();
            var frame = Mats.BikeFrame;
            Vector3 bb = new Vector3(0, wheelR, 0), seatTop = new Vector3(0, 0.92f, -0.12f), headTop = new Vector3(0, 0.95f, 0.36f), headBot = new Vector3(0, 0.62f, 0.42f);
            Vector3 rearAxle = new Vector3(0, wheelR, -wheelZ), frontAxle = new Vector3(0, wheelR, wheelZ);
            f.Cylinder(frame, bb, seatTop, 0.03f, 8);
            f.Cylinder(frame, seatTop + new Vector3(0, -0.05f, 0), headTop + new Vector3(0, -0.05f, 0), 0.028f, 8);
            f.Cylinder(frame, bb, headBot, 0.032f, 8);
            f.Cylinder(frame, headBot, headTop, 0.035f, 8);
            foreach (float x in new[] { -0.05f, 0.05f })
            {
                f.Cylinder(frame, bb + new Vector3(x, 0, 0), rearAxle + new Vector3(x, 0, 0), 0.018f, 6);
                f.Cylinder(frame, seatTop + new Vector3(x, -0.06f, 0), rearAxle + new Vector3(x, 0, 0), 0.016f, 6);
                f.Cylinder(frame, headBot + new Vector3(x, 0, 0), frontAxle + new Vector3(x, 0, 0), 0.02f, 6);
            }
            f.Cylinder(Mats.Chrome, seatTop, seatTop + new Vector3(0, 0.1f, -0.01f), 0.016f, 6);
            f.Sphere(Mats.BikeSeat, seatTop + new Vector3(0, 0.13f, 0.0f), new Vector3(0.09f, 0.04f, 0.15f), 10, 6);
            // front basket with rolled papers
            var basketC = new Vector3(0, 1.0f, 0.62f);
            f.Box(Mats.WoodPale, basketC + new Vector3(0, -0.1f, 0), new Vector3(0.38f, 0.04f, 0.3f));
            foreach (var o in new[] { new Vector3(0.19f, 0, 0), new Vector3(-0.19f, 0, 0) }) f.Box(Mats.WoodPale, basketC + o, new Vector3(0.02f, 0.2f, 0.3f));
            foreach (var o in new[] { new Vector3(0, 0, 0.15f), new Vector3(0, 0, -0.15f) }) f.Box(Mats.WoodPale, basketC + o, new Vector3(0.38f, 0.2f, 0.02f));
            for (int i = 0; i < 4; i++) f.Cylinder(Mats.Newsprint, basketC + new Vector3(-0.15f, 0.02f + (i % 2) * 0.07f, -0.08f + (i / 2) * 0.12f), basketC + new Vector3(0.15f, 0.02f + (i % 2) * 0.07f, -0.08f + (i / 2) * 0.12f), 0.05f, 8);
            // rear rack + satchel
            f.Box(Mats.Metal, new Vector3(0, 0.72f, -0.48f), new Vector3(0.18f, 0.03f, 0.4f));
            f.Box(Mats.Bag, new Vector3(0.17f, 0.6f, -0.48f), new Vector3(0.12f, 0.28f, 0.36f));
            f.Box(Mats.Bag, new Vector3(-0.17f, 0.6f, -0.48f), new Vector3(0.12f, 0.28f, 0.36f));
            Part("Frame", lean.transform, f, "BikeFrame", B.Rider);

            Transform Wheel(string name, Vector3 at)
            {
                var w = Pivot(name, lean.transform, at);
                var k = new MeshKit();
                k.Torus(Mats.Rubber, Vector3.zero, Vector3.right, wheelR - 0.025f, 0.028f, 24, 8);
                k.Torus(Mats.Chrome, Vector3.zero, Vector3.right, wheelR - 0.06f, 0.01f, 24, 5);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    k.Cylinder(Mats.Chrome, Vector3.zero, new Vector3(0, Mathf.Cos(a), Mathf.Sin(a)) * (wheelR - 0.06f), 0.005f, 4, false);
                }
                k.Cylinder(Mats.Metal, new Vector3(-0.05f, 0, 0), new Vector3(0.05f, 0, 0), 0.03f, 8);
                Part(name + "Mesh", w.transform, k, "BikeWheel", B.Rider);
                return w.transform;
            }
            var front = Wheel("FrontWheel", frontAxle);
            var back = Wheel("BackWheel", rearAxle);

            var crank = Pivot("Crank", lean.transform, bb);
            var ck = new MeshKit();
            ck.Torus(Mats.Chrome, Vector3.zero, Vector3.right, 0.09f, 0.012f, 16, 4);
            ck.Cylinder(Mats.Metal, new Vector3(0.08f, 0, 0), new Vector3(0.08f, 0.17f, 0), 0.015f, 6);
            ck.Cylinder(Mats.Metal, new Vector3(-0.08f, 0, 0), new Vector3(-0.08f, -0.17f, 0), 0.015f, 6);
            Part("CrankMesh", crank.transform, ck, "BikeCrank", B.Rider);
            Transform Pedal(string name, Vector3 at)
            {
                var p = Pivot(name, crank.transform, at);
                var pk = new MeshKit(); pk.Box(Mats.Black, Vector3.zero, new Vector3(0.1f, 0.025f, 0.08f));
                Part(name + "Mesh", p.transform, pk, "BikePedal", B.Rider);
                return p.transform;
            }
            var pedalL = Pedal("PedalL", new Vector3(-0.13f, -0.17f, 0));
            var pedalR = Pedal("PedalR", new Vector3(0.13f, 0.17f, 0));

            var bars = Pivot("Handlebar", lean.transform, headTop);
            var hk = new MeshKit();
            hk.Cylinder(Mats.Chrome, Vector3.zero, new Vector3(0, 0.08f, -0.06f), 0.018f, 6);
            hk.Cylinder(Mats.Chrome, new Vector3(-0.27f, 0.08f, -0.1f), new Vector3(0.27f, 0.08f, -0.1f), 0.015f, 8);
            hk.Cylinder(Mats.Rubber, new Vector3(-0.3f, 0.08f, -0.1f), new Vector3(-0.2f, 0.08f, -0.1f), 0.022f, 8);
            hk.Cylinder(Mats.Rubber, new Vector3(0.2f, 0.08f, -0.1f), new Vector3(0.3f, 0.08f, -0.1f), 0.022f, 8);
            hk.Cylinder(Mats.Chrome, new Vector3(0.2f, 0.11f, -0.1f), new Vector3(0.2f, 0.14f, -0.1f), 0.03f, 10);   // bell
            Part("BarsMesh", bars.transform, hk, "BikeBars", B.Rider);

            // rider: chunky and friendly; orange jacket, blue cap, satchel strap
            var rider = Pivot("Rider", lean.transform, Vector3.zero);
            var rk = new MeshKit();
            rk.Sphere(Mats.Trousers, new Vector3(0, 1.08f, -0.14f), new Vector3(0.2f, 0.12f, 0.17f), 10, 6);
            rk.Sphere(Mats.Jacket, new Vector3(0, 1.38f, -0.02f), new Vector3(0.24f, 0.3f, 0.19f), 12, 8);
            rk.Cylinder(Mats.Bag, new Vector3(-0.2f, 1.6f, -0.02f), new Vector3(0.2f, 1.15f, 0.05f), 0.03f, 6);
            rk.Sphere(Mats.Skin1, new Vector3(0, 1.82f, 0.06f), new Vector3(0.17f, 0.18f, 0.17f), 12, 8);
            rk.Sphere(Mats.Skin1, new Vector3(0, 1.8f, 0.22f), new Vector3(0.035f, 0.04f, 0.035f), 6, 4);
            rk.Sphere(Mats.Black, new Vector3(0.06f, 1.86f, 0.2f), Vector3.one * 0.02f, 6, 4);
            rk.Sphere(Mats.Black, new Vector3(-0.06f, 1.86f, 0.2f), Vector3.one * 0.02f, 6, 4);
            rk.Sphere(Mats.Hair2, new Vector3(0, 1.84f, -0.04f), new Vector3(0.17f, 0.15f, 0.15f), 10, 6);
            rk.Sphere(Mats.Cap, new Vector3(0, 1.94f, 0.04f), new Vector3(0.18f, 0.1f, 0.18f), 12, 6);
            rk.Box(Mats.Cap, new Vector3(0, 1.93f, 0.22f), new Vector3(0.22f, 0.025f, 0.16f));
            // left arm to the handlebar
            rk.Cylinder(Mats.Jacket, new Vector3(-0.22f, 1.55f, 0.02f), new Vector3(-0.27f, 1.15f, 0.25f), 0.055f, 8);
            rk.Sphere(Mats.Skin1, new Vector3(-0.26f, 1.03f, 0.27f), Vector3.one * 0.05f, 6, 4);
            Part("RiderMesh", rider.transform, rk, "RiderBody", B.Rider);

            Transform Leg(string side, float x)
            {
                var thigh = Pivot("Thigh" + side, rider.transform, new Vector3(x, 1.06f, -0.12f));
                var tk = new MeshKit(); tk.Cylinder(Mats.Trousers, Vector3.zero, new Vector3(0, -0.42f, 0), 0.07f, 8);
                Part("ThighMesh", thigh.transform, tk, "RiderThigh", B.Rider);
                var shin = Pivot("Shin" + side, thigh.transform, new Vector3(0, -0.42f, 0));
                var sk = new MeshKit();
                sk.Cylinder(Mats.Trousers, Vector3.zero, new Vector3(0, -0.4f, 0), 0.06f, 8);
                sk.Sphere(Mats.White, new Vector3(0, -0.42f, 0.06f), new Vector3(0.06f, 0.05f, 0.11f), 8, 5);
                Part("ShinMesh", shin.transform, sk, "RiderShin", B.Rider);
                return thigh.transform;
            }
            var thighL = Leg("L", -0.11f);
            var thighR = Leg("R", 0.11f);

            var arm = Pivot("ThrowArm", rider.transform, new Vector3(0.22f, 1.55f, 0.0f));
            var ak = new MeshKit();
            ak.Cylinder(Mats.Jacket, Vector3.zero, new Vector3(0.08f, -0.42f, 0.18f), 0.055f, 8);
            ak.Sphere(Mats.Skin1, new Vector3(0.09f, -0.47f, 0.2f), Vector3.one * 0.05f, 6, 4);
            Part("ArmMesh", arm.transform, ak, "RiderArm", B.Rider);

            // a soft blob shadow helps judge position against kerbs
            var sh = new MeshKit();
            sh.Plane(Mats.Shadow, Vector3.zero, new Vector2(0.8f, 1.7f), Vector3.up, Vector3.forward);
            var shadow = Part("BlobShadow", root.transform, sh, "BlobShadow", 0, false);
            shadow.transform.localPosition = new Vector3(0, 0.02f, 0);

            var hand = Pivot("Hand", root.transform, new Vector3(0.32f, 1.55f, 0.2f));

            var bike = root.AddComponent<BikeController>();
            bike.Obstacles = (1 << B.Obstacle) | (1 << B.Character);
            var vis = root.AddComponent<BikeVisuals>();
            vis.Bike = bike; vis.LeanRoot = lean.transform; vis.FrontWheel = front; vis.BackWheel = back; vis.Crank = crank.transform;
            vis.PedalLeft = pedalL; vis.PedalRight = pedalR; vis.Handlebar = bars.transform;
            vis.ThighL = thighL; vis.ThighR = thighR; vis.ShinL = thighL.Find("ShinL"); vis.ShinR = thighR.Find("ShinR");
            vis.ThrowArm = arm.transform;
            vis.WheelRadius = wheelR;
            var blink = new List<Renderer>(lean.GetComponentsInChildren<Renderer>());
            vis.BlinkWhenShielded = blink.ToArray();
            var thrower = root.AddComponent<ThrowController>();
            thrower.Bike = bike; thrower.Visuals = vis; thrower.Hand = hand.transform;
            thrower.ArcBlockers = (1 << B.Building) | (1 << B.Window) | (1 << B.Obstacle) | (1 << B.Character) | (1 << B.LetterboxLayer);
            var impulse = root.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseDuration = 0.25f;
            return Save(root, "Cyclist");
        }
    }
}
