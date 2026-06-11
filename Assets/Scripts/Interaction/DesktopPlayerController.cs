using UnityEngine;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Simple first-person navigation for the laptop/WebGL build:
    /// WASD/arrow keys to move, hold the right mouse button to look around
    /// (familiar from gaming software, as discussed in the VERA meeting;
    /// the cursor stays free for clicking content). Movement uses a
    /// CharacterController so participants cannot walk through exhibits.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DesktopPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float lookSensitivity = 2.5f;
        [SerializeField] private Transform cameraTransform;

        private CharacterController _controller;
        private float _pitch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    cameraTransform = cam.transform;
                }
            }
        }

        private void Update()
        {
            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            if (!Input.GetMouseButton(1))
            {
                return;
            }

            float yaw = Input.GetAxis("Mouse X") * lookSensitivity;
            float pitchDelta = -Input.GetAxis("Mouse Y") * lookSensitivity;

            transform.Rotate(0f, yaw, 0f);

            _pitch = Mathf.Clamp(_pitch + pitchDelta, -80f, 80f);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void HandleMove()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            Vector3 move = (transform.right * h + transform.forward * v) * moveSpeed;
            move.y = -1f; // keep grounded
            _controller.Move(move * Time.deltaTime);
        }
    }
}
