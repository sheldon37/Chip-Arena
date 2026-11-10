using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Substation 128's light show for one frame (docs/arenas/CLUB.md "The show"): the LED dance floor's 270 tiles, the 16
    /// moving heads, the decorative laser fans, the LED wall's and the video cube's panels, and the three lights the show
    /// lends the arena's practical slots (NativeLighting.KitLights). One pure function of the bar grid (NativeClubLooks.Beats),
    /// the music's pulse (NativeClubPulse: presentation only) and the hazards' replicated state (the grid sections, the laser
    /// wall and the drop vents, through the same clocks the simulation decides hits by), so host, guests and the killcam show
    /// the same telegraphs at the same moments.
    /// <para>Priority, so a telegraph always reads: a section that charges, is live or discharges owns its tiles (amber filling to
    /// red under scrolling hazard stripes, red-hot with white veins while live, embers as it discharges); the laser wall owns its
    /// start column through its warning and the tiles under its beams in the pass, wherever no section telegraphs; the fixtures follow the laser, then the drop,
    /// then the charging sections, and dance only while nothing warns. Telegraph colours are never reduced by the light-flash
    /// setting or the tier (2.1 rule 4); only the party's pulses are (Reduced softens them, Calm stops them). Amber and red are
    /// the telegraphs' alone.</para>
    /// Allocation free after <see cref="Prepare"/> (per map).
    /// </summary>
    public sealed class NativeClubShow
    {
        public const int Sections=9,WallW=18,WallH=9,CubeW=4,CubeH=3,CubeFaces=5,CubePanels=CubeW*CubeH*4+CubeW*CubeW;
        /// <summary>Per tile (index i + j * TilesX, i along x from the west edge, j along z from the south edge): linear HDR colour,
        /// what owns it (<see cref="Party"/>, <see cref="Charging"/>, <see cref="Live"/>, <see cref="Discharge"/>, <see cref="LaserLine"/>)
        /// and the owner's level (0..1).</summary>
        public readonly float[] Floor=new float[NativeClubLooks.Tiles*3],Heat=new float[NativeClubLooks.Tiles];public readonly byte[] Owner=new byte[NativeClubLooks.Tiles];
        public const byte Party=0,Charging=1,Live=2,Discharge=3,LaserLine=4;
        /// <summary>The moving heads: place (x, y, z), where each aims (x, y, z), linear colour, light (0..1), beam spread (radius at 10 m).</summary>
        public readonly float[] HeadAt=new float[NativeClubLooks.Heads*3],HeadAim=new float[NativeClubLooks.Heads*3],HeadColor=new float[NativeClubLooks.Heads*3],HeadPower=new float[NativeClubLooks.Heads],HeadSpread=new float[NativeClubLooks.Heads];
        /// <summary>The LED wall's panels (column + row * WallW, row 0 at the bottom; linear HDR) and the cube's (four sides of
        /// CubeW x CubeH, then the bottom's CubeW x CubeW).</summary>
        public readonly float[] Wall=new float[WallW*WallH*3],Cube=new float[CubePanels*3];
        /// <summary>The decorative laser fans: on (0..1), the fan's sweep (rad) and its colour (linear).</summary>
        public float FanOn,FanSweep,FanSpread;public readonly float[] FanColor=new float[3];
        /// <summary>The lights lent to the practical slots: positions with ranges (4 each), linear colours times intensity (3 each).</summary>
        public readonly float[] LightPos=new float[12],LightRgb=new float[9];public int LightCount;
        /// <summary>The hazards' state this frame (the replicated clocks): the grid sections by section index (col + row * 3), the
        /// laser wall and the two drop vents; -1 / null when the map lacks one.</summary>
        public readonly NativeHazardState[] Grid=new NativeHazardState[Sections];public readonly MapHazard[] GridHazard=new MapHazard[Sections];
        public NativeHazardState Laser;public MapHazard LaserHazard;public FactoryHazardState DropW,DropE;public MapHazard DropHazardW,DropHazardE;
        /// <summary>The drop's rise (0..1 through the vents' warning) and its blast (1 at the burst, falling over 1.5 s); the party's
        /// beat, its pattern and the phrase's beat on the bar grid.</summary>
        public double Rise,Blast,Beats,BlastAge=double.PositiveInfinity;public int Pattern;
        public bool Warned;private string prepared;
        private static double H(int a,int b=0,int c=0)=>NativeClubLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double C01(double v)=>v<0?0:v>1?1:v;
        private static int Mod(long a,int n)=>(int)((a%n+n)%n);

        /// <summary>The map's hazards by their ids (once per map; nothing for another map).</summary>
        public void Prepare(ContentMap map)
        {
            if(map?.id==prepared)return;prepared=map?.id;Array.Clear(GridHazard,0,Sections);LaserHazard=DropHazardW=DropHazardE=null;
            foreach(var h in map?.hazards??Array.Empty<MapHazard>())
            {
                if(h==null)continue;
                if(h.kind=="grid"){int s=SectionAt(h.center.x,h.center.z);if(s>=0)GridHazard[s]=h;}
                else if(h.kind=="maglev")LaserHazard=h;
                else if(h.kind=="vent"){if(h.a.x<0)DropHazardW=h;else DropHazardE=h;}
            }
            for(int k=0;k<NativeClubLooks.Heads;k++)
            {
                double x,y,z;if(k<12){x=NativeClubLooks.HeadX[k%6];z=k<6?NativeClubLooks.TrussZ:-NativeClubLooks.TrussZ;y=NativeClubLooks.TrussY-NativeClubLooks.TrussDepth/2-.55;}
                else{x=(k&1)==0?-NativeClubLooks.TrussX:NativeClubLooks.TrussX;z=(k&2)==0?-NativeClubLooks.TrussZ:NativeClubLooks.TrussZ;y=NativeClubLooks.TrussY+NativeClubLooks.TrussDepth/2+.5;}
                HeadAt[k*3]=(float)x;HeadAt[k*3+1]=(float)y;HeadAt[k*3+2]=(float)z;
            }
        }
        /// <summary>The 3x3 section (col + row * 3) a floor point lies in, or -1 off the floor.</summary>
        public static int SectionAt(double x,double z)
        {
            if(x<NativeClubLooks.FloorX0||x>NativeClubLooks.FloorX1||z<NativeClubLooks.FloorZ0||z>NativeClubLooks.FloorZ1)return -1;
            int c=Math.Min(2,(int)((x-NativeClubLooks.FloorX0)/6)),r=Math.Min(2,(int)((z-NativeClubLooks.FloorZ0)/5));return c+r*3;
        }
        public static double TileX(int i)=>NativeClubLooks.FloorX0+(i%NativeClubLooks.TilesX)+.5;
        public static double TileZ(int i)=>NativeClubLooks.FloorZ0+(i/NativeClubLooks.TilesX)+.5;

        /// <summary>
        /// The frame: the replicated state (round index, phase, FightAt) at the field clock `now`, the render clock `render`
        /// (the party's beat behind the fight), the music's `pulse`, the light-flash setting and Reduced effects.
        /// </summary>
        public void Build(int round,NativePhase phase,double fightAt,double now,double render,NativeClubPulse pulse,NativeKitFlash flash)
        {
            Beats=NativeClubLooks.Beats(phase,fightAt,now,render);double beats=Beats;
            float kick=pulse?.Kick??.5f,bass=pulse?.Bass??.6f,level=pulse?.Level??.7f,mid=pulse?.Mid??.5f,high=pulse?.High??.4f;
            // ---- the hazards' state, from the simulation's own clocks
            Warned=false;for(int s=0;s<Sections;s++){var h=GridHazard[s];Grid[s]=h==null?NativeCyberCityHazards.Idle():NativeCyberCityHazards.At(h,phase,round,fightAt,now);if(Grid[s].Phase!=NativeHazardPhase.Idle)Warned=true;}
            Laser=LaserHazard==null?NativeCyberCityHazards.Idle():NativeCyberCityHazards.At(LaserHazard,phase,round,fightAt,now);if(Laser.Phase!=NativeHazardPhase.Idle)Warned=true;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            DropW=DropHazardW==null?default:NativeFactoryHazards.State(DropHazardW,round,clock);DropE=DropHazardE==null?default:NativeFactoryHazards.State(DropHazardE,round,clock);
            var drop=DropHazardW!=null?DropW:DropE;var dropH=DropHazardW??DropHazardE;
            Rise=drop.Phase==NativeFactoryHazards.Warning?drop.Warn:drop.Phase==NativeFactoryHazards.Bursting?1:0;
            BlastAge=double.PositiveInfinity;
            if(dropH!=null&&NativeFactoryHazards.Cycle(dropH,clock,out int cycle,out double u)){double burst=Math.Max(0,dropH.period-dropH.warn-dropH.dwell)+dropH.warn,age=u-burst;if(age<0&&cycle>0)age+=dropH.period;if(age>=0&&age<6)BlastAge=age;}
            Blast=BlastAge<1.5?Math.Exp(-BlastAge*2.2):0;
            Pattern=(int)(((long)Math.Floor(beats/32)%6+6)%6);
            FloorShow(beats,kick,bass,level,mid,high,flash);
            Heads(beats,kick,level,flash);
            Screens(beats,kick,bass,mid,high,level,flash);
            Fans(beats,kick,level);
            Lights(beats,kick,level);
        }

        // ---------------- the dance floor ----------------
        private void FloorShow(double beats,float kick,float bass,float level,float mid,float high,NativeKitFlash flash)
        {
            int nx=NativeClubLooks.TilesX,nz=NativeClubLooks.TilesZ;long beat=(long)Math.Floor(beats);double bf=beats-beat;
            int p=Pattern,q=(p+5)%6;double since=Frac(beats/32)*32,fade=C01(since/1.0);   // the pattern's first beat crossfades from the last
            bool anyWarn=false;for(int s=0;s<Sections;s++)if(Grid[s].Phase!=NativeHazardPhase.Idle)anyWarn=true;
            double calm=flash==NativeKitFlash.Calm?.35:1;
            for(int j=0;j<nz;j++)for(int i=0;i<nx;i++)
            {
                int t=i+j*nx;double x=NativeClubLooks.FloorX0+i+.5,z=NativeClubLooks.FloorZ0+j+.5;
                PartyTile(p,i,j,x,z,beats,beat,bf,kick,bass,level,mid,high,calm,out double r,out double g,out double b);
                if(fade<1){PartyTile(q,i,j,x,z,beats,beat,bf,kick,bass,level,mid,high,calm,out double r2,out double g2,out double b2);r=r2+(r-r2)*fade;g=g2+(g-g2)*fade;b=b2+(b-b2)*fade;}
                // the drop: the floor rises with the build (from the edges in) and a white wave rolls out from the stage at the burst
                if(Rise>0&&Blast<=0){double edge=Math.Max(Math.Abs(x)/9,Math.Abs(z)/7.5),lift=C01(Rise*1.4-(1-edge));double k=1+1.4*lift*Rise;r*=k;g*=k;b*=k;}
                if(BlastAge<2.5){double d=Math.Sqrt(x*x+(z-13)*(z-13)),front=BlastAge*14-d+6,w=Math.Exp(-front*front*.08)*(flash==NativeKitFlash.Full?1:.45)*(1-BlastAge/2.5);r+=2.6*w;g+=2.4*w;b+=2.8*w;}
                if(anyWarn){r*=.42;g*=.42;b*=.42;}
                byte own=Party;double heat=0;
                int s0=SectionAt(x,z);
                if(s0>=0&&GridHazard[s0]!=null)
                {
                    var st=Grid[s0];
                    if(st.Phase==NativeHazardPhase.Warn)
                    {
                        // charging: amber filling toward red under hazard stripes that scroll inward (Reduced: slower; Calm: still)
                        double u=st.Level;var h=GridHazard[s0];double lx=(x-h.center.x)/(h.size.x/2),lz=(z-h.center.z)/(h.size.z/2),ring=Math.Max(Math.Abs(lx),Math.Abs(lz));
                        double scroll=flash==NativeKitFlash.Calm?0:flash==NativeKitFlash.Reduced?.6:1.4,stripe=Frac((x+z)*.5+beats*scroll*.5)<.5?1:.6;
                        double fill=C01((u*1.25-(1-ring))*3);double last=st.Until<NativeClubLooks.Beat?1:0;
                        double k=(.75+.55*u)*(.72+.28*fill)*(last>0?1.2:stripe);
                        r=NativeClubLooks.R(NativeClubLooks.Amber)+(NativeClubLooks.R(NativeClubLooks.Danger)-NativeClubLooks.R(NativeClubLooks.Amber))*u;g=NativeClubLooks.G(NativeClubLooks.Amber)+(NativeClubLooks.G(NativeClubLooks.Danger)-NativeClubLooks.G(NativeClubLooks.Amber))*u;b=NativeClubLooks.B(NativeClubLooks.Amber)+(NativeClubLooks.B(NativeClubLooks.Danger)-NativeClubLooks.B(NativeClubLooks.Amber))*u;
                        r*=k;g*=k;b*=k;own=Charging;heat=u;
                    }
                    else if(st.Phase==NativeHazardPhase.Active)
                    {
                        // live: red-hot, white veins crawling across it (steady, never a strobe)
                        double vein=Math.Max(0,1-Math.Abs(Math.Sin(x*1.7+z*.9+beats*1.3)+Math.Sin(z*2.3-x*.6-beats*.9))*2.8);
                        double k=1.45+.15*Math.Sin(beats*Math.PI*2+i*.7+j*1.3);r=NativeClubLooks.R(NativeClubLooks.Danger)*k+.6*vein;g=NativeClubLooks.G(NativeClubLooks.Danger)*k+.32*vein;b=NativeClubLooks.B(NativeClubLooks.Danger)*k+.28*vein;own=Live;heat=1;
                    }
                    else if(st.Phase==NativeHazardPhase.Clear)
                    {
                        double u=st.Level,ember=H(i,j,(int)(beats*4))*.5+.5;r=NativeClubLooks.R(NativeClubLooks.Danger)*1.4*u*ember+r*(1-u);g=NativeClubLooks.G(NativeClubLooks.Danger)*1.4*u*ember+g*(1-u);b=NativeClubLooks.B(NativeClubLooks.Danger)*1.4*u*ember+b*(1-u);own=Discharge;heat=u;
                    }
                }
                // the laser wall: its start column glows red through the warning (solid in the danger), the tiles under its beams in the pass
                if(LaserHazard!=null&&Laser.Phase!=NativeHazardPhase.Idle&&own==Party)
                {
                    var lh=LaserHazard;double near=lh.center.x-Laser.Direction*lh.size.x/2;
                    if(Laser.Phase==NativeHazardPhase.Warn||Laser.Phase==NativeHazardPhase.Danger)
                    {
                        double d=Math.Abs(x-(near+Laser.Direction*.5));
                        if(d<.6){double k=Laser.Phase==NativeHazardPhase.Danger?1.6:.8+.7*Laser.Level*(.6+.4*Math.Sin(beats*Math.PI));r=NativeClubLooks.R(NativeClubLooks.LaserRed)*k;g=NativeClubLooks.G(NativeClubLooks.LaserRed)*k;b=NativeClubLooks.B(NativeClubLooks.LaserRed)*k;own=LaserLine;heat=Laser.Level;}
                        // chevrons along the outer rows point the way it will run
                        else if((j==0||j==nz-1)&&Laser.Phase==NativeHazardPhase.Danger){double c=Frac((x*Laser.Direction)*.5-beats*.5);if(c<.3){r=NativeClubLooks.R(NativeClubLooks.LaserRed)*1.6;g=NativeClubLooks.G(NativeClubLooks.LaserRed)*1.6;b=NativeClubLooks.B(NativeClubLooks.LaserRed)*1.6;own=LaserLine;heat=.5;}}
                    }
                    else if(Laser.Phase==NativeHazardPhase.Active&&!double.IsNaN(Laser.Head))
                    {
                        double behind=(Laser.Head-x)*Laser.Direction;
                        if(behind>-.55&&behind<3.5){double k=behind<.55?1.8:1.2*Math.Exp(-(behind-.55)*1.1);r=Math.Max(r,NativeClubLooks.R(NativeClubLooks.LaserRed)*k);g=Math.Max(g,NativeClubLooks.G(NativeClubLooks.LaserRed)*k*.6);b=Math.Max(b,NativeClubLooks.B(NativeClubLooks.LaserRed)*k*.6);own=LaserLine;heat=1;}
                    }
                }
                Floor[t*3]=(float)r;Floor[t*3+1]=(float)g;Floor[t*3+2]=(float)b;Owner[t]=own;Heat[t]=(float)heat;
            }
        }
        // One party tile in pattern p (linear HDR): checker pulse, ripples, sweeping bands, the equaliser, sparkle rain, the spiral.
        private static void PartyTile(int p,int i,int j,double x,double z,double beats,long beat,double bf,float kick,float bass,float level,float mid,float high,double calm,out double r,out double g,out double b)
        {
            int[] pal=NativeClubLooks.Party;int bar=(int)((beat/4%7+7)%7);int c0=pal[bar],c1=pal[(bar+2)%pal.Length],c2=pal[(bar+4)%pal.Length];
            double on=0;int col=c0;double kk=calm*kick;
            switch(p)
            {
                case 0:{bool flip=((i>>1)+(j>>1)+beat)%2==0;on=flip?.35+1.4*kk:.08;col=flip?c0:c1;break;}
                case 1:{double d=Math.Sqrt(x*x+z*z),rr=bf*13,w=Math.Abs(d-rr);on=w<1.1?(1.8-w)*(.4+.9*kk):.06+.04*Math.Sin(d-beats);col=((int)(d/3)+beat)%2==0?c0:c2;break;}
                case 2:{double v=Frac((x+z)/7-beats/4);on=v<.28?.5+1.1*kk:v<.4?.18:.05;col=v<.14?c1:c0;break;}
                case 3:{int band=(int)((i*7)/NativeClubLooks.TilesX);double lv=band<2?bass:band<5?mid:high;double hgt=(.25+.75*lv)*(.55+.45*Frac(Math.Sin(i*12.9898+beat*.37)*43758.5453))*NativeClubLooks.TilesZ;
                    double fromMid=Math.Abs(j-(NativeClubLooks.TilesZ-1)/2.0)*2;on=fromMid<hgt?.35+.9*(1-fromMid/Math.Max(1,hgt))+.6*kk:.05;col=fromMid>hgt*.7?c2:c0;break;}
                case 4:{double h=Frac(Math.Sin(i*127.1+j*311.7+beat*74.7)*43758.5453);on=h<.2?(1.6+.8*kk)*(1-bf)*calm+.08:.07;col=h<.07?NativeClubLooks.Party[6]:h<.14?c0:c1;break;}
                default:{double a=Math.Atan2(z,x)/(2*Math.PI),d=Math.Sqrt(x*x+z*z),v=Frac(a*3+d/9-beats/8);on=v<.3?.4+1.2*kk*(1-v/.3):.06;col=v<.15?c2:c0;break;}
            }
            on*=.75+.35*level;r=NativeClubLooks.R(col)*on;g=NativeClubLooks.G(col)*on;b=NativeClubLooks.B(col)*on;
        }

        // ---------------- the moving heads ----------------
        private void Heads(double beats,float kick,float level,NativeKitFlash flash)
        {
            int n=NativeClubLooks.Heads;long bar=(long)Math.Floor(beats/4);int[] pal=NativeClubLooks.Party;
            double lx=double.NaN;bool laser=false;
            if(LaserHazard!=null&&Laser.Phase!=NativeHazardPhase.Idle&&Laser.Phase!=NativeHazardPhase.Clear)
            {laser=true;lx=Laser.Phase==NativeHazardPhase.Active&&!double.IsNaN(Laser.Head)?Math.Max(-9,Math.Min(9,Laser.Head)):LaserHazard.center.x-Laser.Direction*LaserHazard.size.x/2+Laser.Direction*.5;}
            int charging=-1;for(int s=0;s<Sections;s++)if(Grid[s].Phase==NativeHazardPhase.Warn||Grid[s].Phase==NativeHazardPhase.Active){charging=s;break;}
            for(int k=0;k<n;k++)
            {
                double hx=HeadAt[k*3],hy=HeadAt[k*3+1],hz=HeadAt[k*3+2];double ax,az,ay=0,power,spread=.9;int col;
                if(laser&&(Laser.Phase==NativeHazardPhase.Danger||Laser.Phase==NativeHazardPhase.Active))
                {
                    // the heads chase the laser wall: every beam on its line, red
                    ax=lx;az=NativeClubLooks.FloorZ0+(k%6+.5)*15/6.0;power=Laser.Phase==NativeHazardPhase.Active?1:.6+.4*Laser.Level;col=NativeClubLooks.LaserRed;spread=.5;
                }
                else if(Rise>0||Blast>0)
                {
                    // the drop: the heads lift and turn faster as it builds, then slam down on the floor in white
                    double sp=1+Rise*5,a=beats*.25*sp+k*Math.PI*2/n;
                    if(Blast>0){ax=Math.Cos(a)*7;az=Math.Sin(a)*6;power=1;col=0xF4F0FF;spread=1.3;}
                    else{double up=Rise;ax=hx+Math.Cos(a)*(4+8*up);az=hz+Math.Sin(a)*(4+8*up);ay=up*6;power=.5+.5*up;col=pal[Mod(bar+k,pal.Length)];spread=.7+.6*up;}
                }
                else if(charging>=0)
                {
                    // a section charges: the heads find the sections that warn, amber
                    int s=charging;double sx=GridHazard[s].center.x,sz=GridHazard[s].center.z;int m=0;for(int t=0;t<Sections;t++)if(Grid[t].Phase==Grid[charging].Phase&&GridHazard[t]!=null){if(m==k%5){sx=GridHazard[t].center.x;sz=GridHazard[t].center.z;}m++;}
                    double w=beats*.5+k;ax=sx+Math.Sin(w)*2;az=sz+Math.Cos(w*1.3)*1.6;power=.8;col=Grid[charging].Phase==NativeHazardPhase.Active?NativeClubLooks.Danger:NativeClubLooks.Amber;spread=.6;
                }
                else
                {
                    // the party: a figure per 8 bars, the colours changing every bar, the beams breathing with the kick
                    int fig=(int)((bar/8%4+4)%4);double t=beats/4,ph=k*.7;
                    switch(fig)
                    {
                        case 0:ax=Math.Sin(t*Math.PI*.5+ph)*8;az=Math.Cos(t*Math.PI*.37+ph*1.3)*6;break;
                        case 1:ax=(k%2==0?-1:1)*(2+6*Frac(t+k*.1));az=hz*.6;break;
                        case 2:{double a=t*1.2+k*Math.PI*2/n;ax=Math.Cos(a)*7;az=Math.Sin(a)*5.5;break;}
                        default:ax=hx*.3+Math.Sin(t*2+ph)*3;az=-hz*.8+Math.Cos(t*1.7+ph)*2;break;
                    }
                    double kk=flash==NativeKitFlash.Calm?.3:kick;power=.35+.4*level+.35*kk;col=pal[Mod(bar+k/4,pal.Length)];spread=.75+.25*kk;
                }
                HeadAim[k*3]=(float)ax;HeadAim[k*3+1]=(float)ay;HeadAim[k*3+2]=(float)az;HeadPower[k]=(float)C01(power);HeadSpread[k]=(float)spread;
                HeadColor[k*3]=(float)NativeClubLooks.R(col);HeadColor[k*3+1]=(float)NativeClubLooks.G(col);HeadColor[k*3+2]=(float)NativeClubLooks.B(col);
            }
        }

        // ---------------- the LED wall and the video cube ----------------
        // 3 x 5 digits (bit 14 top left, row major), for the drop's countdown.
        private static readonly int[] Digit={0x7B6F,0x2C97,0x73E7,0x73CF,0x5BC9,0x79CF,0x79EF,0x7249,0x7BEF,0x7BCF};
        private void Screens(double beats,float kick,float bass,float mid,float high,float level,NativeKitFlash flash)
        {
            long beat=(long)Math.Floor(beats);double bf=beats-beat;int[] pal=NativeClubLooks.Party;int bar=(int)(((long)Math.Floor(beats/4))%7+7)%7;
            int left=-1;double meter=0;bool danger=false;
            if(Rise>0&&DropHazardW!=null){double rem=(1-Rise)*DropHazardW.warn;left=(int)Math.Ceiling(rem/NativeClubLooks.Beat-1e-6);meter=Rise;}
            if(LaserHazard!=null&&(Laser.Phase==NativeHazardPhase.Warn||Laser.Phase==NativeHazardPhase.Danger)){danger=true;meter=Math.Max(meter,Laser.Level);}
            for(int row=0;row<WallH;row++)for(int c=0;c<WallW;c++)
            {
                int o=(c+row*WallW)*3;double r,g,b;
                if(Blast>0){double k=3.5*Blast*(flash==NativeKitFlash.Full?1:.5);r=k;g=k*.95;b=k;}
                else if(left>0&&left<=4)
                {
                    // the countdown to the drop: the last four beats as big digits, amber, over a dark field
                    int d=Math.Min(9,left),gx=(c-6)/2,gy=6-row;bool lit=c>=6&&c<12&&gy>=0&&gy<5&&((Digit[d]>>(14-(gy*3+gx)))&1)!=0;
                    double k=lit?2.6:.04;r=NativeClubLooks.R(NativeClubLooks.Amber)*k;g=NativeClubLooks.G(NativeClubLooks.Amber)*k;b=NativeClubLooks.B(NativeClubLooks.Amber)*k;
                }
                else if(left>0||danger)
                {
                    // a warning: the meter fills from the bottom (amber for the drop, red for the laser), the rest stripes
                    int colr=danger?NativeClubLooks.LaserRed:NativeClubLooks.Amber;double fillTo=meter*WallH;double stripe=Frac((c+row)*.25-beats*.5)<.5?1:.3;
                    double k=row<fillTo?2.2:.25*stripe;if(danger&&Laser.Phase==NativeHazardPhase.Danger){double arrow=Frac((c*Laser.Direction)/6.0-beats*.5);k=Math.Max(k,arrow<.25&&Math.Abs(row-4)<=2-(int)(arrow*8)?2.8:0);}
                    r=NativeClubLooks.R(colr)*k;g=NativeClubLooks.G(colr)*k;b=NativeClubLooks.B(colr)*k;
                }
                else
                {
                    // the party: a spectrum mirrored about the middle, peaks in the bar's second colour, a slow hue wash behind
                    double band=Math.Abs(c-(WallW-1)/2.0)/(WallW/2.0),lv=band<.3?bass:band<.7?mid:high,top=(.25+.75*lv*(.75+.25*Math.Sin(c*1.3+beats)))*WallH;
                    int colr=row<top*.6?pal[bar]:pal[(bar+2)%pal.Length];double k=row<top?1.1+.9*kick*(flash==NativeKitFlash.Calm?.3:1):.08+.06*Math.Sin(beats*.5+c*.3);
                    r=NativeClubLooks.R(colr)*k;g=NativeClubLooks.G(colr)*k;b=NativeClubLooks.B(colr)*k;
                }
                Wall[o]=(float)r;Wall[o+1]=(float)g;Wall[o+2]=(float)b;
            }
            // the cube: its sides wash in the bar's colours and chase the kick round it; the bottom ripples; warnings as the wall
            for(int p=0;p<CubePanels;p++)
            {
                double r,g,b;int side=p<CubeW*CubeH*4?p/(CubeW*CubeH):4,q=side<4?p%(CubeW*CubeH):p-CubeW*CubeH*4,c=q%CubeW,row=q/CubeW;
                if(Blast>0){double k=3*Blast*(flash==NativeKitFlash.Full?1:.5);r=g=b=k;}
                else if(left>0||danger){int colr=danger?NativeClubLooks.LaserRed:NativeClubLooks.Amber;double k=side<4?(row<meter*CubeH?2.0:.3):(.4+1.4*meter);r=NativeClubLooks.R(colr)*k;g=NativeClubLooks.G(colr)*k;b=NativeClubLooks.B(colr)*k;}
                else
                {
                    double chase=Frac(beats/4+(side*CubeW+c)/(4.0*CubeW)),k=side<4?.25+1.6*Math.Exp(-chase*6)*(.4+.6*kick)+.2*Math.Sin(row+beats):.2+1.2*Math.Exp(-Frac(beats-Math.Sqrt((c-1.5)*(c-1.5)+(row-1.5)*(row-1.5))*.25)*4);
                    int colr=side<4?pal[(bar+side)%pal.Length]:pal[(bar+3)%pal.Length];r=NativeClubLooks.R(colr)*k;g=NativeClubLooks.G(colr)*k;b=NativeClubLooks.B(colr)*k;
                }
                Cube[p*3]=(float)r;Cube[p*3+1]=(float)g;Cube[p*3+2]=(float)b;
            }
        }

        // ---------------- the laser fans and the lent lights ----------------
        private void Fans(double beats,float kick,float level)
        {
            // the fans play the party only: off through every warning so red stays the laser wall's
            double want=Warned||Rise>0?0:.4+.6*level;FanOn=(float)want;FanSweep=(float)(Math.Sin(beats*Math.PI/8)*.35);FanSpread=(float)(.6+.3*Math.Sin(beats*Math.PI/16));
            int col=((long)Math.Floor(beats/16)%2==0)?0x3DFF6A:NativeClubLooks.Cyan;FanColor[0]=(float)NativeClubLooks.R(col);FanColor[1]=(float)NativeClubLooks.G(col);FanColor[2]=(float)NativeClubLooks.B(col);
        }
        private void Lights(double beats,float kick,float level)
        {
            // three lights over the floor and the stage take the show's colour: the floor's mean party colour, two heads'
            double fr=0,fg=0,fb=0;for(int t=0;t<NativeClubLooks.Tiles;t++){fr+=Floor[t*3];fg+=Floor[t*3+1];fb+=Floor[t*3+2];}fr/=NativeClubLooks.Tiles;fg/=NativeClubLooks.Tiles;fb/=NativeClubLooks.Tiles;
            void Put(int k,double x,double y,double z,double range,double r,double g,double b){LightPos[k*4]=(float)x;LightPos[k*4+1]=(float)y;LightPos[k*4+2]=(float)z;LightPos[k*4+3]=(float)range;LightRgb[k*3]=(float)r;LightRgb[k*3+1]=(float)g;LightRgb[k*3+2]=(float)b;}
            Put(0,0,2.2,0,16,fr*2.6,fg*2.6,fb*2.6);
            for(int k=1;k<3;k++){int h=k==1?1:10;double p=HeadPower[h]*1.8;Put(k,HeadAim[h*3],2.4,HeadAim[h*3+2],11,HeadColor[h*3]*p,HeadColor[h*3+1]*p,HeadColor[h*3+2]*p);}
            LightCount=3;
        }
    }
}
