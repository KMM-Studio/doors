using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Game.Player //
{
    /// <summary>
    /// Handles physical player movement, camera rotation, and input polling via Unity Events.
    /// Integrates directly with the Rigidbody physics system and PlayerStats.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class playerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Reference to the player's stat configuration (requires maxSpeed).")]
        [SerializeField] private PlayerStats playerStats;

        [Space]
        [Header("Look Settings")]
        [Tooltip("Multiplier for mouse/stick movement to adjust camera rotation speed.")]
        [SerializeField] private float lookSensitivity = 1.2f;
        [Tooltip("Maximum downward angle in degrees.")]
        [SerializeField] private float minPitch = -85f;
        [Tooltip("Maximum upward angle in degrees.")]
        [SerializeField] private float maxPitch = 85f;

        [Space]
        [FormerlySerializedAs("_playerInput")]
        [Header("Input References")]
        [Tooltip("Reference to the Player Input component handling Unity's new Input System actions.")]
        [SerializeField] private PlayerInput playerInput;

        [Space]
        [Header("Camera References")]
        [Tooltip("The main camera transform attached to the player head.")]
        [SerializeField] private Transform playerCamera;
        
        [Space]
        [Header("Debug Settings")]
        [Tooltip("Toggle visual gizmos and rich text console debugging.")]
        [SerializeField] private bool enableDebug = true;

        private Rigidbody _rb;
        private Vector2 _inputVector;
        private Vector2 _lookVector;
        private float _cameraPitch;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            Debug.Assert(_rb != null, "<color=red><b>[playerMovement]</b></color> Rigidbody is missing on this GameObject!");
        }

        private void Start()
        {
            _rb.freezeRotation = true;
            _rb.WakeUp();

            if (playerCamera == null && Camera.main != null)
            {
                playerCamera = Camera.main.transform;
                if (enableDebug) Debug.Log("<color=yellow><b>[playerMovement]</b></color> playerCamera auto-assigned to Camera.main.");
            }
            
            Debug.Assert(playerCamera != null, "<color=red><b>[playerMovement]</b></color> Camera reference is completely missing!");

            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>();
                Debug.Assert(playerStats != null, "<color=red><b>[playerMovement]</b></color> PlayerStats component is missing! Movement will fail.");
            }
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Unity Event callback for the Move action. 
        /// Assign this in the PlayerInput component under Events -> Player -> Move.
        /// </summary>
        /// <param name="context">The callback context containing the Vector2 input data.</param>
        public void OnMove(InputAction.CallbackContext context)
        {
            _inputVector = context.ReadValue<Vector2>();
            
            if (enableDebug && context.performed)
            {
                Debug.Log($"<color=cyan><b>[playerMovement]</b></color> Move event invoked: {_inputVector}");
            }
        }

        /// <summary>
        /// Unity Event callback for the Look action. 
        /// Assign this in the PlayerInput component under Events -> Player -> Look.
        /// </summary>
        /// <param name="context">The callback context containing the Vector2 look delta.</param>
        public void OnLook(InputAction.CallbackContext context)
        {
            _lookVector = context.ReadValue<Vector2>();
        }

        private void LateUpdate()
        {
            RotatePlayerAndCamera();
        }

        private void FixedUpdate()
        {
            MovePlayer();
        }

        /// <summary>
        /// Calculates and applies horizontal rotation to the player body and vertical pitch to the camera.
        /// </summary>
        private void RotatePlayerAndCamera()
        {
            if (!playerCamera) return;

            // Horizontal rotation (Yaw) rotates the entire player body
            transform.Rotate(Vector3.up * (_lookVector.x * lookSensitivity));

            // Vertical rotation (Pitch) clamps locally on the camera
            _cameraPitch -= _lookVector.y * lookSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, minPitch, maxPitch);
            playerCamera.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }

        /// <summary>
        /// Calculates the movement vector based on camera facing and applies Rigidbody velocity.
        /// </summary>
        private void MovePlayer()
        {
            if (!playerCamera || !playerStats) return;

            Vector3 forward = playerCamera.forward;
            Vector3 right = playerCamera.right;
            
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * _inputVector.y + right * _inputVector.x).normalized;
            Vector3 targetVelocity = moveDir * playerStats.maxSpeed;

            _rb.MovePosition(_rb.position + targetVelocity * Time.fixedDeltaTime);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableDebug) return;

            Vector3 currentPos = transform.position;

            // Visual Scene Debugging: Show intended movement direction
            if (_inputVector.sqrMagnitude > 0.1f && playerCamera != null)
            {
                Vector3 forward = playerCamera.forward;
                Vector3 right = playerCamera.right;
                forward.y = 0f; right.y = 0f;
                
                Vector3 moveDir = (forward.normalized * _inputVector.y + right.normalized * _inputVector.x).normalized;
                
                Gizmos.color = Color.green;
                Gizmos.DrawRay(currentPos, moveDir * 2f);
                Gizmos.DrawWireSphere(currentPos + moveDir * 2f, 0.2f);
            }
        }
#endif
    }
}