using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace ChipArena.UnityConversion
{
    public static class NativeControllerBuildChecks
    {
        static void Check(bool ok,string name){if(!ok)throw new Exception("Controller native gate: "+name);}
        public static void Run()
        {
            var old=JsonUtility.FromJson<ConversionPreferences>("{\"name\":\"Existing pilot\"}");
            if(old.controller==null)old.controller=new NativeControllerPreferences();old.controller.Normalize();
            Check(old.name=="Existing pilot"&&old.controller.enabled&&old.controller.sensitivity==180,"legacy preference migration");
            NativeControllerBindings.Bind(old.controller,PadAction.Attack,(int)PadButton.South);
            var loaded=JsonUtility.FromJson<ConversionPreferences>(JsonUtility.ToJson(old));loaded.controller.Normalize();
            Check(loaded.controller.bindings[0]==(int)PadButton.South&&loaded.controller.bindings[(int)PadAction.Jump]==(int)PadButton.RightTrigger,"Unity JSON preserves remap and swap");
            var previous=Gamepad.current;var device=InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(device,new GamepadState{leftStick=new Vector2(.5f,-.25f)}.WithButton(GamepadButton.South));InputSystem.Update();
                Check(device.buttonSouth.isPressed&&!device.buttonEast.isPressed,"standard positional face mapping");
                Check(Mathf.Abs(device.leftStick.ReadUnprocessedValue().x-.5f)<.001f,"unprocessed analog stick");
                InputSystem.QueueStateEvent(device,new GamepadState());InputSystem.Update();
                Check(!device.buttonSouth.isPressed,"button release");
            }
            finally{InputSystem.RemoveDevice(device);if(previous!=null&&previous.added)previous.MakeCurrent();}
            Debug.Log("PASS controller native gate: legacy prefs, Unity JSON remap, synthetic gamepad press/release and analog input. Physical hardware and menus require playtest.");
        }
    }
}
