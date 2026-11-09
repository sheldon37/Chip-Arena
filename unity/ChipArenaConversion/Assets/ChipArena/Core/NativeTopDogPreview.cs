using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Dev Sandbox's "Preview Top Dog": the whole kit on a loop, written as the host writes the record (docs/TOP-DOG.md), so the pose, the
    /// effects, the sound and the HUD all show it. It reaches nothing else: not the authority, the network frame, damage or movement. The three
    /// swings and the slam, the boing, the zip, the anvil, a peel, the mustard, the glove, a pie, All You Can Eat on the nearest other pilot
    /// (Victim; with none the lasso misses), then the mallet's dash, its smash and its overhead slam (on the spot).
    /// </summary>
    public sealed class NativeTopDogPreview:ITopDogSource
    {
        public const float Loop=27;
        private readonly Dictionary<int,NativeTopDogKit> kits=new Dictionary<int,NativeTopDogKit>();private float started=-100,last;
        public int Victim;public Vector3 VictimAt;
        public NativeTopDogKit Of(int actor)=>kits.TryGetValue(actor,out var k)?k:null;
        public bool Any=>kits.Count>0;
        public void Clear(){kits.Clear();started=-100;}
        public void Step(Actor a,Vector3 feet,float yaw,float time)
        {
            if(a==null||a.SourceSpec!=NativeTopDog.Spec){kits.Clear();started=-100;return;}
            if(time<last-.25f||started<-50)started=(float)Math.Ceiling(time+.3f);last=time;if(time-started>=Loop)started=time;
            if(!kits.TryGetValue(a.Id,out var k)){k=new NativeTopDogKit();kits[a.Id]=k;}
            Write(k,feet,yaw,started,time,Victim,VictimAt);
        }
        private static V3 V(Vector3 v)=>new V3(v.X,v.Y,v.Z);
        /// <summary>The record at `time` of a loop begun at `t0` for a pilot at `feet` facing `yaw`. Pure: the same clock writes the same record.</summary>
        public static void Write(NativeTopDogKit k,Vector3 feet,float yaw,float t0,float time,int victim=0,Vector3 victimAt=default)
        {
            var f=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));var r=new Vector3(f.Z,0,-f.X);var up=Vector3.UnitY;var hand=feet+up*1.3f+f*.35f;float t=time-t0;
            var props=new List<NativeDogProp>();var hits=new List<NativeDogHit>();var marks=new List<NativeDogMark>();k.CastKind=null;k.CastAt=-100;k.CastStep=0;k.Feast=null;k.Peels=NativeTopDog.PeelCharges;
            void Cast(string id,float at,float release,float until,int step=0){if(t>=at&&t<until){k.CastKind=id;k.CastAt=t0+at;k.CastRelease=t0+release;k.CastUntil=t0+until;k.CastStep=step;}}
            void Hit(int id,string kind,float at,Vector3 pos,Vector3 dir,int who=0){if(t>=at&&t<at+1.5f)hits.Add(new NativeDogHit{Id=id,Kind=kind,At=t0+at,Pos=V(pos),Dir=V(dir),Victim=who});}
            NativeDogProp Prop(int id,string kind,float at,float land,float until,Vector3 pos){if(t<at||t>=until)return null;var p=new NativeDogProp{Id=id,Kind=kind,At=t0+at,SampleAt=t0+at,LandAt=t0+land,Until=t0+until,Pos=V(pos),Dir=V(f)};props.Add(p);return p;}
            // The mallet: two swings and the slam.
            Cast(NativeTopDog.Mallet,0,.2f,.48f,0);Cast(NativeTopDog.Mallet,.6f,.8f,1.08f,1);Cast(NativeTopDog.Mallet,1.2f,1.56f,2.02f,2);
            Hit(1,NativeTopDog.HitClang,1.56f,feet+f*NativeTopDog.SlamAhead,up);Hit(2,NativeTopDog.HitBonk,.2f,hand+f*1.2f,f);
            // The boing.
            Cast(NativeTopDog.Boing,2.6f,2.72f,2.9f);Prop(3,NativeTopDog.PropSpring,2.72f,2.72f,3.62f,feet);Hit(4,NativeTopDog.HitBoing,2.72f,feet,up);Hit(5,NativeTopDog.HitThud,4.1f,feet,up);
            // The zip.
            Cast(NativeTopDog.Zip,4.6f,4.9f,5.32f);var zip=Prop(6,NativeTopDog.PropZip,4.9f,4.9f,6.1f,feet);if(zip!=null)zip.To=V(feet+f*NativeTopDog.ZipReach);Hit(7,NativeTopDog.Zip,4.9f,feet,f);
            // The anvil.
            var drop=feet+f*8;Cast(NativeTopDog.Anvil,6.4f,6.62f,7.02f);Prop(8,NativeTopDog.PropAnvil,6.62f,7.52f,11.02f,drop);Hit(9,NativeTopDog.HitAnvil,7.52f,drop,up);
            // A peel, lobbed and lying there.
            var peel=feet+f*5+r*1.5f;Cast(NativeTopDog.Peel,8,8.18f,8.58f);var pp=Prop(10,NativeTopDog.PropPeel,8.18f,8.63f,Loop,hand);if(pp!=null){float ft=NativeTopDog.PeelFlight;pp.Vel=V((peel-hand)/ft+up*(23*ft/2));pp.To=V(peel);}
            // The mustard, sweeping a little, and its puddles.
            Cast(NativeTopDog.Mustard,9.2f,9.3f,10.9f);var sq=Prop(11,NativeTopDog.PropSquirt,9.3f,9.3f,10.9f,hand);if(sq!=null){float sw=(float)Math.Sin((t-9.3f)*2)*.35f;sq.Dir=V(Vector3.Normalize(f+r*sw-up*.12f));sq.Size=NativeTopDog.MustardReach;}
            for(int i=0;i<4;i++){float at=9.45f+i*NativeTopDog.PuddleEvery;var pud=Prop(12+i,NativeTopDog.PropPuddle,at,at,at+NativeTopDog.PuddleLife,feet+f*(6.5f+.4f*i)+r*((float)Math.Sin(i*2)*1.5f));if(pud!=null)pud.Size=NativeTopDog.PuddleRadius;}
            // The glove, punching out at full reach.
            Cast(NativeTopDog.Glove,11.4f,11.58f,12.13f);var gl=Prop(16,NativeTopDog.PropGlove,11.58f,11.78f,12.03f,hand);if(gl!=null){gl.Size=NativeTopDog.GloveReach;gl.Dir=V(f);}
            // A pie, flying and splatting on the floor.
            Cast(NativeTopDog.Pie,12.6f,12.8f,13.2f);float pt=t-12.8f;var splat=hand+f*19.5f-up*1.6f;
            if(pt>=0&&pt<.75f){var pie=Prop(17,NativeTopDog.PropPie,12.8f,-100,13.55f,hand);if(pie!=null){pie.Vel=V(f*NativeTopDog.PieSpeed+up*1.5f);pie.LandAt=-100;}}
            else if(pt>=.75f&&pt<3.75f){var pie=Prop(17,NativeTopDog.PropPie,12.8f,13.55f,16.55f,splat);if(pie!=null)pie.Dir=V(up);}
            Hit(18,NativeTopDog.HitPie,13.55f,splat,-f);
            // All You Can Eat.
            float fa=14.2f;
            if(t>=fa)
            {
                var feast=new NativeDogFeast{At=t0+fa,Throw=t0+fa+NativeTopDog.Twirl,Dir=V(f),From=V(hand)};var hold=feet+f*NativeTopDog.HoldAhead+up*.865f;
                float fly=victim!=0?Math.Min(NativeTopDog.LassoReach,Vector3.Distance(hand,victimAt+up*.6f))/NativeTopDog.LassoSpeed:NativeTopDog.LassoReach/NativeTopDog.LassoSpeed;
                float catchAt=fa+NativeTopDog.Twirl+fly;
                if(t<catchAt||victim==0)
                {
                    float u=Math.Max(0,Math.Min(1,(t-fa-NativeTopDog.Twirl)/Math.Max(.05f,fly)));var target=victim!=0?victimAt+up*.6f:hand+f*NativeTopDog.LassoReach;
                    feast.Lasso=V(Vector3.Lerp(hand,target,u));feast.LassoVel=V(Vector3.Normalize(target-hand)*NativeTopDog.LassoSpeed);feast.LassoAt=time;
                    if(victim==0&&t>=catchAt)feast.Miss=true;feast.Until=t0+catchAt+NativeTopDog.After;
                }
                else
                {
                    feast.Catch=t0+catchAt;feast.Victim=victim;feast.FeedAt=NativeTopDog.FeedAt(feast.Catch);feast.Release=NativeTopDog.ReleaseAt(feast.FeedAt);feast.Pop=NativeTopDog.PopAt(feast.Release);feast.Until=feast.Pop+NativeTopDog.After;
                    feast.Hold=V(hold);feast.From=V(victimAt+up*.865f);
                    for(int i=0;i<NativeTopDog.Feeds;i++)if(time>=NativeTopDog.DownAt(feast.FeedAt,i)){feast.Fed=i+1;Hit(40+i,NativeTopDog.HitGulp,NativeTopDog.DownAt(feast.FeedAt,i)-t0,hold+up*.75f,f,victim);}
                    if(time>=feast.Release){Hit(50,NativeTopDog.HitPow,feast.Release-t0,hold+up*.2f,f,victim);if(time<feast.Pop)marks.Add(new NativeDogMark{Id=victim,Kind=NativeTopDog.Stuffed,At=feast.Release,Until=feast.Pop});}
                    if(time>=feast.Pop)Hit(51,NativeTopDog.HitPop,feast.Pop-t0,hold+f*NativeTopDog.PushBack+up*.2f,up,victim);
                }
                if(time<feast.Until){k.Feast=feast;Cast(NativeTopDog.Feast,fa,fa+NativeTopDog.Twirl,feast.Until-t0);}
            }
            // The mallet's dash, its smash (in the air looking ahead) and its overhead slam (looking down), each landing on the spot.
            Cast(NativeTopDog.Dash,21.9f,22.02f,22.68f);Hit(60,NativeTopDog.HitDash,22.02f,feet,f);
            void Landing(string id,float at,float release,float land,float recover)
            {
                if(t<at||t>=land+recover)return;k.CastKind=id;k.CastAt=t0+at;k.CastRelease=t0+release;
                if(t<land){k.CastStep=0;k.CastUntil=t0+release+2.5f;}else{k.CastStep=1;k.CastUntil=t0+land+recover;}
            }
            Landing(NativeTopDog.Smash,23.1f,23.1f+NativeTopDog.SmashHang,23.48f,NativeTopDog.SmashRecover);Hit(61,NativeTopDog.HitClang,23.48f,feet+f*NativeTopDog.SmashAhead,up);
            Landing(NativeTopDog.Slam,24.5f,24.5f+NativeTopDog.SlamHang,24.84f,NativeTopDog.SlamRecover);Hit(62,NativeTopDog.HitQuake,24.84f,feet,up);
            k.Props=props.ToArray();k.Hits=hits.ToArray();k.Marks=marks.ToArray();k.Meter=t<fa?Math.Min(1,t/fa):0;k.Combo=k.CastStep;k.ComboUntil=k.CastUntil+NativeTopDog.ComboWindow;
            k.Ready=new float[NativeTopDog.Slots];float R(float at,int slot)=>t>=at&&t<at+NativeTopDog.Cooldown[slot]?t0+at+NativeTopDog.Cooldown[slot]:0;
            k.Ready[0]=R(2.72f,0);k.Ready[1]=R(4.9f,1);k.Ready[2]=R(6.62f,2);k.Ready[4]=R(9.3f,4);k.Ready[5]=R(11.58f,5);k.Ready[6]=R(12.8f,6);k.Peels=t>=8.18f&&t<14.18f?1:2;
        }
    }
}
