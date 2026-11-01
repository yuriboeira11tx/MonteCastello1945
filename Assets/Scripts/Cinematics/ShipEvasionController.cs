using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using FEB.UI;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class ShipEvasionController : MonoBehaviour
    {
        [Header("Cena")]
        [SerializeField] Rigidbody vessel;
        [SerializeField] Transform shipVisual;
        [SerializeField] Collider[] hull;
        [SerializeField] Camera viewCamera;
        [SerializeField] CutsceneView view;
        [SerializeField] GameObject torpedoPrefab;
        [SerializeField] Material foamMaterial;
        [SerializeField] Material smokeMaterial;
        [SerializeField] float seaLevel = 3.82f;

        [Header("Manobra • metros e segundos")]
        [SerializeField, Min(.1f)] float cruiseSpeed = 5.8f;
        [SerializeField, Min(.1f)] float maximumSpeed = 7.2f;
        [SerializeField, Min(.01f)] float acceleration = .22f;
        [SerializeField, Range(.1f, 5)] float maximumTurnRate = 2.8f;
        [SerializeField, Min(.1f)] float rudderResponse = .4f;
        [SerializeField] Vector3 cameraOffset = new Vector3(45, 195, -190);

        [Header("Torpedos")]
        [SerializeField, Min(1)] float torpedoSpeed = 13;
        [SerializeField, Min(1)] float firstAttackDelay = 14;
        [SerializeField, Min(10)] float attackInterval = 36;
        [SerializeField, Min(80)] float launchDistance = 330;

        [Header("Encerramento")]
        [SerializeField, Min(20)] float finalTorpedoSpeed = 75;
        [SerializeField, Min(100)] float finalLaunchDistance = 210;
        [SerializeField] Vector3 finalCameraOffset = new Vector3(145, 65, -115);

        sealed class Torpedo
        {
            public Transform model;
            public ParticleSystem wake;
            public Vector3 position, direction;
            public float age;
            public bool active;
        }

        readonly Torpedo[] torpedoes = new Torpedo[4];
        ParticleSystem sternWake, bowPort, bowStarboard, funnelSmoke;
        Vector3 origin, cameraVelocity;
        Quaternion originRotation;
        Rect originalViewport;
        RenderTexture navigationTexture, originalTarget;
        float originalFov, heading, cameraHeading, speed, targetSpeed, rudder, yawRate, steeringInput;
        float simulationTime, nextAttack;
        readonly Vector2[] radarPositions = new Vector2[4];
        readonly Vector2[] radarDirections = new Vector2[4];
        int launchIndex;
        bool running, damaged, initialized, cinematicEnding;
        public int TorpedoesAvoided { get; private set; }
        public int Hits { get; private set; }
        public bool Damaged => damaged;
        public float Speed => speed;
        public float Rudder => rudder;
        public float Heading => Mathf.Repeat(heading, 360);
        public Vector3 Position => vessel.position;
        public bool Running => running;
        public Vector3 LastImpactPoint { get; private set; }
        public event System.Action HitReceived;

        void Awake()
        {
            origin = vessel.position;
            originRotation = vessel.rotation;
            originalViewport = viewCamera.rect;
            originalFov = viewCamera.fieldOfView;
            originalTarget = viewCamera.targetTexture;
            navigationTexture = new RenderTexture(960, 1080, 24, RenderTextureFormat.ARGBHalf)
            { name = "Navegacao_Itapage", antiAliasing = 1 };
            viewCamera.targetTexture = navigationTexture;
            vessel.isKinematic = true;
            vessel.useGravity = false;
            vessel.interpolation = RigidbodyInterpolation.Interpolate;
            for (int i = 0; i < torpedoes.Length; i++)
            {
                var model = Instantiate(torpedoPrefab, transform);
                model.name = "Torpedo_Manobra_" + (i + 1);
                foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
                var body = model.GetComponent<Rigidbody>();
                if (body != null) { body.isKinematic = true; body.useGravity = false; body.detectCollisions = false; }
                var actor = new Torpedo { model = model.transform };
                actor.wake = CreateFoam("Rastro_Torpedo_" + (i + 1), .9f, 4.5f, 5);
                actor.model.gameObject.SetActive(false);
                torpedoes[i] = actor;
            }
            sternWake = CreateFoam("Esteira_Popa", 10, 12, 4);
            bowPort = CreateFoam("Espuma_Proa_Bombordo", 3.5f, 6, 5);
            bowStarboard = CreateFoam("Espuma_Proa_Boreste", 3.5f, 6, 5);
            funnelSmoke = CreateSmoke();
            initialized = true;
        }

        void Start()
        {
            view.SetNavigationTexture(navigationTexture);
            ResetVessel();
        }

        public void BeginSession()
        {
            Hits = TorpedoesAvoided = 0;
            launchIndex = 0;
            ResetVessel();
        }

        public void SetRunning(bool value)
        {
            running = value;
            if (!initialized) return;
            SetEmission(sternWake, value && !damaged);
            SetEmission(bowPort, value && !damaged);
            SetEmission(bowStarboard, value && !damaged);
            SetEmission(funnelSmoke, value && !damaged);
            if (!value) ClearTorpedoes();
        }

        public IEnumerator PlayFinalAttack()
        {
            SetRunning(false);
            cinematicEnding = true;
            rudder = yawRate = 0;
            yield return new WaitForSeconds(1.2f);

            var forward = vessel.rotation * Vector3.forward;
            var right = vessel.rotation * Vector3.right;
            var spawn = vessel.position + right * finalLaunchDistance - forward * 25;
            spawn.y = seaLevel - 1.3f;
            var aim = hull[0].bounds.center;
            aim.y = spawn.y;
            var actor = torpedoes[0];
            ActivateTorpedo(actor, spawn, (aim - spawn).normalized);
            float deadline = Time.time + finalLaunchDistance / finalTorpedoSpeed + 2;
            while (!damaged && actor.active && Time.time < deadline)
            {
                AdvanceTorpedo(actor, Time.deltaTime, finalTorpedoSpeed);
                yield return null;
            }
            if (!damaged) Debug.LogError("O torpedo final não encontrou o casco.", this);
        }

        void ResetVessel()
        {
            vessel.position = origin;
            vessel.rotation = originRotation;
            heading = cameraHeading = originRotation.eulerAngles.y;
            shipVisual.localPosition = Vector3.zero;
            shipVisual.localRotation = Quaternion.Euler(0, -90, 0);
            speed = targetSpeed = cruiseSpeed;
            rudder = yawRate = steeringInput = simulationTime = 0;
            nextAttack = firstAttackDelay;
            damaged = false;
            cinematicEnding = false;
            ClearTorpedoes();
            foreach (var ps in new[] { sternWake, bowPort, bowStarboard, funnelSmoke }) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(); }
            cameraVelocity = Vector3.zero;
            UpdateCamera(true);
        }

        void Update()
        {
            if (!initialized) return;
            steeringInput = 0;
            if (!running) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!damaged)
                {
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steeringInput -= 1;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steeringInput += 1;
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) targetSpeed += Time.deltaTime * .8f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) targetSpeed -= Time.deltaTime * .8f;
                }
            }
            if (Gamepad.current != null && !damaged)
            {
                steeringInput += Gamepad.current.leftStick.x.ReadValue();
                targetSpeed += Gamepad.current.leftStick.y.ReadValue() * Time.deltaTime * .8f;
            }
            steeringInput = Mathf.Clamp(steeringInput, -1, 1);
            targetSpeed = Mathf.Clamp(targetSpeed, 1.5f, maximumSpeed);
        }

        void FixedUpdate()
        {
            if (!running || !initialized || damaged) return;
            float delta = Time.fixedDeltaTime;
            simulationTime += delta;
            rudder = Mathf.MoveTowards(rudder, steeringInput, rudderResponse * delta);
            float effectiveSpeed = targetSpeed * (1 - .16f * Mathf.Abs(rudder));
            speed = Mathf.MoveTowards(speed, effectiveSpeed, acceleration * delta);
            float desiredYaw = rudder * maximumTurnRate * Mathf.Clamp01(speed / cruiseSpeed);
            yawRate = Mathf.MoveTowards(yawRate, desiredYaw, .5f * delta);
            heading += yawRate * delta;
            var rotation = Quaternion.Euler(0, heading, 0);
            vessel.MoveRotation(rotation);
            vessel.MovePosition(vessel.position + rotation * Vector3.forward * (speed * delta));
            if (simulationTime >= nextAttack)
            {
                LaunchTorpedo();
                nextAttack = simulationTime + attackInterval;
            }
            foreach (var actor in torpedoes) if (actor.active) AdvanceTorpedo(actor, delta, torpedoSpeed);
        }

        void LateUpdate()
        {
            if (!initialized) return;
            float heave = .24f * Mathf.Sin(simulationTime * .8f) + .09f * Mathf.Sin(simulationTime * 1.43f);
            float roll = .55f * Mathf.Sin(simulationTime * .62f) - yawRate * .75f;
            float pitch = .22f * Mathf.Sin(simulationTime * .75f + 1);
            shipVisual.localPosition = Vector3.up * heave;
            shipVisual.localRotation = Quaternion.Euler(pitch, 0, roll) * Quaternion.Euler(0, -90, 0);
            UpdateWakes();
            UpdateCamera(false);
            UpdateRadar();
        }

        void LaunchTorpedo()
        {
            Torpedo actor = null;
            foreach (var candidate in torpedoes) if (!candidate.active) { actor = candidate; break; }
            if (actor == null) return;
            float side = launchIndex++ % 2 == 0 ? 1 : -1;
            var forward = vessel.rotation * Vector3.forward;
            var right = vessel.rotation * Vector3.right;
            var spawn = vessel.position + right * side * launchDistance + forward * (launchDistance * .24f);
            var offset = vessel.position - spawn;
            var velocity = forward * speed;
            float a = Vector3.Dot(velocity, velocity) - torpedoSpeed * torpedoSpeed;
            float b = 2 * Vector3.Dot(offset, velocity);
            float intercept = (-b - Mathf.Sqrt(Mathf.Max(0, b * b - 4 * a * offset.sqrMagnitude))) / (2 * a);
            var aim = vessel.position + velocity * Mathf.Max(1, intercept);
            var direction = (aim - spawn).normalized;
            direction.y = 0;
            ActivateTorpedo(actor, new Vector3(spawn.x, seaLevel - 1.3f, spawn.z), direction.normalized);
        }

        void ActivateTorpedo(Torpedo actor, Vector3 position, Vector3 direction)
        {
            actor.direction = direction;
            actor.position = position;
            actor.age = 0;
            actor.active = true;
            actor.model.SetPositionAndRotation(actor.position, Quaternion.LookRotation(actor.direction));
            actor.model.gameObject.SetActive(true);
            actor.wake.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            actor.wake.transform.position = new Vector3(position.x, seaLevel + 2.4f, position.z);
            actor.wake.Play();
        }

        void AdvanceTorpedo(Torpedo actor, float delta, float travelSpeed)
        {
            actor.age += delta;
            Vector3 step = actor.direction * (travelSpeed * delta);
            Vector3 tip = actor.position + actor.direction * 3.2f;
            bool hit = false;
            Vector3 contact = tip + step;
            foreach (var collider in hull)
            {
                if (!collider.enabled) continue;
                if (collider.Raycast(new Ray(tip, actor.direction), out var collision, step.magnitude + .3f))
                { hit = true; contact = collision.point; break; }
                if ((collider.ClosestPoint(contact) - contact).sqrMagnitude < .12f) { hit = true; break; }
            }
            if (hit && !damaged) { Hit(contact); return; }
            actor.position += step;
            actor.model.position = actor.position;
            actor.wake.transform.position = new Vector3(actor.position.x, seaLevel + 2.4f, actor.position.z);
            if (actor.age > 50 || Vector3.Distance(actor.position, vessel.position) > 650)
            {
                TorpedoesAvoided++;
                actor.active = false;
                actor.model.gameObject.SetActive(false);
                actor.wake.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void Hit(Vector3 contact)
        {
            LastImpactPoint = contact;
            damaged = true;
            Hits++;
            SetRunning(false);
            HitReceived?.Invoke();
        }

        void ClearTorpedoes()
        {
            foreach (var actor in torpedoes)
            {
                if (actor == null) continue;
                actor.active = false;
                actor.model.gameObject.SetActive(false);
                actor.wake.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void UpdateRadar()
        {
            int count = 0;
            float range = launchDistance + 70;
            foreach (var actor in torpedoes)
            {
                if (!actor.active) continue;
                var position = vessel.transform.InverseTransformPoint(actor.position);
                var direction = vessel.transform.InverseTransformDirection(actor.direction);
                radarPositions[count] = new Vector2(position.x, -position.z) / range;
                radarDirections[count] = new Vector2(direction.x, -direction.z).normalized;
                count++;
            }
            view.SetRadarContacts(radarPositions, radarDirections, count);
        }

        void UpdateCamera(bool snap)
        {
            // A full viewport keeps the water package's screen-space samples aligned.
            // The UI places this camera's texture in the right half of the screen.
            float resolutionScale = Mathf.Min(1, 1080f / Mathf.Max(1, Screen.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * (cinematicEnding ? 1 : .5f) * resolutionScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * resolutionScale));
            if (navigationTexture.width != width || navigationTexture.height != height || !navigationTexture.IsCreated())
            {
                navigationTexture.Release();
                navigationTexture.width = width;
                navigationTexture.height = height;
                navigationTexture.Create();
            }
            viewCamera.rect = new Rect(0, 0, 1, 1);
            viewCamera.aspect = (float)width / height;
            viewCamera.fieldOfView = cinematicEnding ? 58 : 55;
            cameraHeading = snap ? heading : Mathf.LerpAngle(cameraHeading, heading, 1 - Mathf.Exp(-Time.unscaledDeltaTime * .7f));
            var rotation = Quaternion.Euler(0, cameraHeading, 0);
            var seaPosition = new Vector3(vessel.position.x, seaLevel, vessel.position.z);
            var desired = seaPosition + rotation * (cinematicEnding ? finalCameraOffset : cameraOffset);
            viewCamera.transform.position = snap ? desired : Vector3.SmoothDamp(viewCamera.transform.position, desired, ref cameraVelocity, cinematicEnding ? .65f : 1.4f, 500, Time.unscaledDeltaTime);
            var target = cinematicEnding ? new Vector3(20, 5, 0) : new Vector3(0, 4, 24);
            viewCamera.transform.rotation = Quaternion.LookRotation(seaPosition + rotation * target - viewCamera.transform.position);
        }

        ParticleSystem CreateFoam(string objectName, float size, float lifetime, float perMeter)
        {
            var ps = new GameObject(objectName).AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.startLifetime = lifetime;
            main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size);
            main.startColor = new Color(.9f, .95f, .94f, .65f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = 1800;
            var emission = ps.emission; emission.rateOverTime = 0; emission.rateOverDistance = perMeter;
            var shape = ps.shape; shape.enabled = false;
            var sizeLife = ps.sizeOverLifetime; sizeLife.enabled = true; sizeLife.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .45f, 1, 1.6f));
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.12f),new GradientAlphaKey(0,1)});color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = foamMaterial; renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        ParticleSystem CreateSmoke()
        {
            var ps = new GameObject("Fumaca_Chamine").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.startLifetime=9;main.startSpeed=2;main.startSize=new ParticleSystem.MinMaxCurve(1.4f,2.2f);main.startColor=new Color(.15f,.15f,.14f,.26f);main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=180;
            var emission=ps.emission;emission.rateOverTime=9;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.65f;shape.angle=8;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.7f,1,3));
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});color.color=gradient;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=smokeMaterial;
            return ps;
        }

        void UpdateWakes()
        {
            SetWakePosition(sternWake, new Vector3(0, 0, -55));
            SetWakePosition(bowPort, new Vector3(-2, 0, 50));
            SetWakePosition(bowStarboard, new Vector3(2, 0, 50));
            bool emit = running && !damaged && speed > .3f;
            SetEmission(sternWake, emit);SetEmission(bowPort, emit);SetEmission(bowStarboard, emit);SetEmission(funnelSmoke, emit);
            funnelSmoke.transform.SetPositionAndRotation(vessel.transform.TransformPoint(new Vector3(0,25,-9)),Quaternion.Euler(-90,0,0));
        }

        void SetWakePosition(ParticleSystem ps, Vector3 local)
        { var position = vessel.transform.TransformPoint(local);position.y=seaLevel+2.4f;ps.transform.position=position; }
        static void SetEmission(ParticleSystem ps, bool enabled) { var emission = ps.emission; emission.enabled = enabled; }

        void OnDestroy()
        {
            if (viewCamera != null)
            {
                viewCamera.targetTexture = originalTarget;
                viewCamera.rect = originalViewport;
                viewCamera.fieldOfView = originalFov;
                viewCamera.ResetAspect();
            }
            if (navigationTexture != null) { navigationTexture.Release(); Destroy(navigationTexture); }
        }
    }
}
