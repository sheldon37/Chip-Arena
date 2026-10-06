/** In-place HUD update (presentation only). The 10 Hz HUD redraw used to replace the whole HUD's DOM whenever any number
 * changed, so a button under the cursor could be swapped between mousedown and mouseup and the click was lost (Scores,
 * Builds and Menu while a cooldown ticks). This keeps every element whose tag, class and data-* attributes are unchanged,
 * updating only its attributes and text; anything else (e.g. a new intro card with another data-intro) is rebuilt as before,
 * so its CSS animations still start fresh. */
const template=typeof document!=='undefined'?document.createElement('template'):undefined;
function sameElement(a:Element,b:Element){
 if(a.tagName!==b.tagName||a.getAttribute('class')!==b.getAttribute('class'))return false;
 for(const attr of Array.from(a.attributes).concat(Array.from(b.attributes)))if(attr.name.startsWith('data-')&&a.getAttribute(attr.name)!==b.getAttribute(attr.name))return false;
 return true;
}
function syncAttributes(a:Element,b:Element){
 for(const attr of Array.from(a.attributes))if(!b.hasAttribute(attr.name))a.removeAttribute(attr.name);
 for(const attr of Array.from(b.attributes))if(a.getAttribute(attr.name)!==attr.value)a.setAttribute(attr.name,attr.value);
}
function morphChildren(from:Node,to:Node){
 const next=Array.from(to.childNodes);
 next.forEach((node,i)=>{
  const current=from.childNodes[i];
  if(!current){from.appendChild(node.cloneNode(true));return;}
  if(current.nodeType===Node.TEXT_NODE&&node.nodeType===Node.TEXT_NODE){if(current.nodeValue!==node.nodeValue)current.nodeValue=node.nodeValue;return;}
  if(current instanceof Element&&node instanceof Element&&sameElement(current,node)){syncAttributes(current,node);morphChildren(current,node);return;}
  from.replaceChild(node.cloneNode(true),current);
 });
 while(from.childNodes.length>next.length)from.lastChild!.remove();
}
/** Morph `root`'s children into `html`. Returns false (the caller writes innerHTML) when there is nothing to keep. */
export function morphHud(root:Element,html:string){
 if(!template||!html||!root.firstChild)return false;
 template.innerHTML=html;morphChildren(root,template.content);template.innerHTML='';return true;
}
