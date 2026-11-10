using System;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// Substation 128's part of the stage art's layout export (Tests/StageArtLayout.cs, docs/arenas/STAGE-ART.md): the club's kit
// (Core NativeClubKit: every part with its family, the block it dresses, its kind, colour and glow gain), the places its kit,
// rig, show and backdrop share (Core NativeClubLooks, NativeClubScenery), the moving heads, and each crowd figure's place,
// facing and look (Core NativeClubRig.CrowdAt, NativeSubstationArt), so tools/stages/club_art.py models on today's pieces.
internal static class ClubArtLayout
{
    public static void Write(Utf8JsonWriter w,ContentMap map,MapLook look,Action<string,double,double,double> V,Action<string,double,double,double,double> Q)
    {
        var looks=NativeClubLooks.Resolve(look?.kitLooks);var kit=NativeClubKit.Build(map,look);
        w.WriteStartArray("kinds");foreach(var k in NativeClubLooks.Kinds)w.WriteStringValue(k);w.WriteEndArray();
        w.WriteStartArray("kindColors");for(int k=0;k<NativeClubLooks.Kinds.Length;k++)w.WriteNumberValue(looks.Color[k]);w.WriteEndArray();
        w.WriteStartArray("parts");
        for(int i=0;i<kit.Parts.Count;i++)
        {
            ref var p=ref kit.Parts.Items[i];w.WriteStartObject();w.WriteString("f",kit.Family[i]);if(kit.Owner[i]!=null)w.WriteString("o",kit.Owner[i]);w.WriteNumber("r",kit.Overhead[i]?1:0);w.WriteNumber("k",kit.Kind[i]);
            V("c",p.X,p.Y,p.Z);V("h",p.Hx,p.Hy,p.Hz);Q("q",p.Qx,p.Qy,p.Qz,p.Qw);w.WriteBoolean("s",p.Shadow);w.WriteNumber("col",p.Color);w.WriteNumber("w",Math.Round(p.Window,4));w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartObject("club");
        void N(string n,double v)=>w.WriteNumber(n,Math.Round(v,5));
        N("floorX0",NativeClubLooks.FloorX0);N("floorX1",NativeClubLooks.FloorX1);N("floorZ0",NativeClubLooks.FloorZ0);N("floorZ1",NativeClubLooks.FloorZ1);N("tilesX",NativeClubLooks.TilesX);N("tilesZ",NativeClubLooks.TilesZ);
        N("trussY",NativeClubLooks.TrussY);N("trussX",NativeClubLooks.TrussX);N("trussZ",NativeClubLooks.TrussZ);N("trussDepth",NativeClubLooks.TrussDepth);
        N("cubeY",NativeClubLooks.CubeY);N("cubeHalf",NativeClubLooks.CubeHalf);N("cubeHalfY",NativeClubLooks.CubeHalfY);N("cubeW",NativeClubShow.CubeW);N("cubeH",NativeClubShow.CubeH);
        N("ballX",NativeClubLooks.BallX);N("ballY",NativeClubLooks.BallY);N("ballZ",NativeClubLooks.BallZ);N("ballRadius",NativeClubLooks.BallRadius);
        N("wallZ",NativeClubLooks.WallZ);N("wallX",NativeClubLooks.WallX);N("wallY0",NativeClubLooks.WallY0);N("wallY1",NativeClubLooks.WallY1);N("wallW",NativeClubShow.WallW);N("wallH",NativeClubShow.WallH);
        N("stageY",NativeClubLooks.StageY);N("speakerX0",NativeClubLooks.SpeakerX0);N("speakerX1",NativeClubLooks.SpeakerX1);N("speakerZ",NativeClubLooks.SpeakerZ);N("speakerTop",NativeClubLooks.SpeakerTop);
        N("galleryY",NativeClubLooks.GalleryY);N("djX",NativeClubLooks.DjX);N("djZ",NativeClubLooks.DjZ);N("laserRailZ",NativeClubLooks.LaserRailZ);N("laserTop",NativeClubLooks.LaserTop);
        N("roofY",NativeClubScenery.RoofY);N("pitFloor",NativeClubScenery.PitFloor);
        N("hip",NativeSubstationArt.Hip);N("shoulder",NativeSubstationArt.Shoulder);N("shoulderOut",NativeSubstationArt.ShoulderOut);N("djUpperArm",NativeSubstationArt.DjUpperArm);N("variants",NativeSubstationArt.Variants);
        w.WriteEndObject();
        var show=new NativeClubShow();show.Prepare(map);
        w.WriteStartArray("heads");for(int k=0;k<NativeClubLooks.Heads;k++){w.WriteStartObject();V("at",show.HeadAt[k*3],show.HeadAt[k*3+1],show.HeadAt[k*3+2]);w.WriteBoolean("top",k>=12);w.WriteEndObject();}w.WriteEndArray();
        w.WriteStartArray("crowd");
        for(int k=0;k<NativeClubRig.CrowdMax;k++){NativeClubRig.CrowdAt(k,out double x,out double y,out double z,out double yaw);w.WriteStartArray();foreach(double v in new[]{x,y,z,NativeSubstationArt.Facing(k,yaw),NativeSubstationArt.Variant(k)})w.WriteNumberValue(Math.Round(v,5));w.WriteEndArray();}
        w.WriteEndArray();
    }
}
