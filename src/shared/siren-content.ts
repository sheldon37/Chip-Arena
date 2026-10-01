/** Gameplay tuning; animation phase lengths live exclusively in siren-contract.ts. */
export const SIREN_COMBAT={
 swipe:{damage:9,range:2.3,arc:2.4},rend:{damage:22,range:3,arc:2.8,cooldown:5},
 slam:{damage:16,radius:3.6,launch:9,cooldown:7},
 rush:{damage:14,distance:6,radius:1.1,cooldown:6,trailSeconds:3},
 laugh:{range:7,catchRadius:1.05,cooldown:30},
 hellfire:{damage:10,speed:20,radius:.18,patchRadius:1.1,patchSeconds:10,patchCap:12},
 damnation:{damage:30,speed:13,gravity:8,radius:.42,blast:3.5,cooldown:6},
 eruption:{damage:28,radius:2.2,launch:10,cooldown:5},
 rain:{damage:9,radius:7,cooldown:18,dropEvery:.22,fallSeconds:.8,height:8},
 mark:{damage:85,speed:3,radius:.32,cooldown:35,range:40},
 aimRange:28,fireDps:5,trailCap:24,fieldTick:.25,
} as const;
