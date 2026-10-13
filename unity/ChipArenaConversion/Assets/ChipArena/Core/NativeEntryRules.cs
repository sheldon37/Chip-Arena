using System;
namespace ChipArena.UnityConversion
{
    public static class NativeEntryRules
    {
        public static bool ValidIndex(int index,string kind)=>index>=0&&(kind==NativeHordeDrop.Kind?index<=255:index<=15&&(kind=="vent"||kind=="door"||kind=="cell"));
        public static (float walk,float duration) Timeline(PveEntry entry,bool first)=>NativeHordeDrop.Is(entry)?(0,NativeHordeDrop.Duration(NativeHordeDrop.HeightOf(entry))):Timeline(entry.kind,first);
        public static (float walk,float duration) Timeline(string kind,bool first)=>kind=="vent"?(0,first?1.95f:1.6f):kind=="door"?(first?.6f:0,first?1.8f:1.2f):(first?.5f:0,first?1.5f:1);
        public static float Weight(float distance,string kind,bool wave,bool possession)=>distance<5?0:(distance<10?.4f:distance<=30?1:.6f)*(wave&&kind!="vent"?1.2f:1)*(possession&&kind=="vent"?1.5f:1);
        private static readonly float[,] Y={{0,-2.25f},{.2f,-1.75f},{.35f,-1.4f},{.6f,-1.12f},{.8f,-1.02f},{1.05f,-.72f},{1.3f,-.46f},{1.5f,-.36f},{1.7f,-.16f},{1.95f,-.07f}};
        private static readonly float[,] Z={{0,-.55f},{.8f,-.55f},{1.05f,-.48f},{1.3f,-.38f},{1.5f,-.22f},{1.72f,-.05f},{1.95f,0}};
        private static float Keyed(float t,float[,] keys)
        {int n=keys.GetLength(0);if(t<=keys[0,0])return keys[0,1];if(t>=keys[n-1,0])return keys[n-1,1];int i=0;while(i<n-2&&t>keys[i+1,0])i++;float t1=keys[i,0],p1=keys[i,1],t2=keys[i+1,0],p2=keys[i+1,1],p0=i>0?keys[i-1,1]:p1,p3=i+2<n?keys[i+2,1]:p2,u=(t-t1)/(t2-t1),dt=t2-t1,t0=i>0?keys[i-1,0]:t1-dt,t3=i+2<n?keys[i+2,0]:t2+dt,m1=(p2-p0)/(t2-t0)*dt,m2=(p3-p1)/(t3-t1)*dt,u2=u*u,u3=u2*u;return (2*u3-3*u2+1)*p1+(u3-2*u2+u)*m1+(-2*u3+3*u2)*p2+(u3-u2)*m2;}
        public static V3 Climb(float time,float activeAt){float t=1.95f-(activeAt-time);return new V3(0,Keyed(t,Y),Keyed(t,Z));}
    }
}
