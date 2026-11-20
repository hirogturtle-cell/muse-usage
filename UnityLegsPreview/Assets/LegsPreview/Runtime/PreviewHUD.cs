using UnityEngine;

namespace LegsPreview
{
    /// <summary>Immediate-mode control panel: pose, fabric preset, stocking denier, camera views, live cloth stats.</summary>
    public class PreviewHUD : MonoBehaviour
    {
        public LegPoseAnimator animator;
        public SkirtCloth skirt;
        public ClothHealthMonitor monitor;
        public StudioOrbitCamera orbit;
        public Renderer[] stockingRenderers;
        public bool visible = true;

        static readonly int DenierId = Shader.PropertyToID("_Denier");
        Material _stockingInstance;
        float _denier = -1f;
        float _fps;
        GUIStyle _box, _label, _small;
        const float Width = 270f;

        void Start()
        {
            // one runtime instance shared by both legs, so the slider never edits the material asset
            if (stockingRenderers != null && stockingRenderers.Length > 0 && stockingRenderers[0] != null)
            {
                _stockingInstance = new Material(stockingRenderers[0].sharedMaterial) { name = "Stocking (runtime)" };
                foreach (var r in stockingRenderers) if (r != null) r.sharedMaterial = _stockingInstance;
                if (_stockingInstance.HasProperty(DenierId)) _denier = _stockingInstance.GetFloat(DenierId);
            }
        }

        void OnDestroy()
        {
            if (_stockingInstance != null) Destroy(_stockingInstance);
        }

        void Update()
        {
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-5f);
            _fps = Mathf.Lerp(_fps, 1f / dt, 0.05f);
            if (orbit != null) orbit.inputBlockRect = visible ? new Rect(8, 8, Width, Screen.height - 16) : new Rect();
        }

        void Styles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), alignment = TextAnchor.UpperLeft };
            _label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        }

        void OnGUI()
        {
            Styles();
            if (!visible)
            {
                if (GUI.Button(new Rect(8, 8, 90, 24), "Show panel")) visible = true;
                return;
            }
            GUILayout.BeginArea(new Rect(8, 8, Width, Screen.height - 16));
            GUILayout.BeginVertical(_box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Legs Preview", _label);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{_fps:0} fps", _small);
            if (GUILayout.Button("Hide", GUILayout.Width(44))) visible = false;
            GUILayout.EndHorizontal();

            if (animator != null)
            {
                GUILayout.Label("Motion", _label);
                animator.mode = (LegPoseAnimator.Mode)GUILayout.Toolbar((int)animator.mode, new[] { "Idle", "Walk", "Twirl", "Still" });
            }

            if (skirt != null)
            {
                GUILayout.Label("Skirt fabric", _label);
                var names = new[] { "Wool", "Cotton", "Chiffon", "Denim" };
                int cur = skirt.fabric == SkirtCloth.Fabric.Custom ? -1 : (int)skirt.fabric;
                int sel = GUILayout.SelectionGrid(cur, names, 4);
                if (sel != cur && sel >= 0) skirt.SetFabric((SkirtCloth.Fabric)sel);
                var p = skirt.Current;
                GUILayout.Label($"stretch {p.stretchingStiffness:0.00} · bend {p.bendingStiffness:0.00} · damping {p.damping:0.00} · friction {p.friction:0.00} · {p.solverFrequency:0} Hz", _small);
                bool fan = GUILayout.Toggle(skirt.fanOn, " Studio fan");
                if (fan != skirt.fanOn) skirt.SetFan(fan);
            }

            if (_stockingInstance != null && _denier >= 0f)
            {
                GUILayout.Label($"Stocking sheerness (facing opacity {_denier:0.00})", _label);
                float d = GUILayout.HorizontalSlider(_denier, 0.15f, 0.95f);
                if (!Mathf.Approximately(d, _denier)) { _denier = d; _stockingInstance.SetFloat(DenierId, d); }
                GUILayout.Space(6);
            }

            if (orbit != null)
            {
                GUILayout.Label("Camera", _label);
                int i = 0;
                GUILayout.BeginHorizontal();
                foreach (var v in StudioOrbitCamera.Views)
                {
                    if (GUILayout.Button(v.name)) orbit.SetView(v);
                    if (++i % 3 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                }
                GUILayout.EndHorizontal();
                orbit.autoOrbit = GUILayout.Toggle(orbit.autoOrbit, " Turntable");
                GUILayout.Label("Drag: orbit · Middle-drag: pan · Wheel: zoom", _small);
            }

            if (monitor != null)
            {
                GUILayout.Label("Cloth health", _label);
                var c = GUI.color;
                GUI.color = !monitor.Reported ? Color.white : monitor.Passed ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.5f);
                GUILayout.Label((monitor.Reported ? (monitor.Passed ? "PASS  " : "FAIL  ") : "") + monitor.Summary, _small);
                GUI.color = c;
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
