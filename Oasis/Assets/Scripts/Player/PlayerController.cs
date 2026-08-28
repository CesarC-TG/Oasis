using UnityEngine;
using UnityEngine.InputSystem;
using Oasis.Combat;

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

    [Header("Combate (melee)")]
    public float MeleeDamage = 25f;
    public float MeleeRange = 1.8f;
    public float MeleeRadius = 0.6f;
    public float MeleeCooldown = 0.5f;
    public float ComboWindow = 0.8f;
    public LayerMask MeleeLayerMask = -1;

    [Header("Esquiva")]
    public float DodgeCooldown = 0.5f;

    [Header("VFX (opcional)")]
    public ParticleSystem MeleeVFX;

    private CharacterController _cc;
    private PlayerStats _stats;
    private Animator _animator;
    private Vector3 _velocity;
    private float _xRotation;
    private bool _isCrouching;
    private bool _isSprinting;
    private float _meleeCooldownTimer;
    private float _dodgeCooldownTimer;

    // Combo system — cycles 0→1→2, resets after ComboWindow of inactivity
    private int _comboIndex;
    private float _comboTimer;

    // Hit detection — NonAlloc per ADR-0001
    private readonly Collider[] _meleeResults = new Collider[16];

    private static readonly int _hashSpeed         = Animator.StringToHash("Speed");
    private static readonly int _hashIsCrouching   = Animator.StringToHash("IsCrouching");
    private static readonly int _hashIsGrounded    = Animator.StringToHash("IsGrounded");
    private static readonly int _hashJump          = Animator.StringToHash("Jump");
    private static readonly int _hashIsRunning     = Animator.StringToHash("IsRunning");
    private static readonly int _hashAttack        = Animator.StringToHash("Attack");
    private static readonly int _hashAttackIndex   = Animator.StringToHash("AttackIndex");
    private static readonly int _hashComboIndex    = Animator.StringToHash("ComboIndex");
    private static readonly int _hashDodge         = Animator.StringToHash("Dodge");
    private static readonly int _hashDodgeDirection= Animator.StringToHash("DodgeDirection");
    private static readonly int _hashHeavyHit      = Animator.StringToHash("HeavyHit");
    private static readonly int _hashHitReaction   = Animator.StringToHash("HitReaction");
    private static readonly int _hashHitType       = Animator.StringToHash("HitType");
    private static readonly int _hashDeath         = Animator.StringToHash("Death");

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();
        _animator = GetComponentInChildren<Animator>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (_meleeCooldownTimer > 0f)
            _meleeCooldownTimer -= Time.deltaTime;

        if (_dodgeCooldownTimer > 0f)
            _dodgeCooldownTimer -= Time.deltaTime;

        // Combo window timer — tracks time since last attack
        if (_comboTimer > 0f)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f)
            {
                _comboIndex = 0;
            }
        }

        HandleGroundCheck();
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
        HandleJump();
        HandleDodge();
        HandleAttack();
        ApplyGravity();
        UpdateAnimator();
    }

    void UpdateAnimator()
    {
        if (_animator == null) return;

        float speed = 0f;
        var kb = Keyboard.current;
        if (kb != null)
        {
            bool isMoving = kb.wKey.isPressed || kb.sKey.isPressed ||
                            kb.aKey.isPressed || kb.dKey.isPressed;
            if (isMoving)
                speed = _isSprinting ? 1f : 0.5f;
        }

        _animator.SetFloat(_hashSpeed,       speed, 0.1f, Time.deltaTime);
        _animator.SetBool (_hashIsCrouching, _isCrouching);
        _animator.SetBool (_hashIsGrounded,  IsGrounded());
        _animator.SetBool (_hashIsRunning,   _isSprinting);
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
            _animator?.SetTrigger(_hashJump);
        }
    }

    void HandleDodge()
    {
        if (_dodgeCooldownTimer > 0f) return;

        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null || kb == null) return;
        if (!mouse.rightButton.wasPressedThisFrame) return;

        // Determine dodge direction from WASD input
        // 0 = forward, 1 = back, 2 = left, 3 = right
        int dodgeDir;
        if (kb.wKey.isPressed)
            dodgeDir = 0; // forward
        else if (kb.sKey.isPressed)
            dodgeDir = 1; // back
        else if (kb.aKey.isPressed)
            dodgeDir = 2; // left
        else if (kb.dKey.isPressed)
            dodgeDir = 3; // right
        else
            dodgeDir = 1; // default: back dodge (no directional input)

        _animator?.SetInteger(_hashDodgeDirection, dodgeDir);
        _animator?.SetTrigger(_hashDodge);

        _dodgeCooldownTimer = DodgeCooldown;
    }

    void HandleAttack()
    {
        if (_meleeCooldownTimer > 0f) return;

        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        // Combo system — cycles 0→1→2, resets after ComboWindow of inactivity
        _comboIndex = (_comboIndex + 1) % 3; // 0→1→2→0...
        _comboTimer = ComboWindow;

        if (_animator != null)
        {
            _animator.SetInteger(_hashComboIndex, _comboIndex);
            _animator.SetInteger(_hashAttackIndex, _comboIndex);
            _animator.SetTrigger(_hashAttack);
        }

        // Melee attack — OverlapSphereNonAlloc per ADR-0001
        Vector3 attackOrigin = cameraHolder.position + cameraHolder.forward * 0.5f;
        int hits = Physics.OverlapSphereNonAlloc(attackOrigin, MeleeRadius, _meleeResults, MeleeLayerMask);

        Debug.Log($"[Attack] Origin={attackOrigin}, Radius={MeleeRadius}, Hits={hits}, LayerMask={MeleeLayerMask.value}");

        bool hitSomething = false;
        for (int i = 0; i < hits; i++)
        {
            Debug.Log($"[Attack] Hit[{i}]: {_meleeResults[i].name}, has IDamageable={_meleeResults[i].TryGetComponent<IDamageable>(out _)}");

            // Skip self
            if (_meleeResults[i].transform.IsChildOf(transform)) continue;

            if (_meleeResults[i].TryGetComponent<IDamageable>(out var target))
            {
                var data = DamageData.Physical(MeleeDamage, gameObject);
                data.HitPoint = _meleeResults[i].ClosestPoint(attackOrigin);
                data.HitNormal = (attackOrigin - data.HitPoint).normalized;
                target.ApplyDamage(data);
                hitSomething = true;
            }
        }

        if (hitSomething && MeleeVFX != null)
            MeleeVFX.Play();

        _meleeCooldownTimer = MeleeCooldown;
    }

    void ApplyGravity()
    {
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }
}
