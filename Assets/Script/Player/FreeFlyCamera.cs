using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Простая летающая камера для теста копания: WASD — движение, мышь — обзор,
/// Space/Ctrl — вверх/вниз, Shift — ускорение, Esc — отпустить курсор.
/// При старте телепортируется в стартовую комнату VoxelTerrain.
/// </summary>
public class FreeFlyCamera : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float sprintMultiplier = 3f;
    public float lookSensitivity = 0.12f;

    float pitch;
    float yaw;

    void Start()
    {
        var terrain = Object.FindFirstObjectByType<VoxelTerrain>();
        if (terrain != null)
            transform.position = terrain.SpawnPoint;

        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        LockCursor(true);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            LockCursor(false);
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (mouse.leftButton.wasPressedThisFrame)
                LockCursor(true);
            return;
        }

        Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) move += transform.forward;
        if (keyboard.sKey.isPressed) move -= transform.forward;
        if (keyboard.dKey.isPressed) move += transform.right;
        if (keyboard.aKey.isPressed) move -= transform.right;
        if (keyboard.spaceKey.isPressed) move += Vector3.up;
        if (keyboard.leftCtrlKey.isPressed) move -= Vector3.up;

        float speed = keyboard.leftShiftKey.isPressed ? moveSpeed * sprintMultiplier : moveSpeed;
        transform.position += move.normalized * (speed * Time.deltaTime);
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
