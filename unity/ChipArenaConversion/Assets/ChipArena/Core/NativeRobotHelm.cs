using System;
using System.Numerics;
using Kit=ChipArena.UnityConversion.NativeModelHatMeshes.Kit;
using Paint=ChipArena.UnityConversion.NativeModelHatMeshes.Paint;

namespace ChipArena.UnityConversion
{
    // The robot's helmet and half mask (owner 2026-10-30: "Can we develop essentially a full helmet and a half mask similar to
    // what we had on voxel?"): the original voxel robot's head (its rounded box, the glowing visor bar across it, the three
    // grille slots under that, the jaw below, the square ear modules with their light) made for the model's head in the model's
    // smooth style. They are face gears (the skin map's "face" entry: NativeWeaponSkins FaceHelm, FaceHalf; Runtime/
    // NativeRobotHelmPresentation.cs), the helmet the model's default with the robot suit (owner 2026-10-30: "I think we will make
    // the default outfit the new designed armor and that full helmet"). Built in the head bone's frame (x across, y up, z ahead, as
    // the model hats are) round the model's head as measured in Unity (2026-10-30; its head, eyes, lashes and brows: the ears
    // 10.8 cm to each side, the nose tip 15.1 cm ahead, the back of the skull 8.4 cm behind, the crown 21.2 cm up, the chin's
    // foot 4.5 cm down).
    // - The helmet: a rounded shell, superelliptic round the head and up it (squarer under the middle, for the robot's jaw), a
    //   centimetre or so over all of that and open under the jaw for the neck. Over the eyes a recessed visor of accent glass in
    //   a dark frame; under it three dark grille slots; a seam from the chin up to each ear module; vents at the back; a dark
    //   seal round its foot. It hides the hair (Runtime VisualHatHair) and carries the pilot's hat on
    //   its shell (HatFit).
    // - The half mask: a plate over the nose, mouth and chin (elliptic sections fitted per height, 7 mm or more off the face),
    //   curling under the chin, its wings running back to an ear module over each ear; the grille slots across the mouth and a
    //   light on each cheek.
    // Their shells take the pilot's body colour as the robot suit's plates do (Core/NativeRobotSuit's paints), their light its
    // accent. Presentation only.
    public static class NativeRobotHelm
    {
        /// <summary>The key the hair is held under while the helmet is worn (Runtime VisualPressedHair: hidden).</summary>
        public const string Hair="+helm";
        private static readonly Paint Slot=new Paint(NativeRobotSuit.Screen),Glass=Paint.Lit(1.6f,.4f);
        private static float Smooth(float t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
        private static float Pow(float a,float e)=>MathF.Pow(MathF.Abs(a),e);
        private static float D(float degrees)=>degrees*MathF.PI/180;

        // ---- the helmet's shell ----
        /// <summary>The shell's mid surface: its axis ahead of the head bone, the height of its widest section, its reach there to
        /// each side, ahead and behind, its top and the reach of its lower half below the middle; its exponents round the head
        /// ahead (N: the face flatter) and behind (Nb: rounder, as a hat's band is), up its upper half (M) and down its lower half
        /// (Ml, squarer: the jaw); its thickness.</summary>
        public const float AxisZ=.033f,Mid=.07f,Side=.121f,Ahead=.13f,Behind=.134f,Crown=.228f,Under=.145f,N=2.7f,Nb=2.1f,M=2.5f,Ml=3.2f,Thick=.006f;
        /// <summary>The shell's mid surface at bearing `ph` (0 ahead, toward +x) and latitude `ps` (-pi/2 under .. pi/2 its top).</summary>
        public static Vector3 Shell(float ph,float ps)
        {
            float s=MathF.Sin(ps),c=MathF.Cos(ps),m=s>0?M:Ml,r=Pow(c,2/m),h=MathF.Sign(s)*Pow(s,2/m);
            float n=MathF.Cos(ph)>0?N:Nb,sx=MathF.Sign(MathF.Sin(ph))*Pow(MathF.Sin(ph),2/n),sz=MathF.Sign(MathF.Cos(ph))*Pow(MathF.Cos(ph),2/n);
            return new Vector3(Side*r*sx,Mid+(s>0?Crown-Mid:Under)*h,AxisZ+(sz>0?Ahead:Behind)*r*sz);
        }
        /// <summary>The latitude at which the shell stands at height y (the same at every bearing).</summary>
        public static float Latitude(float y)
        {
            bool up=y>Mid;float h=Math.Clamp((y-Mid)/(up?Crown-Mid:Under),-1,1);return MathF.Asin(MathF.Sign(h)*Pow(h,(up?M:Ml)/2));
        }
        /// <summary>The shell's foot (its lower edge's height) at bearing `ph`: under the jaw ahead, rising round the sides to the nape.</summary>
        public static float Foot(float ph)=>-.0415f-.0265f*MathF.Cos(ph);
        /// <summary>How far the shell's section at height y reaches to each side.</summary>
        public static float Width(float y)=>Shell(MathF.PI/2,Latitude(y)).X;
        /// <summary>Whether a point is inside the shell's outer face grown by `grow` (its implicit form: under 1 inside).</summary>
        public static float Inside(Vector3 p,float grow=0)
        {
            float a=Side+Thick*.5f+grow,dz=p.Z-AxisZ,f=(dz>0?Ahead:Behind)+Thick*.5f+grow,dy=p.Y-Mid,h=(dy>0?Crown-Mid:Under)+Thick*.5f+grow,m=dy>0?M:Ml,n=dz>0?N:Nb;
            return MathF.Pow(MathF.Pow(Pow(p.X/a,n)+Pow(dz/f,n),m/n)+Pow(dy/h,m),1/m);
        }
        private static Vector3 HelmAt(float ph,float y)=>Shell(ph,Latitude(y));
        // The bearing at which the shell's front (or, behind, its back) reaches x across at height y.
        private static float Across(float x,float y,bool behind=false){float ph=MathF.Asin(Math.Min(1,Pow(x/Width(y),(behind?Nb:N)/2)));return behind?MathF.PI-ph:ph;}

        // ---- the half mask's plate ----
        /// <summary>The plate's mid surface: elliptic sections round an axis at z HalfZ, by height (HalfY), each its half width
        /// (HalfA) and its reach ahead (HalfF); fitted to the face measured per height with 9.5 mm to its mid surface; its bearings'
        /// reach (to an ear module either side) and thickness.</summary>
        public const float HalfZ=.02f,HalfReach=96,HalfThick=.005f;
        private static readonly float[] HalfY={-.065f,-.055f,-.045f,-.035f,-.025f,-.015f,-.005f,.005f,.015f,.025f,.035f,.045f,.055f,.065f,.075f};
        private static readonly float[] HalfA={.062f,.066f,.070f,.074f,.078f,.082f,.087f,.091f,.095f,.099f,.105f,.111f,.120f,.125f,.131f};
        private static readonly float[] HalfF={.078f,.092f,.108f,.117f,.121f,.124f,.128f,.132f,.136f,.140f,.143f,.141f,.134f,.126f,.118f};
        // A smooth curve through a table's values (Catmull-Rom; the ends held), so the plate shades without creases at its rows.
        private static float Spline(float[] v,float y)
        {
            int n=HalfY.Length,i=0;while(i<n-2&&y>HalfY[i+1])i++;float t=Math.Clamp((y-HalfY[i])/(HalfY[i+1]-HalfY[i]),0,1);
            float p0=v[Math.Max(0,i-1)],p1=v[i],p2=v[i+1],p3=v[Math.Min(n-1,i+2)];
            return .5f*(2*p1+(p2-p0)*t+(2*p0-5*p1+4*p2-p3)*t*t+(3*p1-p0-3*p2+p3)*t*t*t);
        }
        /// <summary>The plate's mid surface at bearing `ph` (its section's angle, 0 ahead) and height y.</summary>
        public static Vector3 Plate(float ph,float y)=>new Vector3(Spline(HalfA,y)*MathF.Sin(ph),y,HalfZ+Spline(HalfF,y)*MathF.Cos(ph));
        /// <summary>The plate's top edge at bearing `ph`: under the eyes, over the bridge of the nose, rising past the eyes' outer
        /// corners to the ear modules.</summary>
        public static float HalfTop(float ph){float a=MathF.Abs(ph)*180/MathF.PI;return .040f+.008f*MathF.Exp(-a*a/144)+.028f*Smooth((a-55)/35);}
        /// <summary>The plate's foot at bearing `ph`: under the chin ahead, along the jaw to the ear modules.</summary>
        public static float HalfFoot(float ph)=>-.062f+.09f*Smooth(MathF.Abs(ph)*180/MathF.PI/100);
        /// <summary>The ear modules' centre (+x side; the other mirrored) and half sizes: the helmet's on its sides, the half mask's
        /// over the ears.</summary>
        public static readonly Vector3 HelmEar=new Vector3(Side+Thick*.5f+.011f,.062f,.028f),HalfEar=new Vector3(.121f,.05f,.012f),EarHalf=new Vector3(.011f,.03f,.026f);

        // ---- the hat on the helmet ----
        /// <summary>A hat on the helmet: the hat made for the head under it, scaled about HatPivot and lifted (ahead too, as the
        /// helmet stands further ahead of the face than behind the head), each hat by its own measure (the closed hats about an
        /// eighth larger and 2 cm up, the circlet, the antenna's band, the cone and the halo up), so it clears the shell by 2 mm
        /// or more (measured against the shell's implicit form; a cone's elastic runs into it).</summary>
        public static readonly Vector3 HatPivot=new Vector3(0,.03f,.025f);
        private static readonly string[] FitHats={"cap","beanie","top","wizard","cowboy","crown","cone","antenna","halo"};
        private static readonly float[] FitScale={1.12f,1.14f,1.14f,1.14f,1.13f,1.01f,1.04f,1.04f,1},FitLift={.02f,.02f,.02f,.02f,.02f,.012f,.02f,.02f,.02f},FitAhead={.008f,.008f,.004f,.004f,.008f,.004f,.008f,0,0};
        /// <summary>Hat `hat`'s fit on the helmet: a point p of it (head bone frame) goes to p * scale + move (a hat not made for
        /// the model takes the closed hats' fit).</summary>
        public static void HatFit(string hat,out float scale,out Vector3 move)
        {
            int i=Math.Max(0,Array.IndexOf(FitHats,hat));scale=FitScale[i];move=HatPivot*(1-scale)+new Vector3(0,FitLift[i],FitAhead[i]);
        }
        public static Vector3 HatOn(string hat,Vector3 p){HatFit(hat,out float k,out var move);return p*k+move;}

        // ---- building ----
        // The outward normal of surface `f` (by bearing and height) at (ph, y), away from the vertical axis at z `axis`.
        private static Vector3 NormalOf(Func<float,float,Vector3> f,float ph,float y,float axis)
        {
            const float e=.002f,ey=.001f;var p=f(ph,y);var n=Vector3.Cross(f(ph+e,y)-f(ph-e,y),f(ph,y+ey)-f(ph,y-ey));var o=new Vector3(p.X,0,p.Z-axis);
            if(n.LengthSquared()<1e-16f)n=o;n=Vector3.Normalize(n);return Vector3.Dot(n,o)<0?-n:n;
        }
        // A strip laid on surface `f` (by bearing and height): bearings ph0..ph1, heights y0(ph)..y1(ph), standing `lift` off it
        // and `thick` deep, painted `paint`.
        private static void Band(Kit k,SheldonSection s,Func<float,float,Vector3> f,float axis,float ph0,float ph1,Func<float,float> y0,Func<float,float> y1,float lift,float thick,int nu,int nv,Paint paint)
        {
            float Ph(float u)=>ph0+(ph1-ph0)*u;float Y(float u,float v)=>y0(Ph(u))+(y1(Ph(u))-y0(Ph(u)))*v;
            Vector3 Nn(float u,float v)=>NormalOf(f,Ph(u),Y(u,v),axis);
            k.Slab(s,nu,nv,false,thick,(u,v)=>f(Ph(u),Y(u,v))+Nn(u,v)*(lift+thick*.5f),Nn,(u,v)=>paint,(u,v)=>paint);
        }
        private static void Band(Kit k,SheldonSection s,Func<float,float,Vector3> f,float axis,float ph0,float ph1,float y0,float y1,float lift,float thick,int nu,int nv,Paint paint)=>
            Band(k,s,f,axis,ph0,ph1,_=>y0,_=>y1,lift,thick,nu,nv,paint);
        // An ear module either side (centre `c` on the +x side, mirrored): its shell plate, a dark screen on its outer face with a
        // light in its upper front corner, as the robot's (and the robot suit's) modules.
        private static void Ears(Kit k,SheldonSection s,Vector3 c)
        {
            foreach(float side in new[]{-1f,1f})
            {
                var at=new Vector3(side*c.X,c.Y,c.Z);var ax=new Vector3(side,0,0);
                NativeRobotSuit.Box(k,s,at,ax,Vector3.UnitY,Vector3.UnitZ,EarHalf,f=>f==0?NativeRobotSuit.Plate(1):NativeRobotSuit.Plate(.82f));
                NativeRobotSuit.Box(k,s,at+ax*(EarHalf.X+.0008f),ax,Vector3.UnitY,Vector3.UnitZ,new Vector3(.0012f,EarHalf.Y*.68f,EarHalf.Z*.66f),f=>Slot);
                NativeRobotSuit.Box(k,s,at+ax*(EarHalf.X+.0022f)+new Vector3(0,EarHalf.Y*.3f,EarHalf.Z*.3f),ax,Vector3.UnitY,Vector3.UnitZ,new Vector3(.0008f,.0058f,.0058f),f=>NativeRobotSuit.Light);
            }
        }

        /// <summary>The helmet's parts (one, on the head).</summary>
        public static SheldonSection[] Helm()
        {
            var k=new Kit();var s=k.Part("helmet");var centre=new Vector3(0,Mid,AxisZ);
            float Ps(float ph,float v){float foot=Latitude(Foot(ph));return foot+(MathF.PI/2-foot)*v;}
            k.Slab(s,56,22,true,Thick,(u,v)=>Shell(u*MathF.PI*2,Ps(u*MathF.PI*2,v)),(u,v)=>Shell(u*MathF.PI*2,Ps(u*MathF.PI*2,v))-centre,(u,v)=>NativeRobotSuit.Plate(1),(u,v)=>NativeRobotSuit.Metal(.45f));
            float face=Thick*.5f;
            // The visor: the glass over the eyes (y .047-.083, 9.8 cm to each side), a dark frame standing 2.5 mm proud round it.
            const float g0=.047f,g1=.083f,f0=.04f,f1=.09f;float gw=Across(.098f,.065f),fw=Across(.106f,.065f);
            Band(k,s,HelmAt,AxisZ,-gw,gw,g0,g1,face+.0004f,.002f,26,6,Glass);
            Band(k,s,HelmAt,AxisZ,-fw,fw,g1,f1,face+.0004f,.0046f,28,1,NativeRobotSuit.Metal(.7f));
            Band(k,s,HelmAt,AxisZ,-fw,fw,f0,g0,face+.0004f,.0046f,28,1,NativeRobotSuit.Metal(.7f));
            Band(k,s,HelmAt,AxisZ,gw,fw,g0,g1,face+.0004f,.0046f,1,6,NativeRobotSuit.Metal(.7f));
            Band(k,s,HelmAt,AxisZ,-fw,-gw,g0,g1,face+.0004f,.0046f,1,6,NativeRobotSuit.Metal(.7f));
            // The grille: three slots under the visor, the lowest shorter.
            float[] slotY={.012f,0,-.012f},slotX={.036f,.036f,.026f};
            for(int i=0;i<3;i++){float w=Across(slotX[i],slotY[i]);Band(k,s,HelmAt,AxisZ,-w,w,slotY[i]-.00225f,slotY[i]+.00225f,face+.0003f,.0018f,8,1,Slot);}
            // The jaw's seam: across the chin and up each cheek to under the ear module.
            float Seam(float ph){float a=MathF.Abs(ph);return -.044f+.069f*Smooth((a-D(30))/D(60));}
            Band(k,s,HelmAt,AxisZ,-D(90),D(90),Seam,ph=>Seam(ph)+.0035f,face+.0002f,.0014f,40,1,Slot);
            // Vents at the back.
            for(int i=0;i<3;i++){float y=.006f+i*.012f,w=Across(.022f,y,true);Band(k,s,HelmAt,AxisZ,w,MathF.PI*2-w,y-.002f,y+.002f,face+.0003f,.0016f,6,1,Slot);}
            // The seal round the foot.
            k.Tube(s,56,7,t=>Shell(t*MathF.PI*2,Latitude(Foot(t*MathF.PI*2))),_=>.0042f,(u,v)=>NativeRobotSuit.Metal(.55f),true);
            Ears(k,s,HelmEar);
            return k.Parts.ToArray();
        }

        /// <summary>The half mask's parts (one, on the head).</summary>
        public static SheldonSection[] Half()
        {
            var k=new Kit();var s=k.Part("half mask");float reach=D(HalfReach);
            float Ph(float u)=>(u*2-1)*reach;
            Vector3 P(float u,float v){float ph=Ph(u),y=HalfFoot(ph)+(HalfTop(ph)-HalfFoot(ph))*v;return Plate(ph,y);}
            k.Slab(s,40,12,false,HalfThick,P,(u,v)=>{var p=P(u,v);return new Vector3(p.X,0,p.Z-HalfZ);},(u,v)=>NativeRobotSuit.Plate(1),(u,v)=>NativeRobotSuit.Metal(.45f));
            float face=HalfThick*.5f;
            // The grille across the mouth.
            float[] slotY={.004f,-.008f,-.02f},slotX={.034f,.034f,.024f};
            for(int i=0;i<3;i++){float w=MathF.Asin(slotX[i]/Spline(HalfA,slotY[i]));Band(k,s,Plate,HalfZ,-w,w,slotY[i]-.00225f,slotY[i]+.00225f,face+.0003f,.0018f,8,1,Slot);}
            // A light on each cheek.
            foreach(float side in new[]{-1f,1f})Band(k,s,Plate,HalfZ,side*D(46),side*D(50),.008f,.024f,face+.0003f,.0015f,1,4,NativeRobotSuit.Soft);
            Ears(k,s,HalfEar);
            return k.Parts.ToArray();
        }
    }
}
