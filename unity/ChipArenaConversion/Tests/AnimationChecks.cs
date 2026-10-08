using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

internal static class AnimationChecks
{
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    public static int Run()
    {
        int clips=0,poses=0; var names=new HashSet<string>(); var row=new float[64]; var other=new float[64];
        var pose=new CapturedRig.Pose();
        for(int set=0;set<CapturedClips.Sets.Length;set++) foreach(var clip in CapturedClips.Sets[set])
        {
            clips++; Check(names.Add(clip.Name),"Duplicate clip");
            Check(clip.Decode().Length==clip.Frames*64,"Missing channels");
            float movement=0; clip.Sample(0,other);
            for(int sample=0;sample<=clip.Frames*2;sample++)
            {
                float time=sample/(float)(clip.Frames*2)*clip.Duration;
                clip.Sample(time,row); CapturedRig.Sample(row,set,pose); poses++;
                for(int c=0;c<64;c++) { Check(!float.IsNaN(row[c])&&!float.IsInfinity(row[c]),"Non-finite data"); movement+=Math.Abs(row[c]-other[c]); }
                for(int b=0;b<CapturedRig.Bones.Length;b++)
                {
                    Quaternion q=pose.Rotation[b]; Vector3 p=pose.WorldPosition[b];
                    Check(Math.Abs(q.Length()-1)<.001f,clip.Name+" invalid joint quaternion "+b);
                    Check(!float.IsNaN(p.X)&&p.Length()<4,clip.Name+" exploded joint "+b);
                    int parent=CapturedRig.Bones[b].Parent;
                    if(parent>=0) Check(Math.Abs(Vector3.Distance(p,pose.WorldPosition[parent])-CapturedRig.Bones[b].Position.Length())<.0001f,"Rig bone length changed");
                }
                Check(pose.Lowest()>=-.8651f,clip.Name+" shell below floor");
                if(set!=3)
                {
                    float length=set==2?1.69f:set==1?.84f:1.28f;
                    CapturedRig.BladeFloor(pose,false,length); pose.World();
                    Vector3 wrist=pose.WorldPosition[CapturedRig.RHand];
                    if(wrist.Y>-.78f) Check((wrist+Vector3.Transform(Vector3.UnitZ*length,pose.WorldRotation[CapturedRig.RHand])).Y>=-.7801f,"Blade below floor");
                }
            }
            Check(movement>.01f,"Static clip "+clip.Name);
            if(clip.Loop)
            {
                clip.Sample(.123f,row);clip.Sample(.123f+clip.Duration,other);
                for(int c=0;c<64;c++) Check(Math.Abs(row[c]-other[c])<.0001f,"Loop seam "+clip.Name);
            }
            if(clip.Marks.Length==4)
            {
                Check(Math.Abs(clip.AttackTime(.14f,.14f,.16f,.32f)-clip.Marks[1])<.00001f,"Contact starts outside hit window");
                Check(Math.Abs(clip.AttackTime(.30f,.14f,.16f,.32f)-clip.Marks[2])<.00001f,"Contact ends outside hit window");
                float previous=-1;
                for(int i=0;i<=200;i++) { float t=clip.AttackTime(i*.62f/200,.14f,.16f,.32f); Check(t>=previous-.00001f,"Retiming reversed"); previous=t; }
            }
        }
        // Reflection must preserve direction, joint handedness and wrist-to-blade alignment.
        Quaternion rotation=CapturedRig.Euler(.6f,-.8f,.4f); Vector3 point=new Vector3(.3f,.7f,-.4f);
        Check(Vector3.Distance(CapturedRig.Mirror(Vector3.Transform(point,rotation)),Vector3.Transform(CapturedRig.Mirror(point),CapturedRig.Mirror(rotation)))<.00001f,"Coordinate reflection mismatch");
        for(int s=0;s<4;s++)
        {
            CapturedClips.Sets[s][0].Sample(0,row); CapturedRig.Sample(row,s,pose);
            Vector3 blade=Vector3.Normalize(new Vector3(row[42],row[43],row[44]));
            Vector3 actual=Vector3.Transform(Vector3.UnitZ,Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest])*pose.WorldRotation[CapturedRig.RHand]);
            Check(Vector3.Dot(blade,actual)>.9999f,"Wrist lost blade orientation");
        }
        for(int i=0;i<=80;i++)
        {
            CapturedClips.Sets[0][0].Sample(0,row); CapturedRig.Sample(row,0,pose); CapturedRig.Roll(pose,i/80f);
            Check(Math.Abs(pose.Lowest()+.865f)<.0001f,"Roll shell floor contact");
        }
        bool rejects=false;
        try { new CapturedClip("corrupt","",30,2,false,Array.Empty<float>(),Array.Empty<float>(),"!").Decode(); } catch(FormatException) { rejects=true; }
        Check(rejects,"Corrupt clips accepted");
        Console.WriteLine("PASS captured animations: "+clips+" clips / "+poses+" poses; finite normalized joints, bone lengths, floor, loops, contact timing, wrist orientation, reflection, roll and corrupt-data rejection");
        return clips;
    }
}
