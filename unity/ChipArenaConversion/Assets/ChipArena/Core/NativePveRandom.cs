using System;
namespace ChipArena.UnityConversion
{
    // Source pve/rng.ts: FNV-1a typed parts + murmur finalizer; independent mulberry32 streams.
    public sealed class NativePveRandom
    {
        private uint state;public NativePveRandom(uint seed){state=seed;}
        public double Next(){unchecked{state+=0x6D2B79F5;uint t=state;t=(t^(t>>15))*(t|1);t^=t+(t^(t>>7))*(t|61);return (t^(t>>14))/4294967296d;}}
        public static uint Hash(params object[] parts)
        {
            unchecked{uint h=0x811c9dc5;void Mix(uint b){h=(h^(b&255))*0x01000193;}
            foreach(var part in parts){if(part is string s){Mix(0x53);foreach(char c in s){Mix(c);Mix((uint)c>>8);}}else{uint n=Convert.ToUInt32(part);Mix(0x4e);Mix(n);Mix(n>>8);Mix(n>>16);Mix(n>>24);}Mix(0x7c);}
            h^=h>>16;h*=0x85ebca6b;h^=h>>13;h*=0xc2b2ae35;h^=h>>16;return h;}
        }
    }
}
