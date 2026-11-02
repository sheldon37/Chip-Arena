using System;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class ControllerChecks
{
    static void Check(bool ok,string name){if(!ok)throw new Exception("Controller: "+name);Console.WriteLine("PASS controller "+name);}
    public static void Run()
    {
        var p=new NativeControllerPreferences();p.Normalize();
        Check(p.enabled&&p.bindings.Length==(int)PadAction.Count,"old saves receive defaults");
        Check(p.bindings.Distinct().Count()==p.bindings.Length,"defaults cover all actions without conflicts");
        int attack=p.bindings[(int)PadAction.Attack],jump=p.bindings[(int)PadAction.Jump];
        Check(NativeControllerBindings.Bind(p,PadAction.Attack,jump)==(int)PadAction.Jump&&p.bindings[(int)PadAction.Jump]==attack,"conflicts swap actions");
        var jsonOptions=new JsonSerializerOptions{IncludeFields=true};
        var copy=JsonSerializer.Deserialize<NativeControllerPreferences>(JsonSerializer.Serialize(p,jsonOptions),jsonOptions);copy.Normalize();
        Check(copy.bindings.SequenceEqual(p.bindings),"custom layout persists through JSON round trip");
        p.bindings[0]=-5;p.sensitivity=float.NaN;p.deadzone=float.PositiveInfinity;p.labels="unknown";p.Normalize();
        Check(p.bindings.SequenceEqual(NativeControllerBindings.Defaults())&&p.sensitivity==180&&p.deadzone==.18f&&p.labels=="Auto","corrupt settings safely recover");
        p.bindings[1]=p.bindings[0];p.Normalize();Check(p.bindings.Distinct().Count()==p.bindings.Length,"duplicate saved bindings recover");
        Check(NativeControllerBindings.Label((int)PadButton.South,"PlayStation")=="Cross"&&NativeControllerBindings.Label((int)PadButton.South,"Xbox")=="A","platform labels share physical position");
        Check(NativeControllerBindings.AxisScale(.1f,.18f)==0&&Math.Abs(NativeControllerBindings.AxisScale(1,.18f)-1)<.0001,"deadzone preserves full stick range");
        var state=new NativeControllerState();uint trigger=1u<<(int)PadButton.RightTrigger;
        state.Step(p.bindings,trigger,false,false);Check(!state.Held[(int)PadAction.Attack],"connect while held waits for release");
        state.Step(p.bindings,0,false,false);state.Step(p.bindings,trigger,false,false);Check(state.Down[0]&&state.Held[0],"attack press produces edge and hold");
        state.Step(p.bindings,trigger,false,false);Check(!state.Down[0]&&state.Held[0],"held trigger does not repeat edge");
        state.Step(p.bindings,0,false,true);Check(state.Up[0]&&!state.Held[0],"disconnect or focus loss releases held action");
        state.Step(p.bindings,trigger,false,false);Check(!state.Held[0],"return waits for neutral");
        state.Step(p.bindings,0,false,false);uint up=1u<<(int)PadButton.Up;
        state.Step(p.bindings,up,true,false);Check(state.Down[(int)PadAction.Slot5]&&!state.Held[(int)PadAction.Slot1],"modifier selects fifth ability exclusively");
        state.Step(p.bindings,up,false,false);Check(state.Up[(int)PadAction.Slot5]&&!state.Held[(int)PadAction.Slot1],"modifier release does not fire base ability");
        state.Step(p.bindings,0,false,false);state.Step(p.bindings,up,false,false);Check(state.Down[(int)PadAction.Slot1],"fresh physical press rearms base ability");
        var defaults=new NativeControllerPreferences();defaults.Normalize();Check(defaults.bindings.SequenceEqual(NativeControllerBindings.Defaults()),"reset returns a fresh independent layout");
    }
}
