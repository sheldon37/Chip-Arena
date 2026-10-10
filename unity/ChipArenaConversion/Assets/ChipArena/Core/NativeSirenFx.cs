using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>The siren-* event types client/siren/fx.ts draws (shared/siren-contract.ts:140-160).</summary>
    public enum NativeSirenEvent{Slam,Rush,Laugh,Lunge,Catch,Crush,Whiff,Cast,Explode,Eruption,Drop,SeekerOut,SeekerHit,Hit}
    /// <summary>One-shot pieces of siren/fx.ts: floor shock ring, flash sprite, claw slash, ground cracks, flame-shell explosion.</summary>
    public enum NativeSirenFxKind{Shock,Flash,Slash,Cracks,Explosion}
    /// <summary>siren/fx.ts shot kinds (SirenShot.kind).</summary>
    public enum NativeSirenShotKind{Hellfire,Damnation,Drop,Seeker}

    public struct NativeSirenTransient
    {
        public NativeSirenFxKind Kind;public double Born,Life,X,Y,Z,Radius,Opacity,Yaw;public bool Dark,Rend;public int Side,Laid,Flames,Crack;
    }
    /// <summary>A Siren projectile's presentation state (siren/fx.ts ShotView): first sight, last position and the drop marker.</summary>
    public sealed class NativeSirenShotState
    {public long Id;public NativeSirenShotKind Kind;public double Born,LastX,LastY,LastZ;public int Seen;}
    /// <summary>An eruption's column state after <see cref="NativeSirenFx.Eruption"/> (the ring and fill are C9's telegraphs).</summary>
    public sealed class NativeSirenEruptionState
    {public long Id;public bool Burst,Column;public double Acc,Width,Height,Spin,Fade;public int Seen;}

    /// <summary>
    /// Pure port of client/siren/fx.ts SirenFx at 05950ea (everything but the mark crosshair, NativeSirenMark below, the victim's
    /// struggle pose and cutscene, C16, and the draws): the event table, the one-shot pieces, the claw slash with its
    /// lingering flames, projectile trails and coronas, fire-patch, eruption and rain flames and the Last Laugh burn, all
    /// in the source's world frame and fed the source's random stream in the source's order. Also motion.ts emitFlames (the
    /// body fire) and first-person.ts (the claw view model). One shared world <see cref="Fire"/> pool replaces the source's
    /// 1600-cube world field and the 420-cube field on each Siren rig (they burn and age identically). Allocation free after
    /// warm-up; the runtime (Runtime/NativeDevEffects.cs) feeds it what host, guests and the killcam all hold.
    /// </summary>
    public sealed class NativeSirenFx
    {
        public const int FireCapacity=2048,ViewCapacity=120,MaxTransients=256,CrackSlots=32,Cracks=9;
        public const int Ink=0x0a080e,Ash=0xcfc6ea,Warn=0xff2f44,WarnDeep=0x8a0d1c,FlashColor=0xf2eeff;
        public const double ArcSpan=Math.PI*.85;public const int ArcSegments=36;
        public readonly NativeFlameField Fire;
        public double Time,Quality=1,Dt;
        private readonly Func<double> R;
        private readonly NativeSirenTransient[] transients=new NativeSirenTransient[MaxTransients];private int count;
        /// <summary>Crack boxes per slot: angle, length, width for each of the nine.</summary>
        public readonly double[] CrackBoxes=new double[CrackSlots*Cracks*3];private int crackCursor;
        private readonly List<NativeSirenShotState> shots=new List<NativeSirenShotState>();private readonly Stack<NativeSirenShotState> spareShots=new Stack<NativeSirenShotState>();
        private readonly Dictionary<long,double> patches=new Dictionary<long,double>(),rains=new Dictionary<long,double>(),grabs=new Dictionary<long,double>(),auras=new Dictionary<long,double>();
        private readonly Dictionary<long,int> patchSeen=new Dictionary<long,int>(),rainSeen=new Dictionary<long,int>();
        private readonly List<NativeSirenEruptionState> eruptions=new List<NativeSirenEruptionState>();private readonly Stack<NativeSirenEruptionState> spareEruptions=new Stack<NativeSirenEruptionState>();
        private readonly List<long> stale=new List<long>();private int frame;
        private readonly double[] slashMatrix=new double[16],scratchA=new double[16],scratchB=new double[16];

        public NativeSirenFx(Func<double> random,int capacity=FireCapacity){R=random??throw new ArgumentNullException(nameof(random));Fire=new NativeFlameField(capacity,random);}
        public int Count=>count;
        /// <summary>The frame counter <see cref="Begin"/> advances (shots not seen this frame are gone).</summary>
        public int Frame=>frame;
        public NativeSirenTransient this[int i]=>transients[i];
        public IReadOnlyList<NativeSirenShotState> Shots=>shots;
        public IReadOnlyList<NativeSirenEruptionState> Eruptions=>eruptions;
        /// <summary>JavaScript Math.round (halves up), which the source uses for every count.</summary>
        public static int JsRound(double x)=>(int)Math.Floor(x+.5);
        private static double Sm(double x)=>NativeFxMath.Smooth(x);
        private static double Clamp(double x)=>NativeFxMath.Clamp01(x);

        /// <summary>siren/fx.ts clear (round or generation change, reset).</summary>
        public void Clear()
        {
            count=0;Fire.Clear();foreach(var s in shots)spareShots.Push(s);shots.Clear();patches.Clear();patchSeen.Clear();rains.Clear();rainSeen.Clear();grabs.Clear();auras.Clear();
            foreach(var e in eruptions)spareEruptions.Push(e);eruptions.Clear();
        }
        /// <summary>update's clock: dt = clamp(time - last, 0, .05).</summary>
        public double Begin(double time,double quality){double d=time-Time;Dt=Math.Min(.05,Math.Max(0,d));if(double.IsNaN(Dt))Dt=0;Time=time;Quality=quality;frame++;return Dt;}

        // ---- one-shot pieces (siren/fx.ts add/shock/flash/slash/cracks/explosion) --------------------------------------
        private void Add(in NativeSirenTransient t)
        {
            if(count==MaxTransients){Array.Copy(transients,1,transients,0,count-1);count--;}
            transients[count++]=t;
        }
        private double BornAt(double born)=>double.IsNaN(born)?Time:born;
        /// <summary>Expanding floor ring: dark (a soot shockwave, ring .55-1) or pale (a burning edge, ring .86-1, additive).</summary>
        public void Shock(double x,double y,double z,double radius,double life,bool dark,double born=double.NaN)
        =>Add(new NativeSirenTransient{Kind=NativeSirenFxKind.Shock,Born=BornAt(born),Life=life,X=x,Y=y,Z=z,Radius=radius,Dark=dark,Opacity=dark?.6:.8});
        /// <summary>Bright (additive #f2eeff) puff sprite.</summary>
        public void Flash(double x,double y,double z,double size,double life,double opacity,double born=double.NaN)
        =>Add(new NativeSirenTransient{Kind=NativeSirenFxKind.Flash,Born=BornAt(born),Life=life,X=x,Y=y,Z=z,Radius=size,Opacity=opacity});
        /// <summary>
        /// Claw slash: a crescent pivoting at (x, y, z) (the Siren's centre lifted .38 m) turned to yaw (source radians),
        /// sweeping from the Siren's left to its right for side +1.
        /// </summary>
        public void Slash(bool rend,int side,double x,double y,double z,double yaw,double born=double.NaN)
        =>Add(new NativeSirenTransient{Kind=NativeSirenFxKind.Slash,Born=BornAt(born),Life=rend?.42:.24,X=x,Y=y,Z=z,Yaw=yaw,Rend=rend,Side=side>=0?1:-1,Flames=JsRound((rend?26:9)*(Quality<.5?.5:1))});
        /// <summary>Radiating ground cracks (Grave Slam): nine black boxes.</summary>
        public void Crack(double x,double y,double z,double radius,double born=double.NaN)
        {
            int slot=crackCursor;crackCursor=(crackCursor+1)%CrackSlots;
            for(int i=0;i<Cracks;i++){int k=(slot*Cracks+i)*3;double a=i/(double)Cracks*Math.PI*2+R()*.4,len=radius*(.5+R()*.5);CrackBoxes[k]=a;CrackBoxes[k+1]=len;CrackBoxes[k+2]=.07+R()*.05;}
            Add(new NativeSirenTransient{Kind=NativeSirenFxKind.Cracks,Born=BornAt(born),Life=1.6,X=x,Y=y,Z=z,Radius=radius,Crack=slot});
        }
        /// <summary>Black-flame explosion (Damnation, seeker): a flame shell that swells and tears, rings, a flash and bursts.</summary>
        public void Explosion(double x,double y,double z,double radius,double born=double.NaN)
        {
            double q=Quality<.5?.5:1;
            Add(new NativeSirenTransient{Kind=NativeSirenFxKind.Explosion,Born=BornAt(born),Life=.7,X=x,Y=y,Z=z,Radius=radius});
            Shock(x,y,z,radius*1.15,.5,true,born);Shock(x,y,z,radius,.35,false,born);Flash(x,y+.6,z,radius*1.6,.3,.85,born);
            Fire.Burst(x,y+.4,z,JsRound(110*q),radius*2.2,1.1,.2,NativeFlameTone.Black,1.2,1.6);Fire.Burst(x,y+.4,z,JsRound(40*q),radius*1.6,1.2,.14,NativeFlameTone.Smoke,1.6,1.2);Fire.Burst(x,y+.4,z,JsRound(24*q),radius*2.5,.8,.05,NativeFlameTone.Ember,2);
        }
        /// <summary>
        /// siren/fx.ts event (all but swipe/rend, which are <see cref="Slash"/>): radius NaN takes the source default; k is the
        /// grown Siren's visual scale (1 in PvP); born NaN means now (the source's this.time).
        /// </summary>
        public void Event(NativeSirenEvent type,double x,double y,double z,double radius=double.NaN,bool damnation=false,double k=1,double born=double.NaN)
        {
            double q=Quality<.5?.5:1;var F=Fire;
            switch(type)
            {
                case NativeSirenEvent.Slam:{double r=double.IsNaN(radius)?3.6:radius;Shock(x,y,z,r,.45,true,born);Shock(x,y,z,r*.65,.3,false,born);Crack(x,y+.03,z,r*.8,born);F.Burst(x,y+.1,z,JsRound(70*q),5.5,.9,.16,NativeFlameTone.Black,.6,1.2);F.Burst(x,y+.1,z,JsRound(14*q),4,.7,.06,NativeFlameTone.Ember,1.5);Flash(x,y,z,2.6,.25,.5,born);break;}
                case NativeSirenEvent.Rush:F.Burst(x,y-.4*k,z,JsRound(30*q),3,.6,.12,NativeFlameTone.Black,.4);Shock(x,y-.83*k,z,1.4*k,.25,false,born);break;
                case NativeSirenEvent.Laugh:Shock(x,y-.83*k,z,2.2*k,.6,true,born);F.Burst(x,y+.5*k,z,JsRound(16*q),1.5,.8,.09,NativeFlameTone.Black,1.4);break;
                case NativeSirenEvent.Lunge:F.Burst(x,y,z,JsRound(20*q),2.5,.5,.1,NativeFlameTone.Black,.5);break;
                case NativeSirenEvent.Catch:F.Burst(x,y+.4*k,z,JsRound(30*q),2.2,.7,.1,NativeFlameTone.Black,1);Flash(x,y,z,1.2,.15,.6,born);break;
                case NativeSirenEvent.Crush:F.Burst(x,y,z,JsRound(60*q),4,.9,.12,NativeFlameTone.Black,1.2);F.Burst(x,y,z,JsRound(16*q),5,.6,.05,NativeFlameTone.Ember,1);Flash(x,y,z,1.6,.2,.9,born);Shock(x,y-1.3,z,2.4,.4,true,born);break;
                case NativeSirenEvent.Whiff:F.Burst(x,y,z,JsRound(14*q),2,.5,.09,NativeFlameTone.Smoke,.6);break;
                case NativeSirenEvent.Cast:F.Burst(x,y,z,JsRound((damnation?36:10)*q),damnation?3:1.6,.45,.09,NativeFlameTone.Black,.8);Flash(x,y,z,damnation?1.6:.7,.12,.7,born);break;
                case NativeSirenEvent.Explode:Explosion(x,y,z,double.IsNaN(radius)?3.5:radius,born);break;
                case NativeSirenEvent.Eruption:{double r=double.IsNaN(radius)?2.2:radius;Shock(x,y,z,r*1.4,.35,false,born);Flash(x,y+1,z,2.5,.2,.7,born);break;}
                case NativeSirenEvent.Drop:F.Burst(x,y+.05,z,JsRound(10*q),1.8,.45,.08,NativeFlameTone.Black,.8);Shock(x,y,z,.7,.2,false,born);break;
                case NativeSirenEvent.SeekerOut:F.Burst(x,y,z,JsRound(70*q),4.5,1,.14,NativeFlameTone.Smoke,1);Flash(x,y,z,2.8,.3,1,born);Shock(x,Math.Max(.02,y-1),z,2.6,.4,false,born);break;
                case NativeSirenEvent.SeekerHit:Explosion(x,y,z,4.2,born);Flash(x,y,z,3.6,.35,1,born);break;
                case NativeSirenEvent.Hit:F.Burst(x,y,z,JsRound(12*q),2.2,.45,.08,NativeFlameTone.Black,.9);F.Burst(x,y,z,JsRound(4*q),3,.3,.04,NativeFlameTone.Ember,.5);break;
            }
        }
        /// <summary>Last Laugh windup aura: a dark floor ring at the feet every .7 s while it stands there smiling.</summary>
        public void Aura(long id,double x,double feetY,double z,double k)
        {if(!auras.TryGetValue(id,out double last))last=-1;if(Time-last>.7){auras[id]=Time;Shock(x,feetY,z,2.1*k,.8,true);}}

        // ---- projectiles (updateShots, trail) ----------------------------------------------------------------------
        /// <summary>trail(): rate·dt cubes (stochastic rounding) strung back along the last step.</summary>
        public void Trail(double x,double y,double z,double vx,double vy,double vz,double dt,double rate,double size,double life,NativeFlameTone tone,double spread)
        {
            double n=rate*dt;int count=(int)Math.Floor(n)+(R()<n%1?1:0);
            for(int i=0;i<count;i++)
            {double back=R()*dt;Fire.Emit(x-vx*back+(R()-.5)*spread,y-vy*back+(R()-.5)*spread,z-vz*back+(R()-.5)*spread,-vx*.08+(R()-.5)*.4,.3+R()*.6,-vz*.08+(R()-.5)*.4,life*(.6+R()*.6),size*(.7+R()*.6),tone,1.8,2.2);}
        }
        /// <summary>One live Siren projectile this frame (state.projectiles order): its trails, corona and ember sparks.</summary>
        public NativeSirenShotState Shot(long id,NativeSirenShotKind kind,double born,double x,double y,double z,double vx,double vy,double vz)
        {
            NativeSirenShotState v=null;for(int i=0;i<shots.Count;i++)if(shots[i].Id==id){v=shots[i];break;}
            if(v==null){v=spareShots.Count>0?spareShots.Pop():new NativeSirenShotState();v.Id=id;v.Kind=kind;v.Born=born;shots.Add(v);}
            v.LastX=x;v.LastY=y;v.LastZ=z;v.Seen=frame;double dt=Dt,q=Quality<.5?.5:Quality;
            if(kind==NativeSirenShotKind.Seeker){Trail(x,y,z,vx,vy,vz,dt,48*q,.16,.9,NativeFlameTone.Black,.35);Trail(x,y,z,vx,vy,vz,dt,10*q,.06,.8,NativeFlameTone.Ember,.45);}
            else if(kind==NativeSirenShotKind.Drop)Trail(x,y,z,vx,vy,vz,dt,40*q,.07,.25,NativeFlameTone.Black,.05);
            else
            {
                bool big=kind==NativeSirenShotKind.Damnation;
                Trail(x,y,z,vx,vy,vz,dt,(big?160:80)*q,big?.24:.13,big?.75:.5,NativeFlameTone.Black,big?.4:.2);Trail(x,y,z,vx,vy,vz,dt,(big?40:16)*q,big?.16:.09,big?.9:.6,NativeFlameTone.Smoke,big?.5:.25);
                // Corona: black tongues licking off the surface, against the violet glow.
                double r=big?.5:.26;int n=JsRound((big?50:24)*q*dt*10);
                for(int k=0;k<n;k++){double a=R()*Math.PI*2,u=R()*2-1,w=Math.Sqrt(1-u*u);Fire.Emit(x+Math.Cos(a)*w*r,y+u*r,z+Math.Sin(a)*w*r,Math.Cos(a)*w*.8-vx*.1,u*.8+.6,Math.Sin(a)*w*.8-vz*.1,.18+R()*.15,(big?.12:.07)*(.7+R()*.5),NativeFlameTone.Black,2,2.5);}
                if(R()<dt*(big?14:6)&&dt>0)Trail(x,y,z,vx,vy,vz,dt,1/dt,.05,.4,NativeFlameTone.Ember,.1);
            }
            return v;
        }
        /// <summary>Projectiles not seen this frame are gone: a small black puff where they were (not seekers or drops).</summary>
        public void EndShots()
        {
            for(int i=0;i<shots.Count;i++)
            {
                var v=shots[i];if(v.Seen==frame)continue;
                if(Time-v.Born>.05&&v.Kind!=NativeSirenShotKind.Seeker&&v.Kind!=NativeSirenShotKind.Drop)Fire.Burst(v.LastX,v.LastY,v.LastZ,v.Kind==NativeSirenShotKind.Damnation?20:10,2,.5,.1,NativeFlameTone.Black,1);
                shots.RemoveAt(i--);spareShots.Push(v);
            }
        }
        /// <summary>The orb core pulse (seekers 9 Hz, the rest 20 Hz).</summary>
        public static double ShotPulse(NativeSirenShotKind kind,double time)=>.5+.5*Math.Sin(time*(kind==NativeSirenShotKind.Seeker?9:20));

        // ---- fire patches, eruptions and rain (updatePatches / updateEruptions / updateRains) ---------------------------
        /// <summary>One live fire patch (kind 0 hellfire, 1 trail, 2 scorch): its spawn burst and rising flames.</summary>
        public void Patch(long id,int kind,double x,double y,double z,double radius,double at,double until)
        {
            double q=Quality<.5?.45:Quality;
            if(!patches.TryGetValue(id,out double acc)){acc=0;if(Time-at<.3)Fire.Burst(x,y+.1,z,JsRound(12+16*radius),2+radius,.6,.11,NativeFlameTone.Black,1);}
            patchSeen[id]=frame;double fade=1-Sm((Time-(until-.8))/.8);
            // Flames rise from all over the patch; density follows its area.
            acc+=Dt*q*fade*(kind==1?16:26)*Math.Max(.6,radius*radius);
            while(acc>=1){acc-=1;double a=R()*Math.PI*2,r=Math.Sqrt(R())*radius*.95;Fire.Emit(x+Math.Cos(a)*r,y+.05,z+Math.Sin(a)*r,(R()-.5)*.25,.5+R()*.9,(R()-.5)*.25,.45+R()*.45,(.08+R()*.08)*(kind==2?1.3:1),R()<.04?NativeFlameTone.Ember:R()<.15?NativeFlameTone.Smoke:NativeFlameTone.Black,2.4,1.8);}
            patches[id]=acc;
        }
        public void EndPatches()=>Forget(patches,patchSeen);
        private void Forget(Dictionary<long,double> map,Dictionary<long,int> seen)
        {
            stale.Clear();foreach(var pair in seen)if(pair.Value!=frame)stale.Add(pair.Key);
            foreach(long id in stale){seen.Remove(id);map.Remove(id);}
        }
        /// <summary>One live eruption: warning flames until burstAt, then the burst puffs, the jet column and its flames.</summary>
        public NativeSirenEruptionState Eruption(long id,double x,double y,double z,double radius,double at,double burstAt,double until)
        {
            double q=Quality<.5?.5:1;NativeSirenEruptionState v=null;for(int i=0;i<eruptions.Count;i++)if(eruptions[i].Id==id){v=eruptions[i];break;}
            if(v==null){v=spareEruptions.Count>0?spareEruptions.Pop():new NativeSirenEruptionState();v.Id=id;v.Burst=false;v.Acc=0;eruptions.Add(v);}
            v.Seen=frame;double t=Time;bool warn=t<burstAt;double u=Clamp((t-at)/Math.Max(.05,burstAt-at));v.Column=false;
            if(warn)
            {
                v.Acc+=Dt*q*(8+30*u);
                while(v.Acc>=1){v.Acc-=1;double a=R()*Math.PI*2,r=Math.Sqrt(R())*radius;Fire.Emit(x+Math.Cos(a)*r,y+.05,z+Math.Sin(a)*r,0,.6+u*1.6,0,.35,.06+.05*u,R()<.1?NativeFlameTone.Warn:NativeFlameTone.Black,2,2);}
            }
            else
            {
                double b=Clamp((t-burstAt)/Math.Max(.05,until-burstAt));v.Height=Math.Max(.01,5.6*Math.Sqrt(Sm(b*3)));v.Column=b<1;v.Width=radius*.8*(1-.3*b);v.Spin=t*3;v.Fade=1-Sm((b-.55)/.45);
                if(!v.Burst){v.Burst=true;Fire.Burst(x,y+.2,z,JsRound(60*q),2.2,.8,.16,NativeFlameTone.Black,7,1);Fire.Burst(x,y+.2,z,JsRound(14*q),3,.7,.05,NativeFlameTone.Ember,8);}
                v.Acc+=Dt*q*120*(1-b);
                while(v.Acc>=1){v.Acc-=1;double a=R()*Math.PI*2,r=Math.Sqrt(R())*radius*.7;Fire.Emit(x+Math.Cos(a)*r,y+.1,z+Math.Sin(a)*r,(R()-.5)*.6,8+R()*6,(R()-.5)*.6,.45+R()*.3,.14+R()*.12,R()<.06?NativeFlameTone.Ember:R()<.2?NativeFlameTone.Smoke:NativeFlameTone.Black,-2,1.2);}
            }
            return v;
        }
        public void EndEruptions(){for(int i=0;i<eruptions.Count;i++){var v=eruptions[i];if(v.Seen==frame)continue;eruptions.RemoveAt(i--);spareEruptions.Push(v);}}
        /// <summary>One rain cloud: black storm cubes under the ceiling and warn cubes on its floor edge.</summary>
        public void Rain(long id,double x,double y,double z,double radius,double at,double until)
        {
            rainSeen[id]=frame;double t=Time;if(t>until)return;double q=Quality<.5?.45:1;rains.TryGetValue(id,out double acc);
            double k=Sm((t-at)/.8)*(1-Sm((t-(until-1))/1));acc+=Dt*q*55*k*(radius/7)*(radius/7);
            // The storm gathers just under Ward Zero's ceiling grid (about 9 m); drops spawn around 8 m.
            while(acc>=1){acc-=1;double a=R()*Math.PI*2,d=Math.Sqrt(R())*radius*1.05;Fire.Emit(x+Math.Cos(a)*d,y+8.2+R()*.8,z+Math.Sin(a)*d,(R()-.5)*.4,-.1+R()*.2,(R()-.5)*.4,1.6+R()*1,.35+R()*.35,R()<.25?NativeFlameTone.Smoke:NativeFlameTone.Black,.05,.8);}
            rains[id]=acc;
            if(R()<Dt*q*20){double a=R()*Math.PI*2;Fire.Emit(x+Math.Cos(a)*radius,y+.05,z+Math.Sin(a)*radius,0,.4,0,.6,.07,NativeFlameTone.Warn,.6,2);}
        }
        public void EndRains()=>Forget(rains,rainSeen);

        // ---- Last Laugh burn (updateGrabs, the flame half) ---------------------------------------------------------------
        /// <summary>The victim joints the burn climbs over (CapturedRig head, chest, spine, arms, elbows, hips, knees).</summary>
        public static readonly int[] GrabJoints={5,3,2,7,11,8,12,14,18,15,19};
        /// <summary>The char on the victim: colour lerps toward #0c0a10 by .85·burn, emission scales by 1-.8·burn.</summary>
        public static double Burn(double age)=>Sm((age-.9)/1.6);
        /// <summary>
        /// One held victim: black flame climbs over it from the grip. joints holds 22 row-major 4x4 source-frame bone
        /// matrices (CapturedRig order).
        /// </summary>
        public void Grab(long victim,double age,double[] joints)
        {
            double q=Quality<.5?.5:1,burn=Burn(age);grabs.TryGetValue(victim,out double acc);acc+=Dt*q*(10+90*burn);
            while(acc>=1)
            {
                acc-=1;int j=GrabJoints[(int)Math.Floor(R()*(burn>.3?GrabJoints.Length:3))];double lx=(R()-.5)*.2,ly=(R()-.5)*.2,lz=(R()-.5)*.2;
                NativeFxMath.Point(joints,j*16,lx,ly,lz,out double wx,out double wy,out double wz);
                Fire.Emit(wx,wy,wz,(R()-.5)*.3,1.2+R()*.8,(R()-.5)*.3,.45+R()*.35,.07+R()*.06,R()<.05?NativeFlameTone.Ember:NativeFlameTone.Black,2.6,2);
            }
            grabs[victim]=acc;
        }

        // ---- per-frame end -------------------------------------------------------------------------------------------
        /// <summary>Ticks the one-shot pieces (newest first, as the source) and advances the fire by this frame's dt.</summary>
        public void Tick()
        {
            for(int i=count-1;i>=0;i--)
            {
                double u=(Time-transients[i].Born)/transients[i].Life;
                if(u>=1||u<-.5){Array.Copy(transients,i+1,transients,i,count-i-1);count--;continue;}
                if(transients[i].Kind==NativeSirenFxKind.Slash)LaySlash(ref transients[i],Math.Max(0,u));
            }
            Fire.Update(Dt);
        }
        public static double SlashSpeed(bool rend)=>rend?3.2:3.6;
        /// <summary>The slash shader inputs at progress u: the sweep (leading edge along uv.x) and the fade.</summary>
        public static void SlashUniforms(bool rend,double u,out double sweep,out double fade){sweep=Math.Min(1.9,u*SlashSpeed(rend));fade=1-Sm((u-.45)/.55);}
        /// <summary>The crescent mesh's world matrix (source frame): pivot·yaw·T(0,0,.1)·R(pitch,0,roll)·S(-outer·side,1,outer).</summary>
        public void SlashMatrix(in NativeSirenTransient s,double[] m)
        {
            double outer=s.Rend?1.6:1.2;
            NativeFxMath.Compose(scratchA,s.X,s.Y,s.Z,0,s.Yaw,0,1,1,1);
            NativeFxMath.Compose(scratchB,0,0,.1,s.Rend?.3:.1,0,s.Side*(s.Rend?.9:.5),outer*-s.Side,1,outer);
            NativeFxMath.Multiply(m,scratchA,scratchB);
        }
        private void LaySlash(ref NativeSirenTransient s,double u)
        {
            // Lay the lingering flames along the part of the arc already swept.
            int want=(int)Math.Floor(Math.Min(1,u*SlashSpeed(s.Rend))*s.Flames);if(s.Laid>=want)return;SlashMatrix(s,slashMatrix);
            for(;s.Laid<want;s.Laid++)
            {
                double a=-ArcSpan/2+ArcSpan*(s.Laid+.5)/s.Flames,rr=.62+R()*.34;NativeFxMath.Point(slashMatrix,0,Math.Sin(a)*rr,0,Math.Cos(a)*rr,out double x,out double y,out double z);
                for(int k=0;k<(s.Rend?3:2);k++){bool ember=R()<.06;Fire.Emit(x+(R()-.5)*.1,y+(R()-.5)*.08,z+(R()-.5)*.1,(R()-.5)*.2,.5+R()*.6,(R()-.5)*.2,(s.Rend?.85:.5)+R()*.35,(s.Rend?.075:.055)*(.7+R()*.5)*(ember?.45:1),ember?NativeFlameTone.Ember:NativeFlameTone.Soot,1.8,2.2);}
            }
        }
        /// <summary>Shock ring at progress u: scale and opacity.</summary>
        public static void ShockLook(in NativeSirenTransient s,double u,out double scale,out double opacity){scale=.2+s.Radius*(1-Math.Pow(1-u,3));opacity=(s.Dark?.6:.8)*Math.Pow(1-u,1.5);}
        public static void FlashLook(in NativeSirenTransient s,double u,out double scale,out double opacity){scale=s.Radius*(.4+.6*Math.Sqrt(u));opacity=s.Opacity*(1-u)*(1-u);}
        public static void CrackLook(double u,out double scale,out double opacity){opacity=.85*(1-Sm((u-.5)/.5));scale=.6+.4*Math.Min(1,u*8);}
        /// <summary>Explosion shell at progress u: radius, vertical squash .85, turn 2u, fade.</summary>
        public static void ExplosionLook(in NativeSirenTransient s,double u,out double scale,out double turn,out double fade){scale=s.Radius*.8*Math.Sqrt(Math.Min(1,u*2.4));turn=u*2;fade=1-Sm((u-.35)/.65);}

        // ---- geometry (three.js ARC, RingGeometry, SphereGeometry, CylinderGeometry as built by fx.ts) -------------------
        /// <summary>arcGeometry(): 37 x 2 vertices (inner .5 with uv.y 0, outer 1 with uv.y 1), uv.x along the arc.</summary>
        public static readonly double[] ArcPositions=ArcBuild(out ArcUv,out ArcIndices);public static readonly double[] ArcUv;public static readonly int[] ArcIndices;
        private static double[] ArcBuild(out double[] uv,out int[] idx)
        {
            var p=new double[(ArcSegments+1)*2*3];uv=new double[(ArcSegments+1)*2*2];idx=new int[ArcSegments*6];
            for(int i=0;i<=ArcSegments;i++){double u=i/(double)ArcSegments,a=-ArcSpan/2+ArcSpan*u,s=Math.Sin(a),c=Math.Cos(a);for(int j=0;j<2;j++){double r=j==0?.5:1;int v=i*2+j;p[v*3]=s*r;p[v*3+1]=0;p[v*3+2]=c*r;uv[v*2]=u;uv[v*2+1]=j;}}
            for(int i=0;i<ArcSegments;i++){int a=i*2;idx[i*6]=a;idx[i*6+1]=a+1;idx[i*6+2]=a+3;idx[i*6+3]=a;idx[i*6+4]=a+3;idx[i*6+5]=a+2;}
            return p;
        }
        /// <summary>SphereGeometry(1, 32, 20) positions (= normals) and uvs.</summary>
        public static void Sphere(out double[] positions,out double[] uv,out int[] indices,int w=32,int h=20)
        {
            positions=new double[(w+1)*(h+1)*3];uv=new double[(w+1)*(h+1)*2];var list=new List<int>();
            for(int iy=0;iy<=h;iy++){double v=iy/(double)h;for(int ix=0;ix<=w;ix++){double u=ix/(double)w;int k=iy*(w+1)+ix;positions[k*3]=-Math.Cos(u*Math.PI*2)*Math.Sin(v*Math.PI);positions[k*3+1]=Math.Cos(v*Math.PI);positions[k*3+2]=Math.Sin(u*Math.PI*2)*Math.Sin(v*Math.PI);uv[k*2]=u;uv[k*2+1]=1-v;}}
            for(int iy=0;iy<h;iy++)for(int ix=0;ix<w;ix++){int a=iy*(w+1)+ix+1,b=iy*(w+1)+ix,c=(iy+1)*(w+1)+ix,d=(iy+1)*(w+1)+ix+1;if(iy!=0){list.Add(a);list.Add(b);list.Add(d);}if(iy!=h-1){list.Add(b);list.Add(c);list.Add(d);}}
            indices=list.ToArray();
        }
        /// <summary>The eruption JET: open CylinderGeometry(.55, 1, 1, 32, 8) lifted so its base is the origin.</summary>
        public static void Jet(out double[] positions,out double[] normals,out double[] uv,out int[] indices)
        {
            const int w=32,h=8;const double top=.55,bottom=1;positions=new double[(w+1)*(h+1)*3];normals=new double[positions.Length];uv=new double[(w+1)*(h+1)*2];indices=new int[w*h*6];double slope=bottom-top;
            for(int y=0;y<=h;y++){double v=y/(double)h,radius=v*(bottom-top)+top;for(int x=0;x<=w;x++){double u=x/(double)w,t=u*Math.PI*2,s=Math.Sin(t),c=Math.Cos(t);int k=y*(w+1)+x;positions[k*3]=radius*s;positions[k*3+1]=-v+.5+.5;positions[k*3+2]=radius*c;double l=Math.Sqrt(s*s+slope*slope+c*c);normals[k*3]=s/l;normals[k*3+1]=slope/l;normals[k*3+2]=c/l;uv[k*2]=u;uv[k*2+1]=1-v;}}
            int n=0;for(int x=0;x<w;x++)for(int y=0;y<h;y++){int a=y*(w+1)+x,b=(y+1)*(w+1)+x,c=(y+1)*(w+1)+x+1,d=y*(w+1)+x+1;indices[n++]=a;indices[n++]=b;indices[n++]=d;indices[n++]=b;indices[n++]=c;indices[n++]=d;}
        }
    }

    /// <summary>
    /// client/siren/motion.ts emitFlames and the Damnation charge: the black fire shed by a posed Siren rig (21 joint emitters
    /// and 16 flame-tongue tips). One instance per Siren; joints are 22 row-major 4x4 source-frame bone matrices.
    /// </summary>
    public sealed class NativeSirenBody
    {
        private struct Emitter{public int Joint;public double X,Y,Z,Rate,Size,Up,Spread;public bool Tail,Hands;}
        private static Emitter E(int j,double x,double y,double z,double rate,double size,double up,double spread,bool tail=false,bool hands=false)=>new Emitter{Joint=j,X=x,Y=y,Z=z,Rate=rate,Size=size,Up=up,Spread=spread,Tail=tail,Hands=hands};
        private static readonly Emitter[] Emitters={
            E(5,0,.34,-.02,38,.075,1.7,.13),E(5,0,.24,-.15,16,.065,1.3,.12),E(6,.1,.06,0,26,.07,1.4,.09),E(10,-.1,.06,0,26,.07,1.4,.09),
            E(3,0,.3,-.12,30,.08,1.5,.16),E(3,0,.1,-.17,16,.07,1.2,.14),E(2,0,.05,-.14,10,.065,1,.12),E(3,.27,.12,0,12,.05,1.2,.06),E(3,-.27,.12,0,12,.05,1.2,.06),
            E(1,.16,-.04,0,8,.05,1,.06),E(1,-.16,-.04,0,8,.05,1,.06),E(7,0,-.12,-.06,9,.055,1,.05),E(11,0,-.12,-.06,9,.055,1,.05),E(8,0,-.08,-.05,8,.05,1,.05),E(12,0,-.08,-.05,8,.05,1,.05),
            E(9,0,-.12,.06,7,.05,.9,.05,hands:true),E(13,0,-.12,.06,7,.05,.9,.05,hands:true),
            E(15,0,-.5,0,30,.07,.2,.06,tail:true),E(19,0,-.5,0,30,.07,.2,.06,tail:true),E(15,0,-.2,0,14,.06,.5,.07,tail:true),E(19,0,-.2,0,14,.06,.5,.07,tail:true)};
        /// <summary>model.ts TONGUES: joint, x, y, z, height, lean.</summary>
        public static readonly double[] Tongues={
            5,-.115,.33,.01,.85,-.2, 5,-.045,.35,-.03,1.2,-.32, 5,.04,.35,-.01,1.05,-.28, 5,.115,.33,.02,.8,-.18, 5,0,.31,-.12,1.0,-.62, 5,-.09,.29,-.13,.75,-.7, 5,.09,.29,-.12,.7,-.7,
            6,.1,.07,0,.8,-.15, 6,.17,.04,-.05,.6,-.3, 10,-.1,.07,0,.8,-.15, 10,-.17,.04,-.05,.6,-.3,
            3,-.11,.31,-.12,.95,-.45, 3,.09,.32,-.13,1.05,-.5, 3,0,.22,-.17,.85,-.75, 3,-.2,.12,-.15,.6,-.7, 3,.2,.12,-.15,.6,-.7};
        public const int TongueCount=16;
        private readonly double[] acc=new double[Emitters.Length],tips=new double[TongueCount];private bool started,tipsStarted;
        private readonly double[] local=new double[16],world=new double[16];
        public static int EmitterCount=>Emitters.Length;
        /// <summary>A flame tongue's flicker (applySirenLook): height scale and lean/whip rotation at time t.</summary>
        public static void Tongue(int i,double t,double flare,double lean,out double scaleY,out double rx,out double rz)
        {
            double x=Tongues[i*6+1],height=Tongues[i*6+4],baseLean=Tongues[i*6+5],phase=i*1.7+x*9,n=.5+.5*Math.Sin(t*7.3+phase)*Math.Sin(t*3.1+phase*1.9);
            scaleY=height*(.7+.55*n)*(1+flare*.5);rx=baseLean-.5*lean+.14*Math.Sin(t*2.6+phase);rz=.24*Math.Sin(t*3.4+phase*1.3);
        }
        /// <summary>
        /// emitFlames: q is the effects quality, (vx, vz) the body's velocity, flare the hellfire/damnation flare, ranged the
        /// smoothed ranged-mode blend, lean the smoothed forward lean, t the clock the tongues flicker on.
        /// </summary>
        public void Emit(NativeFlameField flames,double[] joints,double dt,double quality,double vx,double vz,bool grabbing,double flare,double ranged,double t,double lean)
        {
            if(dt<=0)return;var R=flames.Random;if(!started){for(int i=0;i<acc.Length;i++)acc[i]=R();started=true;}
            double q=quality<.5?.45:quality,boost=1+flare*2+(grabbing?.5:0),fast=Math.Min(1,Math.Sqrt(vx*vx+vz*vz)/6),lifeK=1-.45*fast;
            if(!tipsStarted){for(int i=0;i<tips.Length;i++)tips[i]=R();tipsStarted=true;}
            // Each flame tongue sheds small tongues from its tip, so the fire keeps rising off the body in columns.
            for(int i=0;i<TongueCount;i++)
            {
                tips[i]+=7*q*boost*dt;if(tips[i]<1)continue;Tongue(i,t,flare,lean,out double sy,out double rx,out double rz);
                NativeFxMath.Compose(local,Tongues[i*6+1],Tongues[i*6+2],Tongues[i*6+3],rx,0,rz,1,sy,1);NativeFxMath.Multiply(world,joints,(int)Tongues[i*6]*16,local);
                NativeFxMath.Point(world,0,0,.36*sy,0,out double wx,out double wy,out double wz);
                while(tips[i]>=1){tips[i]-=1;flames.Emit(wx+(R()-.5)*.04,wy,wz+(R()-.5)*.04,-vx*.2+(R()-.5)*.08,1.1+R()*.8,-vz*.2+(R()-.5)*.08,.32+R()*.25,.05+R()*.025,R()<.05?NativeFlameTone.Ember:NativeFlameTone.Black,2.8,2.4);}
            }
            for(int i=0;i<Emitters.Length;i++)
            {
                var e=Emitters[i];double rate=e.Rate*q*boost*.65;if(e.Hands)rate*=1+ranged*3.2;acc[i]+=rate*dt;if(acc[i]<1)continue;
                NativeFxMath.Point(joints,e.Joint*16,e.X,e.Y,e.Z,out double wx,out double wy,out double wz);
                while(acc[i]>=1)
                {
                    acc[i]-=1;double r=e.Spread,jx=(R()-.5)*2*r,jy=(R()-.5)*r,jz=(R()-.5)*2*r;bool ember=R()<(e.Hands&&ranged>.5?.16:.06);double trail=e.Tail?.45:.22;
                    flames.Emit(wx+jx,wy+jy,wz+jz,-vx*trail+(R()-.5)*.16,e.Up*(.6+R()*.6)*(e.Tail?.5:1),-vz*trail+(R()-.5)*.16,(e.Tail?.4:.55)*(.7+R()*.6)*lifeK,e.Size*.85*(ember?.45:1)*(.75+R()*.5),ember?NativeFlameTone.Ember:R()<.18?NativeFlameTone.Smoke:NativeFlameTone.Black,e.Tail?.6:2.6,e.Tail?2.6:2.2);
                }
            }
        }
        /// <summary>The hellfire/damnation flare: 1 at the release, 0 beyond .2 s either side.</summary>
        public static double Flare(double t,double hitAt)=>Math.Max(0,1-Math.Abs(t-hitAt)/.2);
        /// <summary>Damnation windup progress (-1 when no charge shows): the ball swells overhead until the release.</summary>
        public static double Charge(double t,double at,double hitAt)=>t<hitAt+.02?NativeFxMath.Clamp01((t-at)/Math.Max(.05,hitAt-at)):-1;
        /// <summary>
        /// finishLook's Damnation charge: round(60·dt·quality·(1+3dm)) cubes on the swelling ball at root-local
        /// (0, 1.42-(1-dm)·.4, .12). root is the source-frame root matrix (row-major, offset 0).
        /// </summary>
        public static void ChargeFlames(NativeFlameField flames,double[] root,double dm,double dt,double quality)
        {
            var R=flames.Random;double r=.08+.44*dm*dm;NativeFxMath.Point(root,0,0,1.42-(1-dm)*.4,.12,out double cx,out double cy,out double cz);int n=NativeSirenFx.JsRound(60*dt*quality*(1+dm*3));
            for(int i=0;i<n;i++){double a=R()*Math.PI*2,u=R()*2-1,w=Math.Sqrt(1-u*u);flames.Emit(cx+Math.Cos(a)*w*r,cy+u*r,cz+Math.Sin(a)*w*r,Math.Cos(a)*w*.5,u*.5+.8,Math.Sin(a)*w*.5,.25+R()*.2,.06+.08*dm,R()<.08?NativeFlameTone.Ember:NativeFlameTone.Black,2.4,2);}
        }
        public void Reset(){started=tipsStarted=false;}
    }

    /// <summary>client/siren/first-person.ts: the Siren's first-person claws, palm orbs and hand flames.</summary>
    public static class NativeSirenView
    {
        public const double Swap=.35;
        /// <summary>The claw-to-orb blend: swap = clamp((now - modeAt)/.35); ranged mode shows orbs.</summary>
        public static double Ranged(bool ranged,double now,double modeAt){double swap=NativeFxMath.Clamp01((now-modeAt)/Swap);return ranged?swap:1-swap;}
        /// <summary>The arm pose (source Euler XYZ, radians) for side +1 (left) or -1 (right).</summary>
        public static void Arm(int side,double t,double ranged,out double ax,out double az,out double ex,out double hx,out double hz)
        {double sway=Math.Sin(t*1.7+side)*.03;ax=-1.05+sway;az=side*.18;ex=-1.0-.25*ranged;hx=.55-1.25*ranged;hz=side*(.15+.2*ranged);}
        public static readonly double[] ClawX={-.045,0,.045};
        /// <summary>The orb scale for hand i (0 left, 1 right).</summary>
        public static double Orb(int i,double t,double ranged)=>.075*ranged*(1+.12*Math.Sin(t*12+i));
        /// <summary>
        /// Hand flames in view-rig space: accumulator acc, hands given as row-major 4x4 matrices relative to the view root
        /// (source frame); returns the new accumulator. The caller then advances the field by the same clamped dt.
        /// </summary>
        public static double Flames(NativeFlameField flames,double acc,double dt,double ranged,double[] left,double[] right)
        {
            var R=flames.Random;acc+=Math.Max(0,Math.Min(.05,dt))*(10+30*ranged);
            while(acc>=1)
            {
                acc-=1;var hand=R()<.5?left:right;NativeFxMath.Point(hand,0,(R()-.5)*.08,-.12,.06,out double x,out double y,out double z);
                flames.Emit(x,y,z,(R()-.5)*.1,.35+R()*.3,(R()-.5)*.1,.35+R()*.2,.025+R()*.02,R()<.1?NativeFlameTone.Ember:NativeFlameTone.Black,1.2,2.4);
            }
            return acc;
        }
    }

    /// <summary>
    /// client/siren/fx.ts updateMarks and markBeeps at 05950ea: Marked's "You...." crosshair, the lock-on telegraph that the
    /// Siren's fourth ranged move (keys 1-4 in ranged mode: Damnation, Eruption, Black Rain, Marked) puts on the marked player.
    /// It shows from lockAt (the voice line ends, siren-lock) while the mark lives, centred <see cref="Lift"/> above the
    /// target's body centre and turned with the camera (group.quaternion = camera.quaternion), over everything (depthTest
    /// false, renderOrder 999) in one transparent DoubleSide glowMaterial of PALETTE.warn that each beep turns white. In the
    /// billboard plane (x right, y up, before the group scale): RingGeometry(.86, 1, 64) turning, RingGeometry(.12, .17, 4)
    /// turned 45 degrees (a square outline) and four TICK boxes .06 x .32 x .01 centred .98 out, which swing .8 rad into the
    /// cardinal points over the lock. The group closes from 1.6 to .85 and swells 12% on each beep; once the seeker is thrown
    /// it holds .85, turns slowly and pulses its opacity. The beeps (siren-audio.ts) climb from 550 Hz at the same instants.
    /// </summary>
    public static class NativeSirenMark
    {
        /// <summary>SIREN_TIMING.mark (shared/siren-contract.ts): the "You...." voice, then the lock until the seeker is thrown.</summary>
        public const double Voice=1.2,Lock=1.2,Recover=.5;
        public const double Lift=.35,RingInner=.86,SquareInner=.12,SquareOuter=.17,SquareTurn=Math.PI/4,TickRadius=.98,TickWidth=.06,TickLength=.32,TickSwing=.8;
        public const int RingSegments=64,SquareSegments=4,TickCount=4,BeepCap=40;
        public const double FirstGap=.3,MinGap=.07,GapDecay=.8,BeepTail=.02,BeepFlash=.09,BeepSwell=.12,WhiteAt=.3;
        public const double StartSize=1.6,Closing=.75,ThrownSize=.85,Spin=2.4,ThrownSpin=.8,LowTone=550,ToneRise=850;
        /// <summary>PALETTE.warn, and the beep flash (uColor 0xffffff); glowMaterial colours are raw ShaderMaterial colours.</summary>
        public const int Warn=NativeSirenFx.Warn,Flash=0xffffff;
        /// <summary>
        /// The marked player's own first-person view (Unity readability, owner playtest 2026-10-10: "people won't know they're
        /// getting targeted"). The source centres the billboard .17 m under the eye (Lift .35 against eyeHeight .52), inside the
        /// near plane, so a first-person target only hears the beeps. Here the same crosshair is held ScreenDistance in front of
        /// the eye with its ring spanning ScreenReach of the half-height at lock start (it then closes to .85/1.6 of that).
        /// </summary>
        public const double ScreenDistance=1,ScreenReach=.9;
        /// <summary>A seeker steers at its target's body centre lifted .2 m (Runtime/NativeSiren.cs); with no record seen, the
        /// crosshair goes on the player its velocity points at within this cosine.</summary>
        public const double SeekerAim=.2,AimCos=.97;

        /// <summary>markBeeps: from lockAt, gaps of .3 s shrinking by .8 toward .07 s, before throwAt - .02 (at most 40).
        /// Writes up to into.Length instants into <paramref name="into"/> (may be null) and returns how many there are.</summary>
        public static int Beeps(double lockAt,double throwAt,double[] into=null)
        {
            int n=0;double t=lockAt,gap=FirstGap;
            while(t<throwAt-BeepTail&&n<BeepCap){if(into!=null&&n<into.Length)into[n]=t;n++;t+=gap;gap=Math.Max(MinGap,gap*GapDecay);}
            return n;
        }
        /// <summary>siren-audio.ts: the beep at <paramref name="at"/> sounds at 550 + u·850 Hz, u its place in the lock.</summary>
        public static double Tone(double lockAt,double throwAt,double at)=>LowTone+(at-lockAt)/(throwAt-lockAt)*ToneRise;
        /// <summary>updateMarks beep: 1 - age/.09 for a beep under .09 s old (the latest wins), else 0.</summary>
        public static double Beep(double lockAt,double throwAt,double time)
        {
            double beep=0,t=lockAt,gap=FirstGap;
            for(int n=0;t<throwAt-BeepTail&&n<BeepCap;n++){if(time>=t&&time-t<BeepFlash)beep=1-(time-t)/BeepFlash;t+=gap;gap=Math.Max(MinGap,gap*GapDecay);}
            return beep;
        }
        /// <summary>
        /// updateMarks for a shown mark at <paramref name="time"/>: the group scale (beep swell included), the ring's turn
        /// (rotation.z), the ticks' swing past the cardinal points, the shared material's opacity and whether it flashes white.
        /// </summary>
        public static void Look(double lockAt,double throwAt,double time,out double scale,out double spin,out double tickTurn,out double opacity,out bool white)
        {
            bool thrown=time>=throwAt;double u=NativeFxMath.Clamp01((time-lockAt)/Math.Max(.1,throwAt-lockAt)),size=thrown?ThrownSize:StartSize-Closing*NativeFxMath.Smooth(u);
            double beep=Beep(lockAt,throwAt,time);
            scale=size*(1+BeepSwell*beep);spin=time*(thrown?ThrownSpin:Spin);tickTurn=thrown?0:(1-u)*TickSwing;
            opacity=thrown?.75+.2*Math.Sin(time*6):.85+.15*beep;white=beep>WhiteAt;
        }
        /// <summary>Tick i: its centre (x right, y up, before the group scale) and a, the angle from up toward right of its long
        /// (radial) axis; the source turns the box by rotation.z = -a.</summary>
        public static void Tick(int i,double tickTurn,out double x,out double y,out double a){a=i*Math.PI/2+tickTurn;x=Math.Sin(a)*TickRadius;y=Math.Cos(a)*TickRadius;}
        /// <summary>World units per crosshair unit for the first-person target, from the camera's vertical field of view.</summary>
        public static double ScreenUnit(double verticalFovDegrees)=>ScreenDistance*Math.Tan(verticalFovDegrees*Math.PI/360)*ScreenReach/StartSize;
        /// <summary>Cosine between a seeker's velocity and the way from it to a candidate's aim point.</summary>
        public static double Aim(double sx,double sy,double sz,double vx,double vy,double vz,double tx,double ty,double tz)
        {
            double dx=tx-sx,dy=ty-sy,dz=tz-sz,d=Math.Sqrt(dx*dx+dy*dy+dz*dz),v=Math.Sqrt(vx*vx+vy*vy+vz*vz);
            return d<1e-6||v<1e-6?-1:(dx*vx+dy*vy+dz*vz)/(d*v);
        }
    }

    /// <summary>
    /// Who wears the Marked crosshair this frame, from what host, guests and the killcam all hold: the mark records
    /// (NativeMarkView: owner, target, lockAt, throwAt) and the Siren's seekers. The source keeps a SirenMark from the cast
    /// until its seeker is gone (or the target dies) and draws the crosshair on that target from lockAt; the native record
    /// ends with the move's recovery .5 s after the throw, so a live seeker keeps its owner's crosshair on the target the last
    /// record named (or the authority's own seeker target, or, with no record seen, the player the seeker flies at). Only the
    /// marked player wears it. Allocation free; Begin, then Record and Seeker, then End, once per frame.
    /// </summary>
    public sealed class NativeSirenMarkTracker
    {
        public const int Capacity=32;
        public struct Mark{public int Owner,Target;public double LockAt,ThrowAt;}
        private struct Memo{public int Owner,Target,Frame,RecordFrame;public double LockAt,ThrowAt;}
        private readonly Memo[] memos=new Memo[Capacity];private readonly Mark[] shown=new Mark[Capacity];private int memoCount,count,frame;
        public int Count=>count;
        public Mark this[int i]=>shown[i];
        public void Clear(){memoCount=count=0;}
        public void Begin(){frame++;count=0;}
        private int Find(int owner){for(int i=0;i<memoCount;i++)if(memos[i].Owner==owner)return i;return -1;}
        private void Show(int owner,int target,double lockAt,double throwAt){if(count<Capacity)shown[count++]=new Mark{Owner=owner,Target=target,LockAt=lockAt,ThrowAt=throwAt};}
        /// <summary>A mark record: remembered for its seeker, shown from lockAt while its target lives.</summary>
        public void Record(int owner,int target,double lockAt,double throwAt,bool targetAlive,double time)
        {
            int i=Find(owner);if(i<0){if(memoCount==Capacity)return;i=memoCount++;}
            memos[i]=new Memo{Owner=owner,Target=target,LockAt=lockAt,ThrowAt=throwAt,Frame=frame,RecordFrame=frame};
            if(targetAlive&&time>=lockAt)Show(owner,target,lockAt,throwAt);
        }
        /// <summary>The target the owner's last record (or seeker) named; 0 when none was seen.</summary>
        public int Remembered(int owner){int i=Find(owner);return i<0?0:memos[i].Target;}
        /// <summary>
        /// A live seeker of <paramref name="owner"/> flying at <paramref name="target"/> (0 when unknown): keeps the crosshair once
        /// the record is gone. With no record seen, the crosshair starts thrown (lock and throw at the seeker's birth).
        /// </summary>
        public void Seeker(int owner,int target,double born,bool targetAlive,double time)
        {
            int i=Find(owner);if(i>=0&&memos[i].RecordFrame==frame)return;
            if(i<0){if(memoCount==Capacity||target==0)return;i=memoCount++;memos[i]=new Memo{Owner=owner,LockAt=born,ThrowAt=born,RecordFrame=-1};}
            if(target!=0)memos[i].Target=target;memos[i].Frame=frame;
            if(targetAlive&&memos[i].Target!=0&&time>=memos[i].LockAt)Show(owner,memos[i].Target,memos[i].LockAt,memos[i].ThrowAt);
        }
        /// <summary>Forgets owners with neither a record nor a seeker this frame.</summary>
        public void End(){int k=0;for(int i=0;i<memoCount;i++)if(memos[i].Frame==frame)memos[k++]=memos[i];memoCount=k;}
    }

    /// <summary>client/pve/gore.ts black flame: hit puffs, the gib spray and puff, and stump drips (C17's FlameField part).</summary>
    public static class NativeGoreFlames
    {
        public const double DripRate=6;public const int HitFlames=2,GibFlames=12;
        /// <summary>hit(): two black cubes pushed along the hit direction.</summary>
        public static void Hit(NativeFlameField f,double x,double y,double z,double hitYaw)
        {double dx=Math.Sin(hitYaw),dz=Math.Cos(hitYaw);for(int i=0;i<HitFlames;i++)f.Emit(x+dx*.1,y,z+dz*.1,dx*.8,.6,dz*.8,.55,.09,NativeFlameTone.Black,1.8);}
        /// <summary>gib(): the soot spray cone (hash-seeded) along from (0,0,1 when absent) and a black puff.</summary>
        public static void Gib(NativeFlameField f,uint seed,double x,double y,double z,bool hasFrom,double fx,double fy,double fz,double quality)
        {
            if(!hasFrom){fx=0;fy=0;fz=1;}double len=Math.Sqrt(fx*fx+fy*fy+fz*fz);if(len==0)len=1;double dx=fx/len,dz=fz/len,k=Math.Max(.3,Math.Min(1,quality));
            int spray=NativeSirenFx.JsRound(10+(18-10)*k);
            for(int i=0;i<spray;i++)
            {
                double a=(NativeGoreRules.Hash01(seed,i,1)-.5)*.9,c=Math.Cos(a),s=Math.Sin(a),sp=2+2.5*NativeGoreRules.Hash01(seed,i,2);
                f.Emit(x,y,z,(dx*c-dz*s)*sp,.8+1.6*NativeGoreRules.Hash01(seed,i,3),(dx*s+dz*c)*sp,.45+.3*NativeGoreRules.Hash01(seed,i,4),.05+.04*NativeGoreRules.Hash01(seed,i,5),NativeFlameTone.Soot,-4.5,1.2);
            }
            f.Burst(x,y,z,NativeSirenFx.JsRound(GibFlames*k),1.1,.7,.12,NativeFlameTone.Black,1.1,2.0);
        }
        /// <summary>drip(): black flame dripping off a stump, about six a second; returns the accumulator.</summary>
        public static double Drip(NativeFlameField f,double x,double y,double z,double dt,double quality,double acc)
        {acc+=dt*DripRate*Math.Max(.3,quality);while(acc>=1){acc-=1;f.Emit(x,y,z,0,-.4,0,.5,.06,NativeFlameTone.Black,1.4,2.2);}return acc;}
        /// <summary>horde-view.ts hash(n, k): a stump's first drip phase.</summary>
        public static double Phase(int n,int k)=>(uint)((uint)((n+1)*unchecked((int)0x9e3779b1))^(uint)((k+1)*unchecked((int)0x85ebca77)))/4294967296.0;
    }
}
