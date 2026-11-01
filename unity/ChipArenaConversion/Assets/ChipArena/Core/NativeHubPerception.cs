using System;
namespace ChipArena.UnityConversion
{
    public enum HubZombieState : byte { None, Wander, Idle, Alert, Chase, Search }
    public enum HubNoiseKind : byte { Walk, Sprint, Land, Fight, Fire, Explosion, Cast, Gate }
    // Public presentation contract. Times use arena simulation time; LookYaw is degrees, +Z = 0.
    [Serializable] public sealed class HubZombieView
    {
        public HubZombieState State;public float AlertAt=-100,LookYaw;
        public HubZombieView Copy()=>new HubZombieView{State=State,AlertAt=AlertAt,LookYaw=LookYaw};
        public static bool Valid(HubZombieState state,float at,float yaw)=>state>=HubZombieState.None&&state<=HubZombieState.Search&&MathUtil.Finite(at)&&at>=-100&&MathUtil.Finite(yaw)&&Math.Abs(yaw)<=360;
        public static bool Valid(HubZombieView v)=>v==null||v.State>=HubZombieState.Wander&&v.State<=HubZombieState.Search&&MathUtil.Finite(v.AlertAt)&&v.AlertAt>=-100&&MathUtil.Finite(v.LookYaw)&&Math.Abs(v.LookYaw)<=360;
    }
    public static class HubPerception
    {
        public const float Sight=18,Close=3,HalfCone=55,LostSight=3,SearchTravel=10,SearchIdle=4,NoiseLifetime=.75f,SenseInterval=.2f;
        public static float Radius(HubNoiseKind k)=>k==HubNoiseKind.Walk?5:k==HubNoiseKind.Sprint?10:k==HubNoiseKind.Land?12:k==HubNoiseKind.Fight?14:k==HubNoiseKind.Fire?28:k==HubNoiseKind.Explosion?36:k==HubNoiseKind.Cast?22:16;
        public static bool Sees(V3 from,float yaw,V3 to,bool lineOfSight,bool fenceBlocked)
        {
            if(!lineOfSight||fenceBlocked)return false;var d=to-from;if(d.Length>Sight)return false;
            if(d.Length<=Close)return true;d.y=0;return V3.Dot(V3.Forward(yaw),d.Normalized)>=(float)Math.Cos(HalfCone*Math.PI/180)-.00001f;
        }
        public static bool Hears(V3 listener,V3 at,HubNoiseKind kind,bool fenceBlocked)=>!fenceBlocked&&(listener-at).Length<=Radius(kind);
        public static float FlatDistance(V3 a,V3 b){var d=a-b;d.y=0;return d.Length;}
        public static float Yaw(V3 d)=>(float)(Math.Atan2(d.x,d.z)*180/Math.PI);
    }
    // Host only: no knowledge of a target's current position unless Observe receives a real sight/noise sample.
    public sealed class HubZombieBrain
    {
        public readonly HubZombieView View=new HubZombieView();
        public int Target;public V3 Goal;public float LastNotice=-100;public bool NeedsGoal;
        private uint random;private float until,searchEnd;private bool searching;
        public HubZombieBrain(int seed,float now,V3 home,float yaw)
        {random=unchecked((uint)seed*747796405u+2891336453u);if(random==0)random=1;Goal=home;View.LookYaw=yaw;Idle(now);}
        private float Roll(){random^=random<<13;random^=random>>17;random^=random<<5;return (random&0xffffff)/16777216f;}
        private void Idle(float now){View.State=HubZombieState.Idle;until=now+2+4*Roll();NeedsGoal=false;}
        public void Wander(float now,V3 goal){Goal=goal;View.State=HubZombieState.Wander;until=now+15;NeedsGoal=false;}
        public void Observe(float now,int target,V3 point,V3 position)
        {
            if(target<=0)return;
            if(View.State!=HubZombieState.Alert&&View.State!=HubZombieState.Chase)
            {View.State=HubZombieState.Alert;View.AlertAt=now;until=now+.4f+.4f*Roll();}
            Target=target;Goal=point;LastNotice=now;View.LookYaw=HubPerception.Yaw(point-position);NeedsGoal=false;
        }
        private void Search(float now){View.State=HubZombieState.Search;Target=0;searching=false;searchEnd=now+HubPerception.SearchTravel;NeedsGoal=false;}
        public void Step(float now,V3 position,bool targetValid)
        {
            if((View.State==HubZombieState.Alert||View.State==HubZombieState.Chase)&&!targetValid)Search(now);
            if(View.State==HubZombieState.Alert&&now>=until)View.State=HubZombieState.Chase;
            if(View.State==HubZombieState.Chase&&now-LastNotice>=HubPerception.LostSight)Search(now);
            if(View.State==HubZombieState.Search)
            {
                if(!searching&&HubPerception.FlatDistance(position,Goal)<=1){searching=true;searchEnd=Math.Min(searchEnd,now+HubPerception.SearchIdle);}
                if(now>=searchEnd){View.State=HubZombieState.Wander;NeedsGoal=true;}
            }
            else if(View.State==HubZombieState.Idle&&now>=until){View.State=HubZombieState.Wander;NeedsGoal=true;}
            else if(View.State==HubZombieState.Wander&&!NeedsGoal&&(HubPerception.FlatDistance(position,Goal)<=1||now>=until))Idle(now);
            if(View.State==HubZombieState.Wander||View.State==HubZombieState.Chase||View.State==HubZombieState.Search&&!searching)View.LookYaw=HubPerception.Yaw(Goal-position);
        }
        public bool SearchInPlace=>View.State==HubZombieState.Search&&searching;
        public int Pick(int count)=>count<=1?0:Math.Min(count-1,(int)(Roll()*count));
    }
}
