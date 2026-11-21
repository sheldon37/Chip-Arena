using System;

namespace ChipArena.UnityConversion
{
    // Pure port of the source's field telegraphs (client/chaos-visuals.ts at 05950ea), the Siren floor telegraphs
    // (client/siren/fx.ts) and the ability aim preview (client/ability-preview.ts). Cosmetic only: callers pass the
    // authoritative field (position, gameplay radius, born/until) and nothing here feeds back into the match.
    // Owner request 2026-10-04: chip and ability areas are never outlined. An area reads through its volume (the
    // NativeBlastSim field emitters), an edge-less ground glow that fades to exactly 0 at the gameplay radius, the cores
    // and the translucent domes. Owner 2026-10-11: no ground glow in mid-air (GlowShown). Coordinates and rotations are the
    // source's numbers, as in NativeBlastSim: the converted maps keep them, so the drawn scene is the source's mirrored in Z
    // exactly like the maps, and the source's counter-clockwise front faces are Unity's clockwise front faces.

    /// <summary>One opaque unlit core cube: centre, full edge lengths, rotation (three.js Euler XYZ as a quaternion), sRGB hex.</summary>
    public struct NativeFieldCube{public double X,Y,Z,Sx,Sy,Sz,Qx,Qy,Qz,Qw;public int Color;}

    /// <summary>Shared unit geometry built once: positions (xyz, unit radius), per-vertex alpha and triangles in the source's order.</summary>
    public sealed class NativeFieldMesh
    {
        public readonly float[] Positions,Alpha;public readonly int[] Triangles;
        public int Count=>Alpha.Length;
        public NativeFieldMesh(float[] positions,float[] alpha,int[] triangles){Positions=positions;Alpha=alpha;Triangles=triangles;}
    }

    /// <summary>What a field draws besides its ground glow.</summary>
    public enum NativeFieldLook{Glow,BlackHole,Bomb,Mine,Implosion,Dome}

    /// <summary>
    /// A chip bomb, implosion bomb or mine body at one moment, for drawing the modelled bomb (NativeVfxMeshes.Bomb) lit, with
    /// its band and fuse tip glowing (owner 2026-10-11 effects overhaul: the bomb was a grey box with a blinking white box).
    /// The timing is exactly the source cube look's (<see cref="NativeFieldRules.Cubes"/>): the same spin, BombEdge pulse,
    /// FuseVisible blink speeding up with the fuse and final-half-second flicker. Its place is not: it stands on its floor
    /// (<see cref="NativeFieldRules.BombBase"/>; owner cut, review 2026-10-11: the source's body cube hovered .28 m over a
    /// flat floor and sank into a raised one, and with the ground disc gone the bomb is the fuse's only telegraph). Cosmetic only.
    /// </summary>
    public struct NativeBombPose
    {
        public NativeFieldLook Look;
        /// <summary>The casing centre: on the field's x/z, its casing resting on <see cref="NativeFieldRules.BombBase"/>
        /// (<see cref="NativeFieldRules.BombLift"/> above it, half the scaled casing height up; the pulse grows it from the floor).</summary>
        public double X,Y,Z;
        /// <summary>Scale of the bomb mesh per axis; 1 is its rest size (casing diameter NativeVfxMeshes.BombDiameter, the
        /// source's .38 m body cube edge). A bomb or implosion pulses uniformly by ±12% (BombEdge); a mine is the source's flat
        /// .5 x .16 x .5 body and does not pulse.</summary>
        public double Sx,Sy,Sz;
        /// <summary>The body's spin: three.js Euler(implosion ? age*2.5 : 0, age*2, 0) as a quaternion, used directly as a Unity
        /// Quaternion exactly as NativeFieldPresentation draws the source's body cube.</summary>
        public double Qx,Qy,Qz,Qw;
        /// <summary>The source's fuse cube centre (.66 m above the field origin, .22 m for a mine), not spun with the body: the
        /// timing reference only; the modelled tip is at Y + BombTipY x Sy.</summary>
        public double FuseX,FuseY,FuseZ;
        /// <summary>The source's fuse cube shows this frame (FuseVisible): its blink and its final-half-second flicker.</summary>
        public bool FuseLit;
        /// <summary>Fuse progress u (0 at birth, 1 at the end), the blink rate in rad/s (6 + 20u), seconds since birth and left.</summary>
        public double Progress,BlinkRate,Age,Left;
        /// <summary>Band and fuse-tip glow, 0..1: above 0 exactly while FuseLit, with a soft edge on each blink
        /// (<see cref="NativeFieldRules.BombGlow"/>), and growing with the fuse. Multiply the emissive parts by it.</summary>
        public double Emissive;
        /// <summary>The casing's sRGB colour (the source's body cube: 0x2a0f46 for an implosion, 0x3a3434 otherwise).</summary>
        public int Body;
    }

