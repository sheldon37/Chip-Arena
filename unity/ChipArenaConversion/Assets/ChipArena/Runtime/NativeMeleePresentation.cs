using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Presentation-only data for the captured-melee layers (Core/MeleeMotion): the struck pilot's hit reaction, and the
    // swing window, authoritative step and Hurricane flag the footwork reads. Nothing here feeds back into authority:
    // Actors carry the authority timing, heading and movement in the existing snapshot.
    public sealed partial class NativePilot { public MeleeReaction Reaction; }
    public sealed partial class NativeArena
    {
        // shared/simulation.ts damage(): a direct, unarmoured hit that leaves the pilot alive records its reaction; so does a
        // melee blow dealt as non-direct damage (the Siren's claws), so the struck pilot reacts to it (Core/NativeHitReact).
        private void MeleeHitReaction(NativePilot target,Vector3 direction,Vector3? hitPoint,float actual,bool head,bool direct)
        {
            if(NativeMeleeKitRules.LightArmour(target.MeleeKit,Time,actual)||SirenArmor(target)||actual<=0||target.Hp<=0||!direct||!hitPoint.HasValue&&direction.magnitude<=.01f)return;
            float facing=target.Reaction!=null&&target.StunEnd>Time?-target.Reaction.Yaw*Mathf.Rad2Deg:target.Yaw;
            var r=MeleeReaction.Hit(Vec(target.Body.position),facing,Vec(direction),Vec(hitPoint??target.Body.position+Vector3.up*.1f),Time,actual,head);
            r.Until=Mathf.Max(r.Until,target.StunEnd);target.Reaction=r;
        }
        // successfulBlock: the parried attacker reels from the blocker through its stun.
        private void MeleeParryReaction(NativePilot attacker,Vector3 away)=>attacker.Reaction=MeleeReaction.ParryOf(Vec(attacker.Body.position),attacker.Yaw,Vec(away),Time,attacker.StunEnd);
        // settleRiposte: a counter that ended the riposte early shortens the reaction it stretched.
        private void SettleMeleeReaction(NativePilot target){var h=target.Reaction;if(h!=null&&h.Until>Time)h.Until=h.Parry?Time:Mathf.Min(h.Until,h.At+.24f);}
        private void MeleePresentation(NativePilot p,Actor a)
        {
            var s=p.Swing;bool live=s!=null&&Time<p.AttackEnd;
            a.SwingAt=s!=null&&Time<=s.HitUntil?s.HitAt:-100;a.SwingUntil=s!=null&&Time<=s.HitUntil?s.HitUntil:-100;
            a.CommitAt=live?s.At:-100;a.CommitUntil=live?p.AttackEnd:-100;a.CommitYaw=live?s.Yaw*Mathf.Rad2Deg:a.Yaw;
            a.StepAt=live?s.StepAt:0;a.StepUntil=live?s.StepUntil:0;a.StepDistance=live?s.Distance:0;a.Followup=live&&s.Followup;a.SprintAttack=live&&s.SprintAttack;a.OpenerLunge=live&&s.OpenerLunge;
            a.AirKind=live?s.AirKind:"";a.FallHeight=live?s.FallHeight:0;a.AirStartY=live?s.AirStartY:0;a.AirAttack=live&&s.Air;a.PlungeAt=live?s.PlungeAt:0;a.ImpactAt=live?s.ImpactAt:-1;a.AirborneAt=p.AirborneAt;a.AirAttackUsed=p.AirAttackUsed;
            a.SwingRemaining=live?s.Remaining:0;a.ChainAt=live?s.ChainAt:0;a.MeleeBufferUntil=p.MeleeBufferedUntil;a.ComboUntil=p.MeleeComboUntil;a.Hurricane=Has(p,"hurricane");
            var h=p.Reaction;a.ReactionAt=h?.At??-100;a.ReactionUntil=h?.Until??0;a.ReactionYaw=h?.Yaw??0;a.ReactionTurn=h?.Turn??0;a.ReactionStrength=h?.Strength??0;
            a.ReactionDir=h!=null?new V3(h.Direction.X,h.Direction.Y,h.Direction.Z):new V3();a.ReactionPoint=h!=null?new V3(h.Point.X,h.Point.Y,h.Point.Z):new V3();a.ReactionZone=h?.Zone??0;a.ReactionParry=h?.Parry??false;
        }
    }
}
