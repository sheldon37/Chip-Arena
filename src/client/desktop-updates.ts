declare global {interface Window {arenaDesktop?:{openUpdater:()=>Promise<boolean>}}}
export function hasDesktopUpdater(){return typeof window.arenaDesktop?.openUpdater==='function';}
export async function openDesktopUpdater(){return window.arenaDesktop?.openUpdater();}
