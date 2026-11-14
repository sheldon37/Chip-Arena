using System;
using System.IO;
using ChipArena.UnityConversion;

// The controller's menu focus feedback (Core NativeMenuFocusMotion, Runtime NativeMenuFocusFx): the hover tick only on a
// move or a fresh focus, the pop and breath curves, and the wiring in the controller cursor and NativeUi's sound hook.
internal static class MenuFocusChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Menu focus: "+what);count++;}
    public static void Run(string root)
    {
        count=0;var m=new NativeMenuFocusMotion();
        Check(m.Step(0,1),"the first focus ticks");
        for(int f=1;f<120;f++)Check(!m.Step(0,1+f/60.0),"a held focus never ticks again ("+f+")");
        Check(m.Step(3,4)&&m.MovedAt==4,"a move ticks and restarts the pop");
        Check(!m.Step(3,4.02),"the next frame on the same control is quiet");
        m.Hide();Check(m.Step(3,9),"focus shown again after the mouse took over ticks");
        int ticks=0;for(int i=0;i<10;i++)if(m.Step(i%2,10+i*.12))ticks++;Check(ticks==10,"held-direction repeats tick on each step");
        Check(NativeMenuFocusMotion.PopAt(0,false)==1&&NativeMenuFocusMotion.PopAt(NativeMenuFocusMotion.Pop,false)==0,"the pop runs from 1 to 0");
        float prev=2;for(double t=0;t<=NativeMenuFocusMotion.Pop+.05;t+=.005){float p=NativeMenuFocusMotion.PopAt(t,false);Check(p>=0&&p<=1&&p<=prev,"the pop only eases out ("+t+")");prev=p;}
        Check(NativeMenuFocusMotion.PopAt(-1,false)==0&&NativeMenuFocusMotion.PopAt(double.NaN,false)==0&&NativeMenuFocusMotion.PopAt(double.NegativeInfinity,false)==0,"no pop before a move or at an unknown time");
        Check(NativeMenuFocusMotion.PopAt(0,true)==0&&NativeMenuFocusMotion.Breath(.4,true)==0,"reduced effects: no pop, no breath");
        for(double t=0;t<4;t+=.05){float b=NativeMenuFocusMotion.Breath(t,false);Check(b>=0&&b<=1,"the breath stays in 0..1");}
        Check(NativeMenuFocusMotion.Breath(double.NaN,false)==0,"no breath at an unknown time");
        // Wiring: the cursor draws through the effect (and hides it when focus is not shown); the tick goes through the one hook.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        var cursor=R("NativeController.cs");int at=cursor.IndexOf("private void ControllerCursor()",StringComparison.Ordinal);var body=at>=0?cursor.Substring(at,Math.Min(900,cursor.Length-at)):"";
        Check(body.Contains("NativeMenuFocusFx.Draw(")&&body.Contains("NativeMenuFocusFx.Hide()")&&!body.Contains("GUI.DrawTexture"),"the controller cursor draws the focus glow and hides it");
        var fx=R("NativeMenuFocusFx.cs");Check(fx.Contains("NativeUi.Cue(NativeUiSounds.Hover)")&&fx.Contains("EventType.Repaint"),"the effect ticks through NativeUi on Repaint");
        Check(R("NativeUi.cs").Contains("public static void Cue(string key)=>Sound?.Invoke(key);"),"NativeUi raises outside cues on its one sound hook");
        foreach(var f in new[]{"Core/NativeMenuFocusMotion.cs.meta","Runtime/NativeMenuFocusFx.cs.meta"}){var t=File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));Check(t.Contains("guid: ")&&!t.Contains(" \n"),"meta "+f);}
        Console.WriteLine("PASS menu focus: "+count+" checks (the hover tick only on a move or a fresh focus, the pop and breath, the glow wired into the controller cursor)");
    }
}
