using System;
namespace ChipArena.UnityConversion
{
    // Exact existing card artwork, quantized once at build time instead of rasterized on a player's first roll.
    public static class NativeDeployCardCache
    {
        public const string Folder="DeployCards/";
        public static string Key(string id,bool mutator,string theme=null)
        {
            string value=id==NativeDeployArt.Back?"back":(mutator?"m-":"i-")+id+(mutator?"":"-"+theme);
            foreach(char c in value)if(!(char.IsLetterOrDigit(c)||c=='-'||c=='_'))return null;
            return Folder+value;
        }
        public static byte[] Pack(float[] pixels)
        {var bytes=new byte[pixels.Length];for(int i=0;i<bytes.Length;i++)bytes[i]=(byte)Math.Round(Math.Max(0,Math.Min(1,pixels[i]))*255,MidpointRounding.ToEven);return bytes;}
        public static float[] Unpack(byte[] bytes)
        {if(bytes==null||bytes.Length!=NativeDeployArt.TilePixels*NativeDeployArt.TilePixels*4)throw new FormatException("Invalid deployment card pixels");var pixels=new float[bytes.Length];for(int i=0;i<bytes.Length;i++)pixels[i]=bytes[i]/255f;return pixels;}
    }
}
