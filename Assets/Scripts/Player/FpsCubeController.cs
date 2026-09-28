using UnityEngine;

namespace Robogee.Player
{
    /// <summary>
    /// Legacy prototype. Prefer <see cref="UnitBMotor"/>.
    /// </summary>
    [System.Obsolete("Use UnitBMotor instead.")]
    [RequireComponent(typeof(CharacterController))]
    public class FpsCubeController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] Transform cameraPivot;
        [SerializeField] float mouseSensitivity = 2f;
        [SerializeField] float minPitch = -80f;
        [SerializeField] float maxPitch = 80f;

        CharacterController _controller;
        float _pitch;
        float _verticalVelocity;
        bool _cursorLocked = true;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    cameraPivot = cam.transform;
            }
        }

        void Start() => SetCursorLock(true);

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorLock(!_cursorLocked);
            if (_cursorLocked)
                Look();
            Move();
        }

        void Look()
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            transform.Rotate(0f, mouseX, 0f);
            if (cameraPivot == null)
                return;
            _pitch = Mathf.Clamp(_pitch - mouseY, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void Move()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(x, 0f, z);
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            Vector3 worldMove = transform.TransformDirection(input) * moveSpeed;
            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                    _verticalVelocity = -2f;
                if (Input.GetButtonDown("Jump"))
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * Time.deltaTime;
            worldMove.y = _verticalVelocity;
            _controller.Move(worldMove * Time.deltaTime);
        }

        void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
