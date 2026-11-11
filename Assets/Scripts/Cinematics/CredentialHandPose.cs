using System;
using UnityEngine;

internal sealed class CredentialHandPose
{
    // Flexão e desvio lateral, em graus a partir da pose de repouso de cada falange FEB.
    static readonly Vector2[] IndexAngles = { new(42f, -11f), new(72f, -6f), new(-5f, 6f) };
    static readonly Vector2[] ThumbAngles = { new(25f, -30f), new(-1f, -14f), new(1f, -6f) };
    static readonly Vector2[] RestingAngles = { new(32f, 0f), new(54f, 0f), new(27f, 0f) };

    readonly Transform shoulder, wrist;
    readonly Quaternion palmInWrist;
    readonly Vector3 pinchInWrist;
    readonly Digit index, thumb, middleFinger, ringFinger, littleFinger;
    readonly float upperLength, forearmLength;

    public CredentialHandPose(Animator actor, bool left)
    {
        Transform Bone(HumanBodyBones l, HumanBodyBones r) => actor.GetBoneTransform(left ? l : r);
        shoulder = Bone(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm);
        var elbow = Bone(HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm);
        wrist = Bone(HumanBodyBones.LeftHand, HumanBodyBones.RightHand);
        upperLength = Vector3.Distance(shoulder.position, elbow.position);
        forearmLength = Vector3.Distance(elbow.position, wrist.position);
        var middle = Bone(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal);
        var little = Bone(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal);
        var indexRoot = Bone(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal);
        var thumbRoot = Bone(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal);
        int side = left ? -1 : 1;
        var palm = Quaternion.LookRotation(middle.position - wrist.position, indexRoot.position - little.position);
        palmInWrist = Quaternion.Inverse(wrist.rotation) * palm;
        var rest = actor.avatar.humanDescription.skeleton;
        Vector3 thumbOpposition = palmInWrist * new Vector3(-side * .021f, .026f, .145f) - thumbRoot.localPosition;
        Vector3 inward = palmInWrist * (-side * Vector3.right);
        index = new Digit(actor, left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal,
            rest, IndexAngles, inward, side);
        thumb = new Digit(actor, left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal,
            rest, ThumbAngles, thumbOpposition, side);
        middleFinger = new Digit(actor, left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal,
            rest, RestingAngles, inward, side);
        ringFinger = new Digit(actor, left ? HumanBodyBones.LeftRingProximal : HumanBodyBones.RightRingProximal,
            rest, RestingAngles, inward, side);
        littleFinger = new Digit(actor, left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal,
            rest, RestingAngles, inward, side);
        // O papel fica entre as duas polpas, e não no centro geométrico da mão.
        pinchInWrist = (index.Pad + thumb.Pad) * .5f;
    }

    public Vector3 Contact => wrist.position + wrist.rotation * pinchInWrist;

    public Pose Reach(Transform edge, Transform hint)
    {
        var wristTarget = edge.position - (edge.position - shoulder.position).normalized * pinchInWrist.magnitude;
        Quaternion wristRotation = wrist.rotation;
        // A pinça fica deslocada para o lado do polegar. Resolver novamente a posição do punho
        // permite alcançar a borda com esse deslocamento e preservar a direção do antebraço.
        for (int i = 0; i < 6; i++)
        {
            Vector3 elbow = ElbowPosition(wristTarget, hint.position);
            Vector3 forward = (wristTarget - elbow).normalized;
            Vector3 neutral = Vector3.ProjectOnPlane(shoulder.position - elbow, forward).normalized;
            Vector3 up = Vector3.ProjectOnPlane(edge.up, forward).normalized;
            Vector3 thumbAxis = Vector3.RotateTowards(neutral, up, 62f * Mathf.Deg2Rad, 0f);
            wristRotation = Quaternion.LookRotation(forward, thumbAxis) * Quaternion.Inverse(palmInWrist);
            Vector3 next = edge.position - wristRotation * pinchInWrist;
            next = shoulder.position + Vector3.ClampMagnitude(next - shoulder.position, (upperLength + forearmLength) * .95f);
            if ((next - wristTarget).sqrMagnitude < .00000025f) { wristTarget = next; break; }
            wristTarget = next;
        }
        return new Pose(wristTarget, wristRotation);
    }

    Vector3 ElbowPosition(Vector3 end, Vector3 hint)
    {
        Vector3 root = shoulder.position, along = (end - root).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(end, root), Mathf.Abs(upperLength - forearmLength) + .0001f,
            upperLength + forearmLength - .0001f);
        float projected = (upperLength * upperLength - forearmLength * forearmLength + distance * distance) / (2f * distance);
        Vector3 pole = Vector3.ProjectOnPlane(hint - root, along).normalized;
        return root + along * projected + pole * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - projected * projected));
    }

    public void Pinch(float weight)
    {
        index.Apply(weight);
        thumb.Apply(weight);
        middleFinger.Apply(weight);
        ringFinger.Apply(weight);
        littleFinger.Apply(weight);
    }

    sealed class Digit
    {
        readonly Transform[] bones = new Transform[3];
        readonly Quaternion[] pose = new Quaternion[3];
        public readonly Vector3 Pad;

        public Digit(Animator actor, HumanBodyBones first, SkeletonBone[] rest, Vector2[] angles, Vector3 closing, int side)
        {
            for (int i = 0; i < bones.Length; i++) bones[i] = actor.GetBoneTransform((HumanBodyBones)((int)first + i));
            Quaternion parentRotation = Quaternion.identity;
            Vector3 position = Vector3.zero, axis = Vector3.zero;
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                int boneIndex = Array.FindIndex(rest, entry => entry.name == bone.name);
                Quaternion bind = boneIndex >= 0 ? rest[boneIndex].rotation : bone.localRotation;
                position += parentRotation * bone.localPosition;
                Quaternion neutral = parentRotation * bind;
                // A última falange prolonga o eixo do segmento anterior no referencial deste avatar.
                axis = (i < 2 ? bones[i + 1].localPosition : Quaternion.Inverse(bind) * bone.localPosition).normalized;
                Vector3 along = neutral * axis;
                Vector3 fold = Vector3.ProjectOnPlane(closing, along).normalized;
                Vector3 lateral = Vector3.Cross(along, fold);
                float spread = angles[i].y * side * Mathf.Deg2Rad;
                Vector3 direction = Quaternion.AngleAxis(angles[i].x, lateral) * along * Mathf.Cos(spread) +
                    lateral * Mathf.Sin(spread);
                Quaternion bent = Quaternion.FromToRotation(along, direction) * neutral;
                pose[i] = Quaternion.Inverse(parentRotation) * bent;
                parentRotation = bent;
            }
            Pad = position + parentRotation * (axis * .016f);
        }

        public void Apply(float weight)
        {
            for (int i = 0; i < bones.Length; i++)
                bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, pose[i], weight);
        }
    }
}
