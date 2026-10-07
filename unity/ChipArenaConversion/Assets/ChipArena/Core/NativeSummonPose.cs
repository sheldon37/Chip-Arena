using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    public static class NativeSummonPose
    {
        [ThreadStatic] private static Vector3[] angles;
        public static void Sample(CapturedRig.Pose p,bool toy,float t,float speed,float attackAge,float hitAge,int idLength)
        {
            p.Reset();float gait=t*speed*2.2f,walk=(float)Math.Sin(gait);var e=angles??(angles=new Vector3[22]);Array.Clear(e,0,e.Length);e[0].Z=(float)Math.Sin(gait*.5f)*.075f;e[2].X=toy?0:.2f;e[14].X=walk*.38f;e[18].X=-walk*.38f;e[15].X=Math.Max(0,walk)*.5f;e[19].X=Math.Max(0,-walk)*.5f;
            if(toy){e[7]=new Vector3(-.35f,0,-.1f);e[11]=new Vector3(-.2f,0,.05f);e[8].X=-1.3f;e[12].X=-1.1f;}
            else{e[7].X=-1.25f+walk*.12f;e[11].X=-1.25f-walk*.12f;e[8].X=-.18f;e[12].X=-.3f;e[5].Z=(float)Math.Sin(t*1.7f+idLength)*.12f;}
            float windup=toy?.45f:.42f,recover=toy?.55f:.48f,a=attackAge;
            if(a>=0&&a<windup+recover)
            {
                if(!toy){float wind=MathUtil.Smooth(a/windup),strike=MathUtil.Smooth((a-windup)/.12f),rec=MathUtil.Smooth((a-windup-.12f)/(recover-.12f)),hold=a<windup?wind:1-rec,swing=a<windup?0:strike*(1-rec);
                    e[2].X=.2f-.38f*hold+.78f*swing;p.Position[3].Z=.3f*swing;e[5].X=-.3f*hold+.25f*swing;e[7].X=-1.25f-1.35f*hold+1.9f*swing;e[11].X=-1.25f-1.5f*hold+2.05f*swing;e[7].Z=.35f*hold;e[11].Z=-.35f*hold;e[8].X=-.18f-.9f*hold+.7f*swing;e[12].X=-.3f-.9f*hold+.8f*swing;e[14].X=.45f*swing;e[18].X=-.55f*swing;e[19].X=.4f*swing;}
                else{float aim=a<windup?MathUtil.Smooth(a/(windup*.7f)):1-MathUtil.Smooth((a-windup-.18f)/(recover-.18f)),kick=a>=windup?Math.Max(0,1-(a-windup)/.12f):0;
                    e[11].X=-.2f-1.3f*aim+.35f*kick;e[11].Y=.25f*aim;e[12].X=-1.1f+aim;e[7].X=-.35f-1.05f*aim+.3f*kick;e[7].Z=-.1f-.35f*aim;e[8].X=-1.3f+.55f*aim;e[5]=new Vector3(.12f*aim,.25f*aim,0);e[2].X=-.12f*kick;p.Position[0].Z=-.06f*kick;e[14].X=.3f*aim;e[18].X=-.25f*aim;}
            }
            if(hitAge<.28f){float h=(float)Math.Sin(MathUtil.Clamp(hitAge/.28f,0,1)*Math.PI);e[2].X-=.4f*h;e[5].X-=.35f*h;p.Position[0].Z-=.08f*h;e[7].X+=.4f*h;e[11].X+=.3f*h;}
            for(int i=0;i<22;i++)p.Rotation[i]=CapturedRig.Euler(e[i].X,e[i].Y,e[i].Z);
        }
        public static void Death(CapturedRig.Pose p,bool toy,float age)
        {Sample(p,toy,0,0,100,100,0);float f=MathUtil.Smooth(age/.5f);if(toy){p.Rotation[0]=CapturedRig.Euler(-1.45f*f);p.Position[0].Y=-.3f*f;p.Position[0].Z=-.25f*f;p.Rotation[14]=p.Rotation[18]=p.Rotation[15]=p.Rotation[19]=Quaternion.Identity;}else{p.Rotation[0]=CapturedRig.Euler(1.2f*f);p.Position[0].Y=-.45f*f;p.Rotation[15]=p.Rotation[19]=CapturedRig.Euler(1.3f*f);p.Rotation[14]=p.Rotation[18]=CapturedRig.Euler(-.9f*f);p.Rotation[5]=CapturedRig.Euler(.5f*f);p.Rotation[7]=p.Rotation[11]=CapturedRig.Euler(-.4f-.6f*f);}p.Position[0].X=age>.55f?(float)Math.Sin(age*90)*.03f:0;}
    }
}
