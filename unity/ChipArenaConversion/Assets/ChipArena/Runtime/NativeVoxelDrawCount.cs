using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The DEV voxel draw counter's bank side (Core NativeVoxelDraws; docs/VOXEL-STYLE.md): Flush counts each draw list's instances by
    // the kind of the model id its part is registered under. The part-to-kind map is filled from the bank's ids when a part it has not
    // seen flushes (at most once a frame: new parts are a split's halves, a wound's carve, a twin made on first use), so a counted frame
    // pays one dictionary lookup per draw list, and a frame with counting off pays nothing. Presentation only.
    public sealed partial class NativeSourceModels
    {
        private readonly Dictionary<Part,NativeVoxelDraws.Kind> voxelKinds=new Dictionary<Part,NativeVoxelDraws.Kind>();private int voxelKindsFrame=-1;
        private void VoxelCount(Part part,int count)
        {
            if(part==null||count<=0)return;
            if(!voxelKinds.TryGetValue(part,out var kind))
            {
                if(voxelKindsFrame!=Time.frameCount){voxelKindsFrame=Time.frameCount;VoxelKindsFill();}
                if(!voxelKinds.TryGetValue(part,out kind)){kind=NativeVoxelDraws.Kind.None;voxelKinds[part]=kind;}
            }
            NativeVoxelDraws.Add(kind,count,Time.frameCount);
        }
        // Every registered part's kind; a part under several ids (an alias: a DEV class's weapon, a one-finish class's plain ids) takes
        // NativeVoxelDraws.Merge of theirs. Parts released since stay keyed until the map passes its cap, then it starts again.
        private void VoxelKindsFill()
        {
            if(voxelKinds.Count>20000)voxelKinds.Clear();
            var fresh=new Dictionary<Part,NativeVoxelDraws.Kind>();
            foreach(var pair in models)if(pair.Value!=null)foreach(var p in pair.Value)if(p!=null)
            {var k=NativeVoxelDraws.Of(pair.Key,p.Name);fresh[p]=fresh.TryGetValue(p,out var was)?NativeVoxelDraws.Merge(was,k):k;}
            if(pieces!=null)foreach(var p in pieces)if(p!=null)fresh[p]=NativeVoxelDraws.Kind.Piece;
            // the Siren's animated pieces (NativeSirenLookPresentation): hers
            foreach(var pair in lookPieces)if(pair.Value!=null)foreach(var piece in pair.Value)
            {
                var k=NativeVoxelDraws.Of(pair.Key);if(piece.Part!=null)fresh[piece.Part]=k;
                if(piece.Shapes!=null)foreach(var s in piece.Shapes)if(s!=null)fresh[s]=k;
            }
            foreach(var pair in fresh)voxelKinds[pair.Key]=pair.Value;
        }
    }
}
