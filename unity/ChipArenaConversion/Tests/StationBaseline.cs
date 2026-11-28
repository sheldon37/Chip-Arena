#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The Siren's seven kinds as the presentation draws them, measured (Tests/StationEnemyChecks holds the result to
    // Tests/station-enemies-baseline.json, which this same code wrote from the shared base before the Central Station's kinds
    // existed, commit 8a5de43): every per-kind table, every model's size and sums on all three tiers, the takes each kind walks,
    // runs, stands and wanders on, and a scripted walk, attack, hit and death through the real gait for each kind. Only APIs
    // the base already had are used, so the base compiles it unchanged. Lines are "key value" with values in round-trip form.
    internal static class StationBaseline
    {
        public const int Seven=7;
        private static string R(double v)=>v.ToString("R",CultureInfo.InvariantCulture);
        public static List<KeyValuePair<string,string>> Measure()
        {
            var o=new List<KeyValuePair<string,string>>();void Put(string k,string v)=>o.Add(new KeyValuePair<string,string>(k,v));
            for(int k=0;k<Seven;k++)
            {
                string n="kind"+k+".";
                Put(n+"id",NativeHordeKinds.Ids[k]);Put(n+"name",NativeHordeKinds.Names[k]);Put(n+"shipped",NativeHordeKinds.Shipped[k]?"1":"0");Put(n+"looks",NativeHordeKinds.Looks[k].ToString());
                Put(n+"capsule",R(NativeHordeKinds.Radius[k])+" "+R(NativeHordeKinds.Height[k])+" "+NativeHordeKinds.Axis[k]+" "+R(NativeHordeKinds.Root[k])+" "+R(NativeHordeKinds.CenterAhead[k])+" "+R(NativeHordeKinds.Scale[k]));
                Put(n+"mass",R(NativeHordeKinds.Mass[k]));Put(n+"seam",NativeHordeKinds.Seam[k]+" "+NativeHordeKinds.SeamClass[k]);Put(n+"attack",NativeHordeKinds.Default[k].ToString());
                var p=NativeHordeKinds.ProposedRow(k);Put(n+"row",p.id+" "+p.attack+" "+string.Join(" ",new[]{p.hp,p.damage,p.walk,p.run,p.reach,p.startRange,p.hit,p.cycle,p.lunge,p.impulse,p.reward,p.limbFrac,p.headFrac,p.stagger,p.smashRadius}.Select(x=>R(x))));
                Put(n+"tempo",R(NativeZombieMotion.Tempo[k]));Put(n+"head",R(NativeHordeModels.HeadWidth[k])+" "+R(NativeHordeModels.HeadDrop[k]));
                Put(n+"takes",string.Join(",",NativeZombieMotion.Walks(k).Select(NativeZombieClips.Key))+"|"+string.Join(",",NativeZombieMotion.Runs(k).Select(NativeZombieClips.Key))+"|"+string.Join(",",NativeZombieMotion.Idles(k).Select(NativeZombieClips.Key))+"|"+string.Join(",",NativeZombieMotion.Wanders(k).Select(NativeZombieClips.Key)));
                Put(n+"give",R(NativeHordeKinds.Give(k))+" "+R(NativeHordeKinds.WorldRadius(k))+" "+R(NativeHordeKinds.WorldHeight(k))+" "+R(NativeHordeKinds.RootHeight(k,false))+" "+R(NativeHordeKinds.DrawRoot(k,true))+" "+R(NativeHordeMotion(k)));
                for(int l=0;l<NativeHordeKinds.Looks[k];l++)
                {
                    var t=NativeHordeModels.Tongues(k,l);Put(n+"look"+l+".tongues",t.Length+" "+R(t.Sum(g=>g.X+g.Y*2+g.Z*3+g.Height*5+g.Phase*7+g.Joint*11+g.Layout*13)));
                    for(int tier=0;tier<3;tier++){var m=NativeHordeModels.Build(k,l,tier);Put(n+"look"+l+".tier"+tier,Mesh(m.Mesh)+(m.Jaw!=null?" jaw "+Mesh(m.Jaw):""));}
                }
                Put(n+"motion",Motion(k));Put(n+"death",Death(k));
            }
            return o;
        }
        private static double NativeHordeMotion(int k)=>NativeHordeGait.Stride(k,1.4f)+NativeHordeGait.Duty(k,1.4f)*3+NativeHordeGait.RunBlend(k,2.5f)*7+NativeHordeGait.Reach(k)*11;
        // A model's size and its sums (positions, normals, colours, emission; the bones they ride).
        private static string Mesh(WeaponMesh mesh)
        {
            int tris=mesh.Triangles,verts=0;double px=0,py=0,pz=0,nn=0,cc=0,ee=0,bb=0;
            foreach(var s in mesh.Sections){verts+=s.Positions.Count;bb+=s.Bone*s.Positions.Count;foreach(var v in s.Positions){px+=v.X;py+=v.Y;pz+=v.Z;}foreach(var v in s.Normals)nn+=Math.Abs(v.X)+Math.Abs(v.Y)*2+Math.Abs(v.Z)*3;foreach(var c in s.Colors)cc+=c.X+c.Y*2+c.Z*3+c.W*5;foreach(var e in s.Emission)ee+=e.X+e.Y*2+e.Z*3+e.W*5;}
            return tris+" "+verts+" "+R(px)+" "+R(py)+" "+R(pz)+" "+R(nn)+" "+R(cc)+" "+R(ee)+" "+R(bb);
        }
        // A scripted run through the real gait: stand, walk a curve, attack, take a heavy blow, run (sums of every joint).
        private static string Motion(int k)
        {
            var g=new NativeHordeGait();var react=new NativeHordeReact();react.Bind(k);var pose=new CapturedRig.Pose();var st=NativeHordeKinds.ProposedRow(k);float sc=NativeHordeKinds.Scale[k];
            float strike=NativeHordeKinds.Strike(st.hit,false,0),recover=NativeHordeKinds.Recover(st.hit,st.cycle,false,0);double sp=0,sq=0,sy=0,sf=0;var at=new Vector3(0,NativeHordeKinds.Root[k],0);float yaw=0;
            for(int i=0;i<360;i++)
            {
                float t=i/60f,speed=i<60?0:i<180?st.walk/sc:i<240?0:st.run/sc;yaw+=i>=60&&i<180?.5f:0;float r=yaw*(float)Math.PI/180;var v=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r))*speed;at+=v/60;
                if(i==200)react.Add(t,new Vector2(0,-1),StaggerWeight.Heavy,48,true,false,true,st.stagger);
                var f=new HordeMotionInput{Kind=k,Id=60000+k,Seed=NativeHordeKinds.Seed(60000+k),Time=t,Root=at,Velocity=v,Yaw=yaw,Ground=0,Health=1,AttackAge=i>=185&&i<185+recover*60?(i-185)/60f:float.NaN,Strike=strike,Recover=recover,
                    Attack=NativeHordeKinds.Default[k],Side=1,HasTarget=true,Target=new Vector3(1,.85f,3)};
                g.Pose(f,pose,react);
                for(int b=0;b<22;b++){var w=pose.WorldPosition[b];sp+=w.X+w.Y*2+w.Z*3;var q=pose.WorldRotation[b];sq+=Math.Abs(q.X)+Math.Abs(q.Y)*2+Math.Abs(q.Z)*3+Math.Abs(q.W)*5;}
                sy+=g.Yaw;for(int ft=0;ft<4;ft++)sf+=g.FootWorld[ft].X+g.FootWorld[ft].Z*2+(g.FootAir[ft]?1:0);
            }
            return R(sp)+" "+R(sq)+" "+R(sy)+" "+R(sf);
        }
        private static string Death(int k)
        {
            var pose=new CapturedRig.Pose();double s=0;foreach(var push in new[]{new Vector2(0,-1),new Vector2(1,0),new Vector2(0,1),new Vector2(-.7f,.7f)})for(int i=0;i<=23;i++)
            {NativeHordeGait.Death(pose,k,77u+(uint)k,i*.1f,push);pose.World();for(int b=0;b<22;b++){var w=pose.WorldPosition[b];s+=w.X+w.Y*2+w.Z*3;}}
            return R(s);
        }
        public static string Write(){var sb=new StringBuilder();sb.Append("# The Siren's seven kinds as drawn at the shared base 8a5de43 (Tests/StationBaseline.cs wrote it; Tests/StationEnemyChecks compares).\n");foreach(var p in Measure())sb.Append(p.Key).Append(' ').Append(p.Value).Append('\n');return sb.ToString();}
    }
}
#endif
