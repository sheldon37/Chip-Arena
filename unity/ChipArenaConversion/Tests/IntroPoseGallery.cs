using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The canonical poses of docs/INTROS-V2.md 9b as tiny keyed scripts (Core NativeIntroPoses: the bases, the walk and the run on the
    /// model's feet, the weapon holds with the grip IK), so IntroPoseChecks holds each to the anatomical limits, to the model and to the
    /// floor, as it holds the registered intros. The code here is the guide's: each entry is what an author writes in a constructor, in
    /// Arms and in GripAt.
    /// </summary>
    internal static class IntroPoseGallery
    {
        internal sealed class Pose:NativeIntroV2Script,INativeIntroGrip
        {
            public readonly string Name,Spec;public readonly float Seconds;private readonly Action<float,CapturedRig.Pose,Vector3,Quaternion> arms;private readonly Func<float,NativeIntroGripAt> grip;
            private readonly bool feet;private readonly Func<float,bool,float> kneel;
            protected override string WeaponSpec=>Spec;
            protected override bool ModelFeet=>feet;
            protected override float ModelKneel(float t,bool left)=>kneel?.Invoke(t,left)??0;
            public Pose(string name,string spec,float duration,Action<Pose,NativeEntranceKeys,List<NativeEntrancePose.Step>> build,Action<float,CapturedRig.Pose,Vector3,Quaternion> arms=null,
                Func<float,NativeIntroGripAt> grip=null,bool feet=true,Func<float,bool,float> kneel=null)
                :base("gallery-"+name,duration,duration*.8f,duration*.5f,0x9fd8ff)
            {
                Name=name;Spec=spec;Seconds=duration;this.arms=arms;this.grip=grip;this.feet=feet;this.kneel=kneel;var k=new NativeEntranceKeys();var plan=new List<NativeEntrancePose.Step>();
                build(this,k,plan);Keys=k;Steps=plan.ToArray();
            }
            public void Stance(float[] stance)=>Start=stance;
            public void Path(Func<float,Vector3,Vector3> path)=>RootPath=path;
            protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)=>arms?.Invoke(t,pose,root,rotation);
            public bool GripAt(float t,out NativeIntroGripAt at){at=grip?.Invoke(t)??default;return at.Weight>0;}
            /// <summary>(The checks' own reach into the stance helper.)</summary>
            public static float[] StanceOf(float lx,float lz,float lyaw,float rx,float rz,float ryaw,float facing=0)=>Stance(lx,lz,lyaw,rx,rz,ryaw,facing);
        }
        // Each pose held a second (a key at 0 and at 1.2 on the same basis, the living hold from 0) unless it moves.
        private static Pose Held(string name,string spec,float[] basis,float[] stance,Action<float,CapturedRig.Pose,Vector3,Quaternion> arms=null,Func<float,NativeIntroGripAt> grip=null,float yaw=0,Func<float,bool,float> kneel=null)
            =>new Pose(name,spec,1.2f,(p,k,plan)=>{p.Stance(stance);k.Key(0,basis).Root(0,.88f,0,yaw);k.Key(1.2f,basis).Root(0,.88f,0,yaw);},arms,grip,true,kneel);
        private static NativeIntroGripAt On(float pull=0,float shoulder=0)=>new NativeIntroGripAt(1,default,0,pull,shoulder);
        public static readonly string[] Names={"walk","run","kneel","crouch","landing","hero","hero-wide","sword-guard","greatsword-guard","katana-guard","rifle-aim","machinegun-aim","pistol-aim","bow-draw","staff-hold","shield-brace"};
        public static Pose Make(string name)
        {
            switch(name)
            {
                case "walk":return new Pose(name,null,4.2f,(p,k,plan)=>
                {
                    // Six steps from standing and a stop: the hips up to speed over .5 s, .65 m a step every .55 s, slowing to rest at 3.2 m;
                    // then a settle into the hero stance.
                    var line=new NativeIntroFeet.Track(Vector3.Zero,0,.15f,3.6f,3.2f,.5f,.6f);p.Path((t,r)=>{var a=line.At(t);return new Vector3(a.X,r.Y,a.Z);});
                    p.Stance(Pose.StanceOf(.1f,0,.08f,-.1f,0,-.08f));k.Key(0,NativeEntranceKeys.Stand()).Root(0,.88f,0,0);
                    float end=NativeIntroPoses.Walk(k,plan,line,.2f,6);
                    k.Key(end+.5f,NativeIntroPoses.Hero()).Root(0,.88f,3.2f,0);k.Key(4.2f,NativeIntroPoses.Hero()).Root(0,.88f,3.2f,0);
                });
                case "run":return new Pose(name,null,2.4f,(p,k,plan)=>
                {
                    // Six strides of a run already under way, 4 m/s (1.36 m a stride every .34 s).
                    var line=new NativeIntroFeet.Track(Vector3.Zero,0,-10,10,80,.01f,.01f);p.Path((t,r)=>{var a=line.At(t);return new Vector3(a.X,r.Y,a.Z);});
                    p.Stance(Pose.StanceOf(.08f,line.Along(0)+.22f,.05f,-.08f,line.Along(0)-.9f,-.05f));
                    NativeIntroPoses.Run(k,plan,line,0,6);
                });
                case "kneel":return Held(name,null,NativeIntroPoses.Kneel(),Pose.StanceOf(.13f,.42f,.1f,-.13f,-.1f,-.3f),kneel:(t,left)=>left?0:1);
                case "crouch":return Held(name,null,NativeIntroPoses.Crouch(),Pose.StanceOf(.15f,.1f,.2f,-.15f,-.12f,-.3f));
                case "landing":return Held(name,null,NativeIntroPoses.Landing(),Pose.StanceOf(.2f,.05f,.25f,-.2f,-.08f,-.35f));
                case "hero":return Held(name,null,NativeIntroPoses.Hero(),Pose.StanceOf(.13f,.04f,.15f,-.13f,-.04f,-.3f));
                case "hero-wide":return Held(name,null,NativeIntroPoses.HeroWide(),Pose.StanceOf(.24f,.02f,.3f,-.24f,-.02f,-.3f));
                case "sword-guard":return Held(name,"sword",NativeIntroPoses.Guard(),Pose.StanceOf(.12f,.2f,.15f,-.15f,-.2f,-.5f),(t,pose,root,rot)=>NativeIntroPoses.Guard(pose,"sword"));
                case "greatsword-guard":return Held(name,"greatsword",NativeIntroPoses.Guard(),Pose.StanceOf(.12f,.2f,.15f,-.15f,-.2f,-.5f),(t,pose,root,rot)=>NativeIntroPoses.Guard(pose,"greatsword"),t=>On());
                case "katana-guard":return Held(name,NativeKatana.Spec,NativeIntroPoses.Guard(),Pose.StanceOf(.12f,.2f,.15f,-.15f,-.2f,-.5f),(t,pose,root,rot)=>NativeIntroPoses.Guard(pose,NativeKatana.Spec),t=>On());
                case "rifle-aim":return Held(name,"rifle",NativeIntroPoses.RifleAim(),Pose.StanceOf(.14f,.18f,.3f,-.14f,-.16f,-.6f),(t,pose,root,rot)=>NativeIntroPoses.AimRifle(pose,"rifle"),t=>On(0,1));
                case "machinegun-aim":return Held(name,"machinegun",NativeIntroPoses.RifleAim(),Pose.StanceOf(.14f,.18f,.3f,-.14f,-.16f,-.6f),(t,pose,root,rot)=>NativeIntroPoses.AimRifle(pose,"machinegun",0,-.08f),t=>On(0,1));
                case "pistol-aim":return Held(name,"pistol",NativeIntroPoses.PistolAim(),Pose.StanceOf(.13f,.04f,.1f,-.13f,-.04f,-.1f),(t,pose,root,rot)=>NativeIntroPoses.AimPistol(pose),t=>On());
                case "bow-draw":return Held(name,"bow",NativeIntroPoses.BowDraw(),Pose.StanceOf(.13f,.05f,.4f,-.13f,-.05f,0,-1.45f),(t,pose,root,rot)=>NativeIntroPoses.DrawBow(pose,1.45f,0,NativeIntroPoses.BowAnchorPull),t=>On(NativeIntroPoses.BowAnchorPull),-1.45f);
                case "staff-hold":return Held(name,"fire",NativeIntroPoses.StaffHold(),Pose.StanceOf(.12f,.2f,.15f,-.15f,-.2f,-.5f),(t,pose,root,rot)=>NativeIntroPoses.HoldStaff(pose,"fire"),t=>On());
                case "shield-brace":return Held(name,"shield",NativeIntroPoses.ShieldBrace(),Pose.StanceOf(.13f,.22f,.1f,-.15f,-.22f,-.45f),(t,pose,root,rot)=>NativeIntroPoses.BraceShield(pose),t=>On());
            }
            return null;
        }
    }
}
