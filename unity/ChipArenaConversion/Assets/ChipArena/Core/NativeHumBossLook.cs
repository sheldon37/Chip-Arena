using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE HUM's skeleton (tools/gear/hum_boss.py models its body on these joints; Tests/HumBossChecks holds the two tables to each other). A joint is its bind
    /// position in the root's frame at the neutral stoop; every mesh of the bank lies in that space, so a joint's world matrix for skinning is its posed place, its
    /// <see cref="BossPose"/> rotation and minus its bind position: the rig's rotations are turns away from the bind pose, not limb frames.
    /// </summary>
    public static class NativeHumBossLook
    {
        public const int Root=0,Pelvis=1,Spine0=2,Spine1=3,Chest=4,Neck0=5,Neck1=6,Neck2=7,Head=8,JawU=9,JawL=10,
            ClavL=11,UpperL=12,ForeL=13,HandL=14,ClavR=15,UpperR=16,ForeR=17,HandR=18,ThighL=19,ShinL=20,FootL=21,ThighR=22,ShinR=23,FootR=24,RibL=25,RibR=26,Ballast=27,
            FingerFirst=28,TubeFirst=52,WireFirst=59,StripFirst=73,TileFirst=113,WallFore=117,WallHand=118,WallFingerFirst=119,Count=131;
        public const int Fingers=4,FingerLinks=3,Tubes=7,WireLinks=2,Strips=10,StripLinks=4,Tiles=4;
        /// <summary>Hand h (0 left, 1 right), finger f (0 the innermost toward the body's middle on the left hand's table order), link k.</summary>
        public static int Finger(int h,int f,int k)=>FingerFirst+(h*Fingers+f)*FingerLinks+k;
        public static int WallFinger(int f,int k)=>WallFingerFirst+f*FingerLinks+k;public static int Tube(int i)=>TubeFirst+i;public static int Wire(int i,int k)=>WireFirst+i*WireLinks+k;public static int Strip(int s,int k)=>StripFirst+s*StripLinks+k;public static int Tile(int i)=>TileFirst+i;
        public static readonly int[] Parent=new int[Count];public static readonly Vector3[] Bind=new Vector3[Count],Rest=new Vector3[Count];public static readonly string[] Names=new string[Count];
        /// <summary>The limbs' lengths (m): upper arm, forearm, thigh, shin; each wallpaper strip's length (a link is a quarter of it); a wire tail's link.</summary>
        public const float UpperArm=1.62f,ForeArm=1.62f,Thigh=1f,Shin=1.02f,WireLength=.24f;
        public static readonly float[] StripLength={1.82f,1.56f,1.94f,1.46f,1.40f,1.72f,1.50f,1.34f,1.62f,1.20f};
        /// <summary>The hands' and feet's ankle height over the floor in the bind pose (the hand stands up on its arched fingers).</summary>
        public const float WristHeight=.4f,AnkleHeight=.42f;
        /// <summary>The limbs' knee and elbow poles in the root's frame at the bind pose.</summary>
        public static readonly Vector3 ArmPoleL=new Vector3(-.85f,.35f,-.4f),LegPoleL=new Vector3(-.03f,0,1);
        /// <summary>Each finger link's bind pitch (degrees, + up), its yaw off the hand's forward (left hand, degrees: the right mirrors) and length.</summary>
        public static readonly float[,] FingerPitch={{8,-36,-72},{12,-38,-70},{10,-40,-72},{6,-32,-68}};
        public static readonly float[] FingerYawL={34,12,-8,-31};
        /// <summary>The bind turn of each limb bone (its frame at the bind pose: local -Y down the bone, +X the hinge): the rig's pose takes limb frames over this.</summary>
        public static readonly Quaternion[] BindLimb=new Quaternion[Count];
        static NativeHumBossLook()
        {
            void J(int i,string name,int parent,float x,float y,float z){Names[i]=name;Parent[i]=parent;Bind[i]=new Vector3(x,y,z);Rest[i]=parent<0?Bind[i]:Bind[i]-Bind[parent];BindLimb[i]=Quaternion.Identity;}
            J(0,"root",-1,0f,0f,0f);
            J(1,"pelvis",0,0f,2.05f,-0.62f);
            J(2,"spine0",1,0f,2.62f,-0.62f);
            J(3,"spine1",2,0f,3.14f,-0.44f);
            J(4,"chest",3,0f,3.5f,-0.06f);
            J(5,"neck0",4,0f,3.5f,0.3f);
            J(6,"neck1",5,0f,3.42f,0.62f);
            J(7,"neck2",6,0f,3.35f,0.95f);
            J(8,"head",7,0f,3.3f,1.24f);
            J(9,"jaw-u",8,0f,3.49f,1.38f);
            J(10,"jaw-l",8,0f,3.11f,1.38f);
            J(11,"clav-l",4,-0.15f,3.47f,0.08f);
            J(12,"upper-l",11,-0.42f,3.3f,0.15f);
            J(13,"fore-l",12,-1.2571f,1.913f,0.1493f);
            J(14,"hand-l",13,-0.92f,0.4f,0.62f);
            J(15,"clav-r",4,0.15f,3.47f,0.08f);
            J(16,"upper-r",15,0.42f,3.3f,0.15f);
            J(17,"fore-r",16,1.2571f,1.913f,0.1493f);
            J(18,"hand-r",17,0.92f,0.4f,0.62f);
            J(19,"thigh-l",1,-0.26f,2.02f,-0.62f);
            J(20,"shin-l",19,-0.3221f,1.144f,-0.1417f);
            J(21,"foot-l",20,-0.34f,0.42f,-0.86f);
            J(22,"thigh-r",1,0.26f,2.02f,-0.62f);
            J(23,"shin-r",22,0.3221f,1.144f,-0.1417f);
            J(24,"foot-r",23,0.34f,0.42f,-0.86f);
            J(25,"rib-l",4,-0.1f,3.42f,-0.3f);
            J(26,"rib-r",4,0.1f,3.42f,-0.3f);
            J(27,"ballast",4,0f,3.04f,-0.12f);
            J(28,"finger-l0-0",14,-0.805f,0.46f,0.7912f);
            J(29,"finger-l0-1",28,-0.6281f,0.5157f,1.1457f);
            J(30,"finger-l0-2",29,-0.4834f,0.3276f,1.3603f);
            J(31,"finger-l1-0",14,-0.88f,0.46f,0.81f);
            J(32,"finger-l1-1",31,-0.8084f,0.5536f,1.2443f);
            J(33,"finger-l1-2",32,-0.7494f,0.3319f,1.5218f);
            J(34,"finger-l2-0",14,-0.96f,0.46f,0.81f);
            J(35,"finger-l2-1",34,-1.006f,0.5347f,1.231f);
            J(36,"finger-l2-2",35,-1.0433f,0.3097f,1.4965f);
            J(37,"finger-l3-0",14,-1.035f,0.46f,0.7912f);
            J(38,"finger-l3-1",37,-1.1816f,0.4976f,1.1179f);
            J(39,"finger-l3-2",38,-1.3083f,0.344f,1.3287f);
            J(40,"finger-r0-0",18,0.805f,0.46f,0.7912f);
            J(41,"finger-r0-1",40,0.6281f,0.5157f,1.1457f);
            J(42,"finger-r0-2",41,0.4834f,0.3276f,1.3603f);
            J(43,"finger-r1-0",18,0.88f,0.46f,0.81f);
            J(44,"finger-r1-1",43,0.8084f,0.5536f,1.2443f);
            J(45,"finger-r1-2",44,0.7494f,0.3319f,1.5218f);
            J(46,"finger-r2-0",18,0.96f,0.46f,0.81f);
            J(47,"finger-r2-1",46,1.006f,0.5347f,1.231f);
            J(48,"finger-r2-2",47,1.0433f,0.3097f,1.4965f);
            J(49,"finger-r3-0",18,1.035f,0.46f,0.7912f);
            J(50,"finger-r3-1",49,1.1816f,0.4976f,1.1179f);
            J(51,"finger-r3-2",50,1.3083f,0.344f,1.3287f);
            J(52,"tube-0",1,0f,2.3146f,-0.9235f);
            J(53,"tube-1",2,0f,2.644f,-0.9223f);
            J(54,"tube-2",2,0f,2.9676f,-0.8267f);
            J(55,"tube-3",3,0f,3.243f,-0.6404f);
            J(56,"tube-4",3,0f,3.4977f,-0.4043f);
            J(57,"tube-5",4,0f,3.6791f,-0.1253f);
            J(58,"tube-6",4,0f,3.7305f,0.1717f);
            J(59,"wire-0-0",52,0.1317f,2.0038f,-0.8826f);
            J(60,"wire-0-1",59,0.1317f,1.7638f,-0.8826f);
            J(61,"wire-1-0",53,-0.1317f,2.339f,-0.9945f);
            J(62,"wire-1-1",61,-0.1317f,2.099f,-0.9945f);
            J(63,"wire-2-0",54,0.1317f,2.708f,-1.0023f);
            J(64,"wire-2-1",63,0.1317f,2.468f,-1.0023f);
            J(65,"wire-3-0",55,-0.1317f,2.9834f,-0.816f);
            J(66,"wire-3-1",65,-0.1317f,2.7434f,-0.816f);
            J(67,"wire-4-0",56,0.1317f,3.2723f,-0.6222f);
            J(68,"wire-4-1",67,0.1317f,3.0323f,-0.6222f);
            J(69,"wire-5-0",57,-0.1317f,3.5456f,-0.409f);
            J(70,"wire-5-1",69,-0.1317f,3.3056f,-0.409f);
            J(71,"wire-6-0",58,0.1317f,3.8166f,-0.1297f);
            J(72,"wire-6-1",71,0.1317f,3.5766f,-0.1297f);
            J(73,"strip-0-0",11,-0.46f,3.44f,0.02f);
            J(74,"strip-0-1",73,-0.46f,2.985f,0.02f);
            J(75,"strip-0-2",74,-0.46f,2.53f,0.02f);
            J(76,"strip-0-3",75,-0.46f,2.075f,0.02f);
            J(77,"strip-1-0",15,0.46f,3.44f,0.02f);
            J(78,"strip-1-1",77,0.46f,3.05f,0.02f);
            J(79,"strip-1-2",78,0.46f,2.66f,0.02f);
            J(80,"strip-1-3",79,0.46f,2.27f,0.02f);
            J(81,"strip-2-0",4,-0.26f,3.54f,-0.3f);
            J(82,"strip-2-1",81,-0.26f,3.055f,-0.3f);
            J(83,"strip-2-2",82,-0.26f,2.57f,-0.3f);
            J(84,"strip-2-3",83,-0.26f,2.085f,-0.3f);
            J(85,"strip-3-0",4,0.24f,3.52f,-0.28f);
            J(86,"strip-3-1",85,0.24f,3.155f,-0.28f);
            J(87,"strip-3-2",86,0.24f,2.79f,-0.28f);
            J(88,"strip-3-3",87,0.24f,2.425f,-0.28f);
            J(89,"strip-4-0",3,-0.27f,3.04f,-0.5f);
            J(90,"strip-4-1",89,-0.27f,2.69f,-0.5f);
            J(91,"strip-4-2",90,-0.27f,2.34f,-0.5f);
            J(92,"strip-4-3",91,-0.27f,1.99f,-0.5f);
            J(93,"strip-5-0",3,0.29f,2.98f,-0.46f);
            J(94,"strip-5-1",93,0.29f,2.55f,-0.46f);
            J(95,"strip-5-2",94,0.29f,2.12f,-0.46f);
            J(96,"strip-5-3",95,0.29f,1.69f,-0.46f);
            J(97,"strip-6-0",1,-0.4f,2.2f,-0.52f);
            J(98,"strip-6-1",97,-0.4f,1.825f,-0.52f);
            J(99,"strip-6-2",98,-0.4f,1.45f,-0.52f);
            J(100,"strip-6-3",99,-0.4f,1.075f,-0.52f);
            J(101,"strip-7-0",1,0.4f,2.17f,-0.54f);
            J(102,"strip-7-1",101,0.4f,1.835f,-0.54f);
            J(103,"strip-7-2",102,0.4f,1.5f,-0.54f);
            J(104,"strip-7-3",103,0.4f,1.165f,-0.54f);
            J(105,"strip-8-0",1,-0.2f,2.16f,-0.84f);
            J(106,"strip-8-1",105,-0.2f,1.755f,-0.84f);
            J(107,"strip-8-2",106,-0.2f,1.35f,-0.84f);
            J(108,"strip-8-3",107,-0.2f,0.945f,-0.84f);
            J(109,"strip-9-0",1,0.24f,2.13f,-0.84f);
            J(110,"strip-9-1",109,0.24f,1.83f,-0.84f);
            J(111,"strip-9-2",110,0.24f,1.53f,-0.84f);
            J(112,"strip-9-3",111,0.24f,1.23f,-0.84f);
            J(113,"tile-0",25,-0.42f,2.96f,-0.12f);
            J(114,"tile-1",25,-0.4f,3.24f,0.06f);
            J(115,"tile-2",26,0.41f,2.9f,-0.06f);
            J(116,"tile-3",26,0.36f,2.72f,0.1f);
            J(117,"wall-fore",0,1.2571f,1.913f,0.1493f);
            J(118,"wall-hand",117,0.92f,0.4f,0.62f);
            J(119,"wall-finger-0-0",118,0.805f,0.46f,0.7912f);
            J(120,"wall-finger-0-1",119,0.6281f,0.5157f,1.1457f);
            J(121,"wall-finger-0-2",120,0.4834f,0.3276f,1.3603f);
            J(122,"wall-finger-1-0",118,0.88f,0.46f,0.81f);
            J(123,"wall-finger-1-1",122,0.8084f,0.5536f,1.2443f);
            J(124,"wall-finger-1-2",123,0.7494f,0.3319f,1.5218f);
            J(125,"wall-finger-2-0",118,0.96f,0.46f,0.81f);
            J(126,"wall-finger-2-1",125,1.006f,0.5347f,1.231f);
            J(127,"wall-finger-2-2",126,1.0433f,0.3097f,1.4965f);
            J(128,"wall-finger-3-0",118,1.035f,0.46f,0.7912f);
            J(129,"wall-finger-3-1",128,1.1816f,0.4976f,1.1179f);
            J(130,"wall-finger-3-2",129,1.3083f,0.344f,1.3287f);
            BindLimb[UpperL]=LimbFrame(Bind[UpperL],Bind[ForeL],Bind[HandL],ArmPoleL);BindLimb[ForeL]=LimbFrame2(Bind[UpperL],Bind[ForeL],Bind[HandL],ArmPoleL);
            var armR=new Vector3(-ArmPoleL.X,ArmPoleL.Y,ArmPoleL.Z);BindLimb[UpperR]=LimbFrame(Bind[UpperR],Bind[ForeR],Bind[HandR],armR);BindLimb[ForeR]=LimbFrame2(Bind[UpperR],Bind[ForeR],Bind[HandR],armR);
            BindLimb[ThighL]=LimbFrame(Bind[ThighL],Bind[ShinL],Bind[FootL],LegPoleL);BindLimb[ShinL]=LimbFrame2(Bind[ThighL],Bind[ShinL],Bind[FootL],LegPoleL);
            var legR=new Vector3(-LegPoleL.X,LegPoleL.Y,LegPoleL.Z);BindLimb[ThighR]=LimbFrame(Bind[ThighR],Bind[ShinR],Bind[FootR],legR);BindLimb[ShinR]=LimbFrame2(Bind[ThighR],Bind[ShinR],Bind[FootR],legR);
        }
        /// <summary>A limb bone's frame: local -Y along it, local +X on the hinge (the cross of the two bones), +Z the side it bends toward.</summary>
        public static Quaternion Limb(Vector3 from,Vector3 to,Vector3 hinge,Vector3 pole)
        {
            var d=to-from;float l=d.Length();if(l<1e-6f)return Quaternion.Identity;var y=-d/l;var x=hinge-y*Vector3.Dot(hinge,y);float xl=x.Length();
            if(xl<1e-5f)return BossMath.Bone(from,to,pole);x/=xl;var z=Vector3.Cross(x,y);return BossMath.FromBasis(x,y,z);
        }
        private static Quaternion LimbFrame(Vector3 a,Vector3 b,Vector3 c,Vector3 pole)=>Limb(a,b,Vector3.Cross(b-a,c-b),pole);
        private static Quaternion LimbFrame2(Vector3 a,Vector3 b,Vector3 c,Vector3 pole)=>Limb(b,c,Vector3.Cross(b-a,c-b),pole);
    }
}
