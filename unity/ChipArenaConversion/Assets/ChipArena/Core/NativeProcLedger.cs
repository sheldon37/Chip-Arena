using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    public sealed class NativeProcLedger
    {
        private readonly HashSet<string> round=new HashSet<string>(),impacts=new HashSet<string>();private readonly Queue<string> order=new Queue<string>();
        public bool Add(string key){if(key.EndsWith(":exit")||key.EndsWith(":panic")||key.EndsWith(":round-two")||key.StartsWith("undo:"))return round.Add(key);if(!impacts.Add(key))return false;order.Enqueue(key);if(order.Count>4096)impacts.Remove(order.Dequeue());return true;}
        public void Clear(){round.Clear();impacts.Clear();order.Clear();}
    }
    public sealed class NativeShotScale {public float Value=1;}
    public static class NativeChipRanks
    {
        public static int RarityRank(string rarity){int rank=System.Array.IndexOf(new[]{"common","uncommon","rare","epic","legendary","mythic","mishima","ansatsuken","l44t","siren"},rarity);return rank<0?10:rank;}
        public static int BotRank(string rarity)=>System.Math.Min(5,RarityRank(rarity));
    }
}
