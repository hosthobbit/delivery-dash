using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using B = DeliveryDash.EditorTools.DeliveryDashBuilder;

namespace DeliveryDash.EditorTools
{
    /// <summary>
    /// Lays out the street and wires up the scene. The street runs along +Z:
    ///   -90 … -3   practice lane (first play only)
    ///     0        start line
    ///     0 … 230  terraces both sides
    ///   230 … 310  park on the left, zebra crossing at 262
    ///   310 … 530  terraces; a driveway on the right at ~430 with a car that reverses out
    ///   530 … 610  shop parade on the left; the dog's house on the right at ~568
    ///   610 … 880  terraces, potholes
    ///   880        finish line
    /// </summary>
    public static class StreetBuilder
    {
        public const float StartZ = 0f, FinishZ = 880f, PracticeZ = -86f;
        const float RoadHalf = 3.5f, PavementOuter = 5.7f;
        const float W = Prefabs.HouseWidth;

        static Transform world;
        static readonly List<House> houses = new List<House>();
        static readonly List<Hazard> hazards = new List<Hazard>();
        static readonly List<Pothole> potholes = new List<Pothole>();
        static House practiceHouse;

        public static void Build(VolumeProfile profile)
        {
            houses.Clear(); hazards.Clear(); potholes.Clear(); practiceHouse = null;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            world = new GameObject("Street").transform;

            Lighting();
            Road(-130f, 960f);
            Backdrop(-130f, 960f);
            Terraces();
            Furniture();
            var cyclist = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs.Cyclist);
            cyclist.transform.position = new Vector3(0, 0, StartZ - 3f);
            Systems(cyclist, profile);

            EditorSceneManager.SaveScene(scene, B.ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(B.ScenePath, true) };
            Debug.Log($"DD_SCENE houses={houses.Count} subscribers={houses.FindAll(h => h.Subscriber).Count} hazards={hazards.Count}");
        }

