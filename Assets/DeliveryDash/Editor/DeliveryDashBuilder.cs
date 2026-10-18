using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeliveryDash.EditorTools
{
    /// <summary>
    /// Builds the whole game from code: project settings, textures, materials, meshes, prefabs, the
    /// street scene, and the desktop / browser players.
    ///   Menu:   Delivery Dash > Build Everything
    ///   Batch:  Unity -batchmode -nographics -projectPath . -executeMethod DeliveryDash.EditorTools.DeliveryDashBuilder.GenerateAndBuildLinux -quit
    /// </summary>
    public static class DeliveryDashBuilder
    {
        public const string Root = "Assets/DeliveryDash/";
        public const string Gen = Root + "Generated/";
        public const string PrefabDir = Root + "Prefabs/";
        public const string ScenePath = Root + "Scenes/DeliveryDash.unity";
        const string LinuxPath = "Build/Linux/DeliveryDash.x86_64";
        public const string WebGLPath = "Build/WebGL";

        // physics layers
        public const int Ground = 8, Building = 9, Window = 10, LetterboxLayer = 11, Obstacle = 12, Character = 13, Paper = 14, Rider = 15;

        [MenuItem("Delivery Dash/Build Everything (assets + scene)")]
        public static void GenerateAll()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ConfigureProject();
            foreach (var d in new[] { Gen, Gen + "Meshes", Gen + "Materials", PrefabDir, Root + "Scenes", Root + "Settings" }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            Mats.Make();
            MakePipelines();
            var profile = MakeVolumeProfile();
            Prefabs.MakeAll();
            StreetBuilder.Build(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"DD_GENERATE_OK in {sw.Elapsed.TotalSeconds:F1}s");
        }

        [MenuItem("Delivery Dash/Build Linux Player")]
        public static void BuildLinux()
        {
            ConfigureProject();
            Directory.CreateDirectory("Build/Linux");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = LinuxPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
            Debug.Log($"DD_BUILD_OK size={report.summary.totalSize / (1024 * 1024)}MB");
        }

        public static void GenerateAndBuildLinux() { GenerateAll(); BuildLinux(); }

        [MenuItem("Delivery Dash/Build Web (WebGL) Player")]
        public static void BuildWebGL()
        {
            ConfigureProject();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true; // browsers can't mix cached files from different builds
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.template = "PROJECT:DeliveryDash";
            PlayerSettings.runInBackground = false;
            if (Directory.Exists(WebGLPath)) Directory.Delete(WebGLPath, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = WebGLPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed: " + report.summary.result);
            Debug.Log("DD_WEBGL_OK");
        }

        public static void GenerateAndBuildWebGL() { GenerateAll(); BuildWebGL(); }

        // ------------------------------------------------------------------ project settings

        static void ConfigureProject()
        {
            PlayerSettings.companyName = "Host Hobbit";
            PlayerSettings.productName = "Delivery Dash";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore });
            try { PlayerSettings.SplashScreen.show = false; } catch { }
            var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var input = so.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 1; so.ApplyModifiedPropertiesWithoutUndo(); } // Input System only

            // physics layers
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            void L(int i, string n) { layers.GetArrayElementAtIndex(i).stringValue = n; }
            L(Ground, "Ground"); L(Building, "Building"); L(Window, "Window"); L(LetterboxLayer, "Letterbox");
            L(Obstacle, "Obstacle"); L(Character, "Character"); L(Paper, "Paper"); L(Rider, "Rider");
            tags.ApplyModifiedPropertiesWithoutUndo();
            Physics.queriesHitBackfaces = false;
            Physics.autoSyncTransforms = true;   // kinematic movers: queries see moved transforms
        }

        // ------------------------------------------------------------------ render pipelines + quality presets

        static void MakePipelines()
        {
            var renderer = Recreate(Root + "Settings/Renderer.asset", () => ScriptableObject.CreateInstance<UniversalRendererData>());
            renderer.renderingMode = RenderingMode.Forward;
            EditorUtility.SetDirty(renderer);

            UniversalRenderPipelineAsset Pipe(string name, float shadowDist, int msaa, int shadowRes, bool soft, int cascades, float scale)
            {
                string path = Root + $"Settings/URP_{name}.asset";
                if (AssetDatabase.LoadMainAssetAtPath(path)) AssetDatabase.DeleteAsset(path);
                var urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, path);
                urp.renderScale = scale;
                urp.msaaSampleCount = msaa;
                urp.supportsHDR = true;
                urp.shadowDistance = shadowDist;
                urp.shadowCascadeCount = cascades;
                urp.mainLightShadowmapResolution = shadowRes;
                var so = new SerializedObject(urp);
                SetBool(so, "m_SoftShadowsSupported", soft);
                SetInt(so, "m_SoftShadowQuality", soft ? 2 : 1);
                SetBool(so, "m_SupportsTerrainHoles", false);
                SetFloat(so, "m_ShadowDepthBias", 0.7f);
                SetFloat(so, "m_ShadowNormalBias", 0.5f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(urp);
                return urp;
            }
            var low = Pipe("Low", 28f, 1, 1024, false, 1, 0.85f);
            var med = Pipe("Medium", 45f, 2, 2048, true, 2, 1f);
            var high = Pipe("High", 70f, 4, 4096, true, 2, 1f);
            GraphicsSettings.defaultRenderPipeline = high;

            // three quality levels: Low, Medium, High, each with its own pipeline asset
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var list = qs.FindProperty("m_QualitySettings");
            while (list.arraySize < 3) list.InsertArrayElementAtIndex(list.arraySize);
            while (list.arraySize > 3) list.DeleteArrayElementAtIndex(list.arraySize - 1);
            var pipes = new[] { low, med, high };
            string[] names = { "Low", "Medium", "High" };
            for (int i = 0; i < 3; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = names[i];
                e.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipes[i];
                var v = e.FindPropertyRelative("vSyncCount"); if (v != null) v.intValue = 1;
                var lod = e.FindPropertyRelative("lodBias"); if (lod != null) lod.floatValue = i == 0 ? 0.7f : i == 1 ? 1f : 1.5f;
                var aniso = e.FindPropertyRelative("anisotropicTextures"); if (aniso != null) aniso.intValue = i == 0 ? 0 : 2;
                var soft = e.FindPropertyRelative("softParticles"); if (soft != null) soft.boolValue = false;
                var tex = e.FindPropertyRelative("globalTextureMipmapLimit"); if (tex != null) tex.intValue = i == 0 ? 1 : 0;
            }
            qs.FindProperty("m_CurrentQuality").intValue = 2;
            var per = qs.FindProperty("m_PerPlatformDefaultQuality");
            if (per != null)
                for (int i = 0; i < per.arraySize; i++)
                {
                    var e = per.GetArrayElementAtIndex(i);
                    string plat = e.FindPropertyRelative("first").stringValue;
                    e.FindPropertyRelative("second").intValue = plat == "WebGL" ? 1 : 2;
                }
            qs.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static T Recreate<T>(string path, Func<T> make) where T : UnityEngine.Object
        {
            if (AssetDatabase.LoadMainAssetAtPath(path)) AssetDatabase.DeleteAsset(path);
            var a = make();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void SetBool(SerializedObject so, string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; }
        static void SetFloat(SerializedObject so, string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; }
        static void SetInt(SerializedObject so, string n, int v) { var p = so.FindProperty(n); if (p == null) return; if (p.propertyType == SerializedPropertyType.Enum) p.enumValueFlag = v; else p.intValue = v; }

        static VolumeProfile MakeVolumeProfile()
        {
            string path = Root + "Settings/MorningGrade.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path)) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f);
            var ca = profile.Add<ColorAdjustments>();
            ca.postExposure.Override(0.25f); ca.contrast.Override(8f); ca.saturation.Override(14f);
            var wb = profile.Add<WhiteBalance>(); wb.temperature.Override(9f); wb.tint.Override(2f);
            var vig = profile.Add<Vignette>(); vig.intensity.Override(0.16f); vig.smoothness.Override(0.6f);
            var smh = profile.Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.95f, 0.97f, 1.08f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1.0f, 0.94f, 0f));
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public static Mesh SaveMesh(Mesh m, string name)
        {
            string path = Gen + "Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(m, existing); UnityEngine.Object.DestroyImmediate(m); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }

    /// <summary>Every material, made once. URP Lit for the world (one consistent, soft "storybook" palette).</summary>
    public static class Mats
    {
        static Shader lit, unlit, particles;
        public static Material BrickRed, BrickYellow, BrickBrown, RenderCream, RenderSage, RenderBlue, RenderPink, RenderLilac,
            Slate, SlateDark, Tile, Trim, Sill, Stone, Coping, Glass, GlassBroken, Path, Pavement, Kerb, Tarmac, Grass, Hedge, Leaves, LeavesAutumn,
            Bark, Wood, WoodPale, Metal, Black, White, Brass, Rubber, Chrome, LampGlow, Paint, PaintYellow, Marker, MarkerDone, MarkerRing,
            ArcLine, Reticle, Warning, Newsprint, NewsBand, Shadow, Chequer, Flower1, Flower2, Flower3, Flower4, Soil, Cone, Indicator, IndicatorOff,
            ReverseOff, Headlight, Brake, BeaconOn, Cloth1, Cloth2, Cloth3, Cloth4, Cloth5, Skin1, Skin2, Skin3, Hair1, Hair2, Hair3, DogBrown, DogWhite, DogCollar,
            BikeFrame, BikeSeat, Jacket, Trousers, Cap, Bag, Confetti, Spark, Dust, GlassShard, Sign, SignText, Awning1, Awning2, Awning3, ShopWindow, Railing, Bollard;
        public static Material[] Doors, CarPaints, WallSet;

        public static Material Lit(string name, Color colour, Texture2D tex = null, float smooth = 0.25f, float metallic = 0f, Color? emission = null)
        {
            var m = new Material(lit) { name = name };
            m.SetColor("_BaseColor", colour);
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            m.enableInstancing = true;
            return Save(m);
        }

        public static Material Unlit(string name, Color colour, Texture2D tex = null, bool transparent = false)
        {
            var m = new Material(unlit) { name = name };
            m.SetColor("_BaseColor", colour);
            if (tex) m.SetTexture("_BaseMap", tex);
            if (transparent) MakeTransparent(m);
            return Save(m);
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static Material Particle(string name, Texture2D tex, bool additive = false)
        {
            var m = new Material(particles) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return Save(m);
        }

        static Material Save(Material m)
        {
            string path = DeliveryDashBuilder.Gen + "Materials/" + m.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { existing.shader = m.shader; existing.CopyPropertiesFromMaterial(m); existing.shaderKeywords = m.shaderKeywords; existing.renderQueue = m.renderQueue; EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static void Make()
        {
            lit = Shader.Find("Universal Render Pipeline/Lit");
            unlit = Shader.Find("Universal Render Pipeline/Unlit");
            particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!lit || !unlit || !particles) throw new Exception("URP shaders not found");

            var brickA = TextureKit.Brick("BrickRed", new Color(0.72f, 0.33f, 0.24f), new Color(0.6f, 0.26f, 0.2f), new Color(0.86f, 0.82f, 0.74f), 1);
            var brickB = TextureKit.Brick("BrickYellow", new Color(0.86f, 0.72f, 0.5f), new Color(0.74f, 0.6f, 0.42f), new Color(0.92f, 0.9f, 0.84f), 2);
            var brickC = TextureKit.Brick("BrickBrown", new Color(0.55f, 0.36f, 0.28f), new Color(0.45f, 0.29f, 0.24f), new Color(0.8f, 0.77f, 0.7f), 3);
            var render = TextureKit.Render("Render", 4);
            var slate = TextureKit.Slate("Slate", 5);
            var tarmac = TextureKit.Tarmac("Tarmac", 6);
            var paving = TextureKit.Paving("Paving", new Color(0.78f, 0.76f, 0.72f), 7);
            var pathTex = TextureKit.Paving("PathStone", new Color(0.86f, 0.78f, 0.66f), 8);
            var grass = TextureKit.Grass("Grass", 9);
            var leaves = TextureKit.Leaves("Leaves", 10);
            var wood = TextureKit.Wood("Wood", 11);
            var news = TextureKit.Newsprint("Newsprint");
            var soft = TextureKit.Soft("Soft");
            var ring = TextureKit.Ring("Ring");
            var square = TextureKit.Square("Square");
            var warning = TextureKit.Warning("Warning");
            var cracked = TextureKit.Cracked("Cracked");
            var chequer = TextureKit.Chequer("Chequer");

            BrickRed = Lit("BrickRed", Color.white, brickA, 0.12f);
            BrickYellow = Lit("BrickYellow", Color.white, brickB, 0.12f);
            BrickBrown = Lit("BrickBrown", Color.white, brickC, 0.12f);
            RenderCream = Lit("RenderCream", new Color(0.98f, 0.92f, 0.78f), render, 0.15f);
            RenderSage = Lit("RenderSage", new Color(0.7f, 0.82f, 0.68f), render, 0.15f);
            RenderBlue = Lit("RenderBlue", new Color(0.66f, 0.8f, 0.92f), render, 0.15f);
            RenderPink = Lit("RenderPink", new Color(0.98f, 0.76f, 0.74f), render, 0.15f);
            RenderLilac = Lit("RenderLilac", new Color(0.8f, 0.74f, 0.9f), render, 0.15f);
            WallSet = new[] { BrickRed, BrickYellow, RenderCream, BrickBrown, RenderSage, BrickRed, RenderBlue, BrickYellow, RenderPink, RenderLilac };
            Slate = Lit("Slate", new Color(0.82f, 0.86f, 0.95f), slate, 0.35f);
            SlateDark = Lit("SlateDark", new Color(0.6f, 0.62f, 0.7f), slate, 0.35f);
            Tile = Lit("RoofTile", new Color(0.86f, 0.5f, 0.38f), slate, 0.3f);
            Trim = Lit("Trim", new Color(0.97f, 0.96f, 0.93f), null, 0.4f);
            Sill = Lit("Sill", new Color(0.88f, 0.86f, 0.8f), null, 0.2f);
            Stone = Lit("Stone", new Color(0.84f, 0.81f, 0.74f), paving, 0.15f);
            Coping = Lit("Coping", new Color(0.8f, 0.78f, 0.72f), null, 0.15f);
            Glass = Lit("Glass", new Color(0.42f, 0.56f, 0.68f), null, 0.92f, 0.1f);
            GlassBroken = Lit("GlassBroken", Color.white, cracked, 0.5f);
            Path = Lit("Path", Color.white, pathTex, 0.15f);
            Pavement = Lit("Pavement", Color.white, paving, 0.12f);
            Kerb = Lit("Kerb", new Color(0.76f, 0.75f, 0.72f), null, 0.1f);
            Tarmac = Lit("Tarmac", Color.white, tarmac, 0.18f);
            Grass = Lit("Grass", Color.white, grass, 0.05f);
            Hedge = Lit("Hedge", new Color(0.85f, 0.95f, 0.85f), leaves, 0.05f);
            Leaves = Lit("TreeLeaves", Color.white, leaves, 0.05f);
            LeavesAutumn = Lit("TreeLeavesWarm", new Color(1f, 0.85f, 0.55f), leaves, 0.05f);
            Bark = Lit("Bark", new Color(0.42f, 0.32f, 0.24f), wood, 0.1f);
            Wood = Lit("Wood", new Color(0.62f, 0.45f, 0.3f), wood, 0.2f);
            WoodPale = Lit("WoodPale", new Color(0.86f, 0.78f, 0.64f), wood, 0.2f);
            Metal = Lit("Metal", new Color(0.25f, 0.27f, 0.3f), null, 0.5f, 0.6f);
            Black = Lit("Black", new Color(0.09f, 0.09f, 0.1f), null, 0.4f);
            White = Lit("White", new Color(0.95f, 0.95f, 0.93f), null, 0.3f);
            Brass = Lit("Brass", new Color(0.9f, 0.7f, 0.3f), null, 0.75f, 0.9f);
            Rubber = Lit("Rubber", new Color(0.08f, 0.08f, 0.09f), null, 0.2f);
            Chrome = Lit("Chrome", new Color(0.85f, 0.86f, 0.9f), null, 0.85f, 1f);
            LampGlow = Lit("LampGlow", new Color(1f, 0.95f, 0.8f), null, 0.5f, 0f, new Color(1.6f, 1.4f, 1f));
            Paint = Lit("RoadPaint", new Color(0.95f, 0.95f, 0.92f), null, 0.3f);
            PaintYellow = Lit("RoadPaintYellow", new Color(0.98f, 0.82f, 0.2f), null, 0.3f);
            Doors = new[]
            {
                Lit("DoorRed", new Color(0.8f, 0.17f, 0.17f), null, 0.6f), Lit("DoorNavy", new Color(0.13f, 0.2f, 0.42f), null, 0.6f),
                Lit("DoorGreen", new Color(0.13f, 0.42f, 0.3f), null, 0.6f), Lit("DoorYellow", new Color(0.98f, 0.76f, 0.18f), null, 0.6f),
                Lit("DoorBlack", new Color(0.1f, 0.1f, 0.12f), null, 0.7f), Lit("DoorTeal", new Color(0.12f, 0.55f, 0.6f), null, 0.6f),
                Lit("DoorLilac", new Color(0.6f, 0.45f, 0.75f), null, 0.6f), Lit("DoorSky", new Color(0.45f, 0.7f, 0.92f), null, 0.6f),
            };
            CarPaints = new[]
            {
                Lit("CarRed", new Color(0.85f, 0.18f, 0.16f), null, 0.75f, 0.2f), Lit("CarBlue", new Color(0.2f, 0.42f, 0.8f), null, 0.75f, 0.2f),
                Lit("CarMint", new Color(0.55f, 0.85f, 0.75f), null, 0.75f, 0.2f), Lit("CarYellow", new Color(0.98f, 0.8f, 0.25f), null, 0.75f, 0.2f),
                Lit("CarSilver", new Color(0.75f, 0.77f, 0.8f), null, 0.8f, 0.4f), Lit("CarOrange", new Color(0.95f, 0.5f, 0.2f), null, 0.75f, 0.2f),
            };
            Marker = Unlit("Marker", new Color(1f, 0.86f, 0.25f));
            MarkerDone = Unlit("MarkerDone", new Color(0.4f, 0.9f, 0.5f, 0.8f), ring, true);
            MarkerRing = Unlit("MarkerRing", new Color(1f, 0.86f, 0.25f, 0.85f), ring, true);
            ArcLine = Unlit("ArcLine", new Color(1f, 1f, 1f, 0.85f), soft, true);
            Reticle = Unlit("Reticle", new Color(1f, 0.95f, 0.4f, 0.95f), ring, true);
            Warning = Unlit("WarningSign", Color.white, warning, true);
            Newsprint = Lit("Newsprint", Color.white, news, 0.1f);
            NewsBand = Lit("NewsBand", new Color(0.85f, 0.2f, 0.2f), null, 0.3f);
            Shadow = Unlit("BlobShadow", new Color(0, 0, 0, 0.35f), soft, true);
            Chequer = Lit("Chequer", Color.white, chequer, 0.2f);
            Flower1 = Lit("FlowerPink", new Color(1f, 0.45f, 0.65f), null, 0.3f);
            Flower2 = Lit("FlowerYellow", new Color(1f, 0.85f, 0.25f), null, 0.3f);
            Flower3 = Lit("FlowerPurple", new Color(0.62f, 0.4f, 0.9f), null, 0.3f);
            Flower4 = Lit("FlowerWhite", new Color(0.97f, 0.96f, 0.93f), null, 0.3f);
            Soil = Lit("Soil", new Color(0.36f, 0.26f, 0.2f), null, 0.05f);
            Cone = Lit("Cone", new Color(1f, 0.45f, 0.1f), null, 0.4f);
            Indicator = Lit("LightOn", new Color(1f, 0.7f, 0.2f), null, 0.6f, 0f, new Color(4f, 2.4f, 0.6f));
            IndicatorOff = Lit("IndicatorOff", new Color(0.8f, 0.5f, 0.2f), null, 0.7f);
            ReverseOff = Lit("ReverseOff", new Color(0.85f, 0.85f, 0.85f), null, 0.7f);
            Headlight = Lit("Headlight", new Color(1f, 1f, 0.9f), null, 0.9f, 0f, new Color(0.6f, 0.6f, 0.5f));
            Brake = Lit("TailLight", new Color(0.8f, 0.1f, 0.1f), null, 0.8f, 0f, new Color(0.6f, 0.05f, 0.05f));
            BeaconOn = Lit("Beacon", new Color(1f, 0.6f, 0.15f), null, 0.6f, 0f, new Color(3f, 1.4f, 0.2f));
            Cloth1 = Lit("ClothRed", new Color(0.86f, 0.3f, 0.3f), null, 0.15f);
            Cloth2 = Lit("ClothBlue", new Color(0.3f, 0.46f, 0.8f), null, 0.15f);
            Cloth3 = Lit("ClothGreen", new Color(0.36f, 0.66f, 0.42f), null, 0.15f);
            Cloth4 = Lit("ClothMustard", new Color(0.88f, 0.7f, 0.25f), null, 0.15f);
            Cloth5 = Lit("ClothDenim", new Color(0.28f, 0.33f, 0.48f), null, 0.15f);
            Skin1 = Lit("Skin1", new Color(0.98f, 0.8f, 0.66f), null, 0.3f);
            Skin2 = Lit("Skin2", new Color(0.78f, 0.56f, 0.4f), null, 0.3f);
            Skin3 = Lit("Skin3", new Color(0.48f, 0.32f, 0.24f), null, 0.3f);
            Hair1 = Lit("Hair1", new Color(0.3f, 0.2f, 0.13f), null, 0.3f);
            Hair2 = Lit("Hair2", new Color(0.86f, 0.62f, 0.3f), null, 0.3f);
            Hair3 = Lit("Hair3", new Color(0.82f, 0.82f, 0.85f), null, 0.3f);
            DogBrown = Lit("DogBrown", new Color(0.66f, 0.42f, 0.24f), null, 0.2f);
            DogWhite = Lit("DogWhite", new Color(0.96f, 0.93f, 0.86f), null, 0.2f);
            DogCollar = Lit("DogCollar", new Color(0.85f, 0.15f, 0.2f), null, 0.4f);
            BikeFrame = Lit("BikeFrame", new Color(0.12f, 0.6f, 0.68f), null, 0.75f, 0.3f);
            BikeSeat = Lit("BikeSeat", new Color(0.32f, 0.2f, 0.14f), null, 0.4f);
            Jacket = Lit("Jacket", new Color(1f, 0.55f, 0.15f), null, 0.25f);
            Trousers = Lit("Trousers", new Color(0.2f, 0.25f, 0.4f), null, 0.2f);
            Cap = Lit("Cap", new Color(0.18f, 0.42f, 0.85f), null, 0.3f);
            Bag = Lit("Satchel", new Color(0.86f, 0.25f, 0.2f), null, 0.3f);
            Sign = Lit("Signboard", new Color(0.15f, 0.3f, 0.5f), null, 0.4f);
            SignText = Lit("SignText", new Color(1f, 0.95f, 0.85f), null, 0.4f);
            Awning1 = Lit("AwningRed", new Color(0.86f, 0.25f, 0.25f), null, 0.2f);
            Awning2 = Lit("AwningGreen", new Color(0.2f, 0.56f, 0.4f), null, 0.2f);
            Awning3 = Lit("AwningBlue", new Color(0.25f, 0.45f, 0.75f), null, 0.2f);
            ShopWindow = Lit("ShopWindow", new Color(0.5f, 0.62f, 0.72f), null, 0.9f, 0f, new Color(0.25f, 0.22f, 0.16f));
            Railing = Lit("Railing", new Color(0.12f, 0.16f, 0.14f), null, 0.5f, 0.3f);
            Bollard = Lit("Bollard", new Color(0.15f, 0.15f, 0.18f), null, 0.4f);

            Confetti = Particle("Confetti", square);
            Spark = Particle("Sparkle", soft, true);
            Dust = Particle("Dust", soft);
            GlassShard = Particle("GlassShard", square);
            AssetDatabase.SaveAssets();
        }
    }
}
