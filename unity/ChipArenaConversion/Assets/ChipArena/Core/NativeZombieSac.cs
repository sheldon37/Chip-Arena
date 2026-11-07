using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The spitter's throat sac on the model (docs/ZOMBIE-MODELS.md; Runtime/NativeZombieModelPresentation.cs; the voxel spitter's
    // sac is its neck joint swollen, NativeHordeKindPresentation HordeQueue): a sac under the jaw, ahead of the neck, built at the
    // model's rest joints (root space, +z the model's front). It rides neck_01 at its base and the head toward its top (so a nod
    // folds it under the jaw), and swells by its one blend shape with NativeHordeFx.Swell through a spit's wind-up: forward and
    // down, its glow rising with it. Its back half is flattened into the neck. Colours: an acid green with darker veins and
    // a paler tip, blending into the zombie's own skin where it meets the jaw. Presentation only.
    public static class NativeZombieSac
    {
        public const int Rings=9,Sides=16;
        /// <summary>Half sizes (m) at rest and swollen: across, up, ahead.</summary>
        public static readonly Vector3 Rest=new Vector3(.040f,.034f,.028f),Swollen=new Vector3(.064f,.054f,.060f);
        /// <summary>Its centre: this share of the way from neck_01 to head, this far ahead of that line (m).</summary>
        public const float Up=.38f,Ahead=.045f;
        /// <summary>Swollen, its centre moves this far ahead and down (m).</summary>
        public const float SwellAhead=.014f,SwellDown=.010f;
        public const int SacColour=0x7d9a30,VeinColour=0x44541a,TipColour=0xa9c943;
        /// <summary>Its steady glow and the glow at full swell (the toon's vertex-colour share).</summary>
        public const float GlowRest=.06f,GlowSwollen=.6f;
        public sealed class Sac
        {
            public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>(),Swell=new List<Vector3>();
            /// <summary>sRGB colour, alpha 1.</summary>
            public readonly List<Vector4> Colors=new List<Vector4>();
            /// <summary>Each vertex's share on the head (the rest on neck_01).</summary>
            public readonly List<float> Head=new List<float>();
            public readonly List<int> Indices=new List<int>();
        }
        private static Vector4 Rgb(int c)=>new Vector4((c>>16&255)/255f,(c>>8&255)/255f,(c&255)/255f,1);
        private static float Smooth(float x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
        /// <summary>The sac at rest for a neck at `neck` and a head at `head` (root space), facing `forward`, its top blending into
        /// `skin` (0xRRGGBB); null when the joints coincide.</summary>
        public static Sac Build(Vector3 neck,Vector3 head,Vector3 forward,int skin)
        {
            var up=head-neck;if(up.Length()<1e-4f)return null;up=Vector3.Normalize(up);
            var ahead=forward-up*Vector3.Dot(forward,up);if(ahead.Length()<1e-4f)return null;ahead=Vector3.Normalize(ahead);
            var across=Vector3.Cross(up,ahead);
            var centre=neck+(head-neck)*Up+ahead*Ahead;var swollen=centre+ahead*SwellAhead-up*SwellDown;
            var s=new Sac();Vector4 sac=Rgb(SacColour),vein=Rgb(VeinColour),tip=Rgb(TipColour),own=Rgb(skin);
            // Rings from the top (under the jaw) to the bottom, each a loop; a pole vertex at each end.
            Vector3 At(Vector3 d,Vector3 c,Vector3 half)
            {
                float z=d.Z<0?d.Z*.35f:d.Z;
                return c+across*(d.X*half.X)+up*(d.Y*half.Y)+ahead*(z*half.Z);
            }
            void Add(Vector3 d,float theta)
            {
                var p=At(d,centre,Rest);s.Positions.Add(p);s.Swell.Add(At(d,swollen,Swollen)-p);
                var n=across*(d.X/Rest.X)+up*(d.Y/Rest.Y)+ahead*(d.Z/Rest.Z);s.Normals.Add(Vector3.Normalize(n));
                // Veins: five meridians that wander down the front; the tip paler; the top into the skin.
                float wander=theta*5f/2f+d.Y*1.7f,veinShare=MathF.Abs(MathF.Sin(wander))<.11f&&d.Z>-.2f&&d.Y<.6f?.8f:0;
                var c=Vector4.Lerp(sac,tip,Smooth((-d.Y-.35f)/.65f));c=Vector4.Lerp(c,vein,veinShare);c=Vector4.Lerp(c,own,Smooth((d.Y-.35f)/.5f));c.W=1;
                s.Colors.Add(c);s.Head.Add(.75f*Smooth((d.Y+.2f)/1.2f));
            }
            Add(new Vector3(0,1,0),0);
            for(int r=1;r<Rings;r++)
            {
                float phi=MathF.PI*r/Rings,y=MathF.Cos(phi),ring=MathF.Sin(phi);
                for(int k=0;k<Sides;k++){float theta=MathF.PI*2*k/Sides;Add(new Vector3(MathF.Sin(theta)*ring,y,MathF.Cos(theta)*ring),theta);}
            }
            Add(new Vector3(0,-1,0),0);
            int last=s.Positions.Count-1;
            int V(int r,int k)=>1+(r-1)*Sides+(k%Sides);
            for(int k=0;k<Sides;k++)Tri(s,0,V(1,k),V(1,k+1),centre);
            for(int r=1;r<Rings-1;r++)for(int k=0;k<Sides;k++){Tri(s,V(r,k),V(r+1,k),V(r+1,k+1),centre);Tri(s,V(r,k),V(r+1,k+1),V(r,k+1),centre);}
            for(int k=0;k<Sides;k++)Tri(s,last,V(Rings-1,k+1),V(Rings-1,k),centre);
            return s;
        }
        // A triangle turned to face away from the sac's centre (its cross product outward, as the skeleton's).
        private static void Tri(Sac s,int a,int b,int c,Vector3 centre)
        {
            var pa=s.Positions[a];var f=Vector3.Cross(s.Positions[b]-pa,s.Positions[c]-pa);var mid=(pa+s.Positions[b]+s.Positions[c])/3;
            if(Vector3.Dot(f,mid-centre)>=0){s.Indices.Add(a);s.Indices.Add(b);s.Indices.Add(c);}else{s.Indices.Add(a);s.Indices.Add(c);s.Indices.Add(b);}
        }
        /// <summary>The sac's glow share at `swell` (0..1, NativeHordeFx.Swell).</summary>
        public static float Glow(float swell)=>GlowRest+(GlowSwollen-GlowRest)*Math.Clamp(swell,0,1);
    }
}
