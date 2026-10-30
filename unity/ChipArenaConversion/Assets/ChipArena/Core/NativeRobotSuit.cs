using System;
using System.Collections.Generic;
using System.Numerics;
using Kit=ChipArena.UnityConversion.NativeModelHatMeshes.Kit;
using Paint=ChipArena.UnityConversion.NativeModelHatMeshes.Paint;

namespace ChipArena.UnityConversion
{
    // The robot suit (owner 2026-10-30: "Lets also design clothing that resembles our previous player model (Robotic style) and
    // reference the previous face. Of course, in the new player style."; the model's default outfit since owner 2026-10-30: "I
    // think we will make the default outfit the new designed armor and that full helmet"): the original voxel robot's build worn
    // as plates over a dark fitted under-suit (Under), in the model's own smooth style. From the robot: its white shell (here the
    // pilot's body colour), the glowing accent at its waist and edges, the reactor panel and its cross on its back, its segmented
    // dark neck, the square ear modules with their light either side of its head; its head (the visor bar, the grille) is the
    // robot's helmet (Core/NativeRobotHelm), whose own ear modules then take these ones' place. Each piece is authored on the
    // pack body at rest (its bind pose: metres, x the body's right, y up, z ahead) and rides one bone (Bones), drawn with that
    // bone's matrix times its bind pose (Runtime/NativeRobotSuitPresentation.cs). The fit was measured in Unity on the rest
    // body in the under-suit (2026-10-30): its cross-sections round the torso, each limb and the neck, the plates a centimetre
    // or so over them. Presentation only.
    public static class NativeRobotSuit
    {
        /// <summary>The bones the pieces ride, by index (the parts' Bone).</summary>
        public static readonly string[] Bones={"spine_05","pelvis","neck_01","head","upperarm_l","upperarm_r","lowerarm_l","lowerarm_r","thigh_l","thigh_r","calf_l","calf_r"};
        public const int Chest=0,Hips=1,Neck=2,Head=3,ArmL=4,ArmR=5,ForearmL=6,ForearmR=7,ThighL=8,ThighR=9,ShinL=10,ShinR=11;
        /// <summary>The under-suit: the model's fitted pieces the plates sit over (its hair and skin stay the pilot's own).</summary>
        public const string Under="upper:sweater,lower:skinny-jeans,hands:gloves,feet:work-boots";
        /// <summary>The pilot's look with the under-suit in place of its clothes (Core/NativeLook map).</summary>
        public static string Look(string look)
        {
            var map=NativeLook.With(NativeLook.Normalize(Under),NativeLook.Hair,NativeLook.Of(look,NativeLook.Hair));int skin=NativeLook.SkinOf(look);
            return skin<0?map:NativeLook.WithSkin(map,skin);
        }

        // ---- paints (the helmet's and half mask's too: Core/NativeRobotHelm): the robot's shell under the body colour, a little self-lit as the voxel pilot's armour light keeps it
        // bright in any arena's light, dark metal seams and joints, the accent's light ----
        internal const int Shell=0xf3f1ea,Dark=0x2b3038,Darker=0x1a1e24,Screen=0x0f1317;
        private const float ShellGlow=.22f;
        internal static Paint Plate(float shade=1)=>new Paint(Shell,shade,false,ShellGlow){Body=true};
        internal static Paint Metal(float shade=1)=>new Paint(Dark,shade);
        internal static readonly Paint Light=Paint.Lit(2.2f),Soft=Paint.Lit(1.2f,.3f);
        private const float Gap=.013f,Thick=.008f;

