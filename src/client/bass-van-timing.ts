/** Shared server-time timeline; the bass envelope is sampled from the supplied music. */
export const BASS_VAN={arrive:.7,grab:1.08,toss:1.25,land:1.85,jumpStart:1.65,driverSeated:2.05,doorClose:2.08,musicStart:2.35,sourceAudioStart:4,drive:4.1,end:5} as const;
const bass=[0.406,0.490,0.188,0.127,0.114,0.098,0.077,0.070,0.061,0.034,0.030,0.026,0.272,0.999,0.846,0.634,0.494,0.400,0.342,0.296,0.245,0.244,0.318,0.299,0.355,0.362,0.746,1.000,0.570,0.314,0.349,0.246,0.272,0.296,0.372,0.414,0.265,0.225,0.206,0.186,1.000,0.757,0.598,0.352,0.175,0.162,0.127,0.133,0.127,0.088,0.088,0.119,0.215,0.400,1.000,0.694,0.489,0.239,0.175,0.145,0.105,0.079,0.071,0.046,0.057,0.044,0.036,0.447,1.000,0.627,0.322,0.132,0.056,0.022,0.088,0.155,0.071,0.025,0.031,0.014];
export function vanBass(age:number){
 const offset=age-BASS_VAN.musicStart;if(offset<0||age>=BASS_VAN.end)return 0;
 const frame=offset*30,i=Math.floor(frame),a=bass[i]??0,b=bass[i+1]??0;
 return Math.sqrt(a+(b-a)*(frame-i));
}
