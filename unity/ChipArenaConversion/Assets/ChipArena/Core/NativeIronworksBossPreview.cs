using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE CONVERTER's DEV preview (DEV panel · Camera · "Ironworks boss preview", on the Ironworks map: a live instance or the
    /// sandbox): a scripted record that plays every part of the fight on the presentation clock, so the arrival, the eight actions,
    /// both phase changes, the moments (a choked blow, a kneel, a crash), a crust break, a freed grab and the death can be seen
    /// without Astra's rules. Presentation only: nothing is spawned, nothing collides or hurts, and the record never reaches the
    /// authority or the network. A pure function of the clock (two peers, or two frame rates, see the same record at the same time).
    /// The script (seconds from the start; it loops every <see cref="Loop"/> s): the arrival on Hydraulic Hell's beats; a walk and a
    /// turn; pour; blow; stomp (front right); grab (front left, held and thrown); the front crust broken; a blow choked; a kneel; the
    /// shed (phase 2, the hall's vessels jolt awake); slam; deslag; call (vessel A); a grab freed by allies; a rear stomp; the break
    /// (phase 3: the pulpit torn, the shell cracked white, the pit welling); call (both vessels); charge into the east wall (the
    /// crash); the last blow (28 drops); the death (the shift whistle, the foundry stopping).
    /// </summary>
    public static class NativeIronworksBossPreview
    {
        /// <summary>The DEV toggle and its start on the presentation clock (Runtime sets both).</summary>
        public static bool On;public static double Start=double.NaN;
        /// <summary>The record the preview drew last and on which clock (the DEV HUD's banner and plate read it).</summary>
        public static IronworksBossView Shown=IronworksBossView.None;public static double ShownClock;
        /// <summary>The banner's eyebrow as the expedition HUD names the area (BOSS ARRIVAL · CITY IRONWORKS).</summary>
        public static readonly string BannerArea=ExpeditionBossModel.Eyebrow+" · CITY IRONWORKS";
        public const float SpawnAt=.5f;
        /// <summary>The script's length (the death and a breath after it), s.</summary>
        public static readonly float Loop;
        /// <summary>The preview's pilots (x, z): the look-at targets and the actions' aim points.</summary>
        public static readonly Vector2[] Pilots={new Vector2(-28.0f,2.6f),new Vector2(-31.0f,-6.2f),new Vector2(-41.5f,4.6f),new Vector2(-27.0f,-2.4f)};
        private struct Act{public string Id;public float At;public int Target,Variant;public float Tx,Tz,Yaw,FreedAt;}
        private struct Key{public float At,X,Z,Yaw;public byte Kind;}
        private const byte Smooth=0,Charge=1;
        private static readonly Act[] acts;private static readonly Key[] keys;
        /// <summary>The script's moments (s from the start) the checks and the renders key on.</summary>
        public static float ArrivalEnd,PourAt,BlowAt,StompAt,GrabAt,CrustAt,ChokeAt,KneelAt,Phase2At,SlamAt,DeslagAt,CallAt,FreedGrabAt,RearStompAt,Phase3At,Call2At,ChargeAt,CrashAt,LastBlowAt,DeathAt;
        static NativeIronworksBossPreview()
        {
            var a=new System.Collections.Generic.List<Act>();var k=new System.Collections.Generic.List<Key>();
            var beats=IronworksBossBeats.Fallback;float t=SpawnAt+beats.Drop+IronworksBossRig.ArrivalTail+.3f;ArrivalEnd=t;
            const float X=NativeIronworksBossArena.SpawnX,Z=NativeIronworksBossArena.SpawnZ;
            void K(float at,float x,float z,float yaw,byte kind=Smooth)=>k.Add(new Key{At=at,X=x,Z=z,Yaw=yaw,Kind=kind});
            float A(string id,float at,int target=-1,int variant=0,float tx=float.NaN,float tz=float.NaN,float yaw=float.NaN,float freed=float.NaN)
            {var m=NativeIronworksBoss.Find(id);var p=target>=0?Pilots[target]:new Vector2(tx,tz);a.Add(new Act{Id=id,At=at,Target=target,Variant=variant,Tx=p.X,Tz=p.Y,Yaw=yaw,FreedAt=freed});return at+m.Total;}
            K(0,X,Z,90);K(t,X,Z,90);
            // A walk forward-left and a turn back toward the pilots by the shutter: the gait, the trunnion swing on the stop.
            K(t+.6f,X,Z,90);K(t+5.2f,X+3.4f,Z+3.2f,62);K(t+6.6f,X+3.4f,Z+3.2f,100);
            PourAt=t+6.8f;t=A("pour",PourAt,0,0,float.NaN,float.NaN,100);
            K(t+.4f,X+3.4f,Z+3.2f,100);K(t+3.0f,X+1.6f,Z+.6f,118);
            BlowAt=t+3.2f;t=A("blow",BlowAt);
            K(t+.3f,X+1.6f,Z+.6f,118);K(t+1.6f,X+1.6f,Z+.6f,140);
            StompAt=t+1.8f;var st=Pilots[1];t=A("stomp",StompAt,-1,1,X+1.6f+4.6f,Z+.6f-2.6f);
            K(t+.4f,X+1.6f,Z+.6f,140);K(t+2.4f,X+1.6f,Z+.6f,60);
            GrabAt=t+2.6f;t=A("grab",GrabAt,0);
            CrustAt=t+.9f;
            K(t+.4f,X+1.6f,Z+.6f,60);K(t+3.4f,X-1.0f,Z-.4f,95);
            ChokeAt=t+3.6f;var bl=NativeIronworksBoss.Find("blow");a.Add(new Act{Id="blow",At=ChokeAt,Target=-1,Tx=float.NaN,Tz=float.NaN,Yaw=float.NaN,FreedAt=float.NaN});
            float choke=ChokeAt+.9f;t=A("choke",choke);
            KneelAt=t+1.4f;t=A("kneel",KneelAt);
            K(KneelAt+4.6f,X-1.0f,Z-.4f,95);K(KneelAt+6.0f,X-1.0f,Z-.4f,95);
            Phase2At=KneelAt+6.0f;t=Phase2At+NativeIronworksBoss.Find("shed").Total;
            K(t+.4f,X-1.0f,Z-.4f,95);K(t+2.8f,X+.8f,Z+.4f,80);
            SlamAt=t+3.0f;t=A("slam",SlamAt,-1,0,float.NaN,float.NaN,80);
            K(t+.4f,X+.8f,Z+.4f,80);K(t+2.0f,X+.8f,Z+.4f,60);
            DeslagAt=t+2.2f;t=A("deslag",DeslagAt,-1,0,float.NaN,float.NaN,60);
            K(t+.4f,X+.8f,Z+.4f,60);K(t+2.4f,X+.4f,Z+.2f,-20);
            CallAt=t+2.6f;t=A("call",CallAt,-1,1);
            K(t+.4f,X+.4f,Z+.2f,-20);K(t+2.6f,X+1.2f,Z+.4f,70);
            FreedGrabAt=t+2.8f;var gr=NativeIronworksBoss.Find("grab");t=A("grab",FreedGrabAt,3,0,float.NaN,float.NaN,float.NaN,FreedGrabAt+gr.Windup+gr.Strike+.55f);
            K(t+.4f,X+1.2f,Z+.4f,70);K(t+2.0f,X+1.2f,Z+.4f,150);
            RearStompAt=t+2.2f;t=A("stomp",RearStompAt,-1,3,X+1.2f+4.4f,Z+.4f+3.0f);
            K(t+.4f,X+1.2f,Z+.4f,150);K(t+2.6f,X-1.6f,Z+1.0f,95);K(t+3.2f,X-1.6f,Z+1.0f,95);
            Phase3At=t+3.4f;t=Phase3At+NativeIronworksBoss.Find("break").Total;
            K(t+.4f,X-1.6f,Z+1.0f,95);K(t+2.0f,X-1.6f,Z+1.0f,80);
            Call2At=t+2.2f;t=A("call",Call2At,-1,3);
            // The charge: to the west side, then 13 m east along z 5.5 into the east wall beside the shutter (the crash).
            K(t+.4f,X-1.6f,Z+1.0f,80);K(t+5.0f,X-5.0f,5.5f,90);K(t+5.8f,X-5.0f,5.5f,90);
            ChargeAt=t+6.0f;var ch=NativeIronworksBoss.Find("charge");float run0=ChargeAt+ch.Windup,run1=run0+ch.Strike+.35f;
            K(run0,X-5.0f,5.5f,90);K(run1,X+7.4f,5.5f,90,Charge);
            a.Add(new Act{Id="charge",At=ChargeAt,Target=-1,Tx=X+7.4f,Tz=5.5f,Yaw=90,FreedAt=float.NaN});
            CrashAt=run1+.05f;t=A("crash",CrashAt);
            K(t+.6f,X+7.4f,5.5f,90);K(t+4.6f,X+.4f,Z+.6f,110);
            LastBlowAt=t+4.8f;t=A("blow",LastBlowAt);
            K(t+.4f,X+.4f,Z+.6f,110);K(t+1.6f,X+.4f,Z+.6f,100);
            DeathAt=t+1.8f;Loop=DeathAt+NativeIronworksBoss.Find("die").Total+1.5f;K(DeathAt,X+.4f,Z+.6f,100);K(Loop,X+.4f,Z+.6f,100);
            acts=a.ToArray();keys=k.ToArray();
        }

        /// <summary>The preview's record at presentation time `t` (from <see cref="Start"/>), its root velocity, the pilot it looks
        /// at, and its two clocks: the arrival's boss song clock and the death's age (NaN when not running).</summary>
        public static void Fill(double t,ref IronworksBossView v,out Vector3 velocity,out Vector2 target,out double arrival,out double death)
        {
            double since=t-Start;double loops=Math.Floor(since/Loop);float p=(float)(since-loops*Loop);float baseAt=(float)(Start+loops*Loop);
            View(p,baseAt,ref v,out velocity,out target,out arrival,out death);
        }
        /// <summary>The record `p` seconds into the script, its times offset to `baseAt` on the clock.</summary>
        public static void View(float p,float baseAt,ref IronworksBossView v,out Vector3 velocity,out Vector2 target,out double arrival,out double death)
        {
            v=IronworksBossView.None;v.live=true;v.spawned=baseAt+SpawnAt;v.maxHp=6000;
            Root(p,out float x,out float z,out float yaw);Root(p+1/240f,out float x2,out float z2,out _);Root(p-1/240f,out float x1,out float z1,out _);
            v.x=x;v.z=z;v.y=0;v.yaw=yaw;velocity=new Vector3((x2-x1)*120,0,(z2-z1)*120);
            arrival=p>=SpawnAt?p-SpawnAt:double.NaN;death=p>=DeathAt?p-DeathAt:double.NaN;
            // Phases, parts.
            v.phase=p>=Phase3At?3:p>=Phase2At?2:1;if(p>=Phase2At)v.phaseAt=baseAt+(p>=Phase3At?Phase3At:Phase2At);
            if(p>=CrustAt){v.parts&=~1;v.partAt0=baseAt+CrustAt;}
            if(p>=Phase2At){var sh=NativeIronworksBoss.Find("shed");for(int g=1;g<4;g++){v.parts&=~(1<<g);v.SetPartAt(g,baseAt+Phase2At+sh.Windup+.1f*g);}}
            if(p>=Phase3At+.9f){v.parts&=~(1<<IronworksBossView.PartPulpit);v.partAt4=baseAt+Phase3At+.9f;}
            v.hp=v.maxHp*(p<Phase2At?Math.Max(.71f,1-.29f*Math.Max(0,p-ArrivalEnd)/(Phase2At-ArrivalEnd)):p<Phase3At?Math.Max(.36f,.69f-.33f*(p-Phase2At)/(Phase3At-Phase2At)):Math.Max(0,.34f-.34f*(p-Phase3At)/(DeathAt-Phase3At)));
            v.stagger=p>=KneelAt-3&&p<KneelAt?(p-(KneelAt-3))/3:0;
            // The action playing (the latest started, held for its length).
            target=Pilots[0];
            for(int i=acts.Length-1;i>=0;i--)
            {
                var a=acts[i];if(p<a.At)continue;var m=NativeIronworksBoss.Find(a.Id);if(p>a.At+m.Total+.5f)break;
                // A choked blow ends at its choke; the death takes over everything.
                v.action=a.Id;v.actionAt=baseAt+a.At;v.targetX=a.Tx;v.targetZ=a.Tz;v.variant=a.Variant;v.actionYaw=float.IsNaN(a.Yaw)?float.NaN:a.Yaw;
                if(a.Target>=0)target=Pilots[a.Target];if(a.Id=="grab")v.targetSeat=float.IsNaN(a.FreedAt)||p<a.FreedAt?a.Target:-1;
                break;
            }
            if(p>=DeathAt){v.deathAt=baseAt+DeathAt;v.hp=0;v.action=null;v.actionAt=float.NaN;}
        }
        /// <summary>The scripted root at `p`: eased between keys (zero speed at each), the charge's launch, run and skid.</summary>
        public static void Root(float p,out float x,out float z,out float yaw)
        {
            int i=0;while(i<keys.Length-1&&keys[i+1].At<=p)i++;
            if(i>=keys.Length-1){var e=keys[keys.Length-1];x=e.X;z=e.Z;yaw=e.Yaw;return;}
            var a=keys[i];var b=keys[i+1];float u=b.At>a.At?BossMath.Clamp01((p-a.At)/(b.At-a.At)):1,f;
            if(b.Kind==Charge)
            {
                float T=b.At-a.At,acc=Math.Min(.3f,T*.25f),skid=Math.Min(.45f,T*.3f),cruise=T-acc-skid,s=p-a.At;float vmax=1/(acc*.5f+cruise+skid*.5f);
                f=s<acc?.5f*vmax*s*s/acc:s<acc+cruise?vmax*(acc*.5f+(s-acc)):s<T?vmax*(acc*.5f+cruise+(s-acc-cruise)-.5f*(s-acc-cruise)*(s-acc-cruise)/skid):1;
            }
            else f=BossMath.Smoother(u);
            x=a.X+(b.X-a.X)*f;z=a.Z+(b.Z-a.Z)*f;yaw=a.Yaw+(b.Yaw-a.Yaw)*BossMath.Smoother(u);
        }
        /// <summary>The preview's blows (time, push, size) for the flinch: a burst of hits in the opening walk and while it kneels.</summary>
        public static int Blows(float p,float baseAt,float[] at,Vector3[] push,float[] size)
        {
            int n=0;
            for(int k=0;k<5&&n<at.Length;k++){float b=ArrivalEnd+1.4f+k*.5f;if(p<b)break;at[n]=baseAt+b;push[n]=Vector3.Normalize(new Vector3(k%2==0?-1f:-.6f,0,k%2==0?.3f:-.5f));size[n]=k==3?1.3f:.7f;n++;}
            for(int k=0;k<3&&n<at.Length;k++){float b=KneelAt+.9f+k*.55f;if(p<b)break;at[n]=baseAt+b;push[n]=Vector3.Normalize(new Vector3(-1,0,.2f*k));size[n]=.9f;n++;}
            return n;
        }
    }
}
