using UnityEngine;

namespace LegsPreview
{
    /// <summary>Turntable-style studio camera: drag to orbit, middle-drag to pan, wheel to zoom.</summary>
    [RequireComponent(typeof(Camera))]
    public class StudioOrbitCamera : MonoBehaviour
    {
        public Vector3 target = new Vector3(0f, 0.6f, 0f);
        public float distance = 2.4f;
        public float yaw = 0f;
        public float pitch = 4f;
        public Vector2 distanceLimits = new Vector2(0.6f, 5f);
        public Vector2 pitchLimits = new Vector2(-10f, 60f);
        public float orbitSpeed = 4f;
        public float smoothing = 10f;
        public bool autoOrbit;
        public float autoOrbitDegPerSec = 12f;
        [Tooltip("Ignore mouse input while the pointer is over this screen rect (the HUD).")]
        public Rect inputBlockRect = new Rect(0, 0, 0, 0);

        Vector3 _target;
        float _yaw, _pitch, _distance;

        public struct View { public string name; public float yaw, pitch, distance; public Vector3 target; }

        public static readonly View[] Views =
        {
            new View { name = "Front",    yaw =   0f, pitch =  4f, distance = 2.4f, target = new Vector3(0, 0.60f, 0) },
            new View { name = "3/4",      yaw =  35f, pitch =  6f, distance = 2.3f, target = new Vector3(0, 0.60f, 0) },
            new View { name = "Side",     yaw =  90f, pitch =  4f, distance = 2.4f, target = new Vector3(0, 0.60f, 0) },
            new View { name = "Back",     yaw = 180f, pitch =  6f, distance = 2.4f, target = new Vector3(0, 0.62f, 0) },
            new View { name = "Skirt",    yaw =  20f, pitch = 10f, distance = 1.3f, target = new Vector3(0, 0.82f, 0) },
            new View { name = "Stocking", yaw = -25f, pitch =  2f, distance = 1.0f, target = new Vector3(0, 0.35f, 0) },
        };

        void Start()
        {
            _target = target; _yaw = yaw; _pitch = pitch; _distance = distance;
            ApplyTransform();
        }

        public void SetView(View v)
        {
            yaw = v.yaw; pitch = v.pitch; distance = v.distance; target = v.target;
        }

        void LateUpdate()
        {
            Vector2 mp = PreviewInput.MousePosition;
            bool overHud = inputBlockRect.Contains(new Vector2(mp.x, Screen.height - mp.y));
            if (!overHud)
            {
                if (PreviewInput.OrbitHeld)
                {
                    Vector2 d = PreviewInput.MouseDelta;
                    yaw += d.x * orbitSpeed;
                    pitch -= d.y * orbitSpeed;
                }
                else if (PreviewInput.PanHeld)
                {
                    Vector2 d = PreviewInput.MouseDelta * (distance * 0.02f);
                    target -= transform.right * d.x + transform.up * d.y;
                }
                float scroll = PreviewInput.Scroll;
                if (Mathf.Abs(scroll) > 0.01f) distance *= Mathf.Pow(0.9f, scroll);
            }
            if (autoOrbit) yaw += autoOrbitDegPerSec * Time.deltaTime;

            pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
            distance = Mathf.Clamp(distance, distanceLimits.x, distanceLimits.y);

            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            _yaw = Mathf.LerpAngle(_yaw, yaw, k);
            _pitch = Mathf.Lerp(_pitch, pitch, k);
            _distance = Mathf.Lerp(_distance, distance, k);
            _target = Vector3.Lerp(_target, target, k);
            ApplyTransform();
        }

        void ApplyTransform()
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _target + rot * new Vector3(0f, 0f, -_distance);
            transform.rotation = rot;
        }
    }
}