    public static class NativeFieldRules
    {
        /// <summary>
        /// Ally/enemy tint of ability fields (telegraphColor). The design, the source's tests, its visual lab and the owner's
        /// 2026-10-04 note want it; the pinned Electron renderer never passes the local pilot (renderer.ts:133), so the shipped
        /// game shows the kind colour. Set false to match the shipped look. Chip fields keep their colour either way.
        /// </summary>
        public static readonly bool TintAbilityFields=true;
        public const int Ally=0x55dcff,Enemy=0xff5a46;
        /// <summary>FIELD_GLOW_ALPHA: the glow disc's opacity at the centre (vertex alpha 1 there, 0 on every rim vertex).</summary>
        public const double GlowAlpha=.22,DomeAlpha=.13;
        public const int GlowSegments=40,DomeWidth=14,DomeHeight=6;
        /// <summary>Most cubes one field draws (black hole: core, hidden inner cube, 10 accretion cubes).</summary>
        public const int MaxCubes=12;

        public static int KindColor(string kind)
        {
            switch(kind)
            {
                case "black-hole":return 0xab67ff;case "acid":return 0xb8f53c;case "fire":return 0xff672d;case "frost":return 0x8fdfff;
                case "time":return 0x83aeff;case "repulsion":return 0xff728f;case "heal":return 0x79ffa9;case "storm":return 0xddadff;
                case "bomb":return 0xffa141;case "implosion":return 0xb068ff;case "mine":return 0xff515d;default:return 0xffffff;
            }
        }
        /// <summary>ABILITY_FX (shared/abilities.ts): fx tags of ability-owned fields.</summary>
        public static bool IsAbilityFx(string fx)
        {
            switch(fx)
            {
                case "frag":case "rocket":case "orbital-mark":case "flame-lance":case "meteor-strike":case "tidal-wave":case "geyser-wall":
                case "blink-strike":case "ball-lightning":case "shackles":case "singularity":return true;
                default:return false;
            }
        }
        /// <summary>
        /// The field colour, picked once when a field is first drawn (telegraphColor). viewer: there is a local pilot (none on a
        /// dedicated host). ally: see <see cref="IsAlly"/>. The friendly-fire setting plays no part.
        /// </summary>
        public static int Color(string kind,string fx,bool viewer,bool ally)
        {
            if(!IsAbilityFx(fx))return fx=="thunderstep"?0x9fd2ff:fx=="comet"?0xffc04a:KindColor(kind);
            if(!viewer||!TintAbilityFields)return KindColor(kind);
            return ally?Ally:Enemy;
        }
        /// <summary>Own fields, any PvE field and same-team fields in teams mode are allied. Unity teams are seat numbers (0 is a team).</summary>
        public static bool IsAlly(bool self,bool pve,bool teams,bool sameTeam)=>self||pve||teams&&sameTeam;
        /// <summary>
        /// The fallback birth of an ability zone that carries no replicated Born (0), first computed when it is first seen
        /// (firstSeen: the field clock then). Exact where it follows from the expiry (NativeAbilities): Singularity lasts its
        /// duration, Meteor Strike's burning ground its duration (x1.5 with Cinder Field), the Orbital Mark its fuse. These are
        /// the zones with an arrival burst or an aged core, so a late joiner or a presentation reset (killcam start and end)
        /// does not replay them. The Meteor Strike and Geyser Wall bombs expire after their fuse plus a close-cast fuse and a
        /// .35 s stagger per geyser, which the zone does not record: their birth is at most until - fuse, so a bomb first seen
        /// late is aged from there (exact for a far-cast meteor and the first geyser), else from when it was first seen. They
        /// are fused, so they get no arrival burst either way.
        /// </summary>
        public static double ZoneBorn(string ability,string kind,double until,double duration,double fuse,bool cinderField,double firstSeen)
        {
            if(ability=="singularity")return until-duration;
            if(ability=="meteor-strike"&&kind=="fire")return until-duration*(cinderField?1.5:1);
            if(ability=="orbital-mark")return until-fuse;
            if((ability=="meteor-strike"||ability=="geyser-wall")&&fuse>0)return Math.Min(firstSeen,until-fuse);
            return firstSeen;
        }

