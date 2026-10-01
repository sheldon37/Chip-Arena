/**
 * Boss music timing. Beat times were detected from the track itself: the tempo drifts (~143.5 BPM until ~1:55, then
 * ~145.6), so never derive beats from a BPM. Every lookup here is a pure function of song time, so the simulation
 * (hazards), the client (stage animation, music sync) and late joiners all agree.
 *
 * Song time = sim.time - Snapshot.bossSong.startedAt. Past loop.end the song wraps to loop.start on a bar boundary
 * (the tail fades out), so use loopedSongTime() before any lookup that drives gameplay or visuals.
 */
export type BossSectionId='build'|'groove'|'lift'|'breather'|'climax'|'outro';
export interface BossTrack {
 id:string;file:string;duration:number;
 /** Seconds from the start of the file, ascending. */
 beats:readonly number[];
 /** Index into beats of the first downbeat (bar 1, beat 1). Bars are 4 beats. */
 firstDownbeat:number;
 sections:readonly {id:BossSectionId;fromBar:number;toBar:number}[];
 /** The Siren intro lasts from song t=0 to the downbeat of bar 5. */
 introEnd:number;
 loop:{start:number;end:number;fromBar:number;toBar:number};
}

export const BLACK_SIREN_PROTOCOL:BossTrack={
 id:'black-siren-protocol',file:'music/black-siren-protocol.mp3',duration:193.632,
 beats:[
 .1858,.6037,1.0159,1.428,1.8402,2.2523,2.6645,3.0883,3.5004,3.9184,4.3421,4.7543,5.178,5.596,6.0082,6.4261,
 6.8441,7.2678,7.68,8.1038,8.5217,8.9397,9.3576,9.7756,10.1936,10.6173,11.0353,11.4532,11.8712,12.295,12.7129,13.1309,
 13.5546,13.9726,14.3848,14.8085,15.2265,15.6444,16.0624,16.4862,16.9041,17.3221,17.74,18.158,18.5818,18.9997,19.4177,19.8356,
 20.2536,20.6774,21.0895,21.5133,21.9312,22.355,22.7672,23.1909,23.6089,24.0268,24.4448,24.8686,25.2865,25.7045,26.1224,26.552,
 26.9642,27.3879,27.8001,28.2239,28.6418,29.0598,29.4777,29.8957,30.3137,30.7374,31.1554,31.5733,31.9913,32.4151,32.833,33.251,
 33.6689,34.0869,34.5107,34.9286,35.3466,35.7645,36.1825,36.6063,37.0242,37.4422,37.8601,38.2781,38.7019,39.1198,39.5378,39.9557,
 40.3737,40.7917,41.2096,41.6334,42.0513,42.4693,42.8931,43.311,43.729,44.1469,44.5649,44.9887,45.4066,45.8246,46.2425,46.6605,
 47.0843,47.5022,47.9202,48.3381,48.7561,49.1799,49.592,50.0158,50.4337,50.8517,51.2697,51.6876,52.1114,52.5293,52.9473,53.3653,
 53.789,54.2012,54.6249,55.0429,55.4609,55.8788,56.2968,56.7205,57.1385,57.5565,57.9744,58.3982,58.8103,59.2283,59.6463,60.07,
 60.4822,60.9059,61.3239,61.7419,62.1598,62.5778,63.0015,63.4079,63.8259,64.2438,64.6618,65.0797,65.5035,65.9215,66.3394,66.7574,
 67.1811,67.5991,68.0171,68.435,68.853,69.2709,69.6889,70.1127,70.5306,70.9486,71.3665,71.7787,72.2024,72.6146,73.0384,73.4563,
 73.8743,74.2922,74.7102,75.1282,75.5519,75.9641,76.382,76.8,77.218,77.6359,78.0597,78.4718,78.8898,79.3078,79.7257,80.1495,
 80.5674,80.9796,81.3976,81.8155,82.2335,82.6572,83.0694,83.4932,83.9111,84.3233,84.7412,85.1592,85.5771,85.9951,86.4131,86.831,
 87.249,87.6669,88.0849,88.4971,88.9208,89.333,89.7509,90.1689,90.5868,90.999,91.417,91.8349,92.2529,92.6708,93.083,93.501,
 93.9189,94.3369,94.7548,95.1728,95.5907,96.0087,96.4209,96.8446,97.2568,97.6747,98.0927,98.5107,98.9228,99.3408,99.7587,100.1651,
 100.5888,101.0068,101.4248,101.8369,102.2549,102.6728,103.0908,103.5029,103.9267,104.3389,104.7568,105.169,105.5869,105.9991,106.4171,106.8292,
 107.2472,107.6651,108.0773,108.4952,108.919,109.3195,109.7375,110.1555,110.5676,110.9798,111.3919,111.8099,112.222,112.64,113.0522,113.4701,
 113.8765,114.2944,114.7066,115.1245,115.5367,115.9546,116.3668,116.779,117.1911,117.6091,118.0154,118.4334,118.8397,119.2577,119.6698,120.082,
 120.4941,120.9179,121.3243,121.7306,122.1486,122.5607,122.9729,123.385,123.7972,124.2093,124.6215,125.0337,125.4458,125.858,126.2759,126.6823,
 127.0944,127.5124,127.9245,128.3367,128.7488,129.161,129.5732,129.9853,130.3975,130.8096,131.2218,131.6339,132.0461,132.4582,132.8704,133.2883,
 133.6947,134.1127,134.519,134.937,135.3491,135.7613,136.1734,136.5856,136.9977,137.4099,137.822,138.2342,138.6463,139.0585,139.4707,139.8828,
 140.295,140.7071,141.1193,141.5314,141.9436,142.3557,142.7679,143.18,143.5922,144.0044,144.4165,144.8287,145.2408,145.653,146.0651,146.4773,
 146.8894,147.3016,147.7137,148.1259,148.538,148.9502,149.3624,149.7687,150.1809,150.593,151.0052,151.4173,151.8237,152.2416,152.648,153.0601,
 153.4723,153.8844,154.2966,154.7088,155.1209,155.5273,155.9394,156.3516,156.7637,157.1701,157.5822,157.9944,158.4065,158.8187,159.2308,159.643,
 160.0493,160.4615,160.8678,161.28,161.6863,162.0985,162.5107,162.9228,163.335,163.7413,164.1535,164.5656,164.972,165.3841,165.7963,166.2026,
 166.6148,167.0211,167.4333,167.8454,168.2518,168.6639,169.0703,169.4824,169.8946,170.301,170.7131,171.1195,171.5258,171.9612,172.4024,172.7623,
 173.1686,173.5866,173.9987,174.4051,174.8172,175.2294,175.6415,176.0479,176.4542,176.8722,177.2844,177.6849,178.0971,178.5092,178.9214,179.3335,
 179.7399,180.1404,180.5351,180.9415,181.3769,181.7948,182.2186,182.6598,183.0951,183.5015,183.9311,184.3664,184.8134,185.2488,185.7074,186.1544,
 186.5781,187.0193,187.4663,187.9133,188.3603,188.8073,189.2542,189.707,190.154,190.572,190.9899,191.4079,191.8084,192.209,192.6385,193.0565,
 ],
 firstDownbeat:1,
 sections:[{id:'build',fromBar:1,toBar:16},{id:'groove',fromBar:17,toBar:44},{id:'lift',fromBar:45,toBar:60},{id:'breather',fromBar:61,toBar:80},{id:'climax',fromBar:81,toBar:104},{id:'outro',fromBar:105,toBar:115}],
 introEnd:7.2678,
 loop:{start:74.2922,end:173.5866,fromBar:45,toBar:104},
};
export const BOSS_TRACKS={'black-siren-protocol':BLACK_SIREN_PROTOCOL} as const;
export type BossTrackId=keyof typeof BOSS_TRACKS;