        // ---- the rest body (bind pose) ----
        private static readonly Vector3 Pelvis=new Vector3(0,.9869f,.0238f),NeckRoot=new Vector3(0,1.4914f,-.0155f),HeadRoot=new Vector3(0,1.5734f,-.0048f);
        private static readonly Vector3 UpperArm=new Vector3(-.1326f,1.4253f,-.0275f),Forearm=new Vector3(-.2909f,1.2154f,-.0299f),Hand=new Vector3(-.4269f,1.06f,.0575f);
        private static readonly Vector3 Thigh=new Vector3(-.0929f,.9612f,.038f),Calf=new Vector3(-.1031f,.5361f,.0299f),Foot=new Vector3(-.1172f,.1095f,-.0052f);
        // The torso's outline round a vertical axis at z .02 (plus TorsoZ), from ahead (0) to behind (12) in 15 degree steps, at
        // each height (the sweater over the chest, the jeans at the hips; under the arms the shoulder's reach).
        private static readonly float[] TorsoY={1.00f,1.03f,1.06f,1.10f,1.14f,1.18f,1.22f,1.26f,1.30f,1.34f,1.38f,1.42f,1.45f};
        private static readonly float[] TorsoZ={-.0088f,-.0061f,-.0016f,-.0052f,-.0017f,.0055f,.0056f,.0077f,.0054f,0,-.0084f,-.0206f,-.0287f};
        private static readonly float[] TorsoReach={
            .106f,.107f,.122f,.138f,.151f,.158f,.154f,.150f,.142f,.135f,.122f,.107f,.093f,
            .103f,.105f,.117f,.133f,.140f,.147f,.149f,.143f,.136f,.128f,.117f,.108f,.093f,
            .126f,.132f,.137f,.143f,.154f,.160f,.150f,.162f,.158f,.152f,.143f,.134f,.120f,
            .128f,.134f,.141f,.144f,.148f,.155f,.156f,.156f,.156f,.153f,.144f,.137f,.130f,
            .124f,.130f,.136f,.137f,.139f,.141f,.142f,.146f,.148f,.145f,.138f,.130f,.126f,
            .112f,.120f,.126f,.127f,.126f,.127f,.129f,.129f,.137f,.137f,.129f,.119f,.116f,
            .104f,.114f,.121f,.130f,.130f,.129f,.130f,.129f,.129f,.129f,.125f,.108f,.097f,
            .095f,.106f,.111f,.126f,.136f,.135f,.137f,.137f,.136f,.133f,.122f,.101f,.094f,
            .096f,.107f,.124f,.139f,.147f,.148f,.144f,.146f,.149f,.142f,.124f,.101f,.092f,
            .096f,.109f,.123f,.143f,.153f,.155f,.156f,.165f,.178f,.151f,.127f,.101f,.087f,
            .092f,.101f,.115f,.134f,.147f,.165f,.172f,.176f,.179f,.154f,.118f,.092f,.080f,
            .081f,.085f,.093f,.111f,.155f,.168f,.170f,.170f,.170f,.126f,.097f,.080f,.072f,
            .059f,.068f,.076f,.084f,.119f,.156f,.160f,.160f,.139f,.100f,.075f,.067f,.061f};
        /// <summary>The torso's reach at bearing `th` (0 ahead, toward the body's right) and height y, and its axis's z there.</summary>
        public static float Torso(float th,float y,out float z)
        {
            int r=0;while(r<TorsoY.Length-2&&y>TorsoY[r+1])r++;float fy=Math.Clamp((y-TorsoY[r])/(TorsoY[r+1]-TorsoY[r]),0,1);
            float a=MathF.Abs(MathF.IEEERemainder(th,MathF.PI*2)),k=a/(MathF.PI/12);int c=Math.Min(11,(int)k);float fk=k-c;
            float At(int row)=>TorsoReach[row*13+c]+(TorsoReach[row*13+c+1]-TorsoReach[row*13+c])*fk;
            z=.02f+TorsoZ[r]+(TorsoZ[r+1]-TorsoZ[r])*fy;return At(r)+(At(r+1)-At(r))*fy;
        }
        private static Vector3 OnTorso(float th,float y,float lift){float r=Torso(th,y,out float z)+lift;return new Vector3(MathF.Sin(th)*r,y,z+MathF.Cos(th)*r);}
        private static Vector3 Mirror(Vector3 v)=>new Vector3(-v.X,v.Y,v.Z);
        private static float Smooth(float t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
        private static float D(float degrees)=>degrees*MathF.PI/180;

        // A limb's frame at rest: its root, its axis toward the next joint, and a bearing reference square to it.
        private readonly struct Limb
        {
            public readonly Vector3 O,A,R,S;
            public Limb(Vector3 from,Vector3 to,Vector3 reference){O=from;A=Vector3.Normalize(to-from);R=Vector3.Normalize(reference-A*Vector3.Dot(reference,A));S=Vector3.Cross(A,R);}
            public Vector3 At(float t,float th,float r,float cu=0,float cv=0)=>O+A*t+R*(cu+MathF.Cos(th)*r)+S*(cv+MathF.Sin(th)*r);
            public Vector3 Out(float th)=>R*MathF.Cos(th)+S*MathF.Sin(th);
        }
        // A part on both sides: built on the left, then its mirror on the right.
        private static void Both(Kit k,string name,int left,Action<SheldonSection,Func<Vector3,Vector3>> build)
        {
            build(k.Part(name+" L",left),v=>v);build(k.Part(name+" R",left+1),Mirror);
        }
        // A box on three axes (half sizes), each face painted by `face` (0 +x, 1 -x, 2 +y, 3 -y, 4 +z, 5 -z).
        internal static void Box(Kit k,SheldonSection s,Vector3 c,Vector3 ax,Vector3 ay,Vector3 az,Vector3 half,Func<int,Paint> face,Func<Vector3,Vector3> map=null)
        {
            map??=v=>v;Vector3 X=ax*half.X,Y=ay*half.Y,Z=az*half.Z;
            void F(int i,Vector3 n,Vector3 a,Vector3 b,Vector3 d,Vector3 e)
            {
                var p=face(i);var nn=map(c+n)-map(c);nn=Vector3.Normalize(nn);
                int f=s.Positions.Count;k.Vertex(s,map(c+a),nn,p);k.Vertex(s,map(c+b),nn,p);k.Vertex(s,map(c+d),nn,p);k.Vertex(s,map(c+e),nn,p);Kit.Quad(s,f,f+1,f+2,f+3);
            }
            F(0,ax,X-Y-Z,X+Y-Z,X+Y+Z,X-Y+Z);F(1,-ax,-X-Y-Z,-X-Y+Z,-X+Y+Z,-X+Y-Z);
            F(2,ay,-X+Y-Z,-X+Y+Z,X+Y+Z,X+Y-Z);F(3,-ay,-X-Y-Z,X-Y-Z,X-Y+Z,-X-Y+Z);
            F(4,az,-X-Y+Z,X-Y+Z,X+Y+Z,-X+Y+Z);F(5,-az,-X-Y-Z,-X+Y-Z,X+Y-Z,X-Y-Z);
        }

        /// <summary>The suit's parts (rest pose, one per piece and side), each on its bone's index (Bones).</summary>
        public static SheldonSection[] Build()
        {
            var k=new Kit();
            ChestPlate(k);BackPlate(k);Belt(k);Collar(k);Ears(k);Pauldrons(k);Vambraces(k);Legs(k);
            return k.Parts.ToArray();
        }

        // ---- the chest: a plate over the pectorals under a V at the throat, a dark seam across it, a light strip along its
        // foot ----
        private static void ChestPlate(Kit k)
        {
            var s=k.Part("chest",Chest);const float half=62;
            float Th(float u)=>D((u*2-1)*half);
            float Bottom(float th){float e=MathF.Abs(th)/D(half);return 1.252f+.02f*e*e;}
            float Top(float th){float e=MathF.Abs(th)/D(half);return 1.422f-.028f*MathF.Pow(e,1.5f)-.022f*MathF.Exp(-MathF.Pow(th/D(13),2));}
            Vector3 P(float u,float v){float th=Th(u),y=Bottom(th)+(Top(th)-Bottom(th))*v;return OnTorso(th,y,Gap+Thick*.5f+.007f*MathF.Sin(MathF.PI*v)*MathF.Cos(th*1.4f));}
            Vector3 Out(float u,float v){float th=Th(u);return new Vector3(MathF.Sin(th),0,MathF.Cos(th));}
            k.Slab(s,26,8,false,Thick,P,Out,(u,v)=>MathF.Abs(v-.5f)<.05f?Metal(.8f):Plate(.97f+.03f*v),(u,v)=>Metal(.6f));
            // The light strip along the foot and a short bar either side of the throat.
            k.Slab(s,26,1,false,.002f,(u,v)=>{var p=P(.04f+u*.92f,.05f+v*.05f);var n=Out(.04f+u*.92f,.08f);return p+n*(Thick*.5f+.001f);},Out,(u,v)=>Light,(u,v)=>Light);
            foreach(float side in new[]{-1f,1f})
                k.Slab(s,8,1,false,.002f,(u,v)=>{float uu=.5f+side*(.14f+u*.16f);var p=P(uu,.86f+v*.05f);return p+Out(uu,.88f)*(Thick*.5f+.001f);},Out,(u,v)=>Soft,(u,v)=>Soft);
        }

        // ---- the back: a plate across the shoulder blades carrying the robot's reactor panel, its glowing cross ----
        private static void BackPlate(Kit k)
        {
            var s=k.Part("back",Chest);const float half=56;
            float Th(float u)=>MathF.PI+D((u*2-1)*half);
            Vector3 P(float u,float v){float th=Th(u),e=MathF.Abs(th-MathF.PI)/D(half),y=1.222f+.016f*e*e+(.206f-.03f*e*e-.016f*e*e)*v;return OnTorso(th,y,Gap+Thick*.5f+.005f*MathF.Sin(MathF.PI*v));}
            Vector3 Out(float u,float v){float th=Th(u);return new Vector3(MathF.Sin(th),0,MathF.Cos(th));}
            k.Slab(s,24,8,false,Thick,P,Out,(u,v)=>MathF.Abs(v-.18f)<.05f?Metal(.8f):Plate(.95f),(u,v)=>Metal(.6f));
            // The reactor: a dark housing standing off the plate, its screen with vents either side and the cross alight.
            var at=P(.5f,.55f);var back=-Vector3.UnitZ;var c=at+back*(Thick*.5f+.012f);
            Box(k,s,c,Vector3.UnitX,Vector3.UnitY,back,new Vector3(.05f,.06f,.012f),f=>f==4?new Paint(Screen):Metal(f==2?.9f:.7f));
            var face=c+back*.0125f;
            Box(k,s,face,Vector3.UnitX,Vector3.UnitY,back,new Vector3(.034f,.0055f,.0015f),f=>Light);
            Box(k,s,face,Vector3.UnitX,Vector3.UnitY,back,new Vector3(.0055f,.044f,.0015f),f=>Light);
            foreach(float side in new[]{-1f,1f})for(int i=0;i<4;i++)
                Box(k,s,face+new Vector3(side*.042f,-.03f+i*.02f,0),Vector3.UnitX,Vector3.UnitY,back,new Vector3(.004f,.0035f,.0008f),f=>new Paint(Darker));
            // A plate rim round the housing.
            Box(k,s,c+back*-.004f,Vector3.UnitX,Vector3.UnitY,back,new Vector3(.056f,.066f,.004f),f=>Plate(.85f));
        }

        // ---- the waist: a dark belt with the robot's glowing band round it and a buckle plate ahead ----
        private static void Belt(Kit k)
        {
            var s=k.Part("belt",Hips);
            Vector3 P(float u,float v){float th=u*MathF.PI*2,y=.976f+.052f*v;return OnTorso(th,y,Gap*.85f+.004f);}
            Vector3 Out(float u,float v){float th=u*MathF.PI*2;return new Vector3(MathF.Sin(th),0,MathF.Cos(th));}
            k.Slab(s,48,5,true,.009f,P,Out,(u,v)=>v>.35f&&v<.65f?Light:Metal(v<.15f||v>.85f?.7f:1),(u,v)=>Metal(.5f));
            var front=P(0,.5f)+Vector3.UnitZ*.008f;
            Box(k,s,front,Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ,new Vector3(.034f,.026f,.006f),f=>f==4?Plate(1):Plate(.8f));
            Box(k,s,front+Vector3.UnitZ*.0065f,Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ,new Vector3(.012f,.004f,.0008f),f=>Light);
        }

        // ---- the neck: the robot's segmented dark collar, three ribs, a light ring at its top ----
        private static void Collar(Kit k)
        {
            var s=k.Part("collar",Neck);var neck=new Limb(NeckRoot,HeadRoot,Vector3.UnitZ);
            Vector3 P(float u,float v){float th=u*MathF.PI*2,t=-.014f+.046f*v,rib=.0035f*MathF.Pow(MathF.Abs(MathF.Sin(v*MathF.PI*3)),.6f);float front=.051f-.009f*v,side=.06f-.016f*v;
                float r=front*side/MathF.Sqrt(MathF.Pow(side*MathF.Cos(th),2)+MathF.Pow(front*MathF.Sin(th),2));return neck.At(t,th,r+rib,.008f);}
            k.Slab(s,32,9,true,.006f,P,(u,v)=>neck.Out(u*MathF.PI*2),(u,v)=>v>.9f?Soft:Metal(.75f+.25f*MathF.Abs(MathF.Sin(v*MathF.PI*3))),(u,v)=>Metal(.5f));
        }

        // ---- the head: the robot's square ear modules over the ears, each with its light ----
        private static void Ears(Kit k)
        {
            var s=k.Part("ears",Head);
            foreach(float side in new[]{-1f,1f})
            {
                var c=HeadRoot+new Vector3(side*.121f,.066f,.012f);var ax=new Vector3(side,0,0);
                Box(k,s,c,ax,Vector3.UnitY,Vector3.UnitZ,new Vector3(.011f,.026f,.024f),f=>f==0?Plate(1):Plate(.82f));
                Box(k,s,c+ax*.0115f,ax,Vector3.UnitY,Vector3.UnitZ,new Vector3(.0012f,.017f,.015f),f=>new Paint(Screen));
                Box(k,s,c+ax*.0128f+new Vector3(0,.004f,.004f),ax,Vector3.UnitY,Vector3.UnitZ,new Vector3(.0008f,.0055f,.0055f),f=>Light);
                // A dark band from the module back into the hair.
                Box(k,s,c+new Vector3(-side*.004f,.0f,-.032f),ax,Vector3.UnitY,Vector3.UnitZ,new Vector3(.005f,.008f,.012f),f=>Metal(.7f));
            }
        }

        // ---- the shoulders: rounded pauldrons over each shoulder's top and outside, a light strip along the edge ----
        private static void Pauldrons(Kit k)
        {
            var arm=new Limb(UpperArm,Forearm,new Vector3(-.7985f,.602f,.0069f));
            float[] t={-.058f,-.04f,-.016f,.012f,.042f,.072f,.098f},r={.028f,.06f,.08f,.09f,.093f,.094f,.098f};
            float R(float v){float x=v*(t.Length-1);int i=Math.Min(t.Length-2,(int)x);float f=x-i;return r[i]+(r[i+1]-r[i])*f;}
            float T(float v){float x=v*(t.Length-1);int i=Math.Min(t.Length-2,(int)x);float f=x-i;return t[i]+(t[i+1]-t[i])*f;}
            Both(k,"pauldron",ArmL,(s,map)=>
            {
                Vector3 P(float u,float v){float th=D((u*2-1)*112);return map(arm.At(T(v),th,R(v)+Thick*.5f));}
                Vector3 Out(float u,float v){float th=D((u*2-1)*112);var o=arm.Out(th)*.8f-arm.A*(1-v)*.6f;return map(arm.O+o)-map(arm.O);}
                k.Slab(s,20,10,false,Thick,P,Out,(u,v)=>MathF.Abs(v-.62f)<.05f?Metal(.8f):Plate(1-.05f*v),(u,v)=>Metal(.6f));
                k.Slab(s,20,1,false,.002f,(u,v)=>{float uu=.06f+u*.88f,th=D((uu*2-1)*112);return map(arm.At(T(.9f+v*.06f),th,R(.93f)+Thick+.001f));},Out,(u,v)=>Light,(u,v)=>Light);
            });
        }

        // ---- the forearms: squared vambraces, a seam and a light ring (the right one stops short of the wrist device) ----
        private static void Vambraces(Kit k)
        {
            var arm=new Limb(Forearm,Hand,new Vector3(-.7951f,.5285f,-.2975f));
            foreach(int side in new[]{0,1})
            {
                var s=k.Part(side==0?"vambrace L":"vambrace R",ForearmL+side);Func<Vector3,Vector3> map=side==0?v=>v:Mirror;
                float t0=.034f,t1=side==0?.168f:.122f;
                Vector3 P(float u,float v){float th=u*MathF.PI*2,t=t0+(t1-t0)*v,w=(t-.035f)/.135f,r=.061f-.004f*w*w-.006f*MathF.Max(0,w-.6f)*2.5f;
                    float sq=1/MathF.Pow(MathF.Pow(MathF.Abs(MathF.Cos(th)),3)+MathF.Pow(MathF.Abs(MathF.Sin(th)),3),1/3f);return map(arm.At(t,th,r*Math.Min(1.12f,sq)*.94f+.006f*MathF.Exp(-MathF.Pow(th/.25f,2))));}
                Vector3 Out(float u,float v)=>map(arm.O+arm.Out(u*MathF.PI*2))-map(arm.O);
                k.Slab(s,24,6,true,.007f,P,Out,(u,v)=>MathF.Abs(v-.5f)<.09f?Metal(.8f):v>.8f?Soft:Plate(1),(u,v)=>Metal(.5f));
            }
        }

        // ---- the legs: thigh plates ahead, knee caps, shin guards with a keel, light strips ----
        private static void Legs(Kit k)
        {
            var thigh=new Limb(Thigh,Calf,Vector3.UnitZ);var shin=new Limb(Calf,Foot,Vector3.UnitZ);
            Both(k,"thigh",ThighL,(s,map)=>
            {
                Vector3 P(float u,float v){float th=D((u*2-1)*72),t=.1f+.205f*v,r=.084f-.065f*(t-.1f)+.013f+Thick*.5f+.004f*MathF.Sin(MathF.PI*v);return map(thigh.At(t,th,r,-.008f));}
                Vector3 Out(float u,float v)=>map(thigh.O+thigh.Out(D((u*2-1)*72)))-map(thigh.O);
                k.Slab(s,16,7,false,Thick,P,Out,(u,v)=>MathF.Abs(v-.3f)<.06f?Metal(.8f):Plate(1),(u,v)=>Metal(.6f));
                k.Slab(s,16,1,false,.002f,(u,v)=>{float uu=.08f+u*.84f;return P(uu,.93f+v*.05f)+Vector3.Normalize(Out(uu,.95f))*(Thick*.5f+.001f);},Out,(u,v)=>Light,(u,v)=>Light);
            });
            Both(k,"knee",ShinL,(s,map)=>
            {
                Vector3 P(float u,float v){float th=D((u*2-1)*66),t=-.05f+.105f*v,w=Math.Max(0,MathF.Sin(MathF.PI*Math.Clamp(v*1.05f,0,1))),r=.046f+.026f*MathF.Pow(w,.55f)+.012f*w*MathF.Cos(th);return map(shin.At(t,th,r,-.013f));}
                Vector3 Out(float u,float v)=>map(shin.O+shin.Out(D((u*2-1)*66))-shin.A*(v-.5f)*1.2f)-map(shin.O);
                k.Slab(s,14,8,false,Thick,P,Out,(u,v)=>MathF.Abs(v-.62f)<.06f?Metal(.8f):Plate(1),(u,v)=>Metal(.6f));
            });
            Both(k,"shin",ShinL,(s,map)=>
            {
                Vector3 P(float u,float v){float th=D((u*2-1)*86),t=.078f+.265f*v,r=.064f+Thick*.5f-.004f*v+.007f*MathF.Exp(-MathF.Pow(th/.22f,2));return map(shin.At(t,th,r,-.016f));}
                Vector3 Out(float u,float v)=>map(shin.O+shin.Out(D((u*2-1)*86)))-map(shin.O);
                k.Slab(s,16,9,false,Thick,P,Out,(u,v)=>MathF.Abs(v-.78f)<.05f?Metal(.8f):Plate(1),(u,v)=>Metal(.6f));
                k.Slab(s,2,6,false,.002f,(u,v)=>{float uu=.47f+u*.06f,vv=.08f+v*.55f;return P(uu,vv)+Vector3.Normalize(Out(uu,vv))*(Thick*.5f+.0035f);},Out,(u,v)=>Light,(u,v)=>Light);
            });
        }
    }
}
