using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Silent Kill's camera and effects (owner 2026-11-13: "Fangs we also need more camera work and the effects still need improvement",
    /// then "I mean for there intros"; docs/FANGS.md "The intro"). Presentation only: the entrance keeps its length, its name's moment and
    /// its beats. The camera has a shot to each beat (the cut to the strike on the blow itself, a whip landing with the step), stays out of
    /// the floor and the pilot, moves smoothly outside its cuts, holds still from the settle and keeps the pilot in frame; its jolts obey
    /// the player's Camera shake setting. The effects stay inside the entrance budget with the blades handed in, thin on the low tier,
    /// draw the same through a sink that knows venom and one that does not, and each beat has its own.
    /// </summary>
    internal static class FangsIntroChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Fangs intro: "+what);count++;}
        private class Sink:INativeEntranceFx,INativeEntranceProps
        {
            public int Count,Glows,Streaks,Smokes,Floor,Eyes,Stars,Fumes;public bool Broken;public readonly List<(WeaponShape shape,bool glow,Vector3 p,Vector3 s)> Pieces=new List<(WeaponShape,bool,Vector3,Vector3)>();
            protected void V(Vector3 v){if(!(float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)))Broken=true;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Count++;Glows++;V(at);V(color);if(profile==1&&size<.08f&&Math.Abs(at.Y-NativeFangsIntro.EyeHeight)<.05f)Eyes++;if(profile==1)Stars++;if(seed>=22840&&seed<22900||seed>=22970&&seed<22990){Fumes++;if(coverage!=0||profile!=1)Broken=true;}}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Count++;Streaks++;V(head);V(velocity);V(color);if(blur==1&&head.Y<NativeEntranceStage.FloorY+.06f)Floor++;}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Count++;Smokes++;V(at);}
            public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color){V(position);Pieces.Add((shape,glow,position,scale));}
            public void Reset(){Count=Glows=Streaks=Smokes=Floor=Eyes=Stars=Fumes=0;Pieces.Clear();}
        }
        private sealed class VenomSink:Sink,INativeEntranceVenom
        {
            public int Beads;
            public void Bead(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed){Count++;Beads++;V(head);V(segment);}
        }
        private static float AgeOf(NativeEntranceScript sc,float t){float lo=0,hi=sc.Length+1;for(int i=0;i<40;i++){float m=(lo+hi)*.5f;if(sc.Clock(m)<t)lo=m;else hi=m;}return hi;}
        private static float Body(Vector3 eye,Vector3 a,Vector3 b){var ab=b-a;float u=Math.Max(0,Math.Min(1,Vector3.Dot(eye-a,ab)/Math.Max(1e-6f,ab.LengthSquared())));var n=a+ab*u;var off=eye-n;return (float)Math.Sqrt(off.X*off.X+off.Z*off.Z+(off.Y*.35f)*(off.Y*.35f));}
        private static bool InFrame(NativeEntranceView v,Vector3 p,float aspect,out float x,out float y)
        {
            var f=Vector3.Normalize(v.Look-v.Eye);var right=Vector3.Normalize(Vector3.Cross(f,v.Up));var up=Vector3.Cross(right,f);var d=p-v.Eye;float z=Vector3.Dot(d,f);
            float tan=(float)Math.Tan(v.Fov*Math.PI/360);x=z>0?Vector3.Dot(d,right)/(z*tan*aspect):9;y=z>0?Vector3.Dot(d,up)/(z*tan):9;return z>.05f&&Math.Abs(x)<=1&&Math.Abs(y)<=1;
        }
        // The two fangs as a model might hold them: at the hips, swept fast through the flurry and the strike.
        private static void Feed(NativeFangsIntro fi,float t)
        {
            bool shown=fi.Solid(t,CapturedRig.RHand)>.5f;float fast=t>=NativeFangsIntro.FlurryAt&&t<NativeFangsIntro.Hero?1:0;
            for(int hand=0;hand<2;hand++)
            {
                float a=t*14*fast+hand*2.1f;var root=new Vector3((hand==0?-.25f:.25f)+.2f*fast*(float)Math.Sin(a),.95f+.25f*fast*(float)Math.Cos(a*.7f),.2f+.3f*fast*(float)Math.Cos(a));
                fi.Blade(t,hand,root,root+Vector3.Normalize(new Vector3((float)Math.Sin(a*1.3f),-.4f+.6f*fast*(float)Math.Cos(a),(float)Math.Cos(a*1.3f)+.2f))*.34f,shown);
            }
        }
        public static void Run(string root)
        {
            count=0;var fi=new NativeFangsIntro();fi.Prepare();
            // ---- the entrance itself is as it was: its length, its name's moment, its beats ----
            Check(NativeFangsIntro.Total==7f&&NativeFangsIntro.CardAt==5.2f&&NativeFangsIntro.End==6.7f&&Math.Abs(NativeIntroRules.Duration(NativeFangs.Entrance)-7)<1e-6&&Math.Abs(NativeIntroRules.NameAt(NativeFangs.Entrance)-5.2)<1e-6&&fi.Length==7f&&fi.SettleAt==6.7f,"its length, its name's moment and its settle");
            Check(fi.Hit==NativeFangsIntro.StrikeAt&&NativeFangsIntro.FormAt<NativeFangsIntro.StepAt&&NativeFangsIntro.StepAt<NativeFangsIntro.ArriveAt&&NativeFangsIntro.ArriveAt<NativeFangsIntro.FlurryAt&&NativeFangsIntro.FlurryAt<NativeFangsIntro.StrikeAt&&NativeFangsIntro.StrikeAt<NativeFangsIntro.Hero&&NativeFangsIntro.Hero<NativeFangsIntro.CardAt,"its beats in order, the slow motion on the strike");
            float back=fi.Hit+fi.SlowHold+fi.SlowCatchUp;Check(back<=NativeFangsIntro.CardAt,"the slow motion is caught up before the name");
            // ---- the camera: a shot to each beat ----
            var shots=fi.Shots;
            Check(shots.Length>=8&&shots[0].At==0&&shots.Zip(shots.Skip(1),(a,b)=>a.At<b.At).All(x=>x)&&shots[^1].At==NativeFangsIntro.CardAt&&shots[^1].In==NativeEntranceCut.Cut,"its shots in time order, from the first frame to the name's own");
            Check(shots.Any(s=>s.At==NativeFangsIntro.FormAt&&s.In==NativeEntranceCut.Cut),"a shot of its own as it forms");
            Check(shots.Any(s=>s.In==NativeEntranceCut.Cut&&s.At<NativeFangsIntro.StepAt&&s.At>=NativeFangsIntro.StepAt-.4f),"a shot set before the step, so the step is seen whole");
            Check(shots.Count(s=>s.In==NativeEntranceCut.Whip)==1&&shots.Any(s=>s.In==NativeEntranceCut.Whip&&s.At>=NativeFangsIntro.ArriveAt&&s.At<=NativeFangsIntro.FlurryAt&&s.InTime<=.25f),"one whip, landing with the step");
            Check(shots.Any(s=>s.In==NativeEntranceCut.Cut&&s.At==NativeFangsIntro.LeapAt&&s.Follow<0&&s.Eye0.Y<.6f&&s.Look0.Y>1.3f&&s.Fov0>=46)&&NativeFangsIntro.Way(NativeFangsIntro.LeapAt).Y<.15f&&NativeFangsIntro.Way(NativeFangsIntro.LeapAt+.25f).Y>1f,"a cut as it leaves the floor: low, wide and looking up, the leap held in a still frame");
            Check(shots.Any(s=>s.In==NativeEntranceCut.Cut&&s.At<NativeFangsIntro.StrikeAt-.2f&&s.At>NativeFangsIntro.StrikeAt-.7f&&Math.Abs(s.Eye0.X)>2.5f&&Math.Abs(s.Eye0.Z)<.6f),"the strike's lunge in profile");
            Check(shots.Any(s=>s.In==NativeEntranceCut.Cut&&s.At==NativeFangsIntro.StrikeAt&&s.Eye0.Y>2.5f&&s.Eye2.Y>s.Eye0.Y&&s.Fov1>s.Fov0&&Math.Abs(s.Look0.X-NativeFangsIntro.StrikeSpot.X)<.5f&&Math.Abs(s.Look0.Z-NativeFangsIntro.StrikeSpot.Z)<.5f),"the cut to the strike is on the blow, from above where it lands, lifting and widening over the bloom");
            Check(shots.Any(s=>s.In==NativeEntranceCut.Blend&&s.At>NativeFangsIntro.StrikeAt&&s.At+s.InTime<=NativeFangsIntro.CardAt+1e-4f),"the camera comes down from the strike to the hero in one move, done before the name's cut");
            Check(shots.All(s=>Math.Abs(s.Roll0)<=.4f&&Math.Abs(s.Roll1)<=.4f&&s.Fov0>=28&&s.Fov0<=60&&s.Fov1>=28&&s.Fov1<=60)&&shots[^1].Roll0==0&&shots[^1].Roll1==0&&shots[^2].Roll0==0&&shots[^2].Roll1==0,"tilts and lenses stay moderate, and the hero and the name are level");
            // Clear of the floor and the pilot, smooth outside the cuts, the whip and the hit-stop, still from the settle, the pilot in frame.
            const float dt=1/240f,aspect=16/9f;var cuts=new List<(float from,float to)>{(fi.Hit-2.5f*dt,fi.Hit+2.5f*dt)};
            for(int k=1;k<shots.Length;k++){var sh=shots[k];float at=AgeOf(fi,sh.At);if(sh.In==NativeEntranceCut.Cut)cuts.Add((at-2.5f*dt,at+2.5f*dt));else if(sh.In==NativeEntranceCut.Whip)cuts.Add((AgeOf(fi,sh.At-sh.InTime*.5f)-2.5f*dt,AgeOf(fi,sh.At+sh.InTime*.5f)+2.5f*dt));}
            fi.Shake=false;NativeEntranceView a=default,b=default,c=default;double worstAccel=0,worstTurn=0;string accelAt="",turnAt="";int framed=0,visible=0;
            for(int i=0;i*dt<=fi.Length;i++)
            {
                float age=i*dt,t=fi.Clock(Math.Min(age,fi.SettleAt));a=b;b=c;fi.Camera(age,out c);
                Check(float.IsFinite(c.Eye.X)&&float.IsFinite(c.Eye.Y)&&float.IsFinite(c.Eye.Z)&&float.IsFinite(c.Look.X)&&float.IsFinite(c.Up.Y)&&c.Fov>=28&&c.Fov<=60,"a whole camera at "+age.ToString("0.000"));
                Check(c.Eye.Y>=NativeEntranceStage.FloorY+NativeEntranceCamera.EyeClearance-1e-3f,"camera under the floor at "+age.ToString("0.000"));
                var head=fi.Anchor(NativeEntranceAnchor.Head,t);var pelvis=fi.Anchor(NativeEntranceAnchor.Pelvis,t);var feet=(fi.Anchor(NativeEntranceAnchor.LeftFoot,t)+fi.Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;
                float body=Math.Min(Body(c.Eye,head,pelvis),Body(c.Eye,pelvis,feet));Check(body>=NativeEntranceCamera.BodyClearance-.02f,"camera inside the pilot at "+age.ToString("0.000")+" ("+body.ToString("0.000")+" m)");
                if(age>=fi.SettleAt)Check(c.Eye==b.Eye&&c.Look==b.Look&&c.Fov==b.Fov||age<fi.SettleAt+dt,"camera still moving after it settled at "+age.ToString("0.000"));
                if(i>=2&&!cuts.Any(w=>age-2*dt>=w.from&&age-2*dt<=w.to||age>=w.from&&age<=w.to))
                {
                    double acc=((c.Eye-2*b.Eye+a.Eye)/(dt*dt)).Length();var fa=Vector3.Normalize(a.Look-a.Eye);var fb=Vector3.Normalize(b.Look-b.Eye);var fc=Vector3.Normalize(c.Look-c.Eye);double turn=((fc-2*fb+fa)/(dt*dt)).Length();
                    if(acc>worstAccel){worstAccel=acc;accelAt=age.ToString("0.000");}if(turn>worstTurn){worstTurn=turn;turnAt=age.ToString("0.000");}
                    Check(acc<=80,"camera jerks at "+age.ToString("0.000")+" ("+acc.ToString("0")+" m/s^2)");Check(turn<=60,"camera turn jerks at "+age.ToString("0.000")+" ("+turn.ToString("0")+" rad/s^2)");
                }
                if(fi.Solid(t,CapturedRig.Chest)>.5f&&i%4==0)
                {
                    visible++;bool inFrame=InFrame(c,fi.Anchor(NativeEntranceAnchor.Chest,t),aspect,out _,out _);if(inFrame)framed++;
                    if(age>=NativeFangsIntro.CardAt){Check(inFrame,"loses the pilot under the name at "+age.ToString("0.000"));InFrame(c,head,aspect,out _,out float hy);Check(hy>-.3f&&hy<.92f,"the hero frame puts the head at "+hy.ToString("0.00"));}
                }
            }
            Check(framed>=visible*.9,"the pilot is in frame only "+framed+" of "+visible+" samples");
            // The leap whole in its frame: head and feet both inside it from the launch to the landing, and the lunge's whole way in the profile.
            for(float t=NativeFangsIntro.LeapAt+.02f;t<NativeFangsIntro.StrikeAt-.02f;t+=1/60f)
            {
                fi.Camera(AgeOf(fi,t),out var v);var head=fi.Anchor(NativeEntranceAnchor.Head,t);var feet=(fi.Anchor(NativeEntranceAnchor.LeftFoot,t)+fi.Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;
                bool headIn=InFrame(v,head,aspect,out _,out float hy),feetIn=InFrame(v,feet,aspect,out _,out float fy);Check(headIn&&feetIn&&Math.Abs(hy)<.9f&&Math.Abs(fy)<.95f,"the leap or the lunge leaves the frame at "+t.ToString("0.00")+" (head "+hy.ToString("0.00")+", feet "+fy.ToString("0.00")+")");
            }
            // With its jolts on: still clear of the floor and the pilot, and still from the settle.
            fi.Shake=true;
            for(int i=0;i*dt<=fi.Length;i++)
            {
                float age=i*dt,t=fi.Clock(Math.Min(age,fi.SettleAt));b=c;fi.Camera(age,out c);var head=fi.Anchor(NativeEntranceAnchor.Head,t);var pelvis=fi.Anchor(NativeEntranceAnchor.Pelvis,t);var feet=(fi.Anchor(NativeEntranceAnchor.LeftFoot,t)+fi.Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;
                Check(c.Eye.Y>=NativeEntranceStage.FloorY+NativeEntranceCamera.EyeClearance-.08f&&Math.Min(Body(c.Eye,head,pelvis),Body(c.Eye,pelvis,feet))>=NativeEntranceCamera.BodyClearance-.1f,"a jolt puts the camera in the floor or the pilot at "+age.ToString("0.000"));
                if(age>=fi.SettleAt+dt)Check(c.Eye==b.Eye&&c.Look==b.Look,"a jolt still moves the camera after it settled at "+age.ToString("0.000"));
            }
            fi.Shake=false;
            // The same age gives the same camera whatever was asked before.
            {
                var ages=Enumerable.Range(0,141).Select(i=>i*.05f).ToArray();var first=new Dictionary<float,NativeEntranceView>();var rng=new Random(1113);
                foreach(var pass in new[]{ages,ages.Reverse().ToArray(),ages.OrderBy(_=>rng.Next()).ToArray()})foreach(float age in pass){fi.Camera(age,out var v);if(!first.TryGetValue(age,out var was)){first[age]=v;continue;}Check(was.Eye==v.Eye&&was.Look==v.Look&&was.Up==v.Up&&was.Fov==v.Fov,"the camera differs at "+age+" s asked in another order");}
            }
            // ---- its jolts obey the Camera shake setting ----
            {
                float[] beats={NativeFangsIntro.StrikeAt+.04f,NativeFangsIntro.ArriveAt+.03f,NativeFangsIntro.FlurryAt+.47f,NativeFangsIntro.FlurryAt+.87f,NativeFangsIntro.FlurryAt+1.47f};
                fi.Shake=true;Check(beats.All(t=>fi.CameraJolt(t).Length()>1e-4f)&&fi.CameraJolt(1f)==Vector3.Zero&&fi.CameraJolt(NativeFangsIntro.End)==Vector3.Zero,"a jolt on the strike, the step's arrival and each cut, none between");
                float worst=0;for(float t=0;t<7;t+=1/240f)worst=Math.Max(worst,fi.CameraJolt(t).Length());Check(worst<=.075f,"the jolts stay small ("+worst.ToString("0.000")+" m)");
                float age=AgeOf(fi,NativeFangsIntro.StrikeAt+.04f);fi.Camera(age,out var on);fi.Shake=false;fi.Camera(age,out var off);
                Check(beats.All(t=>fi.CameraJolt(t)==Vector3.Zero)&&(on.Eye-off.Eye).Length()>1e-4f&&on.Look==off.Look&&on.Fov==off.Fov,"with Camera shake off the entrance's camera takes no jolt, and is otherwise the same");
                fi.Shake=true;Check(new NativeFangsIntro().Shake,"on by default");
                string intros=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeIntros.cs")),game=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/ConversionGame.cs"));
                int set=intros.IndexOf("if(script is NativeFangsIntro silent)silent.Shake=CameraShake;",StringComparison.Ordinal),cam=intros.IndexOf("script.Camera(age,out var view);",StringComparison.Ordinal);
                Check(set>0&&cam>set&&game.Contains("presentation.CameraShake=prefs.cameraShake;"),"the runtime hands the player's Camera shake setting to the entrance before it asks for the camera");
            }
            // ---- the look: condensing ----
            Check(fi.Solid(0,CapturedRig.Chest)==0&&fi.Solid(NativeFangsIntro.FormAt,CapturedRig.Chest)==0&&fi.Solid(NativeFangsIntro.FormAt+NativeFangsIntro.FormFade*.5f,CapturedRig.Chest) is >.3f and <.7f&&fi.Solid(NativeFangsIntro.FormAt+NativeFangsIntro.FormFade,CapturedRig.Head)==1&&fi.Solid(NativeFangsIntro.StepAt-.01f,CapturedRig.Chest)==1
                &&fi.Solid((NativeFangsIntro.StepAt+NativeFangsIntro.ArriveAt)*.5f,CapturedRig.Chest)==0&&fi.Solid(NativeFangsIntro.ArriveAt+.03f,CapturedRig.Chest)==1&&fi.Solid(6,CapturedRig.RHand)==1,"unseen, condensed over the form, gone through the step, whole after it");
            Check(fi.Ghosts(NativeFangsIntro.StepAt+.05f,new NativeEntranceGhost[8])==0,"no afterimage on the step (the keyed stance's silhouette is not the crouch the model is in)");
            // ---- the effects ----
            // A playthrough at 60 frames a second with the blades handed in: whole, inside the entrance budget, thinner on the low tier, and
            // the same number of draws through a sink that knows venom as through one that does not.
            {
                int peak=0,peakLow=0;float peakAt=0;long sum=0,sumLow=0;bool broken=false,same=true;int fumes=0,beads=0;
                var hi=new NativeFangsIntro();var lo=new NativeFangsIntro();var plain=new NativeFangsIntro();var s=new VenomSink();var l=new VenomSink();var p=new Sink();var lights=new NativeEntranceLights();
                for(float age=0;age<=7;age+=1/60f)
                {
                    float t=hi.Clock(age);Feed(hi,t);Feed(lo,t);Feed(plain,t);
                    s.Reset();hi.Effects(t,s,1);if(s.Count>peak){peak=s.Count;peakAt=age;}sum+=s.Count;broken|=s.Broken;fumes+=s.Fumes;beads+=s.Beads;s.Beads=0;
                    l.Reset();lo.Effects(t,l,.3f);peakLow=Math.Max(peakLow,l.Count);sumLow+=l.Count;broken|=l.Broken;l.Beads=0;
                    p.Reset();plain.Effects(t,p,1);same&=p.Count==s.Count;broken|=p.Broken;
                    s.Reset();hi.Props(t,s,1);broken|=s.Broken;
                    foreach(var piece in s.Pieces){if(!piece.glow||piece.p.Y>NativeEntranceStage.FloorY+.25f)continue;bool flat=piece.s.Y<.12f&&Math.Min(piece.s.X,piece.s.Z)>.25f;Check(!(flat||piece.shape==WeaponShape.Ring),"no glowing ring or disc on the floor at "+age.ToString("0.00"));}
                    hi.Lights(t,lights);Check(lights.Points<=NativeStageLighting.Max&&lights.Directionals<=NativeEntranceLights.MaxDirectional,"its lights fit the stage at "+age.ToString("0.00"));
                }
                Check(!broken,"every effect whole");Check(peak<=600,"at most 600 effect particles a frame ("+peak+" at "+peakAt.ToString("0.00")+")");Check(sumLow<sum*.6,"the low tier thins them ("+sumLow+" of "+sum+")");
                Check(same,"a sink without venom draws as many");Check(fumes>200&&beads>100,"the venom's fumes are drawn as light, and its drops go to a sink that knows them ("+fumes+", "+beads+")");
                Console.WriteLine("PASS Fangs intro effects: at most "+peak+" particles a frame (at "+peakAt.ToString("0.00")+" s; "+peakLow+" on the low tier, "+(100*sumLow/Math.Max(1,sum))+"% of the draws)");
            }
            // Each beat's own: the eyes; the glints up the fangs when they are handed in; the strike's veins over the floor and its bloom.
            {
                VenomSink At(float t,bool feed,float q=1){var intro=new NativeFangsIntro();if(feed)for(int i=0;i<30;i++)Feed(intro,t-(29-i)/60f);var r=new VenomSink();intro.Effects(t,r,q);return r;}
                Check(At(.25f,false).Eyes==2&&At(NativeFangsIntro.FormAt+NativeFangsIntro.FormFade+.05f,false).Eyes==0&&At(3,false).Eyes==0,"two eyes in the dark, gone once it has formed");
                float glintAt=NativeFangsIntro.CardAt+.25f;Check(At(glintAt,true).Stars>At(glintAt,false).Stars&&At(NativeFangsIntro.FormAt+NativeFangsIntro.FormFade+.2f,true).Stars>At(NativeFangsIntro.FormAt+NativeFangsIntro.FormFade+.2f,false).Stars
                    &&At(NativeFangsIntro.ArriveAt+.15f,true).Stars>At(NativeFangsIntro.ArriveAt+.15f,false).Stars,"a glint up the fangs as it forms, as it lands from the step and as the name lands, only on blades handed in");
                var before=At(NativeFangsIntro.StrikeAt-.05f,false);var bloom=At(NativeFangsIntro.StrikeAt+.45f,false);var late=At(NativeFangsIntro.StrikeAt+NativeFangsIntro.BloomLife+.05f,false);
                Check(before.Fumes==0&&bloom.Fumes>=NativeFangsIntro.BloomPetals*4&&late.Fumes==0&&At(NativeFangsIntro.CardAt+.01f,false).Fumes==0&&NativeFangsIntro.StrikeAt+NativeFangsIntro.BloomGone<=NativeFangsIntro.CardAt+1e-4f,"the bloom: petals of fumes from the strike, gone by the name ("+bloom.Fumes+")");
                Check(before.Floor==0&&bloom.Floor>=9*NativeFangsIntro.VeinSegments&&At(NativeFangsIntro.StrikeAt+.45f,false,.3f).Floor<bloom.Floor,"veins of venom light over the floor from the strike, fewer on the low tier ("+bloom.Floor+")");
                Check(bloom.Beads>10&&At(NativeFangsIntro.FlurryAt+.9f,true).Beads>0,"venom splashed by the strike and flung by the flurry, as drops");
            }
            Console.WriteLine("PASS Fangs intro: "+count+" checks (camera: at most "+worstAccel.ToString("0")+" m/s^2 at "+accelAt+", "+worstTurn.ToString("0")+" rad/s^2 at "+turnAt+"; the pilot in frame in "+framed+" of "+visible+" samples)");
        }
    }
}
