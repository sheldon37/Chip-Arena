using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Substation 128's art poses (docs/arenas/STAGE-ART.md, docs/arenas/CLUB.md "Stage art"): where each modelled rig part of the
    /// stage bank ChipArenaStageClub (tools/stages/club_art.py and club_props.py; the frames are listed there) stands this frame,
    /// read only from Core NativeClubRig's outputs after its Build, so the models move exactly as the box rig did: the stacks'
    /// cones pumping with the kick and thrown out at the drop, the sixteen moving heads panning and tilting where the show aims
    /// them (their lenses in the show's colour), the laser wall's emitter posts rising and riding the pass, the mirror balls
    /// turning, and the Greys: the DJ nodding on the beat with a hand at the headphones and the other on the mixer or thrown up
    /// through the build and the drop, and the crowd of Grey aliens in the galleries and the pit bobbing, swaying and throwing
    /// their long arms up. Pure and allocation free per frame.
    /// </summary>
    public static class NativeSubstationArt
    {
        /// <summary>The bank and the rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public const string Bank="ChipArenaStageClub";public const int Variants=4;
        public static readonly string[] Slots={"cones-w","cones-e","yoke-hang","yoke-stand","head","laser-post","ball",
            "dj-legs","dj-torso","dj-head","dj-arm-ear","dj-upper","dj-fore",
            "grey-legs-0","grey-legs-1","grey-legs-2","grey-legs-3","grey-body-0","grey-body-1","grey-body-2","grey-body-3","grey-arm-0","grey-arm-1","grey-arm-2","grey-arm-3"};
        public const int Cones=0,YokeHang=2,YokeStand=3,Head=4,LaserPost=5,Ball=6,DjLegs=7,DjTorso=8,DjHead=9,DjArmEar=10,DjUpper=11,DjFore=12,GreyLegs=13,GreyBody=17,GreyArm=21;
        /// <summary>The slots that cast shadows: the fixtures, the posts, the balls and the DJ; not the cones (inside their
        /// cabinets) nor the crowd (beyond the hall's walls, 256 draws).</summary>
        public static bool Casts(int slot)=>slot>=YokeHang&&slot<GreyLegs;
        /// <summary>A Grey's build at height 1 (the models' own, tools/stages/club_props.py): the hip joint over the feet, the
        /// shoulders over the hip and out from the spine.</summary>
        public const double Hip=.84,Shoulder=.5,ShoulderOut=.24;
        /// <summary>The DJ's left arm: the upper arm's length (shoulder to elbow) and the elbow's bend at the mixer, straightening
        /// as the arm goes up.</summary>
        public const double DjUpperArm=.3,DjElbow=1.15,DjElbowUp=.2;
        /// <summary>A crowd figure's look (skin tone and outfit), stable per figure.</summary>
        public static int Variant(int k)=>(int)(NativeClubLooks.Hash(k,17,5)*Variants)%Variants;

        public static void Pose(NativeClubRig rig,NativeClubShow show,double render,NativeStageArtPose into)
        {
            into.Clear();if(rig==null||show==null)return;var I=FactoryQ.Identity;
            // the stacks' cones: each stack's six woofers as one piece in the world's frame, thrown along -z by the pump; the
            // dust caps' light is the build's and the burst's
            for(int s=0;s<2;s++)into.Add(Cones+s,0,0,-rig.ConePump[s],I,1,rig.ConeGlow[s*3],rig.ConeGlow[s*3+1],rig.ConeGlow[s*3+2]);
            // the moving heads: the yoke turned to the pan (hung under the truss, or standing on a tower top), the head along
            // the aim, its lens in the show's colour
            for(int k=0;k<NativeClubLooks.Heads;k++)
            {
                double hx=show.HeadAt[k*3],hy=show.HeadAt[k*3+1],hz=show.HeadAt[k*3+2];bool top=k>=12;double p=show.HeadPower[k]*3.2;
                into.Add(top?YokeStand:YokeHang,hx,hy,hz,FactoryQ.Axis(0,1,0,rig.HeadYaw[k]));
                into.Add(Head,hx,hy,hz,FactoryQ.YawPitch(rig.HeadYaw[k],rig.HeadPitch[k]),1,show.HeadColor[k*3]*p+.2,show.HeadColor[k*3+1]*p+.2,show.HeadColor[k*3+2]*p+.2);
            }
            // the laser wall's emitter posts: up out of their slots, riding with the pass (the north one faces south, the south
            // one is the same model turned half round)
            if(rig.PostRise>.001)
            {
                double y=(rig.PostRise-1)*NativeClubLooks.LaserTop;
                into.Add(LaserPost,rig.PostX,y,NativeClubLooks.LaserRailZ,I,1,5,.25,.3);into.Add(LaserPost,rig.PostX,y,-NativeClubLooks.LaserRailZ,FactoryQ.Axis(0,1,0,Math.PI),1,5,.25,.3);
            }
            // the mirror balls, turning opposite ways
            double turn=NativeClubRig.BallTurn(render);
            into.Add(Ball,-NativeClubLooks.BallX,NativeClubLooks.BallY,NativeClubLooks.BallZ,FactoryQ.Axis(0,1,0,-turn));into.Add(Ball,NativeClubLooks.BallX,NativeClubLooks.BallY,NativeClubLooks.BallZ,FactoryQ.Axis(0,1,0,turn));
            Dj(rig,into);
            // the crowd
            for(int k=0;k<NativeClubRig.CrowdMax;k++)
            {
                int o=k*NativeClubRig.CrowdStride;double tall=rig.CrowdPose[o+4];if(tall<=0)continue;
                double x=rig.CrowdPose[o],y=rig.CrowdPose[o+1],z=rig.CrowdPose[o+2],yaw=rig.CrowdPose[o+3],bob=rig.CrowdPose[o+5],sway=rig.CrowdPose[o+6];bool up=rig.CrowdPose[o+9]>.5;
                // (a pit figure is turned half round from the rig's yaw: its left arm is the rig's right one, mirrored)
                double armL=rig.CrowdPose[o+7],armR=rig.CrowdPose[o+8];if(k>=48){double swap=armL;armL=-armR;armR=-swap;sway=-sway;}
                Grey(into,Variant(k),x,y,z,Facing(k,yaw),tall,bob,sway,armL,armR,up);
            }
        }
        /// <summary>The way a crowd figure faces (its model looks along its own +z): the galleries' along the rig's yaw, into the
        /// hall; the pit's toward the stage (the rig's yaw there points a symmetric box the other way round).</summary>
        public static double Facing(int k,double yaw)=>k<48?yaw:yaw-Math.PI;
        /// <summary>One Grey: the legs from the feet (stretched to the bobbing hip), the body and head from the hip leaning with
        /// the sway, the two arms from the shoulders (hanging a little out, or thrown up and waving).</summary>
        public static void Grey(NativeStageArtPose into,int variant,double x,double y,double z,double facing,double tall,double bob,double sway,double armL,double armR,bool up)
        {
            var q=FactoryQ.Axis(0,1,0,facing);var lean=q*FactoryQ.Axis(0,0,1,sway);double hip=Hip*tall+bob;
            into.Add(GreyLegs+variant,x,y,z,q,hip/Hip);
            into.Add(GreyBody+variant,x,y+hip,z,lean,tall);
            q.Rotate(1,0,0,out double rx,out _,out double rz);double sy=y+hip+Shoulder*tall;
            for(int side=0;side<2;side++)
            {
                double sd=side==0?-1:1,a=side==0?armL:armR;
                // the model hangs down its -y from the shoulder: hanging, the rig's small outward angle; up, turned over (pi) and waving
                into.Add(GreyArm+variant,x+rx*sd*ShoulderOut,sy,z+rz*sd*ShoulderOut,q*FactoryQ.Axis(0,0,1,up?a+Math.PI:a),tall);
            }
        }
        // The DJ (the model faces the hall, -z): the legs on the stage, the torso, the head nodding on the beat (dipping and
        // tipping forward), the right arm bent to the headphones, the left arm from the shoulder (upper arm) and the elbow
        // (forearm and hand): on the mixer, or thrown up through the build and the drop.
        private static void Dj(NativeClubRig rig,NativeStageArtPose into)
        {
            double x=NativeClubLooks.DjX,z=NativeClubLooks.DjZ,y0=NativeClubLooks.StageY,nod=rig.DjNod;var I=FactoryQ.Identity;
            into.Add(DjLegs,x,y0,z,I);into.Add(DjTorso,x,y0+1.25,z-.02*nod,I);
            into.Add(DjHead,x,y0+1.73-nod,z-.04,FactoryQ.Axis(1,0,0,-nod*3));
            into.Add(DjArmEar,x+.26,y0+1.5-nod*.5,z-.05,I);
            var arm=FactoryQ.Axis(1,0,0,rig.DjArm);double sx=x-.27,sy=y0+1.55,sz=z;into.Add(DjUpper,sx,sy,sz,arm);
            arm.Rotate(0,-DjUpperArm,0,out double ox,out double oy,out double oz);
            double bend=DjElbow+(DjElbowUp-DjElbow)*NativeClubLooks.Clamp01(rig.DjLift);
            into.Add(DjFore,sx+ox,sy+oy,sz+oz,FactoryQ.Axis(1,0,0,rig.DjArm+bend));
        }
    }
}
