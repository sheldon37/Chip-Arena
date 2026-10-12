using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

internal static class BowInputChecks
{
    static void Check(bool ok,string why){if(!ok)throw new Exception("Bow input: "+why);}
    public static void Run(string root,ConversionContent data)
    {
        data.Validate();
        foreach(var weapon in data.weapons)
        {
            Check(data.weights.Count(x=>x!=null&&x.name==weapon.id)==1,"exactly one weight: "+weapon.id);
            var stats=NativeContentRules.Stats(data,weapon.id,new Dictionary<string,int>());
            var roll=NativeContentRules.Roll(data,weapon.id,stats);
            Check(roll.duration>0&&roll.distance>0&&roll.cost>0&&!double.IsNaN(roll.cooldown),"usable roll: "+weapon.id);
        }
        Check(data.Weight("bow")==4&&data.Weight("cyberlord")==3,"intentional new weights");
        var weights=data.weights;
        try
        {
            data.weights=weights.Where(x=>x.name!="bow").ToArray();
            Check(NativeContentRules.Roll(data,"bow",NativeContentRules.Stats(data,"bow",new Dictionary<string,int>())).weight==4,"missing entry safely rolls");
            bool rejected=false;try{data.Validate();}catch(InvalidOperationException e){rejected=e.Message.Contains("weapon weight: bow");}
            Check(rejected,"validation rejects missing shipped weight");
            data.weights=null;Check(data.Weight("bow")==4,"missing table fallback");
            data.weights=new NumberValue[]{null};Check(data.Weight("bow")==4,"null entry fallback");
        }
        finally{data.weights=weights;}
        Console.WriteLine("PASS all content weapons have weights and roll; missing weight fallback and validation guard");

        // Exercise the production keyboard gate, including edge semantics and pending-frame latching.
        var pending=new InputFrame();
        pending.AbilityQ|=NativeRangedRules.KeyboardQ(data,"bow",false);
        Check(!pending.AbilityQ,"idle key cannot cast");
        pending.AbilityQ|=NativeRangedRules.KeyboardQ(data,"bow",true);
        Check(pending.AbilityQ&&data.Ability("bow","q").id=="volley","Q key-down routes Volley");
        pending.AbilityQ|=NativeRangedRules.KeyboardQ(data,"bow",false);
        Check(pending.AbilityQ&&!pending.HeldOnly().AbilityQ,"queued edge survives render updates but is not repeated as held input");
        foreach(string spec in new[]{"pistol","machinegun","fire","sword","cyberlord"})
            Check(NativeRangedRules.KeyboardQ(data,spec,true)&&!NativeRangedRules.KeyboardQ(data,spec,false),"existing Q edge: "+spec);
        Check(!NativeRangedRules.KeyboardQ(data,"rifle",true)&&NativeRangedRules.Scope("rifle")&&!NativeRangedRules.Scope("bow"),"rifle Q remains ADS; Bow Q is not ADS");
        Check(data.Ability("cyberlord","q").id=="data-dash"&&NativeRangedRules.KeyboardQ(data,"cyberlord",true),"Cyber Lord Q routes Data Dash");
        Check(!NativeRangedRules.Q("volley"),"Volley remains on its own dispatch and presentation path");
        var source=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/ConversionGame.cs"));
        Check(source.Contains("pending.AbilityQ|=NativeRangedRules.KeyboardQ(content,p.Seat.spec,Input.GetKeyDown(KeyCode.Q));"),"real keyboard adapter uses tested gate without an extra filter");
        Console.WriteLine("PASS production keyboard Q gate: Bow Volley edge/latching, legacy abilities and rifle-only ADS");
    }
}
