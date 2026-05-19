using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public enum CameraMode { FirstPerson, ThirdPerson }

    [Header("Modo actual")]
    public CameraMode currentMode = CameraMode.FirstPerson;  // Primera persona es el modo principal

    [Header("Referencias")]
    public Transform cameraHolder;

    [Header("Tercera persona")]
    public float distance = 4f;
    public float height = 1.5f;
    public float smoothSpeed = 10f;

    [Header("Colision de camara")]
    public float collisionRadius = 0.2f;
    public LayerMask collisionMask;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            ToggleMode();

        if (currentMode == CameraMode.ThirdPerson)
            FollowThirdPerson();
    }

    void ToggleMode()
    {
        currentMode = currentMode == CameraMode.FirstPerson
            ? CameraMode.ThirdPerson
            : CameraMode.FirstPerson;

        if (currentMode == CameraMode.FirstPerson)
        {
            transform.SetParent(cameraHolder);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            transform.SetParent(null);
        }
    }

    void FollowThirdPerson()
    {
        Vector3 origin = cameraHolder.position + Vector3.up * height;
        Vector3 direction = -cameraHolder.forward;
        float desiredDistance = distance;

        // si hay una pared entre el jugador y la camara, acercarla
        if (Physics.SphereCast(origin, collisionRadius, direction, out RaycastHit hit, desiredDistance, collisionMask))
            desiredDistance = hit.distance - collisionRadius;

        Vector3 target = origin + direction * desiredDistance;

        transform.position = Vector3.Lerp(transform.position, target, smoothSpeed * Time.deltaTime);
        transform.LookAt(cameraHolder.position + Vector3.up * 0.5f);
    }
}
