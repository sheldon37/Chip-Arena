using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Melee hit reactions (Core/NativeHitReact), presentation only. The ledger classifies each frame's Hit / Block / Emperor /
    // Arcade events (melee or not, the attacker's weapon heft and position) for every rig's hit layer, so host, guests and
    // the killcam replay pose the same reactions from the same replicated data; nothing here feeds back into authority, aim
    // or input.
    public sealed partial class ArenaPresentation
    {
        private readonly NativeHitLedger hitLedger=new NativeHitLedger();
        /// <summary>Reads this frame's events (once per event id); Render calls it before posing, the camera kick may call it first.</summary>
        public void ObserveHits(Snapshot s)=>hitLedger.Observe(s);
        /// <summary>
        /// The local pilot's first-person camera kick for a melee hit or a blocked melee blow, applied to the camera after the
        /// aim point was taken (the view is rebuilt from the look angles every frame, so the kick never accumulates or reaches
        /// aim). `scale` is the settings scale: 0 with camera shake off, reduced with reduced effects.
        /// </summary>
        public void HitKick(Transform view,Snapshot s,int localId,float time,float scale)
        {
            if(view==null||s?.Actors==null||!(scale>0))return;Actor local=null;
            foreach(var a in s.Actors)if(a!=null&&a.Id==localId){local=a;break;}
            if(local==null)return;hitLedger.Hit(local.Id,local.ReactionAt,out var hit);hitLedger.Block(local.Id,time,.5f,out var block);
            if(!NativeHitRules.CameraKick(local,time,hit,block,scale,out var k))return;
            view.rotation*=Quaternion.Euler(k.Pitch,k.Yaw,k.Roll);view.position+=view.rotation*new Vector3(k.Offset.X,k.Offset.Y,k.Offset.Z);
        }
        // The first-person view model drops its guard while the local pilot is stunned and jolts back on a melee hit.
        private Vector3 HitViewDip(Actor source,float time,out float pitch)
        {
            hitLedger.Hit(source.Id,source.ReactionAt,out var hit);var d=NativeHitRules.ViewDip(source,time,hit,out pitch);return new Vector3(d.X,d.Y,d.Z);
        }
    }
}
