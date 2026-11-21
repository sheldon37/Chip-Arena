using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    public sealed class NativeCrowdGrid
    {
        private readonly Dictionary<long,int> heads=new Dictionary<long,int>(256);private int[] next=new int[256];
        public void Clear()=>heads.Clear();
        public static int Cell(float v)=>(int)Math.Floor(v/2);
        private static long Key(int x,int z)=>((long)x<<32)|(uint)z;
        public void Add(int index,float x,float z)
        {if(index>=next.Length)Array.Resize(ref next,Math.Max(index+1,next.Length*2));long key=Key(Cell(x),Cell(z));next[index]=heads.TryGetValue(key,out int head)?head:-1;heads[key]=index;}
        public int Head(int x,int z)=>heads.TryGetValue(Key(x,z),out int head)?head:-1;
        public int Next(int index)=>next[index];
    }
}
