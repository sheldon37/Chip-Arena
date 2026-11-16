using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public readonly struct NativeRecording
    {
        public readonly string Key, Take;
        public readonly float Gain, Rate;

        public NativeRecording(string key, float gain = 1, float rate = 1, string take = null)
        {
            Key = key;
            Gain = gain;
            Rate = rate;
            Take = take;
        }
    }

    public static class NativeRecordedCues
    {
        // Dedicated recordings take precedence over these substitutions from the installed bank.
        public static NativeRecording Weapon(string id)
        {
            switch (id)
            {
                case "blade-cut": return new NativeRecording("melee.sword.swing", .87f);
                case "blade-slice": return new NativeRecording("melee.sword.hit", .82f);
                case "blade-draw": return new NativeRecording("saw.scrape", .57f, 1.16f, "0");
                case "blade-sheathe": return new NativeRecording("saw.scrape", .39f, .83f, "0");
                case "blade-click": return new NativeRecording("gun.rocket.dud", .43f, 1.37f);
                case "blade-split": return new NativeRecording("death.style.gib", .72f);
                case "blade-dash": return new NativeRecording("move.roll", .77f, 1.08f);
                case "blade-wind": return new NativeRecording("melee.greatsword.spin", .60f);
                case "blade-hone": return new NativeRecording("saw.scrape", .4f, .94f, "0");
                case "blade-wave": return new NativeRecording("melee.greatsword.swing", .75f, 1.14f);
                case "blade-clash": return new NativeRecording("melee.block", .85f);
                case "fang-cut": return new NativeRecording("melee.daggers.swing", .76f, 1.12f);
                case "fang-stab": return new NativeRecording("melee.daggers.hit", .78f);
                case "fang-throw": return new NativeRecording("melee.daggers.throw", .73f, 1.08f);
                case "fang-hiss": return new NativeRecording("turret.overheat", .4f, 1.24f, "0");
                case "fang-vanish": return new NativeRecording("magic.blink-strike", .63f, 1.15f);
                case "fang-smoke": return new NativeRecording("turret.overheat", .57f, .9f, "0");
                case "fang-bloom": return new NativeRecording("blast.toxic", .67f);
                case "ring-cut": return new NativeRecording("melee.sword.swing", .76f, .85f);
                case "ring-slice": return new NativeRecording("melee.sword.hit", .74f, 1.18f);
                case "ring-throw": return new NativeRecording("melee.greatsword.spin", .79f, 1.25f);
                case "ring-ping": return new NativeRecording("melee.shield.throw", .72f, 1.16f, "2");
                case "ring-catch": return new NativeRecording("gun.rocket.dud", .57f, 1.12f);
                case "ring-flick": return new NativeRecording("melee.daggers.throw", .71f, 1.19f);
                case "ring-burst": return new NativeRecording("blast.kinetic", .8f);
                default: return default;
            }
        }

        public static NativeRecording Expedition(string id)
        {
            switch (id)
            {
                case "boss.station.doors-warn": return new NativeRecording("alarm.trip", .65f);
                case "boss.station.doors": return new NativeRecording("break.metal", .8f);
                case "boss.station.unfold": return new NativeRecording("structure.groan", .76f);
                case "boss.station.swipe-windup": return new NativeRecording("melee.greatsword.swing", .64f);
                case "boss.station.swipe": return new NativeRecording("structure.rebar", .77f);
                case "boss.station.arc": return new NativeRecording("blast.static", .8f);
                case "boss.station.brakes": return new NativeRecording("turret.overheat", .8f, .89f, "0");
                case "boss.station.impact":
                case "boss.station.express-hit": return new NativeRecording("break.metal", .85f);
                case "boss.station.slam": return new NativeRecording("structure.thud", .95f, .87f);
                case "boss.station.shockwave": return new NativeRecording("blast.kinetic", .68f);
                case "boss.station.hurt": return new NativeRecording("gun.rocket.dud", .6f, .88f);
                case "boss.station.lamp-break": return new NativeRecording("break.glass", .68f);
                case "boss.station.panto-break": return new NativeRecording("structure.snap", .82f);
                case "boss.station.death": return new NativeRecording("structure.land.metal", .94f);
                case "boss.ironworks.arrive.rumble": return new NativeRecording("structure.rumble", .66f);
                case "boss.ironworks.arrive.crack":
                case "boss.ironworks.crust.crack": return new NativeRecording("structure.crack", .74f);
                case "boss.ironworks.arrive.emerge":
                case "boss.ironworks.crust.break": return new NativeRecording("break.stone", .79f);
                case "boss.ironworks.pour.windup":
                case "boss.ironworks.blow.inhale": return new NativeRecording("turret.overheat", .65f, .83f, "0");
                case "boss.ironworks.blow.land": return new NativeRecording("magic.fire.hit", .63f);
                case "boss.ironworks.stomp.lift":
                case "boss.ironworks.rear.creak": return new NativeRecording("structure.groan", .73f);
                case "boss.ironworks.stomp.impact":
                case "boss.ironworks.slam.impact": return new NativeRecording("structure.thud", .92f, .86f);
                case "boss.ironworks.slam.wave": return new NativeRecording("blast.quake", .7f);
                case "boss.ironworks.grab.snap": return new NativeRecording("hazard.spikes", .75f);
                case "boss.ironworks.deslag.land": return new NativeRecording("blast.debris", .68f);
                case "boss.ironworks.charge.crash":
                case "boss.ironworks.death.buckle": return new NativeRecording("structure.land.metal", .9f);
                case "boss.ironworks.knee.buckle":
                case "boss.ironworks.hurt.clang": return new NativeRecording("structure.rebar", .61f);
                case "pve.ironworks.stoker.hook.windup":
                case "pve.ironworks.rigger.rattle": return new NativeRecording("magic.shackles", .68f, 1f, "1");
                case "pve.ironworks.stoker.hook.throw":
                case "pve.ironworks.rigger.throw": return new NativeRecording("melee.daggers.throw", .68f);
                case "pve.ironworks.stoker.hook.yank": return new NativeRecording("magic.shackles", .68f, .87f, "0");
                case "pve.ironworks.slagback.shed": return new NativeRecording("break.stone", .72f);
                case "pve.ironworks.slagback.slam": return new NativeRecording("structure.thud", .8f);
                case "pve.ironworks.ladleman.pour.windup": return new NativeRecording("turret.overheat", .63f, .86f, "0");
                case "pve.ironworks.ladleman.spill": return new NativeRecording("magic.fire.hit", .74f);
                case "pve.ironworks.rigger.land": return new NativeRecording("move.land", .79f, .85f);
                case "pve.ironworks.rigger.snap": return new NativeRecording("structure.snap", .76f);
                case "pve.ironworks.cinderling.pop": return new NativeRecording("blast.mine", .55f, 1.15f);
                default: return default;
            }
        }

        public static bool Released(bool pending, float release, float until, float now) =>
            !pending && release <= until && now >= release && now - release < .25f;
    }

    // Hits are published in authority ID order. Keep one high-water mark per pilot in an arena.
    public sealed class NativeWeaponAudioHistory
    {
        private readonly Dictionary<int,int> lastHit = new Dictionary<int,int>();
        private readonly Dictionary<int,float> lastRelease = new Dictionary<int,float>();

        public bool Hit(int seat, int id, float at, float now)
        {
            if(!(now >= at && now-at < .25f) || lastHit.TryGetValue(seat, out int last) && last >= id)return false;
            lastHit[seat] = id;
            return true;
        }

        public bool Release(int seat, bool pending, float at, float until, float now)
        {
            if(!NativeRecordedCues.Released(pending,at,until,now) || lastRelease.TryGetValue(seat,out float last) && last >= at)return false;
            lastRelease[seat] = at;
            return true;
        }

        public void Clear(){lastHit.Clear();lastRelease.Clear();}
    }
}
