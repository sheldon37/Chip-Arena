import path from 'node:path';

/** Works with native Windows separators and rejects traversal outside the public root. */
export function staticPath(root:string,pathname:string,paths:typeof path=path):string|null {
 const base=paths.resolve(root),candidate=paths.resolve(base,'.'+(pathname==='/'?'/index.html':pathname));
 const relative=paths.relative(base,candidate);
 return relative && relative!=='..'&&!relative.startsWith('..'+paths.sep)&&!paths.isAbsolute(relative)?candidate:null;
}
