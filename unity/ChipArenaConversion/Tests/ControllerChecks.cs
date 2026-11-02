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
        var remap=new NativeControllerPreferences();remap.Normalize();NativeControllerBindings.Bind(remap,PadAction.Chips,(int)PadButton.South);
        var menuState=new NativeControllerState();menuState.Step(remap.bindings,0,false,false);
        menuState.Step(remap.bindings,1u<<(int)PadButton.South,false,true);
        Check(!menuState.Down.Any(v=>v)&&!menuState.Held.Any(v=>v),"menu confirmation suppresses remapped gameplay actions");
        menuState.Step(remap.bindings,1u<<(int)PadButton.South,false,false);
        Check(!menuState.Down.Any(v=>v),"leaving menu with confirm held does not fire a gameplay action");
        var nav=new NativeMenuNavigation();
        nav.BeginFrame();nav.Add(new NativeMenuNavigation.Item{X=0,Y=0,W=100,H=30});nav.Add(new NativeMenuNavigation.Item{X=120,Y=0,W=100,H=30});nav.Add(new NativeMenuNavigation.Item{X=0,Y=50,W=100,H=30,Slider=true});
        Check(nav.Pulse(0,.4f,0)==0,"menu stick drift does not move focus");
        Check(nav.Pulse(0,1,0)==3&&nav.Pulse(0,1,.2f)==0&&nav.Pulse(0,1,.36f)==3&&nav.Pulse(0,1,.4f)==0,"menu hold delays and repeats without frame-rate bursts");
        Check(nav.Pulse(0,0,.41f)==0&&nav.Pulse(1,0,.42f)==1,"menu release and direction change rearm navigation");
        nav.Move(3);Check(nav.Focus==0,"first menu input selects an available option");
        nav.Move(1);Check(nav.Focus==1,"menu right selects neighboring column");
        nav.Move(2);nav.Move(3);Check(nav.Focus==2,"menu down selects next row");
        Check(nav.Move(1)==1&&nav.Focus==2&&nav.Move(2)==-1,"menu left/right adjust focused slider without leaving it");
        nav.Move(4);Check(nav.Focus==0,"menu up leaves slider for previous row");
        nav.Reset();Check(!nav.Current(out _),"menu context reset removes stale controls and focus");
        nav.BeginFrame();nav.Add(new NativeMenuNavigation.Item{X=0,Y=100,W=100,H=30,Clipped=true,ClipY=0,ClipH=50});nav.EnsureFocus();
        Check(nav.ScrollNeeded()==86,"menu focus scrolls lower options into view");
        nav.BeginFrame();nav.Add(new NativeMenuNavigation.Item{X=0,Y=-30,W=100,H=20,Clipped=true,ClipY=0,ClipH=50});
        Check(nav.ScrollNeeded()==-36,"menu focus scrolls upper options into view");
        nav.BeginFrame();Check(!nav.Current(out _)&&nav.Move(3)==0,"empty menu cannot activate stale focus");
    }
}
