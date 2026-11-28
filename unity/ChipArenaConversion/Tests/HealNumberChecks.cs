#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // First-person own heals (owner 2026-10-12: "Change healing number display to be less obstructive on first person"):
    // Core/NativeEventFeedback's NativeHealReadout and the track's `readout` diversion. A first-person view pilot's heals never
    // land in the central safe area (they feed the running total by the vitals, which keeps clear of it at every window and
    // interface scale, the old spot under the crosshair being inside it); 60 Hz ticks merge into one counting total that
    // allocates nothing; every other number (other targets' heals, all damage, own damage lanes, the cap) replays the
    // source parity fixture exactly; the third-person own-heal fade dims Classic's spawn on the crosshair and never the
    // shoulder rig's. Source scans pin the Runtime routing.
    internal static class HealNumberChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Heal numbers: "+what);count++;}
        private static bool Near(double a,double b,double e=1e-4)=>Math.Abs(a-b)<=e;
        private static ArenaEvent Heal(int id,float time,int target,float amount)=>new ArenaEvent{Id=id,Time=time,Kind=EventKind.Heal,Source=target,Target=target,Amount=amount};
        public static void Run(string root)
        {
            var bodies=new Dictionary<int,V3>{{1,new V3(0,.86f,0)},{2,new V3(4,.86f,8)},{3,new V3(-3,.86f,6)}};NativeCombatNumbers.Anchor anchor=(int id,out V3 b)=>bodies.TryGetValue(id,out b);

            // 1. Every other number is the source's: the parity fixture replayed with each pilot as the first-person view
            // gives the same numbers (ids, targets, lanes, amounts, times, positions, labels), only that pilot's heals
            // flagged Readout (and unlabelled), and their sum in the readout.
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/event-feedback-parity.json")));int replays=0,flagged=0;
            V3 Vec(JsonElement e)=>new V3(e.GetProperty("x").GetSingle(),e.GetProperty("y").GetSingle(),e.GetProperty("z").GetSingle());
            foreach(var scenario in document.RootElement.GetProperty("numbers").EnumerateArray())
            {
                int local=scenario.GetProperty("local").GetInt32();bool pve=scenario.GetProperty("pve").GetBoolean();var seats=new Dictionary<int,V3>();
                foreach(var p in scenario.GetProperty("players").EnumerateArray())seats[p.GetProperty("id").GetInt32()]=Vec(p.GetProperty("pos"));NativeCombatNumbers.Anchor seat=(int id,out V3 b)=>seats.TryGetValue(id,out b);
                foreach(int view in seats.Keys.Prepend(0))
                {
                    var plain=new NativeCombatNumbers();var diverted=new NativeCombatNumbers();int mine=flagged;
                    bool healed=view>0&&scenario.GetProperty("frames").EnumerateArray().Any(f=>f.GetProperty("events").EnumerateArray().Any(e=>e.GetProperty("kind").GetString()=="Heal"&&e.GetProperty("target").GetInt32()==view));
                    foreach(var frame in scenario.GetProperty("frames").EnumerateArray())
                    {
                        var events=frame.GetProperty("events").EnumerateArray().Select(e=>new ArenaEvent{Id=e.GetProperty("id").GetInt32(),Time=e.GetProperty("time").GetSingle(),Kind=(EventKind)Enum.Parse(typeof(EventKind),e.GetProperty("kind").GetString()),Source=e.GetProperty("source").GetInt32(),Target=e.GetProperty("target").GetInt32(),Amount=e.GetProperty("amount").GetSingle(),Position=Vec(e.GetProperty("pos"))}).ToList();
                        float time=frame.GetProperty("time").GetSingle();int gen=frame.GetProperty("generation").GetInt32();
                        var a=plain.Update(events,time,gen,local,pve,seat);var b=diverted.Update(events,time,gen,local,pve,seat,view);string where=scenario.GetProperty("name").GetString()+" view "+view+" @"+time;
                        Check(a.Count==b.Count,where+": "+b.Count+" numbers != "+a.Count);
                        for(int i=0;i<a.Count;i++)
                        {
                            var x=a[i];var y=b[i];bool own=view>0&&y.Heal&&y.Target==view;
                            Check(x.Id==y.Id&&x.Target==y.Target&&x.Heal==y.Heal&&x.Lane==y.Lane&&x.At==y.At&&Near(x.Amount,y.Amount,1e-6)&&(x.Position-y.Position).Length<1e-6,where+" #"+i+" same number");
                            Check(!x.Readout&&y.Readout==own&&(own?y.Label==null:y.Label==x.Label),where+" #"+i+" only the view pilot's heals go to the readout");if(own)flagged++;
                        }
                        Check(plain.Heals.Total==0&&(view>0||diverted.Heals.Total==0),where+": no readout without a first-person view");
                    }
                    Check(healed==(flagged>mine),"view "+view+": its heals reach the readout exactly when it is healed");replays++;
                }
            }
            Check(flagged>0,"the fixture's own heals reach the readout");

            // 2. A burst on the view pilot (1): 3 HP/s regeneration every 60 Hz tick and 4 HP lifesteal every 7th, beside an
            // ally (3) healing and a foe (2) taking hits. In first person no number of mine is drawable and the readout counts
            // the exact total; in third person the same events give the source's numbers.
            var fp=new NativeCombatNumbers();var tp=new NativeCombatNumbers();var events2=new List<ArenaEvent>();int id=0;float sum=0;string lastLabel="";int lastValue=0;bool monotonic=true;float firstShown=-1;
            for(int tick=0;tick<=120;tick++)
            {
                float t=10+tick/60f;events2.Clear();events2.Add(Heal(++id,t,1,.05f));sum+=.05f;
                if(tick%7==0){events2.Add(Heal(++id,t,1,4));sum+=4;events2.Add(new ArenaEvent{Id=++id,Time=t,Kind=EventKind.Hit,Source=1,Target=2,Amount=8});events2.Add(Heal(++id,t,3,.2f));}
                var f=fp.Update(events2,t,0,1,false,anchor,1);var p=tp.Update(events2,t,0,1,false,anchor);
                Check(f.Count==p.Count&&f.All(n=>n.Readout==(n.Heal&&n.Target==1)),"first person flags exactly my heals @"+t);
                Check(f.Where(n=>!n.Readout).Select(n=>n.Label).SequenceEqual(p.Where(n=>!(n.Heal&&n.Target==1)).Select(n=>n.Label)),"the foe's damage and the ally's heals read the same in both views");
                var r=fp.Heals;Check(Near(r.Total,sum,1e-3),"the readout holds the running total ("+r.Total+" of "+sum+")");
                if(r.Visible(t)){if(firstShown<0)firstShown=t;monotonic&=r.Value>=lastValue;lastValue=r.Value;lastLabel=r.Label;}
            }
            Check(p0(tp)>=4,"third person keeps a stack of my heal numbers (the source's)");
            Check(firstShown==10&&monotonic&&lastValue==(int)Math.Floor(sum+.5)&&lastLabel=="+"+lastValue,"one total counting up from the first 4 HP heal to +"+lastValue);
            int p0(NativeCombatNumbers track)=>track.Numbers.Count(n=>n.Heal&&n.Target==1);

            // 3. Merging rules: a sub-1 HP total waits unseen and shows once it reaches 1; a heal while it shows adds; one after
            // the Hold + Fade window starts a new total; a clock running back starts over; Clear empties it.
            // (1/32 HP ticks, exact in binary: 30 make .9375, the 32nd reaches 1, 60 make 1.875.)
            var h=new NativeHealReadout();for(int k=0;k<30;k++)h.Add(1+k/60f,1/32f);Check(h.Total==.9375f&&!h.Visible(1.5f)&&h.Alpha(1.5f,false)==0,"tiny ticks fold in unseen below 1 HP");
            for(int k=30;k<60;k++)h.Add(1+k/60f,1/32f);Check(h.Total==1.875f&&h.Visible(2)&&h.Value==2&&h.Label=="+2"&&Near(h.ShownAt,1+31/60f,1e-6),"shown from the tick that reaches 1 HP");
            h.Add(2.9f,3);Check(h.Total==4.875f&&h.Label=="+5","a heal while it shows (fading) adds to it");
            h.Add(2.9f+NativeHealReadout.Hold+NativeHealReadout.Fade+.01f,2);Check(Near(h.Total,2)&&h.Label=="+2","a heal after it has gone starts a new total");
            h.Add(1,6);Check(Near(h.Total,6)&&h.Last==1,"a clock running back starts over");h.Clear();Check(h.Total==0&&!h.Visible(1),"cleared");
            var track=new NativeCombatNumbers();track.Update(new List<ArenaEvent>{Heal(1,5,1,9)},5,0,1,false,anchor,1);Check(track.Heals.Visible(5),"track feeds it");
            track.Update(new List<ArenaEvent>(),5.1f,1,1,false,anchor,1);Check(!track.Heals.Visible(5.1f)&&track.Heals.Total==0,"a new generation (round) clears it with the numbers");

            // 4. The envelope: in over .12 s, full (.72) while heals come and .6 s after, out by Hold + Fade; it drifts up 10
            // only while fading and rises 4 into place; Reduced effects: no fade-in, no motion, the same end.
            h.Add(20,12);float o=NativeHealReadout.Opacity,end=20+NativeHealReadout.Hold+NativeHealReadout.Fade;
            Check(h.Alpha(20,false)==0&&Near(h.Alpha(20.06f,false),o*.5f)&&Near(h.Alpha(20.3f,false),o)&&Near(h.Alpha(20+NativeHealReadout.Hold,false),o)&&Near(h.Alpha(20.85f,false),o*.5f)&&h.Alpha(end+.001f,false)==0,"opacity envelope");
            Check(Near(h.Lift(20,false),-NativeHealReadout.Rise)&&Near(h.Lift(20.3f,false),0)&&Near(h.Lift(end-.0001f,false),NativeHealReadout.Drift,1e-2),"rises into place, drifts up while fading");
            Check(Near(h.Alpha(20,true),o)&&Near(h.Alpha(20.85f,true),o*.5f)&&h.Alpha(end+.001f,true)==0,"Reduced effects: no fade-in, same fade out");
            for(float t=19.9f;t<end+.2f;t+=.01f){Check(h.Lift(t,true)==0&&h.Alpha(t,false)<=o+1e-6f&&h.Alpha(t,true)<=o+1e-6f,"still and quiet with Reduced effects");}
            Check(NativeHealReadout.Size<18&&o<1,"smaller than the world numbers (18-25) and below full opacity");

            // 5. Never in the central safe area: the readout over the vitals (NativeFightHud.HealthBox: 24 from the left and
            // bottom, 256 wide, 93 high on melee and 124 otherwise) at every window 1280x720..3440x1440, interface scale
            // .75..1.5, lift and label width, stays on screen, above the panel, right of the widest sprint tag (mono .6 em
            // plus 5%) and inside the vitals column; the old spot under the crosshair (OwnX/OwnY) was inside it.
            var fightHud=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeFightHud.cs"));
            Check(fightHud.Contains("float w=256,pad=14,h=11+24+6+8+10+14+5+3+(melee?0:9+14+5+3)+12;")&&fightHud.Contains("var box=HealthBox(p,24,H-24);")&&NativeHealReadout.Pad==14,"the vitals box these checks lay out");
            float exhausted=NativeSprintTag.ExhaustedText.Length*.6f*NativeSprintTag.ExhaustedSize+NativeSprintTag.ExhaustedTracking*NativeSprintTag.ExhaustedSize*NativeSprintTag.ExhaustedText.Length,tagRight=24+1+(exhausted+2*NativeSprintTag.PadX+2*NativeSprintTag.Border)*1.05f;
            int cases=0;
            foreach(var (sw,sh) in new[]{(1280,720),(1600,900),(1920,1080),(2560,1440),(3440,1440)})foreach(float scale in new[]{.75f,1,1.25f,1.5f})foreach(float boxH in new[]{93f,124f})foreach(float lift in new[]{-NativeHealReadout.Rise,0,NativeHealReadout.Drift})
            {
                var c=NativeUiMotion.Measure(sw,sh,scale);float bx=24,by=c.Height-24-boxH;NativeHealReadout.Place(bx,by,256,lift,out float x,out float y,out float w,out float hh);string at=sw+"x"+sh+" x"+scale+" box "+boxH+" lift "+lift;
                Check(!NativeHealReadout.Central(x,y,w,hh,c.Width,c.Height),"readout clear of the centre at "+at);
                Check(x>=0&&y>=0&&y+hh<=by&&x+w<=bx+256-NativeHealReadout.Pad+1e-3f&&x>=tagRight&&w>=6*.6f*NativeHealReadout.Size,"readout on screen, over the panel, right of the sprint tag, six glyphs wide at "+at);
                for(int lane=0;lane<NativeCombatNumbers.Lanes;lane++)for(float age=0;age<NativeCombatNumbers.Life;age+=.05f)
                {float nx=c.Width*NativeCombatNumbers.OwnX(lane),ny=c.Height*NativeCombatNumbers.OwnY(age);Check(NativeHealReadout.Central(nx-20,ny-12,40,24,c.Width,c.Height),"the old own-number spot was central at "+at);}
                cases++;
            }

            // 6. Third person: my heal numbers spawn 1.15 m over my body and rise .9 m/s. Classic (4.4 m back, .55 up, FOV 60)
            // puts the spawn on the crosshair, where they dim to AimAlpha; the default right shoulder (.62 / .25 / 2.9 m, FOV 55)
            // keeps them clear (never dimmed) for pitches -30..45 over their whole life. Canvas 1600x900, 25-unit numbers.
            float Fade(NativeCameraShape s,bool classic,float pitchDeg,float age,string label,int lane=1)
            {
                double p=pitchDeg*Math.PI/180;var fwd=new V3(0,-(float)Math.Sin(p),(float)Math.Cos(p));var up=new V3(0,(float)Math.Cos(p),(float)Math.Sin(p));var eye=new V3(0,NativeViewRules.EyeHeight,0);
                V3 cam=classic?eye-fwd*NativeViewRules.ThirdDistance+new V3(0,NativeViewRules.ThirdLift,0):eye+new V3(s.Side,s.Lift,0)-fwd*s.Back;
                var d=new V3((lane-1)*NativeCombatNumbers.Drift*age,NativeCombatNumbers.Head+age*NativeCombatNumbers.Rise,0)-cam;float z=Dot(d,fwd),k=450/(float)Math.Tan((classic?NativeViewRules.ThirdFov:s.Fov)*Math.PI/360);
                return NativeCombatNumbers.AimFade(label,25,800+d.x/z*k,450-Dot(d,up)/z*k,800,450);
            }
            static float Dot(V3 a,V3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
            var shoulder=NativeCameraShape.ShoulderRight;
            Check(Near(Fade(shoulder,true,0,0,"+0.2"),NativeCombatNumbers.AimAlpha,1e-3)&&Near(Fade(shoulder,true,6,0,"+5"),NativeCombatNumbers.AimAlpha,1e-3),"Classic spawns my heals on the crosshair: dimmed");
            Check(Fade(shoulder,true,0,.9f,"+5")>.9f,"and full again once they rise clear");
            for(float pitch=-30;pitch<=45;pitch+=5)for(float age=0;age<NativeCombatNumbers.Life;age+=.05f)for(int lane=0;lane<NativeCombatNumbers.Lanes;lane++)Check(Fade(shoulder,false,pitch,age,"+0.25",lane)==1,"the shoulder rig never dims them (pitch "+pitch+", age "+age+", lane "+lane+")");
            Check(NativeCombatNumbers.AimFade("+5",25,800,450,800,450)==NativeCombatNumbers.AimAlpha&&NativeCombatNumbers.AimFade("+5",25,800,450-NativeCombatNumbers.AimClear-40,800,450)==1,"fade ends");
            float prev=0;for(float dy=0;dy<200;dy+=2){float f=NativeCombatNumbers.AimFade("+12",25,800,450-dy,800,450);Check(f>=prev-1e-6f,"the fade eases with distance");prev=f;}

            // 7. No per-frame allocations: once a value's label exists, counting, reading the envelope and merging my
            // first-person ticks into their (unlabelled) number allocate nothing.
            var z=new NativeHealReadout();for(int k=0;k<200;k++){z.Add(30+k/60f,.5f);_=z.Label;_=z.Alpha(30+k/60f,false);_=z.Lift(30+k/60f,false);}
            z.Clear();long before=GC.GetAllocatedBytesForCurrentThread();
            for(int k=0;k<200;k++){z.Add(30+k/60f,.5f);_=z.Label;_=z.Alpha(30+k/60f,false);_=z.Lift(30+k/60f,true);}
            long readoutBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(readoutBytes==0&&z.Label=="+100","the readout allocates nothing while counting to +100 ("+readoutBytes+" bytes)");
            var other=new NativeHealReadout();other.Add(1,76);var again=new NativeHealReadout();again.Add(2,75.6f);Check(other.Label=="+76"&&ReferenceEquals(other.Label,again.Label),"labels come from one cache");
            var big=new NativeHealReadout();big.Add(1,1234.4f);Check(big.Label=="+1234"&&ReferenceEquals(big.Label,big.Label),"a large total keeps its label until it changes");
            var merge=new NativeCombatNumbers();var one=new List<ArenaEvent>{Heal(1,40,1,.05f)};merge.Update(one,40,0,1,false,anchor,1);
            var tick2=new List<ArenaEvent>{new ArenaEvent()};_=merge.Heals.Label;before=GC.GetAllocatedBytesForCurrentThread();
            for(int k=1;k<9;k++){tick2[0]=Heal(1+k,40+k/60f,1,.05f);merge.Update(tick2,40+k/60f,0,1,false,anchor,1);}
            long mergeBytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(mergeBytes==0&&merge.Numbers.Count==1&&merge.Numbers[0].Readout,"my first-person ticks merge into one unlabelled number without allocating ("+mergeBytes+" bytes)");

            // 8. The Runtime routes them so: first person (the setting or the scope) diverts the view pilot's heals and skips
            // drawing any of them, its damage numbers keep the crosshair rule, third-person own heals fade at the aim point,
            // and the fight HUD draws the readout over the vitals in the heal colours with Reduced effects passed.
            var combat=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeCombatHud.cs"));
            Check(combat.Contains("private bool FirstPersonView=>prefs.firstPerson||presentation.FirstPerson;")&&combat.Contains("combatTrack.Update(arena.Events,arena.Time,arena.Generation,view,arena.Pve!=null,combatAnchor,FirstPersonView?view:0)"),"first person diverts the view pilot's heals");
            Check(combat.Contains("if(n.Readout||first&&n.Heal&&n.Target==view)continue;"),"no own heal number is drawn in first person");
            Check(combat.Contains("bool own=prefs.firstPerson&&n.Target==view;")&&combat.Contains("if(own)at=new Vector3(width*NativeCombatNumbers.OwnX(n.Lane),height*(1-NativeCombatNumbers.OwnY(age)),1);"),"own damage numbers keep their first-person spot");
            Check(combat.Contains("float fade=n.Heal&&n.Target==view?NativeCombatNumbers.AimFade(n.Label,numberSize,at.x,height-at.y,width/2,height/2):1;")&&combat.Contains("float alpha=NativeCombatNumbers.Alpha(age)*fade;"),"third-person own heals fade at the aim point; nothing else does");
            Check(fightHud.Contains("HealReadout(box);")&&fightHud.Contains("var heals=combatTrack.Heals;float t=arena.Time;bool reduced=prefs.reducedEffects;float a=heals.Alpha(t,reduced);")&&fightHud.Contains("NativeHealReadout.Place(box.x,box.y,box.width,heals.Lift(t,reduced),")&&fightHud.Contains("U.Hex(NativeCombatNumbers.HealColor,a)"),"the fight HUD draws the readout over the vitals");
            Console.WriteLine("PASS heal numbers: "+count+" checks (parity fixture replayed with "+replays+" first-person views, "+flagged+" own heals diverted, every other number identical; 60 Hz burst counted into one total; merge, rewind and envelope rules; "+cases+" window/scale/layout cases clear of the centre; Classic dimmed, shoulder untouched; zero allocations; Runtime routing)");
        }
    }
}
#endif
