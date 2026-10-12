using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeAudio
    {
        // ---------- movement feel: the dagger air flip's whoosh (owner 2026-10-12, "Daggers can double-jump") ----------
        // move.jump and move.land are played by MoveCues from the replicated contact flag. An air jump has no flag: each pilot's
        // presented root is watched by the same rule the jump animation uses (Core NativeJumpTrack), and a dagger pilot's air
        // jump plays the dagger swing a little faster as the flip's whoosh (no new bank key). Presentation only.
        private readonly Dictionary<int,NativeJumpTrack> jumpTracks=new Dictionary<int,NativeJumpTrack>();
        private int jumpGeneration=-1;
        /// <summary>The flip whoosh: the dagger swing at this rate and gain (own pilot quieter, as MoveCues plays it).</summary>
        public const float FlipWhooshRate=1.2f,FlipWhooshGain=.5f,FlipWhooshOwnGain=.4f;
        private void MoveFeelCues(NativeArena arena)
        {
            if(arena==null)return;if(arena.Generation!=jumpGeneration){jumpGeneration=arena.Generation;jumpTracks.Clear();}
            float t=arena.Replica?arena.ReplicaRenderTime:arena.Time;var me=arena.Local;
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;
                if(!jumpTracks.TryGetValue(p.Seat.id,out var track))jumpTracks[p.Seat.id]=track=new NativeJumpTrack();
                int jumps=track.AirJumps;
                bool blocked=a.Hp<=0||a.Rolling(t)||a.StunUntil>t||a.JuggleUntil>t||NativeSpecialPose.Active(a,t)||NativeSpecialPose.Down(a,t);
                track.Observe(t,a.Grounded,p.Body.transform.position.y,blocked);
                if(track.AirJumps>jumps&&a.Hp>0&&NativeJumpRules.Flips(p.Seat.spec)){bool mine=p==me;BankPlay("melee.daggers.swing",p.Body.position,mine?FlipWhooshOwnGain:FlipWhooshGain,0,null,mine,null,FlipWhooshRate);}
            }
        }
    }
}
