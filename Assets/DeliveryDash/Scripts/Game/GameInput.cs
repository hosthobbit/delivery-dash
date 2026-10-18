using UnityEngine;
using UnityEngine.InputSystem;

namespace DeliveryDash
{
    /// <summary>
    /// All controls, built with the Input System. Two maps: "Ride" (gameplay, disabled in menus so a
    /// menu click can never throw a paper) and "UI" (pause/back). Keyboard bindings can be changed
    /// in Settings; overrides are saved in PlayerPrefs.
    /// </summary>
    public class GameInput
    {
        const string OverridesKey = "dd.bindings";

        public readonly InputActionAsset Asset;
        public readonly InputActionMap Ride, UI;
        public readonly InputAction Steer, Accelerate, Brake, Throw, Aim, AimStick, SideLeft, SideRight, Bell, Pause, Back;

        /// <summary>Actions players can rebind in Settings, with friendly names.</summary>
        public (InputAction action, int binding, string label)[] Rebindable;

        public GameInput()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Ride = Asset.AddActionMap("Ride");
            UI = Asset.AddActionMap("UI");

            Steer = Ride.AddAction("Steer", InputActionType.Value);
            Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            Steer.AddBinding("<Gamepad>/leftStick/x");

            Accelerate = Ride.AddAction("Accelerate", InputActionType.Value);
            Accelerate.AddBinding("<Keyboard>/w");
            Accelerate.AddBinding("<Keyboard>/upArrow");
            Accelerate.AddBinding("<Gamepad>/rightTrigger");

            Brake = Ride.AddAction("Brake", InputActionType.Value);
            Brake.AddBinding("<Keyboard>/s");
            Brake.AddBinding("<Keyboard>/downArrow");
            Brake.AddBinding("<Gamepad>/leftTrigger");

            Throw = Ride.AddAction("Throw", InputActionType.Button);
            Throw.AddBinding("<Mouse>/leftButton");
            Throw.AddBinding("<Keyboard>/space");
            Throw.AddBinding("<Gamepad>/buttonSouth");

            Aim = Ride.AddAction("Aim", InputActionType.PassThrough);
            Aim.AddBinding("<Pointer>/position");

            AimStick = Ride.AddAction("AimStick", InputActionType.Value);
            AimStick.AddBinding("<Gamepad>/rightStick");

            SideLeft = Ride.AddAction("SideLeft", InputActionType.Button);
            SideLeft.AddBinding("<Keyboard>/q");
            SideLeft.AddBinding("<Gamepad>/leftShoulder");

            SideRight = Ride.AddAction("SideRight", InputActionType.Button);
            SideRight.AddBinding("<Keyboard>/e");
            SideRight.AddBinding("<Gamepad>/rightShoulder");

            Bell = Ride.AddAction("Bell", InputActionType.Button);
            Bell.AddBinding("<Keyboard>/b");
            Bell.AddBinding("<Gamepad>/buttonNorth");

            Pause = UI.AddAction("Pause", InputActionType.Button);
            Pause.AddBinding("<Keyboard>/escape");
            Pause.AddBinding("<Keyboard>/p");
            Pause.AddBinding("<Gamepad>/start");

            Back = UI.AddAction("Back", InputActionType.Button);
            Back.AddBinding("<Gamepad>/buttonEast");

            Rebindable = new[]
            {
                (Accelerate, 0, "Speed up"),
                (Brake, 0, "Brake"),
                (Steer, 1, "Steer left"),     // binding 0 is the composite; 1 = negative, 2 = positive
                (Steer, 2, "Steer right"),
                (Throw, 1, "Throw (keyboard)"),
                (SideLeft, 0, "Aim left side"),
                (SideRight, 0, "Aim right side"),
                (Bell, 0, "Bell"),
            };

            string saved = PlayerPrefs.GetString(OverridesKey, "");
            if (!string.IsNullOrEmpty(saved)) { try { Asset.LoadBindingOverridesFromJson(saved); } catch { /* ignore a bad save */ } }
            UI.Enable();
        }

        public void SaveOverrides()
        {
            PlayerPrefs.SetString(OverridesKey, Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void ResetOverrides()
        {
            Asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverridesKey);
        }

        public static string Describe(InputAction a, int binding) => a.bindings[binding].ToDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);

        public bool UsingGamepad { get; private set; }

        /// <summary>Notes which device the player is using, for button prompts.</summary>
        public void TrackDevice()
        {
            var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.buttonSouth.isPressed || pad.rightTrigger.isPressed)) UsingGamepad = true;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) UsingGamepad = false;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.isPressed)) UsingGamepad = false;
        }
    }
}
