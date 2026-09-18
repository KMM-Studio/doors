using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class playerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Look Settings")]
    [SerializeField] private float lookSensitivity = 1.2f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [FormerlySerializedAs("_playerInput")]
    [Header("Input")]
    [Tooltip("Reference to the Player input component")]
    [SerializeField] private PlayerInput playerInput;

    [Header("References")]
    [SerializeField] private Transform playerCamera;

    private Rigidbody _rb;
    private Vector2 _inputVector;
    private Vector2 _lookVector;
    private float _cameraPitch;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _rb.WakeUp();

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        ToggleAction("Move", true);
        ToggleAction("Look", true);
    }

    private void OnDisable()
    {
        ToggleAction("Move", false);
        ToggleAction("Look", false);
    }

    private void ToggleAction(string actionName, bool enable)
    {
        if (playerInput == null) return;
        var action = playerInput.actions.FindAction(actionName);
        if (action == null) return;

        if (enable) action.Enable();
        else action.Disable();
    }

    private void Update()
    {
        GetInput();
    }

    private void LateUpdate()
    {
        RotatePlayerAndCamera();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void GetInput()
    {
        if (playerInput == null) return;

        var moveAction = playerInput.actions.FindAction("Move");
        if (moveAction != null) _inputVector = moveAction.ReadValue<Vector2>();

        var lookAction = playerInput.actions.FindAction("Look");
        if (lookAction != null) _lookVector = lookAction.ReadValue<Vector2>();
    }

    private void RotatePlayerAndCamera()
    {
        if (playerCamera == null) return;

        // 1. Horizontal rotation (Yaw) rotates the entire player body
        transform.Rotate(Vector3.up * (_lookVector.x * lookSensitivity));

        // 2. Vertical rotation (Pitch) clamps locally on the camera
        _cameraPitch -= _lookVector.y * lookSensitivity;
        _cameraPitch = Mathf.Clamp(_cameraPitch, minPitch, maxPitch);
        playerCamera.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    private void MovePlayer()
    {
        if (playerCamera is null || playerInput is null) return;

        Vector3 forward = playerCamera.forward;
        Vector3 right = playerCamera.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * _inputVector.y + right * _inputVector.x).normalized;
        Vector3 targetVelocity = moveDir * moveSpeed;

        _rb.MovePosition(_rb.position + targetVelocity * Time.fixedDeltaTime);
    }
}