        /// <summary>The field group's base height: a body-centred point (y .85-.95) sits .7 lower, a floor point stays at .07.</summary>
        public static double Origin(double y)=>Math.Max(.07,y-.7);
        public static double GlowY(double y)=>Origin(y)+.03;
        /// <summary>How far above NativeBlastSim.NearGround the glow disc fades out.</summary>
        public const double GlowFade=.6;
        /// <summary>
        /// Beyond the source (owner 2026-10-11: an area indicator left by a wall hit "appears like a floating pancake"): the share
        /// of a field's flat ground glow that shows, given the floor under the field (NativeBlastField.Floor). 1 while the field
        /// is at most NearGround above that floor, as every field on the ground is (a floor point, a pilot's centre); fading to
        /// 0 over GlowFade above that, so a field on a wall or a ceiling, over a pit, or riding a pilot high in the air draws
        /// no disc in mid-air. Its volume, cores and domes still draw. floor null (no floor known): the source, always 1.
        /// </summary>
        public static double GlowShown(double y,double? floor)=>floor.HasValue?Math.Max(0,Math.Min(1,1-(y-floor.Value-NativeBlastSim.NearGround)/GlowFade)):1;
        public static NativeFieldLook LookOf(string kind,string fx)
        {
            if(kind=="black-hole")return NativeFieldLook.BlackHole;
            if((kind=="bomb"||kind=="mine"||kind=="implosion")&&!IsAbilityFx(fx)&&fx!="artillery"&&fx!="thunderstep"&&fx!="comet")
                return kind=="mine"?NativeFieldLook.Mine:kind=="implosion"?NativeFieldLook.Implosion:NativeFieldLook.Bomb;
            return kind=="time"||kind=="repulsion"?NativeFieldLook.Dome:NativeFieldLook.Glow;
        }
        private static double Clamp01(double v)=>Math.Max(0,Math.Min(1,v));
        /// <summary>Fuse progress u (0 at birth, 1 at the end of the fuse).</summary>
        public static double Progress(double born,double until,double time)=>Clamp01((time-born)/Math.Max(.01,until-born));
        /// <summary>The fuse blinks faster as u grows, then flickers in the last half second.</summary>
        public static bool FuseVisible(double age,double u,double left)=>Math.Sin(age*(6+u*20))>-.2&&!(left<.5&&Math.Sin(left*60)>.3);
        /// <summary>Uniform edge of a chip bomb or implosion body (mines do not pulse).</summary>
        public static double BombEdge(double age,double u)=>.38*(1+Math.Sin(age*(8+u*16))*.12);
        /// <summary>Implosion orbit radius: .75 m tightening to .25 m as the fuse burns.</summary>
        public static double OrbitRadius(double u)=>.75-u*.5;
        /// <summary>Black-hole accretion cube n: orbit angle and radius (fixed, not scaled with the field).</summary>
        public static double DiscAngle(int n,double age)=>n*Math.PI/5+age*2.4;
        public static double DiscRadius(int n)=>.95+(n%2)*.3;

