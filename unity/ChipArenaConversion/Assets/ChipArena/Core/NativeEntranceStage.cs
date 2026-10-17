using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>Where a directed entrance's set pieces go (Runtime: the source-model piece batches, stage lit).</summary>
    public interface INativeEntranceProps
    {
        /// <summary>A primitive (source frame): lit (Lambert, its colour the albedo) or glow (unlit, its colour emitted; linear, may
        /// exceed 1 for the bloom).</summary>
        void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color);
    }
    /// <summary>
    /// The light of one directed entrance frame, in the source renderer's units (three.js colour x intensity, linear), which
    /// Runtime converts exactly as it converts the baked entrance stages (NativeEntranceLight, NativeStageLighting): a
    /// background and fog, a hemisphere ambient, up to two directional lights (Unity lights; the first casts the shadows) and
    /// up to eight point lights (the shaders' stage block).
    /// </summary>
    public sealed class NativeEntranceLights
    {
        public const int MaxDirectional=2;
        public Vector3 Background,Fog;public float FogNear,FogFar;
        public Vector3 Sky,Ground;
        public int Directionals,Points;
        public readonly Vector3[] DirectionTo=new Vector3[MaxDirectional],DirectionColor=new Vector3[MaxDirectional];
        public readonly NativeStageLight[] Point=new NativeStageLight[NativeStageLighting.Max];
        public void Clear(){Directionals=0;Points=0;Background=Fog=Sky=Ground=Vector3.Zero;FogNear=9;FogFar=30;}
        public void Directional(Vector3 toLight,Vector3 color){if(Directionals>=MaxDirectional||color.X+color.Y+color.Z<=0)return;DirectionTo[Directionals]=Vector3.Normalize(toLight);DirectionColor[Directionals]=color;Directionals++;}
        /// <summary>A point light (decay 2, three's default), kept among the eight that reach the stage centre most.</summary>
        public void Add(Vector3 at,Vector3 color,float distance){Points=NativeStageLighting.Add(Point,Points,new NativeStageLight(at.X,at.Y,at.Z,color.X,color.Y,color.Z,distance,2));}
    }
    /// <summary>
    /// Presentation only: the directed entrances' arena stage. A dark floor slab with a faint 1 m grid, a ring of twelve
    /// neon pylons 7.5 m out (their strips power on, flash and pulse with the entrance) and a distant skyline of dark towers
    /// with lit windows in the fog, all from source-model primitives (no flat glowing discs or rings on the floor), and the
    /// light rig the entrances animate. The subject stands at the origin; the floor top is FloorY.
    /// </summary>
    public static class NativeEntranceStage
    {
        /// <summary>The floor's top (source frame): where the intro rig's soles rest (root .88, ankles .085, soles .07 below).</summary>
        public const float FloorY=.012f;
        /// <summary>The arena floor around the dais (its top), and the dais's circumradius.</summary>
        public const float GroundY=FloorY-.12f,DaisRadius=2.6f;
        private static readonly Vector3 DaisColor=NativeEntranceFx.Hex(0x1b2236),DaisSkirt=NativeEntranceFx.Hex(0x0a0e19);
        public const int Pylons=12;public const float PylonRadius=7.5f,PylonHeight=3.6f;
        public static readonly Vector3 FloorColor=NativeEntranceFx.Hex(0x0d1322);
        private static readonly Vector3 Metal=NativeEntranceFx.Hex(0x1a2032),Tower=NativeEntranceFx.Hex(0x0b0e18);
        /// <summary>Pylon `i`'s position on the ring (they skip the camera's usual opening in front).</summary>
        public static Vector3 Pylon(int i){float a=(i+.5f)/Pylons*6.2831853f;return new Vector3((float)Math.Sin(a)*PylonRadius,0,(float)Math.Cos(a)*PylonRadius);}
        /// <summary>
        /// The floor, the grid (`grid` scales its faint glow; `pulse` a wave from the centre at `pulseAt`), the pylons (each
        /// strip's level from `pylon`) in `accent` and the skyline.
        /// </summary>
        public static void Arena(INativeEntranceProps props,float t,Vector3 accent,ReadOnlySpan<float> pylon,float grid,float pulseAt=-10,Vector3? gridColor=null)
        {
            props.Piece(WeaponShape.Cube,false,new Vector3(0,GroundY-.5f,0),Quaternion.Identity,new Vector3(70,1,70),FloorColor);
            // The dais: a hexagon (three crossed slabs) whose top is the floor the pilot stands on, with a darker bevelled skirt.
            for(int k=0;k<3;k++)
            {
                var q=NativeEntranceMath.Yaw(k*1.0471976f);
                props.Piece(WeaponShape.Cube,false,new Vector3(0,(FloorY+GroundY)*.5f,0),q,new Vector3(DaisRadius,FloorY-GroundY,DaisRadius*1.7320508f),DaisColor);
                props.Piece(WeaponShape.Cube,false,new Vector3(0,GroundY+.03f,0),q,new Vector3(DaisRadius+.12f,.06f,(DaisRadius+.12f)*1.7320508f),DaisSkirt);
            }
            var g=gridColor??new Vector3(.010f,.034f,.05f);
            if(grid>.001f)
            {
                // Lines in 1 m steps out to 9 m, each broken into 2 m segments that fade with distance; the pulse brightens the
                // segments its front passes.
                for(int axis=0;axis<2;axis++)for(int line=-9;line<=9;line++)for(int seg=-4;seg<4;seg++)
                {
                    float mid=seg*2+1,dist=(float)Math.Sqrt(mid*mid+line*line),fade=NativeEntranceMath.Clamp01(1-dist/10.5f);if(fade<=0)continue;
                    float wave=pulseAt>-5?NativeEntranceMath.Bump(t-pulseAt-dist*.09f,0,.45f)*(1-dist/12):0;float level=grid*fade*fade+wave*2.2f;if(level<=.003f)continue;
                    float gy=Math.Abs(line)<DaisRadius*.8f&&Math.Abs(mid)<DaisRadius*.8f?FloorY:GroundY;var at=axis==0?new Vector3(line,gy+.002f,mid):new Vector3(mid,gy+.002f,line);var size=axis==0?new Vector3(.018f,.004f,2):new Vector3(2,.004f,.018f);
                    props.Piece(WeaponShape.Cube,true,at,Quaternion.Identity,size,g*level+accent*(wave*.08f));
                }
            }
            for(int i=0;i<Pylons;i++)
            {
                var p=Pylon(i);float yaw=(float)Math.Atan2(p.X,p.Z);var q=NativeEntranceMath.Yaw(yaw);var inward=-Vector3.Normalize(p);
                props.Piece(WeaponShape.Cube,false,p+new Vector3(0,PylonHeight*.5f+GroundY,0),q,new Vector3(.42f,PylonHeight,.42f),Metal);
                props.Piece(WeaponShape.Cube,false,p+new Vector3(0,GroundY+.09f,0),q,new Vector3(.7f,.18f,.7f),Metal*.7f);
                float level=pylon[i];if(level<=.002f)continue;
                props.Piece(WeaponShape.Cube,true,p+inward*.215f+new Vector3(0,PylonHeight*.5f+GroundY+.1f,0),q,new Vector3(.07f,PylonHeight-.5f,.012f),accent*(level*2.4f));
                props.Piece(WeaponShape.Cube,true,p+new Vector3(0,PylonHeight+GroundY+.03f,0),q,new Vector3(.44f,.05f,.44f),accent*(level*1.4f));
            }
            Skyline(props);
        }
        private static void Skyline(INativeEntranceProps props)
        {
            for(int i=0;i<30;i++)
            {
                float a=(i+.35f*NativeEntranceMath.Rand(77,i))/30*6.2831853f,r=21+8*NativeEntranceMath.Rand(78,i),w=2.5f+3.5f*NativeEntranceMath.Rand(79,i),h=5+13*NativeEntranceMath.Rand(80,i)*NativeEntranceMath.Rand(81,i)+3*NativeEntranceMath.Rand(82,i);
                var p=new Vector3((float)Math.Sin(a)*r,FloorY+h*.5f,(float)Math.Cos(a)*r);var q=NativeEntranceMath.Yaw(a);
                props.Piece(WeaponShape.Cube,false,p,q,new Vector3(w,h,w*.8f),Tower);
                int windows=2+(int)(4*NativeEntranceMath.Rand(83,i));var inward=-Vector3.Normalize(new Vector3(p.X,0,p.Z));var across=Vector3.Cross(Vector3.UnitY,inward);
                for(int k=0;k<windows;k++)
                {
                    float y=FloorY+h*(.15f+.75f*NativeEntranceMath.Rand(84,i*8+k)),x=(NativeEntranceMath.Rand(87,i*8+k)-.5f)*w*.6f,warm=NativeEntranceMath.Rand(85,i*8+k);
                    var c=warm>.6f?new Vector3(1,.5f,.2f):new Vector3(.3f,.5f,1);
                    props.Piece(WeaponShape.Cube,true,new Vector3(p.X,y,p.Z)+inward*(w*.4f+.02f)+across*x,q,new Vector3(w*.18f,.09f,.02f),c*(.12f+.16f*NativeEntranceMath.Rand(86,i*8+k)));
                }
                if(h>12)props.Piece(WeaponShape.Cube,true,new Vector3(p.X,FloorY+h+.1f,p.Z),q,new Vector3(.18f,.18f,.18f),new Vector3(1.2f,.08f,.06f));
            }
        }
        /// <summary>The pylons' solid boxes for the camera guard (centre, half size pairs; axis aligned, generous).</summary>
        public static int PylonBoxes(Span<Vector4> into,int at)
        {
            for(int i=0;i<Pylons&&at+1<into.Length;i++){var p=Pylon(i);into[at++]=new Vector4(p.X,PylonHeight*.5f,p.Z,0);into[at++]=new Vector4(.32f,PylonHeight*.5f+.2f,.32f,0);}
            return at;
        }
        /// <summary>
        /// The base light rig: the night background and fog, the hemisphere ambient, a cool key from the front-left (scaled by
        /// `key`), a rim behind the subject in `rim` and a magenta-pink fill from the side (scaled by `fill`). Entrances add their own.
        /// </summary>
        public static void Rig(NativeEntranceLights l,float key,Vector3 rim,float rimLevel,float fill,float ambient=1)
        {
            l.Clear();l.Background=NativeEntranceFx.Hex(0x05070f);l.Fog=l.Background;l.FogNear=10;l.FogFar=34;
            l.Sky=NativeEntranceFx.Hex(0x9fbcdf,.55f*ambient);l.Ground=NativeEntranceFx.Hex(0x14101d,.6f*ambient);
            l.Directional(new Vector3(3,5,4),NativeEntranceFx.Hex(0xd9e6ff,1.15f*key));
            if(rimLevel>0)l.Add(new Vector3(-.4f,2.3f,-1.6f),rim*(7*rimLevel),9);
            if(fill>0)l.Add(new Vector3(-3,1.3f,1),NativeEntranceFx.Hex(0xf369db,3.4f*fill),8);
        }
    }
}
