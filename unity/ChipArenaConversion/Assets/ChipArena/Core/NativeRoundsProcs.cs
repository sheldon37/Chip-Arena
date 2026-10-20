using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A proc look's kind (NativeRoundsProcs).</summary>
    public enum RoundsProcKind:byte{None,Phase,Packet,Grapple,Tractor,Stasis,Execute,Reclaim,Transit,Shield,Pulse,ArcFault,Fragment,Thorns,BlockHeal,Regen,ReloadHeal,Swap}

    /// <summary>A pilot's body this frame for the procs: seat, presented body centre, alive, its pack proc looks and stasis look, its
    /// replicated frozenUntil and shieldUntil (authority s; shieldUntil 0: a frame from a host that carries none).</summary>
    public struct RoundsBody{public int Seat;public Vector3 Body;public bool Alive;public RoundsProc Procs;public int Stasis;public float FrozenUntil,ShieldUntil;}

    /// <summary>A protection shell drawn this frame (checks): its seat and kind, the end it is drawn to, and whether this is its
    /// shatter (a shell cut short by the authority breaks up after its end instead of fading before it).</summary>
    public struct RoundsShellShape{public int Seat;public RoundsProcKind Kind;public float End;public bool Shatter,Known;}

    /// <summary>
    /// The ROUNDS packs' procs, pulses and buffs that had no look of their own, each from a replicated event (the authority's
    /// own Block, Hit, Freeze, Teleport, Field, BlockPulse, Explosion, Heal and ReloadEnd events, the same on the host, every guest
    /// and the killcam) aged on the presentation clock:
    /// <list type="bullet">
    /// <item>Phase Reserve: a phase shimmer round the blocker for its protection (one per block activation).</item>
    /// <item>Packet Theft: data packets lifted off the target and streamed into the thief on each direct hit.</item>
    /// <item>Grapple Driver, Tractor Hook: a hook chain or a tractor beam from the holder to the pulled target.</item>
    /// <item>The block interdicts' freeze (Disruption Pulse, Lunar Interdict, Shadow Interdict): a stasis cage round each frozen
    /// pilot to its replicated frozenUntil.</item>
    /// <item>Finish Protocol: a finisher slash where a direct hit lands on a target already below 30% health.</item>
    /// <item>Reclamation Core, Mulligan Reserve: the body dissolving where it was, rebuilt where it blinks to, and its protection
    /// shell. Impact Transit: entry and exit apertures. Emergency Vent: its protection shell. Every protection shell (these and
    /// Phase Reserve's) ends exactly at the pilot's replicated shieldUntil, shortened or cleared early too (a shell cut short
    /// shatters after its end); the authority's grant durations are only the fallback for frames that carry no shieldUntil (old
    /// hosts). A newer shell on the same pilot takes over from an older one.</item>
    /// <item>Position Exchange, Dimensional Exchange: from the authority's swap event (a Teleport "swap": the owner's old and
    /// new body centres, the swapped pilot): both bodies phase out where they stood as body-shaped holograms, their streams and
    /// afterimages cross between the two ends, and each phases in where the other stood (the swapped pilot's arrival lasts
    /// through its replicated freeze). Position Exchange: clean scan-line holograms on two arcs, cyan for the owner and amber
    /// for the swapped pilot. Dimensional Exchange: a rift tears open at each end, the streams braid along the fold, glitching
    /// afterimages, magenta and violet.</item>
    /// <item>Guard Discharge, Charge Discharge, Arc Fault, Fragment Driver: a ring on the floor exactly the pulse's radius (the
    /// authority's 3 m and 1.8 m blasts) with its own arcs, impulse streaks or fragments over the vanilla blast.</item>
    /// <item>Thorn Plating, Retribution Frame: spikes off the holder and a return spark to the attacker. The block heals, the
    /// regeneration and Mending Loader: restore rings and repair motes on the holder.</item>
    /// </list>
    /// A fixed store; allocation free.
    /// </summary>
    public sealed class NativeRoundsProcs
    {
        /// <summary>A live proc. A protection shell's Until is the end it is drawn to: the pilot's replicated shieldUntil once one
        /// is seen (Known; Peak the latest seen, Cut when it was shortened or cleared), else the fallback grant from its event.</summary>
        public struct Item{public RoundsProcKind Kind;public int Seat,Target,Variant,Id;public float At,Until,Floor,Peak;public Vector3 A,B;public float Radius;public bool Known,Cut;}
        public const int Capacity=96;
        public readonly Item[] Items=new Item[Capacity];public int Count;
        /// <summary>The protection a shell shows when the pilot frames carry no shieldUntil (old hosts only): the authority's grants
        /// (NativeChips: Phase Reserve .6 s on a successful block, Reclamation Core / Mulligan Reserve 1.2 s with the emergency
        /// blink, Emergency Vent 2 s with its repulsion).</summary>
        public const float PhaseFallback=.6f,ReclaimFallback=1.2f,VentFallback=2;
        /// <summary>A cut shell's shatter (s after its end), and the swap look's span and the swapped pilot's longest daze (s).</summary>
        public const float ShatterSpan=.22f,SwapSpan=.62f,SwapDaze=1;
        /// <summary>Protection shells drawn this frame (checks).</summary>
        public readonly RoundsShellShape[] Shells=new RoundsShellShape[24];public int ShellCount;
        /// <summary>Whether a kind draws a protection shell (bound to the pilot's shieldUntil).</summary>
        public static bool Protects(RoundsProcKind k)=>k==RoundsProcKind.Phase||k==RoundsProcKind.Shield||k==RoundsProcKind.Reclaim;
        private readonly float[] regenUntil=new float[16];private readonly int[] regenSeat=new int[16];
        /// <summary>How long each kind draws (s), past its own until for those that carry one; a protection shell is remembered at
        /// least as long as its fallback grant (its event, fed again while it is listed, then finds it and keeps its bound end).</summary>
        public static float Span(RoundsProcKind k)=>k==RoundsProcKind.Phase?PhaseFallback:k==RoundsProcKind.Shield?VentFallback:k==RoundsProcKind.Packet?.6f:k==RoundsProcKind.Grapple||k==RoundsProcKind.Tractor?.4f:k==RoundsProcKind.Execute?.35f:k==RoundsProcKind.Reclaim?ReclaimFallback:k==RoundsProcKind.Transit?.4f:k==RoundsProcKind.Pulse||k==RoundsProcKind.ArcFault||k==RoundsProcKind.Fragment?.55f:k==RoundsProcKind.Thorns?.35f:k==RoundsProcKind.BlockHeal||k==RoundsProcKind.ReloadHeal?.6f:k==RoundsProcKind.Swap?SwapSpan+SwapDaze-.4f:.3f;
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
                    if((source&RoundsProc.Phase)!=0&&!Recent(RoundsProcKind.Phase,e.Source,t,.65f))Add(new Item{Kind=RoundsProcKind.Phase,Seat=e.Source,Id=e.Id,At=t,Until=t+PhaseFallback});
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
                    // The swap (NativeChips swap-shop): From the owner's old body centre, Position its new one (the swapped pilot's old).
                    if(e.Detail=="swap"){if((source&(RoundsProc.Swap|RoundsProc.Shuffle))!=0)Add(new Item{Kind=RoundsProcKind.Swap,Seat=e.Source,Target=e.Target,Id=e.Id,At=t,Until=t+SwapSpan,A=V(e.From),B=at,Variant=(source&RoundsProc.Shuffle)!=0?1:0});break;}
                    // The chip blinks (the emergency exit, the wormhole) carry no detail; a kit's or a swap's teleport is not theirs.
                    if(e.Detail!=null)break;
                    if((source&RoundsProc.Reclaim)!=0)Add(new Item{Kind=RoundsProcKind.Reclaim,Seat=e.Source,Id=e.Id,At=t,Until=t+ReclaimFallback,A=V(e.From),B=at});
                    else if((source&RoundsProc.Transit)!=0)Add(new Item{Kind=RoundsProcKind.Transit,Seat=e.Source,Id=e.Id,At=t,Until=t+.4f,A=V(e.From),B=at});
                    break;
                case EventKind.Field:
                    if((source&RoundsProc.Vent)!=0&&e.Detail=="panic-button")Add(new Item{Kind=RoundsProcKind.Shield,Seat=e.Source,Id=e.Id,At=t,Until=t+VentFallback,Variant=1});
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
            int keep=0;ShellCount=0;
            for(int i=0;i<Count;i++)
            {
                var it=Items[i];if(Protects(it.Kind))Bind(ref it,i,keep,bodies,bodyCount,clock);float age=clock-it.At;
                if(age>Math.Max(it.Until-it.At,Span(it.Kind))+.4f)continue;Items[keep++]=it;if(age<0)continue;
                uint seed=(uint)it.Id*2654435761u+(uint)it.Kind;
                Body(bodies,bodyCount,it.Seat,out var me);Body(bodies,bodyCount,it.Target,out var them);
                switch(it.Kind)
                {
                    case RoundsProcKind.Phase:if(me.Seat!=0)Protect(fx,it,me.Body,age,Cyan,seed,true);break;
                    case RoundsProcKind.Shield:if(me.Seat!=0)Protect(fx,it,me.Body,age,Red,seed,false);break;
                    case RoundsProcKind.Packet:Packets(fx,lit,it,age,me,seed);break;
                    case RoundsProcKind.Grapple:case RoundsProcKind.Tractor:if(me.Seat!=0&&them.Seat!=0)Tether(fx,lit,it.Kind==RoundsProcKind.Grapple,me.Body,them.Body,age,seed);break;
                    case RoundsProcKind.Stasis:{float until=Math.Max(it.Until,them.Seat!=0?them.FrozenUntil:0);if(them.Seat!=0&&clock<until+.25f)Stasis(fx,lit,them.Body,age,until-it.At,it.Variant,seed);break;}
                    case RoundsProcKind.Execute:Slash(fx,it.A,it.B,age,seed);break;
                    case RoundsProcKind.Reclaim:Reclaim(fx,it,age,me,seed);break;
                    case RoundsProcKind.Swap:Swap(fx,lit,it,age,me,them,seed,low);break;
                    case RoundsProcKind.Transit:Aperture(fx,it.A,it.B,age,seed);Aperture(fx,it.B,it.A,age-.05f,seed+9);break;
                    case RoundsProcKind.Pulse:case RoundsProcKind.ArcFault:case RoundsProcKind.Fragment:Ring(fx,ground,it,age,seed,low);break;
                    case RoundsProcKind.Thorns:if(me.Seat!=0)Thorns(fx,me.Body,them.Seat!=0?them.Body:it.A,age,seed);break;
                    case RoundsProcKind.BlockHeal:case RoundsProcKind.ReloadHeal:if(me.Seat!=0)Restore(fx,me.Body,age,it.Kind==RoundsProcKind.ReloadHeal,seed);break;
                }
            }
            Count=keep;
            for(int i=0;i<regenSeat.Length;i++){if(regenSeat[i]!=0&&clock>regenUntil[i]+1){regenSeat[i]=0;continue;}if(regenSeat[i]==0||clock>regenUntil[i])continue;if(Body(bodies,bodyCount,regenSeat[i],out var b)&&b.Alive)Repair(fx,b.Body,clock,(uint)regenSeat[i]);}
        }
        // A protection shell's end this frame: the pilot's replicated shieldUntil whenever the frame carries one (the authority's actual
        // protection: extended, shortened or already over), ended now when it is cleared to the old default after one was seen,
        // the fallback grant from its event while none ever was (an old host). A newer shell on the same pilot (Items before
        // `keep` are this frame's kept ones, after `i` the ones still to come) takes over at its start.
        private void Bind(ref Item it,int i,int keep,RoundsBody[] bodies,int bodyCount,float clock)
        {
            if(Body(bodies,bodyCount,it.Seat,out var b))
            {
                float end=b.ShieldUntil;
                if(end>0){it.Known=true;if(end>it.Peak)it.Peak=end;it.Until=end;}
                else if(it.Known)it.Until=Math.Min(it.Until,clock);
                it.Cut=it.Known&&it.Until<it.Peak-.05f;
            }
            float next=float.PositiveInfinity;
            for(int k=0;k<Count;k++){if(k>=keep&&k<=i)continue;var o=Items[k];if(o.Seat==it.Seat&&Protects(o.Kind)&&o.At>it.At&&o.At<=clock&&o.At<next)next=o.At;}
            if(next<it.Until){it.Until=next;it.Cut=false;}
        }
        // A protection shell to its bound end (fading over its last .15 s), then, when the authority cut it short, its shatter.
        private void Protect(NativeBossVfx fx,in Item it,Vector3 c,float age,Vector3 rgb,uint seed,bool phase)
        {
            float life=it.Until-it.At;
            if(age<life){Shell(fx,c,age,life,rgb,seed,phase);Record(it,false);}
            else if(it.Cut&&age<life+ShatterSpan){Shatter(fx,c,age-life,rgb,seed);Record(it,true);}
        }
        private void Record(in Item it,bool shatter){if(ShellCount<Shells.Length)Shells[ShellCount++]=new RoundsShellShape{Seat=it.Seat,Kind=it.Kind,End=it.Until,Shatter=shatter,Known=it.Known};}
        // A shell cut short: its motes burst outward off the sphere and fade, with a quick flash of the shell's light.
        private static void Shatter(NativeBossVfx fx,Vector3 c,float since,Vector3 rgb,uint seed)
        {
            float u=since/ShatterSpan;if(u<0||u>=1)return;int n=fx.Scale(26);float fade=1-BossMath.Smooth(u);
            for(int i=0;i<n;i++)
            {
                float y=1-2*(i+.5f)/n,r=(float)Math.Sqrt(Math.Max(0,1-y*y)),ph=i*2.39996f+H(seed,i)*.6f;var d=new Vector3((float)Math.Cos(ph)*r,y*1.25f,(float)Math.Sin(ph)*r);
                float out1=.62f+.75f*BossMath.Smooth(u)*(.6f+.4f*H(seed,i+50));var p=c+d*out1;fx.Spark(p,d*(2.4f*(1-u)),rgb*(1.8f*fade),.022f,.05f,.06f,.8f);
            }
            if(u<.35f)fx.Glow(c,rgb*(1.6f*(1-u/.35f)),1.1f,NativeBossVfx.RingGlow);
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
        // where it lands, inside a gold protection shell to its bound end.
        private void Reclaim(NativeBossVfx fx,in Item it,float age,in RoundsBody me,uint seed)
        {
            if(age<.3f){int n=fx.Scale(30);for(int i=0;i<n;i++){float u=age/.3f;var d=NativeBossVfx.Cone(Vector3.UnitY,2,H(seed,i),H(seed,i+40),H(seed,i+80));fx.Glow(it.A+d*(.7f*(1-u)),Red*(1.6f*(1-u))+Gold*(.6f*u),.07f,NativeBossVfx.Mote);}fx.Glow(it.A,Gold*(2*(1-age/.3f)),.5f,NativeBossVfx.Core);}
            var c=me.Seat!=0?me.Body:it.B;
            if(age<.4f){for(int r=0;r<4;r++){float y=-.9f+1.9f*BossMath.Smooth((age-r*.05f)/.3f);int seg=16;for(int k=0;k<seg;k++){float a0=k*6.2831853f/seg,a1=(k+.8f)*6.2831853f/seg;var p0=c+new Vector3((float)Math.Cos(a0)*.5f,y,(float)Math.Sin(a0)*.5f);var p1=c+new Vector3((float)Math.Cos(a1)*.5f,y,(float)Math.Sin(a1)*.5f);fx.Spark(p1,p1-p0,Gold*(1.6f*(1-age/.4f)),.02f,(p1-p0).Length(),1,.9f);}}}
            Protect(fx,it,c,age,Gold,seed,false);
        }
        // ---- the swap (Position Exchange, Dimensional Exchange) ----
        // Position Exchange: the owner's stream cyan, the swapped pilot's amber. Dimensional Exchange: magenta and violet, in rifts
        // round a dark core.
        private static readonly Vector3 SwapOwner=Lin(0x5ff6ff),SwapOwnerCore=Lin(0xeaffff),SwapOther=Lin(0xffb347),SwapOtherCore=Lin(0xfff1c4);
        private static readonly Vector3 RiftOwner=Lin(0xff4fd8),RiftOwnerCore=Lin(0xffe2f6),RiftOther=Lin(0x9a7bff),RiftOtherCore=Lin(0xece4ff),RiftDark=Lin(0x12051c);
        /// <summary>The swap's timing (s from its event): the departures dissolve over SwapDissolve, the streams cross over
        /// SwapTravel, the arrivals build from SwapArriveAt over SwapBuild.</summary>
        public const float SwapDissolve=.3f,SwapTravel=.2f,SwapArriveAt=.08f,SwapBuild=.26f;
        // A pilot's outline for the holograms (the armoured frame: legs, hips, torso, shoulders, the block head): its radius at
        // height share h (0 the feet, 1 the top of the head), 1.7 m tall round a body centre PilotCentre over the feet.
        private static readonly float[] ProfileH={0,.05f,.42f,.48f,.56f,.68f,.73f,.76f,.785f,.81f,.96f,1},ProfileR={.15f,.17f,.16f,.2f,.22f,.25f,.28f,.13f,.1f,.2f,.2f,.13f};
        private const float GhostHeight=1.7f;
        private static float Profile(float h){h=BossMath.Clamp01(h);for(int i=1;i<ProfileH.Length;i++)if(h<=ProfileH[i]){float u=(h-ProfileH[i-1])/(ProfileH[i]-ProfileH[i-1]);return ProfileR[i-1]+(ProfileR[i]-ProfileR[i-1])*u;}return ProfileR[ProfileR.Length-1];}
        private static float GhostY(float h)=>-NativeRoundsFields.PilotCentre+h*GhostHeight;
        // The swap: both bodies phase out where they stood, their streams and afterimages cross between the two ends, and each
        // phases in on its live body (the event's end while a body is not presented); rings on the floor under both ends.
        // Dimensional Exchange inside a rift at each end, its streams braided along the fold, its holograms glitching.
        private static void Swap(NativeBossVfx fx,RoundsLit lit,in Item it,float age,in RoundsBody me,in RoundsBody them,uint seed,bool low)
        {
            bool rift=it.Variant==1;var a=it.A;var b=it.B;var d=b-a;var flat=new Vector3(d.X,0,d.Z);float l=flat.Length();var f=l>1e-3f?flat/l:Vector3.UnitZ;var side=new Vector3(f.Z,0,-f.X);
            float bow=BossMath.Clamp(d.Length()/6,.5f,1.4f);
            Vector3 own=rift?RiftOwner:SwapOwner,ownCore=rift?RiftOwnerCore:SwapOwnerCore,other=rift?RiftOther:SwapOther,otherCore=rift?RiftOtherCore:SwapOtherCore;
            if(rift){float open=BossMath.Smooth(age/.07f)*(1-BossMath.Smooth((age-.34f)/.16f));Rift(fx,lit,a,side,age,open,own,ownCore,seed,low);Rift(fx,lit,b,side,age,open,other,otherCore,seed+5,low);}
            // The owner leaves `a` for `b`; the swapped pilot leaves `b` for `a`.
            Depart(fx,a,f,age,own,ownCore,rift,side,seed,low);Depart(fx,b,-f,age,other,otherCore,rift,side,seed+31,low);
            Stream(fx,a,b,side,0,age,own,ownCore,rift,bow,seed,low);Stream(fx,b,a,side,1,age,other,otherCore,rift,bow,seed+57,low);
            var ownerAt=me.Seat!=0?me.Body:b;var otherAt=them.Seat!=0?them.Body:a;
            float daze=them.Seat!=0&&them.FrozenUntil>it.At?Math.Min(SwapDaze,them.FrozenUntil-it.At):0;
            Arrive(fx,ownerAt,age,own,ownCore,rift,side,0,seed+71,low);Arrive(fx,otherAt,age,other,otherCore,rift,side,daze,seed+89,low);
            float grow=BossMath.Smooth(age/.38f),fade=1-BossMath.Smooth((age-.12f)/.32f);
            if(fade>0){int seg=low?14:26;var lift=new Vector3(0,NativeRoundsFields.PilotCentre-.03f,0);FloorRing(fx,a-lift,.25f+.8f*grow,Vector3.Lerp(other,own,.35f)*(1.8f*fade),.022f,seg,age*2);FloorRing(fx,b-lift,.25f+.8f*grow,Vector3.Lerp(own,other,.35f)*(1.8f*fade),.022f,seg,-age*2);}
        }
        // One ring of the outline at height share h round `c`: `seg` tangential streaks, its radius scaled by `spread`.
        private static void Slice(NativeBossVfx fx,Vector3 c,float h,float spread,Vector3 rgb,int seg,float width,float turn)
        {
            float rad=Profile(h)*spread;var o=c+new Vector3(0,GhostY(h),0);
            for(int k=0;k<seg;k++){float a0=turn+k*6.2831853f/seg,a1=a0+.78f*6.2831853f/seg;var p0=o+new Vector3((float)Math.Cos(a0)*rad,0,(float)Math.Sin(a0)*rad);var p1=o+new Vector3((float)Math.Cos(a1)*rad,0,(float)Math.Sin(a1)*rad);fx.Spark(p1,p1-p0,rgb,width,(p1-p0).Length(),1,.9f);}
        }
        // A body-shaped hologram round `c` (an afterimage): `rings` slices of the outline, each a little brighter or dimmer; `glitch`
        // shifts each slice sideways along `side` (Dimensional Exchange), re-rolled `tick` by tick.
        private static void Ghost(NativeBossVfx fx,Vector3 c,Vector3 rgb,float light,int rings,int seg,Vector3 side,float glitch,uint seed,int tick)
        {
            if(light<=.01f)return;
            for(int r=0;r<rings;r++){float h=(r+.5f)/rings;var o=glitch>0?c+side*((H(seed,r*13+tick)-.5f)*2*glitch):c;Slice(fx,o,h,1,rgb*(light*(.6f+.25f*H(seed,r+90))),seg,.014f,r*.41f);}
            Meridians(fx,c,1,1,rgb*(.8f*light),seg/2,.013f,tick*.05f);
            fx.Glow(c+new Vector3(0,.05f,0),rgb*(.18f*light),.95f,NativeBossVfx.Mote);
        }
        // The outline's meridians (`count` lines up the profile from the feet to height share `top`): with the rings they read as
        // the body's wire frame rather than a coil.
        private static void Meridians(NativeBossVfx fx,Vector3 c,float top,float spread,Vector3 rgb,int count,float width,float turn)
        {
            if(top<=.02f||count<=0)return;
            for(int m=0;m<count;m++)
            {
                float a=turn+m*6.2831853f/count,ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);float r0=ProfileR[0]*spread;var prev=c+new Vector3(ca*r0,GhostY(0),sa*r0);
                for(int j=1;j<ProfileH.Length;j++){float h=Math.Min(ProfileH[j],top),r=Profile(h)*spread;var p=c+new Vector3(ca*r,GhostY(h),sa*r);fx.Spark(p,p-prev,rgb,width,(p-prev).Length(),1,.95f);prev=p;if(ProfileH[j]>=top)break;}
            }
        }
        // A departure: the body standing at `c` as a hologram dissolving from the head down over SwapDissolve (hottest at the
        // dissolve front), each slice breaking into motes swept toward the other end (`toward`); a flash and a seam of light up the
        // body at the swap itself. In a rift the slices pinch in toward its plane and jitter.
        private static void Depart(NativeBossVfx fx,Vector3 c,Vector3 toward,float age,Vector3 rgb,Vector3 core,bool rift,Vector3 side,uint seed,bool low)
        {
            int rings=low?9:15,seg=low?8:14;float front=age/SwapDissolve;int tick=(int)(age*30);
            for(int r=0;r<rings;r++)
            {
                float h=(r+.5f)/rings,goAt=(1-h)*.85f+.15f*H(seed,r+200),since=(front-goAt)*SwapDissolve;
                if(since<0)
                {
                    float hot=Math.Max(0,1-(goAt-front)*6);var o=rift?c+side*((H(seed,r*13+tick)-.5f)*.12f):c;
                    Slice(fx,o,h,rift?1-.5f*BossMath.Smooth(front):1,Vector3.Lerp(rgb,core,hot)*(.75f+1.6f*hot),seg,.017f,r*.41f+age*(rift?4:1.5f));
                }
                else if(since<.32f)
                {
                    float u=since/.32f;int m=low?1:3;float rad=Profile(h);
                    for(int k=0;k<m;k++)
                    {
                        float ang=H(seed,r*7+k)*6.2831853f;var p0=c+new Vector3((float)Math.Cos(ang)*rad,GhostY(h),(float)Math.Sin(ang)*rad);
                        var v=toward*(1.6f+1.4f*H(seed,r*7+k+40))+new Vector3(0,.5f+.8f*H(seed,r*7+k+80),0)+(rift?side*((H(seed,r*7+k+120)-.5f)*2):Vector3.Zero);
                        var p=p0+v*(since*(1-.4f*u));fx.Glow(p,rgb*(1.5f*(1-u)),.06f,NativeBossVfx.Mote);fx.Spark(p,v,core*(1.2f*(1-u)),.012f,.03f,.05f,.85f);
                    }
                }
            }
            float left=1-(front-.075f)/.85f;if(left>0)Meridians(fx,c,Math.Min(1,left),rift?1-.5f*BossMath.Smooth(front):1,rgb*.9f,low?4:7,.013f,age*(rift?4:1.5f));
            if(age<.08f)fx.Glow(c,core*(3*(1-age/.08f)),1.1f,NativeBossVfx.Core);
            if(age<.14f){float u=age/.14f;var top=c+new Vector3(0,GhostY(1)*(1-u*.3f),0);var bottom=c+new Vector3(0,GhostY(0),0);fx.Spark(top,top-bottom,core*(2.4f*(1-u)),.03f,(top-bottom).Length(),1,.6f);}
        }
        // A stream between the ends: the owner's (strand 0) bows up and to one side, the swapped pilot's (strand 1) less and to the
        // other; Dimensional Exchange braids both round the straight fold between the centres (a turn each, half a turn apart).
        private static Vector3 PathAt(Vector3 a,Vector3 b,Vector3 side,float u,int strand,bool rift,float bow)
        {
            var p=Vector3.Lerp(a,b,u);float w=4*u*(1-u);
            if(!rift)return p+new Vector3(0,(strand==0?.75f:.4f)*bow*w,0)+side*((strand==0?.55f:-.55f)*bow*w);
            float turn=u*6.2831853f+(strand==0?0:(float)Math.PI),rr=.34f*bow*(float)Math.Sqrt(w);
            return p+side*((float)Math.Cos(turn)*rr)+new Vector3(0,(float)Math.Sin(turn)*rr+.15f*w,0);
        }
        // A stream: its comet runs the path over SwapTravel (a hot head, a tapering trail), afterimages of the body stand where the
        // head passed and fade, and the path lingers behind the head as a thin beam, fading by SwapTravel + .3 s.
        private static void Stream(NativeBossVfx fx,Vector3 a,Vector3 b,Vector3 side,int strand,float age,Vector3 rgb,Vector3 core,bool rift,float bow,uint seed,bool low)
        {
            float head=BossMath.Smoother(age/SwapTravel);int segs=low?12:24;int tick=(int)(age*30);
            float beam=age<SwapTravel?1:1-BossMath.Smooth((age-SwapTravel)/.3f);
            if(beam>0)
            {
                var prev=PathAt(a,b,side,0,strand,rift,bow);
                for(int k=1;k<=segs;k++){float u=(float)k/segs;if(u>head+1e-4f)break;var p=PathAt(a,b,side,u,strand,rift,bow);float flick=rift?.55f+.45f*H(seed,k+tick*31):1;fx.Spark(p,p-prev,rgb*(.9f*beam*flick),rift?.022f:.017f,(p-prev).Length(),1,.95f);prev=p;}
            }
            if(age<SwapTravel+.06f)
            {
                float fade=age<SwapTravel?1:1-(age-SwapTravel)/.06f;var hp=PathAt(a,b,side,head,strand,rift,bow);fx.Glow(hp,core*(2.6f*fade),.34f,NativeBossVfx.Core);fx.Glow(hp,rgb*(1.2f*fade),.8f,NativeBossVfx.Mote);
                var prev=hp;for(int k=1;k<=8;k++){float u=head-k*.045f;if(u<0)break;var p=PathAt(a,b,side,u,strand,rift,bow);float w=1-k/9f;fx.Spark(prev,prev-p,Vector3.Lerp(rgb,core,w*.6f)*(2.2f*fade*w),.05f*w+.012f,(prev-p).Length(),1,.9f);prev=p;}
            }
            // Afterimages: the body's hologram standing on the line between the ends (a little to its stream's side) where the head
            // was passing, each fading over .26 s.
            int echoes=low?1:3;
            for(int e=0;e<echoes;e++)
            {
                float te=SwapTravel*(low?.5f:.22f+.28f*e),since=age-te;if(since<0||since>.26f)continue;
                var c=Vector3.Lerp(a,b,BossMath.Smoother(te/SwapTravel))+side*(strand==0?.32f:-.32f);float light=.8f*(1-since/.26f)*(e==1?1:.8f);
                Ghost(fx,c,rift?Vector3.Lerp(rgb,core,.3f):rgb,light,low?6:9,low?6:10,side,rift?.07f:0,seed+(uint)e*17,(int)(age*24));
            }
        }
        // An arrival on the body at `c`: a bright scan ring sweeps up the outline from the feet over SwapBuild, the slices it passed
        // glowing and settling, motes converging into the body, a rim flash as it completes. `daze` (s from the event: the swapped
        // pilot's replicated freeze): a few hologram slices flicker on the body until then.
        private static void Arrive(NativeBossVfx fx,Vector3 c,float age,Vector3 rgb,Vector3 core,bool rift,Vector3 side,float daze,uint seed,bool low)
        {
            float a=age-SwapArriveAt;if(a<0)return;float u=a/SwapBuild;int rings=low?8:13,seg=low?8:12;
            if(u<1.6f)
            {
                float front=BossMath.Smooth(Math.Min(1,u)),settle=u<1?1:1-(u-1)/.6f;
                for(int r=0;r<rings;r++)
                {
                    float h=(r+.5f)/rings;if(h>front)break;float near=Math.Max(0,1-(front-h)*5);var o=rift?c+side*((H(seed,r*11+(int)(age*30))-.5f)*.1f*(1-front)):c;
                    Slice(fx,o,h,1+.22f*near,Vector3.Lerp(rgb,core,near)*((.35f+1.4f*near)*settle),seg,.016f,r*.37f-age*2);
                }
                Meridians(fx,c,front,1.04f,rgb*(.8f*settle),low?4:7,.013f,-age*2);
                if(u<1)Slice(fx,c,front,1.3f,core*2.4f,low?12:20,.024f,age*3);
            }
            if(a<.24f){int n=low?6:12;float w=BossMath.Smooth(a/.24f);for(int k=0;k<n;k++){float ang=k*6.2831853f/n+H(seed,k)*.5f,rr=.85f*(1-w)+.1f;var p=c+new Vector3((float)Math.Cos(ang)*rr,(H(seed,k+30)-.5f)*1.4f*(1-w),(float)Math.Sin(ang)*rr);fx.Glow(p,rgb*(1.6f*(1-w*.5f)),.07f,NativeBossVfx.Mote);}}
            if(u>=.9f&&u<1.5f)fx.Glow(c,rgb*(1.2f*(1-(u-.9f)/.6f)),1.05f,NativeBossVfx.RingGlow);
            if(daze>0&&age<daze&&a>SwapBuild)
            {
                int tick=(int)(age*20);float on=1-BossMath.Smooth((age-(daze-.15f))/.15f);
                for(int k=0;k<(low?2:3);k++)Slice(fx,c+side*((H(seed,tick*5+k+60)-.5f)*.08f),H(seed,tick*5+k),1.08f,rgb*(.9f*on),seg,.014f,tick*.7f);
            }
        }
        // Dimensional Exchange's rift at `c` across `side` (its plane faces along the swap): a lens of crackling lips `open` wide
        // round a dark core, light bleeding off it, shards of space tumbling out.
        private static void Rift(NativeBossVfx fx,RoundsLit lit,Vector3 c,Vector3 side,float age,float open,Vector3 edge,Vector3 core,uint seed,bool low)
        {
            if(open<=.01f)return;int seg=low?10:18,tick=(int)(age*30);float w=.5f*open;Vector3 l0=default,r0=default;
            for(int s=0;s<=seg;s++)
            {
                float v=-1+2f*s/seg,x=w*(float)Math.Pow(Math.Max(0,1-v*v),.7f),j=(H(seed,s*3+tick*17)-.5f)*.06f*open;var y=new Vector3(0,v*.98f+.02f,0);
                var lp=c+y-side*(x+j);var rp=c+y+side*(x-j);
                if(s>0){var col=Vector3.Lerp(edge,core,(1-Math.Abs(v))*.6f)*(2.2f*open);fx.Spark(lp,lp-l0,col,.028f,(lp-l0).Length(),1,.95f);fx.Spark(rp,rp-r0,col,.028f,(rp-r0).Length(),1,.95f);}
                l0=lp;r0=rp;
            }
            for(int k=-1;k<=1;k++)fx.Glow(c+new Vector3(0,k*.42f,0),RiftDark,(k==0?.95f:.6f)*open,NativeBossVfx.Mote,.8f*open);
            fx.Glow(c,edge*(.5f*open),1.6f*open,NativeBossVfx.Mote);
            int n=low?2:5;
            for(int k=0;k<n;k++)
            {
                float t=age+H(seed,k+300)*.1f,sgn=k%2==0?1:-1;var p=c+side*(sgn*(.3f+.9f*t))+new Vector3(0,(H(seed,k+310)-.5f)*1.4f+.4f*t,0);
                var m=Matrix4x4.CreateScale((.55f+.4f*H(seed,k+320))*open)*Matrix4x4.CreateRotationX(t*9+k)*Matrix4x4.CreateRotationY(t*7+k*2)*Matrix4x4.CreateTranslation(p);
                lit.Add(NativeRoundsMeshes.SatBlade,m,new Vector3(.04f,.02f,.05f),1.2f,edge*(1.6f*open),.9f,core*(2*open));
            }
        }
        // A ring of light on the floor round `c`: radius `r`, `seg` streaks.
        private static void FloorRing(NativeBossVfx fx,Vector3 c,float r,Vector3 rgb,float width,int seg,float turn)
        {
            for(int k=0;k<seg;k++){float a0=turn+k*6.2831853f/seg,a1=turn+(k+.82f)*6.2831853f/seg;var p0=c+new Vector3((float)Math.Cos(a0)*r,0,(float)Math.Sin(a0)*r);var p1=c+new Vector3((float)Math.Cos(a1)*r,0,(float)Math.Sin(a1)*r);fx.Spark(p1,p1-p0,rgb,width,(p1-p0).Length(),1,.95f);}
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