        static GameObject Place(GameObject prefab, Vector3 pos, float yaw, Transform parent = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent ? parent : world);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            return go;
        }

        // ------------------------------------------------------------------ light and atmosphere

        static void Lighting()
        {
            var sunGo = new GameObject("Morning Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.76f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sunGo.transform.rotation = Quaternion.Euler(38f, -58f, 0f); // low morning sun from the east, raking across the fronts
            sunGo.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = sun;

            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "MorningSky" };
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_AtmosphereThickness", 0.75f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.72f, 1f));
            sky.SetColor("_GroundColor", new Color(0.62f, 0.66f, 0.62f));
            sky.SetFloat("_Exposure", 1.25f);
            AssetDatabase.CreateAsset(sky, B.Gen + "Materials/MorningSky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.7f, 0.64f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.4f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.92f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 150f;
        }

        // ------------------------------------------------------------------ road, pavements, markings

        static void Road(float z0, float z1)
        {
            var ground = new GameObject("Road").transform;
            ground.SetParent(world);
            // build in 60 m chunks so the camera can cull what's behind
            for (float z = z0; z < z1; z += 60f)
            {
                float len = Mathf.Min(60f, z1 - z), zc = z + len / 2;
                var k = new MeshKit();
                k.Box(Mats.Tarmac, new Vector3(0, -0.1f, zc), new Vector3(RoadHalf * 2, 0.2f, len), 0.25f);
                foreach (int s in new[] { -1, 1 })
                {
                    float px = s * (RoadHalf + PavementOuter) / 2;
                    k.Box(Mats.Pavement, new Vector3(px, 0.065f, zc), new Vector3(PavementOuter - RoadHalf, 0.13f, len), 1f / 1.2f);
                    k.Box(Mats.Kerb, new Vector3(s * (RoadHalf + 0.075f), 0.075f, zc), new Vector3(0.15f, 0.15f, len));
                    // edge line
                    k.Box(Mats.Paint, new Vector3(s * (RoadHalf - 0.35f), 0.003f, zc), new Vector3(0.1f, 0.006f, len));
                }
                // dashed centre line
                for (float d = z; d < z + len; d += 6f)
                    k.Box(Mats.Paint, new Vector3(0, 0.003f, d + 1.5f), new Vector3(0.12f, 0.006f, 3f));
                var chunk = new GameObject($"Road {z:0}");
                chunk.transform.SetParent(ground);
                var (mesh, mats) = k.Build($"Road{z:0}");
                chunk.AddComponent<MeshFilter>().sharedMesh = B.SaveMesh(mesh, $"Road{(int)z}");
                chunk.AddComponent<MeshRenderer>().sharedMaterials = mats;
                chunk.layer = B.Ground;
                chunk.isStatic = true;
            }
            // colliders: road surface and both pavements
            Collider(ground, "RoadCollider", new Vector3(0, -0.1f, (z0 + z1) / 2), new Vector3(RoadHalf * 2, 0.2f, z1 - z0), B.Ground);
            foreach (int s in new[] { -1, 1 })
                Collider(ground, "Pavement", new Vector3(s * (RoadHalf + PavementOuter) / 2, 0.065f, (z0 + z1) / 2), new Vector3(PavementOuter - RoadHalf, 0.13f, z1 - z0), B.Ground);

            // zebra crossing with stripes, zig-zags and beacons
            var zk = new MeshKit();
            for (int i = 0; i < 7; i++) zk.Box(Mats.Paint, new Vector3(-3.0f + i * 1.0f, 0.004f, 262f), new Vector3(0.5f, 0.008f, 2.8f));
            foreach (int s in new[] { -1, 1 })
                for (int i = 0; i < 8; i++)
                {
                    float z = 262f - 5f - i * 1.2f;
                    zk.BoxRotated(Mats.Paint, new Vector3(s * 3.0f + ((i % 2 == 0) ? 0.18f : -0.18f), 0.004f, z), new Vector3(0.08f, 0.008f, 1.25f), Quaternion.Euler(0, (i % 2 == 0 ? 17f : -17f), 0));
                }
            // double yellow lines along the shop parade, "SLOW" by the crossing
            for (float z = 530f; z < 610f; z += 1f)
            {
                zk.Box(Mats.PaintYellow, new Vector3(-RoadHalf + 0.25f, 0.004f, z + 0.5f), new Vector3(0.08f, 0.008f, 1f));
                zk.Box(Mats.PaintYellow, new Vector3(-RoadHalf + 0.4f, 0.004f, z + 0.5f), new Vector3(0.08f, 0.008f, 1f));
            }
            var marks = new GameObject("Markings");
            marks.transform.SetParent(world);
            var (mm, mmats) = zk.Build("Markings");
            marks.AddComponent<MeshFilter>().sharedMesh = B.SaveMesh(mm, "Markings");
            marks.AddComponent<MeshRenderer>().sharedMaterials = mmats;
            Painted("SLOW", new Vector3(1.75f, 0.01f, 240f));
            Painted("SLOW", new Vector3(-1.75f, 0.01f, 284f), 180f);
            Painted("PRACTICE", new Vector3(0, 0.01f, -74f));
            foreach (int s in new[] { -1, 1 }) Place(Prefabs.Beacon, new Vector3(s * 3.9f, 0.13f, 260.2f), 0);
        }

        static void Painted(string text, Vector3 at, float yaw = 0f)
        {
            var go = new GameObject("Paint " + text);
            go.transform.SetParent(world);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, yaw, 0f));
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = AssetDatabase.LoadAssetAtPath<Font>(B.Root + "Resources/Fonts/NotoSansBlack.ttf");
            tm.fontSize = 80; tm.characterSize = 0.06f; tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 1f, 1f, 0.85f);
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            go.transform.localScale = new Vector3(1f, 2.2f, 1f); // stretched lettering, like real road paint
        }

        static BoxCollider Collider(Transform parent, string name, Vector3 c, Vector3 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.layer = layer;
            var col = go.AddComponent<BoxCollider>();
            col.center = c; col.size = size;
            return col;
        }

        /// <summary>Back gardens and trees beyond the houses, so the street doesn't float in space.</summary>
        static void Backdrop(float z0, float z1)
        {
            var k = new MeshKit();
            foreach (int s in new[] { -1, 1 })
                k.Box(Mats.Grass, new Vector3(s * 40f, -0.05f, (z0 + z1) / 2), new Vector3(56f, 0.1f, z1 - z0), 0.08f);
            // street ends: a row of houses across the road at both ends closes the view
            var end = new GameObject("Backdrop");
            end.transform.SetParent(world);
            var (m, mats) = k.Build("Backdrop");
            end.AddComponent<MeshFilter>().sharedMesh = B.SaveMesh(m, "Backdrop");
            end.AddComponent<MeshRenderer>().sharedMaterials = mats;
            var rnd = new System.Random(77);
            for (float z = z0; z < z1; z += 9f)
                foreach (int s in new[] { -1, 1 })
                {
                    float x = s * (15f + (float)rnd.NextDouble() * 10f);
                    Place(Prefabs.BackTree, new Vector3(x, 0, z + (float)rnd.NextDouble() * 6f), (float)rnd.NextDouble() * 360f);
                }
            for (int i = -1; i <= 1; i++)
            {
                // gardens facing back down the street, bodies running off beyond each end
                Place(Prefabs.Houses[(i + 3) % 10], new Vector3(i * W - W / 2, 0, z0 + 12f), 90f);
                Place(Prefabs.Houses[(i + 7) % 10], new Vector3(i * W + W / 2, 0, z1 - 12f), -90f);
            }
        }

        // ------------------------------------------------------------------ houses

        /// <summary>Place a house on a side (+1 right, -1 left) occupying z..z+W. Returns it.</summary>
        static House HouseAt(int side, float z, int variant, bool subscriber)
        {
            Vector3 pos = side > 0 ? new Vector3(PavementOuter, 0, z) : new Vector3(-PavementOuter, 0, z + W);
            int n = Prefabs.Houses.Length;
            var go = Place(Prefabs.Houses[((variant % n) + n) % n], pos, side > 0 ? 0f : 180f);
            var h = go.GetComponent<House>();
            // the delivery zone: the front garden, path and doorstep, in world space
            var a = go.transform.TransformPoint(new Vector3(0.3f, 0, 0));
            var b = go.transform.TransformPoint(new Vector3(4.45f, 0, W));
            h.Plot = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
            h.Subscriber = subscriber;
            if (subscriber)
            {
                h.Marker.gameObject.SetActive(true);
                h.MarkerRing.gameObject.SetActive(true);
            }
            houses.Add(h);
            return h;
        }

        static void Terraces()
        {
            // which houses take a paper: chosen by hand for a good rhythm (both sides, some pairs, some gaps)
            var subsRight = new HashSet<int> { 2, 7, 12, 17, 26, 33, 41, 47, 55, 64, 72, 80, 88, 97, 108, 120, 131 };
            var subsLeft = new HashSet<int> { 4, 10, 15, 22, 30, 38, 61, 70, 79, 91, 102, 116, 126, 139 };   // 102 falls in the shop parade, so 30 deliveries in all
            int nRight = 0, nLeft = 0;

            // practice lane: plain houses, one subscriber on the right
            for (float z = -118f; z < StartZ - 2f; z += W)
            {
                bool practice = z > -30f && z < -24f;
                var h = HouseAt(1, z, 3 + (int)(z / W), practice);
                if (practice) { houses.Remove(h); practiceHouse = h; h.Subscriber = false; }
                HouseAt(-1, z, 6 + (int)(z / W), false);
            }

            var rnd = new System.Random(2026);
            for (float z = StartZ + 0.5f; z < 940f; z += W)
            {
                int variant = rnd.Next(10);
                // right side: a driveway at ~430 for the reversing car; the dog's house at ~568
                if (z >= 427f && z < 427f + W)
                {
                    Place(Prefabs.Garage, new Vector3(PavementOuter, 0, z), 0f);
                    var car = Place(Prefabs.ReversingCar, new Vector3(PavementOuter + 2.6f, 0.14f, z + W / 2), 90f);
                    car.GetComponent<ReversingCar>().StopX = 1.4f;
                    hazards.Add(car.GetComponent<Hazard>());
                }
                else
                {
                    bool sub = z < FinishZ - 4f && subsRight.Contains(nRight);
                    var h = HouseAt(1, z, variant, sub);
                    if (z >= 565f && z < 565f + W)
                    {
                        var dog = Place(Prefabs.Dog, h.transform.TransformPoint(new Vector3(1.6f, 0.15f, 2.2f)), -90f);
                        hazards.Add(dog.GetComponent<Hazard>());
                    }
                }
                nRight++;

                // left side: park from 230 to 310, shops from 530 to 610
                if (z >= 228f && z < 312f) Park(z);
                else if (z >= 528f && z < 612f)
                {
                    int shop = (int)((z - 528f) / W) % 3;
                    Place(Prefabs.Shops[shop], new Vector3(-PavementOuter, 0, z + W), 180f);
                }
                else
                {
                    bool sub = z < FinishZ - 4f && subsLeft.Contains(nLeft);
                    HouseAt(-1, z, (variant + 5) % 10, sub);
                }
                nLeft++;
            }
        }

        static void Park(float z)
        {
            var k = new MeshKit();
            k.Box(Mats.Grass, new Vector3(-12f, 0.05f, z + W / 2), new Vector3(12.6f, 0.1f, W), 0.5f);
            var go = new GameObject("Park");
            go.transform.SetParent(world);
            var (m, mats) = k.Build("Park");
            go.AddComponent<MeshFilter>().sharedMesh = B.SaveMesh(m, "ParkGrass");
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            Collider(go.transform, "ParkGround", new Vector3(-12f, 0.05f, z + W / 2), new Vector3(12.6f, 0.1f, W), B.Ground);
            for (float r = z; r < z + W - 0.1f; r += 2.2f)
                if (!(r > 259f && r < 264f)) Place(Prefabs.Railing, new Vector3(-PavementOuter - 0.1f, 0.1f, r), 0);
            if ((int)(z / W) % 2 == 0) Place(Prefabs.Trees[(int)(z / 7) % 3], new Vector3(-9f - (z % 3), 0.1f, z + 2.5f), z * 13f);
            if ((int)(z / W) % 3 == 1) Place(Prefabs.Bench, new Vector3(-7.4f, 0.1f, z + 2.5f), -90f);
        }

        // ------------------------------------------------------------------ street furniture and hazards

        static void Furniture()
        {
            var rnd = new System.Random(99);
            // lamp posts near the kerb, alternating sides
            for (float z = -80f; z < 930f; z += 27f)
            {
                int s = ((int)(z / 27f) % 2 == 0) ? 1 : -1;
                if (z > 255f && z < 268f) continue;
                Place(Prefabs.LampPost, new Vector3(s * 3.95f, 0.13f, z), s > 0 ? 0f : 180f);
            }
            // pavement trees
            foreach (var (x, z) in new[] { (4.75f, 95f), (-4.75f, 160f), (4.75f, 205f), (4.75f, 335f), (-4.75f, 395f), (-4.75f, 470f), (4.75f, 640f), (-4.75f, 700f), (4.75f, 760f), (-4.75f, 830f) })
                Place(Prefabs.Trees[rnd.Next(3)], new Vector3(x, 0.13f, z), rnd.Next(360));
            // bin day: wheelie bins on the pavement, a few sticking out into the road
            foreach (var (x, z) in new[] { (4.4f, 58f), (-4.4f, 120f), (3.2f, 178f), (4.5f, 228f), (-3.2f, 352f), (4.4f, 380f), (-4.4f, 505f), (3.15f, 652f), (-4.4f, 682f), (4.4f, 742f), (-3.2f, 802f), (4.4f, 850f) })
                Place(Prefabs.Bins[rnd.Next(3)], new Vector3(x, x > -3.5f && x < 3.5f ? 0f : 0.13f, z), x > 0 ? -90f : 90f);
            // parked cars along the kerb (one side at a time, so the road stays open)
            foreach (var (x, z, yaw) in new[] { (2.6f, 30f, 0f), (-2.6f, 140f, 180f), (2.6f, 196f, 0f), (-2.6f, 340f, 180f), (2.6f, 372f, 0f), (-2.6f, 455f, 180f), (2.6f, 498f, 0f), (2.6f, 620f, 0f), (-2.6f, 668f, 180f), (2.6f, 712f, 0f), (-2.6f, 772f, 180f), (2.6f, 826f, 0f) })
                Place(Prefabs.Cars[rnd.Next(Prefabs.Cars.Length)], new Vector3(x, 0f, z), yaw);
            Place(Prefabs.PostBox, new Vector3(-4.6f, 0.13f, 545f), 90f);
            Place(Prefabs.PostBox, new Vector3(4.6f, 0.13f, 300f), -90f);
            foreach (int s in new[] { -1, 1 }) { Place(Prefabs.Bollard, new Vector3(s * 4.0f, 0.13f, 258.6f), 0); Place(Prefabs.Bollard, new Vector3(s * 4.0f, 0.13f, 265.4f), 0); }

            // practice cones: two gates to ride through
            foreach (var (x, z) in new[] { (-3.3f, -56f), (-0.3f, -56f), (0.3f, -44f), (3.3f, -44f) }) Place(Prefabs.Cone, new Vector3(x, 0, z), 0);

            // potholes
            foreach (var (x, z) in new[] { (1.6f, 365f), (1.4f, 652f), (-1.6f, 724f), (0.6f, 796f), (-0.8f, 858f) })
            {
                var p = Place(Prefabs.Pothole, new Vector3(x, 0f, z), rnd.Next(360));
                potholes.Add(p.GetComponent<Pothole>());
            }

            // people: one crossing at the zebra, three strolling on the pavements
            var crosser = Place(Prefabs.People[0], new Vector3(-4.7f, 0.13f, 262.2f), 90f).GetComponent<Pedestrian>();
            crosser.Behaviour = Pedestrian.Kind.Crosser;
            crosser.WalkTo = new Vector3(4.7f, 0.13f, 262.2f);
            crosser.WarnDistance = 44f; crosser.ActDistance = 33f;
            hazards.Add(crosser);
            foreach (var (p, from, to) in new[] { (1, new Vector3(4.6f, 0.13f, 150f), new Vector3(4.6f, 0.13f, 175f)), (2, new Vector3(-4.6f, 0.13f, 420f), new Vector3(-4.6f, 0.13f, 445f)), (3, new Vector3(-4.7f, 0.13f, 560f), new Vector3(-4.7f, 0.13f, 590f)) })
            {
                var walker = Place(Prefabs.People[p], from, 0).GetComponent<Pedestrian>();
                walker.Behaviour = Pedestrian.Kind.Stroller;
                walker.WalkTo = to;
                walker.WalkSpeed = 1.1f;
                hazards.Add(walker);
            }

            Place(Prefabs.StartArch, new Vector3(0, 0, StartZ), 0);
            Place(Prefabs.FinishArch, new Vector3(0, 0, FinishZ), 0);
        }

        // ------------------------------------------------------------------ cameras, managers, UI

        static void Systems(GameObject cyclist, VolumeProfile profile)
        {
            var bike = cyclist.GetComponent<BikeController>();
            var thrower = cyclist.GetComponent<ThrowController>();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 160f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<CinemachineBrain>();
            camGo.transform.position = new Vector3(0, 8, -14);

            var cmGo = new GameObject("CM Rider Camera");
            var cm = cmGo.AddComponent<CinemachineCamera>();
            cm.Follow = cyclist.transform;
            cm.LookAt = cyclist.transform;
            cm.Lens = new LensSettings { FieldOfView = 50f, NearClipPlane = 0.3f, FarClipPlane = 160f, Dutch = 0f, ModeOverride = LensSettings.OverrideModes.None };
            var follow = cmGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 7.6f, -9.2f);
            var ts = follow.TrackerSettings;
            ts.BindingMode = BindingMode.WorldSpace;
            ts.PositionDamping = new Vector3(0.9f, 0.5f, 0.35f);
            follow.TrackerSettings = ts;
            var composer = cmGo.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = new Vector3(0f, 0f, 7.5f);  // look ahead down the street
            composer.Damping = new Vector2(0.6f, 0.5f);
            var listener = cmGo.AddComponent<CinemachineImpulseListener>();
            listener.Gain = 1f;

            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            // aiming preview
            var arcGo = new GameObject("Throw Arc");
            var arc = arcGo.AddComponent<LineRenderer>();
            arc.sharedMaterial = Mats.ArcLine;
            arc.widthCurve = new AnimationCurve(new Keyframe(0, 0.1f), new Keyframe(1, 0.24f));
            arc.numCapVertices = 4;
            arc.shadowCastingMode = ShadowCastingMode.Off;
            arc.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.9f, 0.4f), 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.45f, 0), new GradientAlphaKey(1f, 1) },
            };
            var retKit = new MeshKit();
            retKit.Plane(Mats.Reticle, Vector3.zero, new Vector2(1.6f, 1.6f), Vector3.up, Vector3.forward);
            var reticle = new GameObject("Throw Target");
            var (rm, rmats) = retKit.Build("Reticle");
            reticle.AddComponent<MeshFilter>().sharedMesh = B.SaveMesh(rm, "Reticle");
            var rr = reticle.AddComponent<MeshRenderer>();
            rr.sharedMaterials = rmats;
            rr.shadowCastingMode = ShadowCastingMode.Off;

            var systems = new GameObject("Game");
            var pool = new GameObject("Paper Pool").AddComponent<PaperPool>();
            pool.transform.SetParent(systems.transform);
            pool.Prefab = Prefabs.Newspaper.GetComponent<Newspaper>();
            pool.Size = 24;
            thrower.Cam = cam; thrower.Arc = arc; thrower.Reticle = reticle.transform; thrower.Pool = pool;

            var route = systems.AddComponent<RouteManager>();
            route.StartZ = StartZ; route.FinishZ = FinishZ; route.PracticeStartZ = PracticeZ + 6f;
            route.ParSeconds = 150f;
            route.Houses = houses.ToArray();
            route.PracticeHouse = practiceHouse;
            route.Potholes = potholes.ToArray();
            route.DeliveredMarkerMaterial = Mats.MarkerDone;

            var fx = systems.AddComponent<Effects>();
            fx.Confetti = Particles("Confetti", Mats.Confetti, 0.09f, 1.6f, 4.5f, 0.8f, new Color(1f, 0.4f, 0.5f), new Color(0.4f, 0.8f, 1f), true);
            fx.Sparkle = Particles("Sparkle", Mats.Spark, 0.25f, 0.8f, 2.5f, -0.1f, new Color(1f, 0.9f, 0.4f), new Color(1f, 1f, 0.8f), false);
            fx.Glass = Particles("Glass", Mats.GlassShard, 0.06f, 1.2f, 3.5f, 1.4f, new Color(0.8f, 0.92f, 1f), new Color(1f, 1f, 1f), true);
            fx.Dust = Particles("Dust", Mats.Dust, 0.5f, 1.0f, 1.4f, -0.05f, new Color(0.75f, 0.7f, 0.62f, 0.6f), new Color(0.85f, 0.82f, 0.76f, 0.4f), false);
            foreach (var ps in new[] { fx.Confetti, fx.Sparkle, fx.Glass, fx.Dust }) ps.transform.SetParent(systems.transform);

            var sfx = systems.AddComponent<Sfx>();
            sfx.Bike = bike;
            var ui = systems.AddComponent<UIManager>();
            ui.Cam = cam;

            var game = systems.AddComponent<GameFlow>();
            game.Bike = bike; game.Thrower = thrower; game.Pool = pool; game.Route = route; game.Ui = ui; game.Audio = sfx; game.Fx = fx;
            game.Hazards = hazards.ToArray();
            game.Impulse = cyclist.GetComponent<CinemachineImpulseSource>();
            game.ImpulseListener = listener;
            // automated testing hook on its own object (it can outlive scene reloads during tests)
            var qa = new GameObject("QA").AddComponent<QaDriver>();
            qa.Game = game;
        }

        static ParticleSystem Particles(string name, Material mat, float size, float life, float speed, float gravity, Color a, Color b, bool spin)
        {
            var go = new GameObject(name + " FX");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            if (spin) { main.startRotation3D = false; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); }
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0.2f));
            if (spin) { var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-6f, 6f); }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }
    }
}
