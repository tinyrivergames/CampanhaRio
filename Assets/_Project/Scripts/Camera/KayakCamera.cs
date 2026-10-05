using System;
using CampanhaRio.Kayak;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampanhaRio.CameraSystem
{
    /// <summary>
    /// Close orbit camera around the paddler. Low and near, so the forest surrounds the view:
    ///   pivot on the paddler's chest, ~3.2 m away at ~11° pitch (≈1.4 m above the water), mouse / right stick orbit,
    ///   wheel zoom. After a few seconds without look input it eases back behind the kayak's VELOCITY (not its facing:
    ///   the kayak can be angled against the current). Steering never depends on the camera.
    /// Calm and fast water add small offsets on top of this framing. Never below the water; solid obstacles pull it in
    /// fast and let it out slowly; foliage between camera and paddler dither-fades instead of pushing (shader side).
    /// Subtle handheld drift and landing/bump impulses keep it alive.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class KayakCamera : MonoBehaviour
    {
        [Serializable]
        public struct Offsets
        {
            [Tooltip("Added to the distance (m).")] public float distance;
            [Tooltip("Added to the pitch (degrees).")] public float pitch;
            [Tooltip("Added to the FOV (degrees).")] public float fov;
            [Tooltip("Multiplies the follow smoothing time.")] public float followScale;
        }

        static readonly int PivotId = Shader.PropertyToID("_CayaCameraPivot");

        [Tooltip("Leave empty: the camera follows the LocalPlayer kayak.")]
        public KayakController target;
        public KayakInput input;

        [Header("Framing")]
        [Tooltip("Orbit pivot above the kayak origin (m): the paddler's upper chest.")]
        public float pivotHeight = 0.75f;
        [Tooltip("Pivot shift toward the travel direction (m), so a little more river shows ahead.")]
        public float pivotLead = 0.6f;
        [Tooltip("Over-the-shoulder offset to the camera's right (m), so the paddler doesn't hide the line ahead.")]
        public float shoulderOffset = 0.35f;
        public float distance = 4.6f; // a wider view of what happens around (parcels in the water, the other boats)
        public Vector2 distanceRange = new Vector2(2.5f, 5.5f);
        [Tooltip("Default pitch (degrees, + = looking down).")]
        public float pitch = 16f;
        public Vector2 pitchRange = new Vector2(-8f, 40f);
        [Tooltip("The camera aims this far above the pivot (m). Negative puts the paddler lower in the frame.")]
        public float aimHeight = 0.3f;
        public float baseFov = 62f;

        [Header("Context offsets")]
        [Tooltip("Rapids: a bit lower and closer, slightly wider FOV.")]
        public Offsets fast = new Offsets { distance = -0.35f, pitch = -2.5f, fov = 3f, followScale = 0.7f };
        [Tooltip("Calm water and backwaters: a bit higher and slower.")]
        public Offsets calm = new Offsets { distance = 0.3f, pitch = 3f, fov = 0f, followScale = 1.5f };
        [Tooltip("Current speed (m/s) at or below which the calm offsets apply fully.")]
        public float calmCurrent = 1f;
        [Tooltip("Current speed (m/s) at or above which the fast offsets apply fully.")]
        public float fastCurrent = 5f;
        public float contextBlendTime = 1.5f;

        [Header("Speed (Stage 8)")]
        [Tooltip("Kayak speed range (m/s) over which the speed effects ramp in.")]
        public Vector2 speedRange = new Vector2(3f, 11f);
        [Tooltip("FOV added at the top of the speed range, and while boosting (degrees). 62 -> 72, ~76 boosting.")]
        public float speedFov = 10f;
        public float boostFov = 4f;
        [Tooltip("Extra FOV while the water surges the kayak forward (degrees); the old Natural game feel value.")]
        public float surgeFov = 2f;
        [Tooltip("At top speed the camera sits this much closer (m) and lower (degrees of pitch).")]
        public float speedCloser = 0.25f;
        public float speedLower = 1.5f;
        [Tooltip("Look ahead along the velocity (m per m/s), capped (m).")]
        public float lookAheadPerSpeed = 0.1f;
        public float maxLookAhead = 1.4f;
        [Tooltip("Tiny high-frequency shake in whitewater only (m) and its frequency (Hz).")]
        public float whitewaterShake = 0.025f;
        public float shakeFrequency = 17f;
        [Tooltip("Brush-stroke speed streaks at the screen edges: strength, and the speed range (m/s) over which they appear.")]
        [Range(0f, 1f)] public float streakStrength = 0.8f;
        public Vector2 streakSpeedRange = new Vector2(6.5f, 12f);
        [Tooltip("Water drops on the lens after a big splash: chance per landing or hard hit, and how fast they dry (1/s).")]
        [Range(0f, 1f)] public float lensDropChance = 0.6f;
        public float lensDryRate = 0.8f;

        [Header("Mouse / right stick")]
        [Tooltip("Degrees per unit of look input (mouse delta is pre-scaled in the Input Actions).")]
        public float sensitivity = 2.2f;
        [Tooltip("Degrees per second at full stick deflection.")]
        public float stickSpeed = 110f;
        public bool invertY;
        [Tooltip("Look smoothing (1/s). Higher = snappier.")]
        public float lookSmoothing = 18f;
        [Tooltip("Meters per wheel notch / per second of d-pad.")]
        public float zoomSpeed = 0.45f;
        [Tooltip("Lock and hide the cursor in Play mode (Esc unlocks, click re-locks).")]
        public bool lockCursor = true;

        [Header("Auto-recenter")]
        public bool autoRecenter = true;
        [Tooltip("Seconds without look input before easing back behind the direction of travel.")]
        public float recenterDelay = 2.5f;
        [Tooltip("How fast it eases back (1/s).")]
        public float recenterSpeed = 1.1f;
        [Tooltip("Below this speed (m/s) the camera follows the kayak's facing instead of its velocity.")]
        public float velocityHeadingSpeed = 0.6f;

        [Header("Follow")]
        [Tooltip("Pivot position lag (s).")]
        public float followSmoothTime = 0.12f;
        [Tooltip("Extra vertical lag while airborne (s), so jumps read well.")]
        public float airborneVerticalLag = 0.3f;

        [Header("Water and obstacles")]
        [Tooltip("Never closer than this to the water surface (m).")]
        public float minHeightAboveWater = 0.25f;
        public bool avoidObstacles = true;
        [Tooltip("Solid layers that block the camera (terrain, cliffs, rocks). Foliage is NOT here: it dither-fades instead.")]
        public LayerMask collisionLayers;
        public float collisionRadius = 0.25f;
        public float minDistance = 1.2f;
        [Tooltip("How fast the camera moves in when blocked / eases back out when clear (1/s).")]
        public float pullInSpeed = 14f;
        public float easeOutSpeed = 1.5f;
        [Tooltip("Foliage within this radius of the camera→paddler line fades out (m). Sent to the foliage shaders.")]
        public float foliageFadeRadius = 1.3f;

        [Header("Life")]
        [Tooltip("Master scale for handheld drift, roll and impulses (0 = rock steady).")]
        [Range(0f, 2f)] public float intensity = 1f;
        public bool handheld = true;
        [Tooltip("Handheld drift: rotation (degrees) and speed.")]
        public float handheldAngle = 0.35f;
        public float handheldSpeed = 0.35f;
        [Tooltip("Camera roll per degree/second of turning, and its cap (degrees).")]
        public float turnRoll = 0.03f;
        public float maxRoll = 2.5f;
        [Tooltip("Impulse on a landing (m per m/s of impact) and on a full bump (m).")]
        public float landingImpulse = 0.08f;
        public float bumpImpulse = 0.1f;
        [Tooltip("Impulse multiplier per impact level: bump, scrape, spin-out, capsize.")]
        public float[] impulsePerLevel = { 1f, 1.3f, 2f, 3f };
        public float impulseRecovery = 7f;
        [Tooltip("Juice (Stage 8): how quickly an FOV punch (a boost) settles back (1/s).")]
        public float fovPunchRecovery = 3f;

        [Header("Capsize")]
        [Tooltip("The camera lowers this much (m) while the kayak is upside down, keeping its framing (it never rolls with the kayak).")]
        public float capsizeLower = 0.3f;
        [Tooltip("How fast the underwater look fades in and out (1/s).")]
        public float underwaterFade = 4f;

        /// <summary>0..1: how "underwater" the view is (the HUD tints the screen blue with it).</summary>
        public float UnderwaterAmount { get; private set; }
        /// <summary>Audio hook (Etapa 15): true when the view goes under, false when it comes back up.</summary>
        public event Action<bool> Underwater;

        public float Yaw => yaw;
        public float Pitch => currentPitch;

        Camera cam;
        float yaw, pitchInput, targetYaw, targetPitch, currentPitch, zoom;
        float lastLookTime = -100f;
        float context, contextVelocity, fov, roll;
        float allowedDistance = float.MaxValue;
        Vector3 pivot, pivotVelocity;
        float pivotY, pivotYVelocity;
        Vector3 impulse, impulseVelocity;
        float fovPunch;

        /// <summary>A camera kick (juice): a velocity added to the spring-back impulse (m/s, scaled by Intensity).</summary>
        public void Kick(Vector3 velocity) => impulseVelocity += velocity * intensity;

        /// <summary>A quick FOV widening that settles back (a boost punch).</summary>
        public void FovPunch(float degrees) => fovPunch += degrees * Mathf.Clamp01(intensity);
        readonly RaycastHit[] hits = new RaycastHit[8];
        KayakController subscribed;
        bool started;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (collisionLayers.value == 0) collisionLayers = LayerMask.GetMask("Environment", "Obstacle");
        }

        void OnEnable()
        {
            KayakRegistry.LocalChanged += Follow;
            Follow(target ? target : LocalKayak());
        }

        void OnDisable()
        {
            KayakRegistry.LocalChanged -= Follow;
            Unsubscribe();
            if (lockCursor) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            Shader.SetGlobalVector(SpeedStreaksId, Vector4.zero);
            Shader.SetGlobalVector(LensId, Vector4.zero);
        }

        /// <summary>The kayak marked LocalPlayer (or any kayak when none is marked, e.g. an old test scene).</summary>
        static KayakController LocalKayak() =>
            KayakRegistry.Local ? KayakRegistry.Local : KayakRegistry.All.Count > 0 ? KayakRegistry.All[0] : FindAnyObjectByType<KayakController>();

        /// <summary>Follow another kayak (the local player's; called when it changes).</summary>
        public void Follow(KayakController kayak)
        {
            if (!kayak) kayak = LocalKayak();
            if (subscribed == kayak && target == kayak) return;
            Unsubscribe();
            target = kayak;
            if (!target) return;
            input = target.input;
            target.Respawned += Snap;
            target.OnLand += OnLanded;
            target.OnImpact += OnImpacted;
            subscribed = target;
            if (started) Snap();
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed.Respawned -= Snap;
            subscribed.OnLand -= OnLanded;
            subscribed.OnImpact -= OnImpacted;
            subscribed = null;
        }

        void Start()
        {
            started = true;
            sensitivity *= Core.GameSettings.MouseSensitivity; // the player's settings (Stage 9.5)
            stickSpeed *= Core.GameSettings.MouseSensitivity;
            if (Core.GameSettings.InvertY) invertY = !invertY;
            if (!target) Follow(LocalKayak());
            Snap();
            if (lockCursor && Application.isPlaying && !Application.isBatchMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        void OnLanded(float airTime)
        {
            impulseVelocity += Vector3.down * target.LastLandingImpact * landingImpulse * 6f * intensity;
            if (airTime > 0.35f) SplashLens(0.5f + airTime * 0.5f);
        }

        void OnImpacted(ImpactLevel level, float strength, Vector3 point, Vector3 normal)
        {
            float scale = impulsePerLevel[Mathf.Clamp((int)level, 0, impulsePerLevel.Length - 1)];
            impulseVelocity += UnityEngine.Random.onUnitSphere * strength * bumpImpulse * scale * 6f * intensity;
            if (level >= ImpactLevel.Scrape) SplashLens(0.4f + 0.2f * (int)level);
        }

        /// <summary>Jump behind the kayak (start and respawn).</summary>
        public void Snap()
        {
            if (!target) return;
            yaw = targetYaw = HeadingYaw();
            pitchInput = targetPitch = 0f;
            currentPitch = pitch;
            zoom = 0f;
            context = contextVelocity = 0f;
            pivot = PivotTarget();
            pivotY = pivot.y;
            pivotVelocity = Vector3.zero;
            pivotYVelocity = 0f;
            impulse = impulseVelocity = Vector3.zero;
            fovPunch = 0f;
            UnderwaterAmount = 0f;
            allowedDistance = float.MaxValue;
            fov = baseFov;
            if (cam) cam.fieldOfView = fov;
            Place(0f, true);
        }

        void LateUpdate()
        {
            if (!target) { Follow(null); if (!target) return; }
            float dt = Time.deltaTime;
            HandleCursor();
            ReadLook(dt);

            // Context: -1 calm .. +1 fast
            float current = target.CurrentSpeed;
            float targetContext = current <= calmCurrent ? -1f : current >= fastCurrent ? 1f
                : Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(calmCurrent, fastCurrent, current));
            context = Mathf.SmoothDamp(context, targetContext, ref contextVelocity, contextBlendTime * 0.5f);

            // Follow the pivot (vertical lag while airborne)
            var offsets = CurrentOffsets();
            Vector3 wanted = PivotTarget();
            Vector3 flat = Vector3.SmoothDamp(new Vector3(pivot.x, 0f, pivot.z), new Vector3(wanted.x, 0f, wanted.z), ref pivotVelocity,
                followSmoothTime * offsets.followScale);
            pivotY = Mathf.SmoothDamp(pivotY, wanted.y, ref pivotYVelocity, target.IsAirborne ? airborneVerticalLag : followSmoothTime);
            pivot = new Vector3(flat.x, pivotY, flat.z);

            // Impulses (spring back)
            impulseVelocity += (-impulse * impulseRecovery * impulseRecovery - impulseVelocity * impulseRecovery * 1.4f) * dt;
            impulse += impulseVelocity * dt;

            Place(dt, false);

            float targetFov = baseFov + offsets.fov + speedFov * SpeedShare + boostFov * target.BoostVisual + surgeFov * target.SurgeVisual;
            fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-1.5f * dt));
            fovPunch = Mathf.Lerp(fovPunch, 0f, 1f - Mathf.Exp(-fovPunchRecovery * dt));
            cam.fieldOfView = fov + fovPunch;
            Shader.SetGlobalVector(PivotId, new Vector4(pivot.x, pivot.y, pivot.z, foliageFadeRadius));
            UpdateUnderwater(dt);
            UpdateSpeedFx(dt);
        }



        // ------------------------------------------------------------------ speed (Stage 8)

        static readonly int SpeedStreaksId = Shader.PropertyToID("_CayaSpeedStreaks"), LensId = Shader.PropertyToID("_CayaLens");
        float speedShare, streakScroll, lensDrops, lensSeed, whitewater;

        /// <summary>0..1: how far into the speed range the kayak is (smoothed, so effects don't flicker).</summary>
        float SpeedShare => speedShare;

        void UpdateSpeedFx(float dt)
        {
            float speed = target.Speed;
            float wanted = Mathf.InverseLerp(speedRange.x, speedRange.y, speed);
            speedShare = Mathf.Lerp(speedShare, wanted, 1f - Mathf.Exp(-2.5f * dt));

            // Whitewater: on a wave train, ledge or pour-over, or simply in very fast water
            string f = target.FeatureName;
            float wantedWhite = f.StartsWith("ChuteWaveTrain") || f.StartsWith("Ledge") || f.StartsWith("PourOver") ? 1f
                              : Mathf.InverseLerp(7f, 10f, target.CurrentSpeed);
            whitewater = Mathf.Lerp(whitewater, target.IsAirborne ? 0f : wantedWhite, 1f - Mathf.Exp(-4f * dt));

            float streaks = streakStrength * intensity * Mathf.Clamp01(Mathf.InverseLerp(streakSpeedRange.x, streakSpeedRange.y, speed) + target.BoostVisual * 0.6f);
            streakScroll += dt * (0.25f + speed * 0.06f);
            lensDrops = Mathf.MoveTowards(lensDrops, 0f, lensDryRate * dt);
            Shader.SetGlobalVector(SpeedStreaksId, new Vector4(Mathf.Clamp01(streaks), streakScroll, target.BoostVisual, 0f));
            Shader.SetGlobalVector(LensId, new Vector4(lensDrops, lensSeed, 0f, 0f));
        }

        Vector3 Shake()
        {
            float amount = whitewaterShake * whitewater * intensity;
            if (amount <= 0f) return Vector3.zero;
            float t = Time.time * shakeFrequency;
            return new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * amount);
        }

        /// <summary>Bow spray on the lens (visual only, so plain Random is fine).</summary>
        void SplashLens(float strength)
        {
            if (UnityEngine.Random.value > lensDropChance) return;
            lensDrops = Mathf.Max(lensDrops, Mathf.Clamp01(strength));
            lensSeed = UnityEngine.Random.value * 10f;
        }
        /// <summary>Upside down (capsized, or most of the way over): the view goes "under" for the tint and the audio hook.</summary>
        void UpdateUnderwater(float dt)
        {
            var mode = target.Mode;
            bool over = (mode == KayakMode.Capsizing || mode == KayakMode.Capsized || mode == KayakMode.Rolling) && target.State.capsizeProgress > 0.55f;
            bool wasUnder = UnderwaterAmount > 0.5f;
            UnderwaterAmount = Mathf.MoveTowards(UnderwaterAmount, over ? 1f : 0f, underwaterFade * dt);
            bool isUnder = UnderwaterAmount > 0.5f;
            if (isUnder != wasUnder) Underwater?.Invoke(isUnder);
        }
        void HandleCursor()
        {
            if (!lockCursor || !Application.isPlaying || Application.isBatchMode) return;
            if (input && input.UnlockCursorPressed) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        void ReadLook(float dt)
        {
            Vector2 look = input ? input.Look : Vector2.zero;
            bool cursorFree = lockCursor && Application.isPlaying && Cursor.lockState != CursorLockMode.Locked && !(input && input.LookIsStick);
            if (cursorFree) look = Vector2.zero;
            if (look.sqrMagnitude > 1e-5f)
            {
                lastLookTime = Time.time;
                Vector2 delta = input.LookIsStick ? look * (stickSpeed * dt) : look * sensitivity;
                targetYaw += delta.x;
                targetPitch += (invertY ? delta.y : -delta.y);
            }
            float zoomInput = input ? input.Zoom : 0f;
            zoom = Mathf.Clamp(zoom - zoomInput * zoomSpeed, distanceRange.x - distance, distanceRange.y - distance);

            // Auto-recenter behind the direction of travel
            if (autoRecenter && Time.time - lastLookTime > recenterDelay)
            {
                float k = 1f - Mathf.Exp(-recenterSpeed * dt);
                targetYaw = Mathf.LerpAngle(targetYaw, HeadingYaw(), k);
                targetPitch = Mathf.Lerp(targetPitch, 0f, k);
            }
            targetPitch = Mathf.Clamp(targetPitch, pitchRange.x - pitch, pitchRange.y - pitch);
            float s = 1f - Mathf.Exp(-lookSmoothing * dt);
            yaw = Mathf.LerpAngle(yaw, targetYaw, s);
            pitchInput = Mathf.Lerp(pitchInput, targetPitch, s);
        }

        void Place(float dt, bool snap)
        {
            var offsets = CurrentOffsets();
            currentPitch = Mathf.Clamp(pitch + offsets.pitch + pitchInput - speedLower * SpeedShare, pitchRange.x, pitchRange.y);
            float wantedDistance = Mathf.Clamp(distance + offsets.distance + zoom - speedCloser * SpeedShare, distanceRange.x, distanceRange.y);
            Quaternion orbit = Quaternion.Euler(currentPitch, yaw, 0f);
            Vector3 dir = orbit * Vector3.back;

            float free = avoidObstacles ? FreeDistance(pivot, dir, wantedDistance) : wantedDistance;
            if (snap || allowedDistance > wantedDistance) allowedDistance = snap ? free : wantedDistance;
            float speed = free < allowedDistance ? pullInSpeed : easeOutSpeed;
            allowedDistance = snap ? free : Mathf.Lerp(allowedDistance, free, 1f - Mathf.Exp(-speed * dt));
            Vector3 position = pivot + dir * Mathf.Min(wantedDistance, allowedDistance) + impulse + Shake();

            // Never below the water
            if (target.river)
            {
                float waterY = target.river.GetWaterHeight(position) + minHeightAboveWater;
                if (position.y < waterY) position.y = waterY;
            }

            // Handheld drift + turn roll
            float t = Time.time * handheldSpeed;
            float h = handheld ? handheldAngle * intensity : 0f;
            Quaternion drift = Quaternion.Euler((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * h, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * h, 0f);
            float targetRoll = Mathf.Clamp(-target.YawRate * turnRoll, -maxRoll, maxRoll) * intensity;
            roll = snap ? targetRoll : Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-2f * dt));

            // Look ahead along the velocity (only the aim moves; the camera keeps its distance to the paddler)
            Vector3 flatV = target.Velocity; flatV.y = 0f;
            Vector3 ahead = flatV.sqrMagnitude > 0.01f ? flatV.normalized * Mathf.Min(flatV.magnitude * lookAheadPerSpeed, maxLookAhead) : Vector3.zero;
            Vector3 aim = pivot + Vector3.up * aimHeight + ahead;
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(aim - position) * drift * Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>Spherecast from the pivot toward the camera against solid obstacles only.</summary>
        float FreeDistance(Vector3 from, Vector3 dir, float wanted)
        {
            float free = wanted;
            int count = Physics.SphereCastNonAlloc(from, collisionRadius, dir, hits, wanted, collisionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(target.transform)) continue;
                if (hits[i].distance <= 0f) continue; // started inside: ignore rather than snap
                free = Mathf.Min(free, hits[i].distance);
            }
            return Mathf.Max(free, minDistance);
        }

        Offsets CurrentOffsets()
        {
            var zero = new Offsets { followScale = 1f };
            var o = context < 0f ? calm : fast;
            float t = Mathf.Abs(context);
            return new Offsets
            {
                distance = Mathf.Lerp(zero.distance, o.distance, t),
                pitch = Mathf.Lerp(zero.pitch, o.pitch, t),
                fov = Mathf.Lerp(zero.fov, o.fov, t),
                followScale = Mathf.Lerp(1f, o.followScale, t),
            };
        }

        Vector3 PivotTarget()
        {
            Vector3 heading = Quaternion.Euler(0f, HeadingYaw(), 0f) * Vector3.forward;
            Vector3 cameraRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            return target.transform.position + Vector3.up * (pivotHeight - capsizeLower * UnderwaterAmount) + heading * pivotLead + cameraRight * shoulderOffset;
        }

        /// <summary>Yaw of the direction of travel (velocity when moving, facing when nearly still).</summary>
        float HeadingYaw()
        {
            Vector3 v = target.Velocity; v.y = 0f;
            Vector3 f = target.transform.forward; f.y = 0f;
            float blend = Mathf.InverseLerp(velocityHeadingSpeed * 0.5f, velocityHeadingSpeed * 1.5f, v.magnitude);
            Vector3 dir = Vector3.Slerp(f.normalized, v.sqrMagnitude > 1e-4f ? v.normalized : f.normalized, blend);
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
    }
}
