using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A proc look's kind (NativeRoundsProcs).</summary>
    public enum RoundsProcKind:byte{None,Phase,Packet,Grapple,Tractor,Stasis,Execute,Reclaim,Transit,Shield,Pulse,ArcFault,Fragment,Thorns,BlockHeal,Regen,ReloadHeal}

    /// <summary>A pilot's body this frame for the procs: seat, presented body centre, alive, its pack proc looks and stasis look.</summary>
    public struct RoundsBody{public int Seat;public Vector3 Body;public bool Alive;public RoundsProc Procs;public int Stasis;public float FrozenUntil;}

    /// <summary>
    /// The ROUNDS packs' procs, pulses and buffs that had no look of their own, each from a replicated event (the authority's
    /// own Block, Hit, Freeze, Teleport, Field, BlockPulse, Explosion, Heal and ReloadEnd events, the same on the host, every guest
    /// and the killcam) aged on the presentation clock:
    /// <list type="bullet">
    /// <item>Phase Reserve: a phase shimmer round the blocker for the .6 s protection (one per block activation).</item>
    /// <item>Packet Theft: data packets lifted off the target and streamed into the thief on each direct hit.</item>
    /// <item>Grapple Driver, Tractor Hook: a hook chain or a tractor beam from the holder to the pulled target.</item>
    /// <item>The block interdicts' freeze (Disruption Pulse, Lunar Interdict, Shadow Interdict): a stasis cage round each frozen
    /// pilot to its replicated frozenUntil.</item>
    /// <item>Finish Protocol: a finisher slash where a direct hit lands on a target already below 30% health.</item>
    /// <item>Reclamation Core, Mulligan Reserve: the body dissolving where it was, rebuilt where it blinks to, and its 1.2 s
    /// protection shell. Impact Transit: entry and exit apertures. Emergency Vent: its 2 s protection shell.</item>
    /// <item>Guard Discharge, Charge Discharge, Arc Fault, Fragment Driver: a ring on the floor exactly the pulse's radius (the
    /// authority's 3 m and 1.8 m blasts) with its own arcs, impulse streaks or fragments over the vanilla blast.</item>
    /// <item>Thorn Plating, Retribution Frame: spikes off the holder and a return spark to the attacker. The block heals, the
    /// regeneration and Mending Loader: restore rings and repair motes on the holder.</item>
    /// </list>
    /// A fixed store; allocation free.
    /// </summary>
    public sealed class NativeRoundsProcs
    {
        public struct Item{public RoundsProcKind Kind;public int Seat,Target,Variant,Id;public float At,Until,Floor;public Vector3 A,B;public float Radius;}
        public const int Capacity=96;
        public readonly Item[] Items=new Item[Capacity];public int Count;
        private readonly float[] regenUntil=new float[16];private readonly int[] regenSeat=new int[16];
        /// <summary>How long each kind draws (s), past its own until for those that carry one.</summary>
        public static float Span(RoundsProcKind k)=>k==RoundsProcKind.Phase?.6f:k==RoundsProcKind.Packet?.6f:k==RoundsProcKind.Grapple||k==RoundsProcKind.Tractor?.4f:k==RoundsProcKind.Execute?.35f:k==RoundsProcKind.Reclaim?1.2f:k==RoundsProcKind.Transit?.4f:k==RoundsProcKind.Pulse||k==RoundsProcKind.ArcFault||k==RoundsProcKind.Fragment?.55f:k==RoundsProcKind.Thorns?.35f:k==RoundsProcKind.BlockHeal||k==RoundsProcKind.ReloadHeal?.6f:.3f;
        /// <summary>No proc live and nobody regenerating.</summary>
        public bool Idle{get{if(Count>0)return false;for(int i=0;i<regenSeat.Length;i++)if(regenSeat[i]!=0)return false;return true;}}
        public void Clear(){Count=0;for(int i=0;i<regenSeat.Length;i++){regenSeat[i]=0;regenUntil[i]=-1e9f;}}
        public void Rewind(float clock){int k=0;for(int i=0;i<Count;i++){if(Items[i].At>clock+.05f)continue;Items[k++]=Items[i];}Count=k;}
        private void Add(in Item it)
        {
            for(int i=0;i<Count;i++)if(Items[i].Id==it.Id&&Items[i].Kind==it.Kind&&Items[i].Target==it.Target)return;
            if(Count<Items.Length){Items[Count++]=it;return;}
            int old=0;for(int i=1;i<Count;i++)if(Items[i].At<Items[old].At)old=i;Items[old]=it;
        }
        private bool Recent(RoundsProcKind k,int seat,float at,float within){for(int i=0;i<Count;i++)if(Items[i].Kind==k&&Items[i].Seat==seat&&Math.Abs(Items[i].At-at)<within)return true;return false;}
        private static Vector3 V(V3 v)=>new Vector3(v.x,v.y,v.z);
        /// <summary>
        /// One replicated event. `source`: the procs of its Source pilot (the attacker, the blocker, the healed); `targetPilot`
        /// whether its Target is a pilot; `executes`: a hit on a target whose health before it was below 30% of its maximum (the
        /// presenter reads the replicated health); `stasis`: the source's stasis look; `floor`: the floor under the event's point
        /// (the presenter's raycast; the pulse rings lie on it).
        /// </summary>
        public void Event(in ArenaEvent e,RoundsProc source,bool targetPilot,bool executes,int stasis,float floor)
        {
            if(source==RoundsProc.None)return;var at=V(e.Position);float t=e.Time;
            switch(e.Kind)
            {
                case EventKind.Block:
                    if((source&RoundsProc.Phase)!=0&&!Recent(RoundsProcKind.Phase,e.Source,t,.65f))Add(new Item{Kind=RoundsProcKind.Phase,Seat=e.Source,Id=e.Id,At=t,Until=t+.6f});
                    if((source&RoundsProc.BlockHeal)!=0&&!Recent(RoundsProcKind.BlockHeal,e.Source,t,.65f))Add(new Item{Kind=RoundsProcKind.BlockHeal,Seat=e.Source,Id=e.Id,At=t,Until=t+.6f});
                    break;
                case EventKind.Hit:case EventKind.Headshot:
                    if(e.Detail=="thorns"){if((source&RoundsProc.Thorns)!=0)Add(new Item{Kind=RoundsProcKind.Thorns,Seat=e.Source,Target=e.Target,Id=e.Id,At=t,Until=t+.35f,A=at});break;}
                    if(!targetPilot||!(e.Amount>0)||e.Detail!="direct"&&e.Detail!="melee")break;
                    if((source&RoundsProc.Packet)!=0)Add(new Item{Kind=RoundsProcKind.Packet,Seat=e.Source,Target=e.Target,Id=e.Id,At=t,Until=t+.6f,A=at});
                    if((source&RoundsProc.Grapple)!=0)Add(new Item{Kind=RoundsProcKind.Grapple,Seat=e.Source,Target=e.Target,Id=e.Id,At=t,Until=t+.4f,A=at});
                    if((source&RoundsProc.Tractor)!=0)Add(new Item{Kind=RoundsProcKind.Tractor,Seat=e.Source,Target=e.Target,Id=e.Id,At=t,Until=t+.4f,A=at});
                    if((source&RoundsProc.Execute)!=0&&executes)Add(new Item{Kind=RoundsProcKind.Execute,Seat=e.Source,Target=e.Target,Id=e.Id,At=t,Until=t+.35f,A=at,B=V(e.Normal)});
                    break;
                case EventKind.Freeze:
                    if((source&RoundsProc.Stasis)!=0)Add(new Item{Kind=RoundsProcKind.Stasis,Seat=e.Source,Target=e.Target,Id=e.Id,At=t,Until=t+Math.Max(.1f,e.Amount),Variant=stasis,A=at});
                    break;
                case EventKind.Teleport:
                    if((source&RoundsProc.Reclaim)!=0)Add(new Item{Kind=RoundsProcKind.Reclaim,Seat=e.Source,Id=e.Id,At=t,Until=t+1.2f,A=V(e.From),B=at});
                    else if((source&RoundsProc.Transit)!=0&&e.Detail==null)Add(new Item{Kind=RoundsProcKind.Transit,Seat=e.Source,Id=e.Id,At=t,Until=t+.4f,A=V(e.From),B=at});
                    break;
                case EventKind.Field:
                    if((source&RoundsProc.Vent)!=0&&e.Detail=="panic-button")Add(new Item{Kind=RoundsProcKind.Shield,Seat=e.Source,Id=e.Id,At=t,Until=t+2,Variant=1});
                    break;
                case EventKind.BlockPulse:
                    if((source&(RoundsProc.GuardPulse|RoundsProc.ChargePulse))!=0)Add(new Item{Kind=RoundsProcKind.Pulse,Seat=e.Source,Id=e.Id,At=t,Until=t+.55f,A=at,Floor=floor,Radius=e.Radius>0?e.Radius:3,Variant=(source&RoundsProc.ChargePulse)!=0?1:0});
                    break;
                case EventKind.Explosion:
                    if(e.Detail=="static"&&(source&RoundsProc.ArcFault)!=0)Add(new Item{Kind=RoundsProcKind.ArcFault,Seat=e.Source,Id=e.Id,At=t,Until=t+.55f,A=at,Floor=floor,Radius=e.Radius>0?e.Radius:3});
                    else if(e.Detail=="shrapnel"&&(source&RoundsProc.Fragment)!=0)Add(new Item{Kind=RoundsProcKind.Fragment,Seat=e.Source,Id=e.Id,At=t,Until=t+.55f,A=at,Floor=floor,Radius=e.Radius>0?e.Radius:1.8f});
                    break;
                case EventKind.Heal:
                    if((source&RoundsProc.Regen)!=0&&e.Source==e.Target)Regen(e.Source,t);
                    break;
                case EventKind.ReloadEnd:
                    if((source&RoundsProc.ReloadHeal)!=0)Add(new Item{Kind=RoundsProcKind.ReloadHeal,Seat=e.Source,Id=e.Id,At=t,Until=t+.6f});
                    break;
            }
        }
        // Regeneration heals every tick: a seat counts as regenerating for .3 s after its latest heal.
        private void Regen(int seat,float t)
        {
            int free=-1;for(int i=0;i<regenSeat.Length;i++){if(regenSeat[i]==seat){regenUntil[i]=Math.Max(regenUntil[i],t+.3f);return;}if(free<0&&regenSeat[i]==0)free=i;}
            if(free>=0){regenSeat[free]=seat;regenUntil[free]=t+.3f;}
        }
        /// <summary>Whether `seat` regenerates at `clock` (checks).</summary>
        public bool Regenerating(int seat,float clock){for(int i=0;i<regenSeat.Length;i++)if(regenSeat[i]==seat&&seat!=0)return clock<=regenUntil[i];return false;}
        private static bool Body(RoundsBody[] bodies,int n,int seat,out RoundsBody b){for(int i=0;i<n;i++)if(bodies[i].Seat==seat){b=bodies[i];return true;}b=default;return false;}
        private static float H(uint s,int i)=>BossMath.Hash(s,i);
        private static readonly Vector3 Cyan=Lin(0x6ff6ff),Gold=Lin(0xffd27a),Green=Lin(0x7dffa8),Red=Lin(0xff4a5a),Violet=Lin(0xc79bff),Teal=Lin(0x78f7e7),White=new Vector3(1,1,1);
        private static Vector3 Lin(int hex){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Vector3((float)r,(float)g,(float)b);}
        /// <summary>The pulse rings' ground looks: Guard Discharge, Charge Discharge, Arc Fault, Fragment Driver.</summary>
        public static readonly RoundsFieldLook GuardRing=NativeRoundsLooks.Pulse(0,0x78f7e7,0xeafffb,0x1aa6a0),ChargeRing=NativeRoundsLooks.Pulse(0,0xffd27a,0xfff6dc,0xd08a1a),ArcRing=NativeRoundsLooks.Pulse(1,0xc79bff,0xf6eeff,0x6a2fd8),FragRing=NativeRoundsLooks.Pulse(2,0xffb070,0xfff0e0,0x8a4a2a);
        /// <summary>Draws every live proc at `clock` (the pulse rings into `ground`).</summary>
        public void Draw(float clock,RoundsBody[] bodies,int bodyCount,NativeBossVfx fx,RoundsLit lit,RoundsSurface ground,bool low)
        {
            int keep=0;
            for(int i=0;i<Count;i++)
            {
                var it=Items[i];float age=clock-it.At;
                if(age>Math.Max(it.Until-it.At,Span(it.Kind))+.4f)continue;Items[keep++]=it;if(age<0)continue;
                uint seed=(uint)it.Id*2654435761u+(uint)it.Kind;
                Body(bodies,bodyCount,it.Seat,out var me);Body(bodies,bodyCount,it.Target,out var them);
                switch(it.Kind)
                {
                    case RoundsProcKind.Phase:if(me.Seat!=0)Shell(fx,me.Body,age,it.Until-it.At,Cyan,seed,true);break;
                    case RoundsProcKind.Shield:if(me.Seat!=0)Shell(fx,me.Body,age,it.Until-it.At,Red,seed,false);break;
                    case RoundsProcKind.Packet:Packets(fx,lit,it,age,me,seed);break;
                    case RoundsProcKind.Grapple:case RoundsProcKind.Tractor:if(me.Seat!=0&&them.Seat!=0)Tether(fx,lit,it.Kind==RoundsProcKind.Grapple,me.Body,them.Body,age,seed);break;
                    case RoundsProcKind.Stasis:{float until=Math.Max(it.Until,them.Seat!=0?them.FrozenUntil:0);if(them.Seat!=0&&clock<until+.25f)Stasis(fx,lit,them.Body,age,until-it.At,it.Variant,seed);break;}
                    case RoundsProcKind.Execute:Slash(fx,it.A,it.B,age,seed);break;
                    case RoundsProcKind.Reclaim:Reclaim(fx,it,age,me,seed);break;
                    case RoundsProcKind.Transit:Aperture(fx,it.A,it.B,age,seed);Aperture(fx,it.B,it.A,age-.05f,seed+9);break;
                    case RoundsProcKind.Pulse:case RoundsProcKind.ArcFault:case RoundsProcKind.Fragment:Ring(fx,ground,it,age,seed,low);break;
                    case RoundsProcKind.Thorns:if(me.Seat!=0)Thorns(fx,me.Body,them.Seat!=0?them.Body:it.A,age,seed);break;
                    case RoundsProcKind.BlockHeal:case RoundsProcKind.ReloadHeal:if(me.Seat!=0)Restore(fx,me.Body,age,it.Kind==RoundsProcKind.ReloadHeal,seed);break;
                }
            }
            Count=keep;
            for(int i=0;i<regenSeat.Length;i++){if(regenSeat[i]!=0&&clock>regenUntil[i]+1){regenSeat[i]=0;continue;}if(regenSeat[i]==0||clock>regenUntil[i])continue;if(Body(bodies,bodyCount,regenSeat[i],out var b)&&b.Alive)Repair(fx,b.Body,clock,(uint)regenSeat[i]);}
        }
        // A shell of flickering motes round the body (Phase Reserve's phase, a protection shell), fading over its last .15 s, with
        // two colour-split ghosts jittering beside the body for the phase.
        private static void Shell(NativeBossVfx fx,Vector3 c,float age,float life,Vector3 rgb,uint seed,bool phase)
        {
            if(age>life)return;float fade=1-BossMath.Smooth((age-(life-.15f))/.15f),pop=age<.08f?1+3*(1-age/.08f):1;int n=fx.Scale(phase?42:34);
            for(int i=0;i<n;i++)
            {
                float y=1-2*(i+.5f)/n,r=(float)Math.Sqrt(Math.Max(0,1-y*y)),ph=i*2.39996f+age*(phase?1.6f:.6f);var d=new Vector3((float)Math.Cos(ph)*r,y*1.25f,(float)Math.Sin(ph)*r);
                float fl=phase?((int)(age*30+i*3)%4==0?.25f:1):.7f+.3f*(float)Math.Sin(age*8+i);fx.Glow(c+d*.62f,rgb*(1.3f*fade*fl*pop),.07f,NativeBossVfx.Mote);
            }
            if(phase)for(int g=-1;g<=1;g+=2){float j=(H(seed,(int)(age*40)+g*7)-.5f)*.12f;fx.Glow(c+new Vector3(g*.09f+j,.1f,0),(g<0?new Vector3(1.4f,.2f,.5f):new Vector3(.2f,.9f,1.6f))*(.6f*fade),.9f,NativeBossVfx.Mote);}
        }
        // Three data packets lifted off the target and streamed along an arc into the thief, each with a short trail, an absorb flash.
        private static void Packets(NativeBossVfx fx,RoundsLit lit,in Item it,float age,in RoundsBody me,uint seed)
        {
            var to=me.Seat!=0?me.Body+new Vector3(0,.3f,0):it.A;
            for(int k=0;k<3;k++)
            {
                float t0=k*.07f,u=(age-t0)/.42f;if(u<0||u>1.2f)continue;float w=BossMath.Smoother(Math.Min(1,u));var lift=new Vector3(0,.5f+.25f*k,0);
                var p=Vector3.Lerp(it.A,to,w)+lift*(4*w*(1-w));var m=Matrix4x4.CreateRotationY(age*9+k)*Matrix4x4.CreateRotationX(age*7)*Matrix4x4.CreateTranslation(p);
                if(u<=1){lit.Add(NativeRoundsMeshes.Packet,m,new Vector3(.05f,.06f,.07f),.8f,Cyan*2.2f,.7f,Cyan*1.6f);fx.Glow(p,Cyan*1.4f,.16f,NativeBossVfx.Core);
                    var q=Vector3.Lerp(it.A,to,BossMath.Smoother(Math.Max(0,u-.12f)))+lift*(4*Math.Max(0,w-.1f)*(1-Math.Max(0,w-.1f)));fx.Spark(p,p-q,Cyan*1.2f,.03f,(p-q).Length(),1,.9f);}
                else fx.Glow(to,Cyan*(2.5f*(1.2f-u)/.2f),.4f,NativeBossVfx.Core);
            }
            if(age<.12f)fx.Glow(it.A,Cyan*(2*(1-age/.12f)),.35f,NativeBossVfx.RingGlow);
        }
        // A hook chain (lit links along the line, the hook at the target) or a tractor beam (a column with rings running to the
        // holder) from the holder's hand height to the target as it is pulled; it snaps out in .06 s and fades after .3 s.
        private static void Tether(NativeBossVfx fx,RoundsLit lit,bool chain,Vector3 me,Vector3 them,float age,uint seed)
        {
            var a=me+new Vector3(0,.35f,0);var b=them+new Vector3(0,.2f,0);float out1=BossMath.Smooth(age/.06f),fade=1-BossMath.Smooth((age-.28f)/.12f);if(fade<=0)return;var end=Vector3.Lerp(a,b,out1);var d=end-a;float len=d.Length();if(len<.05f)return;var dir=d/len;
            if(chain)
            {
                var col=Lin(0xffa040);int links=Math.Min(40,(int)(len/.07f));float yaw=(float)Math.Atan2(dir.X,dir.Z),pitch=-(float)Math.Asin(BossMath.Clamp(dir.Y,-1,1));
                for(int k=0;k<links;k++){var p=a+dir*(k*.07f+.035f);var m=Matrix4x4.CreateRotationZ(k%2==0?0:1.5708f)*Matrix4x4.CreateRotationX(1.5708f)*Matrix4x4.CreateRotationX(pitch)*Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateTranslation(p);lit.Add(NativeRoundsMeshes.Link,m,new Vector3(.18f,.17f,.16f),1,col*(.4f*fade),.8f,col*fade);}
                lit.Add(NativeRoundsMeshes.Hook,Matrix4x4.CreateRotationX(pitch)*Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateTranslation(end),new Vector3(.2f,.2f,.22f),1.2f,col*(1.2f*fade),.85f,col*2);
                fx.Glow(end,col*(2*fade),.25f,NativeBossVfx.Core);
            }
            else
            {
                var col=Lin(0x8dffc8);fx.Spark(end,end-a,col*(1.4f*fade),.06f,len,1,.6f);fx.Spark(end,end-a,col*(.6f*fade),.2f,len,1,.5f);
                for(int k=0;k<5;k++){float u=1-((age*2.2f+k*.2f)%1);fx.Glow(a+d*u,col*(1.8f*fade),.22f,NativeBossVfx.RingGlow);}
            }
        }
        // A stasis cage round a frozen pilot: three counter-turning rings of light round its body, a shimmer up its height, the
        // look's tint (Disruption: cyan and magenta glitch; Lunar: silver; Shadow: violet with dark wisps), snapping out at its end.
        private static void Stasis(NativeBossVfx fx,RoundsLit lit,Vector3 c,float age,float life,int variant,uint seed)
        {
            var hot=variant==1?Lin(0xd8e4ff):variant==2?Lin(0xa46bff):Cyan;var alt=variant==1?White:variant==2?Lin(0x3b1478):Lin(0xff4fd8);
            float end=age-life,on=end<0?BossMath.Smooth(age/.08f):1-BossMath.Smooth(end/.25f);if(on<=0)return;float burst=end>0?end/.25f:0;
            for(int r=0;r<3;r++)
            {
                float y=(r-1)*.5f,rad=(.55f+.08f*(r==1?1:0))*(1+burst*.8f),spin=age*(r%2==0?1.4f:-1.1f)+r;int seg=fx.Low?12:20;
                for(int k=0;k<seg;k++){if(variant==0&&((int)(age*20)+k+r)%7==0)continue;float a0=spin+k*6.2831853f/seg,a1=spin+(k+.75f)*6.2831853f/seg;var p0=c+new Vector3((float)Math.Cos(a0)*rad,y,(float)Math.Sin(a0)*rad);var p1=c+new Vector3((float)Math.Cos(a1)*rad,y,(float)Math.Sin(a1)*rad);fx.Spark(p1,p1-p0,(k%5==0?alt:hot)*(1.5f*on),.02f,(p1-p0).Length(),1,.95f);}
            }
            int m=fx.Scale(16);for(int i=0;i<m;i++){float a=i*2.39996f,y=((age*.35f+i/(float)m)%1)*1.9f-.95f;fx.Glow(c+new Vector3((float)Math.Cos(a)*.5f,y,(float)Math.Sin(a)*.5f),hot*(.9f*on),.06f,NativeBossVfx.Mote);}
            if(variant==2)fx.SmokeFlow(age,c-new Vector3(0,.8f,0),Vector3.Zero,.3f,.15f,6,1,.3f,.8f,new Vector3(.02f,.012f,.03f),.35f*on,0,life,seed,1,glow:.15f);
        }
        // Finish Protocol's finisher: two crossing slashes over the hit point with sparks.
        private static void Slash(NativeBossVfx fx,Vector3 at,Vector3 normal,float age,uint seed)
        {
            float u=age/.35f;if(u>1)return;float grow=BossMath.Smooth(u/.25f),fade=1-BossMath.Smooth((u-.4f)/.6f);var col=Red*2.4f+new Vector3(.6f,.4f,.4f);
            for(int s=-1;s<=1;s+=2){var d=Vector3.Normalize(new Vector3(.7f*s,.7f,.2f));var a=at-d*(.45f*grow);var b=at+d*(.45f*grow);fx.Spark(b,b-a,col*fade,.05f,(b-a).Length(),1,.3f);}
            if(u<.3f)fx.Glow(at,col*(1.5f*(1-u/.3f)),.6f,NativeBossVfx.Core);fx.SparkBurst(age,at,normal.LengthSquared()>.01f?normal:Vector3.UnitY,1.2f,4,10,.3f,0,.02f,seed,1,1.2f,.015f,1.2f,false,-1e9f);
        }
        // The emergency blink: the body dissolves into motes rushing into its old centre, then is rebuilt by scan rings sweeping up
        // where it lands, inside a gold protection shell for its 1.2 s.
        private static void Reclaim(NativeBossVfx fx,in Item it,float age,in RoundsBody me,uint seed)
        {
            if(age<.3f){int n=fx.Scale(30);for(int i=0;i<n;i++){float u=age/.3f;var d=NativeBossVfx.Cone(Vector3.UnitY,2,H(seed,i),H(seed,i+40),H(seed,i+80));fx.Glow(it.A+d*(.7f*(1-u)),Red*(1.6f*(1-u))+Gold*(.6f*u),.07f,NativeBossVfx.Mote);}fx.Glow(it.A,Gold*(2*(1-age/.3f)),.5f,NativeBossVfx.Core);}
            var c=me.Seat!=0?me.Body:it.B;
            if(age<.4f){for(int r=0;r<4;r++){float y=-.9f+1.9f*BossMath.Smooth((age-r*.05f)/.3f);int seg=16;for(int k=0;k<seg;k++){float a0=k*6.2831853f/seg,a1=(k+.8f)*6.2831853f/seg;var p0=c+new Vector3((float)Math.Cos(a0)*.5f,y,(float)Math.Sin(a0)*.5f);var p1=c+new Vector3((float)Math.Cos(a1)*.5f,y,(float)Math.Sin(a1)*.5f);fx.Spark(p1,p1-p0,Gold*(1.6f*(1-age/.4f)),.02f,(p1-p0).Length(),1,.9f);}}}
            Shell(fx,c,age,1.2f,Gold,seed,false);
        }
        // A vertical aperture ring at `at` facing `toward`, opening and closing over .4 s.
        private static void Aperture(NativeBossVfx fx,Vector3 at,Vector3 toward,float age,uint seed)
        {
            if(age<0||age>.4f)return;float open=(float)Math.Sin(Math.PI*age/.4f);var f=toward-at;f.Y=0;float l=f.Length();f=l>1e-3f?f/l:Vector3.UnitZ;var side=new Vector3(f.Z,0,-f.X);
            int seg=fx.Low?14:24;for(int k=0;k<seg;k++){float a0=k*6.2831853f/seg,a1=(k+1)*6.2831853f/seg;var p0=at+(side*(float)Math.Cos(a0)*.45f+Vector3.UnitY*(float)Math.Sin(a0)*.8f)*open;var p1=at+(side*(float)Math.Cos(a1)*.45f+Vector3.UnitY*(float)Math.Sin(a1)*.8f)*open;fx.Spark(p1,p1-p0,Violet*2*open,.03f,(p1-p0).Length(),1,.95f);}
            fx.Glow(at,Violet*(1.4f*open),1.1f*open,NativeBossVfx.Mote);
        }
        // A pulse's ring on the floor exactly its radius (the ground surface's Pulse style draws the expanding band and its flash)
        // with its own particles: impulse streaks (Charge Discharge), branching arcs (Arc Fault, violet: no blue or cyan lightning
        // in areas), fragments (Fragment Driver).
        private static void Ring(NativeBossVfx fx,RoundsSurface ground,in Item it,float age,uint seed,bool low)
        {
            float floor=it.Floor;float g=NativeRoundsFields.GroundRadius(it.Radius,it.A.Y,floor);if(g<.3f)g=Math.Min(it.Radius,.3f+it.Radius*.2f);
            var look=it.Kind==RoundsProcKind.ArcFault?ArcRing:it.Kind==RoundsProcKind.Fragment?FragRing:it.Variant==1?ChargeRing:GuardRing;
            NativeRoundsFields.Disc(ground,new Vector3(it.A.X,it.A.Y,it.A.Z),floor,g,low?4:6,low?36:64,(seed&0xffff)/655.36f,look,age,.55f,it.Radius,1);
            var c=new Vector3(it.A.X,floor,it.A.Z);float grow=BossMath.Smooth(age/.16f),fade=1-BossMath.Smooth((age-.15f)/.4f);if(fade<=0)return;
            if(it.Kind==RoundsProcKind.ArcFault){int n=low?4:7;for(int k=0;k<n;k++){float a=(k+H(seed,k))*6.2831853f/n;var to=c+new Vector3((float)Math.Cos(a)*g*grow,.05f,(float)Math.Sin(a)*g*grow);var prev=c+new Vector3(0,.3f,0);for(int s=1;s<=6;s++){float u=s/6f;var p=Vector3.Lerp(c+new Vector3(0,.3f,0),to,u)+new Vector3(H(seed,k*31+s)-.5f,(H(seed,k*31+s+9)-.5f)*.4f,H(seed,k*31+s+17)-.5f)*(.3f*(float)Math.Sin(Math.PI*u));fx.Spark(p,p-prev,Violet*(2.4f*fade),.02f,(p-prev).Length(),1,.95f);prev=p;}}}
            else if(it.Kind==RoundsProcKind.Fragment){fx.RubbleBurst(age,it.A,Vector3.UnitY,1.8f,6,low?6:12,.9f,0,.04f,new Vector3(.32f,.3f,.28f),seed,1,1.2f,floor);fx.SparkBurst(age,it.A,Vector3.UnitY,1.9f,7,low?10:18,.35f,0,.02f,seed,2,1.1f,.016f,1.1f,true,floor);}
            else if(it.Variant==1){int n=low?8:14;for(int k=0;k<n;k++){float a=k*6.2831853f/n;var d=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));var p=c+d*(g*grow*.95f)+new Vector3(0,.25f,0);fx.Spark(p,d*4,Gold*(2*fade),.035f,.35f,.08f,.9f);}}
        }
        // Thorns: spikes off the holder toward the attacker and a red spark running back to it.
        private static void Thorns(NativeBossVfx fx,Vector3 me,Vector3 attacker,float age,uint seed)
        {
            float u=age/.35f;if(u>1)return;var col=Lin(0xff5a7a);int n=fx.Scale(10);
            for(int k=0;k<n;k++){var d=NativeBossVfx.Cone(attacker-me,1.2f,H(seed,k),H(seed,k+20),H(seed,k+40));var a=me+d*.25f;var b=me+d*(.25f+.4f*BossMath.Smooth(u/.3f));fx.Spark(b,b-a,col*(2.2f*(1-u)),.03f,(b-a).Length(),1,.2f);}
            var p=Vector3.Lerp(me,attacker,Math.Min(1,u/.6f));fx.Glow(p,col*(2*(1-u)),.2f,NativeBossVfx.Core);
        }
        // A green restore ring rising up the body with plus-shaped motes (block heals; Mending Loader's at the reload).
        private static void Restore(NativeBossVfx fx,Vector3 c,float age,bool reload,uint seed)
        {
            float u=age/.6f;if(u>1)return;float y=-.85f+1.8f*BossMath.Smooth(u);int seg=18;
            for(int k=0;k<seg;k++){float a0=k*6.2831853f/seg,a1=(k+.8f)*6.2831853f/seg,r=.5f+.1f*u;var p0=c+new Vector3((float)Math.Cos(a0)*r,y,(float)Math.Sin(a0)*r);var p1=c+new Vector3((float)Math.Cos(a1)*r,y,(float)Math.Sin(a1)*r);fx.Spark(p1,p1-p0,Green*(1.8f*(1-u)),.022f,(p1-p0).Length(),1,.9f);}
            int n=fx.Scale(reload?6:10);for(int i=0;i<n;i++){float a=i*2.39996f;var p=c+new Vector3((float)Math.Cos(a)*.4f,-.4f+u*1.4f+H(seed,i)*.4f,(float)Math.Sin(a)*.4f);fx.Spark(p+new Vector3(.04f,0,0),new Vector3(.08f,0,0),Green*(1.6f*(1-u)),.018f,.08f,1,.95f);fx.Spark(p+new Vector3(0,.04f,0),new Vector3(0,.08f,0),Green*(1.6f*(1-u)),.018f,.08f,1,.95f);}
        }
        // Repair motes spiralling up the body while it regenerates (a fixed rate, each mote's slot fixed on the clock).
        private static void Repair(NativeBossVfx fx,Vector3 c,float clock,uint seed)
        {
            float rate=12*fx.RateScale;int last=(int)Math.Floor(clock*rate),first=last-(int)(1.2f*rate);
            for(int s=first;s<=last;s++){float born=s/rate,a=clock-born;if(a<0||a>1.1f)continue;float u=a/1.1f,ang=H(seed,s)*6.28f+a*3;var p=c+new Vector3((float)Math.Cos(ang)*.45f,-.8f+1.7f*u,(float)Math.Sin(ang)*.45f);fx.Glow(p,Green*(1.4f*(float)Math.Sin(Math.PI*u)),.07f,NativeBossVfx.Mote);}
        }
    }
}
