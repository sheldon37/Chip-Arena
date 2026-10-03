"""TEST-ONLY artifacts never carry the editable game source: no chip-arena-source.zip, no src/ tree, no source maps.
Usage: python source_privacy.py <zip>...  (exit 1 and list the offending entries if any ZIP has source in it)."""
import io,re,sys,zipfile
SOURCE_ARCHIVE='chip-arena-source.zip'
SOURCE_MAP=re.compile(r'\.(m|c)?js\.map$|\.css\.map$')
def private_name(name):
 """True for a path that would publish source: the source archive, anything under a src/ folder, or a source map."""
 parts=name.replace('\\','/').lower().split('/')
 return parts[-1]==SOURCE_ARCHIVE or 'src' in parts[:-1] or bool(SOURCE_MAP.search(parts[-1]))
def zip_problems(archive,depth=0):
 """Offending entry names in a ZIP and in ZIPs nested inside it (shown as outer!inner)."""
 own=not isinstance(archive,zipfile.ZipFile);z=zipfile.ZipFile(archive) if own else archive;bad=[]
 try:
  for info in z.infolist():
   if private_name(info.filename):bad.append(info.filename);continue
   if info.filename.lower().endswith('.zip') and depth<3:
    try:inner=zipfile.ZipFile(io.BytesIO(z.read(info)))
    except zipfile.BadZipFile:continue
    bad+=[info.filename+'!'+n for n in zip_problems(inner,depth+1)]
 finally:
  if own:z.close()
 return bad
if __name__=='__main__':
 failed=False
 for path in sys.argv[1:]:
  bad=zip_problems(path)
  if bad:failed=True;print(path+': source content: '+', '.join(bad[:20])+(' ...' if len(bad)>20 else ''))
  else:print(path+': no source archive, src/ tree or source map')
 sys.exit(1 if failed or not sys.argv[1:] else 0)
