using UnityEngine;

namespace LegsPreview
{
    /// <summary>
    /// Lightweight procedural motion that gives the cloth something to react to:
    /// idle weight-shift, walk-in-place and a twirl that flares the skirt.
    /// Runs in Update so the Cloth solver (which steps after animation) sees the new pose.
    /// </summary>
    public class LegPoseAnimator : MonoBehaviour
    {
        public enum Mode { Idle, WalkInPlace, Twirl, Still }

        public Mode mode = Mode.Idle;
        public Transform hips;
        public Transform thighL, shinL, thighR, shinR;

        [Header("Idle")]
        public float idleShiftPeriod = 5.5f;
        public float idleShiftDistance = 0.015f;

        [Header("Walk")]
        public float stridesPerSecond = 0.9f;
        public float hipFlexion = 22f;
        public float swingKneeFlexion = 55f;

        [Header("Twirl")]
        public float twirlPeakDegPerSec = 330f;
        public float twirlPulsePeriod = 6f;

        [Tooltip("How quickly the pose blends when switching modes.")]
        public float blendSpeed = 4f;

        struct Pose
        {
            public Vector3 hipsOffset;
            public float hipsYaw, hipsRoll;
            public float flexL, flexR, abdL, abdR, kneeL, kneeR;

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                return new Pose
                {
                    hipsOffset = Vector3.Lerp(a.hipsOffset, b.hipsOffset, t),
                    hipsYaw = Mathf.Lerp(a.hipsYaw, b.hipsYaw, t),
                    hipsRoll = Mathf.Lerp(a.hipsRoll, b.hipsRoll, t),
                    flexL = Mathf.Lerp(a.flexL, b.flexL, t), flexR = Mathf.Lerp(a.flexR, b.flexR, t),
                    abdL = Mathf.Lerp(a.abdL, b.abdL, t), abdR = Mathf.Lerp(a.abdR, b.abdR, t),
                    kneeL = Mathf.Lerp(a.kneeL, b.kneeL, t), kneeR = Mathf.Lerp(a.kneeR, b.kneeR, t),
                };
            }
        }

        Pose _pose;
        float _time, _twirlYaw;
        bool _captured;
        Vector3 _hipsPos;
        Quaternion _hipsRot, _thighLRot, _shinLRot, _thighRRot, _shinRRot;

        void Awake() => Capture();

        void Capture()
        {
            if (_captured || hips == null) return;
            _hipsPos = hips.localPosition; _hipsRot = hips.localRotation;
            _thighLRot = thighL.localRotation; _shinLRot = shinL.localRotation;
            _thighRRot = thighR.localRotation; _shinRRot = shinR.localRotation;
            _captured = true;
        }

        public void SetMode(Mode m) => mode = m;

        void Update()
        {
            if (!_captured) return;
            float dt = Time.deltaTime;
            _time += dt;

            Pose target = default;
            switch (mode)
            {
                case Mode.Idle:
                {
                    float s = Mathf.Sin(_time * Mathf.PI * 2f / idleShiftPeriod);
                    float dx = idleShiftDistance * s;
                    target.hipsOffset = new Vector3(dx, -0.004f * Mathf.Abs(s), 0);
                    target.hipsRoll = -2f * s;
                    target.hipsYaw = 3f * Mathf.Sin(_time * Mathf.PI * 2f * 0.11f);
                    float abd = -Mathf.Asin(Mathf.Clamp(dx / 0.85f, -1, 1)) * Mathf.Rad2Deg;
                    target.abdL = target.abdR = abd;
                    // relaxed (unloaded) knee softens slightly
                    target.kneeL = 7f * Mathf.Max(0f, s);
                    target.kneeR = 7f * Mathf.Max(0f, -s);
                    target.flexL = 0.4f * target.kneeL;
                    target.flexR = 0.4f * target.kneeR;
                    break;
                }
                case Mode.WalkInPlace:
                {
                    float ph = _time * stridesPerSecond * Mathf.PI * 2f;
                    float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
                    target.flexR = hipFlexion * s;
                    target.flexL = -hipFlexion * s;
                    target.kneeR = swingKneeFlexion * Mathf.Pow(Mathf.Max(0f, c), 2f);
                    target.kneeL = swingKneeFlexion * Mathf.Pow(Mathf.Max(0f, -c), 2f);
                    target.hipsOffset = new Vector3(0.012f * c, -0.014f * Mathf.Abs(s), 0);
                    target.hipsYaw = 5f * s;
                    target.hipsRoll = 3f * c;
                    break;
                }
                case Mode.Twirl:
                {
                    float pulse = 0.5f - 0.5f * Mathf.Cos(_time * Mathf.PI * 2f / twirlPulsePeriod);
                    _twirlYaw += twirlPeakDegPerSec * pulse * dt;
                    target.hipsYaw = _twirlYaw;
                    target.hipsOffset = new Vector3(0, -0.01f * pulse, 0);
                    target.kneeL = target.kneeR = 6f * pulse;
                    target.flexL = target.flexR = 3f * pulse;
                    break;
                }
                case Mode.Still:
                    break;
            }

            if (mode != Mode.Twirl)
            {
                // unwind any accumulated twirl to the nearest full turn
                _twirlYaw = Mathf.MoveTowards(_twirlYaw, Mathf.Round(_twirlYaw / 360f) * 360f, 120f * dt);
                target.hipsYaw += _twirlYaw;
            }

            _pose = Pose.Lerp(_pose, target, 1f - Mathf.Exp(-blendSpeed * dt));
            if (mode == Mode.Twirl) _pose.hipsYaw = target.hipsYaw; // no lag on continuous rotation
            ApplyPose(_pose);
        }

        void ApplyPose(Pose p)
        {
            hips.localPosition = _hipsPos + p.hipsOffset;
            hips.localRotation = _hipsRot * Quaternion.Euler(0f, p.hipsYaw, p.hipsRoll);
            // +X on a thigh bone = hip flexion (knee forward, towards -Z); -X on a shin = knee flexion.
            thighL.localRotation = _thighLRot * Quaternion.Euler(p.flexL, 0f, p.abdL);
            thighR.localRotation = _thighRRot * Quaternion.Euler(p.flexR, 0f, p.abdR);
            shinL.localRotation = _shinLRot * Quaternion.Euler(-p.kneeL, 0f, 0f);
            shinR.localRotation = _shinRRot * Quaternion.Euler(-p.kneeR, 0f, 0f);
        }
    }
}
