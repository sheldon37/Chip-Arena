using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2's weapon anchors on the drawn weapon (docs/INTROS-V2.md 2a; integrator 2026-11-20: "the anchors must be right for every
    /// weapon"): for each of the fifteen v2 weapons, as drawn with no clip playing (the weapon on its hand's weapon frame, Runtime
    /// IntroV2Drawn), in both skins (the Standard model as built, NativeWeaponModels.Build; the Prototype bank's "weapon-"+spec), on each
    /// hand that holds it: the grip (NativeIntroWeapon.Grip: the frame's origin, the shield's bar) on or in the weapon, the tip
    /// (NativeIntroWeapon.Tip) on its far end, and the muzzle (a gun's at the drawn skin's crown, NativeMuzzleFx.Crown; a staff's head; the bow's
    /// arrow rest) on its front, each within TipNear of the weapon's surface. The Katana among them: the clip-free Katana lies along its
    /// frame's +z with every other weapon (its tip socket on its blade's point), so a blade line and RightTip on +z are right for it.
    /// </summary>
    internal static class IntroAnchorChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro anchors: "+what);count++;}
        /// <summary>How far an anchor may sit off the weapon's surface (m): a socket a little proud of a muzzle's face or a point.</summary>
        public const float TipNear=.045f,GripNear=.045f;
        /// <summary>How much of the weapon may reach past its tip socket along the grip-to-tip line (m): a staff's crown over its cast socket.</summary>
        public const float TipFar=.06f;
        public static void Run(string root)
        {
            count=0;string text;
            using(var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json"))))
            using(var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString())))using(var zip=new GZipStream(bytes,CompressionMode.Decompress))using(var reader=new StreamReader(zip))text=reader.ReadToEnd();
            float worst=0;string worstWhat="";int models=0;
            foreach(var spec in NativeIntroCatalog.WeaponSpecs)
            {
                foreach(bool standard in new[]{true,false})
                {
                    var tris=new Dictionary<int,List<Vector3>>();
                    if(standard)
                    {
                        var mesh=NativeWeaponModels.Build(spec,NativeWeaponModels.Full);Check(mesh!=null,spec+" has a Standard model");
                        foreach(var s in mesh.Sections){if(!tris.TryGetValue(s.Bone,out var l))tris[s.Bone]=l=new List<Vector3>();for(int i=0;i+2<s.Indices.Count;i+=3){l.Add(s.Positions[s.Indices[i]]);l.Add(s.Positions[s.Indices[i+1]]);l.Add(s.Positions[s.Indices[i+2]]);}}
                    }
                    else
                    {
                        if(!Bank(text,"weapon-"+spec,out var model))continue;// (the blade classes and the bow have no Prototype Series model)
                        foreach(var sec in model.GetProperty("sections").EnumerateArray())
                        {
                            int bone=sec.GetProperty("bone").GetInt32();var f=sec.GetProperty("positions").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray();var idx=sec.GetProperty("indices").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                            if(!tris.TryGetValue(bone,out var l))tris[bone]=l=new List<Vector3>();
                            foreach(int i in idx)l.Add(new Vector3(-f[i*3],f[i*3+1],f[i*3+2]));// (Unity frame: x mirrored into the source frame)
                        }
                    }
                    models++;
                    foreach(bool left in new[]{true,false})
                    {
                        if(!NativeIntroWeapon.Holds(spec,left))continue;string what=spec+(standard?" (Standard)":" (Prototype)")+(left?" in the left hand":" in the right hand");
                        Check(tris.TryGetValue(left?NativeWeaponModels.LeftHand:NativeWeaponModels.RightHand,out var t)&&t.Count>=3,what+" is drawn on that hand's frame");
                        bool sockets=NativeIntroWeapon.Grip(spec,left,out var grip);sockets&=NativeIntroWeapon.Tip(spec,left,out var tip);sockets&=NativeIntroWeapon.Muzzle(spec,left,out var muzzle);
                        Check(sockets,what+": grip, tip and muzzle sockets");
                        // (a gun's muzzle anchor is the drawn skin's crown, where the match's flash leaves it)
                        if(NativeWeaponDressing.Gun(spec))muzzle=new Vector3(0,0,NativeMuzzleFx.Crown(spec,standard));
                        float dg=Inside(t,grip)?0:Surface(t,grip),dt=Surface(t,tip),dm=Surface(t,muzzle);
                        foreach(var (name,d,near) in new[]{("grip",dg,GripNear),("tip",dt,TipNear),("muzzle",dm,TipNear)})
                        {
                            Check(d<=near,what+": the "+name+" anchor is "+(d*100).ToString("0.0")+" cm off the drawn weapon (at most "+(near*100).ToString("0.0")+")");
                            if(d>worst){worst=d;worstWhat=what+" "+name;}
                        }
                        // the tip at the weapon's far end along its line from the grip (no part of it further out along that line by more than TipNear)
                        // (not the shield's: a plate has no one far end; its tip is the match's socket on its upper face, NativeWeaponDressing.BladeTip)
                        var line=tip-grip;if(spec!="shield"&&line.LengthSquared()>1e-4f){line=Vector3.Normalize(line);float far=t.Max(p=>Vector3.Dot(p-grip,line)),at=Vector3.Dot(tip-grip,line);Check(far-at<=TipFar,what+": the tip is the far end ("+(far-at).ToString("0.000")+" m of weapon beyond it)");}
                    }
                }
            }
            Console.WriteLine("PASS intro anchors: "+count+" checks ("+models+" weapon models of the fifteen v2 weapons: every grip, tip and muzzle anchor on the drawn weapon, the worst "+(worst*100).ToString("0.0")+" cm, "+worstWhat+")");
        }
        private static bool Bank(string text,string id,out JsonElement model)
        {
            model=default;int at=text.IndexOf("\"id\":\""+id+"\"",StringComparison.Ordinal);if(at<0)return false;
            int start=text.LastIndexOf('{',at),depth=0,end=start;
            for(int i=start;i<text.Length;i++){if(text[i]=='{')depth++;else if(text[i]=='}'&&--depth==0){end=i;break;}}
            using var doc=JsonDocument.Parse(text.Substring(start,end-start+1));model=doc.RootElement.Clone();return true;
        }
        /// <summary>Whether `p` is inside the triangle soup (a ray along +x crosses it an odd number of times; nudged off the seams).</summary>
        private static bool Inside(List<Vector3> t,Vector3 p)
        {
            var o=p+new Vector3(0,1.3e-4f,.7e-4f);int crossings=0;
            for(int i=0;i+2<t.Count;i+=3){if(RayHits(o,t[i],t[i+1],t[i+2]))crossings++;}
            return (crossings&1)==1;
        }
        private static bool RayHits(Vector3 o,Vector3 a,Vector3 b,Vector3 c)
        {
            var d=Vector3.UnitX;var e1=b-a;var e2=c-a;var h=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,h);if(Math.Abs(det)<1e-12f)return false;
            float f=1/det;var s=o-a;float u=f*Vector3.Dot(s,h);if(u<0||u>1)return false;var q=Vector3.Cross(s,e1);float v=f*Vector3.Dot(d,q);if(v<0||u+v>1)return false;
            return f*Vector3.Dot(e2,q)>0;
        }
        /// <summary>The distance from `p` to the nearest triangle of the soup.</summary>
        private static float Surface(List<Vector3> t,Vector3 p){float best=float.MaxValue;for(int i=0;i+2<t.Count;i+=3)best=Math.Min(best,(Closest(p,t[i],t[i+1],t[i+2])-p).Length());return best;}
        // (Ericson, Real-Time Collision Detection 5.1.5: the closest point on a triangle)
        private static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float den=1/(va+vb+vc);return a+ab*(vb*den)+ac*(vc*den);
        }
    }
}
