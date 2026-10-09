using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The menu's live pilot portrait scene (client/menu-portraits.ts at 05950ea): the orthographic framing, the portrait lights
    // and ACES exposure, the podium and lobby chair (lobby-poses.ts lobbyChair), the animated Auditor halo (auditor-halo.ts),
    // the portrait mannequin's colours (character-rig.ts without arena glow), the hat top that raises the framing, and the
    // physical texture size. Source frame throughout (+x the rig's left); the Unity boundary mirrors x once.
    public struct PortraitFrame {public float Half,Aspect;public Vector3 Position,Target;}
    public sealed class PortraitMesh
    {
        public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();public readonly List<int> Indices=new List<int>();
        public void Clear(){Positions.Clear();Normals.Clear();Indices.Clear();}
    }
    public struct PortraitPiece {public string Name;public Vector3 Position,Size;public int Color;}
    public static class NativePortraitRules
    {
        // menu-portraits.ts:47: the mannequin group stands .88 up, over the podium.
        public const float GroupY=.88f,Near=.1f,Far=30;
        // menu-portraits.ts:24: toneMapping ACESFilmic, toneMappingExposure 1.1; alpha clear (0,0).
        public const float Exposure=1.1f;
        // menu-portraits.ts:50: HemisphereLight(0xe3eeff, 0x474054, 1.7); key DirectionalLight(0xffedda, 2.6) at (3.5,4,2.5);
        // cool rim DirectionalLight(0x849fe3, 2) at (-3,2,-2). Directional lights shine from their position toward the origin.
        public const int HemiSky=0xe3eeff,HemiGround=0x474054,KeyColor=0xffedda,RimColor=0x849fe3;
        public const float HemiIntensity=1.7f,KeyIntensity=2.6f,RimIntensity=2;
        public static readonly Vector3 KeyPosition=new Vector3(3.5f,4,2.5f),RimPosition=new Vector3(-3,2,-2);
        // menu-portraits.ts:48: the podium, CylinderGeometry(.96, 1.02, .075, 32) at y -.065, Lambert 0x333440.
        public const float PodiumTop=.96f,PodiumBottom=1.02f,PodiumHeight=.075f,PodiumY=-.065f;public const int PodiumSegments=32,PodiumColor=0x333440;
        // lobby-poses.ts lobbyChair(): unit boxes (frame 0x303744, pad 0x6a4851, trim 0xe6a878), at the scene origin.
        public static readonly PortraitPiece[] Chair=MakeChair();
        private static PortraitPiece[] MakeChair()
        {
            const int frame=0x303744,pad=0x6a4851,trim=0xe6a878;var list=new List<PortraitPiece>
            {new PortraitPiece{Name="seat",Position=new Vector3(0,.34f,0),Size=new Vector3(.68f,.09f,.62f),Color=pad},new PortraitPiece{Name="backrest",Position=new Vector3(0,.79f,-.27f),Size=new Vector3(.68f,.85f,.09f),Color=pad}};
            foreach(float x in new[]{-.29f,.29f})foreach(float z in new[]{-.24f,.24f})list.Add(new PortraitPiece{Name="chair-leg",Position=new Vector3(x,.16f,z),Size=new Vector3(.055f,.31f,.055f),Color=frame});
            foreach(float x in new[]{-.37f,.37f}){list.Add(new PortraitPiece{Name="armrest",Position=new Vector3(x,.60f,0),Size=new Vector3(.09f,.065f,.55f),Color=trim});list.Add(new PortraitPiece{Name="arm-support",Position=new Vector3(x,.46f,-.17f),Size=new Vector3(.045f,.26f,.045f),Color=frame});}
            return list.ToArray();
        }

        // framePortrait(): from the podium to the top of the head or hat (`top`, world y; a bare head 1.72), with a higher
        // view for the reclining pose; narrow cards keep the full character width.
        public static PortraitFrame Frame(int pose,float aspect,float top=1.72f)
        {
            bool sleep=pose==NativeLobbyPoses.Sleep;float high=Math.Min(2.7f,Math.Max(1.72f,top))+.1f,low=-.2f,centre=sleep?.35f:(high+low)/2;
            float half=Math.Max(sleep?1.12f:(high-low)/2*1.16f,.95f/aspect+1e-6f);
            return new PortraitFrame{Half=half,Aspect=aspect,Position=new Vector3(0,sleep?4.8f:centre+.94f,sleep?3.2f:5.2f),Target=new Vector3(0,centre,0)};
        }
        // hatTop(): the world top of the head cosmetic's bounds (three Box3.setFromObject: each mesh's local box corners
        // through its world matrix), here the head-space box of a hat model through the head joint; 0 without a hat.
        public static float HatTop(CapturedRig.Pose worldPose,Vector3 min,Vector3 max)
        {
            Quaternion q=worldPose.WorldRotation[CapturedRig.Head];Vector3 o=worldPose.WorldPosition[CapturedRig.Head];float top=float.MinValue;
            for(int i=0;i<8;i++){var c=new Vector3((i&1)!=0?max.X:min.X,(i&2)!=0?max.Y:min.Y,(i&4)!=0?max.Z:min.Z);top=Math.Max(top,o.Y+Vector3.Transform(c,q).Y);}
            return top+GroupY;
        }

        // ---- Colours (three ColorManagement: hex is sRGB, materials work in linear) -----------------------------------------
        public static float SrgbToLinear(float c)=>c<.04045f?c*.0773993808f:(float)Math.Pow(c*.9478672986+.0521327014,2.4);
        public static float LinearToSrgb(float c)=>c<.0031308f?c*12.92f:1.055f*(float)Math.Pow(c,.41666)-.055f;
        public static Vector3 Linear(int hex)=>new Vector3(SrgbToLinear((hex>>16&255)/255f),SrgbToLinear((hex>>8&255)/255f),SrgbToLinear((hex&255)/255f));
        private static int Byte(float linear)=>(int)Math.Round(Math.Max(0,Math.Min(255,LinearToSrgb(linear)*255)),MidpointRounding.AwayFromZero);
        public static int Hex(Vector3 linear)=>Byte(linear.X)<<16|Byte(linear.Y)<<8|Byte(linear.Z);
        // A #rrggbb string (the saved cosmetics' format), else the fallback.
        public static int Parse(string text,int fallback)
        {
            if(text==null||text.Length!=7||text[0]!='#')return fallback;int v=0;
            for(int i=1;i<7;i++){char c=text[i];int d=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(d<0)return fallback;v=v*16+d;}return v;
        }
        private static float Luma(Vector3 c)=>.2126f*c.X+.7152f*c.Y+.0722f*c.Z;
        // character-rig.ts edgeOf / flexOf: gloves, boots and joints follow the body colour (joints darker than the shell).
        public static int EdgeOf(int body)=>Hex(Vector3.Lerp(Linear(body)*.6f,Linear(0xb5c4ca),.1f));
        public static int FlexOf(int body)
        {Vector3 shell=Linear(body),c=Vector3.Lerp(shell*.08f,Linear(0x354853),.6f);return Hex(Luma(c)<Luma(shell)*.6f?c:shell*.45f);}
        // character-rig.ts ember(): the arena glow colour (the exported pilot's accent parts carry ember('#70e1d0') as their diffuse).
        public static Vector3 Ember(int hex)
        {
            Vector3 c=Linear(hex);float r=c.X,g=c.Y,b=c.Z,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),l=(min+max)/2,h=0,s=0;
            if(min!=max){float d=max-min;s=l<=.5f?d/(max+min):d/(2-max-min);h=max==r?(g-b)/d+(g<b?6:0):max==g?(b-r)/d+2:(r-g)/d+4;h/=6;}
            s=Math.Min(.9f,s+(1-s)*.55f);l=Math.Min(l,.52f);s=Math.Max(0,Math.Min(1,s));l=Math.Max(0,Math.Min(1,l));
            if(s==0)c=new Vector3(l,l,l);else{float p=l<=.5f?l*(1+s):l+s-l*s,q=2*l-p;c=new Vector3(Hue(q,p,h+1f/3),Hue(q,p,h),Hue(q,p,h-1f/3));}
            float y=Luma(c);return y>.4f?c*(.4f/y):c;
        }
        private static float Hue(float p,float q,float t){if(t<0)t+=1;if(t>1)t-=1;if(t<1f/6)return p+(q-p)*6*t;if(t<.5f)return q;if(t<2f/3)return p+(q-p)*6*(2f/3-t);return p;}
        // The exported pilot (tools/export_models.mjs: createCharacterRig('#70e1d0', '#e5e7eb', glow)) recoloured to the
        // portrait's unlit-armour rig (menu-portraits.ts: createCharacterRig(color, bodyColor), no glow): per part class, the
        // factor from the exported diffuse to the portrait diffuse (vertex colours carry the voxel shading multiplicatively).
        public const int PartOther=0,PartBody=1,PartAccent=2,PartEdge=3,PartFlex=4,PartHidden=5;
        public static int PartClass(string name)
        {
            if(string.IsNullOrEmpty(name))return PartOther;if(name.StartsWith("armor-",StringComparison.Ordinal))return PartHidden;
            switch(name){case "hip-shell":case "abdomen-shell":case "ribcage":case "upper-chest":case "collar":case "blank-head":case "crown":return PartBody;
                case "waist-band":case "sternum":return PartAccent;case "back-panel":case "jaw":return PartEdge;case "abdomen":case "neck-joint":return PartFlex;}
            if(Ends(name,"-upper-arm")||Ends(name,"-forearm")||Ends(name,"-thumb")||Ends(name,"-thigh")||Ends(name,"-calf")||Ends(name,"-shin"))return PartBody;
            if(Ends(name,"-shoulder-cap")||Ends(name,"-cuff")||Ends(name,"-kneecap"))return PartAccent;
            if(Ends(name,"-palm")||Ends(name,"-fingers")||Ends(name,"-foot"))return PartEdge;
            if(Ends(name,"-elbow-joint")||Ends(name,"-hip-joint")||Ends(name,"-knee-joint")||Ends(name,"-heel"))return PartFlex;
            return PartOther;
        }
        private static bool Ends(string s,string end)=>s.EndsWith(end,StringComparison.Ordinal);
        public const int ExportBody=0xe5e7eb,ExportAccent=0x70e1d0;
        private static Vector3 Ratio(Vector3 a,Vector3 b)=>new Vector3(b.X>0?a.X/b.X:1,b.Y>0?a.Y/b.Y:1,b.Z>0?a.Z/b.Z:1);
        public static Vector3 BodyTint(int body)=>Ratio(Linear(body),Linear(ExportBody));
        public static Vector3 AccentTint(int accent)=>Ratio(Linear(accent),Ember(ExportAccent));
        public static Vector3 EdgeTint(int body)=>Ratio(Linear(EdgeOf(body)),Linear(EdgeOf(ExportBody)));
        public static Vector3 FlexTint(int body)=>Ratio(Linear(FlexOf(body)),Linear(FlexOf(ExportBody)));

        // ---- Physical texture size --------------------------------------------------------------------------------------
        // menu-portraits.ts:66 sizes the canvas to the card rect at min(devicePixelRatio, 1.5). The kit's scale is not a
        // device pixel ratio: `pixelsPerUnit` (U.S) is the physical pixels per unit of the 1600x900 canvas (1.2 at 1080p,
        // 2.4 at 4K), so the texture is the rect's physical pixels, drawn 1:1, with only the longer side capped at `cap`.
        // False when the stage is too small to read (the emblem stands in then).
        public static bool TextureSize(float width,float height,float pixelsPerUnit,int cap,out int w,out int h)
        {
            float s=Math.Max(pixelsPerUnit,0);float pw=width*s,ph=height*s,longest=Math.Max(pw,ph);if(longest>cap){pw*=cap/longest;ph*=cap/longest;}
            w=Math.Max(1,(int)Math.Round(pw));h=Math.Max(1,(int)Math.Round(ph));return width>=48&&height>=48&&w>=32&&h>=32;
        }

        // ---- Geometry (three r180 primitives, source frame, counter-clockwise front faces) --------------------------------
        public static void Box(PortraitMesh m)
        {
            // BoxGeometry(1,1,1): one quad per face (+x, -x, +y, -y, +z, -z), each split (a,b,d),(b,c,d).
            for(int face=0;face<6;face++)
            {
                int axis=face/2;float sign=face%2==0?1:-1;Vector3 n=axis==0?new Vector3(sign,0,0):axis==1?new Vector3(0,sign,0):new Vector3(0,0,sign);
                Vector3 u=axis==0?new Vector3(0,0,-sign):axis==1?new Vector3(1,0,0):new Vector3(sign,0,0),v=axis==1?new Vector3(0,0,-sign):new Vector3(0,1,0);
                int start=m.Positions.Count;
                for(int iy=0;iy<2;iy++)for(int ix=0;ix<2;ix++){m.Positions.Add(n*.5f+u*(ix-.5f)+v*(.5f-iy));m.Normals.Add(n);}
                m.Indices.Add(start);m.Indices.Add(start+2);m.Indices.Add(start+1);m.Indices.Add(start+2);m.Indices.Add(start+3);m.Indices.Add(start+1);
            }
        }
        public static void Cylinder(PortraitMesh m,float radiusTop,float radiusBottom,float height,int radial)
        {
            float half=height/2,slope=(radiusBottom-radiusTop)/height;int start=m.Positions.Count;
            for(int y=0;y<=1;y++){float radius=y*(radiusBottom-radiusTop)+radiusTop;for(int x=0;x<=radial;x++){float t=x/(float)radial*MeleeMath.TAU,sin=(float)Math.Sin(t),cos=(float)Math.Cos(t);m.Positions.Add(new Vector3(radius*sin,-y*height+half,radius*cos));m.Normals.Add(Vector3.Normalize(new Vector3(sin,slope,cos)));}}
            for(int x=0;x<radial;x++){int a=start+x,b=start+radial+1+x,c=b+1,d=a+1;if(radiusTop>0){m.Indices.Add(a);m.Indices.Add(b);m.Indices.Add(d);}if(radiusBottom>0){m.Indices.Add(b);m.Indices.Add(c);m.Indices.Add(d);}}
            for(int cap=0;cap<2;cap++)
            {
                bool top=cap==0;float radius=top?radiusTop:radiusBottom,sign=top?1:-1;int centre=m.Positions.Count;
                for(int x=1;x<=radial;x++){m.Positions.Add(new Vector3(0,half*sign,0));m.Normals.Add(new Vector3(0,sign,0));}
                int rim=m.Positions.Count;for(int x=0;x<=radial;x++){float t=x/(float)radial*MeleeMath.TAU;m.Positions.Add(new Vector3(radius*(float)Math.Sin(t),half*sign,radius*(float)Math.Cos(t)));m.Normals.Add(new Vector3(0,sign,0));}
                for(int x=0;x<radial;x++){int c=centre+x,i=rim+x;if(top){m.Indices.Add(i);m.Indices.Add(i+1);m.Indices.Add(c);}else{m.Indices.Add(i+1);m.Indices.Add(i);m.Indices.Add(c);}}
            }
        }
        public static void Torus(PortraitMesh m,float radius,float tube,int radial,int tubular,float arc)
        {
            int start=m.Positions.Count;
            for(int j=0;j<=radial;j++)for(int i=0;i<=tubular;i++)
            {
                float u=i/(float)tubular*arc,v=j/(float)radial*MeleeMath.TAU;var p=new Vector3((radius+tube*(float)Math.Cos(v))*(float)Math.Cos(u),(radius+tube*(float)Math.Cos(v))*(float)Math.Sin(u),tube*(float)Math.Sin(v));
                m.Positions.Add(p);m.Normals.Add(Vector3.Normalize(p-new Vector3(radius*(float)Math.Cos(u),radius*(float)Math.Sin(u),0)));
            }
            for(int j=1;j<=radial;j++)for(int i=1;i<=tubular;i++)
            {int a=start+(tubular+1)*j+i-1,b=start+(tubular+1)*(j-1)+i-1,c=start+(tubular+1)*(j-1)+i,d=start+(tubular+1)*j+i;m.Indices.Add(a);m.Indices.Add(b);m.Indices.Add(d);m.Indices.Add(b);m.Indices.Add(c);m.Indices.Add(d);}
        }
        // auditor-halo.ts flame: Shape moveTo(-.06,0), two cubic beziers through (0,.43) back to (.06,0), closed; ShapeGeometry
        // samples each curve at 12 divisions (25 outline points). The outline is ear-clipped: any triangulation of it fills
        // exactly the shape the source's earcut fills. Faces +z, double-sided.
        public static List<Vector2> FlameOutline()
        {
            var points=new List<Vector2>();
            void Bezier(Vector2 p0,Vector2 p1,Vector2 p2,Vector2 p3){for(int i=0;i<=12;i++){float t=i/12f,k=1-t;var p=k*k*k*p0+3*k*k*t*p1+3*k*t*t*p2+t*t*t*p3;if(points.Count>0&&points[points.Count-1]==p)continue;points.Add(p);}}
            Bezier(new Vector2(-.06f,0),new Vector2(-.15f,.16f),new Vector2(.09f,.16f),new Vector2(0,.43f));Bezier(new Vector2(0,.43f),new Vector2(.2f,.24f),new Vector2(.13f,.11f),new Vector2(.06f,0));
            return points;
        }
        public static void Flame(PortraitMesh m)
        {
            var outline=FlameOutline();int start=m.Positions.Count;foreach(var p in outline){m.Positions.Add(new Vector3(p.X,p.Y,0));m.Normals.Add(MeleeMath.Z);}
            foreach(int i in EarClip(outline))m.Indices.Add(start+i);
        }
        private static float Cross(Vector2 o,Vector2 a,Vector2 b)=>(a.X-o.X)*(b.Y-o.Y)-(a.Y-o.Y)*(b.X-o.X);
        // Ear clipping of a simple polygon; triangles wind counter-clockwise (front faces seen from +z).
        public static List<int> EarClip(List<Vector2> points)
        {
            var result=new List<int>();var ring=new List<int>();for(int i=0;i<points.Count;i++)ring.Add(i);
            float area=0;for(int i=0;i<points.Count;i++){var a=points[i];var b=points[(i+1)%points.Count];area+=a.X*b.Y-b.X*a.Y;}if(area<0)ring.Reverse();
            int guard=points.Count*points.Count;
            while(ring.Count>3&&guard-->0)
            {
                for(int k=0;k<ring.Count;k++)
                {
                    int ia=ring[(k+ring.Count-1)%ring.Count],ib=ring[k],ic=ring[(k+1)%ring.Count];Vector2 a=points[ia],b=points[ib],c=points[ic];
                    if(Cross(a,b,c)<=1e-9f)continue;bool inside=false;
                    foreach(int j in ring){if(j==ia||j==ib||j==ic)continue;var p=points[j];if(Cross(a,b,p)>=0&&Cross(b,c,p)>=0&&Cross(c,a,p)>=0){inside=true;break;}}
                    if(inside)continue;result.Add(ia);result.Add(ib);result.Add(ic);ring.RemoveAt(k);break;
                }
            }
            if(ring.Count==3){result.Add(ring[0]);result.Add(ring[1]);result.Add(ring[2]);}
            return result;
        }

        // ---- The Auditor halo (auditor-halo.ts auditorHalo + animateCosmetics) -------------------------------------------
        // Three broken red arcs (MeshBasic 0xff1635, not tone mapped) with additive glow tubes (0xff1735 at .15, no depth
        // write, not tone mapped) and 12 black flames (MeshBasic 0x080007, double-sided), in the head cosmetic socket.
        public const float HaloArcRadius=.32f,HaloArcTube=.019f,HaloAuraTube=.052f,HaloArc=(float)(Math.PI*.57),HaloAuraOpacity=.15f;
        public const int HaloRed=0xff1635,HaloGlow=0xff1735,HaloBlack=0x080007,HaloFlames=12;
        public static void HaloGroup(float time,out Vector3 position,out Quaternion rotation,out float scale)
        {position=new Vector3(0,.46f+(float)Math.Sin(time*2.3)*.012f,0);rotation=MeleeMath.Euler(0,time*.18f,-.12f);scale=.72f;}
        public static void HaloArcLocal(int i,out Vector3 position,out Quaternion rotation){position=new Vector3(0,i==1?.025f:0,0);rotation=MeleeMath.Euler((float)Math.PI/2,0,i*2.1f);}
        public static void HaloFlame(int n,float time,out Vector3 position,out Quaternion rotation,out Vector3 scale)
        {
            float a=n*(float)Math.PI/6,pulse=.65f+.35f*(float)Math.Sin(time*6+n*1.7f);position=new Vector3((float)Math.Cos(a)*.3f,0,(float)Math.Sin(a)*.3f);
            rotation=MeleeMath.Euler(0,-a,(float)Math.Sin(time*5+n)*.17f);scale=new Vector3(.65f+.25f*(float)Math.Sin(time*4+n),pulse,1);
        }
        // The halo's world top for the framing (Box3 over the halo's meshes at `time`): arcs, glow tubes and flames.
        public static float HaloTop(CapturedRig.Pose worldPose,float time)
        {
            HaloGroup(time,out var gp,out var gq,out float gs);Quaternion hq=worldPose.WorldRotation[CapturedRig.Head];Vector3 ho=worldPose.WorldPosition[CapturedRig.Head];float top=float.MinValue;
            Vector3 World(Vector3 local)=>ho+Vector3.Transform(gp+Vector3.Transform(local*gs,gq),hq);
            void Corners(Vector3 min,Vector3 max,Func<Vector3,Vector3> local){for(int i=0;i<8;i++){var c=new Vector3((i&1)!=0?max.X:min.X,(i&2)!=0?max.Y:min.Y,(i&4)!=0?max.Z:min.Z);top=Math.Max(top,World(local(c)).Y);}}
            for(int i=0;i<3;i++)
            {
                HaloArcLocal(i,out var ap,out var aq);
                foreach(float tube in new[]{HaloArcTube,HaloAuraTube}){var box=TorusBounds(tube);Corners(box.min,box.max,c=>ap+Vector3.Transform(c,aq));}
            }
            var outline=FlameOutline();Vector2 fmin=new Vector2(float.MaxValue),fmax=new Vector2(float.MinValue);foreach(var p in outline){fmin=Vector2.Min(fmin,p);fmax=Vector2.Max(fmax,p);}
            for(int n=0;n<HaloFlames;n++){HaloFlame(n,time,out var fp,out var fq,out var fs);Corners(new Vector3(fmin,0),new Vector3(fmax,0),c=>fp+Vector3.Transform(c*fs,fq));}
            return top+GroupY;
        }
        private static (Vector3 min,Vector3 max) TorusBounds(float tube)
        {
            var m=new PortraitMesh();Torus(m,HaloArcRadius,tube,4,22,HaloArc);Vector3 min=new Vector3(float.MaxValue),max=new Vector3(float.MinValue);foreach(var p in m.Positions){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}return (min,max);
        }

        // ---- The sleeping pilot's z Z Z (lobby.css .sleep-marks, lobby-sleep 3.3 s ease-out, staggered 1.1 s) -------------
        // Each mark rises from (0,8) at .7 scale to (22,-44) at 1.25 scale and fades in by 20%, out by the end; reduced
        // motion holds them at .6 opacity, offset (0,0), (13,-12), (27,-27). Returns offset (px), scale and opacity.
        public static void SleepMark(int index,float time,bool reduced,out Vector2 offset,out float scale,out float opacity)
        {
            if(reduced){offset=index==0?Vector2.Zero:index==1?new Vector2(13,-12):new Vector2(27,-27);scale=1;opacity=.6f;return;}
            float p=time-index*1.1f;if(p<0){offset=Vector2.Zero;scale=1;opacity=0;return;}p=p/3.3f-(float)Math.Floor(p/3.3f);
            float e=EaseOut(p);offset=Vector2.Lerp(new Vector2(0,8),new Vector2(22,-44),e);scale=.7f+.55f*e;
            // opacity keyframes 0 -> .8 at 20% -> 0 at 100%, each segment eased (CSS applies the timing function per segment).
            opacity=p<.2f?.8f*EaseOut(p/.2f):.8f*(1-EaseOut((p-.2f)/.8f));
        }
        // CSS ease-out: cubic-bezier(0, 0, .58, 1).
        public static float EaseOut(float x)=>NativeUiMotion.Bezier(0,0,.58f,1,x);
    }
}
