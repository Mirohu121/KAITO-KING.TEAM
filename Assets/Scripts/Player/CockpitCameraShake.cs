using UnityEngine;

namespace Robogee.Player
{
    /// <summary>
    /// 絆-style cockpit jolts driven by <see cref="UnitBMotor"/> events: a dip per footstep,
    /// a slam on landing, a forward lurch when a dash brakes, and rumble while boosting.
    /// Lives on the camera under CameraPivot and only writes its local pose.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public class CockpitCameraShake : MonoBehaviour
    {
        [SerializeField] UnitBMotor motor;
        [SerializeField] [Range(0f, 2f)] float intensity = 1f;

        [Header("Footstep")]
        [SerializeField] float stepDrop = 0.035f;
        [SerializeField] float stepPitch = 0.9f;
        [SerializeField] float stepRoll = 0.35f;

        [Header("Landing")]
        [SerializeField] float landingDrop = 0.14f;
        [SerializeField] float landingPitch = 3.5f;

        [Header("Dash brake")]
        [SerializeField] float brakeDrop = 0.04f;
        [SerializeField] float brakePitch = 2.2f;

        [Header("Boost rumble")]
        [SerializeField] float rumblePosition = 0.006f;
        [SerializeField] float rumbleAngle = 0.25f;
        [SerializeField] float rumbleFrequency = 26f;

        [Header("Spring")]
        [SerializeField] float stiffness = 220f;
        [SerializeField] [Range(0.1f, 1.5f)] float damping = 0.55f;

        struct Spring
        {
            public float X;
            public float V;

            public void Step(float k, float c, float dt)
            {
                V += (-k * X - c * V) * dt;
                X += V * dt;
            }
        }

        Vector3 _basePosition;
        Quaternion _baseRotation;
        Spring _drop;
        Spring _pitch;
        Spring _roll;
        float _rollSign = 1f;
        float _rumble;
        UnitBMotor _subscribed;

        void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            if (motor == null)
                motor = GetComponentInParent<UnitBMotor>();
        }

        void OnEnable() => Subscribe(motor);

        void OnDisable() => Subscribe(null);

        public void Bind(UnitBMotor target)
        {
            motor = target;
            if (isActiveAndEnabled)
                Subscribe(motor);
        }

        void Subscribe(UnitBMotor target)
        {
            if (_subscribed == target)
                return;

            if (_subscribed != null)
            {
                _subscribed.Footstep -= OnFootstep;
                _subscribed.Landed -= OnLanded;
                _subscribed.DashBraked -= OnDashBraked;
            }

            _subscribed = target;
            if (_subscribed == null)
                return;

            _subscribed.Footstep += OnFootstep;
            _subscribed.Landed += OnLanded;
            _subscribed.DashBraked += OnDashBraked;
        }

        // Peak of an underdamped spring is ~0.52 * v0 / ω at ζ≈0.55, so this hits roughly `amplitude`.
        void Kick(ref Spring spring, float amplitude)
        {
            spring.V += amplitude * Mathf.Sqrt(stiffness) * 1.9f;
        }

        void OnFootstep(float strength)
        {
            float s = Mathf.Clamp01(strength);
            Kick(ref _drop, -stepDrop * s);
            Kick(ref _pitch, stepPitch * s);
            Kick(ref _roll, stepRoll * s * _rollSign);
            _rollSign = -_rollSign;
        }

        void OnLanded(float impact)
        {
            float s = Mathf.Clamp(impact, 0.25f, 1f);
            Kick(ref _drop, -landingDrop * s);
            Kick(ref _pitch, landingPitch * s);
        }

        void OnDashBraked(float strength)
        {
            float s = Mathf.Clamp01(strength);
            Kick(ref _drop, -brakeDrop * s);
            Kick(ref _pitch, brakePitch * s);
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f)
                return;

            float k = stiffness;
            float c = 2f * Mathf.Sqrt(k) * damping;
            _drop.Step(k, c, dt);
            _pitch.Step(k, c, dt);
            _roll.Step(k, c, dt);

            bool boosting = motor != null && (motor.IsDashing || motor.IsJumpThrusting);
            _rumble = Mathf.MoveTowards(_rumble, boosting ? 1f : 0f, dt * 6f);

            float t = Time.time * rumbleFrequency;
            float rumbleY = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * rumblePosition * _rumble;
            float rumbleP = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * rumbleAngle * _rumble;
            float rumbleR = (Mathf.PerlinNoise(t * 0.8f, 5.1f) - 0.5f) * 2f * rumbleAngle * _rumble;

            transform.localPosition = _basePosition + new Vector3(0f, (_drop.X + rumbleY) * intensity, 0f);
            transform.localRotation = _baseRotation * Quaternion.Euler(
                (_pitch.X + rumbleP) * intensity,
                0f,
                (_roll.X + rumbleR) * intensity);
        }
    }
}
