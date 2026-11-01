"""Scan only owned release PE files for workspace/profile build-path markers."""
import pathlib
def scan(directory,project):
 project=project.resolve();directory=directory.resolve()
 if not directory.is_relative_to(project):raise RuntimeError('Release scan escaped project')
 roots=[str(project)];parts=project.parts
 for i,part in enumerate(parts):
  if part.lower()=='users' and i+1<len(parts):roots.append(str(pathlib.Path(*parts[:i+2])));break
 values=set()
 for value in roots:
  for variant in [value,value.replace('\\','/'),value.replace('\\','\\\\')]:
   values.add(variant.lower().encode('utf-8'));values.add(variant.lower().encode('utf-16-le'))
 matches=[];count=0
 for p in sorted(directory.rglob('*')):
  if p.is_symlink() or p.is_junction() or not p.resolve().is_relative_to(directory):raise RuntimeError('Linked release input')
  if p.is_file() and p.suffix.lower() in {'.dll','.exe'}:
   data=p.read_bytes()
   if data[:2]!=b'MZ':continue
   count+=1;lower=data.lower()
   if any(marker in lower for marker in values):matches.append(str(p.relative_to(directory)))
 return {'peFilesScanned':count,'encodings':['ASCII/UTF8','UTF16LE'],'markerClasses':['project-root path','current profile path from workspace components'],'matches':matches,'scope':'Literal uncompressed path markers in actual release PE files; not a full embedded/compressed-data privacy audit'}
