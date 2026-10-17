using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A light the station's picker can hand to NativeBlockLit's practical slots: place, range, linear colour times
    /// intensity, its kind (NativeExpeditionStationKit Glow*, <see cref="NativeExpeditionStationLights.KindLook"/> a look
    /// practical, or one of the moving lights), its seed and subject, and the look light it came from.</summary>
    public struct NativeStationLight {public float X,Y,Z,Range,R,G,B,Param;public byte Kind;public short Tag,Look;}

    /// <summary>
    /// The station's practical lights (docs/EXPEDITION-CENTRAL-STATION.md sections 3 and 11). NativeBlockLit lights blocks and
    /// kit from eight point lights; the station has about sixty sources: the look's own eight and the kit's (sodium pendants,
    /// troffers, balcony and bridge globes, the board, CENTRAL, the cafe fire, the booth, the train's lit windows, the signals,
    /// the beacons, the box's red, the corridor's strips, the generator, the cabinets, the flood) and three that move with the
    /// express: its headlight (a forward beam: in the west mouth through the warning, at the nose through the pass), the
    /// window band's strobe beside the train, and nothing at rest. Each frame the eight that matter most to the camera take
    /// the slots (their strength times the fall-off at the camera, plus a share for strong lights further off), easing in
    /// and out so a change never pops, as the town's picker does; the low tier keeps <see cref="NativeExpeditionStationKit.LowSlots"/>.
    /// Colours follow the state: the signals red, green or dark, the beacons sweeping only through the warning and the pass,
    /// the flood blue only while it conducts, the cabinets flaring with their fizz and arc, the lamps' flicker. Kit sources
    /// within 1.5 m of a look light give way to it. Allocation free after Build.
    /// </summary>
    public sealed class NativeExpeditionStationLights
    {
        public const int Slots=NativeMapLooks.MaxLights;public const byte KindLook=255,KindHeadlight=254,KindBand=253;
        public NativeStationLight[] Candidates=Array.Empty<NativeStationLight>();
        public readonly int[] Slot=new int[Slots];public readonly float[] Weight=new float[Slots];public readonly float[] Pos=new float[Slots*4],Rgb=new float[Slots*3],Beam=new float[Slots];
        private MapLookPractical[] looks=Array.Empty<MapLookPractical>();private float[] score=Array.Empty<float>();private readonly int[] want=new int[Slots];private int headlight=-1,band=-1;

        public NativeExpeditionStationLights(){for(int i=0;i<Slots;i++)Slot[i]=-1;}
        public static NativeExpeditionStationLights Build(NativeExpeditionStationKit kit,MapLook look)
        {
            var l=new NativeExpeditionStationLights();var list=new List<NativeStationLight>();l.looks=look?.lights??Array.Empty<MapLookPractical>();
            for(short i=0;i<l.looks.Length;i++)
            {
                var p=l.looks[i];if(p?.pos==null)continue;
                list.Add(new NativeStationLight{X=p.pos.x,Y=p.pos.y,Z=p.pos.z,Range=p.range,R=(float)NativeSceneryBuilder.Channel(p.color,16)*p.intensity,G=(float)NativeSceneryBuilder.Channel(p.color,8)*p.intensity,B=(float)NativeSceneryBuilder.Channel(p.color,0)*p.intensity,Kind=KindLook,Look=i,Tag=-1});
            }
            int fixedCount=list.Count;
            foreach(var g in kit?.Glows??Array.Empty<NativeStationGlow>())
            {
                if(g.Range<3.5f)continue;bool near=false;
                for(int k=0;k<fixedCount;k++){var c=list[k];double dx=c.X-g.X,dy=c.Y-g.Y,dz=c.Z-g.Z;if(dx*dx+dy*dy+dz*dz<2.25){near=true;break;}}if(near)continue;
                list.Add(new NativeStationLight{X=g.X,Y=g.Y,Z=g.Z,Range=g.Range,R=g.R,G=g.G,B=g.B,Param=g.Param,Kind=g.Kind,Tag=g.Tag,Look=-1});
            }
            if(kit!=null&&kit.Parts.Length>0)
            {
                l.headlight=list.Count;list.Add(new NativeStationLight{Kind=KindHeadlight,Range=24,Look=-1,Tag=-1,Y=-10000});
                l.band=list.Count;list.Add(new NativeStationLight{Kind=KindBand,Range=13,Look=-1,Tag=-1,Y=-10000});
            }
            l.Candidates=list.ToArray();l.score=new float[l.Candidates.Length];return l;
        }
        /// <summary>A candidate's place, linear colour times intensity and beam this frame (pure; a dark one has no colour).</summary>
        public void Shade(ref NativeStationLight c,NativeStationState s,double t,out float r,out float g,out float b,out float beam)
        {
            r=c.R;g=c.G;b=c.B;beam=0;float k=1;
            switch(c.Kind)
            {
                case KindLook:k=c.Look>=0&&c.Look<looks.Length?(float)NativeMapLooks.Flicker(looks[c.Look],t):1;
                    // The look's own signal-box red and tunnel green follow the state too.
                    if(c.Look>=0&&c.Look<looks.Length){var id=looks[c.Look].id;if(id=="tunnel-w-green"&&(s==null||s.Signal!=NativeStationState.SignalGreen))k=0;}
                    break;
                case NativeExpeditionStationKit.GlowLampFlicker:case NativeExpeditionStationKit.GlowWindow:k=NativeExpeditionStationKit.FlickerLevel(c.Param,t);break;
                case NativeExpeditionStationKit.GlowTubeFlicker:k=NativeExpeditionStationKit.Stutter(t+c.Param*5.3,9+c.Param*7,.35,.85);break;
                case NativeExpeditionStationKit.GlowBoard:k=NativeExpeditionStationKit.Stutter(t,7,.18,.5);break;
                case NativeExpeditionStationKit.GlowFire:k=NativeExpeditionStationKit.FireLevel(c.Param,t);break;
                case NativeExpeditionStationKit.GlowBox:k=(float)(.85+.15*Math.Sin(t*1.7+c.Param));break;
                case NativeExpeditionStationKit.GlowStrip:k=NativeExpeditionStationKit.Stutter(t+c.Param*3.1,5,.15,.6);break;
                case NativeExpeditionStationKit.GlowGenerator:k=NativeExpeditionStationKit.FlickerLevel(c.Param,t);break;
                case NativeExpeditionStationKit.GlowSignal:
                {
                    // Tag: line * 2 + side. Only the express line turns green; the levers put every signal out.
                    byte a=s?.Signal??NativeStationState.SignalRed;int line=c.Tag/2;
                    if(a==NativeStationState.SignalDark){k=0;break;}
                    if(line==0&&a==NativeStationState.SignalGreen){float m=Math.Max(r,Math.Max(g,b));r=m*.15f;g=m;b=m*.3f;}
                    break;
                }
                case NativeExpeditionStationKit.GlowBeacon:
                {
                    float d=s?.Danger??0;if(d<=0){k=0;break;}
                    // A rotating lamp: one beam turning once every .75 s (NativeWardLight.cginc WardBeam: 1 + yaw).
                    double yaw=(t*1.33+c.Param)%1*Math.PI*2;beam=1+(float)yaw;k=1.4f;break;
                }
                case NativeExpeditionStationKit.GlowWater:
                {
                    float f=s?.FloodGlow??0;if(f>0&&c.Tag<0){r=.05f*f*1.6f;g=.35f*f*1.6f;b=.95f*f*1.6f;}break;
                }
                case NativeExpeditionStationKit.GlowCabinet:
                {
                    var ph=s!=null&&c.Tag>=0&&c.Tag<2?s.CabinetPhase[c.Tag]:NativeHazardPhase.Idle;
                    k=ph==NativeHazardPhase.Warn?(StationFrac(t*13+c.Param)<.5?1.2f:.4f):ph==NativeHazardPhase.Active?3.5f:.15f;break;
                }
                case KindHeadlight:
                {
                    if(s==null||s.Headlight<=0){k=0;break;}int line=s.Line-1;c.Z=NativeExpeditionStation.LineZ[line];c.Y=NativeExpeditionStation.TrackBed+1.2f;
                    if(s.Express==NativeStationState.ExpressWarn){c.X=-NativeExpeditionStation.ShellX+.6f;k=s.Headlight*2.6f;}else{c.X=s.Nose+.6f;k=2.8f;}
                    if(c.X>NativeExpeditionStation.ShellX){k=0;break;}
                    r=1;g=1.02f;b=1.08f;beam=1+(float)(Math.PI/2);break;
                }
                case KindBand:
                {
                    if(s==null||s.Passing<=0){k=0;break;}int line=s.Line-1;c.Z=NativeExpeditionStation.LineZ[line];c.Y=NativeExpeditionStation.TrackBed+2.6f;
                    c.X=Math.Max(-NativeExpeditionStation.ShellX,Math.Min(NativeExpeditionStation.ShellX,s.Nose-NativeExpeditionStation.TrainLength*.5f));
                    k=StationFrac(t*11)<.55f?1.6f:.7f;r=.85f;g=.95f;b=1f;break;
                }
            }
            r*=k;g*=k;b*=k;
            // The zone boss song's hand on the house lights, lamps, board and red signals (never a hazard's light).
            if(s!=null&&s.Music.On)NativeExpeditionStationKit.MusicLight(c.Kind,c.Kind==KindLook&&c.Look>=0&&c.Look<looks.Length?looks[c.Look].id:null,c.X,s.Music,s.Signal,ref r,ref g,ref b);
        }
        private static float StationFrac(double x)=>(float)(x-Math.Floor(x));
        /// <summary>Picks and eases the slots for a camera at (x, y, z); dt 0 snaps (stills). `slots`: how many may light (8,
        /// or 4 on the low tier). Allocation free.</summary>
        public void Update(double x,double y,double z,NativeStationState s,double time,float dt,int slots=Slots,float rate=4)
        {
            int n=Candidates.Length;slots=Math.Max(0,Math.Min(Slots,slots));
            for(int i=0;i<n;i++)
            {
                Shade(ref Candidates[i],s,time,out float r,out float g,out float b,out _);var c=Candidates[i];float lum=r*.3f+g*.55f+b*.15f;
                double dx=c.X-x,dy=(c.Y-y)*1.4,dz=c.Z-z,d=Math.Sqrt(dx*dx+dy*dy+dz*dz),u=d/Math.Max(.1,c.Range),near=u<1?(1-u*u)*(1-u*u):0;
                score[i]=lum*(float)(near+.35/(1+d*d/(c.Range*c.Range*.6)));
                // The express's lights are the threat: they always win a slot while they shine.
                if((c.Kind==KindHeadlight||c.Kind==KindBand)&&lum>0)score[i]+=100;
                // The beacons near the camera come next while the express is due: their sweep is the warning on the walls.
                else if(c.Kind==NativeExpeditionStationKit.GlowBeacon&&lum>0&&d<c.Range*2)score[i]+=50;
            }
            for(int k=0;k<Slots;k++){int best=-1;float bs=1e-4f;if(k<slots)for(int i=0;i<n;i++){if(score[i]<=bs)continue;bool taken=false;for(int j=0;j<k;j++)if(want[j]==i){taken=true;break;}if(!taken){bs=score[i];best=i;}}want[k]=best;}
            float step=dt<=0?1:Math.Min(1,dt*rate);
            for(int sl=0;sl<Slots;sl++)
            {
                int c=Slot[sl];if(c<0)continue;bool keep=false;for(int k=0;k<Slots;k++)if(want[k]==c){keep=true;break;}
                // The express's lights never ease: they arrive and leave with the train.
                bool snap=Candidates[c].Kind==KindHeadlight||Candidates[c].Kind==KindBand;
                Weight[sl]=keep?Math.Min(1,Weight[sl]+(snap?1:step)):Weight[sl]-(snap?1:step);if(Weight[sl]<=0){Weight[sl]=0;Slot[sl]=-1;}
            }
            for(int k=0;k<Slots;k++)
            {
                int c=want[k];if(c<0)continue;bool slotted=false;for(int sl=0;sl<Slots;sl++)if(Slot[sl]==c){slotted=true;break;}if(slotted)continue;
                bool snap=Candidates[c].Kind==KindHeadlight||Candidates[c].Kind==KindBand;
                for(int sl=0;sl<Slots;sl++)if(Slot[sl]<0){Slot[sl]=c;Weight[sl]=dt<=0||snap?1:Math.Min(1,step);break;}
            }
            for(int sl=0;sl<Slots;sl++)
            {
                int c=Slot[sl];if(c<0){Pos[sl*4]=0;Pos[sl*4+1]=-10000;Pos[sl*4+2]=0;Pos[sl*4+3]=1;Rgb[sl*3]=Rgb[sl*3+1]=Rgb[sl*3+2]=0;Beam[sl]=0;continue;}
                Shade(ref Candidates[c],s,time,out float r,out float g,out float b,out float beam);var l=Candidates[c];float w=Weight[sl];
                Pos[sl*4]=l.X;Pos[sl*4+1]=l.Y;Pos[sl*4+2]=l.Z;Pos[sl*4+3]=l.Range;Rgb[sl*3]=r*w;Rgb[sl*3+1]=g*w;Rgb[sl*3+2]=b*w;Beam[sl]=beam;
            }
        }
        /// <summary>Snaps the slots to the steady pick for a still (renders, a cut).</summary>
        public void Settle(double x,double y,double z,NativeStationState s,double time,int slots=Slots){for(int i=0;i<Slots;i++){Slot[i]=-1;Weight[i]=0;}Update(x,y,z,s,time,0,slots);}
        /// <summary>How many slots light this frame.</summary>
        public int Lit(){int n=0;for(int i=0;i<Slots;i++)if(Slot[i]>=0&&(Rgb[i*3]+Rgb[i*3+1]+Rgb[i*3+2])>0)n++;return n;}
    }
}
