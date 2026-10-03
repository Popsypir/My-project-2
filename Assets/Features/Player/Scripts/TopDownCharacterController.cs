using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TopDownCharacterController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Head Settings")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Vector3 headRotationOffset = Vector3.zero;

    private CharacterController controller;
    private Camera mainCamera;
    private Vector3 lookTarget;

    private PlayerControls controls;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Gameplay.Enable();
    }

    private void OnDisable()
    {
        controls.Gameplay.Disable();
    }

    private void Update()
    {
        MoveCharacter();
        UpdateLookTarget();
    }

    private void LateUpdate()
    {
        RotateHead();
    }

    private void MoveCharacter()
    {
        Vector2 moveInput = controls.Gameplay.Move.ReadValue<Vector2>();

        if (moveInput.sqrMagnitude < 0.001f) return;

        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();
        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;

        controller.SimpleMove(moveDirection * moveSpeed);
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateLookTarget()
    {
        Vector2 mousePosition = controls.Gameplay.Look.ReadValue<Vector2>();

        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, groundLayer))
        {
            lookTarget = hitInfo.point;
        }
    }

    private void RotateHead()
    {
        if (headBone == null) return;

        Vector3 directionToTarget = lookTarget - headBone.position;

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion targetHeadRotation = Quaternion.LookRotation(directionToTarget);
            headBone.rotation = targetHeadRotation * Quaternion.Euler(headRotationOffset);
        }
    }
}