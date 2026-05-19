using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float crouchSpeed = 2f;
    public float jumpForce = 5f;
    public float gravity = -20f;

    [Header("Agacharse")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 8f;

    [Header("Camara")]
    public float mouseSensitivity = 2f;
    public Transform cameraHolder;
    public float standCameraY = 0.5f;
    public float crouchCameraY = 0f;

    private CharacterController _cc;
    private PlayerStats _stats;
    private Vector3 _velocity;
    private float _xRotation;
    private bool _isCrouching;
    private bool _isSprinting;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleGroundCheck();
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    bool IsGrounded()
    {
        // SphereCast desde los pies del jugador (en espacio del mundo)
        float bottomY = transform.position.y + _cc.center.y - _cc.height / 2f + _cc.radius;
        Vector3 bottom = new Vector3(transform.position.x, bottomY, transform.position.z);
        return Physics.SphereCast(bottom, _cc.radius * 0.9f, Vector3.down, out _, 0.15f);
    }

    bool HasCeilingAbove()
    {
        float topY = transform.position.y + _cc.center.y + _cc.height / 2f;
        float needed = standHeight - crouchHeight + 0.1f;
        return Physics.Raycast(new Vector3(transform.position.x, topY, transform.position.z), Vector3.up, needed);
    }

    void HandleGroundCheck()
    {
        if (IsGrounded() && _velocity.y < 0)
            _velocity.y = -2f;
    }

    void HandleCrouch()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool wantsCrouch = kb.leftCtrlKey.isPressed;

        if (_isCrouching && !wantsCrouch && HasCeilingAbove())
            wantsCrouch = true;

        _isCrouching = wantsCrouch;

        float targetHeight = _isCrouching ? crouchHeight : standHeight;
        float targetCameraY = _isCrouching ? crouchCameraY : standCameraY;

        _cc.height = Mathf.Lerp(_cc.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        // mantiene el fondo del CC siempre al mismo nivel (los pies no se mueven)
        _cc.center = new Vector3(0, (_cc.height - standHeight) / 2f, 0);

        Vector3 camPos = cameraHolder.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetCameraY, crouchTransitionSpeed * Time.deltaTime);
        cameraHolder.localPosition = camPos;
    }

    void HandleMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

        bool wantsSprint = kb.leftShiftKey.isPressed && !_isCrouching;
        bool isMoving = (x != 0 || z != 0);

        if (wantsSprint && isMoving && _stats != null && _stats.CanRun())
        {
            _isSprinting = _stats.UseRunStamina();
        }
        else
        {
            _isSprinting = false;
            if (_stats != null) _stats.ResetRunTimer();
        }

        float speed = _isCrouching ? crouchSpeed : (_isSprinting ? runSpeed : walkSpeed);
        Vector3 move = transform.right * x + transform.forward * z;
        _cc.Move(move.normalized * speed * Time.deltaTime);
    }

    void HandleMouseLook()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float mouseX = mouse.delta.x.ReadValue() * mouseSensitivity;
        float mouseY = mouse.delta.y.ReadValue() * mouseSensitivity;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);

        cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleJump()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame && IsGrounded() && !_isCrouching)
        {
            // Salto no consume stamina — solo el sprint drena stamina
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }

    void ApplyGravity()
    {
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }
}
