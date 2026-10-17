using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE 6:47's effects (doc section 8): camera-facing and floor quads into a <see cref="NativeFxQuads"/> (Runtime draws them as one
    /// solid and one effects batch through NativeSirenFx, as the station's own effects), each joint's light level (the windows, the
    /// headlamp, the Heart, the cab's glass, the markers, the eyes and the watch: the runtime's per-draw glow tint), and up to three
    /// practical lights (the headlamp's beam, the Heart, the doors' spill) for the station's light slots.
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
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,fx,fy,fz;private float quality;private bool low;
        private readonly double[] pc=new double[SortMax*12],pk=new double[SortMax];private readonly float[] pcol=new float[SortMax*4];private readonly int[] pidx=new int[SortMax];private readonly byte[] pmode=new byte[SortMax];private int pn;
        private static float R(int hex)=>(float)NativeSceneryBuilder.Channel(hex,16);private static float G(int hex)=>(float)NativeSceneryBuilder.Channel(hex,8);private static float B(int hex)=>(float)NativeSceneryBuilder.Channel(hex,0);
        private static double H(int a,int b){unchecked{uint x=(uint)a*0x9E3779B9U^(uint)b*0x85EBCA6BU;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x/4294967296.0;}}
        private static double Frac(double x)=>x-Math.Floor(x);

        public void Build(StationBossRig rig,in StationBossView v,float t,double eyeX,double eyeY,double eyeZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,float effects,bool lowTier)
        {
            Quads.Clear();pn=0;LightCount=0;Telegraph=Beam=Sparks=Steam=Dust=Arcs=Glints=0;
            for(int i=0;i<Glow.Length;i++){Glow[i]=1;Hidden[i]=false;}
            if(rig==null||!rig.Visible||!v.live)return;
            ex=eyeX;ey=eyeY;ez=eyeZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;fx=ry*uz-rz*uy;fy=rz*ux-rx*uz;fz=rx*uy-ry*ux;quality=effects<0?0:effects>1?1:effects;low=lowTier;
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
            Flush();
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

        // ---------------- the headlamp: beam, glare, its light ----------------
        private void Headlamp(StationBossRig rig,in StationBossView v,float t,bool tunnel)
        {
            float k=Glow[NativeStationBossModel.Lamp];if(k<=.02f)return;var p=rig.LampPos;var d=rig.LampDir;
            float arrive=rig.Arriving?1.4f:1;
            // The glare (two layers) and the beam: soft widening strips fading down its length.
            Sprite(p.X,p.Y,p.Z,.5f*k*arrive,1.3f*k,1.35f*k,1.45f*k,.8f);Sprite(p.X,p.Y,p.Z,(tunnel?4.2f:1.6f)*k,.32f*k,.34f*k,.4f*k,.55f);Beam++;
            float len=tunnel?32:18,width=1.3f;int n=Math.Max(5,(int)Math.Round(9*(.4f+.6f*quality)));
            for(int i=0;i<n;i++)
            {
                float a0=len*i/n,a1=len*(i+1.6f)/n,hw=width*(.22f+.95f*i/n),f=(float)Math.Pow(1-(float)i/n,1.5f)*k*.42f;
                Strip(p+d*a0,p+d*a1,hw,.82f*f,.86f*f,.95f*f,.5f,1,true);Beam++;
            }
            // Locked on Departure's lane: a hard white line down the beam.
            if(rig.LampLocked)Strip(p,p+d*len*.5f,.07f,1.6f*k,1.6f*k,1.7f*k,.9f,0,true);
            float yaw=(float)Math.Atan2(d.X,d.Z);if(yaw<0)yaw+=BossMath.Tau;AddLight(p+d*2.4f,tunnel?30:17,.95f*k*1.6f,.97f*k*1.6f,1.0f*k*1.6f,1+yaw);
        }
        // ---------------- the Heart through the torn rear (and the open roof) ----------------
        private void Heart(StationBossRig rig,in StationBossView v,float t)
        {
            float k=Glow[NativeStationBossModel.Heart];if(k<=.02f)return;var h=rig.Shown.Position[NativeStationBossModel.Heart];var back=BossMath.Rotate(rig.Shown.Rotation[NativeStationBossModel.Body],new Vector3(0,0,-.55f));
            Sprite(h.X+back.X,h.Y+back.Y,h.Z+back.Z,1.1f+.4f*k,1.0f*k*.45f,.62f*k*.45f,.22f*k*.45f,.6f);Glints++;
            if(rig.RoofOpen>.3f){var top=h+BossMath.Rotate(rig.Shown.Rotation[NativeStationBossModel.Body],new Vector3(0,1.4f,1.2f));Sprite(top.X,top.Y,top.Z,2.2f*rig.RoofOpen,.5f*k*rig.RoofOpen,.3f*k*rig.RoofOpen,.1f*k*rig.RoofOpen,.5f);}
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
                for(int r=0;r<3;r++){float ph=(float)Frac(t*1.6f+r/3.0);var c=mouth+dir*(ph*1.6f);Ring(c,dir,.25f+ph*1.1f,.04f,R(WarnHex)*k*(1-ph),G(WarnHex)*k*(1-ph),B(WarnHex)*k*(1-ph),.8f);Telegraph++;}
                Sprite(mouth.X,mouth.Y,mouth.Z,.45f,R(WarnHex)*k,G(WarnHex)*k*.8f,B(WarnHex)*k*.5f,.7f);
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
            if(life>.01f)DrawLane(p0,p1,m.Radius,a.Age>=lockAt,a.Age>=a.HitAt,a.Age-a.HitAt,note,life,a.Age);
            if(!rig.DoubleCharge)return;
            // TERMINUS's second charge: the lane swings round with the turn (from the boss along its facing), two chime notes, locks
            // as the turn ends and burns red through the second charge.
            var dd=StationBossMoves.DepartureDouble;float after=dd.Seconds-dd.HitAt,k2=after>0?Math.Max(.3f,(a.Duration-a.HitAt)/after):1;
            float turnAt=a.HitAt+m.Strike*k2,second=a.HitAt+StationBossMoves.SecondCharge*k2,age=a.Age;
            float life2=age<turnAt?0:age<second?BossMath.Smoother((age-turnAt)/.2f):1-BossMath.Smoother((age-second)/(m.Strike*.9f*k2));if(life2<=.01f)return;
            var fw=BossMath.Forward(rig.Yaw);var q0=rig.Root+fw*4.5f;var q1=q0+fw*m.Reach;
            float note2=0;for(int n=0;n<2;n++){float k=age-(turnAt+(.1f+.25f*n)*k2);if(k>=0&&k<.3f)note2=Math.Max(note2,1-k/.3f);}
            DrawLane(q0,q1,m.Radius,age>=second-.15f*k2,age>=second,age-second,note2,life2,age);
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
                int hex=strike?DangerHex:WarnHex;float pulse=strike?1.4f:.75f+.25f*(float)Math.Sin(a.Age*BossMath.Tau*3);var c=door+right*(side*(m.Reach/2+.1f));
                FlatOutline(c,.03f,right,m.Reach/2,fwd,m.Radius*.81f,.06f,R(hex)*pulse*tele*1.5f,G(hex)*pulse*tele*1.5f,B(hex)*pulse*tele*1.5f);Telegraph+=4;
                FlatRect(c,.028f,right,m.Reach/2,fwd,m.Radius*.81f,R(hex)*pulse*tele*.14f,G(hex)*pulse*tele*.14f,B(hex)*pulse*tele*.14f,.45f);Telegraph++;
            }
            // Light blades through the cracked doors, the full spill once they slam open.
            float open=BossMath.Clamp01(rig.Channels[side<0?StationBossCh.DoorL:StationBossCh.DoorR]);if(open>.02f)
            {
                float k=open<.2f?.6f:1.2f;var c=door+right*(side*(.6f+open*1.2f));FlatRect(c,.02f,right,.6f+open*1.2f,fwd,.3f+open*.5f,R(0xFFB45A)*k*.35f,G(0xFFB45A)*k*.35f,B(0xFFB45A)*k*.35f,.6f,true,1);
                AddLight(door+right*(side*1.2f)+Vector3.UnitY*1.5f,6,1.0f*k*.7f,.7f*k*.7f,.35f*k*.7f,0);
            }
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
                    RingFlat(new Vector3(x,.035f,z),m.Radius,.1f,R(WarnHex)*pulse*1.4f,G(WarnHex)*pulse*1.4f,B(WarnHex)*pulse*1.4f,.85f);Telegraph+=12;
                    if(DrawGlass){Flat(x,.03f,z,.4f+u*1.2f,.4f+u*1.2f,0,0,0,0,.25f+.5f*u,false,1);float fy=NativeExpeditionStationKit.VaultY(z)-.5f-u*u*12;Sprite(x,Math.Max(.6f,fy),z,.25f+.3f*u,1.4f,1.6f,1.8f,.8f);Glints++;}
                }
                else if(DrawGlass&&age>=land&&age<land+2.2f)
                {
                    float k=age-land,u=Math.Min(1,k/.6f);
                    for(int s=0;s<12;s++){double ang=H(s,i)*Math.PI*2,dd=(.4+H(s,i+3)*2.4)*(1-(1-u)*(1-u));float px=x+(float)(Math.Cos(ang)*dd),pz=z+(float)(Math.Sin(ang)*dd),yy=.04f+Math.Max(0,(.6f-k)*1.2f*(float)Math.Sin(Math.PI*u));
                        Flat(px,yy,pz,.08f+(float)H(s,5)*.14f,.05f+(float)H(s,6)*.1f,ang+t*(1-u)*6,.5f,.6f,.7f,.85f,false,0);Glints++;}
                    if(k<.3f){Sprite(x,.4f,z,1.2f*(1-k/.3f)+.2f,.8f,.9f,1f,.6f*(1-k/.3f));}
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
                // Two or three arcs from the head's horns up to the vault's iron, re-struck on a 12 Hz jitter.
                int n=live?3:2;
                for(int k=0;k<n;k++)
                {
                    var from=head+BossMath.Rotate(q,new Vector3((k-1)*.6f,.12f,0));uint seed=(uint)(Math.Floor(t*12)+k*31);
                    var to=new Vector3(from.X+(float)(H((int)seed,1)-.5)*7,0,from.Z+(float)(H((int)seed,2)-.5)*6);to.Y=NativeExpeditionStationKit.VaultY(to.Z)-.6f;
                    Bolt(from,to,(int)seed,live?1.6f:.9f,.06f);Arcs++;
                }
                Sprite(head.X,head.Y+.1f,head.Z,1.2f,R(LiveHex)*1.2f,G(LiveHex)*1.2f,B(LiveHex)*1.2f,.7f);
            }
            for(int i=0;i<NativeStationBossArena.PuddleCount;i++)
            {
                float x=NativeStationBossArena.Puddles[i*3],z=NativeStationBossArena.Puddles[i*3+1],r=NativeStationBossArena.Puddles[i*3+2];
                if(live)
                {
                    float crackle=.75f+.25f*(float)Math.Sin(t*41+i*7)*(float)Math.Sin(t*13+i);
                    Flat(x,.03f,z,r,r,0,R(LiveHex)*.5f*crackle,G(LiveHex)*.5f*crackle,B(LiveHex)*.5f*crackle,.7f,true,1);RingFlat(new Vector3(x,.035f,z),r,.08f,R(LiveHex)*1.6f,G(LiveHex)*1.6f,B(LiveHex)*1.6f,.85f);Telegraph+=13;
                    // Little arcs dancing on the water.
                    int sparks=Budget(3);for(int k=0;k<sparks;k++){uint seed=(uint)(Math.Floor(t*15)+i*97+k*13);float ang=(float)(H((int)seed,4)*Math.PI*2),rr=(float)H((int)seed,5)*r;var a0=new Vector3(x+(float)Math.Cos(ang)*rr,.04f,z+(float)Math.Sin(ang)*rr);Bolt(a0,a0+new Vector3((float)(H((int)seed,6)-.5),.5f+(float)H((int)seed,7)*.6f,(float)(H((int)seed,8)-.5)),(int)seed,1,.025f);Arcs++;}
                }
                else if(tele>0)
                {
                    float pulse=(.6f+.4f*(float)Math.Sin(t*BossMath.Tau*3))*tele;RingFlat(new Vector3(x,.035f,z),r,.1f,R(WarnHex)*pulse*1.4f,G(WarnHex)*pulse*1.4f,B(WarnHex)*pulse*1.4f,.85f);
                    Flat(x,.03f,z,r,r,0,R(LiveHex)*.15f*pulse,G(LiveHex)*.15f*pulse,B(LiveHex)*.15f*pulse,.5f,true,1);Telegraph+=13;
                }
            }
            // Arcs from the kneeling hands to the nearest puddles while live.
            if(live&&moving)for(int leg=0;leg<2;leg++)
            {
                var h=rig.Hand(leg);int best=0;float bd=1e9f;for(int i=0;i<NativeStationBossArena.PuddleCount;i++){float dx=NativeStationBossArena.Puddles[i*3]-h.X,dz=NativeStationBossArena.Puddles[i*3+1]-h.Z,dd=dx*dx+dz*dz;if(dd<bd){bd=dd;best=i;}}
                if(bd>14*14)continue;Bolt(h,new Vector3(NativeStationBossArena.Puddles[best*3],.05f,NativeStationBossArena.Puddles[best*3+1]),(int)Math.Floor(t*12)+leg*7,1.2f,.05f);Arcs++;
            }
            if(live||raised)AddLight(head+Vector3.UnitY*.5f,9,R(LiveHex)*1.4f,G(LiveHex)*1.4f,B(LiveHex)*1.4f,0);
        }
        // ---------------- Terminus: the reach ring through the rear-up, the slam's ring of lifted tiles rolling out ----------------
        private void Terminus(StationBossRig rig,in StationBossView v,float t)
        {
            if(rig.ActMove<0||NativeStationBoss.Moves[rig.ActMove].Id!="terminus")return;var a=rig.Act;var m=NativeStationBoss.Moves[rig.ActMove];
            Vector3 c;if(StationBossView.At(v.laneX0))c=new Vector3(v.laneX0,0,v.laneZ0);else c=rig.Root+BossMath.Forward(rig.Yaw)*6.6f;
            if(a.Age<a.HitAt)
            {
                float tele=BossMath.Smoother(a.Age/.4f),pulse=.7f+.3f*(float)Math.Sin(a.Age*BossMath.Tau*2.86f);
                RingFlat(new Vector3(c.X,.035f,c.Z),m.Reach,.12f,R(WarnHex)*pulse*tele*1.5f,G(WarnHex)*pulse*tele*1.5f,B(WarnHex)*pulse*tele*1.5f,.85f);
                RingFlat(new Vector3(c.X,.035f,c.Z),m.Radius,.18f,R(DangerHex)*pulse*tele*1.2f,G(DangerHex)*pulse*tele*1.2f,B(DangerHex)*pulse*tele*1.2f,.85f);Telegraph+=24;
                return;
            }
            float k=a.Age-a.HitAt,run=m.Reach/m.Speed;
            if(k<run+.4f)
            {
                // The lip: a ring of lifted floor tiles rolling out at 12 m/s to 10 m (0.6 m tall), red-hot at the front.
                float r=Math.Min(m.Reach,.5f+m.Speed*k),fade=k<run?1:1-(k-run)/.4f;
                RingWall(new Vector3(c.X,0,c.Z),r,.6f*fade,R(DangerHex)*1.3f*fade,G(DangerHex)*1.3f*fade,B(DangerHex)*1.3f*fade);
                RingFlat(new Vector3(c.X,.04f,c.Z),r,.35f,R(WarnHex)*fade,G(WarnHex)*fade,B(WarnHex)*fade,.8f);Telegraph+=24;
                // Dust thrown up behind the lip.
                int puffs=Budget(10);for(int i=0;i<puffs;i++){float ang=(float)(i*BossMath.Tau/puffs+H(i,9)),rr=Math.Max(.3f,r-.6f);Sprite(c.X+(float)Math.Cos(ang)*rr,.5f+.3f*(float)H(i,4),c.Z+(float)Math.Sin(ang)*rr,.7f+.4f*k,R(DustHex)*.6f,G(DustHex)*.6f,B(DustHex)*.6f,.35f*fade,false);Dust++;}
            }
            if(k<1.6f){int n=Budget(16);for(int i=0;i<n;i++){float ang=(float)(H(i,11)*BossMath.Tau),rr=1.5f+(float)H(i,12)*2.5f*Math.Min(1,k*3);Sprite(c.X+(float)Math.Cos(ang)*rr,.3f+k*1.5f*(float)H(i,13),c.Z+(float)Math.Sin(ang)*rr,1+k,R(DustHex)*.5f,G(DustHex)*.5f,B(DustHex)*.5f,.4f*(1-k/1.6f),false);Dust++;}}
        }
        // ---------------- brakes: sparks off the skidding hands and the wheels, scorch along the skid ----------------
        private void Brakes(StationBossRig rig,in StationBossView v,float t)
        {
            float skid=0;
            if(rig.ActMove>=0&&NativeStationBoss.Moves[rig.ActMove].Id=="departure"){var a=rig.Act;var m=NativeStationBoss.Moves[rig.ActMove];float k=a.Age-a.HitAt-m.Strike;if(k>-.15f&&k<m.Follow)skid=1-BossMath.Clamp01(k/m.Follow);
                // The crouch digs the hands in: sparks at the front hands through the chime.
                if(a.Age>.3f&&a.Age<a.HitAt)for(int leg=0;leg<2;leg++)SparkBurst(rig.Hand(leg),new Vector3(0,1,0),(int)(t*20)+leg*5,4,.35f,.5f);}
            if(skid>0)for(int leg=0;leg<2;leg++)
            {
                var h=rig.Hand(leg);var back=-BossMath.Forward(rig.Yaw);SparkBurst(h,back+Vector3.UnitY*.6f,(int)(t*30)+leg*9,Budget(10),.7f*skid,1.2f);
                Flat(h.X,.02f,h.Z,.35f,1.6f,rig.Yaw,0,0,0,.35f*skid,false,1);
            }
        }
        // ---------------- the arrival: brake sparks off the wheels on the accents, steam, the burst's debris ----------------
        private void Arrival(StationBossRig rig,in StationBossView v,float t)
        {
            if(!rig.Arriving)return;var b=NativeStationBossBeats.Current;float a=rig.ArrivalAge;var body=rig.Shown.Position[NativeStationBossModel.Body];var q=rig.Shown.Rotation[NativeStationBossModel.Body];
            for(int i=0;i<2;i++)
            {
                float k=a-b.Accent(i);if(k<-.1f||k>.9f)continue;float s=k<0?0:1-k/.9f;
                for(int w=0;w<6;w++){var wp=rig.Shown.Position[NativeStationBossModel.Wheel(w)]+Vector3.UnitY*-.45f;SparkBurst(wp,BossMath.Rotate(q,new Vector3(NativeStationBossModel.Side(w)*.4f,.8f,-1)),(int)(t*24)+w*7,Budget(6),s*1.2f,1.4f);}
                Puff(body+BossMath.Rotate(q,new Vector3(0,-.5f,-1)),2.2f,s*.35f,SteamHex);
            }
            // The stop's last screech and the brakes' steam.
            {float k=a-b.Accent1;if(k>0&&k<2){Puff(body+BossMath.Rotate(q,new Vector3(-1.4f,-.3f,1.2f)),1.6f+k,.3f*(1-k/2),SteamHex);Puff(body+BossMath.Rotate(q,new Vector3(1.4f,-.3f,-1.2f)),1.6f+k,.3f*(1-k/2),SteamHex);}}
            // The burst through the gate: sparks and dust at the barrier line, gate posts flying (as glints).
            {float k=a-b.Drop;if(k>-.1f&&k<1.6f){var gate=new Vector3(rig.Root.X,0,NativeStationBossArena.BarrierZ);float s=k<0?0:1-k/1.6f;
                for(int sd=-1;sd<=1;sd+=2)SparkBurst(gate+new Vector3(sd*3,.8f,0),new Vector3(sd*.5f,1,.6f),(int)(t*20)+sd,Budget(12),s*1.4f,1.8f);
                for(int i=0;i<Budget(10);i++){float ang=(float)H(i,21)*BossMath.Pi,rr=1+k*4;Puff(gate+new Vector3((float)Math.Cos(ang)*rr*1.6f,.4f+k,(float)Math.Sin(ang)*rr*.6f),1.4f+k,.3f*s,DustHex);}}}
        }
        // ---------------- the phase changes: the barrier's smash, the board's crash, the roof's tear ----------------
        private void Changes(StationBossRig rig,in StationBossView v,float t)
        {
            if(v.BarrierDown(t)){float k=t-v.barrierDownAt;if(k<2.2f){float s=1-k/2.2f;var c=new Vector3(rig.Root.X,0,NativeStationBossArena.BarrierZ);
                for(int sd=-1;sd<=1;sd+=2){SparkBurst(c+new Vector3(sd*6,.7f,0),new Vector3(sd*.4f,1,-.6f),(int)(t*18)+sd,Budget(14),s*1.5f,2.2f);for(int i=0;i<Budget(6);i++)Puff(c+new Vector3(sd*(3+i*2.2f),.5f+k*.6f,(float)H(i,sd+4)-.5f),1.4f+k*1.2f,.32f*s,DustHex);}}}
            if(v.BoardDown(t)){float k=t-v.boardDownAt-NativeStationBossStage.BoardFallSeconds*.62f;if(k>-.05f&&k<3f){float s=k<0?0:1-k/3f;var c=new Vector3(NativeStationBossArena.BoardX,1.2f,NativeStationBossArena.BoardZ);
                SparkBurst(c+new Vector3(0,2.2f,-1.5f),new Vector3(0,1,-.5f),(int)(t*20),Budget(20),s*1.6f,3f);for(int i=0;i<Budget(12);i++)Puff(c+new Vector3(((float)H(i,30)-.5f)*10,.6f+k*.8f,((float)H(i,31)-.5f)*4),1.8f+k*1.5f,.35f*s,DustHex);}}
            if(v.RoofOpen(t)){float k=t-v.roofOpenAt;if(k<1.4f){float s=1-k/1.4f;var q=rig.Shown.Rotation[NativeStationBossModel.Body];var body=rig.Shown.Position[NativeStationBossModel.Body];
                for(int i=0;i<4;i++){var p=body+BossMath.Rotate(q,new Vector3(i%2==0?-1.38f:1.38f,2.7f,1.45f-i*1.2f));SparkBurst(p,BossMath.Rotate(q,new Vector3(i%2==0?-1:1,1.2f,0)),(int)(t*20)+i*5,Budget(8),s*1.4f,1.4f);}}}
        }
        // ---------------- the breaks: the headlamp's shards, the pantograph's snap, the express's storm of sparks ----------------
        private void Breaks(StationBossRig rig,in StationBossView v,float t)
        {
            if(v.LampBroken(t)){float k=t-v.lampBrokenAt;if(k<1.2f){var p=rig.LampPos;var d=rig.LampDir;int n=Budget(14);
                for(int i=0;i<n;i++){var dir=Vector3.Normalize(d+new Vector3((float)H(i,40)-.5f,(float)H(i,41)*.8f,(float)H(i,42)-.5f));var at=p+dir*(k*5)+Vector3.UnitY*(-4.9f*k*k);if(at.Y<.05f)at.Y=.05f;
                    Sprite(at.X,at.Y,at.Z,.06f+.05f*(float)H(i,43),1.6f*(1-k/1.2f),1.7f*(1-k/1.2f),1.9f*(1-k/1.2f),.9f);Glints++;}
                SparkBurst(p,d,(int)(t*30),Budget(8),Math.Max(0,1-k/.5f),1.0f);}}
            if(v.PantoBroken(t)){float k=t-v.pantoBrokenAt;if(k<1.6f){var p=rig.Shown.Position[NativeStationBossModel.PantoLow];SparkBurst(p,Vector3.UnitY,(int)(t*30),Budget(14),1-k/1.6f,2.0f);
                if(k<.5f)Bolt(p,p+new Vector3(((float)H((int)(t*12),1)-.5f)*3,2.5f,((float)H((int)(t*12),2)-.5f)*3),(int)(t*12),1.4f,.05f);}}
            if(StationBossView.At(v.expressHitAt)&&t>=v.expressHitAt){float k=t-v.expressHitAt;if(k<2.2f){float s=1-k/2.2f;var body=rig.Shown.Position[NativeStationBossModel.Body];var right=BossMath.Right(rig.Yaw);
                for(int i=0;i<3;i++)SparkBurst(body+right*1.6f+BossMath.Forward(rig.Yaw)*((i-1)*3)+Vector3.UnitY*(-.5f+i*.4f),right*-1+Vector3.UnitY*.6f+new Vector3(1,0,0),(int)(t*30)+i*11,Budget(18),s*1.8f,3.0f);
                if(k<.25f)Sprite(body.X+right.X*2,body.Y,body.Z+right.Z*2,5*(1-k/.25f),2.4f,2.2f,1.8f,.8f*(1-k/.25f));}}
        }
        // ---------------- staggers: steam from the underframe, the Heart's flare (its glow) ----------------
        private void Staggers(StationBossRig rig,in StationBossView v,float t)
        {
            float s=Stagger(v,t);if(s<=.02f)return;var q=rig.Shown.Rotation[NativeStationBossModel.Body];var body=rig.Shown.Position[NativeStationBossModel.Body];
            for(int i=0;i<Budget(6);i++)Puff(body+BossMath.Rotate(q,new Vector3(((float)H(i,50)-.5f)*2.4f,-.6f+(float)H(i,51)*.4f,((float)H(i,52)-.5f)*7)),1.2f+(1-s)*1.8f,.35f*s,SteamHex);
            if(v.staggerCause=="crash"){var nose=body+BossMath.Rotate(q,new Vector3(0,0,5.4f));SparkBurst(nose,BossMath.Rotate(q,new Vector3(0,1,-1)),(int)(t*24),Budget(10),s,1.6f);}
        }
        // ---------------- the death: steam venting, the windows dying (their glow), the last breath of steam ----------------
        private void Death(StationBossRig rig,in StationBossView v,float t)
        {
            if(!v.Dead(t))return;float d=t-v.diedAt;var q=rig.Shown.Rotation[NativeStationBossModel.Body];var body=rig.Shown.Position[NativeStationBossModel.Body];
            if(d<3.8f)for(int i=0;i<Budget(8);i++){float k=(float)Frac(d*.6+H(i,60));Puff(body+BossMath.Rotate(q,new Vector3(((float)H(i,61)-.5f)*2.6f,-.5f+k*2.2f,((float)H(i,62)-.5f)*8)),1.0f+k*2,.3f*(1-k)*(1-d/3.8f),SteamHex);}
            if(d>.9f&&d<1.6f){float s=1-(d-.9f)/.7f;for(int i=0;i<6;i++)Puff(rig.Shown.Position[NativeStationBossModel.Wheel(i)],1.5f,.3f*s,DustHex);}
        }
        // ---------------- footfalls: a puff of dust (and a spark off the iron wrist) where a hand slaps down ----------------
        private void Steps(StationBossRig rig,float t)
        {
            if(rig.Landed==0)return;for(int i=0;i<6;i++){if((rig.Landed&(1<<i))==0)continue;var h=rig.Hand(i);for(int k=0;k<Budget(3);k++)Puff(h+new Vector3(((float)H(i,k)-.5f)*.8f,.2f,((float)H(i,k+5)-.5f)*.8f),.7f,.25f,DustHex);}
        }
        // ---------------- the torn cables spark at their ends now and then ----------------
        private void Cables(StationBossRig rig,float t)
        {
            for(int c=0;c<2;c++){int last=(c==0?NativeStationBossModel.CableAFirst:NativeStationBossModel.CableBFirst)+NativeStationBossModel.CableLinks-1;double cyc=t*.7+c*.43;float k=(float)Frac(cyc);if(k>.18f)continue;
                var tip=rig.Shown.Position[last]+BossMath.Rotate(rig.Shown.Rotation[last],new Vector3(0,-.7f,0));SparkBurst(tip,Vector3.UnitY*-.2f+new Vector3(.2f,0,0),(int)Math.Floor(cyc)*13+c,Budget(5),1-k/.18f,.5f);
                Sprite(tip.X,tip.Y,tip.Z,.35f,R(LiveHex)*(1-k/.18f),G(LiveHex)*(1-k/.18f),B(LiveHex)*(1-k/.18f),.8f);}
        }
        // ---------------- Next Stop: amber light floods out of every door ----------------
        private void Spill(StationBossRig rig,in StationBossView v,float t)
        {
            if(rig.ActMove<0||NativeStationBoss.Moves[rig.ActMove].Id!="passengers")return;float open=Math.Max(rig.Channels[StationBossCh.DoorL],rig.Channels[StationBossCh.DoorR]);if(open<=.02f)return;
            var right=BossMath.Right(rig.Yaw);var fwd=BossMath.Forward(rig.Yaw);
            for(int sd=-1;sd<=1;sd+=2){var c=rig.Root+right*(sd*(NativeStationBossArena.DoorX+1.4f))+fwd*NativeStationBossArena.DoorZ;FlatRect(c,.02f,right,1.4f,fwd,.9f,R(0xFFB45A)*.5f*open,G(0xFFB45A)*.5f*open,B(0xFFB45A)*.5f*open,.6f,true,1);
                Sprite(c.X,1.4f,c.Z,1.4f,R(0xFFB45A)*.4f*open,G(0xFFB45A)*.4f*open,B(0xFFB45A)*.4f*open,.5f);}
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
        // A jagged electric bolt from a to b: a bright core and a blue halo, re-struck by its seed.
        private void Bolt(Vector3 a,Vector3 b,int seed,float k,float w)
        {
            int n=Math.Max(3,Math.Min(10,(int)((b-a).Length()/.5f)));var prev=a;
            for(int i=1;i<=n;i++)
            {
                float u=(float)i/n;var p=i==n?b:Vector3.Lerp(a,b,u)+new Vector3((float)H(seed,i*3)-.5f,(float)H(seed,i*3+1)-.5f,(float)H(seed,i*3+2)-.5f)*((b-a).Length()*.12f);
                Strip(prev,p,w*3,R(LiveHex)*.5f*k,G(LiveHex)*.5f*k,B(LiveHex)*.5f*k,.5f,1,true);Strip(prev,p,w,1.4f*k,1.6f*k,1.8f*k,.95f,0,true);prev=p;
            }
            Sprite(b.X,b.Y,b.Z,.35f*k,R(LiveHex)*k,G(LiveHex)*k,B(LiveHex)*k,.7f);
        }
        private void SparkBurst(Vector3 c,Vector3 dir,int seed,int n,float k,float reach)
        {
            if(k<=.01f||n<=0)return;float dl=dir.Length();var d=dl>1e-5f?dir/dl:Vector3.UnitY;
            for(int i=0;i<n;i++)
            {
                var j=Vector3.Normalize(d+new Vector3((float)H(seed,i*4)-.5f,(float)H(seed,i*4+1)*.7f,(float)H(seed,i*4+2)-.5f)*1.4f);float dist=(float)H(seed,i*4+3)*reach;var p0=c+j*dist;
                Strip(p0,p0+j*(.18f+.3f*(float)H(seed,i+90))*k,.016f,R(SparkHex)*2.2f*k,G(SparkHex)*2.2f*k,B(SparkHex)*2.2f*k,.95f,0,true);Sparks++;
            }
            Sprite(c.X,c.Y,c.Z,.5f*k,R(SparkHex)*k,G(SparkHex)*k*.8f,B(SparkHex)*k*.5f,.6f);
        }
        private void Puff(Vector3 c,float size,float a,int hex)
        {
            if(a<=.01f)return;Sprite(c.X,c.Y,c.Z,size,R(hex)*.55f,G(hex)*.55f,B(hex)*.55f,a,false);if(hex==SteamHex)Steam++;else Dust++;
        }
    }
}
