using UnityEngine;
using UnityEngine.InputSystem;

namespace MonteCastello.FEB
{
    public sealed class FEBCharacterShowcase : MonoBehaviour
    {
        public FEBCharacterMotor[] soldiers;
        public Camera showcaseCamera;
        public int selectedIndex;
        public int movementMode;
        public bool controlling;
        Vector3[] starts;
        Quaternion[] rotations;
        Vector3 overviewPosition;
        Quaternion overviewRotation;
        GUIStyle title, label, button, selectedButton;

        void Awake()
        {
            Application.runInBackground = true;
            starts = new Vector3[soldiers.Length];
            rotations = new Quaternion[soldiers.Length];
            for (int i = 0; i < soldiers.Length; i++)
            {
                starts[i] = soldiers[i].transform.position;
                rotations[i] = soldiers[i].transform.rotation;
                soldiers[i].movementCamera = showcaseCamera;
            }
            overviewPosition = showcaseCamera.transform.position;
            overviewRotation = showcaseCamera.transform.rotation;
            SetMovementMode(0);
        }

        public void SelectSoldier(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, soldiers.Length - 1);
            ApplyControl();
        }

        public void SetMovementMode(int mode)
        {
            movementMode = Mathf.Clamp(mode, 0, 2);
            foreach (var soldier in soldiers)
                soldier.previewSpeed = movementMode == 0 ? 0 : movementMode == 1 ? soldier.walkSpeed : soldier.runSpeed;
        }

        public void SetControl(bool enabled)
        {
            controlling = enabled;
            ApplyControl();
        }

        void ApplyControl()
        {
            for (int i = 0; i < soldiers.Length; i++)
            {
                soldiers[i].playerControlled = controlling && i == selectedIndex;
                soldiers[i].ClearSimulatedInput();
            }
        }

        public void ResetAll()
        {
            for (int i = 0; i < soldiers.Length; i++) soldiers[i].ResetPosition(starts[i], rotations[i]);
            SetControl(false);
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.vKey.wasPressedThisFrame) SetControl(!controlling);
            if (k.rKey.wasPressedThisFrame) ResetAll();
            if (k.digit1Key.wasPressedThisFrame) SelectSoldier(0);
            if (k.digit2Key.wasPressedThisFrame) SelectSoldier(1);
            if (k.digit3Key.wasPressedThisFrame) SelectSoldier(2);
            if (k.digit4Key.wasPressedThisFrame) SelectSoldier(3);
            if (k.digit5Key.wasPressedThisFrame) SelectSoldier(4);
            if (k.digit6Key.wasPressedThisFrame) SelectSoldier(5);
        }

        void LateUpdate()
        {
            Vector3 position = overviewPosition;
            Quaternion rotation = overviewRotation;
            if (controlling)
            {
                Vector3 target = soldiers[selectedIndex].transform.position + Vector3.up * 1.1f;
                position = target + new Vector3(0, 1.7f, -4.5f);
                rotation = Quaternion.LookRotation(target - position);
            }
            float t = 1 - Mathf.Exp(-8 * Time.deltaTime);
            showcaseCamera.transform.SetPositionAndRotation(Vector3.Lerp(showcaseCamera.transform.position, position, t), Quaternion.Slerp(showcaseCamera.transform.rotation, rotation, t));
        }

        void OnGUI()
        {
            if (soldiers == null || soldiers.Length == 0) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
                label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 30 };
                selectedButton = new GUIStyle(button); selectedButton.normal.textColor = new Color(.95f, .85f, .45f);
            }
            GUI.Box(new Rect(16, 16, 510, 205), GUIContent.none);
            GUILayout.BeginArea(new Rect(30, 24, 482, 190));
            GUILayout.Label("SOLDADOS DA FEB", title);
            GUILayout.Label(controlling ? $"Controlando soldado {selectedIndex + 1:00}" : "Escolha um soldado e experimente os movimentos.", label);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < soldiers.Length; i++)
                if (GUILayout.Button($"{i + 1:00}", i == selectedIndex ? selectedButton : button)) SelectSoldier(i);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            string[] modes = { "Parado", "Caminhar", "Correr" };
            for (int i = 0; i < modes.Length; i++)
                if (GUILayout.Button(modes[i], movementMode == i ? selectedButton : button)) SetMovementMode(i);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(controlling ? "Visão geral" : "Controlar selecionado", button)) SetControl(!controlling);
            if (GUILayout.Button("Reposicionar", button)) ResetAll();
            GUILayout.EndHorizontal();
            GUILayout.Label("WASD / setas: mover · Shift: correr · 1–6: escolher · V: visão · R: reposicionar", label);
            GUILayout.EndArea();
        }
    }
}
