/** Shared server-time timeline; the bass envelope is sampled from the supplied music. */
export const BASS_VAN={arrive:.7,grab:1.08,toss:1.25,land:1.85,jumpStart:1.65,driverSeated:2.05,doorClose:2.08,musicStart:2.35,sourceAudioStart:2,drive:4.1,end:5} as const;
const bass=[0.017,0.019,0.013,0.008,0.008,0.006,0.054,0.090,0.110,0.068,0.048,0.038,0.016,0.032,0.045,0.024,0.018,0.047,0.027,0.027,0.029,0.038,0.018,0.034,0.076,0.045,0.030,0.088,0.090,0.092,0.048,0.035,0.015,0.017,0.067,0.048,0.030,0.029,0.024,0.013,0.017,0.015,0.008,0.016,0.008,0.897,1.000,0.990,0.611,0.506,0.550,0.728,0.601,0.779,0.660,0.689,0.515,0.554,0.490,1.000,1.000,0.808,0.310,0.209,0.188,0.159,0.125,0.114,0.100,0.057,0.049,0.042,0.397,1.000,0.938,0.563,0.331,0.185,0.083,0.020];
export function vanBass(age:number){
 const offset=age-BASS_VAN.musicStart;if(offset<0||age>=BASS_VAN.end)return 0;
 const frame=offset*30,i=Math.floor(frame),a=bass[i]??0,b=bass[i+1]??0;
 return Math.sqrt(a+(b-a)*(frame-i));
}