        private static void Euler(ref NativeFieldCube c,double ex,double ey)
        {
            // three.js Euler(ex, ey, 0) in XYZ order, as NativeBlastPool.
            double c1=Math.Cos(ex/2),c2=Math.Cos(ey/2),s1=Math.Sin(ex/2),s2=Math.Sin(ey/2);
            c.Qx=s1*c2;c.Qy=c1*s2;c.Qz=s1*s2;c.Qw=c1*c2;
        }
        private static int Put(NativeFieldCube[] into,int n,double x,double y,double z,double sx,double sy,double sz,double ex,double ey,int color)
        {var c=new NativeFieldCube{X=x,Y=y,Z=z,Sx=sx,Sy=sy,Sz=sz,Color=color};Euler(ref c,ex,ey);into[n]=c;return n+1;}
        /// <summary>
        /// The visible core cubes of a field at time (ChaosVisuals.update), in the source's child order. color is the field
        /// colour (<see cref="Color"/>), used by the black-hole accretion disc and the implosion orbit. Returns the count.
        /// </summary>
        public static int Cubes(NativeBlastField f,int color,double time,NativeFieldCube[] into)
        {
            var look=LookOf(f.Kind,f.Fx);if(look==NativeFieldLook.Glow||look==NativeFieldLook.Dome)return 0;
            double gx=f.X,gy=Origin(f.Y),gz=f.Z,age=time-f.Born,left=f.Until-time,u=Progress(f.Born,f.Until,time);int n=0;
            if(look==NativeFieldLook.BlackHole)
            {
                // A dark spinning core; the pale inner cube (.30 inside the .62 core) never shows but is kept for parity.
                n=Put(into,n,gx,gy+.7,gz,.62,.62,.62,age*.8,age*1.3,0x05020c);
                n=Put(into,n,gx,gy+.7,gz,.3,.3,.3,0,0,0xf3e2ff);
                for(int i=0;i<10;i++){double a=DiscAngle(i,age),r=DiscRadius(i);n=Put(into,n,gx+Math.Cos(a)*r,gy+.7+Math.Sin(a*2)*.08,gz+Math.Sin(a)*r,.13,.13,.13,age*3,a,color);}
                return n;
            }
            bool mine=look==NativeFieldLook.Mine,implosion=look==NativeFieldLook.Implosion;double edge=mine?.5:BombEdge(age,u);
            n=Put(into,n,gx,gy+(mine?.1:.4),gz,edge,mine?.16:edge,edge,implosion?age*2.5:0,age*2,implosion?0x2a0f46:0x3a3434);
            if(FuseVisible(age,u,left))n=Put(into,n,gx,gy+(mine?.22:.66),gz,.1,.1,.1,0,0,0xffffff);
            if(implosion)for(int i=0;i<6;i++){double a=i*Math.PI/3+age*(6+u*10),r=OrbitRadius(u);n=Put(into,n,gx+Math.Cos(a)*r,gy+.4+Math.Sin(a+i)*.18,gz+Math.Sin(a)*r,.12,.12,.12,age*7,a,color);}
            return n;
        }

