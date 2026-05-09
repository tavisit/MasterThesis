using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Runtime.Demo
{
    /// <summary>WASD moves along where the camera looks; hold right mouse to turn.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FloatingScreenshotCamera : MonoBehaviour
    {
        [SerializeField]
        private float _moveSpeed = 12f;

        [SerializeField]
        private float _mouseSensitivity = 0.12f;

        private float _yawDeg;
        private float _pitchDeg;

        private void Start()
        {
            Vector3 e = transform.eulerAngles;
            _yawDeg = e.y;
            _pitchDeg = e.x;
            if (_pitchDeg > 180f)
            {
                _pitchDeg -= 360f;
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb == null || mouse == null)
            {
                return;
            }

            ApplyLook(mouse, kb);
            ApplyMoveWASD(kb);
        }

        private void ApplyLook(Mouse mouse, Keyboard kb)
        {
            if (!mouse.rightButton.isPressed || kb.escapeKey.isPressed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 delta = mouse.delta.ReadValue();
            _pitchDeg = Mathf.Clamp(_pitchDeg - delta.y * _mouseSensitivity, -88f, 88f);
            _yawDeg += delta.x * _mouseSensitivity;
            transform.rotation = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
        }

        private void ApplyMoveWASD(Keyboard kb)
        {
            float step = _moveSpeed * Time.deltaTime;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed)
            {
                move += transform.forward;
            }

            if (kb.sKey.isPressed)
            {
                move -= transform.forward;
            }

            if (kb.dKey.isPressed)
            {
                move += transform.right;
            }

            if (kb.aKey.isPressed)
            {
                move -= transform.right;
            }

            if (move.sqrMagnitude > 1e-8f)
            {
                transform.position += move.normalized * step;
            }
        }
    }
}
