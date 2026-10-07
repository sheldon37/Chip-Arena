using System;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeBodyImpact
    {public string kind;public float mass,speed,radius,transfer,referenceDamage;}
    [Serializable] public sealed class NativeWound
    {public int id,part;public float at,radius,severity,energy,momentum,speed;public string kind,trauma;public V3 point,direction;public bool destroyed;}
    public static class NativeBodyDamage
    {
        public static int Part(V3 hit,float yaw,bool head=false)
        {float a=yaw*(float)Math.PI/180,x=hit.x*(float)Math.Cos(a)-hit.z*(float)Math.Sin(a);if(head||hit.y>.4f)return CapturedRig.Head;if(hit.y<-.28f)return x>=0?(hit.y<-.56f?CapturedRig.LKnee:CapturedRig.LHip):(hit.y<-.56f?CapturedRig.RKnee:CapturedRig.RHip);if(Math.Abs(x)>.235f)return x>=0?(hit.y>.02f?CapturedRig.LArm:CapturedRig.LElbow):(hit.y>.02f?CapturedRig.RArm:CapturedRig.RElbow);return CapturedRig.Spine;}
        private static float Clamp(float x,float lo,float hi)=>Math.Max(lo,Math.Min(hi,x));
        public static NativeWound Resolve(NativeBodyImpact impact,int part,float damage,bool lethal,bool headshot=false)
        {
            if(impact==null||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage))return null;bool head=part==CapturedRig.Head,torso=part==CapturedRig.Spine;
            float cavity=head?65:torso?125:90,sever=head?280:torso?1000:350,cut=head?38:torso?380:58,crush=head?110:torso?650:155;
            float scale=Clamp(damage/Math.Max(1,impact.referenceDamage)/(headshot?1.5f:1),0,4),mass=Clamp(impact.mass,0,10000),speed=Clamp(impact.speed,0,1000),transfer=Clamp(impact.transfer,0,1);
            float energy=Math.Min(100000,.5f*mass*speed*speed*transfer*scale),momentum=Math.Min(100000,mass*speed*transfer*scale);string kind;
            switch(impact.kind){case "bullet":kind=lethal&&energy>=sever?"sever":energy>=cavity?"cavity":"puncture";break;case "cut":kind=lethal&&energy>=cut?"sever":"cut";break;case "pierce":kind=energy>=cavity?"cavity":"puncture";break;case "blunt":case "blast":kind=lethal&&energy>=crush?"crush":"bruise";break;default:kind="scorch";break;}
            return new NativeWound{part=part,kind=kind,trauma=impact.kind,energy=energy,momentum=momentum,speed=speed,destroyed=kind=="sever"||kind=="crush",severity=Clamp(energy/(impact.kind=="cut"?cut:impact.kind=="blunt"||impact.kind=="blast"?crush:sever),0,1),radius=Clamp(Clamp(impact.radius,.012f,.3f)*(kind=="cavity"?2:1),.012f,head?.15f:torso?.25f:.12f)};
        }
        public static NativeBodyImpact Projectile(string spec,float speed,float radius)
        {bool gun=spec=="pistol"||spec=="rifle"||spec=="machinegun";float size=Clamp(radius/.045f,.4f,6),mass=spec=="rifle"?.04f:spec=="machinegun"?.012f:.02f;return new NativeBodyImpact{kind=gun?"bullet":"energy",mass=gun?mass*Math.Min(12,size*size*size):.1f,speed=speed,radius=radius,transfer=gun?.4f:.2f,referenceDamage=spec=="pistol"?23:spec=="rifle"?58:spec=="machinegun"?8:spec=="fire"?22:spec=="water"?18:spec=="lightning"?12:spec=="darkness"?27:spec=="leet"?32:39};}
        public static NativeBodyImpact Melee(string spec,float rate)
        {float mass=1.6f,speed=10,radius=.08f,damage=30;string kind="cut";switch(spec){case "greatsword":mass=4.8f;radius=.14f;damage=52;break;case "daggers":mass=.4f;speed=11;radius=.035f;damage=15;break;case "shield":kind="blunt";mass=7;speed=8;radius=.24f;damage=23;break;case "emperor":kind="blunt";mass=3;speed=8;radius=.12f;damage=20;break;case "ansatsuken":kind="blunt";mass=3.5f;radius=.12f;damage=39;break;case "siren":mass=2;radius=.1f;damage=9;break;}return new NativeBodyImpact{kind=kind,mass=mass,speed=speed*(float)Math.Sqrt(Clamp(rate,.15f,5)),radius=radius,transfer=.8f,referenceDamage=damage};}
        public static NativeBodyImpact Blast(float damage)=>new NativeBodyImpact{kind="blast",mass=4,speed=(float)Math.Sqrt(Clamp(damage,0,1000)*5),radius=.25f,transfer=.5f,referenceDamage=Math.Max(1,damage)};
    }
}
