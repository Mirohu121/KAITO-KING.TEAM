using UnityEngine;

namespace Robogee.Player
{
    /// <summary>
    /// Drives Animator from WASD / motor planar motion (Input System → UnitInputFrame).
    /// Expected params: Speed (float), MoveX (float), MoveY (float), IsMoving (bool).
    /// </summary>
    public class UnitMoveAnimator : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] UnitBMotor motor;
        [SerializeField] MonoBehaviour inputSourceBehaviour;
        [SerializeField] float speedDampTime = 0.08f;
        [SerializeField] float moveDampTime = 0.08f;
        [SerializeField] float movingThreshold = 0.12f;
        [SerializeField] string speedParam = "Speed";
        [SerializeField] string moveXParam = "MoveX";
        [SerializeField] string moveYParam = "MoveY";
        [SerializeField] string movingParam = "IsMoving";

        IUnitInputSource _input;
        int _speedHash;
        int _moveXHash;
        int _moveYHash;
        int _movingHash;

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (motor == null)
                motor = GetComponent<UnitBMotor>();
            ResolveInput();
            CacheHashes();
        }

        void ResolveInput()
        {
            if (inputSourceBehaviour is IUnitInputSource typed)
            {
                _input = typed;
                return;
            }

            var human = GetComponent<UnitPlayerInput>();
            if (human != null)
            {
                _input = human;
                inputSourceBehaviour = human;
            }
        }

        void CacheHashes()
        {
            _speedHash = Animator.StringToHash(speedParam);
            _moveXHash = Animator.StringToHash(moveXParam);
            _moveYHash = Animator.StringToHash(moveYParam);
            _movingHash = Animator.StringToHash(movingParam);
        }

        void Update()
        {
            if (animator == null)
                return;
            if (_input == null)
                ResolveInput();

            UnitInputFrame frame = _input != null ? _input.Current : default;
            float speed = motor != null ? motor.PlanarSpeed : 0f;
            bool moving = speed > movingThreshold || frame.MovePlanar.sqrMagnitude > 0.0001f;

            if (HasParam(_speedHash))
                animator.SetFloat(_speedHash, speed, speedDampTime, Time.deltaTime);
            if (HasParam(_moveXHash))
                animator.SetFloat(_moveXHash, frame.MoveX, moveDampTime, Time.deltaTime);
            if (HasParam(_moveYHash))
                animator.SetFloat(_moveYHash, frame.MoveY, moveDampTime, Time.deltaTime);
            if (HasParam(_movingHash))
                animator.SetBool(_movingHash, moving);
        }

        bool HasParam(int hash)
        {
            if (animator.runtimeAnimatorController == null)
                return false;
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash)
                    return true;
            }

            return false;
        }
    }
}