        /// <summary>The emissive weight of a bomb's band and fuse tip (see <see cref="NativeBombPose.Emissive"/>): 0 whenever the
        /// source's fuse cube is hidden; while it shows, a ramp from .15 (blink phase sin = -.2, where the cube appears) to 1
        /// (sin >= .3), scaled from 75% at birth to 100% at the end of the fuse.</summary>
        public static double BombGlow(double age,double u,double left)
        {
            if(!FuseVisible(age,u,left))return 0;double e=Smooth((Math.Sin(age*(6+u*20))+.2)/.5);
            return (.15+.85*e)*(.75+.25*u);
        }
        /// <summary>Gap (m) between the floor and the bottom of the modelled bomb's casing.</summary>
        public const double BombLift=.02;
        /// <summary>
        /// Where the modelled bomb stands (owner cut, review 2026-10-11): the field's floor (NativeBlastField.Floor) when the field
        /// is at most NativeBlastSim.NearGround above it, otherwise the blast emitters' own ground under the field
        /// (max(.03, y - .95), the source's), so a field without a known floor, high on a wall or over a pit keeps the source's
        /// height for its emitters.
        /// </summary>
        public static double BombBase(in NativeBlastField f)=>f.Floor.HasValue&&f.Y-f.Floor.Value<=NativeBlastSim.NearGround?f.Floor.Value:Math.Max(.03,f.Y-.95);
        /// <summary>The modelled bomb's fuse tip at rest scale (NativeVfxMeshes.BombTipY above a casing standing on BombBase):
        /// where the chip bomb's fuse sparks start (NativeBlastSim.Field).</summary>
        public static double BombTipY(in NativeBlastField f)=>BombBase(f)+NativeVfxMeshes.BombDiameter*.5+BombLift+NativeVfxMeshes.BombTipY;
        /// <summary>
        /// The modelled bomb's pose and emissive pulse for a Bomb, Implosion or Mine look at time (false, and a default pose,
        /// for any other look). Same timing as <see cref="Cubes"/>, which still plans the source's cubes for parity; the body
        /// stands on BombBase instead of the source's .4 m (mine .1 m) over the field origin.
        /// </summary>
        public static bool Bomb(NativeBlastField f,double time,out NativeBombPose pose)
        {
            pose=default;var look=LookOf(f.Kind,f.Fx);if(look!=NativeFieldLook.Bomb&&look!=NativeFieldLook.Implosion&&look!=NativeFieldLook.Mine)return false;
            double gx=f.X,gy=Origin(f.Y),gz=f.Z,age=time-f.Born,left=f.Until-time,u=Progress(f.Born,f.Until,time);bool mine=look==NativeFieldLook.Mine,implosion=look==NativeFieldLook.Implosion;
            var c=new NativeFieldCube();Euler(ref c,implosion?age*2.5:0,age*2);double s=mine?1:BombEdge(age,u)/.38,sy=mine?.16/.38:s;
            pose=new NativeBombPose{Look=look,X=gx,Y=BombBase(f)+BombLift+NativeVfxMeshes.BombDiameter*.5*sy,Z=gz,Sx=mine?.5/.38:s,Sy=sy,Sz=mine?.5/.38:s,Qx=c.Qx,Qy=c.Qy,Qz=c.Qz,Qw=c.Qw,
                FuseX=gx,FuseY=gy+(mine?.22:.66),FuseZ=gz,FuseLit=FuseVisible(age,u,left),Progress=u,BlinkRate=6+u*20,Age=age,Left=left,Emissive=BombGlow(age,u,left),Body=implosion?0x2a0f46:0x3a3434};
            return true;
        }

