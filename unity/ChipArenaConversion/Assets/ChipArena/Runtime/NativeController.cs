using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private Gamepad pad;
        private readonly NativeControllerState padState=new NativeControllerState();
        private bool[] padHeld=>padState.Held;private bool[] padDown=>padState.Down;private bool[] padUp=>padState.Up;
        private Vector2 padMoveAxis,padLookAxis,padPointer;
        private bool padInputCaptured;
        private bool padNeutral=true,padUi,padPointerActive,padBack,padCaptureArmed;
        private int padCapture=-1,padContext=-1;
        private float padCaptureUntil,padScroll;
        private string padMessage="";
        private NativeControllerPreferences ControllerPrefs { get { if(prefs.controller==null)prefs.controller=new NativeControllerPreferences();if(prefs.controller.bindings==null)prefs.controller.Normalize();return prefs.controller; } }
        private bool ControllerUi=>arena==null||menu||inspectSeat!=0||BoardOpen||ExpeditionScreenOpen||PvePickOpen||ExpeditionPickPointer||arena!=null&&(match.Phase==NativePhase.Draft||match.Phase==NativePhase.Match);
        private bool PadHeld(PadAction a)=>padHeld[(int)a];
        private bool PadDown(PadAction a)=>padDown[(int)a];
        private bool PadUp(PadAction a)=>padUp[(int)a];
        private bool PadBack=>padBack;
        private string PadLabels=>ControllerPrefs.labels=="Auto"?(pad is DualShockGamepad?"PlayStation":"Xbox"):ControllerPrefs.labels;
        private ButtonControl PadControl(PadButton b)
        {
            switch(b)
            {
                case PadButton.South:return pad.buttonSouth;case PadButton.East:return pad.buttonEast;
                case PadButton.West:return pad.buttonWest;case PadButton.North:return pad.buttonNorth;
                case PadButton.LeftShoulder:return pad.leftShoulder;case PadButton.RightShoulder:return pad.rightShoulder;
                case PadButton.LeftTrigger:return pad.leftTrigger;case PadButton.RightTrigger:return pad.rightTrigger;
                case PadButton.LeftStick:return pad.leftStickButton;case PadButton.RightStick:return pad.rightStickButton;
                case PadButton.Up:return pad.dpad.up;case PadButton.Down:return pad.dpad.down;
                case PadButton.Left:return pad.dpad.left;case PadButton.Right:return pad.dpad.right;
                default:return pad.startButton;
            }
        }
        private bool PadAny(){if(pad.selectButton.isPressed)return true;for(int i=0;i<(int)PadButton.Count;i++)if(PadControl((PadButton)i).isPressed)return true;return false;}
        private Vector2 PadAxis(Vector2 v)=>v*NativeControllerBindings.AxisScale(v.magnitude,ControllerPrefs.deadzone);
        private void ControllerStep()
        {
            NativeControllerPointer.Active=false;padInputCaptured=padCapture>=0;padBack=false;padMoveAxis=padLookAxis=Vector2.zero;padScroll=0;
            for(int i=0;i<padHeld.Length;i++){padUp[i]=false;padDown[i]=false;}
            if(prefs==null||dedicated)return;
            var options=ControllerPrefs;var next=Gamepad.current;
            int context=(int)MenuTarget+32*(int)pauseScreen+(menu?1024:0)+(BoardOpen?2048:0)+(ExpeditionScreenOpen?4096:0)+(PvePickOpen?8192:0)+(ExpeditionPickPointer?16384:0)+(inspectSeat!=0?32768:0)+(updateOpen?65536:0)+(arena==null?0:((int)match.Phase+1)*131072);
            bool ui=ControllerUi,blocked=!Application.isFocused||StageInputHeld||SplitLocalLoading||!options.enabled||next==null||!next.added||!next.enabled;
            if(next!=pad||ui!=padUi||context!=padContext||blocked){padNeutral=true;NativeControllerPointer.ResetNavigation();}
            padContext=context;
            if(padNeutral)padState.Step(options.bindings,0,false,true);
            pad=next;padUi=ui;
            if(pad==null||!pad.added||blocked){padPointerActive=false;padCapture=-1;return;}
            if(padNeutral){if(PadAny())return;padNeutral=false;}
            if(padCapture>=0)
            {
                padState.Step(options.bindings,0,false,true);
                if(Input.GetKeyDown(KeyCode.Escape)||Time.unscaledTime>=padCaptureUntil){padCapture=-1;padMessage="Binding cancelled.";padNeutral=true;return;}
                if(!padCaptureArmed){if(!PadAny())padCaptureArmed=true;return;}
                for(int i=0;i<(int)PadButton.Count;i++)if(PadControl((PadButton)i).wasPressedThisFrame)
                {
                    int other=NativeControllerBindings.Bind(options,(PadAction)padCapture,NativeControllerBindings.Code((PadButton)i,pad.selectButton.isPressed));
                    padMessage=other<0?"Binding changed. Save layout to keep it.":"Swapped with "+NativeControllerBindings.Actions[other]+". Save layout to keep it.";
                    padCapture=-1;padNeutral=true;return;
                }
                return;
            }
            bool modifier=pad.selectButton.isPressed;
            uint buttons=0;for(int i=0;i<(int)PadButton.Count;i++)if(PadControl((PadButton)i).isPressed)buttons|=1u<<i;
            // Fixed menu controls do not also fire remapped gameplay actions.
            padState.Step(options.bindings,buttons,modifier,ui);
            padBack=ui?(pad.startButton.wasPressedThisFrame||pad.buttonEast.wasPressedThisFrame):PadDown(PadAction.Pause);
            // Read unprocessed stick values: apply precisely one configurable radial deadzone.
            var left=PadAxis(pad.leftStick.ReadUnprocessedValue());var right=PadAxis(pad.rightStick.ReadUnprocessedValue());
            if(!ui){padMoveAxis=options.swapSticks?right:left;padLookAxis=options.swapSticks?left:right;padPointerActive=false;return;}
            if(Input.GetAxisRaw("Mouse X")!=0||Input.GetAxisRaw("Mouse Y")!=0||Input.GetMouseButtonDown(0))
            {padPointerActive=false;NativeControllerPointer.Navigating=false;}
            var direction=pad.dpad.ReadValue();if(direction.sqrMagnitude<.1f)direction=left;
            bool confirm=pad.buttonSouth.wasPressedThisFrame;
            var focus=NativeControllerPointer.Navigate(direction.x,-direction.y,confirm,Time.unscaledTime,out float scroll);
            if(NativeControllerPointer.Navigating)
            {
                padPointerActive=true;padPointer=focus;
                if(menu&&scroll!=0)screenScroll.y=Mathf.Clamp(screenScroll.y+scroll/Mathf.Max(.1f,NativeUi.S),0,Mathf.Max(0,screenContent));
            }
            if(padPointerActive)
            {
                NativeControllerPointer.Active=true;
                NativeControllerPointer.Update(padPointer,pad.buttonSouth.wasPressedThisFrame,pad.buttonSouth.wasReleasedThisFrame);
                padScroll=-right.y*Time.unscaledDeltaTime*900;
                if(menu)screenScroll.y=Mathf.Clamp(screenScroll.y+padScroll,0,Mathf.Max(0,screenContent-200));
            }
        }

        private void ControllerLook(float scale)
        {
            float delta=ControllerPrefs.sensitivity*Mathf.Min(Time.unscaledDeltaTime,.05f)*scale;
            yaw+=padLookAxis.x*delta;pitch=Mathf.Clamp(pitch+padLookAxis.y*delta*(ControllerPrefs.invertY?1:-1),-70,70);
        }
        private void ControllerGuiEvent()
        {
            // Capture the window origin before any canvas matrix or scroll group is applied.
            // Never replace Event.current: real mouse events must retain their Used state.
            NativeControllerPointer.BeginGui();
            if(!padPointerActive||!ControllerUi||padCapture>=0||Event.current.type!=EventType.Repaint)return;
            Event.current.mousePosition=padPointer;
        }

        private void ControllerCursor()
        {
            if(Event.current.type!=EventType.Repaint)return;if(!padPointerActive||!ControllerUi){NativeMenuFocusFx.Hide();return;}
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;if(NativeControllerPointer.Navigating&&NativeControllerPointer.Navigation.Current(out var item))
            {
                // The focus glow, with the hover tick on each move (Claude's presentation, NativeMenuFocusFx).
                if(!item.Clipped||item.Y>=item.ClipY&&item.Y+item.H<=item.ClipY+item.ClipH)
                    NativeMenuFocusFx.Draw(new Rect(item.X,item.Y,item.W,item.H),NativeControllerPointer.Navigation.Focus,Time.unscaledTime,prefs.reducedEffects,NativeUi.S);
            }else NativeMenuFocusFx.Hide();GUI.matrix=matrix;
        }
        private void ControllerSettings()
        {
            var p=ControllerPrefs;Field("Controller");p.enabled=NativeControllerPointer.Toggle(p.enabled,"Enable controller");
            Fine(pad==null?"No gamepad detected. Connect a controller to play or remap.":"Connected: "+pad.displayName);
            p.labels=ChoiceGrid(new[]{"Auto","Xbox","PlayStation","Steam"},p.labels);
            Fine("Steam Controller / Steam Deck: select a Gamepad template in Steam Input. Auto uses Xbox names for emulated gamepads.");
            p.sensitivity=Slider("Controller look speed",p.sensitivity,45,360);p.deadzone=Slider("Stick deadzone",p.deadzone,.05f,.45f);
            p.invertY=NativeControllerPointer.Toggle(p.invertY,"Invert controller look Y");p.swapSticks=NativeControllerPointer.Toggle(p.swapSticks,"Swap movement and look sticks");
            Fine("Menus: D-pad or left stick selects options; A / Cross confirms, B / Circle goes back. Left/right adjusts sliders; right stick scrolls. These menu controls stay fixed. View / Create / Back is the chord modifier.");
            for(int i=0;i<(int)PadAction.Count;i++)
            {
                GUILayout.BeginHorizontal();GUILayout.Label(NativeControllerBindings.Actions[i],small,GUILayout.Width(210));
                bool was=GUI.enabled;GUI.enabled=was&&pad!=null&&p.enabled&&padCapture<0;
                if(NativeControllerPointer.Button(NativeControllerBindings.Label(p.bindings[i],PadLabels),button)){padCapture=i;padCaptureArmed=false;padCaptureUntil=Time.unscaledTime+10;NativeControllerPointer.Cancel();padMessage="Release buttons, then press a button (optionally hold View / Create / Back). Escape or 10 seconds cancels.";}
                GUI.enabled=was;GUILayout.EndHorizontal();
            }
            if(padCapture>=0){Fine("Remapping: "+NativeControllerBindings.Actions[padCapture]);if(NativeControllerPointer.Button("Cancel binding",button)){padCapture=-1;padNeutral=true;}}
            if(padMessage.Length>0)Fine(padMessage);
            GUILayout.BeginHorizontal();
            if(NativeControllerPointer.Button("Save layout",button)){p.Normalize();padMessage=TrySavePreferences()?"Controller layout saved.":"Could not save layout. See the error notice.";}
            if(NativeControllerPointer.Button("Restore controller defaults",button)){prefs.controller=new NativeControllerPreferences();prefs.controller.Normalize();padCapture=-1;padNeutral=true;padMessage="Defaults restored. Save layout to keep them.";}
            GUILayout.EndHorizontal();
        }
    }
}
