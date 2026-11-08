using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace UndeadOvertime.Prototype
{
    /// <summary>Local-only movement and hitscan test driver. This is not a multiplayer controller.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        private const float WalkSpeed = 4.5f;
        private const float SprintSpeed = 7f;
        private const float Gravity = -22f;
        private const float FireInterval = 0.18f;
        private const float ShotRange = 100f;
        private const int ShotDamage = 25;
        private const float EyeHeight = 1.62f;

        private CharacterController motor;
        private Camera view;
        private Renderer body;
        private LineRenderer tracer;
        private PrototypeRange range;
        private InputActionMap controls;
        private InputAction move, mouseLook, stickLook, fire, sprint, jump, toggleView, reset, release;
        private Vector3 spawnPosition;
        private float yaw, pitch, verticalVelocity, nextShotAt, tracerUntil, hitMarkerUntil;
        private bool initialized, inputEnabled = true, captured, waitForFireRelease;

        public bool IsThirdPerson { get; private set; }
        public bool IsInputCaptured => captured;
        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }
        public int Kills { get; private set; }
        public bool ShowHitMarker => Time.time < hitMarkerUntil;
        public Camera View => view;

        public void Initialize(Camera camera, Renderer visualBody, Material tracerMaterial, PrototypeRange owner)
        {
            if (initialized) return;
            motor = GetComponent<CharacterController>();
            view = camera;
            body = visualBody;
            range = owner;
            spawnPosition = transform.position;
            yaw = transform.eulerAngles.y;

            // A private map leaves the template's project-wide input actions untouched.
            controls = new InputActionMap("PrototypeRange");
            move = controls.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            mouseLook = controls.AddAction("MouseLook", InputActionType.Value, "<Mouse>/delta");
            stickLook = controls.AddAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick");
            fire = Button("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            toggleView = Button("ToggleView", "<Keyboard>/v", "<Gamepad>/select");
            reset = Button("Reset", "<Keyboard>/r", "<Gamepad>/start");
            release = Button("ReleaseCursor", "<Keyboard>/escape", "<Gamepad>/buttonEast");

            GameObject tracerObject = new GameObject("Shot tracer");
            tracerObject.transform.SetParent(transform, false);
            tracer = tracerObject.AddComponent<LineRenderer>();
            tracer.sharedMaterial = tracerMaterial;
            tracer.positionCount = 2;
            tracer.startWidth = tracer.endWidth = 0.025f;
            tracer.shadowCastingMode = ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.useWorldSpace = true;
            tracer.enabled = false;
            initialized = true;
            if (isActiveAndEnabled) controls.Enable();
            SetThirdPerson(false);
            SetCaptured(inputEnabled && Application.isFocused);
            UpdateCamera();
        }

        private InputAction Button(string name, string keyboardOrMouse, string gamepad)
        {
            InputAction action = controls.AddAction(name, InputActionType.Button, keyboardOrMouse);
            action.AddBinding(gamepad);
            return action;
        }

        private void OnEnable()
        {
            if (initialized && inputEnabled) controls.Enable();
        }

        private void OnDisable()
        {
            controls?.Disable();
            SetCaptured(false);
        }

        private void OnDestroy()
        {
            controls?.Dispose();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) SetCaptured(false);
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!initialized) return;
            if (enabled && isActiveAndEnabled) controls.Enable();
            else
            {
                controls.Disable();
                SetCaptured(false);
            }
        }

        private void SetCaptured(bool value)
        {
            if (!value && !captured) return;
            captured = value;
            if (value) waitForFireRelease = true;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }

        private void Update()
        {
            if (!initialized) return;
            if (tracer.enabled && Time.time >= tracerUntil) tracer.enabled = false;
            if (!fire.IsPressed()) waitForFireRelease = false;
            bool allowInput = inputEnabled && captured;
            if (inputEnabled && release.WasPressedThisFrame())
            {
                SetCaptured(false);
                allowInput = false;
            }
            else if (inputEnabled && !captured && Application.isFocused && fire.WasPressedThisFrame())
            {
                SetCaptured(true);
                // The recapture click must not accidentally fire a shot.
                MovePlayer(false);
                return;
            }

            if (allowInput)
            {
                Vector2 look = mouseLook.ReadValue<Vector2>() * 0.10f
                    + stickLook.ReadValue<Vector2>() * (120f * Time.deltaTime);
                yaw = Mathf.Repeat(yaw + look.x, 360f);
                pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (toggleView.WasPressedThisFrame()) SetThirdPerson(!IsThirdPerson);
                if (reset.WasPressedThisFrame())
                {
                    range.ResetRange();
                    return;
                }
            }
            MovePlayer(allowInput);
            UpdateCamera();
            if (allowInput && !waitForFireRelease && fire.IsPressed()) TryFire();
        }

        private void MovePlayer(bool allowInput)
        {
            Vector2 axes = allowInput ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            float speed = allowInput && sprint.IsPressed() ? SprintSpeed : WalkSpeed;
            if (motor.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (allowInput && motor.isGrounded && jump.WasPressedThisFrame())
                verticalVelocity = Mathf.Sqrt(1.1f * -2f * Gravity);
            verticalVelocity += Gravity * Time.deltaTime;
            Vector3 velocity = (transform.right * axes.x + transform.forward * axes.y) * speed;
            velocity.y = verticalVelocity;
            CollisionFlags collisions = motor.Move(velocity * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            if (transform.position.y < -10f) range.ResetRange();
        }

        public void SetThirdPerson(bool value)
        {
            IsThirdPerson = value;
            if (body != null) body.enabled = value;
            if (initialized) UpdateCamera();
        }

        private void LateUpdate()
        {
            if (initialized) UpdateCamera();
        }

        private void UpdateCamera()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = transform.position + Vector3.up * EyeHeight;
            Vector3 position = pivot;
            if (IsThirdPerson)
            {
                Vector3 offset = rotation * new Vector3(0.55f, 0.2f, -3.2f);
                float distance = offset.magnitude;
                if (Physics.SphereCast(pivot, 0.22f, offset.normalized, out RaycastHit hit,
                    distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    distance = Mathf.Max(0.05f, hit.distance - 0.05f);
                position += offset.normalized * distance;
            }
            view.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Runs the same local shot path for input and automated scene checks.</summary>
        public bool TryFire()
        {
            if (!initialized || Time.time < nextShotAt) return false;
            nextShotAt = Time.time + FireInterval;
            ShotsFired++;
            Ray aim = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 aimPoint = aim.GetPoint(ShotRange);
            if (Physics.Raycast(aim, out RaycastHit aimHit, ShotRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) aimPoint = aimHit.point;

            // The shoulder camera cannot shoot around cover: resolve the hit from inside the player's clearance.
            Vector3 muzzle = transform.position + Vector3.up * 1.4f
                + transform.forward * 0.15f + transform.right * 0.1f;
            Vector3 toAim = aimPoint - muzzle;
            Vector3 end = muzzle + toAim.normalized * Mathf.Min(toAim.magnitude + 0.02f, ShotRange);
            if (Physics.Raycast(muzzle, toAim.normalized, out RaycastHit hit,
                Mathf.Min(toAim.magnitude + 0.02f, ShotRange), Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                PrototypeTarget target = hit.collider.GetComponent<PrototypeTarget>();
                if (target != null && target.IsAlive)
                {
                    Hits++;
                    hitMarkerUntil = Time.time + 0.12f;
                    if (target.TakeDamage(ShotDamage)) Kills++;
                }
            }
            tracer.SetPosition(0, muzzle);
            tracer.SetPosition(1, end);
            tracerUntil = Time.time + 0.07f;
            tracer.enabled = true;
            return true;
        }

        public void ResetPlayer()
        {
            motor.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
            motor.enabled = true;
            yaw = pitch = verticalVelocity = nextShotAt = hitMarkerUntil = 0f;
            ShotsFired = Hits = Kills = 0;
            tracer.enabled = false;
            SetThirdPerson(false);
        }
    }
}
