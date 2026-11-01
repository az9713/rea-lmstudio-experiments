"""Exact public-source allowlist plus project-bound portable binary packaging."""
import pathlib,json,zipfile,hashlib,re
from release_privacy import scan
root=pathlib.Path(__file__).resolve().parent.parent
assert root.name=='lmstudio-experiments'
public=json.loads((root/'public-files.json').read_text())['files']
if len(public)!=len(set(public)):raise RuntimeError('Duplicate public path')
for relative in public:
 p=root/relative
 if not p.is_file() or p.is_symlink() or p.is_junction() or not p.resolve().is_relative_to(root) or p.name.endswith('.private.json'):raise RuntimeError('Invalid public input: '+relative)
 if p.suffix in {'.cs','.csproj','.json','.md','.html','.ps1','.py','.cjs','.txt'}:
  if re.search(rb'[a-z]:[/\\]+users[/\\]+',p.read_bytes().lower()):raise RuntimeError('Local profile path in public source: '+relative)
out=root/'releases';out.mkdir(exist_ok=True);folder=out/'LM-Experiment-Workbench-win-x64'
if not (folder/'experiment-workbench.exe').is_file() or not (folder/'experiment-cli.exe').is_file():raise RuntimeError('Publish both executables first')
privacy=scan(folder,root)
if privacy['matches']:raise RuntimeError('Private build-path markers remain: '+str(privacy['matches']))
(root/'.tools').mkdir(exist_ok=True)
(root/'.tools/pe-privacy-verification.json').write_text(json.dumps(privacy,indent=2),encoding='utf-8')
assets=[];target=out/'LM-Experiment-Workbench-win-x64.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for p in sorted(folder.rglob('*')):
  if p.is_symlink() or p.is_junction() or not p.resolve().is_relative_to(folder):raise RuntimeError('Invalid published input')
  if p.is_file() and p.suffix!='.pdb':z.write(p,str(pathlib.Path(folder.name)/p.relative_to(folder)))
assets.append(target);target=out/'LM-Experiment-Workbench-source.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for relative in public:z.write(root/relative,relative)
assets.append(target)
manifest=[{'file':p.name,'bytes':p.stat().st_size,'sha256':hashlib.file_digest(p.open('rb'),'sha256').hexdigest()} for p in assets]
(out/'release-assets.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(out/'SHA256SUMS.txt').write_text(''.join(f"{p['sha256']}  {p['file']}\n" for p in manifest),encoding='ascii')
print(json.dumps(manifest,indent=2))
