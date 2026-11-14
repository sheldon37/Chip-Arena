using UnityEngine;
namespace ChipArena.UnityConversion
{
    // A paired move's two bodies laid together as the pack pairs them (Core NativePairAlign; owner 2026-11-14: "Fang assassinate
    // animation doesn't actually connect to the enemy player model"; docs/FANGS.md "The paired moves"). The rules hold the foe a set way
    // off and the motion bank draws each clip on its own root, so the drawn hand and fang met air; each of the two bodies now takes its
    // part of the closing (turned about its root, then moved), as VisualMotionPose poses it, eased in and out over the pairing. Both
    // parts come from replicated data (the attacker's record: the pair, its clip and times; the two pilots' scales; their rig roots, drawn
    // at their replicated places and facings), so every client draws the pair alike. The drawn hips move; the feet that follow keep to
    // the floor (VisualMotionFeet), and a corpse of the blow falls from the drawn pose (VisualRigFromDrawn). Presentation only.
    public sealed partial class ArenaPresentation
    {
        // The pair body `a` is in at `time` (this frame's actors: the snapshot BeginCyberLord keeps), the attacker's clip and its time
        // as its body plays it (NativeBladeMotion.Clip), and whether `a` is the victim; false outside an aligned pairing.
        private bool VisualPairOf(Actor a,float time,out Actor attacker,out Actor victim,out string clip,out float clipTime)
        {
            attacker=victim=null;clip=null;clipTime=0;var actors=cyberSnapshot?.Actors;var bank=VisualMotionBank;if(a==null||actors==null||bank==null)return false;
            if(a.PairBy!=0){victim=a;foreach(var x in actors)if(x!=null&&x.Id==a.PairBy){attacker=x;break;}}
            else{attacker=a;var held=NativeBladeView.Of(a);if(held==null||held.Pair==0)return false;foreach(var x in actors)if(x!=null&&x.Id==held.Pair){victim=x;break;}}
            var k=NativeBladeView.Of(attacker);if(victim==null||k==null||k.Pair!=victim.Id||k.CastKind==null)return false;
            return NativeBladeMotion.Clip(attacker,k,time,bank,out clip,out clipTime,out _,out _)&&NativePairAlign.Aligns(clip);
        }
        // This body's part of the pairing (VisualMotionPose, after the clip's own turns, before the contact guard and the feet): its drawn
        // hips turned about its root and moved, by its share of the paired clip in the mix (the attacker's clip, or the victim clip's).
        private void VisualPairAlign(VisualMotion m,Transform root,Actor a,float time,float sum)
        {
            if(m.BodyHips==null||!VisualPairOf(a,time,out var attacker,out var victim,out string clip,out float c))return;
            float share;
            if(a==victim)share=m.VictimShare;
            else share=m.Index.TryGetValue(clip,out int i)&&sum>1e-4f?Mathf.Clamp01(Ease(m.Weights[i])/sum)*m.Weight:0;
            if(share<=0||!rigs.TryGetValue(attacker.Id,out var ar)||!rigs.TryGetValue(victim.Id,out var vr)||ar?.Root==null||vr?.Root==null)return;
            // The rules' placement as drawn: the victim's rig root in the attacker's (the replicated places and facings; a held foe faces
            // the rules' way, CapturedAnimation.MeleeViewOf). The partner posed later this frame stands where it stood last frame: the
            // rules hold both still through the pairing.
            Transform at=ar.Root.transform,vt=vr.Root.transform;float ay=at.eulerAngles.y;
            var g=Quaternion.Inverse(Quaternion.Euler(0,ay,0))*(vt.position-at.position);
            if(!NativePairAlign.Shift(clip,c,g.x,g.z,Mathf.DeltaAngle(ay,vt.eulerAngles.y),(attacker.RigScale+victim.RigScale)*.5f,out var forAttacker,out var forVictim))return;
            var move=a==victim?forVictim:forAttacker;if(!move.Any)return;
            if(move.Yaw!=0)m.BodyHips.RotateAround(root.position,root.up,move.Yaw*share);
            m.BodyHips.position+=root.rotation*new Vector3(move.X,0,move.Z)*share;
        }
    }
}
