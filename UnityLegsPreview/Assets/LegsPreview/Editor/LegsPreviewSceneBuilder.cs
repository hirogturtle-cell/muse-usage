using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LegsPreview.EditorTools
{
    /// <summary>
    /// Generates the whole preview scene: procedural legs (skinned, 2 bones each), pelvis, waistband,
    /// a Cloth skirt with capsule colliders on both legs, stocking/fabric materials, a studio cyclorama,
    /// three-point lighting with softboxes, a reflection probe and an orbit camera.
    ///
    /// Menu:  Tools ▸ Legs Preview ▸ Build Scene
    /// CLI:   Unity -batchmode -projectPath . -executeMethod LegsPreview.EditorTools.LegsPreviewSceneBuilder.BuildFromCommandLine -quit
    /// </summary>
    [InitializeOnLoad]
    public static class LegsPreviewSceneBuilder
    {
        public const string Root = "Assets/LegsPreview";
        public const string ScenePath = Root + "/Scenes/LegsPreview.unity";
        const string GenDir = Root + "/Generated";

        static LegsPreviewSceneBuilder()
        {
            // First open of the project: build the scene automatically so there is something to press Play on.
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ScenePath)) Build(openAfter: true);
            };
        }

        [MenuItem("Tools/Legs Preview/Build Scene", priority = 0)]
        public static void BuildMenu() => Build(openAfter: true);

        [MenuItem("Tools/Legs Preview/Build Scene and Play", priority = 1)]
        public static void BuildAndPlay()
        {
            Build(openAfter: true);
            EditorApplication.isPlaying = true;
        }

        /// <summary>Called from the Play-mode test's prebuild step (via reflection) and from CI.</summary>
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath)) Build(openAfter: false);
            AddToBuildSettings();
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build(openAfter: false);
                Debug.Log("[LegsPreview] Scene built: " + ScenePath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------------------------------------

        public static void Build(bool openAfter)
        {
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(GenDir);
            ConfigureProject();

            bool builtIn = GraphicsSettings.currentRenderPipeline == null;
            var mats = CreateMaterials(builtIn);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- figure
            var root = new GameObject("LegsPreview");
            var hips = new GameObject("Hips").transform;
            hips.SetParent(root.transform, false);

            var legL = BuildLeg(hips, left: true, mats.stocking, out var thighL, out var shinL, out var capsL);
            var legR = BuildLeg(hips, left: false, mats.stocking, out var thighR, out var shinR, out var capsR);

            MeshObject("Pelvis (underskirt shorts)", hips, SaveMesh(LegGeometry.BuildPelvis(), "Pelvis"), mats.lining);
            MeshObject("Waistband", hips, SaveMesh(LegGeometry.BuildWaistband(), "Waistband"), mats.skirt);

            // hip/seat collider pair keeps the skirt from sinking into the pelvis
            var hipColL = SphereCol("ClothCollider_Hip_L", hips, new Vector3(-0.060f, 0.90f, 0.012f), 0.135f);
            var hipColR = SphereCol("ClothCollider_Hip_R", hips, new Vector3(0.060f, 0.90f, 0.012f), 0.135f);

            // ---- skirt cloth
            var skirtGO = new GameObject("Skirt (Cloth)");
            skirtGO.transform.SetParent(hips, false);
            var smr = skirtGO.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = SaveMesh(LegGeometry.BuildSkirt(), "Skirt");
            smr.sharedMaterial = mats.skirt;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = ShadowCastingMode.On;
            var cloth = skirtGO.AddComponent<Cloth>();
            cloth.capsuleColliders = capsL.Concat(capsR).ToArray();
            cloth.sphereColliders = new[] { new ClothSphereColliderPair(hipColL, hipColR) };
            var skirt = skirtGO.AddComponent<SkirtCloth>();
            skirt.ApplyParameters();
            skirt.ApplyCoefficients(); // Start() re-applies at runtime in case welding differs
            var monitor = skirtGO.AddComponent<ClothHealthMonitor>();

            var anim = root.AddComponent<LegPoseAnimator>();
            anim.hips = hips;
            anim.thighL = thighL; anim.shinL = shinL;
            anim.thighR = thighR; anim.shinR = shinR;

            // ---- studio
            var studio = new GameObject("Studio").transform;
            var cyc = MeshObject("Cyclorama", studio, SaveMesh(LegGeometry.BuildCyclorama(), "Cyclorama"), mats.backdrop);
            cyc.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            BuildLighting(studio, mats.softbox);

            // ---- camera
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 26f;          // ≈ 85 mm portrait lens on full frame (vertical)
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 30f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.18f, 0.19f);
            camGO.AddComponent<AudioListener>();
            var orbit = camGO.AddComponent<StudioOrbitCamera>();
            camGO.transform.position = new Vector3(0, 0.77f, -2.39f);
            camGO.transform.LookAt(orbit.target);

            var hudGO = new GameObject("Preview HUD");
            var hud = hudGO.AddComponent<PreviewHUD>();
            hud.animator = anim; hud.skirt = skirt; hud.monitor = monitor; hud.orbit = orbit;
            hud.stockingRenderers = new Renderer[] { legL, legR };

            ConfigureEnvironment();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            if (openAfter) EditorSceneManager.OpenScene(ScenePath);
        }

        // ------------------------------------------------------------------------------------------------ figure

        static SkinnedMeshRenderer BuildLeg(Transform hips, bool left, Material mat,
            out Transform thigh, out Transform shin, out CapsuleCollider[] capsules)
        {
            string s = left ? "L" : "R";
            Vector3 hip = LegGeometry.HipJoint(left), knee = LegGeometry.Knee(left), ankle = LegGeometry.Ankle(left);

            thigh = new GameObject("Thigh_" + s).transform;
            thigh.SetParent(hips, false);
            thigh.localPosition = hip;
            thigh.localRotation = Quaternion.FromToRotation(Vector3.down, knee - hip);

            shin = new GameObject("Shin_" + s).transform;
            shin.SetParent(thigh, false);
            shin.position = hips.TransformPoint(knee);
            shin.rotation = hips.rotation * Quaternion.FromToRotation(Vector3.down, ankle - knee);

            var legGO = new GameObject("Leg_" + s + " (stocking)");
            legGO.transform.SetParent(hips, false);
            var smr = legGO.AddComponent<SkinnedMeshRenderer>();

            Mesh mesh = LegGeometry.BuildLeg(left);
            Matrix4x4 m = legGO.transform.localToWorldMatrix;
            mesh.bindposes = new[] { thigh.worldToLocalMatrix * m, shin.worldToLocalMatrix * m };
            smr.sharedMesh = SaveMesh(mesh, "Leg_" + s);
            smr.bones = new[] { thigh, shin };
            smr.rootBone = thigh;
            smr.quality = SkinQuality.Bone2;
            smr.updateWhenOffscreen = true;
            smr.sharedMaterial = mat;

            // Capsules sit ~3 mm outside the skin so cloth particles rest on the stocking, not inside it.
            capsules = new[]
            {
                LegCapsule("ClothCollider_UpperThigh_" + s, thigh, hips, left, 0.93f, 0.70f),
                LegCapsule("ClothCollider_LowerThigh_" + s, thigh, hips, left, 0.70f, 0.50f),
                LegCapsule("ClothCollider_Shin_" + s, shin, hips, left, 0.44f, 0.10f),
            };
            return smr;
        }

        static CapsuleCollider LegCapsule(string name, Transform bone, Transform hips, bool left, float yTop, float yBottom)
        {
            const float clothOffset = 0.003f;
            float r = 0f;
            for (int i = 0; i <= 10; i++) r = Mathf.Max(r, LegGeometry.MaxRadiusAt(Mathf.Lerp(yBottom, yTop, i / 10f)));
            r += clothOffset;

            Vector3 top = bone.InverseTransformPoint(hips.TransformPoint(LegGeometry.LegAxis(yTop, left)));
            Vector3 bottom = bone.InverseTransformPoint(hips.TransformPoint(LegGeometry.LegAxis(yBottom, left)));

            var go = new GameObject(name);
            go.transform.SetParent(bone, false);
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1; // bone-local Y runs along the leg
            c.center = (top + bottom) * 0.5f;
            c.radius = r;
            c.height = Mathf.Abs(top.y - bottom.y) + 2f * r;
            return c;
        }

        static SphereCollider SphereCol(string name, Transform parent, Vector3 pos, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var c = go.AddComponent<SphereCollider>();
            c.radius = radius;
            return c;
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------------------------------------ lighting

        static void BuildLighting(Transform studio, Material softboxMat)
        {
            var subject = new Vector3(0f, 0.62f, 0f);
            //            name        position                       colour                         intensity angle  shadows  softbox size
            Spot(studio, "Key",       new Vector3(-1.5f, 2.1f, -1.9f), new Color(1.00f, 0.96f, 0.90f), 2.4f, 45f, true,  new Vector2(1.2f, 1.6f), subject, softboxMat);
            Spot(studio, "Fill",      new Vector3(1.9f, 1.0f, -1.6f),  new Color(0.86f, 0.90f, 1.00f), 0.55f, 60f, false, new Vector2(1.0f, 1.4f), subject, softboxMat);
            Spot(studio, "Rim_L",     new Vector3(-1.4f, 1.6f, 1.7f),  new Color(1.00f, 1.00f, 1.00f), 2.0f, 40f, false, new Vector2(0.4f, 1.6f), subject, softboxMat);
            Spot(studio, "Rim_R",     new Vector3(1.5f, 1.4f, 1.6f),   new Color(1.00f, 0.98f, 0.95f), 1.6f, 40f, false, new Vector2(0.4f, 1.6f), subject, softboxMat);
            Spot(studio, "Overhead",  new Vector3(0.0f, 3.2f, -0.3f),  new Color(1.00f, 0.98f, 0.96f), 0.8f, 55f, true,  new Vector2(1.5f, 1.5f), subject, softboxMat);
            Spot(studio, "Backdrop",  new Vector3(0.0f, 0.4f, 1.0f),   new Color(0.95f, 0.95f, 1.00f), 0.6f, 75f, false, Vector2.zero, new Vector3(0f, 1.6f, 2.8f), null);

            var probeGO = new GameObject("Reflection Probe (studio)");
            probeGO.transform.SetParent(studio, false);
            probeGO.transform.position = new Vector3(0f, 0.7f, -0.4f);
            var probe = probeGO.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.size = new Vector3(10f, 10f, 10f);
            probe.resolution = 256;
            probe.hdr = true;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.12f, 0.12f, 0.13f);
        }

        static void Spot(Transform parent, string name, Vector3 pos, Color color, float intensity, float angle,
                         bool shadows, Vector2 softbox, Vector3 target, Material softboxMat)
        {
            var go = new GameObject(name + " Light");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(target - pos);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.spotAngle = angle;
            l.range = 10f;
            l.renderMode = LightRenderMode.ForcePixel;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.9f;
            l.shadowResolution = LightShadowResolution.VeryHigh;
            l.shadowBias = 0.02f;
            l.shadowNormalBias = 0.25f;
            l.shadowNearPlane = 0.2f;
            l.lightmapBakeType = LightmapBakeType.Realtime;

            if (softbox == Vector2.zero || softboxMat == null) return;
            // Emissive panel just behind the light: invisible to the light itself, but it shows up as
            // a believable rectangular highlight in reflections (via the reflection probe).
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = name + " Softbox";
            UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(parent, false);
            Vector3 dir = (target - pos).normalized;
            panel.transform.position = pos - dir * 0.05f;
            panel.transform.rotation = Quaternion.LookRotation(-dir); // quad's visible side faces -Z → towards the subject
            panel.transform.localScale = new Vector3(softbox.x, softbox.y, 1f);
            var mr = panel.GetComponent<MeshRenderer>();
            mr.sharedMaterial = softboxMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        static void ConfigureEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.33f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.23f, 0.23f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.11f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = false;

            string lsPath = GenDir + "/LegsPreviewLighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "LegsPreviewLighting" };
                AssetDatabase.CreateAsset(ls, lsPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            ls.autoGenerate = false;
            Lightmapping.lightingSettings = ls;
        }

        static void ConfigureProject()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 12f;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.antiAliasing = 8;
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            QualitySettings.skinWeights = SkinWeights.FourBones;
        }

        // ------------------------------------------------------------------------------------------------ materials

        struct Mats { public Material stocking, skirt, lining, backdrop, softbox; }

        static Mats CreateMaterials(bool builtIn)
        {
            var mats = new Mats();
            Shader stockingShader = builtIn ? Shader.Find("LegsPreview/SheerStocking") : null;
            Shader fabricShader = builtIn ? Shader.Find("LegsPreview/WovenFabric") : null;
            Shader lit = builtIn ? Shader.Find("Standard") : PipelineLit();

            // White sheer stockings (~20-30 denier look) over a warm, natural skin tone.
            var stocking = new Material(stockingShader != null ? stockingShader : lit) { name = "M_Stocking_WhiteSheer" };
            if (stockingShader != null)
            {
                stocking.SetColor("_SkinColor", new Color(0.87f, 0.67f, 0.56f));
                stocking.SetColor("_NylonColor", new Color(0.97f, 0.97f, 0.96f));
                stocking.SetFloat("_Denier", 0.55f);
                stocking.SetFloat("_GrazingOpacity", 0.97f);
                stocking.SetFloat("_FresnelPower", 2.2f);
                stocking.SetFloat("_NylonSmoothness", 0.55f);
                stocking.SetFloat("_SheenStrength", 0.5f);
                stocking.SetVector("_KnitTiling", new Vector4(320f, 900f, 0f, 0f));
                stocking.SetFloat("_BandStart", 0.80f);
                stocking.SetFloat("_BandHeight", 0.075f);
            }
            else SetLit(stocking, new Color(0.93f, 0.90f, 0.88f), 0.5f);
            mats.stocking = SaveMaterial(stocking);

            // Mid-weight navy wool-blend twill.
            var skirt = new Material(fabricShader != null ? fabricShader : lit) { name = "M_Skirt_NavyTwill" };
            if (fabricShader != null)
            {
                skirt.SetColor("_Color", new Color(0.06f, 0.08f, 0.17f));
                skirt.SetColor("_BackColor", new Color(0.04f, 0.05f, 0.10f));
                skirt.SetFloat("_Smoothness", 0.22f);
                skirt.SetFloat("_SheenStrength", 0.35f);
            }
            else
            {
                SetLit(skirt, new Color(0.06f, 0.08f, 0.17f), 0.22f);
                if (skirt.HasProperty("_Cull")) skirt.SetFloat("_Cull", 0f); // URP/HDRP: render both faces
            }
            mats.skirt = SaveMaterial(skirt);

            var lining = new Material(fabricShader != null ? fabricShader : lit) { name = "M_UnderskirtShorts" };
            if (fabricShader != null)
            {
                lining.SetColor("_Color", new Color(0.035f, 0.04f, 0.08f));
                lining.SetColor("_BackColor", new Color(0.035f, 0.04f, 0.08f));
                lining.SetFloat("_Smoothness", 0.3f);
            }
            else SetLit(lining, new Color(0.035f, 0.04f, 0.08f), 0.3f);
            mats.lining = SaveMaterial(lining);

            var backdrop = new Material(lit) { name = "M_Cyclorama" };
            SetLit(backdrop, new Color(0.62f, 0.62f, 0.63f), 0.08f);
            mats.backdrop = SaveMaterial(backdrop);

            var softbox = new Material(lit) { name = "M_Softbox" };
            SetLit(softbox, Color.black, 0f);
            Color emission = Color.white * 3f;
            if (softbox.HasProperty("_EmissionColor")) softbox.SetColor("_EmissionColor", emission);
            softbox.EnableKeyword("_EMISSION");
            softbox.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mats.softbox = SaveMaterial(softbox);
            return mats;
        }

        static Shader PipelineLit()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
        }

        static void SetLit(Material m, Color color, float smoothness)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        }

        // ------------------------------------------------------------------------------------------------ assets

        /// <summary>Saves or updates in place (keeps the GUID so existing references survive a rebuild).</summary>
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{GenDir}/{name}.asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material SaveMaterial(Material mat)
        {
            string path = $"{GenDir}/{mat.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                string n = mat.name;
                existing.shader = mat.shader;
                existing.CopyPropertiesFromMaterial(mat);
                existing.shaderKeywords = mat.shaderKeywords;
                existing.globalIlluminationFlags = mat.globalIlluminationFlags;
                existing.name = n;
                UnityEngine.Object.DestroyImmediate(mat);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
