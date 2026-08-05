using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public enum CameraMode { FirstPerson, ThirdPerson }

    [Header("Modo actual")]
    public CameraMode currentMode = CameraMode.FirstPerson;

    [Header("Referencias")]
    public Transform cameraHolder;
    public Transform playerBody;    // El GameObject "Player" — usado en 3ª persona

    [Header("Tercera persona")]
    public float distance = 4f;
    public float height = 1.5f;
    public float smoothSpeed = 10f;

    [Header("Colision de camara")]
    public float collisionRadius = 0.2f;
    public LayerMask collisionMask = -1;

    private readonly RaycastHit[] _wallHits = new RaycastHit[1];
    private Vector3 _thirdPersonPosition;

    void Start()
    {
        if (cameraHolder == null)
        {
            Debug.LogWarning("[CameraController] cameraHolder not assigned! Disabling.");
            enabled = false;
            return;
        }

        // Si no se asignó playerBody, usar el parent de cameraHolder (normalmente Player)
        if (playerBody == null && cameraHolder.parent != null)
            playerBody = cameraHolder.parent;

        // Posición inicial 3ª persona
        Vector3 behind = GetPlayerHorizontalBack();
        _thirdPersonPosition = cameraHolder.position + Vector3.up * height + behind * distance;

        // Asegurar que la cámara no sea hija de nada
        if (transform.parent != null)
            transform.SetParent(null, true);
    }

    void Update()
    {
        if (cameraHolder == null) return;

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            ToggleMode();

        if (currentMode == CameraMode.FirstPerson)
            FollowFirstPerson();
        else
            FollowThirdPerson();
    }

    void ToggleMode()
    {
        bool wasFirstPerson = currentMode == CameraMode.FirstPerson;
        currentMode = wasFirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson;

        // Al salir de 1ª persona: la cámara está en la cabeza del jugador.
        // Moverla detrás del jugador para que no empiece pegada a la cara.
        if (wasFirstPerson)
        {
            Vector3 behind = GetPlayerHorizontalBack();
            _thirdPersonPosition = cameraHolder.position + Vector3.up * height + behind * distance;
            transform.position = _thirdPersonPosition;
        }

        Debug.Log($"[CameraController] Switched to {currentMode}");
    }

    void FollowFirstPerson()
    {
        // Cámara pegada a la cabeza del jugador
        transform.position = cameraHolder.position;
        transform.rotation = cameraHolder.rotation;
    }

    void FollowThirdPerson()
    {
        // Origen: cabeza del jugador + altura
        Vector3 origin = cameraHolder.position + Vector3.up * height;

        // Dirección hacia atrás del jugador (horizontal solamente, sin el pitch de la cámara)
        Vector3 behind = GetPlayerHorizontalBack();

        float desiredDistance = distance;

        // Colisión con paredes: si hay algo entre la cámara y el jugador, acercarla
        int hits = Physics.SphereCastNonAlloc(origin, collisionRadius, behind, _wallHits, desiredDistance, collisionMask);
        if (hits > 0 && _wallHits[0].distance > 0.01f)
            desiredDistance = Mathf.Max(0.1f, _wallHits[0].distance - collisionRadius);

        Vector3 target = origin + behind * desiredDistance;

        // Suavizado para evitar tirones
        _thirdPersonPosition = Vector3.Lerp(_thirdPersonPosition, target, smoothSpeed * Time.deltaTime);
        transform.position = _thirdPersonPosition;

        // Mirar al jugador (un poco arriba de los pies)
        transform.LookAt(cameraHolder.position + Vector3.up * 0.5f);
    }

    /// <summary>
    /// Dirección horizontal hacia atrás del jugador (ignora el pitch / mirada vertical).
    /// Usa playerBody.forward si existe; si no, usa cameraHolder.forward aplanado.
    /// </summary>
    Vector3 GetPlayerHorizontalBack()
    {
        Vector3 forward;
        if (playerBody != null)
            forward = playerBody.forward;
        else
            forward = cameraHolder.forward;

        // Aplanar a horizontal
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        return -forward; // hacia atrás
    }
}