/** Song time with the loop applied: unchanged before loop.end, then wraps into [loop.start, loop.end). */
export function loopedSongTime(track:BossTrack,songTime:number){
 const {start,end}=track.loop;if(songTime<end)return songTime;const span=end-start;return start+((songTime-start)%span+span)%span;
}
/** Index of the last beat at or before t (-1 before the first beat). t is looped song time. */
export function beatIndexAt(track:BossTrack,t:number){
 const b=track.beats;let lo=0,hi=b.length-1,found=-1;while(lo<=hi){const mid=(lo+hi)>>1;if(b[mid]<=t){found=mid;lo=mid+1;}else hi=mid-1;}return found;
}
/** Bar (1-based), beat within the bar (1-4) and progress through the current beat (0-1). Before bar 1: bar 0. */
export function barAt(track:BossTrack,t:number){
 const i=beatIndexAt(track,t),b=track.beats;if(i<track.firstDownbeat)return {bar:0,beat:Math.max(1,i+4-track.firstDownbeat+1),phase:0,index:i};
 const rel=i-track.firstDownbeat,next=b[i+1]??b[i]+(b[i]-b[i-1]),phase=Math.min(1,Math.max(0,(t-b[i])/(next-b[i])));
 return {bar:Math.floor(rel/4)+1,beat:rel%4+1,phase,index:i};
}
/** Start time of a bar (1-based). */
export function barStart(track:BossTrack,bar:number){
 const i=track.firstDownbeat+(bar-1)*4;return i<track.beats.length?track.beats[Math.max(0,i)]:track.duration;
}
export function sectionAt(track:BossTrack,t:number):BossSectionId{
 const bar=barAt(track,t).bar;for(const s of track.sections)if(bar>=s.fromBar&&bar<=s.toBar)return s.id;return bar<1?'build':'outro';
}
/** Beat indices crossed in (prev, now] of raw song time, loop-aware. Use for one-shot beat events. */
export function crossedBeats(track:BossTrack,prev:number,now:number):number[]{
 if(!(now>prev))return [];const out:number[]=[],a=loopedSongTime(track,prev),b=loopedSongTime(track,now);
 const span=(from:number,to:number)=>{for(let i=beatIndexAt(track,from)+1;i<track.beats.length&&track.beats[i]<=to;i++)out.push(i);};
 if(b>=a&&now-prev<track.loop.end-track.loop.start)span(a,b);else{span(a,track.loop.end-1e-9);span(track.loop.start-1e-9,b);}
 return out;
}
