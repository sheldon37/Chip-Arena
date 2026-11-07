using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Siren's entrance face beyond the pinned source (owner request 2026-10-10: "make the smile animation on intro more
    /// realistic"). client/siren/intro.ts shows the grin at the 'smile' cue and scales it in x from ~0 to 1 over .55 s, a line
    /// that stretches; the entrance bake also lost the blank oval stare (it held the eyes' last brow cut). This is the acted
    /// smile, drawn natively on the baked head track: a closed curved seam that pulls into the grin corner by corner (the
    /// Siren's left leads, a smirk that resolves by .78 s), the lips lift and part, the 25 fangs bare from the centre out, the
    /// jaw drops and settles, the eyes narrow from below as the cheeks push up, the fangs and eye halos flare and a silent
    /// laugh twitches the jaw and cheeks on beats 15 and 16 of the boss song; then the held grin strains (a 4.1 Hz corner
    /// quiver) and creeps wider. Before the cue: the blank oval stare, its halos at .35 under the bulb and .6 in the blackout.
    /// Every value is a pure function of the intro age and the song's beat table (s = age - beat 14, NativeSirenIntroLight
    /// .SmileBeat): no cue, duration or clock changes. tools/siren_smile.mjs holds the same rules (and the body layer the
    /// entrance bake carries); Tests/SirenSmileFaceChecks.cs holds the two together. Evaluate allocates nothing.
    /// Frame: head-local source frame (three.js, right-handed, +x the Siren's left), row-major for column vectors, 16 values
    /// per piece, as NativeSirenLook. Pieces: 13 upper then 12 lower fangs (the unit fang mesh is FangOutline in (u, v, 0),
    /// the lower row's with v negated, so every fang's transform keeps a positive determinant and one cull mode serves the
    /// instanced and the per-draw paths), 2 eyes and 2 eye halos (side +1, -1), the 4 cheek pieces (NativeSirenDetail.CheekPiece order).
    /// </summary>
    public sealed class NativeSirenSmileFace
    {
        public const int UpperTeeth=13,LowerTeeth=12,Teeth=UpperTeeth+LowerTeeth,CheekPieces=4;
        /// <summary>Seconds after the cue: beat 15 (the 'name' cue), beat 16, the overlay's end and the stage hold's end.</summary>
        public const double Name=.4179,Beat16=.8359,End=1.2596,HoldEnd=3.0596;
        /// <summary>The closed seam: half-width, upper and lower lip arcs (y = c + k x^2), the fangs' reach fraction.</summary>
        public const double ClosedW=.104,ClosedUy=.0535,ClosedUk=1.3,ClosedLy=.0455,ClosedLk=1.3,ClosedReach=.65;
        /// <summary>The rest mouth: the arena grin (tools/siren_design.mjs GRIN, baked into the bank's siren-teeth parts).</summary>
        public const double RestW=.146,RestUy=.08,RestUk=2.6,RestLy=.018,RestLk=4.6;
        /// <summary>The grin's origin on the head and the eyes' and halos' rest placement (buildSiren).</summary>
        public const double MouthZ=.1638,EyeX=.082,EyeY=.206,EyeZ=.1645,EyeTilt=.12,EyeW=.05,EyeH=.08,EyeLift=.0216,HaloX=.079,HaloY=.21,HaloZ=.2,HaloSize=.26;
        /// <summary>The eye halos before the cue: .35 under the bulb, .6 from the blackout (intro.ts).</summary>
        public const double HaloLit=.35,HaloDark=.6;
        /// <summary>The canonical fang's inset (tools/siren_design.mjs GRIN.inset); its gaps on the closed seam and once bared.</summary>
        public const double FangInset=.085,SeamInset=.02,BaredInset=.15;
        /// <summary>
        /// The jaw per unit open: the upper row lifts JawLift; the lower row stretches JawStretchX wide and JawStretchY tall about the
        /// grin's origin and drops JawDrop. applySirenLook's arena jaw stretches the lower row .5 tall and drops it .03, which keeps
        /// the interlocking rows overlapped (a white ribbon cut by slivers); the entrance's jaw drops the row away from the upper
        /// one instead, so a dark zigzag opens between the fangs as the jaw falls and stays through the hold.
        /// </summary>
        public const double JawLift=.006,JawStretchX=.06,JawStretchY=.2,JawDrop=.08;
        /// <summary>
        /// The bite (channel Bite, 0 to 1 with the reveal): the bank grin's interlocking rows reach .74 to 1 and .68 to .9 of the
        /// lip gap, so fully bared they tile the mouth into a white ribbon cut by slivers. The entrance trims the upper row's reach
        /// by BiteUpper and the lower's by BiteLower and grows the fangs' gap to BiteInset, so parallel dark bands open between
        /// the fangs (the design target's zigzag). With Bite 0 (the closed seam, and any face that never sets it) the fangs are
        /// the bank grin's.
        /// </summary>
        public const double BiteUpper=.1,BiteLower=.25,BiteInset=.15;
        /// <summary>The fangs' reach per slot (tools/siren_design.mjs UPPER_REACH, LOWER_REACH): two longer fangs per row.</summary>
        public static readonly double[] UpperReach={.74,.84,.92,1.0,.86,.8,.76,.8,.86,.98,.92,.84,.74},LowerReach={.7,.78,.9,.76,.72,.68,.68,.72,.76,.9,.78,.7};
        /// <summary>The canonical fang outline (u, v): root u 0..1 at v 0, tip (.5, -1), before any inset; and its 5 fan triangles.</summary>
        public static readonly double[] Fang={0,0, 1,0, .9,-.32, .7,-.7, .5,-1, .33,-.66, .12,-.3};
        public static readonly int[] FangFan={0,1,2, 0,2,6, 2,5,6, 2,3,5, 3,4,5};
        /// <summary>The outline's vertex mean (the inset's centre).</summary>
        public static readonly double FangCentreU=Mean(0),FangCentreV=Mean(1);
        private static double Mean(int k){double s=0;for(int i=k;i<Fang.Length;i+=2)s+=Fang[i];return s/(Fang.Length/2);}
        /// <summary>
        /// The unit fang mesh's vertices (u, v): the outline inset by FangInset toward its centre (the bank's fangs); for the lower
        /// row (upper false) v is negated (its tips point up, and its transform maps v back, keeping a positive determinant).
        /// </summary>
        public static void FangOutline(double[] uv,bool upper=true)
        {double sv=upper?1:-1;for(int i=0;i<Fang.Length;i+=2){uv[i]=Fang[i]+(FangCentreU-Fang[i])*FangInset;uv[i+1]=sv*(Fang[i+1]+(FangCentreV-Fang[i+1])*FangInset);}}
        /// <summary>intro.ts BEATS (the boss song's first 18 beats): the cue table when no song beat table is at hand.</summary>
        public static readonly double[] IntroBeats={.1858,.6037,1.0159,1.428,1.8402,2.2523,2.6645,3.0883,3.5004,3.9184,4.3421,4.7543,5.178,5.596,6.0082,6.4261,6.8441,7.2678};

        // ---------------------------------------------------------------- this frame
        /// <summary>Intro age and seconds since the cue.</summary>
        public double Age,S;
        /// <summary>True from the cue while the seam has any light (the grin is hidden before the cue).</summary>
        public bool Grin;
        /// <summary>The seam's flicker in (0..1) and the fangs' glow multiplier (the flicker times the beat kicks and the laugh).</summary>
        public double Vis,Glow;
        /// <summary>Mouth channels: half-widths (WPos the Siren's left), lip arcs, the smirk's tilt, jaw open; the laugh and the hold's strain.</summary>
        public double WPos,WNeg,Uy,Uk,Ly,Lk,Tilt,Open,Chuckle,Strain;
        /// <summary>The bite: the reach trim and wider gaps of the bared entrance fangs (BiteUpper, BiteLower, BiteInset); 0 is the bank grin.</summary>
        public double Bite;
        /// <summary>Eyes: brow cut (anger), squint, the outline index (NativeSirenLook.Shape), the halos' opacity; cheek lift.</summary>
        public double Anger,Squint,HaloOpacity,Cheek;public int Shape;
        /// <summary>
        /// The halos' opacity for Unity's linear additive blend: three adds the sprite to the sRGB-encoded canvas, so over the
        /// black cell the source adds what a linear blend adds at the decoded opacity (as NativeSirenIntroLight.HaloLinear does
        /// for the bulb); added at the raw .6 the halos would bloom over the whole face.
        /// </summary>
        public double HaloAlpha;
        /// <summary>
        /// The halos' width in the linear blend: the radial falloff (1 - r)^1.6 added in linear light and then encoded spreads
        /// much wider than the source's display-space blend; at .62 of the sprite's width it lands within a few percent of the
        /// source's falloff over the bright core (r &lt; .45), so the halos frame the eyes instead of washing over the grin.
        /// </summary>
        public const double HaloSpread=.62;
        /// <summary>
        /// The body layer the entrance bake carries (radians added to intro.ts's pose, whose own smile dip of .1 pitch and .06 roll
        /// it replaces: tools/siren_smile.mjs stagePose; zero before the cue).
        /// </summary>
        public double HeadX,HeadY,HeadZ,NeckX,ChestX,Clavicle;
        /// <summary>The couture glow lines' multipliers through the entrance (tools/siren_design.mjs entranceGlow, baked).</summary>
        public double Seam,Filament;
        /// <summary>Each fang's reach fraction of the rest table (13 upper then 12 lower).</summary>
        public readonly double[] Reach=new double[Teeth];
        /// <summary>Head-local transforms: fangs, eyes, eye halos, cheeks (16 values per piece).</summary>
        public readonly double[] Tooth=new double[Teeth*16],Eye=new double[2*16],Halo=new double[2*16],Cheeks=new double[CheekPieces*16];

        // ---------------------------------------------------------------- easings
        private static double Clamp(double x,double a=0,double b=1)=>x<a?a:x>b?b:x;
        private static double Lerp(double a,double b,double t)=>a+(b-a)*t;
        public static double Smooth(double x){x=Clamp(x);return x*x*(3-2*x);}
        public static double OutCubic(double x){x=Clamp(x);double u=1-x;return 1-u*u*u;}
        public static double OutQuad(double x){x=Clamp(x);double u=1-x;return 1-u*u;}
        public static double InOutSine(double x){x=Clamp(x);return .5-.5*Math.Cos(Math.PI*x);}
        public static double InOutCubic(double x){x=Clamp(x);if(x<.5)return 4*x*x*x;double u=-2*x+2;return 1-u*u*u/2;}
        /// <summary>A sharp-attack, soft-release bump on (0, 1).</summary>
        public static double FBump(double x)=>x<=0||x>=1?0:Math.Sin(Math.PI*x)*(1-x)*1.6;
        /// <summary>sin^2 window over [a, b].</summary>
        public static double Bump(double s,double a,double b){double x=Math.Sin(Math.PI*Clamp((s-a)/(b-a)));return x*x;}
        /// <summary>A glow kick at t0 of height a: 30 ms rise, .12 s decay.</summary>
        public static double Kick(double s,double t0,double a)=>s<t0?0:a*Math.Exp(-(s-t0)/.12)*Clamp((s-t0)/.03);
        /// <summary>The silent laugh: one bump on each of beats 15 and 16.</summary>
        public static double Laugh(double s)=>FBump((s-Name)/.2)+FBump((s-Beat16)/.2);
        /// <summary>
        /// The fang's scale about its outline centre for reach fraction r and bite b: 1 bared without a bite (the canonical fang),
        /// wider on the seam so the closed teeth touch, narrower as the bite opens the gaps (inset FangInset to BiteInset).
        /// </summary>
        public static double FangScale(double r,double b=0){double q=Clamp((.86*r-.56)/.3),full=FangInset+(BiteInset-FangInset)*Clamp(b),inset=full*(SeamInset+(BaredInset-SeamInset)*q)/BaredInset;return (1-inset)/(1-FangInset);}
        /// <summary>The smile cue (beat 14) and the blackout (beat 13) of a beat table (IntroBeats when it has none).</summary>
        public static double Cue(double[] beats)=>NativeSirenIntroLight.Cue(beats!=null&&beats.Length>NativeSirenIntroLight.SmileBeat?beats:IntroBeats,NativeSirenIntroLight.SmileBeat);
        public static double Blackout(double[] beats)=>NativeSirenIntroLight.Cue(beats!=null&&beats.Length>NativeSirenIntroLight.SmileBeat?beats:IntroBeats,NativeSirenIntroLight.BlackoutBeat);

        /// <summary>The face at intro `age` for the song's beat table (null: IntroBeats).</summary>
        public NativeSirenSmileFace Evaluate(double age,double[] beats)
        {
            double cue=Cue(beats),blackout=Blackout(beats),s=age-cue;Age=age;S=s;
            double ch=Laugh(s),strain=Smooth((s-.75)/.35),quiver=Math.Sin(2*Math.PI*4.1*s),creep=Smooth((s-1)/2),over=.006*Bump(s,.40,.66);Chuckle=ch;Strain=strain;
            // 1. The seam flickers in: .65 at 20 ms, down to .25 at 35 ms, full at 60 ms.
            Vis=s<0?0:s<.02?.65*Clamp(s/.02):s<.035?.65-.4*Clamp((s-.02)/.015):s<.06?.25+.75*Clamp((s-.035)/.025):1;
            // 2. The corners pull (the Siren's left leads by .09 s), overshoot, creep and the left corner's strain quiver; the lips lift.
            WPos=(Lerp(ClosedW,RestW,OutCubic((s-.04)/.42))+over)*(1+.05*creep)*(1+.01*strain*quiver);
            WNeg=(Lerp(ClosedW,RestW,OutCubic((s-.13)/.42))+over)*(1+.05*creep);
            double lift=OutCubic((s-.06)/.44);Uy=Lerp(ClosedUy,RestUy,lift);Uk=Lerp(ClosedUk,RestUk,lift)+.25*creep;Ly=Lerp(ClosedLy,RestLy,lift);Lk=Lerp(ClosedLk,RestLk,lift);
            Tilt=.065*Bump(s,.06,.78)+.006*strain*quiver;
            // 3. The fangs bare from the centre out, 24 ms per tooth; the lower row from .23 s.
            for(int i=0;i<UpperTeeth;i++)Reach[i]=Lerp(ClosedReach,1,OutCubic((s-.16-.024*Math.Abs(i-6))/.34));
            for(int i=0;i<LowerTeeth;i++)Reach[UpperTeeth+i]=Lerp(ClosedReach,1,OutCubic((s-.23-.024*Math.Abs(i-5.5))/.34));
            // The bite comes in with the reveal (the closed seam has none), so the bared fangs part into a zigzag.
            Bite=OutCubic((s-.16)/.55);
            // 4. The jaw: about .26 near .5 s, settling near .12 with a slow breath, twitching open on each chuckle.
            Open=Math.Max(0,.26*OutCubic((s-.2)/.3)-.14*InOutSine((s-.5)/.45)+.012*Math.Sin(2.2*Math.Max(0,s-.95))+.02*ch);
            // 5. Eyes: the brow cut descends, the lower edge rises; the halos swell and kick on the name beat.
            Anger=.9*InOutCubic((s-.08)/.42);Squint=.5*OutQuad((s-.14)/.40)-.1*InOutSine((s-.6)/.4);Shape=NativeSirenLook.Shape(Anger);
            HaloOpacity=age<blackout?HaloLit:HaloDark+.12*Bump(s,.2,.75)+.6*Kick(s,Name,.12);HaloAlpha=NativeEntranceLight.ToLinear(Math.Min(1,HaloOpacity));
            // 6. The cheeks lift and twitch with the laugh.
            Cheek=Clamp(OutCubic((s-.08)/.55)+.15*ch,0,1.15);
            // 7. The body layer (baked into the entrance stage by tools/export_cinematics.mjs).
            double dip=InOutSine((s-.10)/.52),rise=InOutSine((s-.75)/.65);
            HeadX=-.035*Bump(s,0,.18)+.15*dip-.07*rise+.03*ch;HeadY=-.06*InOutSine((s-.1)/.6);HeadZ=.08*InOutSine((s-.12)/.58)+.03*Smooth((s-1)/2)-.012*ch;
            NeckX=.35*(.15*dip-.07*rise);ChestX=-.045*Bump(s,.04,.62)+.02*ch;Clavicle=.07*Bump(s,.06,.66)+.04*ch;
            // 8. The fangs flare on beats 15 and 16; the grin shows from the cue while the seam has light.
            Glow=Vis*(1+Kick(s,Name,.28)+Kick(s,Beat16,.12)+.08*ch);Grin=s>=0&&Vis>0;
            // The couture glow lines (seams and nimbus filament) through the blackout and the smile.
            if(age<blackout){Seam=Filament=1;}
            else{double nameAt=NativeSirenIntroLight.Cue(beats!=null&&beats.Length>NativeSirenIntroLight.SmileBeat+1?beats:IntroBeats,NativeSirenIntroLight.SmileBeat+1)-cue,kindle=s>=nameAt?.45*Math.Exp(-(s-nameAt)/.5)*Math.Min(1,(s-nameAt)/.04):0;Seam=.08;Filament=Math.Min(1,.14+kindle+.16*Smooth((s-.6)/.8));}
            // Pieces.
            for(int i=0;i<Teeth;i++)ToothMatrix(i<UpperTeeth,i<UpperTeeth?i:i-UpperTeeth,Reach[i],Tooth,i*16);
            for(int k=0;k<2;k++)
            {
                double side=k==0?1:-1;
                Trz(Eye,k*16,side*EyeX,EyeY+EyeLift*Squint,EyeZ,side*EyeTilt,EyeW*(1+.06*Open),EyeH*(1-.3*Squint));
                Trz(Halo,k*16,side*HaloX,HaloY,HaloZ,0,HaloSize,HaloSize);
            }
            NativeSirenDetail.Cheeks(Cheek,Cheeks,0);
            return this;
        }
        /// <summary>
        /// One fang (grin-local, before the jaw): root chord A->B on its row's arc and the tip, for this frame's mouth and reach
        /// fraction r. tools/siren_design.mjs tooth() generalised to asymmetric half-widths, the smirk's tilt, the reveal and the
        /// bite; at the rest mouth without a bite it is that tooth exactly (the arena grin).
        /// </summary>
        public void FangTriangle(bool upper,int i,double r,out double ax,out double ay,out double bx,out double by,out double tx,out double ty)
        {
            int n=upper?UpperTeeth:LowerTeeth;double step=2.0/n,a=-1+(upper?i:i+.5)*step,b=a+step,m=(a+b)/2;
            ax=X(a);bx=X(b);double xm=X(m),lean=-.22*m*(bx-ax),bite=Clamp(Bite),reach=r*(upper?UpperReach[i]*(1-BiteUpper*bite):LowerReach[i]*(1-BiteLower*bite)),gap=U(xm)-L(xm);
            if(upper){ay=U(ax);by=U(bx);tx=xm+lean;ty=U(xm)-gap*reach;}else{ay=L(ax);by=L(bx);tx=xm+lean;ty=L(xm)+gap*reach;}
        }
        private double X(double u)=>u<0?u*WNeg:u*WPos;
        private double U(double x)=>Uy+Uk*x*x+Tilt*x;
        private double L(double x)=>Ly+Lk*x*x+Tilt*x;
        /// <summary>
        /// The unit fang mesh onto fang i of a row for this frame's mouth channels (WPos .. Lk, Tilt, Open) and reach fraction r,
        /// into m at o: P = A + u(B - A) + vD (D = A + (B - A)/2 - tip; the lower row's mesh holds -v, so its v column is -D),
        /// scaled about the outline centre by FangScale, then the jaw (upper up JawLift open; lower x(1 + JawStretchX open),
        /// y(1 + JawStretchY open) - JawDrop open) and the grin's origin.
        /// </summary>
        public void ToothMatrix(bool upper,int i,double r,double[] m,int o)
        {
            FangTriangle(upper,i,r,out double ax,out double ay,out double bx,out double by,out double tx,out double ty);
            double ux=bx-ax,uy=by-ay,dx=ax+.5*ux-tx,dy=ay+.5*uy-ty,k=FangScale(r,Bite),sv=upper?1:-1;
            m[o]=ux*k;m[o+1]=sv*dx*k;m[o+2]=0;m[o+3]=ax+(1-k)*(FangCentreU*ux+FangCentreV*dx);
            m[o+4]=uy*k;m[o+5]=sv*dy*k;m[o+6]=0;m[o+7]=ay+(1-k)*(FangCentreU*uy+FangCentreV*dy);
            m[o+8]=0;m[o+9]=0;m[o+10]=1;m[o+11]=MouthZ;m[o+12]=0;m[o+13]=0;m[o+14]=0;m[o+15]=1;
            if(upper)m[o+7]+=JawLift*Open;
            else{double sx=1+JawStretchX*Open,sy=1+JawStretchY*Open;for(int c=0;c<4;c++){m[o+c]*=sx;m[o+4+c]*=sy;}m[o+7]-=JawDrop*Open;}
        }
        // T·Rz·S (three.js Matrix4.compose with only a z rotation), z scale 1.
        private static void Trz(double[] m,int o,double px,double py,double pz,double rz,double sx,double sy)
        {
            double c=Math.Cos(rz),s=Math.Sin(rz);
            m[o]=c*sx;m[o+1]=-s*sy;m[o+2]=0;m[o+3]=px;m[o+4]=s*sx;m[o+5]=c*sy;m[o+6]=0;m[o+7]=py;m[o+8]=0;m[o+9]=0;m[o+10]=1;m[o+11]=pz;m[o+12]=0;m[o+13]=0;m[o+14]=0;m[o+15]=1;
        }
        /// <summary>The determinant of fang i's transform in the mouth plane (positive for every fang: no mirrored instance).</summary>
        public double FangDeterminant(int i){int o=i*16;return Tooth[o]*Tooth[o+5]-Tooth[o+1]*Tooth[o+4];}
    }
}
