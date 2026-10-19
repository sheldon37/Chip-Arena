using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE 6:47's effects (doc section 8): camera-facing and floor quads into a <see cref="NativeFxQuads"/> (Runtime draws them as one
    /// solid and one effects batch through NativeSirenFx, as the station's own effects: the telegraphs, the headlamp's beam, the
    /// doors' light, the skid's scorch), particles into <see cref="Vfx"/> (Core NativeBossVfx through the shared effects renderer;
    /// the realism pass, owner 2026-10-19: sparks thrown off the wheels and the iron wrists on gravity and drag, bouncing and
    /// motion-stretched, cooling white to red; brake shoes glowing; branching electrical arcs that re-strike and flicker; soft lit
    /// dust and steam that billow, slow and spread; lit debris of tile, steel and glass; dust motes in the headlamp's beam), each
    /// joint's light level (the windows, the headlamp, the Heart, the cab's glass, the markers, the eyes and the watch: the
    /// runtime's per-draw glow tint), and up to three practical lights (the headlamp's beam, the Heart, the doors' spill) for the
    /// station's light slots.
    /// Telegraphs follow the game's danger language (the hazard kit and the zone enemies): a warning is amber and pulses
    /// (0xFFB020), the strike flashes red (0xFF2A1A); the station's electricity is the Linesman's blue (0x6FD8FF); telegraphs are never
    /// thinned by the effects quality or the low tier (rule zero, readability first). Atmosphere (steam, dust, sparks) scales with
    /// the quality. Pure: the same rig, view, clock and camera give the same quads. Allocation free.
    /// </summary>
    public sealed class NativeStationBossFx
    {
        public const int Capacity=2200,SortMax=360,MaxLights=3;
        public const int WarnHex=0xFFB020,DangerHex=0xFF2A1A,LiveHex=0x6FD8FF,SparkHex=0xFFC060,SteamHex=0xB8C0C8,DustHex=0x8A8070,GlassHex=0xBFD8E8;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        /// <summary>The particles (sparks, arcs, dust, steam, debris, glows) for the shared effects renderer.</summary>
        public readonly NativeBossVfx Vfx=new NativeBossVfx(2600);
        /// <summary>FNV-1a over every telegraph quad this frame (in order): the checks pin it to the telegraphs drawn before the
        /// realism pass.</summary>
        public uint TelegraphHash;
        /// <summary>Each joint's light level this frame (multiplies its emission; 1 as modelled, 0 dark).</summary>
        public readonly float[] Glow=new float[NativeStationBossLook.Count];
        /// <summary>Joints not drawn this frame (a torn flank before the express breaks it; the whole boss deep in the tunnel).</summary>
        public readonly bool[] Hidden=new bool[NativeStationBossLook.Count];
        /// <summary>The practical lights (x, y, z, range; linear r, g, b; beam code as NativeLighting's: 0 a point, 1 + yaw one beam).</summary>
        public readonly float[] LightPos=new float[MaxLights*4],LightRgb=new float[MaxLights*3],LightBeam=new float[MaxLights];public int LightCount;
        /// <summary>This frame's counts by family (budget lines, checks).</summary>
        public int Telegraph,Beam,Sparks,Steam,Dust,Arcs,Glints;
        /// <summary>Optional: Alteration's targets the station's glass events land on (x, z pairs), and whether this presentation
        /// draws the falling glass itself (the DEV preview: the station's own glass is drawn from the zone frame in play).</summary>
        public readonly float[] GlassXZ=new float[8];public int GlassCount;public bool DrawGlass;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,fx,fy,fz;private float quality;private bool low;private uint vseed;private Vector3 drift;
        private static readonly Vector3 SteamC=new Vector3(.74f,.77f,.8f),DustC=new Vector3(.3f,.27f,.21f),Tile=new Vector3(.36f,.34f,.3f),Steel=new Vector3(.2f,.2f,.22f),GlassC=new Vector3(.55f,.66f,.74f),
            ArcCore=new Vector3(2.0f,2.5f,3.2f),BrakeHot=new Vector3(2.6f,.75f,.14f);
        private readonly double[] pc=new double[SortMax*12],pk=new double[SortMax];private readonly float[] pcol=new float[SortMax*4];private readonly int[] pidx=new int[SortMax];private readonly byte[] pmode=new byte[SortMax];private int pn;
        private static float R(int hex)=>(float)NativeSceneryBuilder.Channel(hex,16);private static float G(int hex)=>(float)NativeSceneryBuilder.Channel(hex,8);private static float B(int hex)=>(float)NativeSceneryBuilder.Channel(hex,0);
        private static double H(int a,int b){unchecked{uint x=(uint)a*0x9E3779B9U^(uint)b*0x85EBCA6BU;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x/4294967296.0;}}
        private static double Frac(double x)=>x-Math.Floor(x);

        public void Build(StationBossRig rig,in StationBossView v,float t,double eyeX,double eyeY,double eyeZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,float effects,bool lowTier)
        {
            Quads.Clear();pn=0;LightCount=0;Telegraph=Beam=Sparks=Steam=Dust=Arcs=Glints=0;TelegraphHash=2166136261u;Vfx.Begin(effects,lowTier);
            for(int i=0;i<Glow.Length;i++){Glow[i]=1;Hidden[i]=false;}
            if(rig==null||!rig.Visible||!v.live)return;
            ex=eyeX;ey=eyeY;ez=eyeZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;fx=ry*uz-rz*uy;fy=rz*ux-rx*uz;fz=rx*uy-ry*ux;quality=effects<0?0:effects>1?1:effects;low=lowTier;
            vseed=0x647u^(uint)(StationBossView.At(v.spawned)?(int)(v.spawned*16):0);drift=BossMath.Flat(rig.Follow.Velocity);
            var P=rig.Shown;
            Hidden[NativeStationBossLook.BreachL]=(v.sideBroken&1)==0;Hidden[NativeStationBossLook.BreachR]=(v.sideBroken&2)==0;
            // The pocket watch's minute hand moves on to 6:48 with every clock in the station.
            bool at48=v.Dead(t)&&t-v.diedAt>=NativeStationBossStage.DeathClocks;Hidden[NativeStationBossLook.WatchAt47]=at48;Hidden[NativeStationBossLook.WatchAt48]=!at48;
            // Deep in the west tunnel only the headlight shows (the body comes out of the dark at the mouth).
            bool tunnel=rig.Arriving&&rig.Root.X<NativeStationBossArena.WestMouth-5;if(tunnel)for(int i=0;i<Hidden.Length;i++)Hidden[i]=true;
            Glows(rig,v,t);
            // Telegraphs first (never thinned), then the rest.
            Lane(rig,v,t);Doors(rig,v,t);Alteration(rig,v,t);LiveRail(rig,v,t);Terminus(rig,v,t);
            Headlamp(rig,v,t,tunnel);if(!tunnel){Horns(rig,v,t);Heart(rig,v,t);Brakes(rig,v,t);Arrival(rig,v,t);Changes(rig,v,t);Breaks(rig,v,t);Staggers(rig,v,t);Death(rig,v,t);Steps(rig,t);Cables(rig,t);Spill(rig,v,t);}
            Flush();Sparks=Vfx.Sparks;
        }
        // The telegraph hash over the quads added since `from`.
        private void TeleHash(int from)
        {
            uint h=TelegraphHash;
            for(int q=from;q<Quads.Count;q++)
            {
                for(int k=0;k<12;k++)h=(h^(uint)BitConverter.SingleToInt32Bits(Quads.Corners[q*12+k]))*16777619u;
                for(int k=0;k<4;k++)h=(h^(uint)BitConverter.SingleToInt32Bits(Quads.Colors[q*4+k]))*16777619u;
                h=(h^Quads.Modes[q])*16777619u;h=(h^Quads.Additive[q])*16777619u;h=(h^Quads.Layers[q])*16777619u;
            }
            TelegraphHash=h;
        }

        // ---------------- glows per joint ----------------
        private void Glows(StationBossRig rig,in StationBossView v,float t)
        {
            float hit=StationBossView.At(rig.LastHit)?t-rig.LastHit:99;float flick=hit>=0&&hit<.35f?1-.7f*(float)Math.Abs(Math.Sin(hit*55)):1;
            bool dead=v.Dead(t);float d=dead?t-v.diedAt:-1;
            for(int side=0;side<2;side++)for(int k=0;k<4;k++)
            {
                int j=NativeStationBossLook.Window(side,k);
                // The passengers moving behind the glass, a flicker on every hit; dying one by one from the rear (1.2 to 2.5 s).
                float life=.86f+.14f*(float)Math.Sin(t*(1.3+.4*k)+side*2+k)+.08f*BossBreath.Idle(t,(uint)(31+k+side*4),0,2.5f);
                float order=Rank(k);float dark=dead?BossMath.Smoother((d-1.2f-order*.17f-side*.08f)/.25f):0;
                Glow[j]=Math.Max(0,life*flick*(1-dark)*(1+.4f*Stagger(v,t)));
            }
            float alive=dead?1-BossMath.Smoother((d-1.2f)/1.3f):1;
            Glow[NativeStationBossLook.CabGlow]=(.9f+.1f*(float)Math.Sin(t*2.1f))*flick*alive;Glow[NativeStationBossLook.Markers]=v.LampBroken(t)?.4f*alive:alive;
            // The headlamp: dark once shot out (a spluttering ember for a moment), flaring with the lamp channel; dying in the death.
            float lamp=rig.Channels[StationBossCh.Lamp];if(v.LampBroken(t)){float k=t-v.lampBrokenAt;lamp=k<.6f?(Frac(k*17)<.5f?.6f:0)*(1-k/.6f):0;}
            if(dead){float k=d-1.2f;lamp*=k<0?1:k<1.3f?(Frac(k*9)<.6f?1-k/1.3f:.1f):0;}
            Glow[NativeStationBossModel.Lamp]=lamp;
            Glow[NativeStationBossModel.Heart]=(.85f+.9f*rig.HeartPulse+1.6f*Stagger(v,t))*alive;
            // The Stationmaster's eyes and watch (the watch blazes when held up), the passengers' veins on the legs.
            float watch=1+1.2f*BossMath.Clamp01((-rig.Channels[StationBossCh.SRaP]-90)/80);
            Glow[NativeStationBossModel.SmHead]=dead?1-BossMath.Smoother((d-2.5f)/.8f):1;Glow[NativeStationBossModel.SmRHand]=watch*(dead?1-BossMath.Smoother((d-3.0f)/.6f):1);
            for(int i=0;i<6;i++){Glow[NativeStationBossModel.Hip(i)]=alive;Glow[NativeStationBossModel.Knee(i)]=alive;}
            // The pantograph's insulators glow blue while it is raised; dark once snapped.
            float pan=v.PantoBroken(t)?0:.4f+1.6f*BossMath.Clamp01(rig.PantoRaise);Glow[NativeStationBossModel.PantoHead]=pan;
            Glow[NativeStationBossLook.HornL]=Glow[NativeStationBossLook.HornR]=1+2*rig.Channels[StationBossCh.Horn];
        }
        private static float Rank(int k)=>k==2?0:k==0?1:k==1?2:3;
        private static float Stagger(in StationBossView v,float t){if(!StationBossView.At(v.staggerAt)||v.staggerCause=="part")return 0;float k=t-v.staggerAt;return k<0||k>v.staggerSeconds?0:(float)Math.Exp(-k*1.5f)*Math.Min(1,k/.08f);}

        // ---------------- the headlamp: beam, glare, its light; dust motes drifting in the beam ----------------
        private void Headlamp(StationBossRig rig,in StationBossView v,float t,bool tunnel)
        {
            float k=Glow[NativeStationBossModel.Lamp];if(k<=.02f)return;var p=rig.LampPos;var d=rig.LampDir;
            float arrive=rig.Arriving?1.4f:1;
            // The glare (a hot core and a wide bloom) and the beam: soft widening strips fading down its length.
            Vfx.Glow(p,new Vector3(1.3f,1.35f,1.45f)*(k*.8f),1.0f*k*arrive,NativeBossVfx.Core);Vfx.Glow(p,new Vector3(.32f,.34f,.4f)*(k*.55f),(tunnel?8.4f:3.2f)*k,NativeBossVfx.Mote);Beam++;
            float len=tunnel?32:18,width=1.3f;int n=Math.Max(5,(int)Math.Round(9*(.4f+.6f*quality)));
            for(int i=0;i<n;i++)
            {
                float a0=len*i/n,a1=len*(i+1.6f)/n,hw=width*(.22f+.95f*i/n),f=(float)Math.Pow(1-(float)i/n,1.5f)*k*.42f;
                Strip(p+d*a0,p+d*a1,hw,.82f*f,.86f*f,.95f*f,.5f,1,true);Beam++;
            }
            // Dust hanging in the beam: motes drifting slowly through the cone, lit by it (brightest near the lamp).
            var side=Vector3.Normalize(Vector3.Cross(d,Math.Abs(d.Y)<.95f?Vector3.UnitY:Vector3.UnitX));var up=Vector3.Cross(side,d);int motes=Budget(28);
            for(int i=0;i<motes;i++)
            {
                float along=(float)Frac(H(i,201)+t*.035f*(.5f+(float)H(i,202)))*len*.8f+.6f,cone=width*(.22f+.95f*along/len)*.8f;
                float ang=(float)(H(i,203)*Math.PI*2+t*.3f*(H(i,204)-.5f)),rr=cone*(float)Math.Sqrt(H(i,205));
                var at=p+d*along+side*(float)Math.Cos(ang)*rr+up*(float)Math.Sin(ang)*rr;float lit=k*(float)Math.Pow(1-along/len,1.3f)*(.5f+.5f*(float)Math.Sin(t*1.7f+i));
                Vfx.Glow(at,new Vector3(.9f,.92f,1f)*(.55f*lit),.05f+.04f*(float)H(i,206),NativeBossVfx.Mote);
            }
            // Locked on Departure's lane: a hard white line down the beam.
            if(rig.LampLocked)Strip(p,p+d*len*.5f,.07f,1.6f*k,1.6f*k,1.7f*k,.9f,0,true);
            float yaw=(float)Math.Atan2(d.X,d.Z);if(yaw<0)yaw+=BossMath.Tau;AddLight(p+d*2.4f,tunnel?30:17,.95f*k*1.6f,.97f*k*1.6f,1.0f*k*1.6f,1+yaw);
        }
        // ---------------- the Heart through the torn rear (and the open roof) ----------------
        private void Heart(StationBossRig rig,in StationBossView v,float t)
        {
            float k=Glow[NativeStationBossModel.Heart];if(k<=.02f)return;var h=rig.Shown.Position[NativeStationBossModel.Heart];var back=BossMath.Rotate(rig.Shown.Rotation[NativeStationBossModel.Body],new Vector3(0,0,-.55f));
            Vfx.Glow(h+back,new Vector3(1.0f,.62f,.22f)*(k*.45f*.6f),2*(1.1f+.4f*k),NativeBossVfx.Mote);Glints++;
            if(rig.RoofOpen>.3f){var top=h+BossMath.Rotate(rig.Shown.Rotation[NativeStationBossModel.Body],new Vector3(0,1.4f,1.2f));Vfx.Glow(top,new Vector3(.5f,.3f,.1f)*(k*rig.RoofOpen*.5f),4.4f*rig.RoofOpen,NativeBossVfx.Mote);}
            AddLight(h+back*2,7,1.0f*k*.9f,.66f*k*.9f,.3f*k*.9f,0);
        }
        // ---------------- the PA horns: amber rings on every announcement ----------------
        private void Horns(StationBossRig rig,in StationBossView v,float t)
        {
            float k=rig.Channels[StationBossCh.Horn];
            // The chime's notes (Departure, Terminus) and the arrival's chime hit ring the horns too.
            if(rig.ActMove>=0){var m=NativeStationBoss.Moves[rig.ActMove];float s=m.Anticipation>0?rig.Act.HitAt/m.Anticipation:1;if(m.Chime>0)for(int n=0;n<NativeStationBoss.ChimeNotes;n++){float a=rig.Act.Age-m.Note(n)*s;if(a>=0&&a<.6f)k=Math.Max(k,1-a/.6f);}}
            if(rig.Arriving){var b=NativeStationBossBeats.Current;float a=rig.ArrivalAge-b.Drop;if(a>=0&&a<1)k=Math.Max(k,1-a);}
            if(k<=.02f)return;
            for(int side=0;side<2;side++)
            {
                int j=side==0?NativeStationBossLook.HornL:NativeStationBossLook.HornR;var q=rig.Shown.Rotation[j];var mouth=rig.Shown.Position[j]+BossMath.Rotate(q,new Vector3(0,.38f,.42f));var dir=BossMath.Rotate(q,Vector3.UnitZ);
                for(int r=0;r<3;r++){float ph=(float)Frac(t*1.6f+r/3.0);var c=mouth+dir*(ph*1.6f);int q0=Quads.Count;Ring(c,dir,.25f+ph*1.1f,.04f,R(WarnHex)*k*(1-ph),G(WarnHex)*k*(1-ph),B(WarnHex)*k*(1-ph),.8f);Telegraph++;TeleHash(q0);}
                Vfx.Glow(mouth,new Vector3(R(WarnHex),G(WarnHex)*.8f,B(WarnHex)*.5f)*(k*.7f),.9f,NativeBossVfx.Core);
            }
        }
        // ---------------- Departure's lane: amber rails and chevrons pulsing on each note, locked on the fourth, red at the launch ----------------
        private void Lane(StationBossRig rig,in StationBossView v,float t)
        {
            if(rig.ActMove<0||NativeStationBoss.Moves[rig.ActMove].Id!="departure")return;var m=NativeStationBoss.Moves[rig.ActMove];var a=rig.Act;
            float s=m.Anticipation>0?a.HitAt/m.Anticipation:1,lockAt=m.Note(NativeStationBoss.ChimeNotes-1)*s;
            Vector3 p0,p1;
            if(StationBossView.At(v.laneX0)&&StationBossView.At(v.laneX1)){p0=new Vector3(v.laneX0,0,v.laneZ0);p1=new Vector3(v.laneX1,0,v.laneZ1);}
            else{var f=BossMath.Forward(rig.Yaw);p0=rig.Root+f*4.5f;p1=rig.Root+f*(4.5f+m.Reach);}
            float life=a.Age<a.HitAt?BossMath.Smoother(a.Age/.25f):1-BossMath.Smoother((a.Age-a.HitAt)/(m.Strike*.9f));
            float note=0;for(int n=0;n<NativeStationBoss.ChimeNotes;n++){float k=a.Age-m.Note(n)*s;if(k>=0&&k<.3f)note=Math.Max(note,1-k/.3f);}
            if(life>.01f){int q0=Quads.Count;DrawLane(p0,p1,m.Radius,a.Age>=lockAt,a.Age>=a.HitAt,a.Age-a.HitAt,note,life,a.Age);TeleHash(q0);}
            if(!rig.DoubleCharge)return;
            // TERMINUS's second charge: the lane swings round with the turn (from the boss along its facing), two chime notes, locks
            // as the turn ends and burns red through the second charge.
            var dd=StationBossMoves.DepartureDouble;float after=dd.Seconds-dd.HitAt,k2=after>0?Math.Max(.3f,(a.Duration-a.HitAt)/after):1;
            float turnAt=a.HitAt+m.Strike*k2,second=a.HitAt+StationBossMoves.SecondCharge*k2,age=a.Age;
            float life2=age<turnAt?0:age<second?BossMath.Smoother((age-turnAt)/.2f):1-BossMath.Smoother((age-second)/(m.Strike*.9f*k2));if(life2<=.01f)return;
            var fw=BossMath.Forward(rig.Yaw);var q0l=rig.Root+fw*4.5f;var q1=q0l+fw*m.Reach;
            float note2=0;for(int n=0;n<2;n++){float k=age-(turnAt+(.1f+.25f*n)*k2);if(k>=0&&k<.3f)note2=Math.Max(note2,1-k/.3f);}
            int q2=Quads.Count;DrawLane(q0l,q1,m.Radius,age>=second-.15f*k2,age>=second,age-second,note2,life2,age);TeleHash(q2);
        }
        // A charge lane on the floor: the edges, the fill, the chevrons racing toward the far end (standing still once locked);
        // amber through the telegraph, red from the strike.
        private void DrawLane(Vector3 p0,Vector3 p1,float hw,bool locked,bool strike,float sinceStrike,float note,float life,float age)
        {
            int hex=strike?DangerHex:WarnHex;float glow=(locked?1.35f:.7f+.6f*note)*life*(strike?1.6f*(1-BossMath.Smooth(sinceStrike/.4f))+.4f:1);
            var along=p1-p0;along.Y=0;float L=along.Length();if(L<.5f)return;var u=along/L;var side=new Vector3(-u.Z,0,u.X);float y=.035f;
            for(int sg=-1;sg<=1;sg+=2){var c=p0+u*(L/2)+side*(sg*hw);FlatRect(c,y,u,L/2,side,.07f,R(hex)*glow*1.6f,G(hex)*glow*1.6f,B(hex)*glow*1.6f,.95f);Telegraph++;}
            FlatRect(p0+u*(L/2),y-.004f,u,L/2,side,hw,R(hex)*glow*.16f,G(hex)*glow*.16f,B(hex)*glow*.16f,.5f);Telegraph++;
            int count=(int)(L/2.2f);float wave=locked?0:(float)Frac(age/.3f);
            for(int i=0;i<count;i++)
            {
                float d=1.1f+(i+wave)*2.2f;if(d>L-.4f)continue;var c=p0+u*d;float fade=(1-(float)i/count*.55f)*glow;
                for(int sg=-1;sg<=1;sg+=2){var a0=c+side*(sg*hw*.82f);var a1=c+u*.75f;StripFlat(a0,a1,y+.002f,.07f,R(hex)*fade*1.2f,G(hex)*fade*1.2f,B(hex)*fade*1.2f,.85f);Telegraph++;}
            }
        }
        // ---------------- All Aboard: the doors' light blades and the reach rectangle; the hold's drag marks ----------------
        private void Doors(StationBossRig rig,in StationBossView v,float t)
        {
            if(rig.ActMove<0||NativeStationBoss.Moves[rig.ActMove].Id!="doors")return;var a=rig.Act;var m=NativeStationBoss.Moves[rig.ActMove];
            int side=rig.ActSide==1?1:-1;var body=rig.Shown.Rotation[NativeStationBossModel.Body];var right=BossMath.Right(rig.Yaw);var fwd=BossMath.Forward(rig.Yaw);
            var door=rig.Root+right*(side*(NativeStationBossArena.DoorX+.05f))+fwd*NativeStationBossArena.DoorZ;
            float tele=a.Age<a.HitAt?BossMath.Smoother(a.Age/.3f):1-BossMath.Smoother((a.Age-a.HitAt-m.Strike)/.4f);bool strike=a.Age>=a.HitAt;
            if(tele>.01f)
            {
                int hex=strike?DangerHex:WarnHex;float pulse=strike?1.4f:.75f+.25f*(float)Math.Sin(a.Age*BossMath.Tau*3);var c=door+right*(side*(m.Reach/2+.1f));int q0=Quads.Count;
                FlatOutline(c,.03f,right,m.Reach/2,fwd,m.Radius*.81f,.06f,R(hex)*pulse*tele*1.5f,G(hex)*pulse*tele*1.5f,B(hex)*pulse*tele*1.5f);Telegraph+=4;
                FlatRect(c,.028f,right,m.Reach/2,fwd,m.Radius*.81f,R(hex)*pulse*tele*.14f,G(hex)*pulse*tele*.14f,B(hex)*pulse*tele*.14f,.45f);Telegraph++;TeleHash(q0);
            }
            // Light blades through the cracked doors, the full spill once they slam open; the slam's dust.
            float open=BossMath.Clamp01(rig.Channels[side<0?StationBossCh.DoorL:StationBossCh.DoorR]);if(open>.02f)
            {
                float k=open<.2f?.6f:1.2f;var c=door+right*(side*(.6f+open*1.2f));FlatRect(c,.02f,right,.6f+open*1.2f,fwd,.3f+open*.5f,R(0xFFB45A)*k*.35f,G(0xFFB45A)*k*.35f,B(0xFFB45A)*k*.35f,.6f,true,1);
                AddLight(door+right*(side*1.2f)+Vector3.UnitY*1.5f,6,1.0f*k*.7f,.7f*k*.7f,.35f*k*.7f,0);
            }
            if(a.Age>=a.HitAt&&a.Age<a.HitAt+2.2f)Vfx.SmokeBurst(a.Age,door+right*(side*.6f)+new Vector3(0,.5f,0),right*(side*1.8f)+new Vector3(0,.3f,0),.6f,.25f,8,1.8f,.6f,2.2f,DustC,.4f,a.HitAt,.1f,vseed,90,0,0,default,0.38f);
        }
        // ---------------- Platform Alteration: rings and shadows under each target; the glass in the preview ----------------
        private void Alteration(StationBossRig rig,in StationBossView v,float t)
        {
            if(rig.ActMove<0||NativeStationBoss.Moves[rig.ActMove].Id!="alteration"||GlassCount==0)return;var a=rig.Act;var m=NativeStationBoss.Moves[rig.ActMove];
            float land=a.HitAt+m.Strike;float age=a.Age;
            for(int i=0;i<GlassCount;i++)
            {
                float x=GlassXZ[i*2],z=GlassXZ[i*2+1];
                if(age<land&&age>=a.HitAt*.5f)
                {
                    float u=BossMath.Clamp01((age-a.HitAt*.5f)/(land-a.HitAt*.5f)),pulse=.7f+.3f*(float)Math.Sin(age*BossMath.Tau*4);
                    int q0=Quads.Count;RingFlat(new Vector3(x,.035f,z),m.Radius,.1f,R(WarnHex)*pulse*1.4f,G(WarnHex)*pulse*1.4f,B(WarnHex)*pulse*1.4f,.85f);Telegraph+=12;TeleHash(q0);
                    if(DrawGlass){Flat(x,.03f,z,.4f+u*1.2f,.4f+u*1.2f,0,0,0,0,.25f+.5f*u,false,1);float fy=NativeExpeditionStationKit.VaultY(z)-.5f-u*u*12;
                        var pane=new Vector3(x,Math.Max(.6f,fy),z);Vfx.Spark(pane,new Vector3(0,-24*u-1,0),new Vector3(1.4f,1.6f,1.8f)*.8f,.3f+.3f*u,.4f,.03f,.6f);Vfx.Glow(pane,new Vector3(.9f,1f,1.1f)*.6f,.9f,NativeBossVfx.Mote);Glints++;}
                }
                else if(DrawGlass&&age>=land&&age<land+3.0f)
                {
                    // The pane bursting on the floor: glass shards skittering out and settling, a white flash, a little dust.
                    var c=new Vector3(x,.05f,z);Vfx.RubbleBurst(age,c+new Vector3(0,.1f,0),Vector3.UnitY,1.6f,4.2f,16,3.0f,land,.07f,GlassC,vseed,120+i,0,0);
                    Vfx.SparkBurst(age,c,Vector3.UnitY,1.2f,3.5f,14,.35f,land,.03f,vseed,140+i,.55f,.02f,1.5f);
                    float k=age-land;if(k<.3f)Vfx.Glow(new Vector3(x,.4f,z),new Vector3(.8f,.9f,1f)*(.6f*(1-k/.3f)),2.4f*(1-k/.3f)+.4f,NativeBossVfx.Mote);
                    Vfx.SmokeBurst(age,c+new Vector3(0,.2f,0),new Vector3(0,.4f,0),1.4f,.2f,5,1.8f,.5f,1.9f,DustC,.32f,land,.08f,vseed,160+i,0,0,default,0.38f);Glints+=12;
                }
            }
        }
        // ---------------- Live Rail: the pantograph's arcs to the vault, the puddles warned then live, arcs from the hands ----------------
        private void LiveRail(StationBossRig rig,in StationBossView v,float t)
        {
            bool raised=rig.PantoRaise>.35f&&!v.PantoBroken(t);bool live=v.Live(t);bool moving=rig.ActMove>=0&&NativeStationBoss.Moves[rig.ActMove].Id=="live-rail";
            float tele=moving&&rig.Act.Age<rig.Act.HitAt?BossMath.Smoother(rig.Act.Age/.3f):0;
            if(!raised&&!live&&tele<=0)return;
            var head=rig.Shown.Position[NativeStationBossModel.PantoHead];var q=rig.Shown.Rotation[NativeStationBossModel.PantoHead];
            if(raised)
            {
                // Two or three arcs from the head's horns up to the vault's iron, re-struck on a 12 Hz jitter, flickering out between.
                int n=live?3:2;
                for(int k=0;k<n;k++)
                {
                    var from=head+BossMath.Rotate(q,new Vector3((k-1)*.6f,.12f,0));uint seed=(uint)(Math.Floor(t*12)+k*31);
                    var to=new Vector3(from.X+(float)(H((int)seed,1)-.5)*7,0,from.Z+(float)(H((int)seed,2)-.5)*6);to.Y=NativeExpeditionStationKit.VaultY(to.Z)-.6f;
                    Bolt(from,to,(int)seed,(live?1.6f:.9f)*Strike(t,(int)seed),.06f);Arcs++;
                }
                Vfx.Glow(head+new Vector3(0,.1f,0),new Vector3(R(LiveHex),G(LiveHex),B(LiveHex))*(1.2f*.7f),2.4f,NativeBossVfx.Mote);
            }
            for(int i=0;i<NativeStationBossArena.PuddleCount;i++)
            {
                float x=NativeStationBossArena.Puddles[i*3],z=NativeStationBossArena.Puddles[i*3+1],r=NativeStationBossArena.Puddles[i*3+2];
                if(live)
                {
                    float crackle=.75f+.25f*(float)Math.Sin(t*41+i*7)*(float)Math.Sin(t*13+i);int q0=Quads.Count;
                    Flat(x,.03f,z,r,r,0,R(LiveHex)*.5f*crackle,G(LiveHex)*.5f*crackle,B(LiveHex)*.5f*crackle,.7f,true,1);RingFlat(new Vector3(x,.035f,z),r,.08f,R(LiveHex)*1.6f,G(LiveHex)*1.6f,B(LiveHex)*1.6f,.85f);Telegraph+=13;TeleHash(q0);
                    // Little arcs dancing on the water, each strike a crack of sparks.
                    int sparks=Budget(3);for(int k=0;k<sparks;k++){uint seed=(uint)(Math.Floor(t*15)+i*97+k*13);float ang=(float)(H((int)seed,4)*Math.PI*2),rr=(float)H((int)seed,5)*r;var a0=new Vector3(x+(float)Math.Cos(ang)*rr,.04f,z+(float)Math.Sin(ang)*rr);
                        Bolt(a0,a0+new Vector3((float)(H((int)seed,6)-.5),.5f+(float)H((int)seed,7)*.6f,(float)(H((int)seed,8)-.5)),(int)seed,Strike(t*1.25f,(int)seed),.025f);Arcs++;}
                }
                else if(tele>0)
                {
                    float pulse=(.6f+.4f*(float)Math.Sin(t*BossMath.Tau*3))*tele;int q0=Quads.Count;RingFlat(new Vector3(x,.035f,z),r,.1f,R(WarnHex)*pulse*1.4f,G(WarnHex)*pulse*1.4f,B(WarnHex)*pulse*1.4f,.85f);
                    Flat(x,.03f,z,r,r,0,R(LiveHex)*.15f*pulse,G(LiveHex)*.15f*pulse,B(LiveHex)*.15f*pulse,.5f,true,1);Telegraph+=13;TeleHash(q0);
                }
            }
            // Arcs from the kneeling hands to the nearest puddles while live.
            if(live&&moving)for(int leg=0;leg<2;leg++)
            {
                var h=rig.Hand(leg);int best=0;float bd=1e9f;for(int i=0;i<NativeStationBossArena.PuddleCount;i++){float dx=NativeStationBossArena.Puddles[i*3]-h.X,dz=NativeStationBossArena.Puddles[i*3+1]-h.Z,dd=dx*dx+dz*dz;if(dd<bd){bd=dd;best=i;}}
                if(bd>14*14)continue;int s2=(int)Math.Floor(t*12)+leg*7;Bolt(h,new Vector3(NativeStationBossArena.Puddles[best*3],.05f,NativeStationBossArena.Puddles[best*3+1]),s2,1.2f*Strike(t,s2),.05f);Arcs++;
            }
            if(live||raised)AddLight(head+Vector3.UnitY*.5f,9,R(LiveHex)*1.4f*Strike(t,3),G(LiveHex)*1.4f*Strike(t,3),B(LiveHex)*1.4f*Strike(t,3),0);
        }
        // An arc's strength within its 1/12 s strike: full at the strike, decaying, a flicker (a pure function of the clock).
        private static float Strike(float t,int seed){float ph=(float)Frac(t*12);return (.55f+.45f*(float)H(seed,77))*(.45f+.55f*(float)Math.Exp(-ph*3.2f))*(.85f+.15f*(float)Math.Sin(t*173+seed));}
        // ---------------- Terminus: the reach ring through the rear-up, the slam's ring of lifted tiles rolling out ----------------
        private void Terminus(StationBossRig rig,in StationBossView v,float t)
        {
            if(rig.ActMove<0||NativeStationBoss.Moves[rig.ActMove].Id!="terminus")return;var a=rig.Act;var m=NativeStationBoss.Moves[rig.ActMove];
            Vector3 c;if(StationBossView.At(v.laneX0))c=new Vector3(v.laneX0,0,v.laneZ0);else c=rig.Root+BossMath.Forward(rig.Yaw)*6.6f;
            if(a.Age<a.HitAt)
            {
                float tele=BossMath.Smoother(a.Age/.4f),pulse=.7f+.3f*(float)Math.Sin(a.Age*BossMath.Tau*2.86f);int q0=Quads.Count;
                RingFlat(new Vector3(c.X,.035f,c.Z),m.Reach,.12f,R(WarnHex)*pulse*tele*1.5f,G(WarnHex)*pulse*tele*1.5f,B(WarnHex)*pulse*tele*1.5f,.85f);
                RingFlat(new Vector3(c.X,.035f,c.Z),m.Radius,.18f,R(DangerHex)*pulse*tele*1.2f,G(DangerHex)*pulse*tele*1.2f,B(DangerHex)*pulse*tele*1.2f,.85f);Telegraph+=24;TeleHash(q0);
                return;
            }
            float k=a.Age-a.HitAt,run=m.Reach/m.Speed;
            if(k<run+.4f)
            {
                // The lip: a ring of lifted floor tiles rolling out at 12 m/s to 10 m (0.6 m tall), red-hot at the front.
                float r=Math.Min(m.Reach,.5f+m.Speed*k),fade=k<run?1:1-(k-run)/.4f;int q0=Quads.Count;
                RingWall(new Vector3(c.X,0,c.Z),r,.6f*fade,R(DangerHex)*1.3f*fade,G(DangerHex)*1.3f*fade,B(DangerHex)*1.3f*fade);
                RingFlat(new Vector3(c.X,.04f,c.Z),r,.35f,R(WarnHex)*fade,G(WarnHex)*fade,B(WarnHex)*fade,.8f);Telegraph+=24;TeleHash(q0);
            }
            // The slam: dust thrown up behind the rolling lip (born where the lip passed), tiles flung, a cloud at the centre.
            if(k<4.5f)
            {
                int puffs=Budget(22);for(int i=0;i<puffs;i++){float ang=(float)(i*BossMath.Tau/puffs+H(i,9));var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));float rr=1.2f+(m.Reach-1.4f)*(float)H(i,4),born=a.HitAt+Math.Max(0,rr-.5f)/m.Speed;
                    Vfx.SmokeBurst(a.Age,c+d*rr+new Vector3(0,.3f,0),d*1.6f+new Vector3(0,.9f,0),.35f,.3f,1,2.6f,.8f,2.8f,DustC,.5f,born,.02f,vseed,300+i,0,0,default,0.38f);Dust++;}
                Vfx.SmokeBurst(a.Age,c+new Vector3(0,.4f,0),new Vector3(0,1.4f,0),1.6f,.35f,10,3.2f,1.4f,3.8f,DustC,.55f,a.HitAt,.15f,vseed,330,0,0,default,0.38f);
                Vfx.RubbleBurst(a.Age,c+new Vector3(0,.2f,0),Vector3.UnitY,1.0f,6.5f,16,3.6f,a.HitAt,.16f,Tile,vseed,340);
                Vfx.SparkBurst(a.Age,c+new Vector3(0,.1f,0),Vector3.UnitY,1.2f,5.5f,30,.7f,a.HitAt,.05f,vseed,350,1);
            }
        }
        // ---------------- brakes: sparks off the skidding hands and the wheels, hot shoes, scorch along the skid ----------------
        private void Brakes(StationBossRig rig,in StationBossView v,float t)
        {
            float skid=0;
            if(rig.ActMove>=0&&NativeStationBoss.Moves[rig.ActMove].Id=="departure"){var a=rig.Act;var m=NativeStationBoss.Moves[rig.ActMove];float k=a.Age-a.HitAt-m.Strike;if(k>-.15f&&k<m.Follow)skid=1-BossMath.Clamp01(k/m.Follow);
                // The crouch digs the hands in: sparks scraping off the iron wrists through the chime.
                if(a.Age>.3f&&a.Age<a.HitAt+.2f)for(int leg=0;leg<2;leg++){Vfx.SparkFlow(a.Age,rig.Hand(leg)+new Vector3(0,.05f,0),new Vector3(0,1,0)-BossMath.Forward(rig.Yaw)*.6f,.5f,2.6f,30,.45f,.3f,a.HitAt,vseed,400+leg,.85f,.02f,1.4f);Sparks++;}
                // The skid: fountains of sparks thrown back off both hands, the brake shoes glowing, a scorch along the floor.
                for(int leg=0;leg<2;leg++)
                {
                    var h=rig.Hand(leg);var back=-BossMath.Forward(rig.Yaw);float t0=a.HitAt+m.Strike-.15f,t1=a.HitAt+m.Strike+m.Follow;
                    Vfx.SparkFlow(a.Age,h+new Vector3(0,.06f,0),back+Vector3.UnitY*.55f,.35f,7.5f,80,.75f,t0,t1,vseed,410+leg,1.15f,.024f,1.2f,true,0,default,skid);
                    if(skid>0){Vfx.Glow(h+new Vector3(0,.05f,0),BrakeHot*(.7f*skid),.7f,NativeBossVfx.Core);Flat(h.X,.02f,h.Z,.35f,1.6f,rig.Yaw,0,0,0,.35f*skid,false,1);}
                    Vfx.SmokeFlow(a.Age,h+new Vector3(0,.2f,0),back*1.2f+new Vector3(0,.6f,0),.3f,.35f,8,1.6f,.4f,1.8f,DustC,.35f,t0,t1,vseed,420+leg,0,0,default,default,skid,0.38f);
                }
            }
        }
        // ---------------- the arrival: brake sparks off the wheels on the accents, hot wheels, steam, the burst's debris ----------------
        private void Arrival(StationBossRig rig,in StationBossView v,float t)
        {
            if(!rig.Arriving)return;var b=NativeStationBossBeats.Current;float a=rig.ArrivalAge;var body=rig.Shown.Position[NativeStationBossModel.Body];var q=rig.Shown.Rotation[NativeStationBossModel.Body];
            for(int i=0;i<2;i++)
            {
                float k=a-b.Accent(i);if(k<-.1f||k>2.2f)continue;float s=k<0?0:1-BossMath.Clamp01(k/.9f);
                for(int w=0;w<6;w++){var wp=rig.Shown.Position[NativeStationBossModel.Wheel(w)]+Vector3.UnitY*-.45f;var dir=BossMath.Rotate(q,new Vector3(NativeStationBossModel.Side(w)*.4f,.8f,-1));
                    Vfx.SparkFlow(a,wp,dir,.35f,6.5f,50,.8f,b.Accent(i)-.05f,b.Accent(i)+.9f,vseed,500+i*8+w,1.2f,.022f,1.2f,true,0,default,s);
                    float hot=k<0?0:Math.Max(0,1-k/2.2f);if(hot>0)Vfx.Glow(wp+Vector3.UnitY*.2f,BrakeHot*(.55f*hot),.6f,NativeBossVfx.Core);}
                Vfx.SmokeBurst(a,body+BossMath.Rotate(q,new Vector3(0,-.5f,-1)),BossMath.Rotate(q,new Vector3(0,.8f,-1.6f)),.8f,.5f,6,2.2f,.8f,3.2f,SteamC,.42f,b.Accent(i),.4f,vseed,520+i,0,NativeBossVfx.NoFloor,default,0.42f);Steam++;
            }
            // The stop's last screech and the brakes' steam.
            {float k=a-b.Accent1;if(k>0&&k<3.2f)for(int sd=-1;sd<=1;sd+=2)Vfx.SmokeBurst(a,body+BossMath.Rotate(q,new Vector3(sd*1.4f,-.3f,sd*1.2f)),BossMath.Rotate(q,new Vector3(sd*1.8f,.6f,0)),.5f,.6f,7,2.4f,.7f,3.0f,SteamC,.4f,b.Accent1,.6f,vseed,530+sd,0,NativeBossVfx.NoFloor,default,0.42f);}
            // The burst through the gate: sparks and dust at the barrier line, steel and tile flying.
            {float k=a-b.Drop;if(k>-.1f&&k<4f){var gate=new Vector3(rig.Root.X,0,NativeStationBossArena.BarrierZ);
                for(int sd=-1;sd<=1;sd+=2){Vfx.SparkBurst(a,gate+new Vector3(sd*3,.8f,0),new Vector3(sd*.5f,1,.6f),.6f,7,40,.9f,b.Drop,.2f,vseed,540+sd,1.2f);
                    Vfx.RubbleBurst(a,gate+new Vector3(sd*3,1.0f,0),new Vector3(sd*.6f,1,.8f),.6f,6.5f,8,3.6f,b.Drop,.18f,Steel,vseed,545+sd);}
                Vfx.SmokeBurst(a,gate+new Vector3(0,.6f,0),new Vector3(0,.8f,1.2f),2.4f,.35f,18,3.4f,1.4f,4.2f,DustC,.5f,b.Drop,.25f,vseed,550,0,0,default,0.38f);}}
        }
        // ---------------- the phase changes: the barrier's smash, the board's crash, the roof's tear ----------------
        private void Changes(StationBossRig rig,in StationBossView v,float t)
        {
            if(v.BarrierDown(t)){float k=t-v.barrierDownAt;if(k<4.5f){var c=new Vector3(rig.Root.X,0,NativeStationBossArena.BarrierZ);
                for(int sd=-1;sd<=1;sd+=2){Vfx.SparkBurst(t,c+new Vector3(sd*6,.7f,0),new Vector3(sd*.4f,1,-.6f),.6f,7,34,1.0f,v.barrierDownAt,.2f,vseed,600+sd,1.3f);
                    Vfx.RubbleBurst(t,c+new Vector3(sd*5,.9f,0),new Vector3(sd*.5f,1,-.7f),.6f,6,10,4.0f,v.barrierDownAt,.2f,Steel,vseed,605+sd);
                    for(int i=0;i<Budget(6);i++){Vfx.SmokeBurst(t,c+new Vector3(sd*(3+i*2.2f),.5f,(float)H(i,sd+4)-.5f),new Vector3(sd*.4f,.7f,-.4f),.6f,.3f,1,3.0f,1.0f,3.4f,DustC,.48f,v.barrierDownAt,.15f,vseed,610+i*3+sd,0,0,default,0.38f);Dust++;}}}}
            if(v.BoardDown(t)){float hit=v.boardDownAt+NativeStationBossStage.BoardFallSeconds*.62f,k=t-hit;if(k>-.05f&&k<5f){var c=new Vector3(NativeStationBossArena.BoardX,1.2f,NativeStationBossArena.BoardZ);
                Vfx.SparkBurst(t,c+new Vector3(0,2.2f,-1.5f),new Vector3(0,1,-.5f),.8f,7.5f,60,1.2f,hit,.15f,vseed,620,1.3f);
                Vfx.RubbleBurst(t,c+new Vector3(0,.4f,-1),new Vector3(0,1,-.6f),.9f,5,14,4.2f,hit,.15f,Steel,vseed,625);
                for(int i=0;i<Budget(14);i++){Vfx.SmokeBurst(t,c+new Vector3(((float)H(i,30)-.5f)*10,.2f,((float)H(i,31)-.5f)*4),new Vector3(((float)H(i,32)-.5f)*2,.9f,-.6f),.6f,.3f,1,3.6f,1.4f,4.4f,DustC,.5f,hit,.2f,vseed,630+i,0,0,default,0.38f);Dust++;}}}
            if(v.RoofOpen(t)){float k=t-v.roofOpenAt;if(k<2.4f){var q=rig.Shown.Rotation[NativeStationBossModel.Body];var body=rig.Shown.Position[NativeStationBossModel.Body];
                for(int i=0;i<4;i++){var p=body+BossMath.Rotate(q,new Vector3(i%2==0?-1.38f:1.38f,2.7f,1.45f-i*1.2f));Vfx.SparkFlow(t,p,BossMath.Rotate(q,new Vector3(i%2==0?-1:1,1.2f,0)),.5f,4.5f,40,.9f,v.roofOpenAt,v.roofOpenAt+1.2f,vseed,650+i,1.1f,.022f,1.1f,true,0,default,1-BossMath.Clamp01(k/1.4f));}}}
        }
        // ---------------- the breaks: the headlamp's shards, the pantograph's snap, the express's storm of sparks ----------------
        private void Breaks(StationBossRig rig,in StationBossView v,float t)
        {
            if(v.LampBroken(t)){float k=t-v.lampBrokenAt;if(k<3.0f){var p=rig.LampPos;var d=rig.LampDir;
                Vfx.RubbleBurst(t,p,d+new Vector3(0,.3f,0),.8f,5,14,3.0f,v.lampBrokenAt,.05f,GlassC,vseed,700,0,0);Glints+=14;
                if(k<.5f)Vfx.SparkBurst(t,p,d,.9f,4,16,.5f,v.lampBrokenAt,.05f,vseed,705,1);
                if(k<.25f)Vfx.Glow(p,new Vector3(1.6f,1.7f,1.9f)*(1-k/.25f),1.6f,NativeBossVfx.Core);}}
            if(v.PantoBroken(t)){float k=t-v.pantoBrokenAt;if(k<2.2f){var p=rig.Shown.Position[NativeStationBossModel.PantoLow];Vfx.SparkBurst(t,p,Vector3.UnitY,.9f,6,50,1.1f,v.pantoBrokenAt,.3f,vseed,710,1.2f);
                Vfx.RubbleBurst(t,p,Vector3.UnitY,.9f,4,5,2.2f,v.pantoBrokenAt,.1f,Steel,vseed,712);
                if(k<.5f){int s=(int)(t*12);Bolt(p,p+new Vector3(((float)H(s,1)-.5f)*3,2.5f,((float)H(s,2)-.5f)*3),s,1.4f*Strike(t,s)*(1-k/.5f),.05f);}}}
            if(StationBossView.At(v.expressHitAt)&&t>=v.expressHitAt){float k=t-v.expressHitAt;if(k<4.2f){var body=rig.Shown.Position[NativeStationBossModel.Body];var right=BossMath.Right(rig.Yaw);
                for(int i=0;i<3;i++){var p=body+right*1.6f+BossMath.Forward(rig.Yaw)*((i-1)*3)+Vector3.UnitY*(-.5f+i*.4f);Vfx.SparkBurst(t,p,right*-1+Vector3.UnitY*.6f+new Vector3(1,0,0),.7f,9,60,1.2f,v.expressHitAt,.35f,vseed,720+i,1.4f);
                    Vfx.RubbleBurst(t,p,right*-1+Vector3.UnitY*.8f,.7f,7,8,4.0f,v.expressHitAt,.18f,Steel,vseed,725+i);}
                Vfx.SmokeBurst(t,body+right*1.8f,right*-1.5f+new Vector3(0,1,0),1.4f,.4f,16,3.4f,1.4f,4.0f,DustC,.5f,v.expressHitAt,.3f,vseed,730,0,0,default,0.38f);
                if(k<.25f)Vfx.Glow(new Vector3(body.X+right.X*2,body.Y,body.Z+right.Z*2),new Vector3(2.4f,2.2f,1.8f)*(.8f*(1-k/.25f)),10*(1-k/.25f),NativeBossVfx.Mote);}}
        }
        // ---------------- staggers: steam from the underframe, the Heart's flare (its glow) ----------------
        private void Staggers(StationBossRig rig,in StationBossView v,float t)
        {
            float s=Stagger(v,t);if(s<=.02f)return;var q=rig.Shown.Rotation[NativeStationBossModel.Body];var body=rig.Shown.Position[NativeStationBossModel.Body];
            for(int i=0;i<3;i++){var p=body+BossMath.Rotate(q,new Vector3(((float)H(i,50)-.5f)*2.4f,-.6f+(float)H(i,51)*.4f,((float)H(i,52)-.5f)*7));
                Vfx.SmokeFlow(t,p,BossMath.Rotate(q,new Vector3(((float)H(i,53)-.5f)*2,.4f,0)),.4f,.7f,7,2.0f,.5f,2.4f,SteamC,.45f,v.staggerAt,v.staggerAt+v.staggerSeconds,vseed,800+i,0,NativeBossVfx.NoFloor,default,drift,s,0.42f);Steam++;}
            if(v.staggerCause=="crash"){var nose=body+BossMath.Rotate(q,new Vector3(0,0,5.4f));Vfx.SparkBurst(t,nose,BossMath.Rotate(q,new Vector3(0,1,-1)),.8f,6,40,1.0f,v.staggerAt,.2f,vseed,810,1.2f);
                Vfx.SmokeBurst(t,nose,BossMath.Rotate(q,new Vector3(0,.8f,-1)),1.0f,.35f,10,3.0f,1.2f,3.6f,DustC,.5f,v.staggerAt,.2f,vseed,815,0,0,default,0.38f);
                Vfx.RubbleBurst(t,nose+new Vector3(0,.5f,0),BossMath.Rotate(q,new Vector3(0,1,-1)),.8f,5,10,3.5f,v.staggerAt,.16f,Tile,vseed,818);}
        }
        // ---------------- the death: steam venting, the windows dying (their glow), the last breath of steam ----------------
        private void Death(StationBossRig rig,in StationBossView v,float t)
        {
            if(!v.Dead(t))return;float d=t-v.diedAt;var q=rig.Shown.Rotation[NativeStationBossModel.Body];var body=rig.Shown.Position[NativeStationBossModel.Body];
            float vent=1-BossMath.Smooth((d-2.6f)/1.6f);
            if(d<5f)for(int i=0;i<4;i++){var p=body+BossMath.Rotate(q,new Vector3(((float)H(i,61)-.5f)*2.6f,-.5f,((float)H(i,62)-.5f)*8));Vfx.SmokeFlow(t,p,BossMath.Rotate(q,new Vector3(((float)H(i,63)-.5f)*1.5f,1.4f,0)),.4f,.8f,6,2.8f,.6f,2.8f,SteamC,.4f,v.diedAt,v.diedAt+3.8f,vseed,900+i,0,NativeBossVfx.NoFloor,default,default,vent,0.42f);Steam++;}
            if(d>.9f&&d<3.4f)for(int i=0;i<6;i++){var w=rig.Shown.Position[NativeStationBossModel.Wheel(i)];Vfx.SmokeBurst(t,new Vector3(w.X,.3f,w.Z),new Vector3(0,.5f,0),.9f,.25f,2,2.4f,.8f,2.4f,DustC,.42f,v.diedAt+.9f,.15f,vseed,920+i,0,0,default,0.38f);Dust++;}
        }
        // ---------------- footfalls: a puff of dust (and a spark off the iron wrist) where a hand slaps down ----------------
        private void Steps(StationBossRig rig,float t)
        {
            for(int i=0;i<6;i++)if((rig.Landed&(1<<i))!=0)stepAt[i]=t;
            for(int i=0;i<6;i++)
            {
                float k=t-stepAt[i];if(k<0||k>2.0f)continue;var h=rig.Hand(i);int key=950+i*5+((int)(stepAt[i]*7))%5;
                Vfx.SmokeBurst(t,h+new Vector3(0,.1f,0),new Vector3(0,.25f,0),.9f,.2f,4,1.8f,.5f,1.9f,DustC,.36f,stepAt[i],.04f,vseed,key,0,0,default,0.38f);
                if(k<.4f)Vfx.SparkBurst(t,h+new Vector3(0,.05f,0),Vector3.UnitY,1.0f,2.4f,5,.35f,stepAt[i],.02f,vseed,key+1,.8f);Dust++;
            }
        }
        private readonly float[] stepAt={-99,-99,-99,-99,-99,-99};
        // ---------------- the torn cables spark at their ends now and then ----------------
        private void Cables(StationBossRig rig,float t)
        {
            for(int c=0;c<2;c++){int last=(c==0?NativeStationBossModel.CableAFirst:NativeStationBossModel.CableBFirst)+NativeStationBossModel.CableLinks-1;double cyc=t*.7+c*.43;float k=(float)Frac(cyc);if(k>.6f)continue;
                var tip=rig.Shown.Position[last]+BossMath.Rotate(rig.Shown.Rotation[last],new Vector3(0,-.7f,0));float burst=(float)(Math.Floor(cyc)/.7-c*.43/.7);
                Vfx.SparkBurst(t,tip,Vector3.UnitY*-.2f+new Vector3(.2f,0,0),.9f,2.2f,10,.55f,burst,.12f,vseed,980+c*7+(int)Math.Floor(cyc)%7,1);
                if(k<.18f){Vfx.Glow(tip,new Vector3(R(LiveHex),G(LiveHex),B(LiveHex))*(1.6f*(1-k/.18f)),.7f,NativeBossVfx.Core);Bolt(tip,tip+new Vector3(0,-.45f,0)+BossMath.Rotate(rig.Shown.Rotation[last],new Vector3(.25f,0,0)),(int)(t*24)+c,1-k/.18f,.02f);}}
        }
        // ---------------- Next Stop: amber light floods out of every door ----------------
        private void Spill(StationBossRig rig,in StationBossView v,float t)
        {
            if(rig.ActMove<0||NativeStationBoss.Moves[rig.ActMove].Id!="passengers")return;float open=Math.Max(rig.Channels[StationBossCh.DoorL],rig.Channels[StationBossCh.DoorR]);if(open<=.02f)return;
            var right=BossMath.Right(rig.Yaw);var fwd=BossMath.Forward(rig.Yaw);
            for(int sd=-1;sd<=1;sd+=2){var c=rig.Root+right*(sd*(NativeStationBossArena.DoorX+1.4f))+fwd*NativeStationBossArena.DoorZ;FlatRect(c,.02f,right,1.4f,fwd,.9f,R(0xFFB45A)*.5f*open,G(0xFFB45A)*.5f*open,B(0xFFB45A)*.5f*open,.6f,true,1);
                Vfx.Glow(new Vector3(c.X,1.4f,c.Z),new Vector3(R(0xFFB45A),G(0xFFB45A),B(0xFFB45A))*(.4f*open*.5f),2.8f,NativeBossVfx.Mote);}
        }

        // ---------------- primitives ----------------
        private int Budget(int full)=>Math.Max(0,(int)Math.Round(full*(low?.5f:1f)*(.35f+.65f*quality)));
        private void AddLight(Vector3 p,float range,float r,float g,float b,float beam)
        {
            if(LightCount>=MaxLights)return;int i=LightCount++;LightPos[i*4]=p.X;LightPos[i*4+1]=p.Y;LightPos[i*4+2]=p.Z;LightPos[i*4+3]=range;LightRgb[i*3]=r;LightRgb[i*3+1]=g;LightRgb[i*3+2]=b;LightBeam[i]=beam;
        }
        private void Add(double ax,double ay,double az,double bx,double by,double bz,double cx,double cy,double cz,double dx,double dy,double dz,float r,float g,float b,float a,int mode,bool additive)
        {
            if(additive){Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,r,g,b,a,mode,true,1);return;}
            if(pn>=SortMax||!(a>0))return;int o=pn*12;pc[o]=ax;pc[o+1]=ay;pc[o+2]=az;pc[o+3]=bx;pc[o+4]=by;pc[o+5]=bz;pc[o+6]=cx;pc[o+7]=cy;pc[o+8]=cz;pc[o+9]=dx;pc[o+10]=dy;pc[o+11]=dz;
            pcol[pn*4]=r;pcol[pn*4+1]=g;pcol[pn*4+2]=b;pcol[pn*4+3]=a;pmode[pn]=(byte)mode;double mx=(ax+cx)/2-ex,my=(ay+cy)/2-ey,mz=(az+cz)/2-ez;pk[pn]=mx*mx+my*my+mz*mz;pidx[pn]=pn;pn++;
        }
        private void Flush()
        {
            for(int i=1;i<pn;i++){int v=pidx[i];double key=pk[v];int j=i-1;while(j>=0&&pk[pidx[j]]<key){pidx[j+1]=pidx[j];j--;}pidx[j+1]=v;}
            for(int i=0;i<pn;i++){int v=pidx[i],o=v*12;Quads.Add(pc[o],pc[o+1],pc[o+2],pc[o+3],pc[o+4],pc[o+5],pc[o+6],pc[o+7],pc[o+8],pc[o+9],pc[o+10],pc[o+11],pcol[v*4],pcol[v*4+1],pcol[v*4+2],pcol[v*4+3],pmode[v],false,1);}
            pn=0;
        }
        private void Sprite(double x,double y,double z,double size,float r,float g,float b,float a,bool additive=true)
        {
            double sx=rx*size,sy=ry*size,sz=rz*size,tx=ux*size,ty=uy*size,tz=uz*size;
            Add(x-sx-tx,y-sy-ty,z-sz-tz,x+sx-tx,y+sy-ty,z+sz-tz,x+sx+tx,y+sy+ty,z+sz+tz,x-sx+tx,y-sy+ty,z-sz+tz,r,g,b,a,1,additive);
        }
        private void Flat(double x,double y,double z,double hx,double hz,double yaw,float r,float g,float b,float a,bool additive=true,int mode=1)
        {
            double c=Math.Cos(yaw),s=Math.Sin(yaw),ax=c*hx,az=-s*hx,bx=s*hz,bz=c*hz;
            Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,mode,additive);
        }
        // A floor rectangle: centre, half extents along u and v (horizontal unit vectors).
        private void FlatRect(Vector3 c,float y,Vector3 u,float hu,Vector3 v,float hv,float r,float g,float b,float a,bool additive=true,int mode=0)
        {
            var A=c+(-u*hu-v*hv);var B2=c+(u*hu-v*hv);var C=c+(u*hu+v*hv);var D=c+(-u*hu+v*hv);Add(A.X,y,A.Z,B2.X,y,B2.Z,C.X,y,C.Z,D.X,y,D.Z,r,g,b,a,mode,additive);
        }
        private void FlatOutline(Vector3 c,float y,Vector3 u,float hu,Vector3 v,float hv,float w,float r,float g,float b)
        {
            FlatRect(c+v*hv,y,u,hu,v,w,r,g,b,.9f);FlatRect(c-v*hv,y,u,hu,v,w,r,g,b,.9f);FlatRect(c+u*hu,y,u,w,v,hv,r,g,b,.9f);FlatRect(c-u*hu,y,u,w,v,hv,r,g,b,.9f);
        }
        // A soft strip from a to b facing the camera across it (half width w).
        private void Strip(Vector3 a,Vector3 b,float w,float r,float g,float bl,float alpha,int mode,bool additive)
        {
            var mid=(a+b)*.5f;var along=b-a;var toEye=new Vector3((float)(ex-mid.X),(float)(ey-mid.Y),(float)(ez-mid.Z));var side=Vector3.Cross(along,toEye);float l=side.Length();if(l<1e-6f)return;side*=w/l;
            Add(a.X-side.X,a.Y-side.Y,a.Z-side.Z,b.X-side.X,b.Y-side.Y,b.Z-side.Z,b.X+side.X,b.Y+side.Y,b.Z+side.Z,a.X+side.X,a.Y+side.Y,a.Z+side.Z,r,g,bl,alpha,mode,additive);
        }
        // A strip lying on the floor from a to b (half width w).
        private void StripFlat(Vector3 a,Vector3 b,float y,float w,float r,float g,float bl,float alpha)
        {
            var d=b-a;d.Y=0;float l=d.Length();if(l<1e-5f)return;var s=new Vector3(-d.Z,0,d.X)*(w/l);
            Add(a.X-s.X,y,a.Z-s.Z,b.X-s.X,y,b.Z-s.Z,b.X+s.X,y,b.Z+s.Z,a.X+s.X,y,a.Z+s.Z,r,g,bl,alpha,0,true);
        }
        // A thin ring lying flat (radius, line width) in segments.
        private void RingFlat(Vector3 c,float radius,float width,float r,float g,float b,float a)
        {
            int n=Math.Max(16,Math.Min(64,(int)(radius*8)));
            for(int i=0;i<n;i++){double a0=i*Math.PI*2/n,a1=(i+1.04)*Math.PI*2/n;float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1);float r0=radius-width,r1=radius;
                Add(c.X+c0*r0,c.Y,c.Z+s0*r0,c.X+c1*r0,c.Y,c.Z+s1*r0,c.X+c1*r1,c.Y,c.Z+s1*r1,c.X+c0*r1,c.Y,c.Z+s0*r1,r,g,b,a,0,true);}
        }
        // A ring standing up from the floor (the lifted tiles' lip), its top edge brightest.
        private void RingWall(Vector3 c,float radius,float h,float r,float g,float b)
        {
            if(h<=.01f)return;int n=Math.Max(24,Math.Min(72,(int)(radius*7)));
            for(int i=0;i<n;i++){double a0=i*Math.PI*2/n,a1=(i+1.02)*Math.PI*2/n;float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1);float tilt=.25f;
                Add(c.X+c0*radius,0,c.Z+s0*radius,c.X+c1*radius,0,c.Z+s1*radius,c.X+c1*(radius-tilt),h,c.Z+s1*(radius-tilt),c.X+c0*(radius-tilt),h,c.Z+s0*(radius-tilt),r*.5f,g*.5f,b*.5f,.7f,0,true);
                Add(c.X+c0*(radius-tilt),h,c.Z+s0*(radius-tilt),c.X+c1*(radius-tilt),h,c.Z+s1*(radius-tilt),c.X+c1*(radius-tilt-.06f),h+.06f,c.Z+s1*(radius-tilt-.06f),c.X+c0*(radius-tilt-.06f),h+.06f,c.Z+s0*(radius-tilt-.06f),r,g,b,.95f,0,true);}
        }
        // A ring facing along `normal` (the horns' sound rings).
        private void Ring(Vector3 c,Vector3 normal,float radius,float width,float r,float g,float b,float a)
        {
            var u=Vector3.Normalize(Vector3.Cross(normal,Math.Abs(normal.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var v=Vector3.Cross(normal,u);int n=16;
            for(int i=0;i<n;i++){float a0=i*BossMath.Tau/n,a1=(i+1.05f)*BossMath.Tau/n;var d0=u*(float)Math.Cos(a0)+v*(float)Math.Sin(a0);var d1=u*(float)Math.Cos(a1)+v*(float)Math.Sin(a1);
                var p0=c+d0*(radius-width);var p1=c+d1*(radius-width);var p2=c+d1*radius;var p3=c+d0*radius;Add(p0.X,p0.Y,p0.Z,p1.X,p1.Y,p1.Z,p2.X,p2.Y,p2.Z,p3.X,p3.Y,p3.Z,r,g,b,a,0,true);}
        }
        // A jagged, branching electric arc from a to b (re-struck by its seed): midpoint-displaced joints (still at its ends), a
        // white-blue core and a blue halo along every segment, a short side branch off some joints, glows where it lands.
        private void Bolt(Vector3 a,Vector3 b,int seed,float k,float w)
        {
            if(k<=.02f)return;float len=(b-a).Length();int n=Math.Max(4,Math.Min(14,(int)(len/.35f)));var prev=a;var halo=new Vector3(R(LiveHex),G(LiveHex),B(LiveHex));
            for(int i=1;i<=n;i++)
            {
                float u=(float)i/n,env=(float)Math.Sin(Math.PI*u);
                var p=i==n?b:Vector3.Lerp(a,b,u)+new Vector3((float)H(seed,i*3)-.5f,(float)H(seed,i*3+1)-.5f,(float)H(seed,i*3+2)-.5f)*(len*.17f*(.35f+.65f*env));
                Segment(prev,p,w,k,halo);
                if(i<n&&H(seed,i*3+40)<.3f){var dir=Vector3.Normalize(new Vector3((float)H(seed,i*3+41)-.5f,(float)H(seed,i*3+42)-.6f,(float)H(seed,i*3+43)-.5f)+new Vector3(0,0,.001f));
                    var m=p+dir*(len*.09f);var e=m+Vector3.Normalize(dir+new Vector3((float)H(seed,i*3+44)-.5f,0,(float)H(seed,i*3+45)-.5f))*(len*.08f);Segment(p,m,w*.6f,k*.6f,halo);Segment(m,e,w*.45f,k*.4f,halo);}
                prev=p;
            }
            Vfx.Glow(b,halo*(.9f*k),w*16,NativeBossVfx.Core);Vfx.Glow(a,halo*(.5f*k),w*11,NativeBossVfx.Core);
        }
        private void Segment(Vector3 a,Vector3 b,float w,float k,Vector3 halo){var d=b-a;Vfx.Spark(b,d,ArcCore*k,w*1.3f,0,1,0);Vfx.Spark(b,d,halo*(.42f*k),w*5.5f,0,1,0);}
    }
}
