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
        public string[] WeaponOffers = Array.Empty<string>();
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
        public const int DealtProtocol = 2;
        public const int DefaultBlindChoices = 3;
        public string Refusal { get; private set; }
        private readonly bool startingChips;
        private readonly Func<double> random;
        public static int OfferCount(int count,int poolSize)=>count==0?0:Math.Min(poolSize,Math.Max(2,count));
        public static string ProtocolRefusal(int protocol,bool dealt)
        {
            if(dealt && protocol<DealtProtocol)return "All pilots need the dealt Blind Pick update to use these rules.";
            return protocol<1?"All pilots need the Deathmatch and Blind Pick update to use these rules.":null;
        }
        private readonly Func<OpeningPilot,string[]> offer;
        private readonly Func<NativeSeat,string,bool> allowed;
        public NativeOpeningDraft(IEnumerable<NativeSeat> seats, bool blind, bool chips, double now,
            Func<OpeningPilot,string[]> offer, Func<NativeSeat,string,bool> allowed,
            int blindChoices=0, string[] weapons=null, Func<double> random=null)
        {
            this.offer = offer; this.allowed = allowed; startingChips = chips;
            var roster=seats.ToArray();
            Dictionary<int,string[]> pools=null;
            if(blind && blindChoices!=0)
            {
                this.random=random??new SourceRandom(NativeMatch.NewSeed()).Next;
                var ids=(weapons??Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
                pools=roster.ToDictionary(s=>s.id,s=>ids.Where(id=>allowed(s,id)).ToArray());
                int smallest=pools.Count==0?0:pools.Values.Min(pool=>pool.Length);
                if(smallest<2){Refusal="Blind Pick needs at least two eligible weapons for each pilot.";return;}
                blindChoices=OfferCount(blindChoices,smallest);
            }
            foreach (var seat in roster)
            {
                var p = new OpeningPilot(seat){Deadline=now+20};
                Pilots.Add(seat.id,p);
                if (pools!=null)
                {
                    var pool=pools[seat.id];
                    int n=blindChoices;
                    for(int i=0;i<n;i++)
                    {
                        int j=i+(int)(this.random()*(pool.Length-i));
                        (pool[i],pool[j])=(pool[j],pool[i]);
                    }
                    p.WeaponOffers=pool.Take(n).ToArray();
                    p.Weapon=p.WeaponOffers.Contains(seat.spec)?seat.spec:p.WeaponOffers[0];
                }
                if (pools==null && !allowed(seat,p.Weapon)) p.Weapon="pistol";
                if (!blind || seat.bot) LockWeapon(p,now);
                if (seat.bot && p.Step==OpeningStep.Chip) SelectChip(p,p.Offers[0]);
            }
        }

        private void LockWeapon(OpeningPilot p,double now)
        {
            if (!allowed(p.Seat,p.Weapon))
            {
                if(p.WeaponOffers.Length>0){Refusal="Weapon access changed. Restart Blind Pick to deal new choices.";return;}
                p.Weapon="pistol";
            }
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
            if (step==OpeningStep.Weapon && (p.WeaponOffers.Length==0 || Array.IndexOf(p.WeaponOffers,value)>=0) && allowed(p.Seat,value))
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

        public static string RoomRefusal(IReadOnlyCollection<NativeSeat> seats,bool cooperative,bool teams,string map)
        {
            if(cooperative)
                return seats.Count>4?"Siren co-op supports up to four party members.":
                    seats.Count==0?"Wait for a pilot to join.":null;
            if(seats.Count<2)return "Add a bot or wait for a pilot to join.";
            if(seats.Select(s=>teams?s.team:s.id).Distinct().Count()<2)
                return "Both teams need a pilot: add a bot or change teams.";
            if(!NativeTrainingRules.RoomFits(map,seats,teams))
                return "Breakwater supports up to 4 pilots: FFA, 1v1 or 2v2.";
            return null;
        }

        public string[] CardsFor(int seat,bool reveal,Func<string,string> weaponName)
        {
            if(!Pilots.TryGetValue(seat,out var viewer))return Array.Empty<string>();
            if(reveal && Ready)
                return Pilots.Values.Select(p=>p.Seat.name+"   ·   "+weaponName(p.Weapon)).ToArray();
            if(viewer.Step==OpeningStep.Weapon)return viewer.WeaponOffers;
            if(viewer.Step==OpeningStep.Chip)return viewer.Offers;
            return Array.Empty<string>();
        }

        public bool Disconnect(int seat)=>Pilots.Remove(seat);

        public void Tick(double now)
        {
            foreach(var p in Pilots.Values)
            {
                if(p.WeaponOffers.Length>0 && p.Step!=OpeningStep.Weapon && !allowed(p.Seat,p.Weapon))
                {Refusal="Weapon access changed. Restart Blind Pick to deal new choices.";return;}
                if(now<p.Deadline)continue;
                if(p.Step==OpeningStep.Weapon && p.WeaponOffers.Length>0)
                {
                    var legal=p.WeaponOffers.Where(id=>allowed(p.Seat,id)).ToArray();
                    if(legal.Length==0){Refusal="Weapon access changed. Restart Blind Pick to deal new choices.";return;}
                    p.Weapon=legal[(int)(random()*legal.Length)];
                }
                if(p.Step==OpeningStep.Weapon)LockWeapon(p,now);
                else if(p.Step==OpeningStep.Chip)SelectChip(p,p.Offers[0]);
            }
        }
        public bool Ready=>Pilots.Values.All(p=>p.Step==OpeningStep.Ready);
    }
}
