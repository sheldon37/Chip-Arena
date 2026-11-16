using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public enum OpeningStep { Weapon, Chip, Ready, Reveal }

    public sealed class OpeningPilot
    {
        public readonly NativeSeat Seat;
        public string Weapon, Chip;
        public string[] Offers = Array.Empty<string>();
        public OpeningStep Step;
        public double Deadline;
        public OpeningPilot(NativeSeat seat) { Seat = seat; Weapon = seat.spec; }
    }

    // Only the host owns this object. Each private view is assembled for its authenticated seat.
    public sealed class NativeOpeningDraft
    {
        public readonly Dictionary<int,OpeningPilot> Pilots = new Dictionary<int,OpeningPilot>();
        public readonly string Session = Guid.NewGuid().ToString("N");
        private readonly bool startingChips;
        private readonly Func<OpeningPilot,string[]> offer;
        private readonly Func<NativeSeat,string,bool> allowed;
        public NativeOpeningDraft(IEnumerable<NativeSeat> seats, bool blind, bool chips, double now,
            Func<OpeningPilot,string[]> offer, Func<NativeSeat,string,bool> allowed)
        {
            this.offer = offer; this.allowed = allowed; startingChips = chips;
            foreach (var seat in seats)
            {
                var p = new OpeningPilot(seat){Deadline=now+20};
                Pilots.Add(seat.id,p);
                if (!allowed(seat,p.Weapon)) p.Weapon="pistol";
                if (!blind || seat.bot) LockWeapon(p,now);
                if (seat.bot && p.Step==OpeningStep.Chip) SelectChip(p,p.Offers[0]);
            }
        }

        private void LockWeapon(OpeningPilot p,double now)
        {
            if (!allowed(p.Seat,p.Weapon)) p.Weapon="pistol";
            p.Offers=startingChips?offer(p):Array.Empty<string>();
            p.Step=p.Offers.Length>0?OpeningStep.Chip:OpeningStep.Ready;
            p.Deadline=now+20;
        }

        private static void SelectChip(OpeningPilot p,string id)
        {
            p.Chip=id;
            p.Step=OpeningStep.Ready;
        }

        public bool Submit(int seat,OpeningStep step,string value,bool confirm,double now)
        {
            if (!Pilots.TryGetValue(seat,out var p) || p.Step!=step || now>=p.Deadline) return false;
            if (step==OpeningStep.Weapon && allowed(p.Seat,value))
            {
                p.Weapon=value;
                if (confirm) LockWeapon(p,now);
                return true;
            }
            if (step==OpeningStep.Chip && confirm && Array.IndexOf(p.Offers,value)>=0)
            {
                SelectChip(p,value);
                return true;
            }
            return false;
        }

        public void Tick(double now)
        {
            foreach(var p in Pilots.Values)
            {
                if(now<p.Deadline)continue;
                if(p.Step==OpeningStep.Weapon)LockWeapon(p,now);
                else if(p.Step==OpeningStep.Chip)SelectChip(p,p.Offers[0]);
            }
        }
        public bool Ready=>Pilots.Values.All(p=>p.Step==OpeningStep.Ready);
    }
}
