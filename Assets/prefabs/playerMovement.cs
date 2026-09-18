using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class playerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float movementMultiplier = 10f;

    [FormerlySerializedAs("_playerInput")]
    [Header("Input")]
    [Tooltip("Reference to the Player input component")]
    [SerializeField] private PlayerInput playerInput;

    [Header("References")]
    [SerializeField] private Transform playerCamera;
    
    private Rigidbody _rb;
    private Vector2 _inputVector;

    private void Start()
    {
        _rb = transform.GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _rb.WakeUp();

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    private void OnEnable()
    {
        if (playerInput != null)
        {
            playerInput.actions["Move"].Enable();
        }
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            playerInput.actions["Move"].Disable();
        }
    }

    private void Update()
    {
        GetInput();
    }

    private void GetInput()
    {
        _inputVector = playerInput.actions["Move"].ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        if (playerCamera == null || playerInput == null) return;

        // Read Vector2 input from the New Input System (X = horizontal/strafe, Y = vertical/forward)
        
// 2. Get camera directions flattened on the horizontal plane
        Vector3 forward = playerCamera.forward;
        Vector3 right = playerCamera.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // 3. Calculate move direction relative to where you are looking
        Vector3 moveDir = (forward * _inputVector.y + right * _inputVector.x).normalized;

        // 4. Set velocity directly (keeps gravity on the Y axis intact)
        Vector3 targetVelocity = moveDir * moveSpeed;
        _rb.MovePosition(transform.position + targetVelocity * Time.fixedDeltaTime);
    }
}