        // three.js CircleGeometry(1, segments) turned to face +Y (rotation.x = -PI/2): a centre vertex, then segments+1 rim vertices.
        private static NativeFieldMesh Fan(int segments,bool fade)
        {
            var p=new float[(segments+2)*3];var a=new float[segments+2];var t=new int[segments*3];a[0]=1;
            for(int s=0;s<=segments;s++){double th=(double)s/segments*Math.PI*2;p[(s+1)*3]=(float)Math.Cos(th);p[(s+1)*3+2]=(float)-Math.Sin(th);a[s+1]=fade?0:1;}
            for(int i=1;i<=segments;i++){t[(i-1)*3]=i;t[(i-1)*3+1]=i+1;t[(i-1)*3+2]=0;}
            return new NativeFieldMesh(p,a,t);
        }
        // three.js SphereGeometry(1, width, height, 0, 2PI, 0, PI/2): the upper hemisphere, no base cap.
        private static NativeFieldMesh Hemisphere(int width,int height)
        {
            var p=new float[(width+1)*(height+1)*3];var a=new float[(width+1)*(height+1)];int k=0;
            for(int y=0;y<=height;y++)for(int x=0;x<=width;x++){double phi=(double)x/width*Math.PI*2,th=(double)y/height*Math.PI/2;p[k*3]=(float)(-Math.Cos(phi)*Math.Sin(th));p[k*3+1]=(float)Math.Cos(th);p[k*3+2]=(float)(Math.Sin(phi)*Math.Sin(th));a[k++]=1;}
            var t=new System.Collections.Generic.List<int>();
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int ia=y*(width+1)+x+1,ib=y*(width+1)+x,ic=(y+1)*(width+1)+x,id=(y+1)*(width+1)+x+1;
                if(y!=0){t.Add(ia);t.Add(ib);t.Add(id);}
                t.Add(ib);t.Add(ic);t.Add(id);
            }
            return new NativeFieldMesh(p,a,t.ToArray());
        }
        // three.js RingGeometry(inner, outer, segments) turned to face +Y.
        private static NativeFieldMesh Annulus(double inner,double outer,int segments)
        {
            var p=new float[(segments+1)*2*3];var a=new float[(segments+1)*2];var t=new int[segments*6];
            for(int j=0;j<2;j++)for(int i=0;i<=segments;i++){double r=j==0?inner:outer,th=(double)i/segments*Math.PI*2;int v=j*(segments+1)+i;p[v*3]=(float)(r*Math.Cos(th));p[v*3+2]=(float)(-r*Math.Sin(th));a[v]=1;}
            for(int i=0;i<segments;i++){int ia=i,ib=i+segments+1,ic=i+segments+2,id=i+1;t[i*6]=ia;t[i*6+1]=ib;t[i*6+2]=id;t[i*6+3]=ib;t[i*6+4]=ic;t[i*6+5]=id;}
            return new NativeFieldMesh(p,a,t);
        }
        /// <summary>The edge-less glow disc: vertex alpha 1 at the centre and exactly 0 on the rim, scaled to the gameplay radius.</summary>
        public static readonly NativeFieldMesh GlowDisc=Fan(GlowSegments,true);
        /// <summary>The time/repulsion dome at the gameplay radius (front faces only, so it culls away from inside).</summary>
        public static readonly NativeFieldMesh Dome=Hemisphere(DomeWidth,DomeHeight);
        /// <summary>siren/fx.ts DISC (CircleGeometry 48) for the scorch decal and the eruption fill, and RING (.86-1, 64).</summary>
        public static readonly NativeFieldMesh SirenDisc=Fan(48,false),WarnRing=Annulus(.86,1,64);

