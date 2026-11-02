using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;
using Pol=ChipArena.UnityConversion.NativeDeployPresentation.Polarity;

// The Deployment Station's slot sparks (Core NativeDeploySparks, drawn by NativeDeployStage): nothing before a roll, for a late
// joiner or after it; the grinder through the spin and brake; each destination's landing burst in its colour; each mutator
// card's landing by polarity (a rising gold-green fountain and glints, a falling red short circuit with arcs, a cycling violet
// vortex) and the sweep when all three agree; reduced effects a subset with no arcs or stutter; the cabinets' kick; the sound
// cues heard exactly once; and the stage drawing them as camera-facing additive quads after its own items.
internal static class DeploySparkChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Deploy sparks: "+what);count++;}
    private static readonly NativeDeploySparks.Spark[] buf=new NativeDeploySparks.Spark[NativeDeploySparks.Limit];
    private static readonly Matrix4x4 RootI=NativeDeployModels.Root(NativeDeployPresentation.InstanceMachineAt,NativeDeployPresentation.InstanceYaw);
    private static readonly Matrix4x4 RootM=NativeDeployModels.Root(NativeDeployPresentation.MutatorMachineAt,NativeDeployPresentation.MutatorYaw);
    private static readonly int[] Colors={0x8dff5a,0x3fd8ff,0xff8a2a};
    private static float Stop(int r)=>NativeDeployPresentation.StopTime(r);
    private static NativeDeploySparks.Spark[] I(float t,bool reduced=false){int n=NativeDeploySparks.Instance(buf,0,t,RootI,Colors,3,reduced);return buf.Take(n).ToArray();}
    private static NativeDeploySparks.Spark[] M(float t,Pol[] cards,int reels,bool reduced=false){int n=NativeDeploySparks.Mutator(buf,0,t,RootM,cards,reels,reduced);return buf.Take(n).ToArray();}
    private static Vector3 Local(Vector3 world,Matrix4x4 root){Matrix4x4.Invert(root,out var inv);return Vector3.Transform(world,inv);}
    private static Vector3 Rgb(Vector4 c)=>new Vector3(c.X,c.Y,c.Z);
    public static void Run(string root)
    {
        count=0;Quiet();Grinder();Landing();Cards();Sweep();Reduced();Kick();Sound();Stage();Wiring(root);
        Console.WriteLine("PASS deploy sparks: "+count+" checks (none before, after or for a late joiner; the grinder through the brake; landing bursts in the destination's colour; "+
            "boon fountains, bane short circuits, chaos vortices and the sweep; reduced a subset without arcs or stutter; the kick; cues heard once; drawn as camera-facing quads)");
    }
    private static void Quiet()
    {
        var all=new[]{Pol.Boon,Pol.Bane,Pol.Chaos};
        foreach(float t in new[]{float.NaN,float.NegativeInfinity,-1,-.01f,NativeDeploySparks.End,NativeDeploySparks.End+5,30})
            Check(I(t).Length==0&&M(t,all,3).Length==0,"no sparks at "+t);
        Check(NativeDeploySparks.Instance(buf,0,1,RootI,Colors,0,false)==0&&NativeDeploySparks.Mutator(buf,0,1,RootM,all,0,false)==0&&NativeDeploySparks.Mutator(buf,0,1,RootM,null,3,false)==0,"no reels, no sparks");
        Check(NativeDeploySparks.Instance(buf,7,1.2f,RootI,Colors,3,false)>=7,"sparks add after the count given");
        var small=new NativeDeploySparks.Spark[5];Check(NativeDeploySparks.Instance(small,0,Stop(0)+.08f,RootI,Colors,3,false)==5,"never past the buffer");
        var a=I(1.234f);var b=I(1.234f);Check(a.Length>0&&a.Length==b.Length&&a.Zip(b,(x,y)=>x.At==y.At&&x.Axis==y.Axis&&x.Color==y.Color).All(x=>x),"the same roll time draws the same sparks");
    }
    private static void Grinder()
    {
        for(int r=0;r<3;r++)foreach(bool mut in new[]{false,true})
        {
            float stop=Stop(r),brake=stop-NativeDeployPresentation.Decel;
            Check(NativeDeploySparks.GrindRate(r,-.1f,mut)==0&&NativeDeploySparks.GrindRate(r,float.NaN,mut)==0&&NativeDeploySparks.GrindRate(r,stop,mut)==0&&NativeDeploySparks.GrindRate(r,stop+1,mut)==0,"no grinding outside the spin ("+r+")");
            Check(NativeDeploySparks.GrindRate(r,.05f,mut)<NativeDeploySparks.GrindRate(r,.25f,mut),"the grinder rises with the spin-up");
            float cruise=NativeDeploySparks.GrindRate(r,brake-.05f,mut),bite=NativeDeploySparks.GrindRate(r,brake+NativeDeployPresentation.Decel*.5f,mut);
            Check(cruise>0&&bite>2.5f*cruise,"the brake bites harder than the cruise ("+r+", "+mut+")");
            for(float t=0;t<stop;t+=.01f)Check(NativeDeploySparks.GrindRate(r,t,mut)*NativeDeploySparks.Slot<=1,"a tick emits at most one spark");
        }
        // through a roll at 60 fps: a stream at cruise, more as each brake bites
        float Avg(float t0,float t1,Func<NativeDeploySparks.Spark,bool> which){int n=0,k=0;for(float t=t0;t<t1;t+=1/60f){n+=I(t).Count(which);k++;}return n/(float)k;}
        bool G(NativeDeploySparks.Spark s)=>s.Kind==NativeDeploySparks.Kind.Grinder;
        float cruiseAvg=Avg(.6f,.95f,G),brakeAvg=Avg(Stop(0)-.4f,Stop(0)-.15f,G);
        Check(cruiseAvg>=3&&cruiseAvg<=30,"a stream of grinder sparks at cruise ("+cruiseAvg+")");Check(brakeAvg>1.4f*cruiseAvg,"more grinder sparks as the first brake bites ("+brakeAvg+" vs "+cruiseAvg+")");
        foreach(var s in I(.8f).Where(G))
        {
            var l=Local(s.At,RootI);Check(l.Z<NativeDeploySparks.LipZ+.01f&&l.Y<NativeDeploySparks.WindowY&&Math.Abs(l.X)<1.6f,"grinder sparks fly off the lower lips toward the room");
            Check(Rgb(s.Color).X>=Rgb(s.Color).Z,"grinder sparks are hot (orange, not blue)");
        }
        var dial=M(.8f,new[]{Pol.Boon,Pol.Bane,Pol.Chaos},3).Where(G).ToArray();Check(dial.Length>0&&dial.All(s=>s.Color.Z>s.Color.X),"the dials crackle blue-white as they spin");
    }
    private static void Landing()
    {
        int peak=0;for(float t=0;t<NativeDeploySparks.End;t+=1/60f){var s=I(t);peak=Math.Max(peak,s.Length);foreach(var x in s)Check(float.IsFinite(x.At.X+x.At.Y+x.At.Z+x.Axis.X+x.Axis.Y+x.Axis.Z)&&x.Width>0&&x.Color.X>=0&&x.Color.Y>=0&&x.Color.Z>=0,"finite sparks");}
        Check(peak>0&&peak<NativeDeploySparks.Limit*.6f,"the instance machine's busiest frame fits ("+peak+")");
        for(int r=0;r<3;r++)
        {
            var window=Vector3.Transform(new Vector3(NativeDeployModels.ReelAt(r).X,NativeDeploySparks.WindowY,NativeDeploySparks.LipZ),RootI);
            // reel r's own bursts: the first r+1 reels' sparks less the first r's (each reel's sparks are its own)
            NativeDeploySparks.Spark[] Own(float t){int a=NativeDeploySparks.Instance(buf,0,t,RootI,Colors,r,false);var before=buf.Take(a).Where(x=>x.Kind==NativeDeploySparks.Kind.Burst).Select(x=>x.At).ToList();
                int b=NativeDeploySparks.Instance(buf,0,t,RootI,Colors,r+1,false);return buf.Take(b).Where(x=>x.Kind==NativeDeploySparks.Kind.Burst&&!before.Contains(x.At)).ToArray();}
            Check(Own(Stop(r)-.01f).Length==0,"reel "+r+" bursts only as it lands");
            var burst=Own(Stop(r)+.1f);Check(burst.Length>=8&&burst.All(x=>Vector3.Distance(x.At,window)<.6f),"reel "+r+" bursts off its window as it lands");
            Check(I(Stop(r)+.04f).Count(s=>s.Kind==NativeDeploySparks.Kind.Flash&&Vector3.Distance(s.At,window)<.1f)==1,"reel "+r+" flashes its window");
            var avg=Own(Stop(r)+.35f).Aggregate(Vector3.Zero,(a,s)=>a+Vector3.Normalize(Rgb(s.Color)+new Vector3(1e-6f)));var tone=WeaponPaint.Linear(Colors[r]);
            Check(Vector3.Dot(Vector3.Normalize(avg),Vector3.Normalize(new Vector3(tone.X,tone.Y,tone.Z)))>.9f,"reel "+r+"'s burst takes its destination's colour");
        }
        Check(!I(Stop(2)+.5f).Any(s=>s.Kind==NativeDeploySparks.Kind.Flash),"the flashes are brief");
    }
    private static void Cards()
    {
        float t0=Stop(0);var card=NativeDeployModels.CardAt(0);var o=Vector3.Transform(new Vector3(card.X,card.Y,NativeDeploySparks.DomeZ),RootM);
        foreach(var p in new[]{Pol.Boon,Pol.Bane,Pol.Chaos,Pol.Unknown})
        {
            var cards=new[]{p,Pol.Unknown,Pol.Unknown};bool B(NativeDeploySparks.Spark s)=>s.Kind==NativeDeploySparks.Kind.Burst;
            Check(M(t0-.01f,cards,1).Count(B)==0,p+": no burst before the card lands");
            Check(M(t0+.05f,cards,1).Count(s=>s.Kind==NativeDeploySparks.Kind.Flash)==1,p+": the card flashes as it lands");
            var at=M(t0+.35f,cards,1).Where(B).ToArray();Check(at.Length>=(p==Pol.Unknown?4:10),p+": a landing burst ("+at.Length+")");
            var c=at.Aggregate(Vector3.Zero,(a,s)=>a+Rgb(s.Color));float my=at.Average(s=>s.At.Y);
            switch(p)
            {
                case Pol.Boon:Check(c.Y>c.Z&&c.Y>.4f*c.X,"a boon burns gold and green");Check(my>o.Y+.3f,"a boon's fountain rises");break;
                case Pol.Bane:Check(c.X>2*c.Y&&c.X>2*c.Z,"a bane burns red");Check(my<o.Y-.2f,"a bane's sparks fall hard");break;
                case Pol.Chaos:
                    Check(c.Z>c.Y,"chaos burns violet");
                    int oct=at.Select(s=>{var l=Local(s.At,RootM)-new Vector3(card.X,card.Y,0);return (int)Math.Floor((Math.Atan2(l.Y,l.X)+Math.PI)/(Math.PI/4))%8;}).Distinct().Count();
                    Check(oct>=6,"chaos spins out all round the card ("+oct+" of 8)");break;
            }
            bool arcs=false,glints=false;for(float t=t0;t<t0+1.2f;t+=1/60f){var s=M(t,cards,1);arcs|=s.Any(x=>x.Kind==NativeDeploySparks.Kind.Arc);glints|=s.Any(x=>x.Kind==NativeDeploySparks.Kind.Glint);}
            Check(arcs==(p==Pol.Bane),p+": arcs only for a bane");Check(glints==(p==Pol.Boon),p+": glints only for a boon");
        }
        // a bane's flash stutters (full, then cut), and a reduced one only fades
        float F(float t,bool reduced)=>M(t,new[]{Pol.Bane},1,reduced).Where(s=>s.Kind==NativeDeploySparks.Kind.Flash).Sum(s=>s.Color.X);
        Check(F(t0+.06f,false)<.5f*F(t0+.02f,false)&&F(t0+.09f,false)>F(t0+.06f,false),"a bane's flash stutters");
        float last=float.MaxValue;for(float t=t0+.005f;t<t0+.4f;t+=.01f){float v=F(t,true);Check(v<=last+1e-4f,"a reduced bane's flash only fades");last=v;}
    }
    private static void Sweep()
    {
        Check(NativeDeploySparks.Sweep(new[]{Pol.Boon,Pol.Boon,Pol.Boon},3,out var p)&&p==Pol.Boon,"three boons sweep");
        Check(NativeDeploySparks.Sweep(new[]{Pol.Bane,Pol.Bane,Pol.Bane},3,out p)&&p==Pol.Bane&&NativeDeploySparks.Sweep(new[]{Pol.Chaos,Pol.Chaos,Pol.Chaos},3,out p)&&p==Pol.Chaos,"three banes or three chaos sweep");
        Check(!NativeDeploySparks.Sweep(new[]{Pol.Boon,Pol.Boon,Pol.Bane},3,out _)&&!NativeDeploySparks.Sweep(new[]{Pol.Unknown,Pol.Unknown,Pol.Unknown},3,out _)&&!NativeDeploySparks.Sweep(new[]{Pol.Boon,Pol.Boon,Pol.Boon},2,out _)&&!NativeDeploySparks.Sweep(null,3,out _),"mixed, unknown or fewer cards do not");
        float t=NativeDeploySparks.SweepAt+.3f,sign=Vector3.Transform(new Vector3(0,NativeDeploySparks.SignTop,NativeDeploySparks.SignZ),RootM).Y;
        Check(M(t,new[]{Pol.Boon,Pol.Boon,Pol.Boon},3).Count(s=>s.At.Y>sign+.05f)>=10,"three boons: the sign erupts");
        Check(M(t,new[]{Pol.Bane,Pol.Chaos,Pol.Boon},3).Count(s=>s.At.Y>sign+.05f)==0,"mixed cards: the sign stays quiet");
        Check(M(NativeDeploySparks.SweepAt+.1f,new[]{Pol.Bane,Pol.Bane,Pol.Bane},3).Count(s=>s.Kind==NativeDeploySparks.Kind.Arc&&Local(s.At,RootM).Z>.4f)>0,"three banes: arcs crackle over the sign");
        Check(M(NativeDeploySparks.SweepAt+.05f,new[]{Pol.Chaos,Pol.Chaos,Pol.Chaos},3).Count(s=>s.Kind==NativeDeploySparks.Kind.Flash&&Local(s.At,RootM).Z>.4f)==1,"three chaos: the sign flashes");
        int peak=0;foreach(var cards in new[]{new[]{Pol.Boon,Pol.Boon,Pol.Boon},new[]{Pol.Bane,Pol.Bane,Pol.Bane},new[]{Pol.Chaos,Pol.Chaos,Pol.Chaos}})for(float x=0;x<NativeDeploySparks.End;x+=1/60f)peak=Math.Max(peak,M(x,cards,3).Length);
        Check(peak<NativeDeploySparks.Limit,"the mutator machine's busiest frame fits ("+peak+")");
    }
    private static void Reduced()
    {
        var scenes=new Func<float,bool,NativeDeploySparks.Spark[]>[]{(t,r)=>I(t,r),(t,r)=>M(t,new[]{Pol.Boon,Pol.Boon,Pol.Boon},3,r),(t,r)=>M(t,new[]{Pol.Bane,Pol.Bane,Pol.Bane},3,r),(t,r)=>M(t,new[]{Pol.Chaos,Pol.Bane,Pol.Boon},3,r)};
        foreach(var scene in scenes)
        {
            int full=0,less=0;
            for(float t=0;t<NativeDeploySparks.End;t+=1/60f){var a=scene(t,false);var b=scene(t,true);full+=a.Length;less+=b.Length;Check(b.Length<=a.Length,"reduced draws no more");Check(!b.Any(s=>s.Kind==NativeDeploySparks.Kind.Arc),"reduced draws no arcs");}
            Check(full>0&&less<.5f*full,"reduced draws about a third ("+less+" of "+full+")");
        }
    }
    private static void Kick()
    {
        Check(NativeDeploySparks.Kick(0,.8f,null,3,false)==1&&NativeDeploySparks.Kick(0,float.NaN,null,3,false)==1&&NativeDeploySparks.Kick(0,NativeDeploySparks.End,null,3,false)==1,"no kick while spinning, unknown or after");
        Check(NativeDeploySparks.Kick(0,Stop(1)+.05f,null,3,false)>1.3f,"a destination lands with a thump");
        float t0=Stop(0);
        Check(NativeDeploySparks.Kick(1,t0+.05f,new[]{Pol.Boon},1,false)>1.5f,"a boon lifts the cabinet");
        Check(NativeDeploySparks.Kick(1,t0+.01f,new[]{Pol.Bane},1,false)<.5f&&NativeDeploySparks.Kick(1,t0+.05f,new[]{Pol.Bane},1,false)>1,"a bane stutters the cabinet");
        float last=0;for(float t=t0+.005f;t<t0+.6f;t+=.01f){float k=NativeDeploySparks.Kick(1,t,new[]{Pol.Bane},1,true);Check(k<1&&k>=.6f&&k>=last-1e-4f,"a reduced bane dips and recovers without strobing");last=k;}
        Check(NativeDeploySparks.Kick(1,NativeDeploySparks.SweepAt+.05f,new[]{Pol.Boon,Pol.Boon,Pol.Boon},3,false)>2,"three boons light the machine up");
        for(float t=0;t<NativeDeploySparks.End;t+=.01f)foreach(var cards in new[]{new[]{Pol.Bane,Pol.Bane,Pol.Bane},new[]{Pol.Chaos,Pol.Chaos,Pol.Chaos}})Check(NativeDeploySparks.Kick(1,t,cards,3,false)>=.1f,"the kick never blacks out");
    }
    private static void Sound()
    {
        var sched=new NativeDeploySparks.Cue[12];var heard=new NativeDeploySparks.Cue[24];
        int n=NativeDeploySparks.Schedule(sched,0,3,null);Check(n==7&&sched[0].Kind==NativeDeploySparks.CueKind.Spin&&sched[0].At==0,"the instance roll: a spin, three brakes and three stops");
        for(int i=1;i<n;i++)Check(sched[i].At>=sched[i-1].At,"the cues are in time order");
        for(int r=0;r<3;r++){Check(sched.Take(n).Count(c=>c.Kind==NativeDeploySparks.CueKind.Stop&&c.Reel==r&&c.At==Stop(r)&&c.Machine==0)==1,"reel "+r+" stops at its stop time");Check(sched.Take(n).Count(c=>c.Kind==NativeDeploySparks.CueKind.Brake&&c.Reel==r&&Math.Abs(c.At-(Stop(r)-NativeDeployPresentation.Decel))<1e-5f)==1,"reel "+r+" brakes");}
        var mixed=new[]{Pol.Bane,Pol.Chaos,Pol.Boon};n=NativeDeploySparks.Schedule(sched,1,3,mixed);
        Check(n==10&&!sched.Take(n).Any(c=>c.Kind==NativeDeploySparks.CueKind.Sweep),"mixed cards: no sweep cue");
        for(int r=0;r<3;r++)Check(sched.Take(n).Count(c=>c.Kind==NativeDeploySparks.CueKind.Card&&c.Reel==r&&c.Polarity==mixed[r]&&c.At==Stop(r)&&c.Machine==1)==1,"card "+r+"'s sting carries its polarity");
        n=NativeDeploySparks.Schedule(sched,1,3,new[]{Pol.Bane,Pol.Bane,Pol.Bane});Check(n==11&&sched[n-1].Kind==NativeDeploySparks.CueKind.Sweep&&sched[n-1].Polarity==Pol.Bane&&sched[n-1].At==NativeDeploySparks.SweepAt,"three banes: the sweep cue last");
        Check(NativeDeploySparks.Schedule(sched,1,0,mixed)==0&&NativeDeploySparks.Schedule(sched,1,3,null)==0,"no cards, no cues");
        // heard once each, at 60 fps or with a hitch, from the roll's first frame
        foreach(float dt in new[]{1/60f,1/7f})
        {
            int got=0;float before=float.NaN;var seen=new System.Collections.Generic.List<NativeDeploySparks.Cue>();
            for(float t=.004f;t<NativeDeploySparks.End;t+=dt){got=NativeDeploySparks.Heard(sched,n,before,t,heard,0);seen.AddRange(heard.Take(got));before=t;}
            Check(seen.Count==n&&seen.Select(c=>c.At).SequenceEqual(sched.Take(n).Select(c=>c.At)),"every cue heard once, in order (frame "+dt+")");
        }
        Check(NativeDeploySparks.Heard(sched,n,float.NaN,float.NaN,heard,0)==0&&NativeDeploySparks.Heard(sched,n,1,float.NaN,heard,0)==0,"a late joiner hears nothing");
        int late=NativeDeploySparks.Heard(sched,n,float.NaN,2f,heard,0);Check(late>0&&heard.Take(late).All(c=>c.At>1.75f&&c.At<=2f),"a first frame mid-roll hears only the last quarter second");
        int again=NativeDeploySparks.Heard(sched,n,4,.01f,heard,0);Check(again==1&&heard[0].Kind==NativeDeploySparks.CueKind.Spin,"a new roll starts from its spin");
    }
    // ---- the stage: through a real authority ----
    private static readonly ExpeditionInstanceTheme[] Pool={new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"}};
    private static DeployInput Input(ExpeditionDeploymentState s,float si,float sm,float t,bool reduced=false)
        =>new DeployInput{State=s,SinceInstanceRoll=si,SinceMutatorRoll=sm,SinceDeploy=float.NaN,Time=t,Host=true,PartyReady=true,Seats=2,Ready=new[]{true,true},Instances=Pool,Mutators=NativeDeployPresentation.MutatorIds,Reduced=reduced};
    private static void Stage()
    {
        var auth=new NativeExpeditionDeployment("preview","host",91);var st=new NativeDeployStage();
        st.Frame(Input(auth.Snapshot(),float.NaN,float.NaN,0),-1);Check(st.SparkCount==0&&st.CueCount==0,"the idle station: no sparks, no cues");
        Check(auth.RollInstances("host",Pool),"roll");var s=auth.Snapshot();int most=0,stops=0,spins=0;
        for(float t=.004f;t<NativeDeploySparks.End;t+=1/30f)
        {
            st.Frame(Input(s,t,float.NaN,t),1/30f);Check(st.Count<=NativeDeployStage.Capacity,"the draw list fits");most=Math.Max(most,st.SparkCount);
            var quads=Enumerable.Range(0,st.Count).Select(i=>st.Items[i]).Where(it=>it.Texture==NativeDeployStage.TexSpark).ToArray();
            Check(quads.Length==st.SparkCount&&quads.All(q=>q.Model==NativeDeployStage.Model.Quad&&q.Blend==NativeDeployStage.Blend.Additive),"every spark drawn as an additive quad");
            for(int i=0;i<quads.Length;i++)
            {
                var sp=st.Sparks[i];var w=quads[i].World;var at=new Vector3(w.M41,w.M42,w.M43);var face=Vector3.Normalize(sp.At-st.Camera.Eye);
                Check(Vector3.Distance(at,sp.At)<1e-4f&&Vector3.Distance(new Vector3(w.M31,w.M32,w.M33),face)<1e-3f,"a spark's quad sits on it, facing the camera");
            }
            for(int i=0;i<st.CueCount;i++){var c=st.Cues[i];Check(c.Machine==0,"only the instance machine is heard");if(c.Kind==NativeDeploySparks.CueKind.Stop)stops++;if(c.Kind==NativeDeploySparks.CueKind.Spin)spins++;}
        }
        Check(most>0&&spins==1&&stops==3,"the roll sparks, spins once and stops three times ("+most+", "+spins+", "+stops+")");
        Check(auth.RollMutators("host",NativeDeployPresentation.MutatorIds),"mutator roll");var m=auth.Snapshot();int cards=0;
        for(float t=.004f;t<NativeDeploySparks.End;t+=1/30f)
        {
            st.Frame(Input(m,30,t,30+t),1/30f);
            for(int i=0;i<st.CueCount;i++){var c=st.Cues[i];if(c.Kind!=NativeDeploySparks.CueKind.Card)continue;cards++;Check(c.Machine==1&&c.Polarity==NativeDeployPresentation.MutatorOf(m.mutators[c.Reel]).Polarity,"card "+c.Reel+"'s sting is its card's polarity");}
        }
        Check(cards==m.mutators.Length&&cards==3,"each card heard as it lands");
        var late=new NativeDeployStage();late.Frame(Input(m,float.NaN,float.NaN,50),-1);Check(late.SparkCount==0&&late.CueCount==0,"a late joiner sees no sparks and hears no cues");
        var reduced=new NativeDeployStage();int full=0,less=0;
        for(float t=.004f;t<NativeDeploySparks.End;t+=1/30f){st.Frame(Input(m,30,t,60+t),1/30f);reduced.Frame(Input(m,30,t,60+t,true),1/30f);full+=st.SparkCount;less+=reduced.SparkCount;}
        Check(less<.5f*full,"reduced effects thin the station's sparks");
    }
    private static void Wiring(string root)
    {
        var tile=NativeDeployArt.SparkTile();int px=NativeDeployArt.SparkPixels;
        Check(tile.Length==px*px*4&&tile[3]<.01f&&tile[((px/2)*px+px/2)*4+3]>.85f&&tile[((px/2)*px+px/2+px/4)*4+3]<.5f,"the spark is a hot core in a soft halo");
        var scene=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDeployScene.cs"));
        Check(scene.Contains("textures[NativeDeployStage.TexSpark]=Texture(NativeDeployArt.SparkTile()"),"the scene uploads the spark");
        // the dials' thirty cards: each tile rasterised once in parallel and stacked per strip; a strip not ready draws dark, not white
        Check(scene.Contains("Task.WhenAll(tiles).ContinueWith(all=>NativeDeployArt.Stack(all.Result)")&&scene.Contains("tileJobs.TryGetValue(key,out var job)")&&!scene.Contains("for(int i=0;i<ids.Length;i++)tiles.Add(NativeDeployArt.Tile("),"each card tile rasterised once, in parallel");
        Check(scene.Contains("textures[it.Texture]??(it.Texture>=NativeDeployStage.TexStrip?blank:white)"),"a strip still rasterising draws a dark blank");
        var meta=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeDeploySparks.cs.meta"));Check(meta.Contains("guid: ")&&!meta.Contains(" \n"),"the sparks' meta");
    }
}
