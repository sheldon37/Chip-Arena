using System;

namespace ChipArena.UnityConversion
{
    // Pure helpers from shared/abilities.ts at 05950ea. Separate from Unity contact handling.
    public static class NativeAbilityRules
    {
        public enum Press { Refused, Buffered, Cast }
        public static Press Decide(bool active,bool alive,bool blocking,float mana,float cost,float cooldown,bool busy)
        {
            if(!active||!alive||blocking||mana<cost||cooldown>.3f)return Press.Refused;
            return busy||cooldown>0?Press.Buffered:Press.Cast;
        }
        private static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));
        public static float CastTime(double seconds,ChipStats s)=>(float)(seconds/Clamp(s["attackRate"],.15,1.5));
        public static float Cooldown(ContentAbility a,ChipStats s)=>(float)(a.cooldown*Clamp(s["abilityCooldown"],.45,3));
        public static float Cost(ContentAbility a,ChipStats s)=>(float)(a.cost*s["abilityCost"]*s["resourceCost"]);
        public static float Radius(ContentAbility a,ChipStats s)=>(float)(a.radius*Clamp(s["abilityRadius"],.5,1.5));
        public static float Scale(ChipStats s,bool highMana,bool echo=false)=>(float)(Math.Min(2.25,Clamp(Math.Sqrt(s["damage"]),.5,1.5)*Math.Min(2,s["abilityPower"]*(highMana?1+s["arcaneSurge"]:1)))*(echo?.5:1));
        public static int Charges(ChipStats s)=>(int)Math.Min(3,1+Math.Max(0,Math.Floor(s["abilityCharges"])));
        public static bool HoldAim(string id)=>id=="frag-grenade"||id=="meteor-strike"||id=="geyser-wall"||id=="singularity";
        public static float BlastFalloff(float distance,float radius)=>distance>=radius+.4f?0:1-MathUtil.Clamp(distance/(radius+.4f),0,.85f);
        public static float PrimaryWindup(int element,float rate)=>(element==1?.16f:element==2?.12f:element==3?.065f:.22f)/Math.Max(.15f,rate);
    }
    public static class NativeViewRules
    {
        public const float EyeHeight=.52f,ThirdDistance=4.4f,ThirdLift=.55f,CollisionPadding=.2f,FirstFov=75,ThirdFov=60;
        public static bool FirstPerson(bool selected,bool alive,bool menu)=>selected&&alive&&!menu;
        public static bool ViewModelBone(int bone)=>bone==CapturedRig.LElbow||bone==CapturedRig.LHand||bone==CapturedRig.RElbow||bone==CapturedRig.RHand;
    }
}