        // ---- Siren floor telegraphs (siren/fx.ts updatePatches/updateEruptions). The flames, columns and drops are C12. ----
        public const int Scorch=0x000000,Warn=0xff2f44,WarnDeep=0x8a0d1c;
        public const double ScorchAlpha=.75,ScorchFalloff=1.6,ScorchLift=.025,WarnAlpha=.9,WarnFlicker=.15,FillAlpha=.35,RingLift=.06,FillLift=.05;
        /// <summary>Seconds from an eruption's cast to its burst (SIREN_TIMING eruption.warning; Unity: At + 1 = Next = Until - .5).</summary>
        public const double EruptionWarning=1;
        public static double Smooth(double x){x=Clamp01(x);return x*x*(3-2*x);}
        /// <summary>Scorch decal under a fire patch: grows in over .25 s to 1.25 R and is hidden in its last .8 s fade.</summary>
        public static bool ScorchDecal(double at,double until,double radius,double time,out double scale)
        {double fade=1-Smooth((time-(until-.8))/.8),grow=Smooth((time-at)/.25);scale=radius*1.25*Math.Max(.05,grow);return fade>.02;}
        /// <summary>Eruption warning: the pulsing ring (scale ring), the fill growing to R (scale fill), both until the burst.</summary>
        public static bool EruptionWarn(double at,double burstAt,double radius,double time,out double ring,out double fill)
        {
            double u=Clamp01((time-at)/Math.Max(.05,burstAt-at)),pulse=.5+.5*Math.Sin(time*(10+16*u));
            ring=radius*(1+.06*pulse);fill=Math.Max(.01,radius*u);return time<burstAt;
        }
        /// <summary>glowMaterial flicker .15 (materials.ts): multiplies the warn ring's colour.</summary>
        public static double Flicker(double time)=>1-WarnFlicker*(.5+.5*Math.Sin(time*37+Math.Sin(time*11)*3));
        /// <summary>
        /// What a Siren glowMaterial shows on screen, as sRGB 0..1: it is a ShaderMaterial without the output colour
        /// conversion, so the screen gets the colour's linear value times the intensity (the flicker) unencoded. The warn
        /// ring ff2f44 therefore shows as a deep red (1, .03, .06) and the fill 8a0d1c as (.25, .004, .012).
        /// </summary>
        public static void SirenDisplay(int hex,double intensity,out double r,out double g,out double b)
        {NativeBlastSim.Linear(hex,out r,out g,out b);r=Clamp01(r*intensity);g=Clamp01(g*intensity);b=Clamp01(b*intensity);}
        public static bool SirenPatch(string kind)=>kind=="hellfire"||kind=="trail"||kind=="scorch";

        // ---- Ability aim preview (ability-preview.ts): rising motes on the gameplay radius of point-targeted E abilities. ----
        public const int PreviewColor=0x8af4ff,PreviewCap=120;public const double PreviewAlpha=.85,PreviewSize=.14,GeyserSpacing=3,HoldAimSeconds=.2;
        /// <summary>
        /// previewOn (ability-hud.ts): the E slot is casting, or ready (a charge left, or no cooldown left) and affordable
        /// (only magic pilots pay mana; cost includes resourceCost); never with the Necronomicon (E raises the dead); and on a
        /// hold-aim E only once it has been held HOLD_AIM_SECONDS. On cooldown or without mana nothing is drawn.
        /// </summary>
        public static bool PreviewOn(bool casting,double cooldownLeft,bool magic,double mana,double cost,bool necronomicon,double heldFor)
        {
            if(necronomicon||!(heldFor>=HoldAimSeconds))return false;
            return casting||(!magic||mana>=cost)&&cooldownLeft<=0;
        }
        /// <summary>POINT: the point-targeted E abilities that preview; ground: the spells project onto the ground, the frag does not.</summary>
        public static bool Previews(string ability,out bool ground)
        {
            ground=ability!="frag-grenade";
            return ability=="frag-grenade"||ability=="meteor-strike"||ability=="geyser-wall"||ability=="singularity";
        }
        /// <summary>Motes for one circle, appended at used (x, y, z, scale per mote; the cube edge is PreviewSize * scale). A
        /// clamped aim (out of range or the sky) skips two motes in four. Returns the new count, capped at PreviewCap.</summary>
        public static int PreviewMotes(double x,double y,double z,double radius,bool clamped,double time,float[] into,int used)
        {
            int k=(int)Math.Min(40,Math.Max(14,NativeBlastSim.Round(radius*6)));
            for(int i=0;i<k&&used<PreviewCap;i++)
            {
                if(clamped&&i%4>1)continue;
                double a=((double)i/k+time*.05)*Math.PI*2,u=(time*.7+i*.37)%1.0;int w=used*4;
                into[w]=(float)(x+Math.Cos(a)*radius);into[w+1]=(float)(y+.05+u*.35);into[w+2]=(float)(z+Math.Sin(a)*radius);into[w+3]=(float)(1-u*.75);used++;
            }
            return used;
        }
    }
}
