using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The zombies' voices (owner 2026-10-15: "can we add sounds for footsteps and zombies?"; Core NativeZombieVoice) on NativeAudio's
    // existing voices: from HordeCues, every living add's hunting calls (seeded, the same moments on every peer), its attack's
    // wind-up call at the replicated LastAttack and the spitter's spit at its strike; the town zombies' moans and alert shriek
    // (Runtime/NativeHordeRoamPresentation.cs); a hurt on each replicated hit and a death on each replicated MinionDeaths entry.
    // Core NativeZombieVoices keeps one call at a time per add and caps the horde (three calm calls, six in all, the nearest
    // first within 32 m). Each call plays its key's bank clips, else an existing stand-in key, else its built-in synthesized takes.
    // Presentation only: gameplay, timing, hitboxes and networking are unchanged.
    public sealed partial class NativeAudio
    {
        private readonly NativeZombieVoices zombieVoices=new NativeZombieVoices();private int zombieEvent;
        /// <summary>One living add's calls this frame: its hunting call (not while calm or for the Hulk, which roars), its wind-up's, its
        /// spit's.</summary>
        private void ZombieCues(NativeMinion m,int kind,NativeHordeKinds.Attack attack,float strike,Vector3 pos,float t,bool calm)
        {
            zombieVoices.Seen(m.Id,kind,t);uint seed=NativeHordeKinds.Seed(m.Id);
            if(t>=m.LastAttack&&t-m.LastAttack<.15f&&zombieVoices.First(m.Id,NativeZombieVoices.AttackSlot,m.LastAttack))ZombieCall(m.Id,NativeZombieVoice.Windup(attack),pos,seed,kind,t);
            var spit=NativeZombieVoice.Strike(attack);float at=m.LastAttack+strike;
            if(spit!=NativeZombieCall.None&&t>=at&&t-at<.12f&&zombieVoices.First(m.Id,NativeZombieVoices.StrikeSlot,m.LastAttack))ZombieCall(m.Id,spit,pos,seed,kind,t);
            if(calm||kind==NativeHordeKinds.Hulk)return;
            float call=NativeZombieVoice.CallAt(seed,t);
            if(!float.IsNaN(call)&&t>=call&&t-call<.12f&&zombieVoices.First(m.Id,NativeZombieVoices.CallSlot,call))ZombieCall(m.Id,NativeZombieVoice.Hunting(kind),pos,seed,kind,t);
        }
        /// <summary>Each fresh replicated hit on a living add hurts it; each fresh death of an add seen alive rattles.</summary>
        private void ZombieEvents(NativeArena arena,float t)
        {
            var events=arena.Events;int newest=0;for(int i=0;i<events.Count;i++)if(events[i].Id>newest)newest=events[i].Id;if(newest<zombieEvent)zombieEvent=0;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=zombieEvent||t-e.Time>.3f||t<e.Time)continue;
                if((e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot)||e.Target<10000||!(e.Amount>0))continue;
                var m=ZombieOf(arena,e.Target);if(m?.Pve==null||m.Hp<=0||m.Body==null)continue;int kind=zombieVoices.KindOf(m.Id);
                ZombieCall(m.Id,NativeZombieCall.Hurt,m.Body.position,NativeHordeKinds.Seed(m.Id),kind<0?NativeHordeKinds.Shambler:kind,t);
            }
            zombieEvent=newest;
            var deaths=arena.MinionDeaths;
            for(int i=0;i<deaths.Count;i++)
            {
                var d=deaths[i];if(d==null||t<d.at||t-d.at>.15f)continue;int kind=zombieVoices.KindOf(d.id);if(kind<0)continue;
                if(zombieVoices.First(d.id,NativeZombieVoices.DeathSlot,d.at))ZombieCall(d.id,NativeZombieCall.Death,d.position,NativeHordeKinds.Seed(d.id),kind,t);
            }
        }
        private static NativeMinion ZombieOf(NativeArena arena,int id){var list=arena.Minions;for(int i=0;i<list.Count;i++)if(list[i].Id==id)return list[i];return null;}
        /// <summary>One call of add `id` at `pos`, if NativeZombieVoices admits it: its key's clips, else its stand-in, else the
        /// synthesized takes (a death while the bank still has the old add-death grunt: that grunt, from its event).</summary>
        private void ZombieCall(int id,NativeZombieCall call,Vector3 pos,uint seed,int kind,float t,float scale=1)
        {
            if(call==NativeZombieCall.None||Muted||Effects<=0||voices==null)return;
            if(!zombieVoices.Admit(id,call,t,(pos-listener).magnitude))return;
            string key=NativeZombieVoice.Key(call);float gain=NativeZombieVoice.Gain(call)*scale,rate=NativeZombieVoice.Rate(seed,kind);var priority=NativeZombieVoice.Priority(call);
            if(call==NativeZombieCall.Death&&!bank.Has(key)&&bank.Has("pve.add.death"))return;
            CreatureCue(key,NativeZombieVoice.StandIn(call),rate*NativeZombieVoice.StandInRate(call),pos,gain,rate,priority);
        }
    }
}
