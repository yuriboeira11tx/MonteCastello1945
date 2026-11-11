using UnityEngine;
using UnityEngine.Animations.Rigging;

// A temporary final rig layer on the two existing camp soldiers.
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class CampArrivalActorRig : MonoBehaviour
{
    private const float DriverSeatX = -0.355f;

    private Animator actor;
    private RigBuilder builder;
    private RigLayer layer;
    private Transform controls;
    private MultiAimConstraint headAim;
    private Transform gaze;
    private Transform lookTarget;
    private bool addedBuilder;
    private bool builderWasEnabled;
    private bool seated;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private AnimatorCullingMode originalCulling;
    private HandReach leftHand;
    private HandReach rightHand;

    public Transform Eyes { get; private set; }

    public static CampArrivalActorRig Begin(Animator actor, Transform jeep = null) =>
        Begin(actor, jeep, null);

    public static CampArrivalActorRig BeginPassenger(Animator actor, Transform jeep, Vector3 hipLocal) =>
        Begin(actor, jeep, hipLocal);

    private static CampArrivalActorRig Begin(Animator actor, Transform jeep, Vector3? passengerHip)
    {
        if (actor == null || !actor.isHuman || actor.avatar == null || !actor.avatar.isValid)
            return null;

        var rig = actor.gameObject.AddComponent<CampArrivalActorRig>();
        rig.actor = actor;
        rig.originalCulling = actor.cullingMode;
        actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        rig.originalPosition = actor.transform.localPosition;
        rig.originalRotation = actor.transform.localRotation;
        rig.Eyes = actor.GetBoneTransform(HumanBodyBones.Head);
        rig.controls = Child(actor.transform, "Rig da chegada");
        var rigLayer = rig.controls.gameObject.AddComponent<Rig>();
        if (passengerHip.HasValue)
            rig.SeatPassenger(jeep, passengerHip.Value);
        else if (jeep != null)
            rig.SeatDriver(jeep);
        else
        {
            rig.PrepareStandingHand(true);
            rig.PrepareStandingHand(false);
        }
        rig.CreateHeadAim();

        rig.builder = actor.GetComponent<RigBuilder>();
        rig.addedBuilder = rig.builder == null;
        if (rig.addedBuilder)
            rig.builder = actor.gameObject.AddComponent<RigBuilder>();
        rig.builderWasEnabled = rig.builder.enabled;
        rig.layer = new RigLayer(rigLayer);
        rig.builder.layers.Add(rig.layer);
        if (rig.builder.enabled)
            rig.builder.Build();
        else
            rig.builder.enabled = true;
        return rig;
    }

    public void SetLookTarget(Transform target) => lookTarget = target;
    public void ClearLookTarget() => lookTarget = null;
    public void SetHandTarget(bool left, Transform target) => (left ? leftHand : rightHand).target = target;

    private void CreateHeadAim()
    {
        gaze = Child(controls, "Alvo do olhar");
        gaze.position = Eyes.position + actor.transform.forward * 2f;
        headAim = Child(controls, "Olhar").gameObject.AddComponent<MultiAimConstraint>();
        headAim.Reset();
        headAim.data.constrainedObject = Eyes;
        headAim.data.aimAxis = MultiAimConstraintData.Axis.Z;
        headAim.data.upAxis = MultiAimConstraintData.Axis.Y;
        headAim.data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
        headAim.data.maintainOffset = true;
        headAim.data.limits = new Vector2(0f, 74f);
        headAim.data.sourceObjects = new WeightedTransformArray { new WeightedTransform(gaze, 1f) };
        headAim.weight = 0f;
    }

    private void Update()
    {
        if (headAim == null)
            return;
        if (lookTarget != null)
        {
            Vector3 desired = (lookTarget.position + Vector3.up * .065f - Eyes.position).normalized;
            Vector3 current = (gaze.position - Eyes.position).normalized;
            gaze.position = Eyes.position + Vector3.RotateTowards(current, desired,
                2.4f * Time.deltaTime, 0f) * 1.5f;
        }
        headAim.weight = Mathf.MoveTowards(headAim.weight, lookTarget != null ? 0.95f : 0f,
            2.5f * Time.deltaTime);
        leftHand.Update(Time.deltaTime);
        rightHand.Update(Time.deltaTime);
    }

    private void SeatDriver(Transform jeep)
    {
        seated = true;
        // Coordinates measured from the cushion, steering rim and footwell of Jeep_Willys_MB.
        actor.transform.SetPositionAndRotation(jeep.TransformPoint(new Vector3(DriverSeatX, -0.105f, -0.46f)), jeep.rotation);
        Transform pelvis = Marker(jeep, controls, "Assento", DriverSeatX, 0.835f, -0.46f);
        PinHips(pelvis);

        PlaceDriverSide(jeep, true);
        PlaceDriverSide(jeep, false);
    }

    private void PinHips(Transform pelvis)
    {
        var seat = Child(controls, "Quadril no banco").gameObject.AddComponent<MultiPositionConstraint>();
        seat.Reset();
        seat.data.constrainedObject = actor.GetBoneTransform(HumanBodyBones.Hips);
        seat.data.sourceObjects = new WeightedTransformArray { new WeightedTransform(pelvis, 1f) };
    }

    private void SeatPassenger(Transform jeep, Vector3 hip)
    {
        seated = true;
        actor.transform.rotation = jeep.rotation;
        Transform pelvis = actor.GetBoneTransform(HumanBodyBones.Hips);
        actor.transform.position += jeep.TransformPoint(hip) - pelvis.position;
        PinHips(Marker(jeep, controls, "Assento", hip.x, hip.y, hip.z));
        PlacePassengerSide(jeep, hip, true);
        PlacePassengerSide(jeep, hip, false);
    }

    private void PlacePassengerSide(Transform jeep, Vector3 hip, bool left)
    {
        float side = left ? -1f : 1f;
        string suffix = left ? " E" : " D";
        PrimeElbow(left, suffix);
        var arm = Arm(left);
        arm.data.target = Marker(jeep, arm.transform, "Mão no colo" + suffix,
            hip.x + side * .145f, hip.y + .13f, hip.z + .20f);
        arm.data.hint = Marker(jeep, arm.transform, "Cotovelo" + suffix,
            hip.x + side * .30f, hip.y + .20f, hip.z - .015f);
        arm.data.targetRotationWeight = 0f;
        RegisterHand(left, arm);

        var leg = Limb("Perna" + suffix,
            left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
            left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
            left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        // O piso é mais alto em frente ao banco traseiro do Willys.
        float ankleHeight = hip.z < -.9f ? .71f : .60f;
        var ankle = Marker(jeep, leg.transform, "Pé no piso" + suffix,
            hip.x + side * .14f, ankleHeight, hip.z + .34f);
        leg.data.target = ankle;
        leg.data.hint = Marker(jeep, leg.transform, "Joelho" + suffix,
            hip.x + side * .18f, hip.y + .025f, hip.z + .42f);
        Transform toes = actor.GetBoneTransform(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
        if (toes != null)
            ankle.rotation = Quaternion.FromToRotation(toes.position - leg.data.tip.position,
                jeep.TransformDirection(new Vector3(0f, -.24f, 1f))) * leg.data.tip.rotation;
        else
            leg.data.targetRotationWeight = 0f;
    }

    private void PlaceDriverSide(Transform jeep, bool left)
    {
        float side = left ? -1f : 1f;
        string suffix = left ? " E" : " D";
        PrimeElbow(left, suffix);
        var arm = Arm(left);
        Transform wrist = Marker(jeep, arm.transform, "Volante" + suffix, DriverSeatX + side * .192f, 1.115f, -.096f);
        arm.data.target = wrist;
        arm.data.hint = Marker(jeep, arm.transform, "Cotovelo" + suffix, DriverSeatX + side * .285f, .965f, -.35f);

        // Orient the palm from the character's actual knuckles; humanoid wrist axes differ between avatars.
        Transform middle = actor.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        Transform index = actor.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        Transform little = actor.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
        if (middle != null && index != null && little != null)
        {
            Quaternion palm = Quaternion.LookRotation(middle.position - arm.data.tip.position, index.position - little.position);
            Quaternion grip = Quaternion.LookRotation(jeep.TransformDirection(new Vector3(-side * .03f, .03f, 1)), jeep.up);
            wrist.rotation = grip * Quaternion.Inverse(palm) * arm.data.tip.rotation;
        }
        else
            arm.data.targetRotationWeight = 0f;
        RegisterHand(left, arm);

        var leg = Limb("Perna" + suffix,
            left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
            left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
            left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        Transform ankle = Marker(jeep, leg.transform, "Pedal" + suffix, DriverSeatX + side * 0.135f, 0.605f, left ? 0.14f : 0.19f);
        leg.data.target = ankle;
        leg.data.hint = Marker(jeep, leg.transform, "Joelho" + suffix, DriverSeatX + side * 0.185f, 0.87f, 0.08f);
        Transform toes = actor.GetBoneTransform(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
        if (toes != null)
        {
            Transform foot = leg.data.tip;
            Vector3 desired = jeep.TransformDirection(new Vector3(0f, -0.24f, 1f));
            ankle.rotation = Quaternion.FromToRotation(toes.position - foot.position, desired) * foot.rotation;
        }
        else
            leg.data.targetRotationWeight = 0f;
    }

    private void PrimeElbow(bool left, string suffix)
    {
        Transform shoulder = actor.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        Transform elbow = actor.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Transform wrist = actor.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        // The idle clip straightens the left elbow past neutral. Supply a stable bend plane before Two Bone IK.
        Vector3 upper = (elbow.position - shoulder.position).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(actor.transform.forward, upper).normalized;
        Vector3 bent = Vector3.RotateTowards(upper, forward, 52f * Mathf.Deg2Rad, 0f);
        Quaternion bend = Quaternion.FromToRotation(wrist.position - elbow.position, bent) * elbow.rotation;
        var rest = Child(controls, "Flexão do cotovelo" + suffix).gameObject.AddComponent<OverrideTransform>();
        rest.Reset();
        rest.data.constrainedObject = elbow;
        rest.data.space = OverrideTransformData.Space.Local;
        rest.data.rotation = (Quaternion.Inverse(elbow.parent.rotation) * bend).eulerAngles;
        rest.data.positionWeight = 0;
    }

    private TwoBoneIKConstraint Arm(bool left) => Limb(left ? "Braço E" : "Braço D",
        left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
        left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
        left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);

    private void PrepareStandingHand(bool left)
    {
        var arm = Arm(left);
        Transform marker = Child(arm.transform, "Entrega da credencial");
        marker.position = arm.data.tip.position;
        arm.data.target = marker;
        arm.data.hint = Marker(actor.transform, arm.transform, "Cotovelo", left ? -0.43f : 0.43f, 1.02f, 0.02f);
        arm.data.targetRotationWeight = 0f;
        arm.weight = 0f;
        RegisterHand(left, arm);
    }

    private void RegisterHand(bool left, TwoBoneIKConstraint arm)
    {
        var reach = new HandReach(arm, seated);
        if (left) leftHand = reach;
        else rightHand = reach;
    }

    // The driver returns to the steering wheel; the standing actor returns to his animated pose.
    private sealed class HandReach
    {
        public Transform target;
        private readonly TwoBoneIKConstraint arm;
        private readonly bool onWheel;
        private readonly Vector3 restPosition;
        private readonly float restRotationWeight;
        private Vector3 reachPosition;
        private float blend;

        public HandReach(TwoBoneIKConstraint arm, bool onWheel)
        {
            this.arm = arm;
            this.onWheel = onWheel;
            restPosition = arm.data.target.localPosition;
            restRotationWeight = arm.data.targetRotationWeight;
        }

        public void Update(float deltaTime)
        {
            if (target == null && blend <= 0f)
                return;
            Transform marker = arm.data.target;
            if (target != null)
                reachPosition = marker.parent.InverseTransformPoint(target.position);
            blend = Mathf.MoveTowards(blend, target != null ? 1f : 0f, deltaTime * 2.2f);
            float eased = Mathf.SmoothStep(0f, 1f, blend);
            marker.localPosition = onWheel ? Vector3.Lerp(restPosition, reachPosition, eased) : reachPosition;
            arm.data.targetRotationWeight = restRotationWeight * (1f - eased);
            arm.weight = onWheel ? 1f : eased;
        }
    }

    private TwoBoneIKConstraint Limb(string limbName, HumanBodyBones root, HumanBodyBones middle, HumanBodyBones tip)
    {
        var constraint = Child(controls, limbName).gameObject.AddComponent<TwoBoneIKConstraint>();
        constraint.Reset();
        constraint.data.root = actor.GetBoneTransform(root);
        constraint.data.mid = actor.GetBoneTransform(middle);
        constraint.data.tip = actor.GetBoneTransform(tip);
        return constraint;
    }

    private static Transform Child(Transform parent, string objectName)
    {
        Transform child = new GameObject(objectName).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Transform Marker(Transform jeep, Transform parent, string markerName, float x, float y, float z)
    {
        Transform marker = Child(parent, markerName);
        marker.position = jeep.TransformPoint(new Vector3(x, y, z));
        return marker;
    }

    public void Dispose()
    {
        if (actor == null)
            return;
        enabled = false;
        builder.Clear();
        builder.layers.Remove(layer);
        if (addedBuilder)
        {
            builder.enabled = false;
            Destroy(builder);
        }
        else if (builderWasEnabled)
            builder.Build();
        else
            builder.enabled = false;

        Destroy(controls.gameObject);
        if (seated)
            actor.transform.SetLocalPositionAndRotation(originalPosition, originalRotation);
        actor.cullingMode = originalCulling;
        actor = null;
        Destroy(this);
    }
}
