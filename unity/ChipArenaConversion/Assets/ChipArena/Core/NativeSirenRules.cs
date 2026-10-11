using System;
namespace ChipArena.UnityConversion
{
    public sealed class SirenRule
    {public string Id;public float Windup,Active,Recovery,Travel,Cooldown,Damage,Range,Radius,Arc,Speed,Gravity;}
    public static class NativeSirenRules
    {
        public static readonly string[] Melee={"rend","slam","rush","laugh"},Ranged={"damnation","eruption","rain","mark"};
        public static readonly SirenRule[] Moves={
            new SirenRule{Id="swipe",Windup=.12f,Active=.10f,Recovery=.18f,Damage=9,Range=2.3f,Arc=2.4f},
            new SirenRule{Id="rend",Windup=.42f,Active=.14f,Recovery=.49f,Cooldown=5,Damage=22,Range=3,Arc=2.8f},
            new SirenRule{Id="slam",Windup=.38f,Recovery=.62f,Cooldown=7,Damage=16,Radius=3.6f},
            new SirenRule{Id="rush",Windup=.14f,Travel=.44f,Recovery=.24f,Cooldown=6,Damage=14,Range=6,Radius=1.1f},
            new SirenRule{Id="laugh",Windup=2,Travel=.35f,Recovery=.9f,Cooldown=30,Range=7,Radius=1.05f},
            new SirenRule{Id="hellfire",Windup=.16f,Recovery=.26f,Damage=10,Speed=20,Radius=.18f},
            new SirenRule{Id="damnation",Windup=.55f,Recovery=.45f,Cooldown=6,Damage=30,Speed=13,Gravity=8,Radius=.42f},
            new SirenRule{Id="eruption",Windup=.3f,Recovery=.3f,Cooldown=5,Damage=28,Radius=2.2f},
            new SirenRule{Id="rain",Windup=.6f,Recovery=.4f,Cooldown=18,Damage=9,Radius=7},
            new SirenRule{Id="mark",Windup=2.4f,Recovery=.5f,Cooldown=35,Damage=85,Speed=3,Radius=.32f,Range=40}};
        // Last Laugh keeps its raised-hand pose after the timed crush, until the original move ends.
        public static bool GrabPose(bool caught,float now,float until)=>caught&&now<until;
        // The rendered captive faces the holder's yaw + 180 (NativeInteractionPresentation).
        // Store that facing on the death, not the victim's pre-catch aim yaw.
        public static float CrushDeathYaw(float holderYaw)
        {float yaw=(holderYaw+180f)%360f;return yaw<0?yaw+360f:yaw;}
        // Only the actual timed crush has the Siren-death style, not an unrelated kill during a grab.
        public static string DeathStyle(string spec,bool crush,string killEffect)
        =>spec=="siren"?"siren-dissipate":crush?"siren-crush":killEffect??"impact-ragdoll";
        public static SirenRule Rule(string id)=>Array.Find(Moves,m=>m.Id==id);
        public static float Duration(SirenRule r)=>r.Windup+r.Active+r.Recovery+r.Travel;
    }
}
