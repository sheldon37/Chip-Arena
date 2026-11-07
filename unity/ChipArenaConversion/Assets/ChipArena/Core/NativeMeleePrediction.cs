using System;

namespace ChipArena.UnityConversion
{
    public static class NativeMeleePrediction
    {
        public static bool Advance(ref NativePredictionState state,InputFrame input,Func<int,V3?> lockTarget)
        {
            var w=state.MeleeWeapon;var stats=state.MeleeStats;if(w==null||stats==null)return false;
            float now=state.Time;var s=state.Swing?.PredictionCopy();state.Swing=s;
            var point=input.LockTarget>0?lockTarget?.Invoke(input.LockTarget):null;
            if(s!=null&&now<s.HitAt)
            {
                float desired=point.HasValue?(float)(Math.Atan2(point.Value.x-state.Position.x,point.Value.z-state.Position.z)*180/Math.PI):input.Yaw;
                s.Yaw=NativeMeleeFeel.Turn(s.Yaw,desired,w.id,Rules.Dt);
            }
            if(s!=null&&now>s.HitUntil&&(!s.Air||now>=s.Until)&&s.Remaining>0)
            {
                float rate=(float)stats["attackRate"];var echo=NativeMeleeFeel.Begin(w,stats,now,++state.Combo+1,s.Yaw);
                echo.Followup=true;echo.Remaining=s.Remaining-1;echo.HitAt=now+.08f/rate;echo.HitUntil=echo.HitAt+.16f/rate;
                echo.StepAt=now+.08f/rate*.55f;echo.StepUntil=echo.HitAt+.16f/rate*.8f;
                echo.Until=Math.Max(s.Until,echo.HitUntil+NativeMeleeRules.Recovery(w.id)/rate);state.Swing=s=echo;
                state.Ready=echo.Until;state.ComboUntil=echo.Until+NativeMeleeFeel.ComboPause;
            }
            if(s!=null&&now>=s.Until)state.Swing=s=null;
            if(input.Attack&&NativeMeleeKitRules.BufferOpen(s,now))state.BufferUntil=now+NativeMeleeFeel.BufferSeconds;
            bool queued=state.BufferUntil>=now,chain=queued&&NativeMeleeKitRules.ChainReady(s,now);
            bool sprint=NativeSprintAttack.Starts(chain,now>state.ComboUntil?1:state.Combo+2,state.Grounded,input,state.Sprinting);
            bool air=NativeAirAttackSimulation.Airborne(state.Grounded,state.Swimming);bool airAllowed=!air||NativeAirAttackSimulation.Starts(state.Grounded,state.AirborneAt,state.AirAttackUsed,now,input)&&!chain;
            float cost=NativeSprintAttack.Cost(w,stats,sprint)*(air?(NativeAirAttackSimulation.DownPitch(input.Pitch)?1.4f:1.25f):1);
            if(airAllowed&&!input.AbilityQ&&!input.AbilityE&&!input.Roll&&!input.Block&&state.Stamina>=cost&&
                (chain||s==null&&now>=state.Ready)&&(input.Attack||queued||input.Shoot&&s==null))
            {
                if(now>state.ComboUntil)state.Combo=-1;
                float yaw=chain?s.Yaw:input.Yaw*(float)Math.PI/180;
                s=NativeMeleeFeel.Begin(w,stats,now,++state.Combo+1,yaw);
                if(sprint)NativeSprintAttack.Apply(s,(float)stats["attackRate"]);
                if(air){NativeAirAttackSimulation.Begin(s,input.Pitch,state.Position);NativeAirAttackSimulation.Carry(s,state.Velocity);state.AirAttackUsed=true;}
                if(point.HasValue&&!sprint&&!air)
                {
                    var delta=point.Value-state.Position;delta.y=0;
                    if(V3.Dot(delta.Normalized,V3.Forward(s.Yaw*180/(float)Math.PI))>0)
                    {s.Distance=NativeMeleeFeel.Distance(w.id,chain,delta.Length);s.OpenerLunge=!chain&&s.Distance>NativeMeleeFeel.DefaultStep(w.id);}
                }
                s.Remaining=NativeAirAttackSimulation.Down(s)?0:(int)stats["extraStrikes"];
                s.Until=Math.Max(s.Until,now+(float)((w.windup+.16+stats["extraStrikes"]*.24+.12)/stats["attackRate"]));
                if(sprint)state.Sprinting=false;
                if(NativeSprintAttack.Hop(s)){state.Velocity.y=NativeSprintAttack.HopSpeed(s,state.Gravity);state.Grounded=false;}
                state.Swing=s;state.Ready=s.Until;state.Stamina-=cost;state.BufferUntil=-1;state.ComboUntil=s.Until+NativeMeleeFeel.ComboPause;
            }
            return s!=null&&now<s.Until;
        }
    }
}
