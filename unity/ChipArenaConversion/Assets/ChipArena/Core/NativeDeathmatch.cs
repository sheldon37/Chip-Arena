using System;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeMatch
    {
        public int DeathmatchTarget;
        public bool ComebackChips;
        public Action<NativeSeat> StartingChip;

        public void RecordDeathmatchDeath(NativeSeat victim, NativeSeat killer, int seed)
        {
            if (DeathmatchTarget <= 0 || Phase != NativePhase.Fight || victim.respawnAt >= 0) return;
            victim.respawnAt = Time + 3;
            if (killer == null || SameSide(victim, killer)) return;
            victim.enemyDeaths++;
            foreach (var player in Seats)
                if (SameSide(player, killer)) player.score++;

            if (killer.score >= DeathmatchTarget)
            {
                Winner = SetWinner = killer.id;
                string name = Teams ? TeamNames[killer.team] + " team" : killer.name;
                Result = name + " wins · " + killer.score + " kills";
                ResultAt = Time;
                HasFinalKillcam = true;
                FinalVictim = victim.id;
                FinalSeed = seed;
                Phase = NativePhase.Result;
                PhaseEnd = Time + 5 + FinalKillcamHold + NativeFinalKillcam.StartDelay;
                return;
            }
            if (ComebackChips && (victim.enemyDeaths == 3 || victim.enemyDeaths == 6))
            {
                victim.owedPicks++;
                PrepareComebackOffer(victim);
            }
        }

        public bool TryDeathmatchRespawn(NativeSeat seat)
        {
            if (DeathmatchTarget<=0 || Phase!=NativePhase.Fight || !seat.connected || seat.alive ||
                seat.respawnAt<0 || Time<seat.respawnAt) return false;
            seat.respawnAt=-1;
            return true;
        }

        private void PrepareComebackOffer(NativeSeat player)
        {
            if (player.owedPicks <= 0 || player.offers.Count > 0) return;
            player.offers = NativeContentRules.Offers(data, player.spec, player.chips, random.Next,
                Choices, DraftFilter, abilityLoadout: player.weaponTechniques ?? "");
            if (player.offers.Count == 0) player.owedPicks = 0;
        }

        public bool TakeComebackChip(NativeSeat player, int ticket, string chip)
        {
            if (Phase != NativePhase.Fight || DeathmatchTarget <= 0 || player == null ||
                !player.connected || player.owedPicks <= 0 || player.pickTicket != ticket ||
                !player.offers.Contains(chip)) return false;
            player.chips.TryGetValue(chip, out int stacks);
            player.chips[chip] = stacks + 1;
            player.pickTicket++;
            player.owedPicks--;
            player.offers.Clear();
            PrepareComebackOffer(player);
            return true;
        }
    }
}
