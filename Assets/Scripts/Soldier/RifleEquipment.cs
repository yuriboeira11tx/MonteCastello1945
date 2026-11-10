using UnityEngine;

[DisallowMultipleComponent]
public sealed class RifleEquipment : MonoBehaviour
{
    private void OnEnable()
    {
        EquipHelmet();
    }

    private void OnTransformParentChanged()
    {
        if (isActiveAndEnabled) EquipHelmet();
    }

    private void EquipHelmet()
    {
        Animator wearer = GetComponentInParent<Animator>();
        if (wearer == null || wearer.avatar == null || !wearer.avatar.isValid || !wearer.avatar.isHuman) return;

        Transform head = wearer.GetBoneTransform(HumanBodyBones.Head);
        Transform helmetSocket = head != null ? head.Find("EncaixeCapacete") : null;
        Transform helmet = helmetSocket != null ? helmetSocket.Find("Capacete_M1_FEB") : null;
        if (helmet == null) return;

        if (!helmetSocket.gameObject.activeSelf) helmetSocket.gameObject.SetActive(true);
        if (!helmet.gameObject.activeSelf) helmet.gameObject.SetActive(true);
    }
}
