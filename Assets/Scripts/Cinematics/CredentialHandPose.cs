using UnityEngine;
using System;

// A credencial é fina. O contato se dá nas polpas do polegar e indicador, à frente do punho.
internal sealed class CredentialHandPose
{
    readonly Transform shoulder, elbow, wrist;
    readonly Quaternion palmInWrist;
    readonly Vector3 pinchInWrist;
    readonly Transform[] index, thumb;
    readonly Quaternion[] indexRest, thumbRest;
    readonly bool leftHand;
    readonly float upperLength, forearmLength, pinchLength;

    public CredentialHandPose(Animator actor, bool left)
    {
        leftHand = left;
        Transform Bone(HumanBodyBones l, HumanBodyBones r) => actor.GetBoneTransform(left ? l : r);
        shoulder = Bone(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm);
        elbow = Bone(HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm);
        wrist = Bone(HumanBodyBones.LeftHand, HumanBodyBones.RightHand);
        upperLength = Vector3.Distance(shoulder.position, elbow.position);
        forearmLength = Vector3.Distance(elbow.position, wrist.position);
        var middle = Bone(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal);
        var little = Bone(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal);
        index = new[] { Bone(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal),
            Bone(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate),
            Bone(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal) };
        thumb = new[] { Bone(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal),
            Bone(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate),
            Bone(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal) };
        var rest = actor.avatar.humanDescription.skeleton;
        indexRest = RestPose(index, rest);
        thumbRest = RestPose(thumb, rest);
        var palm = Quaternion.LookRotation(middle.position - wrist.position, index[0].position - little.position);
        palmInWrist = Quaternion.Inverse(wrist.rotation) * palm;
        pinchLength = Vector3.Distance(wrist.position, middle.position) * 1.30f;
        pinchInWrist = palmInWrist * (Vector3.forward * pinchLength);
    }

    static Quaternion[] RestPose(Transform[] digit, SkeletonBone[] source) =>
        Array.ConvertAll(digit, bone =>
        {
            int at = Array.FindIndex(source, entry => entry.name == bone.name);
            return at >= 0 ? source[at].rotation : bone.localRotation;
        });

    public Vector3 Contact => wrist.position + wrist.rotation * pinchInWrist;

    public Pose Reach(Transform edge, Transform pole)
    {
        // Para este gesto, mão e antebraço formam uma linha. Reservar flexão evita travar o cotovelo.
        Vector3 toEdge = edge.position - shoulder.position;
        Vector3 contact = shoulder.position + Vector3.ClampMagnitude(toEdge,
            upperLength + forearmLength + pinchLength - .025f);
        Vector3 elbowPosition = Bend(shoulder.position, contact, pole.position, upperLength, forearmLength + pinchLength);
        Vector3 forward = (contact - elbowPosition).normalized;
        Vector3 thumbNeutral = Vector3.ProjectOnPlane(shoulder.position - elbowPosition, forward).normalized;
        Vector3 paperUp = Vector3.ProjectOnPlane(edge.up, forward).normalized;
        Vector3 thumbAxis = Vector3.RotateTowards(thumbNeutral, paperUp, 62f * Mathf.Deg2Rad, 0f);
        Quaternion palm = Quaternion.LookRotation(forward, thumbAxis);
        return new Pose(contact - forward * pinchLength, palm * Quaternion.Inverse(palmInWrist));
    }

    public void Pinch(float weight)
    {
        var palm = wrist.rotation * palmInWrist;
        PositionFinger(index, indexRest, palm, false, weight);
        PositionFinger(thumb, thumbRest, palm, true, weight);
    }

    void PositionFinger(Transform[] finger, Quaternion[] rest, Quaternion palm, bool isThumb, float weight)
    {
        Vector3 up = palm * Vector3.up, forward = palm * Vector3.forward;
        float side = isThumb ? 1f : -1f;
        Vector3 pad = Contact + side * .0035f * up;
        Vector3 distalDirection = (forward * .65f - up * side).normalized;
        Vector3 distal = pad - distalDirection * .016f;
        Vector3 hint = finger[0].position + forward * .07f + up * (side * .055f);
        Vector3 knuckle = Bend(finger[0].position, distal, hint,
            Vector3.Distance(finger[0].position, finger[1].position),
            Vector3.Distance(finger[1].position, finger[2].position));
        Vector3 palmNormal = palm * (leftHand ? Vector3.right : Vector3.left);
        Vector3 closing = isThumb ? (pad - finger[0].position).normalized : palmNormal;
        // Prolonga o último segmento no eixo de repouso da falange distal deste avatar.
        Vector3 tipAxis = Quaternion.Inverse(rest[2]) * finger[2].localPosition.normalized;
        Point(finger[0], rest[0], finger[1].localPosition, knuckle - finger[0].position, closing, isThumb ? 47f : 70f, isThumb ? 34f : 20f, weight);
        Point(finger[1], rest[1], finger[2].localPosition, distal - finger[1].position, closing, 82f, isThumb ? 15f : 8f, weight);
        Point(finger[2], rest[2], tipAxis, distalDirection, closing, 58f, 8f, weight);
    }

    static void Point(Transform joint, Quaternion rest, Vector3 localAxis, Vector3 target, Vector3 closing,
        float flexion, float lateral, float weight)
    {
        if (target.sqrMagnitude < 1e-7f) return;
        var neutral = joint.parent.rotation * rest;
        Vector3 natural = neutral * localAxis.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(closing, natural).normalized;
        if (bend.sqrMagnitude < .9f) return;
        Vector3 axis = Vector3.Cross(natural, bend);
        target.Normalize();
        float curl = Mathf.Clamp(Mathf.Atan2(Vector3.Dot(target, bend), Vector3.Dot(target, natural)) * Mathf.Rad2Deg, -10f, flexion);
        float spread = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(Vector3.Dot(target, axis), -1f, 1f)) * Mathf.Rad2Deg, -lateral, lateral);
        Vector3 limited = Quaternion.AngleAxis(curl, axis) * natural * Mathf.Cos(spread * Mathf.Deg2Rad) +
            axis * Mathf.Sin(spread * Mathf.Deg2Rad);
        var desired = Quaternion.FromToRotation(natural, limited) * neutral;
        joint.rotation = Quaternion.Slerp(joint.rotation, desired, weight);
    }

    static Vector3 Bend(Vector3 root, Vector3 end, Vector3 hint, float upper, float lower)
    {
        Vector3 along = (end - root).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(end, root), Mathf.Abs(upper - lower) + .0001f, upper + lower - .0001f);
        float length = (upper * upper - lower * lower + distance * distance) / (2f * distance);
        Vector3 pole = Vector3.ProjectOnPlane(hint - root, along).normalized;
        return root + along * length + pole * Mathf.Sqrt(Mathf.Max(0f, upper * upper - length * length));
    }
}
