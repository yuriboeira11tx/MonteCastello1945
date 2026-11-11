using System;
using UnityEngine;

// As alças de aço da caixa pequena ficam nas laterais, a 40% da altura do caixote.
internal sealed class CampCrateGrip
{
    readonly Hand left, right;
    readonly Transform actorRoot;

    public CampCrateGrip(Animator actor)
    {
        SkeletonBone[] bindPose = actor.avatar.humanDescription.skeleton;
        actorRoot = actor.transform;
        left = new Hand(actor, true, bindPose);
        right = new Hand(actor, false, bindPose);
    }

    public void PositionTargets(BoxCollider box, Transform leftTarget, Transform rightTarget)
    {
        left.PositionTarget(box, leftTarget);
        right.PositionTarget(box, rightTarget);
    }

    public void CloseFingers(float weight)
    {
        if (weight <= 0f) return;
        left.CloseFingers(weight);
        right.CloseFingers(weight);
    }

    public float ContactWeight(Transform leftTarget, Transform rightTarget)
    {
        // Na transferência, a carga pode vir atravessada ou estar abaixo do alcance do braço.
        Vector3 across = (rightTarget.position - leftTarget.position).normalized;
        float facing = Mathf.InverseLerp(.18f, .8f, Vector3.Dot(across, actorRoot.right));
        float reach = Mathf.Min(left.ReachWeight(leftTarget), right.ReachWeight(rightTarget));
        return Mathf.SmoothStep(0f, 1f, Mathf.Min(facing, reach));
    }

    sealed class Hand
    {
        readonly float side;
        readonly Quaternion palmInWrist;
        readonly Vector3 middleInWrist;
        readonly Transform shoulder;
        readonly float armLength;
        readonly Transform[] fingers = new Transform[15];
        readonly Quaternion[] neutralRotations = new Quaternion[15];
        readonly Quaternion[] gripRotations = new Quaternion[15];

        public Hand(Animator actor, bool isLeft, SkeletonBone[] bindPose)
        {
            side = isLeft ? -1f : 1f;
            Transform wrist = actor.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            shoulder = actor.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var elbow = actor.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            armLength = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, wrist.position);
            var first = isLeft ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
            // HumanBodyBones agrupa três falanges consecutivas por dedo, do polegar ao mínimo.
            for (int i = 0; i < fingers.Length; i++)
            {
                fingers[i] = actor.GetBoneTransform((HumanBodyBones)((int)first + i));
                if (!fingers[i]) continue;
                var name = fingers[i].name;
                int rest = Array.FindIndex(bindPose, item => item.name == name);
                neutralRotations[i] = rest < 0 ? fingers[i].localRotation : bindPose[rest].rotation;
            }

            if (!fingers[3] || !fingers[6] || !fingers[12]) return;
            middleInWrist = wrist.InverseTransformPoint(fingers[6].position);
            Vector3 across = wrist.InverseTransformVector(fingers[3].position - fingers[12].position);
            palmInWrist = Quaternion.LookRotation(middleInWrist, across);
        }

        public float ReachWeight(Transform target) =>
            Mathf.InverseLerp(armLength * 1.16f, armLength * .95f, Vector3.Distance(shoulder.position, target.position));

        public void PositionTarget(BoxCollider box, Transform target)
        {
            Vector3 handle = box.center + new Vector3(side * box.size.x * .5f, -.10f * box.size.y, 0f);
            if (!fingers[3] || !fingers[6] || !fingers[12])
            {
                target.position = box.transform.TransformPoint(handle);
                return;
            }
            // Os nós dos dedos ficam sob o puxador. A palma olha para dentro e um pouco para cima.
            Vector3 forward = new Vector3(-side * .055f, -.29f, 1f);
            Vector3 thumb = new Vector3(side * .42f, 1f, 0f);
            Quaternion palm = Quaternion.LookRotation(forward, thumb);
            Quaternion wrist = palm * Quaternion.Inverse(palmInWrist);
            Vector3 knuckles = handle + new Vector3(side * .014f, -.023f, -.022f);
            Vector3 wristPosition = knuckles - wrist * middleInWrist;
            target.SetPositionAndRotation(box.transform.TransformPoint(wristPosition),
                box.transform.rotation * wrist);
            ShapeFingers(wristPosition, wrist, handle);
        }

        void ShapeFingers(Vector3 wristPosition, Quaternion wrist, Vector3 handle)
        {
            // Cada dedo passa sob a barra e fecha por dentro. O polegar faz oposição por cima.
            for (int digit = 0; digit < 5; digit++)
            {
                Quaternion parentRotation = wrist;
                Vector3 parentPosition = wristPosition;
                for (int joint = 0; joint < 3; joint++)
                {
                    int i = digit * 3 + joint;
                    if (!fingers[i]) break;
                    Vector3 bonePosition = parentPosition + parentRotation * fingers[i].localPosition;
                    Quaternion neutral = parentRotation * neutralRotations[i];
                    Vector3 along = joint < 2 && fingers[i + 1] ? fingers[i + 1].localPosition.normalized : Vector3.up;
                    Vector3 contact = handle + ContactOffset(digit, joint);
                    var turn = Quaternion.FromToRotation(neutral * along, contact - bonePosition);
                    var rotation = Quaternion.RotateTowards(neutral, turn * neutral, joint == 0 ? 70f : 95f);
                    gripRotations[i] = Quaternion.Inverse(parentRotation) * rotation;
                    parentRotation = rotation;
                    parentPosition = bonePosition;
                }
            }
        }

        Vector3 ContactOffset(int digit, int joint)
        {
            if (digit == 0)
            {
                if (joint == 0) return new Vector3(side * .012f, .025f, -.033f);
                if (joint == 1) return new Vector3(-side * .009f, .010f, -.017f);
                return new Vector3(-side * .015f, -.005f, -.004f);
            }
            float z = .038f - (digit - 1) * .022f;
            if (joint == 0) return new Vector3(-side * .003f, -.012f, z);
            if (joint == 1) return new Vector3(-side * .018f, .005f, z + .005f);
            return new Vector3(-side * .008f, .021f, z + .008f);
        }

        public void CloseFingers(float weight)
        {
            if (!fingers[3] || !fingers[6] || !fingers[12]) return;
            for (int i = 0; i < fingers.Length; i++)
                if (fingers[i]) fingers[i].localRotation = Quaternion.Slerp(fingers[i].localRotation, gripRotations[i], weight);
        }
    }
}
