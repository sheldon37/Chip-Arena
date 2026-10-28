#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The model's per-bone reveal for the directed entrances (Core/NativeBodyReveal; owner 2026-10-28: "Sheldon intro still needs
    // revisions, it uses voxel model in the "glitching""). On the game rig at rest, the voxel pilot's own parts (CapturedRig.Boxes,
    // the volume each bone's share drops out or dithers) fall nearest their own bone's segment, so the model loses the same pieces
    // the voxel pilot did. Source scans pin the wiring: the intro's pilot stays on the model through the reveal (no voxel stand-in
    // while it builds in), the shader dithers the body, its outline and its cast shadow by the nearest segment's share, and the
    // segments are written only for the entrance rig and cleared after.
    internal static class BodyRevealChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Body reveal: "+what);count++;}
        public static void Run(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            // 1. The segments on the rest pose: each joint at its parents' offsets summed.
            var bones=CapturedRig.Bones;var at=new Vector3[bones.Length];
            for(int b=0;b<bones.Length;b++)at[b]=(bones[b].Parent>=0?at[bones[b].Parent]:Vector3.Zero)+bones[b].Position;
            var from=new Vector3[NativeBodyReveal.Segments];var to=new Vector3[NativeBodyReveal.Segments];
            Check(NativeBodyReveal.Child.Length==bones.Length&&NativeBodyReveal.Segments==bones.Length-1,"one segment per bone but the motion root");
            for(int b=1;b<bones.Length;b++)
            {
                NativeBodyReveal.Segment(b,at,Vector3.UnitY,out from[b-1],out to[b-1]);
                Check(Vector3.Distance(from[b-1],to[b-1])>.02f,"bone "+bones[b].Name+"'s segment has a length");
                int child=NativeBodyReveal.Child[b];Check(child<0||bones[child].Parent==b,"bone "+bones[b].Name+"'s segment ends on its own child");
            }
            // 2. The voxel parts' centres pick their own bone; a box whose centre lies nearer a neighbour (a part that overlaps
            // the joint) may pick that neighbour, which is one of its bone's own segment neighbours.
            int own=0,total=0;string missed="";
            foreach(var box in CapturedRig.Boxes)
            {
                if(box.Bone<=0)continue;total++;var centre=at[box.Bone]+box.Position;int pick=NativeBodyReveal.Nearest(centre,from,to);
                if(pick==box.Bone){own++;continue;}
                bool neighbour=bones[pick].Parent==box.Bone||bones[box.Bone].Parent==pick||bones[pick].Parent==bones[box.Bone].Parent;
                Check(neighbour,"the "+bones[box.Bone].Name+" part at "+centre+" picks a far bone, "+bones[pick].Name);
                missed+=(missed.Length>0?", ":"")+bones[box.Bone].Name+"→"+bones[pick].Name;
            }
            Console.WriteLine("INFO body reveal: "+own+" of "+total+" voxel parts pick their own bone"+(missed.Length>0?"; the rest a neighbour: "+missed:""));
            Check(total>40&&own>=total*85/100,"most voxel parts pick their own bone ("+own+" of "+total+")");
            // 3. Away from the body: a point beyond a hand picks that hand; above the head, the head.
            Check(NativeBodyReveal.Nearest(at[CapturedRig.LHand]+new Vector3(0,-.12f,0),from,to)==CapturedRig.LHand&&NativeBodyReveal.Nearest(at[CapturedRig.RHand]+new Vector3(0,-.12f,0),from,to)==CapturedRig.RHand,"the fingers pick the hands");
            Check(NativeBodyReveal.Nearest(at[CapturedRig.Head]+new Vector3(0,.2f,0),from,to)==CapturedRig.Head,"the crown picks the head");

            // 4. The wiring.
            string body=R("Runtime/NativeVisualBodyPresentation.cs"),fx=R("Resources/NativeVisualBodyFx.cginc"),toon=R("Resources/NativeVisualBodyToon.shader"),intros=R("Runtime/NativeIntros.cs");
            int pose=body.IndexOf("private void VisualIntroPose(",StringComparison.Ordinal),poseEnd=body.IndexOf("body.Moved=true;",pose,StringComparison.Ordinal);
            Check(pose>0&&poseEnd>pose&&!body.Substring(pose,poseEnd-pose).Contains("reveal(")&&!body.Contains("IntroSolidBones"),"the entrance's pilot is on the model through its reveal (no voxel stand-in while it builds in or glitches)");
            Check(intros.Contains("VisualIntroPose(introRig,actor,arena.Time,entrancePose,entranceReveal);")&&intros.Contains("if(VisualBodyPosed(introRig)&&i<entranceGhostHolders.Length)"),"the entrance's ghosts follow the model");
            Check(body.Contains("if(rig==introRig)VisualRevealLook(body,rig,reveal);")&&body.Contains("NativeBodyReveal.Segment(b,revealAt,headUp,out var from,out var to);")
                &&body.Contains("if(!on&&!body.RevealOn)return;")&&body.Contains("body.Block.SetFloat(RevealOnId,on?1:0);"),"the segments are the entrance rig's, written while a bone is under whole and cleared once after");
            Check(fx.Contains("float _RevealOn;float4 _RevealA[21];float4 _RevealB[21];")&&fx.Contains("if(_RevealOn<0.5)return;")&&fx.Contains("for(int k=0;k<21;k++)")&&fx.Contains("if(e<=best){best=e;share=_RevealA[k].w;}")
                &&fx.Contains("if(share<0.999)clip(share-max(frac(sin(dot(floor(world*120.0),float3(12.9898,78.233,37.719)))*43758.5453),0.001));"),"the shader picks the nearest segment and dithers by its share in the voxel reveal's cells");
            Check(toon.Contains("SpawnClip(i.worldPos);RevealClip(i.worldPos);")&&toon.Contains("SpawnClip(i.world);RevealClip(i.world);"),"the body (and its cast shadow, which runs the body's surf) and its outline");
            Check(NativeBodyReveal.Segments==21,"the shader's arrays hold every segment");
            Console.WriteLine("PASS body reveal: the entrance's pilot on the model through its reveal, bone by bone ("+count+" checks)");
        }
    }
}
#endif
