using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RunRich
{
    // Горизонтальное руление свайпом, как в референсе: держим палец (или мышь)
    // в любом месте экрана и ведём влево / вправо, бегун идёт за свайпом.
    // Поддержаны новая Input System, старая и программное управление (автопилот).
    [DisallowMultipleComponent]
    public sealed class DragInput : MonoBehaviour
    {
        // Нормализованная боковая цель: -1 = левый край дороги, +1 = правый.
        public float Steering { get; private set; }

        // Правда, пока игрок держит экран.
        public bool IsDragging { get; private set; }

        // Правда, как только игрок хоть раз тронул уровень (прячет туториал).
        public bool HasUserInput { get; private set; }

        // Поднято на первом касании уровня.
        public event Action FirstInput;

        // Когда правда, рулением владеет код, а не игрок.
        public bool Programmatic { get; private set; }

        private Vector2 _lastPointerPosition;
        private bool _pointerWasDown;
        private float _programmaticValue;

        // Хук автопилота: рулит без касания устройства.
        public void SetProgrammaticSteering(float value)
        {
            Programmatic = true;
            _programmaticValue = Mathf.Clamp(value, -1f, 1f);
            Steering = _programmaticValue;
        }

        // Хук автопилота: переходит на программное руление и поднимает FirstInput,
        // чтобы забег стартовал без живого касания.
        public void EnableProgrammaticInput()
        {
            Programmatic = true;

            if (HasUserInput) return;
            HasUserInput = true;
            FirstInput?.Invoke();
        }

        public void ClearProgrammaticSteering() => Programmatic = false;

        public void ResetSteering()
        {
            Steering = 0f;
            _programmaticValue = 0f;
            HasUserInput = false;
        }

        private void Update()
        {
            if (Programmatic)
            {
                Steering = _programmaticValue;
                return;
            }

            if (!TryReadPointer(out Vector2 position, out bool isDown))
            {
                IsDragging = false;
                _pointerWasDown = false;
                return;
            }

            if (isDown && !_pointerWasDown)
            {
                _lastPointerPosition = position;
                _pointerWasDown = true;
                IsDragging = true;
                RegisterInteraction();
            }
            else if (isDown)
            {
                float delta = position.x - _lastPointerPosition.x;
                _lastPointerPosition = position;
                IsDragging = true;
                if (Mathf.Abs(delta) > 0.01f) RegisterInteraction();

                float screenSpan = Mathf.Max(1f, Screen.width * GameConfig.SteerScreenSpan);
                Steering = Mathf.Clamp(Steering + delta / screenSpan, -1f, 1f);
            }
            else
            {
                IsDragging = false;
                _pointerWasDown = false;
            }
        }

        private void RegisterInteraction()
        {
            if (HasUserInput) return;
            HasUserInput = true;
            FirstInput?.Invoke();
        }

        private bool TryReadPointer(out Vector2 position, out bool isDown)
        {
#if ENABLE_INPUT_SYSTEM
            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                position = pointer.position.ReadValue();
                isDown = pointer.press.isPressed;
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                position = touch.position;
                isDown = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                return true;
            }

            position = Input.mousePosition;
            isDown = Input.GetMouseButton(0);
            return true;
#else
            position = Vector2.zero;
            isDown = false;
            return false;
#endif
        }
    }
}
