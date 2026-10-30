using System.Runtime.InteropServices;
using UnityEngine;
using NSFGrant.UI;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Simple first-person navigation for the laptop/WebGL build:
    /// WASD/arrow keys to move, and the mouse to look around. Clicking the
    /// scene captures the mouse (browser pointer lock), like a game: moving
    /// the mouse then turns the view with no button held, a center dot
    /// aims, and left-click selects what the dot is on (DesktopInteractor).
    /// Esc releases the cursor for on-screen buttons. Holding the right
    /// button still looks around while the cursor is free. Movement uses a
    /// CharacterController so participants cannot walk through exhibits.
    ///
    /// In a browser, Unity reports the mouse as captured as soon as it asks,
    /// even when the page refuses pointer lock (e.g. embedded in an iframe
    /// without allow="pointer-lock") or has already let it go (Esc), so the
    /// on-screen buttons became unclickable. The browser's real state
    /// (NSFGrantPointerLock.jslib) is checked each frame and the cursor freed
    /// when it isn't locked; after repeated refusals capture stops and
    /// right-drag look takes over.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DesktopPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float lookSensitivity = 2.5f;
        [SerializeField] private Transform cameraTransform;

        private CharacterController _controller;
        private float _pitch;

        /// <summary>The mouse is captured: it turns the view and aims from screen center.</summary>
        public static bool MouseCaptured => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>
        /// Captured, and not by this frame's click: the click that captures
        /// still selects what was under the cursor, not what is at center.
        /// </summary>
        public static bool AimFromCenter => MouseCaptured && Time.frameCount != _capturedFrame;

        private static int _capturedFrame = -1;

        // Set once the browser keeps refusing pointer lock (WebGL only).
        private bool _captureRefused = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        // The browser grants pointer lock asynchronously after the click;
        // give it this long before treating the capture as refused. Chrome
        // also refuses a re-lock for about a second after Esc, so only give
        // up on capturing after a few refusals with no successful lock.
        private const float LockGraceSeconds = 1.5f;
        private const int RefusalsBeforeGivingUp = 3;
        private float _captureRequestedAt = -1f;
        private bool _browserLockSeen;
        private int _refusals;

        [DllImport("__Internal")]
        private static extern int NSFGrantPointerLocked();
#endif

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
            // No walking/looking while a panel is open, e.g. typing the
            // participant ID or answering the quiz.
            if (StudyGuiKit.ModalVisible)
            {
                ReleaseMouse();
                return;
            }
            if (MouseCaptured && Input.GetKeyDown(KeyCode.Escape))
            {
                ReleaseMouse();
            }
            SyncWithBrowserLock();
            // Capture on a click in the scene (not on an on-screen button).
            // In a browser the pointer lock itself starts on that click.
            if (!MouseCaptured && !_captureRefused && Input.GetMouseButtonDown(0) &&
                !StudyGuiKit.PointerOverHud)
            {
                Cursor.lockState = CursorLockMode.Locked;
                _capturedFrame = Time.frameCount;
#if UNITY_WEBGL && !UNITY_EDITOR
                _captureRequestedAt = Time.unscaledTime;
                _browserLockSeen = false;
#endif
            }
            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            if (!MouseCaptured && !Input.GetMouseButton(1))
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

        /// <summary>
        /// Frees the cursor when the browser doesn't actually hold pointer
        /// lock: it let go (Esc, tab switch) or never granted it. A lock that
        /// never arrives means this page can't capture, so stop asking.
        /// </summary>
        private void SyncWithBrowserLock()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!MouseCaptured)
            {
                return;
            }
            if (NSFGrantPointerLocked() != 0)
            {
                _browserLockSeen = true;
                _refusals = 0;
                return;
            }
            if (_browserLockSeen)
            {
                ReleaseMouse();
            }
            else if (Time.unscaledTime - _captureRequestedAt > LockGraceSeconds)
            {
                ReleaseMouse();
                if (++_refusals >= RefusalsBeforeGivingUp)
                {
                    _captureRefused = true;
                    Debug.LogWarning("[DesktopPlayerController] Browser keeps refusing pointer " +
                                     "lock; using right-drag to look around.");
                }
            }
#endif
        }

        private void OnDisable()
        {
            ReleaseMouse();
        }

        private static void ReleaseMouse()
        {
            if (MouseCaptured)
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void OnGUI()
        {
            if (StudyGuiKit.ModalVisible)
            {
                return;
            }
            if (MouseCaptured)
            {
                // Aim dot with a dark outline, readable on any wall.
                float cx = Screen.width / 2f, cy = Screen.height / 2f;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(cx - 4f, cy - 4f, 8f, 8f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(cx - 2.5f, cy - 2.5f, 5f, 5f), Texture2D.whiteTexture);
            }
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Mathf.Max(12, Screen.height / 50)
            };
            // Dark shadow under the text so it reads on the bright ceiling.
            var rect = new Rect(0f, Screen.height * 0.12f, Screen.width, 40f);
            string hint = MouseCaptured ? "Esc to free the cursor for buttons"
                : _captureRefused ? "Right-drag to look around \u00B7 WASD to move"
                : "Click to look around \u00B7 WASD to move";
            style.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), hint, style);
            style.normal.textColor = Color.white;
            GUI.Label(rect, hint, style);
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
