using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

// Engine-free checks for the chip table stage (Core/NativeDraftTable.cs): the layout by grade, the 65 degree framing at
// 1280x720, 1920x1080 and 3440x1440 (every chip and the pilot's upper body inside the frame, no chip ever drawn as large as
// the pilot's hand), the pick timeline, frame-rate independence from 30 to 240 Hz and an allocation-free step. Unity's
// rendering of the stage is not exercised.
internal static class DraftTableChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Draft table: "+what);}
    private static readonly string[] Grades={"legendary","rare","rare","common","epic","uncommon","common","mythic","rare","uncommon","l44t","common"};
    public static IEnumerable<string> Run()
    {
        var order=new int[12];var slots=new Vector3[12];var rowOf=new int[12];int layouts=0;
        // Layout: rows by grade (rarest at the back), at most three rows of six, real-size chips that never touch.
        for(int n=1;n<=12;n++)for(int shift=0;shift<12;shift++)
        {
            var tiers=new string[n];for(int i=0;i<n;i++)tiers[i]=Grades[(i+shift)%Grades.Length];
            int rows=NativeDraftTable.Arrange(tiers,n,order,slots,rowOf);Check(rows>=1&&rows<=NativeDraftTable.MaxRows,"rows "+rows+" for "+n);
            var seen=new bool[n];for(int k=0;k<n;k++){Check(!seen[order[k]],"order repeats an offer");seen[order[k]]=true;}
            for(int k=1;k<n;k++){int a=order[k-1],b=order[k];Check(NativeDraftTable.Rank(tiers[a])>=NativeDraftTable.Rank(tiers[b]),"reading order not by grade");Check(rowOf[b]>=rowOf[a],"reading order goes back a row");
                if(rowOf[a]==rowOf[b])Check(slots[b].X<slots[a].X-NativeDraftTable.PitchX*.99f,"reading order not left to right on screen (world -x)");}
            var perRow=new int[3];for(int i=0;i<n;i++)perRow[rowOf[i]]++;foreach(int c in perRow)Check(c<=NativeDraftTable.MaxPerRow,"more than six in a row");
            for(int i=0;i<n;i++){var s=slots[i];float h=NativeDraftTable.Chip*.5f+NativeDraftTable.Pin;Check(Math.Abs(s.X)+h<NativeDraftTable.TableHalf-.1f&&s.Z-h>NativeDraftTable.TableNear+.08f&&s.Z+h<NativeDraftTable.TableFar-.08f,"chip off the mat");
                for(int j=0;j<i;j++)Check(Math.Abs(slots[j].X-s.X)>=2*h||Math.Abs(slots[j].Z-s.Z)>=2*h,"chips overlap");}
            layouts++;
        }
        yield return "Table layout: "+layouts+" offer sets of 1-12 chips in at most three grade rows of six, rarest at the back, reading order left to right, no overlaps";

        // Framing and sizes at the three resolutions: every frame point inside the window; chips (resting and lifted) at
        // most 80% of a resting palm's projected width, and at least 36 px across at 1280x720.
        var rig=new MeleeRig();var pose=new CapturedRig.Pose();var points=new Vector3[NativeDraftTable.MaxChips*4+8];float worst=0,smallest=float.MaxValue;int frames=0;
        foreach(var (w,h) in new[]{(1280,720),(1920,1080),(3440,1440)})foreach(bool hat in new[]{false,true})for(int n=1;n<=12;n++)
        {
            var tiers=new string[n];for(int i=0;i<n;i++)tiers[i]=Grades[i];NativeDraftTable.Arrange(tiers,n,order,slots);
            NativeDraftTable.Seat(rig,pose,0,out _,out _);var top=NativeDraftTable.ToWorld(pose.WorldPosition[CapturedRig.Head])+new Vector3(0,.36f+(hat?.16f:0),0);
            var ui=NativeUiMotion.Measure(w,h);float card=Math.Max(360,Math.Min(430,ui.Width*.25f)),right=Math.Max(.45f,Math.Min(.9f,(ui.Width-(ui.Height<=760?22:28)-card-24)/ui.Width));
            int count=NativeDraftTable.FramePoints(slots,n,top,.52f,points);var view=NativeDraftTable.Frame(points,count,w/(float)h,.03f,right,.1f,.83f);
            for(int i=0;i<count;i++){var v=NativeDraftTable.Project(view,points[i],out float depth);Check(depth>.2f&&v.X>=.03f-.002f&&v.X<=right+.002f&&v.Y>=.1f-.002f&&v.Y<=.83f+.002f,w+"x"+h+" point "+i+" outside the frame "+v);}
            Vector2 Px(Vector3 p){var v=NativeDraftTable.Project(view,p,out _);return new Vector2(v.X*w,(1-v.Y)*h);}
            var m=new DraftTableMotion();m.Step(slots,order,n,0,10,-1,-1,0,-1,-1,1/60f,false);float palm=float.MaxValue;
            foreach(int b in new[]{CapturedRig.LHand,CapturedRig.RHand}){var c=NativeDraftTable.ToWorld(m.Pose.WorldPosition[b]);palm=Math.Min(palm,(Px(c+new Vector3(.0725f,0,0))-Px(c-new Vector3(.0725f,0,0))).Length());}
            float half=NativeDraftTable.Chip*.5f+NativeDraftTable.Pin;
            for(int i=0;i<n;i++)foreach(float lift in new[]{0f,NativeDraftTable.Lift})
            {
                var s=slots[i]+new Vector3(0,lift,0);float across=(Px(s+new Vector3(half,0,0))-Px(s-new Vector3(half,0,0))).Length(),deep=(Px(s+new Vector3(0,0,half))-Px(s-new Vector3(0,0,half))).Length();
                worst=Math.Max(worst,Math.Max(across,deep)/palm);if(w==1280)smallest=Math.Min(smallest,Math.Min(across,deep));
            }
            frames++;
        }
        Check(worst<.8f,"a chip is drawn at "+worst+" of the palm");Check(smallest>=36,"chips are only "+smallest+" px at 720p");
        yield return "Table framing at 1280x720, 1920x1080 and 3440x1440 ("+frames+" frames): chips and the pilot's upper body inside the frame; the largest chip (lifted) is "+(worst*100).ToString("0")+"% of a resting palm, the smallest "+smallest.ToString("0")+" px at 720p";

        // The pick timeline: reach, grab, carry, plant (the sound inside it), pulse, return.
        Check(NativeDraftTable.Reach(NativeDraftTable.ReachEnd)>.999f&&NativeDraftTable.Grab(NativeDraftTable.ReachEnd)<.001f&&NativeDraftTable.Plant(NativeDraftTable.PlantEnd)>.999f,"timeline ends");
        Check(NativeDraftTable.SoundAt>=NativeDraftTable.PlantStart&&NativeDraftTable.SoundAt<=NativeDraftTable.PlantEnd&&NativeDraftTable.InstalledAt>=NativeDraftTable.PlantStart,"the install sound and label land on the plant");
        Check(NativeDraftTable.Pulse(NativeDraftTable.PulseStart-.01f)<0&&NativeDraftTable.Pulse(NativeDraftTable.PulseEnd+.01f)<0&&Math.Abs(NativeDraftTable.Pulse((NativeDraftTable.PulseStart+NativeDraftTable.PulseEnd)/2)-.5f)<1e-4f,"pulse window");
        yield return InstallCue();

        // Motion: the same hover-then-pick session at 30, 60, 144 and 240 Hz. Wrists and the picked chip never jump between
        // frames (bounded speed), the rates agree at shared instants, the chip ends planted and the step allocates nothing.
        {
            var tiers=new string[8];for(int i=0;i<8;i++)tiers[i]=Grades[i];NativeDraftTable.Arrange(tiers,8,order,slots);int a=order[1],b=order[6];
            var samples=new Dictionary<int,List<Vector3>>();float fastest=0;long allocated=0;string fastestAt="";
            foreach(int hz in new[]{30,60,144,240})
            {
                var m=new DraftTableMotion();float dt=1f/hz;Vector3 lastL=default,lastR=default,lastChip=default;bool first=true,chipShown=false;var list=samples[hz]=new List<Vector3>();int frame=0;
                int steps=(int)Math.Round(5.0*hz);
                for(int f=0;f<=steps;f++)
                {
                    float t=f*dt;int hover=t<1.6f?-1:t<2.6f?a:b;float age=t-3.2f;int chosen=age>=0?b:-1;
                    long before=GC.GetAllocatedBytesForCurrentThread();m.Step(slots,order,8,t,t,chosen>=0?-1:hover,chosen>=0?-1:hover,0,chosen,age,dt,false);if(f>hz)allocated+=GC.GetAllocatedBytesForCurrentThread()-before;
                    var l=NativeDraftTable.ToWorld(m.Pose.WorldPosition[CapturedRig.LHand]);var r=NativeDraftTable.ToWorld(m.Pose.WorldPosition[CapturedRig.RHand]);bool shown=m.Visible[b];var chip=shown?m.Position[b]:lastChip;
                    if(!first){float step=Math.Max((l-lastL).Length(),(r-lastR).Length());if(shown&&chipShown)step=Math.Max(step,(chip-lastChip).Length());step/=dt;if(step>fastest){fastest=step;fastestAt=hz+" Hz t="+t.ToString("0.000");}}
                    first=false;lastL=l;lastR=r;lastChip=chip;chipShown=shown;
                    if(f%(hz/6)==0){list.Add(l);list.Add(r);list.Add(chip);frame++;}
                    if(age>NativeDraftTable.PlantEnd+.05f)Check(!m.Visible[b],"the picked chip is still on screen after the plant");
                    if(age>=0&&age<NativeDraftTable.PlantStart)Check(m.Visible[b]&&m.Scale[b]>.99f,"the picked chip shrinks before the plant");
                }
            }
            float spread=0;int count=Math.Min(Math.Min(samples[30].Count,samples[60].Count),Math.Min(samples[144].Count,samples[240].Count));
            for(int i=0;i<count;i++)foreach(int hz in new[]{30,144,240})spread=Math.Max(spread,(samples[hz][i]-samples[60][i]).Length());
            Check(fastest<4.5f,"a wrist or the chip moves "+fastest+" m/s between frames (a pop) at "+fastestAt);Check(spread<.03f,"rates disagree by "+spread+" m");Check(allocated==0,"DraftTableMotion.Step allocated "+allocated+" bytes");
            yield return "Table motion at 30/60/144/240 Hz: fastest wrist or chip "+fastest.ToString("0.00")+" m/s (no pops), rates agree within "+(spread*1000).ToString("0.0")+" mm over "+count/3+" shared instants, the pick plants and hides the chip, 0 bytes allocated per step";
        }
    }
    // The install cue lands on the plant: PvP from the pick time; the follow-through holds my own event and plays it once.
    private static string InstallCue()
    {
        float at=NativeDraftTable.SoundAt;double plant=10+(double)at;
        Check(!NativeInstallCue.DraftDue(10,10)&&!NativeInstallCue.DraftDue(10,plant-.01)&&NativeInstallCue.DraftDue(10,plant)&&NativeInstallCue.DraftDue(10,plant+.39)&&!NativeInstallCue.DraftDue(10,plant+NativeInstallCue.Fresh),"PvP cue window");
        int Plays(float[] arrive,float endAt,out float playedAt)
        {   // A follow-through from t=0 to endAt at 60 Hz; my event arrives at arrive[0] (and again at arrive[1] if given).
            var c=new NativeInstallCue();int plays=0;playedAt=-1;
            for(int f=0;f<=180;f++){float t=f/60f;string chip=t<endAt?"mishima-armor":null;if(c.Step(chip,0,t)!=null){plays++;playedAt=t;}
                foreach(float a in arrive)if(Math.Abs(t-a)<.5f/60&&!c.Hold("mishima-armor")){plays++;playedAt=t;}}
            return plays;
        }
        Check(Plays(new[]{.02f},NativeDraftTable.ReturnEnd,out float p)==1&&Math.Abs(p-at)<1.5f/60,"host pick: played "+p+" s, not at the plant");
        Check(Plays(new[]{1.2f},NativeDraftTable.ReturnEnd,out p)==1&&Math.Abs(p-1.2f-1/60f)<1.5f/60,"late guest event: played "+p);
        Check(Plays(new[]{.02f},.3f,out p)==1&&Math.Abs(p-.3f)<1.5f/60,"ended early: played "+p);
        Check(Plays(new[]{2.5f},NativeDraftTable.ReturnEnd,out p)==1&&Math.Abs(p-2.5f)<1.5f/60,"event after the follow-through: played "+p);
        Check(Plays(new[]{.02f,.5f},NativeDraftTable.ReturnEnd,out _)==2,"a second event is not swallowed");
        return "Install cue: PvP at the plant ("+at.ToString("0.00")+" s after the pick, .4 s fresh); the Siren follow-through plays my own Install once at the plant, at an early end, or on a late event";
    }
}
