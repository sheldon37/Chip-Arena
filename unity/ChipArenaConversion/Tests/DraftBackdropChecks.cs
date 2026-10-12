#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The chip draft's animated cyber backdrop (Core/NativeDraftBackdrop.cs, Resources/NativeDraftBackdrop.shader,
    // Runtime/NativeDraftBackdropPresentation.cs). Checked: the palette is NativeCyberTheme's; the light follows the stage tint
    // (snapped on a cold start, eased after, never jumping) and the frame stays cyber cyan; the pick draws the packets in and the
    // plant pulse runs its ring out and flashes, only then; Reduced effects is calm (no packets, scan, fall, twinkle, wave or
    // flash; the hall barely breathes and nothing else moves); the low tier drops the motes and streams; under the heading and
    // over the tray the detail and the brightest channel stay under their caps for every state, screen and variant (and the
    // column behind the head never reaches the heading's text); the quad covers the view under the opaque shade; the clocks
    // never jump or run back and hold across a draft's pickers and a killcam; a frame allocates nothing; and the Runtime builds
    // the quad and material once, sets every uniform the shader reads, draws for the draft camera only and touches no lighting
    // state, with the shader registered for the Unity build check.
    internal static class DraftBackdropChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Draft backdrop: "+what);count++;}
        private static void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private const float Dt=1f/60;
        private static readonly Vector3 Teal=NativeDraftBackdrop.Hex(0x70e1d0),Orange=NativeDraftBackdrop.Hex(0xffae55);
        private static readonly int[] Rarities={0xf1f5f9,0x79e59a,0x70b5ff,0xce8aff,0xffae55,0xff6475,0x4dff83,0xff354e,0xb3b5c2,0xb7a8ff,0x70e1d0,0xffffff};
        public static void Run(string root)
        {
            Palette(root);Tint();Reaction();Reduced();Low();Safe();Quad();Clocks();Allocation();Sources(root);
            Console.WriteLine("PASS draft backdrop ("+count+" checks: NativeCyberTheme palette, light follows the stage tint without a jump and the frame stays cyan, pick inflow and plant ring/flash only at the pick, Reduced calm (nothing moves but a slow breath), low tier drops motes and streams, heading and tray bands under their detail and brightness caps on every screen and state, quad covers the view, clocks never jump and hold across pickers and a killcam, zero allocation per frame, quad and material built once, every uniform set, draft camera only, no lighting state touched, shader in the build check)");
        }
        // The stage's real camera (NativeDraftStage framed for a tray at `tray`), in Unity world as the Runtime reads it.
        private static Vector3 U(Vector3 p)=>new Vector3(-p.X,p.Y+.88f,p.Z);
        private static NativeDraftStage Stage(float aspect,float tray)
        {
            var st=new NativeDraftStage();var cards=new Vector2[1];
            for(int i=0;i<3;i++){st.Begin(Dt,false);st.Frame(aspect,tray,Math.Min(.97f,tray+.2f),0,false,Dt);st.Step(Dt,-1,-1,false,cards,0,false,false);}
            return st;
        }
        private static DraftBackdropInput Input(NativeDraftStage st,float aspect,float tray,float head=.15f)
        {
            var eye=U(st.CameraPosition);var fwd=Vector3.Normalize(U(st.CameraTarget)-eye);var right=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,fwd));var up=Vector3.Cross(fwd,right);float line=st.ShadeLine(tray);
            return new DraftBackdropInput{Dt=Dt,Age=-1,Pulse=-1,Tint=Teal,HeadRow=head,TrayRow=line,DeepRow=st.ShadeDeep(line),CamPos=eye,CamForward=fwd,CamUp=up,CamRight=right,TanY=(float)Math.Tan(NativeDraftStage.Fov*Math.PI/360),Aspect=aspect,Near=.06f,Far=350,Glow=U(NativeDraftBackdrop.GlowPoint(st))};
        }
        private static DraftBackdropInput Default(){var st=Stage(16f/9,.72f);return Input(st,16f/9,.72f);}
        private static float Max3(Vector3 v)=>Math.Max(v.X,Math.Max(v.Y,v.Z));
        private static Vector3 Xyz(Vector4 v)=>new Vector3(v.X,v.Y,v.Z);
        // The NativeCyberTheme hexes the backdrop uses are the theme's (the Runtime file is the source of truth).
        private static void Palette(string root)
        {
            string theme=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeCyberTheme.cs"));
            int Hex(string name){var m=Regex.Match(theme,name+@"=0x([0-9a-fA-F]{6})");Check(m.Success,"NativeCyberTheme declares "+name);return Convert.ToInt32(m.Groups[1].Value,16);}
            Check(NativeDraftBackdrop.CyanHex==Hex("CyanHex")&&NativeDraftBackdrop.CyanDeepHex==Hex("CyanDeepHex")&&NativeDraftBackdrop.MagentaHex==Hex("MagentaHex")&&NativeDraftBackdrop.VioletHex==Hex("VioletHex"),"accents are the theme's cyan, deep cyan, magenta and violet");
            Check(NativeDraftBackdrop.VoidHex==Hex("VoidHex")&&NativeDraftBackdrop.NavyHex==Hex("NavyHex")&&NativeDraftBackdrop.IndigoHex==Hex("IndigoHex"),"darks are the theme's void, navy and indigo");
            Near(NativeDraftBackdrop.Linear(.5f),Math.Pow((.5+.055)/1.055,2.4),1e-6,"exact sRGB decode");Near(NativeDraftBackdrop.Linear(.02f),.02/12.92,1e-7,"sRGB decode toe");
        }
        // The light is the stage tint (brightest channel 1); a cold start snaps it, then it follows at TintRate and never jumps;
        // the frame stays cyan-dominant for every rarity; the Siren drafter's ink and the rainbow prism.
        private static void Tint()
        {
            var s=new DraftBackdropState();var i=Default();i.Entering=true;NativeDraftBackdrop.Step(s,i);i.Entering=false;
            Check(s.Tint==Teal,"a cold start snaps to the stage tint");var light=NativeDraftBackdrop.Linear(Teal);light/=Max3(light);
            Near(Vector3.Distance(Xyz(s.Accent),light),0,1e-5,"the light is the stage tint, linear, brightest channel 1");
            float start=Vector3.Distance(Teal,Orange),last=start;i.Tint=Orange;bool monotone=true;float worst=0;int frames=0;
            for(float t=0;t<1.5f;t+=Dt)
            {
                var before=s.Tint;NativeDraftBackdrop.Step(s,i);frames++;float d=Vector3.Distance(s.Tint,Orange);monotone&=d<=last+1e-6f;worst=Math.Max(worst,Vector3.Distance(before,s.Tint));last=d;
                if(frames==22)Check(d<.06f*start,"95% of a tint change in .37 s ("+d/start+" left)");
            }
            Check(monotone,"the tint approaches the stage tint without overshoot");Check(last<1e-3f*start,"the tint settles on the stage tint");
            Check(worst<=start*NativeDraftBackdrop.TintRate*Dt*1.01f,"no tint jump in a frame ("+worst+")");
            foreach(int hex in Rarities)
            {
                var r=new DraftBackdropState();var k=Default();k.Tint=NativeDraftBackdrop.Hex(hex);k.Entering=true;NativeDraftBackdrop.Step(r,k);
                Near(Max3(Xyz(r.Accent)),1,1e-5,"light "+hex.ToString("x6")+" at full brightness");var f=Xyz(r.Frame);Check(f.Z>=f.X&&f.Y>=f.X*1.2f,"frame stays cyan for "+hex.ToString("x6"));
                Check(Max3(Xyz(r.Deep))<.05f&&Max3(Xyz(r.Void))<.01f,"haze and void stay dark navy for "+hex.ToString("x6"));
            }
            var siren=new DraftBackdropState();var sv=Default();sv.Siren=true;sv.Entering=true;NativeDraftBackdrop.Step(siren,sv);var sf=Xyz(siren.Frame);var sc=Xyz(siren.Counter);
            Check(siren.Counter.W==1&&sf.Z>=sf.Y&&sc.X>sc.Y&&sc.Z>sc.Y,"the Siren drafter's ink: violet frame, magenta counter");Check(Xyz(siren.Accent).Z>=Xyz(siren.Accent).Y*.95f,"the Siren drafter's light leans violet");
            var prism=new DraftBackdropState();var pv=Default();pv.Prism=true;pv.Ward=true;pv.Tint=Vector3.One;pv.Entering=true;NativeDraftBackdrop.Step(prism,pv);
            Check(prism.Style.Y==1&&prism.Style.X==1&&Xyz(prism.Accent)==Vector3.One,"the rainbow pick: white light, the shader's prism, the ward");
            var bad=new DraftBackdropState();var bv=Default();bv.Entering=true;NativeDraftBackdrop.Step(bad,bv);bv.Entering=false;bv.Tint=new Vector3(float.NaN,0,0);NativeDraftBackdrop.Step(bad,bv);Check(bad.Tint==Teal,"a broken tint keeps the last one");
        }
        // The pick's inflow (packets rush to the core, the flow clock runs faster) only between the pick and the plant; the plant
        // ring runs out and fades over the pulse and the flash is NativeLighting.Draft's (1 - p)^2, only during the pulse.
        private static void Reaction()
        {
            var s=new DraftBackdropState();var i=Default();i.Entering=true;NativeDraftBackdrop.Step(s,i);i.Entering=false;
            for(int f=0;f<60;f++)NativeDraftBackdrop.Step(s,i);
            Check(s.Pulse.W==0&&s.Pulse.Y==0&&s.Pulse.Z==0&&s.Pulse.X<0,"no inflow, ring or flash before the pick");
            float lastWave=-1,lastFlow=s.Flow;bool grow=true,fade=true;float lastFall=2,flowRest=0,flowRush=0,peakFlash=0;
            for(float age=0;age<3;age+=Dt)
            {
                i.Age=age;i.Pulse=NativeDraftPlant.Pulse(age);NativeDraftBackdrop.Step(s,i);float step=s.Flow-lastFlow;lastFlow=s.Flow;
                bool inPulse=age>=NativeDraftPlant.PulseStart&&age<NativeDraftPlant.PulseEnd;
                if(inPulse){grow&=s.Pulse.X>=lastWave;lastWave=s.Pulse.X;fade&=s.Pulse.Y<=lastFall+1e-6f;lastFall=s.Pulse.Y;peakFlash=Math.Max(peakFlash,s.Pulse.Z);Near(s.Pulse.Z,NativeDraftBackdrop.Flash*s.Pulse.Y,1e-6,"flash follows the ring's fall");}
                else Check(s.Pulse.Y==0&&s.Pulse.Z==0,"no ring or flash outside the plant pulse (age "+age+")");
                if(age>=NativeDraftBackdrop.InflowIn&&age<NativeDraftPlant.PulseStart){Near(s.Pulse.W,1,1e-5,"full inflow from the pick to the plant");flowRush=Math.Max(flowRush,step);}
                if(age>NativeDraftPlant.PulseStart+NativeDraftBackdrop.InflowOut){Check(s.Pulse.W==0,"the inflow eases off after the plant");flowRest=step;}
            }
            Check(grow&&fade,"the ring runs out and fades over the pulse");Near(peakFlash,NativeDraftBackdrop.Flash,.05,"the flash peaks at the plant");
            Near(NativeDraftBackdrop.WaveAt(0),NativeDraftBackdrop.WaveStart,1e-6,"ring starts behind the pilot");Near(NativeDraftBackdrop.WaveAt(1),NativeDraftBackdrop.WaveStart+NativeDraftBackdrop.WaveReach,1e-4,"ring reaches down the corridor");
            Near(flowRush/flowRest,1+NativeDraftBackdrop.FlowBoost,.02,"packets run faster during the inflow");
        }
        // Reduced effects: no packets, scan, streams' fall or twinkle (the flow clock stops), no ring, flash or inflow, no power-on
        // front, a shallower breath on a slower calm clock; with a still camera and tint nothing but that clock changes.
        private static void Reduced()
        {
            var s=new DraftBackdropState();var i=Default();i.Reduced=true;i.Entering=true;NativeDraftBackdrop.Step(s,i);i.Entering=false;
            Check(s.Gains.X==0&&s.Gains.Y==0&&s.Gains.Z<=.45f&&s.Gains.W<=.5f,"reduced: no packets or scan, dim still streams and motes");Check(s.Boot==NativeDraftBackdrop.BootReach,"reduced: no power-on front");
            Check(s.Deep.W==NativeDraftBackdrop.ReducedBreath&&NativeDraftBackdrop.ReducedBreath<NativeDraftBackdrop.Breath*.5f,"reduced: a shallow breath");
            float flow=s.Flow,time=s.Time;int frames=0;
            for(float age=-1;age<3;age+=Dt){frames++;i.Age=age<0?-1:age;i.Pulse=age<0?-1:NativeDraftPlant.Pulse(age);NativeDraftBackdrop.Step(s,i);Check(s.Pulse.Y==0&&s.Pulse.Z==0&&s.Pulse.W==0,"reduced: no ring, flash or inflow at "+age);}
            Check(s.Flow==flow,"reduced: the flow clock (packets, scan, the streams' fall, twinkle) stands still");Near(s.Time-time,frames*Dt*NativeDraftBackdrop.ReducedMotion,1e-4,"reduced: the calm clock runs slowly");
            for(int f=0;f<60;f++)NativeDraftBackdrop.Step(s,i);
            var a=Pack(s);NativeDraftBackdrop.Step(s,i);var b=Pack(s);for(int k=0;k<a.Length;k++)if(k!=0)Check(a[k]==b[k],"reduced: uniform "+k+" holds still");Near(b[0].W-a[0].W,Dt*NativeDraftBackdrop.ReducedMotion,1e-6,"reduced: only the slow calm clock moves");
            // Turning Reduced effects on mid-draft eases the motion out; the flow clock never jumps.
            var t=new DraftBackdropState();var j=Default();j.Entering=true;NativeDraftBackdrop.Step(t,j);j.Entering=false;for(int f=0;f<120;f++)NativeDraftBackdrop.Step(t,j);
            j.Reduced=true;float worst=0,lf=t.Flow;for(int f=0;f<300;f++){NativeDraftBackdrop.Step(t,j);worst=Math.Max(worst,t.Flow-lf);lf=t.Flow;}Check(worst<=Dt*1.0001f&&t.Gains.X<.01f,"switching to Reduced eases the flow out without a jump");
        }
        private static Vector4[] Pack(DraftBackdropState s)=>new[]{s.Cam,s.Clock,s.Fwd,s.Up,s.Right,s.Accent,s.Frame,s.Counter,s.Deep,s.Void,s.Pulse,s.Style,s.Fx,s.Safe,s.Cap,s.Glow};
        // The low graphics tier: no streams or motes (the shader's low variant skips them); packets and the scan stay.
        private static void Low()
        {
            var s=new DraftBackdropState();var i=Default();i.Low=true;i.Entering=true;NativeDraftBackdrop.Step(s,i);
            Check(s.Fx.Z==0&&s.Fx.W==0&&s.Fx.X==1&&s.Fx.Y==1,"low tier: no streams or motes, packets and scan stay");
        }
        // The heading band (its text, left, and the top-right meta, right) and the tray band keep at most HeadDetail / TrayDetail
        // of the detail under HeadCap / TrayCap on every screen, framing and state; the head column never reaches the heading's
        // text; elsewhere the hall stays under Ceiling, below the bloom's threshold.
        private static void Safe()
        {
            Check(NativeDraftBackdrop.HeadCap<=.025f&&NativeDraftBackdrop.TrayCap<=.02f&&NativeDraftBackdrop.HeadDetail<=.25f&&NativeDraftBackdrop.TrayDetail<=.2f,"text-safe caps are low (brightest channel .025 / .02 linear, a quarter of the detail)");
            Check(NativeDraftBackdrop.Ceiling<NativeBloomRules.Threshold,"the hall never reaches the bloom threshold");
            int samples=0;
            foreach(float aspect in new[]{1.25f,4f/3,1.6f,16f/9,2.389f})
            foreach(float tray in new[]{.55f,.64f,.72f,.8f})
            foreach(float head in new[]{.1f,.15f,.2f,.24f})
            foreach(int state in new[]{0,1,2,3,4,5,6})
            {
                var st=Stage(aspect,tray);var s=new DraftBackdropState();var i=Input(st,aspect,tray,head);i.Ward=state==4;i.Siren=state==5;i.Prism=state==6;i.Reduced=state==3;i.Entering=true;NativeDraftBackdrop.Step(s,i);i.Entering=false;
                if(state==1){i.Age=.4f;}if(state==2){i.Age=NativeDraftPlant.PulseStart+.01f;i.Pulse=NativeDraftPlant.Pulse(i.Age);}
                for(int f=0;f<40;f++)NativeDraftBackdrop.Step(s,i);
                Near(s.Safe.X,head,1e-6,"the heading band sits on the heading's row");Near(s.Safe.Y,Math.Max(i.TrayRow,head+.1f),1e-6,"the tray band sits on the shade's line");
                // The heading's text ends .47 screen heights in from either edge (its widest line plus the margin).
                float text=.47f/aspect;
                for(float row=0;row<=head-NativeDraftBackdrop.HeadIn;row+=.01f)for(float col=0;col<=1;col+=.01f)
                {
                    if(col>text&&col<1-text)continue;samples++;
                    Check(NativeDraftBackdrop.SafeAt(col,row,aspect,s.Safe,s.Cap)<=NativeDraftBackdrop.HeadDetail*1.01f,"heading detail at "+col+","+row+" aspect "+aspect);
                    Check(NativeDraftBackdrop.CapAt(col,row,aspect,s.Safe,s.Cap)<=NativeDraftBackdrop.HeadCap*1.01f,"heading cap at "+col+","+row+" aspect "+aspect);
                }
                for(float row=s.Safe.Y;row<=1;row+=.01f)for(float col=0;col<=1;col+=.05f){samples++;Check(NativeDraftBackdrop.SafeAt(col,row,aspect,s.Safe,s.Cap)<=NativeDraftBackdrop.TrayDetail*1.0001f&&NativeDraftBackdrop.CapAt(col,row,aspect,s.Safe,s.Cap)<=NativeDraftBackdrop.TrayCap*1.0001f,"tray band at "+col+","+row);}
                Check(NativeDraftBackdrop.CapAt(.5f,(head+s.Safe.Y)*.5f,aspect,s.Safe,s.Cap)<=NativeDraftBackdrop.Ceiling,"the hall's cap");
            }
            Check(samples>100000,"bands sampled ("+samples+")");
            // The head column: the full glow behind the head on wide screens, none on 5:4; the glow sits behind the pilot.
            Near(NativeDraftBackdrop.Gap(16f/9),NativeDraftBackdrop.HeadGap,1e-6,"16:9 keeps the head column");Check(NativeDraftBackdrop.Gap(1.25f)==0,"5:4 has no head column");
            {var st=Stage(16f/9,.72f);var s=new DraftBackdropState();var i=Input(st,16f/9,.72f);i.Entering=true;NativeDraftBackdrop.Step(s,i);
             Near(NativeDraftBackdrop.SafeAt(.5f,.05f,16f/9,s.Safe,s.Cap),1,1e-6,"behind the head the heading band leaves the glow");
             Near(s.Glow.X,.5,.01,"the glow is behind the pilot");Check(s.Glow.Y>st.Project(U2(st,NativeDraftStage.HeadTop)).Y&&s.Glow.Y<st.Project(st.RestPlexus).Y,"the glow sits between the head's top and the solar plexus ("+s.Glow.Y+")");}
        }
        private static Vector3 U2(NativeDraftStage st,float above){var p=st.Pose.WorldPosition[CapturedRig.Head];return new Vector3(p.X,p.Y+above,p.Z);}
        // The quad: half way to the far plane, over the whole view from above its top to under the opaque shade.
        private static void Quad()
        {
            foreach(float aspect in new[]{1.25f,16f/9,2.389f})
            {
                var st=Stage(aspect,.72f);var s=new DraftBackdropState();var i=Input(st,aspect,.72f);i.Entering=true;NativeDraftBackdrop.Step(s,i);
                Check(s.Distance>=i.Near*4&&s.Distance<=NativeDraftBackdrop.QuadMax&&s.Distance<i.Far,"quad between the near and far planes");Check(s.Top<0&&s.Bottom>=i.DeepRow&&s.Bottom<=1.02f,"quad from above the top to under the opaque shade");
                var q=NativeDraftBackdrop.QuadFrame(s,i.TanY,aspect);Check(q.X>=s.Distance*i.TanY*aspect,"quad as wide as the view");Check(q.Y+q.Z>=s.Distance*i.TanY&&q.Y-q.Z<=s.Distance*i.TanY*(1-2*i.DeepRow),"quad spans the rows it should");
                var bad=new DraftBackdropState();var b=i;b.DeepRow=-1;b.Far=float.NaN;b.TanY=0;b.Entering=true;NativeDraftBackdrop.Step(bad,b);Check(bad.Bottom>=1&&bad.Distance>0&&bad.Fwd.W>0,"unknown layout and camera fall back to the whole view");
            }
        }
        // The calm and flow clocks only run forward, never faster than their rate, through a draft's pickers (no cold start
        // between them: the presence and power-on hold) and resume across a killcam; they rebase only on a cold start past
        // RebaseAfter, when nothing is visible.
        private static void Clocks()
        {
            var s=new DraftBackdropState();var i=Default();i.Entering=true;NativeDraftBackdrop.Step(s,i);i.Entering=false;float lt=s.Time,lf=s.Flow;bool ok=true;
            for(int picker=0;picker<3;picker++)for(float age=-2;age<2.5f;age+=Dt)
            {
                i.Age=age<0?-1:age;i.Pulse=age<0?-1:NativeDraftPlant.Pulse(age);i.Tint=picker%2==0?Teal:Orange;NativeDraftBackdrop.Step(s,i);
                float dtc=s.Time-lt,dfc=s.Flow-lf;ok&=dtc>0&&dtc<=Dt*1.0001f&&dfc>=0&&dfc<=Dt*(1+NativeDraftBackdrop.FlowBoost)*1.0001f;lt=s.Time;lf=s.Flow;
                if(picker>0)Check(s.Presence==1&&s.Boot==NativeDraftBackdrop.BootReach,"a new picker never restarts the fade or the power-on");
            }
            Check(ok,"clocks run forward at their rate through three pickers");
            float held=s.Time;i.Entering=true;i.Age=-1;i.Pulse=-1;NativeDraftBackdrop.Step(s,i);Check(s.Time>held&&s.Time-held<=Dt*1.0001f&&s.Presence==0,"after a killcam the clocks resume where they were (the hall fades back in)");
            s.Time=NativeDraftBackdrop.RebaseAfter+1;s.Flow=2000;i.Entering=false;NativeDraftBackdrop.Step(s,i);Check(s.Time>NativeDraftBackdrop.RebaseAfter,"no rebase while visible");
            i.Entering=true;NativeDraftBackdrop.Step(s,i);Check(s.Time<=Dt*1.0001f&&s.Flow<=Dt*(1+NativeDraftBackdrop.FlowBoost)&&s.Presence==0,"rebased on a cold start, while invisible");
            // The fade in and the power-on front.
            var b=new DraftBackdropState();var j=Default();j.Entering=true;NativeDraftBackdrop.Step(b,j);j.Entering=false;Check(b.Presence==0&&b.Boot==0,"a cold start begins dark");
            for(float t=0;t<NativeDraftBackdrop.FadeIn+.05f;t+=Dt)NativeDraftBackdrop.Step(b,j);Check(b.Presence==1&&b.Boot>=NativeDraftBackdrop.BootSpeed*NativeDraftBackdrop.FadeIn*.95f,"faded in and powered on in "+NativeDraftBackdrop.FadeIn+" s");
        }
        // Step for many frames allocates nothing (the Runtime reuses one state, one input struct and one material).
        private static void Allocation()
        {
            var s=new DraftBackdropState();var i=Default();i.Entering=true;NativeDraftBackdrop.Step(s,i);i.Entering=false;for(int f=0;f<200;f++)NativeDraftBackdrop.Step(s,i);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<10000;f++){i.Age=f%600<300?-1:(f%300)*Dt;i.Pulse=i.Age<0?-1:NativeDraftPlant.Pulse(i.Age);i.Reduced=f%997==0;i.Tint=f%400<200?Teal:Orange;i.Entering=f%2500==0;NativeDraftBackdrop.Step(s,i);NativeDraftBackdrop.QuadFrame(s,i.TanY,i.Aspect);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes over 10000 frames)");
        }
        // The Runtime drawer, the draft's wiring, the shader and the build check.
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string drawer=R("NativeDraftBackdropPresentation.cs"),draft=R("NativeDraftPresentation.cs"),screen=R("NativeDraftScreen.cs");
            Check(Regex.Matches(drawer,@"new Material\(").Count==1&&drawer.Contains("if(draftBackMaterial==null)")&&drawer.Contains("materials.Add(draftBackMaterial);"),"the material is built once and released with the presentation");
            Check(Regex.Matches(drawer,@"new Mesh\b").Count==1&&drawer.Contains("if(draftBackQuad==null)draftBackQuad=BackdropQuad();"),"the quad is built once");
            int body=drawer.IndexOf("bool entering=",StringComparison.Ordinal),end=drawer.IndexOf("private static NVector BackN(",StringComparison.Ordinal);Check(body>0&&end>body,"drawer body found");string frame=drawer.Substring(body,end-body);
            Check(Regex.Matches(frame,@"\bnew (?!DraftBackdropInput\{|NVector\(|Vector3\()").Count==0&&!frame.Contains("=>")&&!frame.Contains("Resources.Load"),"a frame builds nothing (value types only, no closures or loads)");
            Check(frame.Contains("Graphics.DrawMesh(draftBackQuad,matrix,m,0,camera,0,null,ShadowCastingMode.Off,false);"),"drawn for the draft camera only, no shadows");
            Check(drawer.Contains("if(draftBackLow!=LowGraphics){draftBackLow=LowGraphics;if(draftBackLow)draftBackMaterial.EnableKeyword(BackLowKeyword);else draftBackMaterial.DisableKeyword(BackLowKeyword);}")&&drawer.Contains("BackLowKeyword=\"DRAFT_BACKDROP_LOW\""),"the low tier's keyword switches only when the tier changes");
            string code=Regex.Replace(drawer,@"//[^\n]*","");foreach(var word in new[]{@"\bRenderSettings\b",@"\bQualitySettings\b",@"\bLight\b",@"\.intensity\b",@"(?i)\bfog",@"(?i)\bambient",@"\bshadowStrength\b"})Check(!Regex.IsMatch(code,word),"the drawer leaves the stage's lighting state alone ("+word+")");
            Check(draft.Contains("NativeLighting.Draft(camera,tint,flash);\n            // The animated cyber corridor behind the pilot (NativeDraftBackdropPresentation.cs), on the stage's frame step.\n            DraftBackdrop(camera,tint,age,pulse,trayLine,reduced,siren,dt);"),"RenderDraft draws the backdrop on the stage's tint, age, pulse and step, after the stage lights");
            Check(draft.Contains("presentation.DraftHeadLine=draftHeadLine;presentation.DraftWard=pve;presentation.DraftPrism=pve&&tier==\"rainbow\";"),"DraftFrame passes the heading's row, the PvE / Siren pick's ward and the rainbow prism");
            Check(screen.Contains("bottom=y+Mathf.Clamp(H*.024f,4,30);draftHeadLine=bottom/H;"),"every draft heading records its bottom row");
            string shaderPath=Path.Combine(root,"Assets/ChipArena/Resources/NativeDraftBackdrop.shader");Check(File.Exists(shaderPath)&&File.Exists(shaderPath+".meta"),"shader and meta under Resources");string shader=File.ReadAllText(shaderPath);
            var names=Regex.Match(shader,@"float4 (_Back[^;]*);").Groups[1].Value.Split(',').Select(x=>x.Trim()).ToArray();Check(names.Length==16,"the shader's uniforms ("+names.Length+")");
            foreach(var n in names)Check(drawer.Contains("Shader.PropertyToID(\""+n+"\")")&&Regex.IsMatch(drawer,@"m\.SetVector\(Back"+n.Substring(5)+@"Id,BackV\(s\."),"the drawer sets "+n);
            Check(shader.Contains("\"Queue\"=\"Background\"")&&shader.Contains("ZWrite Off")&&shader.Contains("#pragma multi_compile_local __ DRAFT_BACKDROP_LOW")&&!shader.Contains("multi_compile_fog")&&!shader.Contains("UNITY_APPLY_FOG"),"drawn first, no depth write, a low variant, no fog");
            int a=shader.IndexOf("// ---- DRAFT BACKDROP SHARED BEGIN",StringComparison.Ordinal),z=shader.IndexOf("// ---- DRAFT BACKDROP SHARED END",StringComparison.Ordinal);Check(a>0&&z>a,"shared block markers (the offline GLSL renders compile it)");
            string shared=shader.Substring(a,z-a);
            Check(shared.Contains("#define BACK_FLOOR 0.0")&&NativeDraftBackdrop.FloorY==0&&shared.Contains("#define BACK_HALF 4.6")&&NativeDraftBackdrop.Half==4.6f&&shared.Contains("#define BACK_FAR 400.0")&&NativeDraftBackdrop.Reach==400,"the shader's corridor is the Core's");Check(!Regex.IsMatch(shared,@"\b(fmod|atan2|mul|static|ddx|ddy|rsqrt)\b"),"the shared block stays GLSL-portable");
            Check(Regex.Matches(shared,@"#ifndef DRAFT_BACKDROP_LOW").Count==2&&shared.IndexOf("BackGlyph(sq*",StringComparison.Ordinal)>shared.IndexOf("#ifndef DRAFT_BACKDROP_LOW",StringComparison.Ordinal),"the low variant skips the streams and the motes");
            Check(shared.Contains("float hb=BackSq((col-.5)*_BackUp.w/max(_BackCap.w,1e-3));float hs=max(smoothstep(_BackSafe.x-.01,_BackSafe.x+.07,row),exp(-hb*hb)),tb=1.0-smoothstep(_BackSafe.y-.06,_BackSafe.y,row);")&&NativeDraftBackdrop.HeadIn==.01f&&NativeDraftBackdrop.HeadOut==.07f&&NativeDraftBackdrop.TrayIn==.06f,"the shader's bands are SafeAt / CapAt's");
            Check(shared.Contains("float safe=lerp(_BackSafe.z,1.0,hs)*lerp(_BackSafe.w,1.0,tb),cap=min(lerp(_BackCap.x,_BackCap.z,hs),lerp(_BackCap.y,_BackCap.z,tb));")&&shared.Contains("float peak=max(c.r,max(c.g,c.b));return c*min(1.0,cap/max(peak,1e-6));"),"the shader keeps the safe share of the detail and caps the brightest channel");
            var metas=Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Select(p=>Regex.Match(File.ReadAllText(p),@"guid: ([0-9a-f]{32})").Groups[1].Value).ToList();
            foreach(var f in new[]{"Resources/NativeDraftBackdrop.shader.meta","Core/NativeDraftBackdrop.cs.meta","Runtime/NativeDraftBackdropPresentation.cs.meta"}){var g=Regex.Match(File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)),@"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&metas.Count(x=>x==g)==1,f+" has its own GUID");}
            Check(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs")).Contains("Resources.Load<Shader>(\"NativeDraftBackdrop\")"),"shader registered in NativeBuildChecks");
        }
    }
}
#endif
