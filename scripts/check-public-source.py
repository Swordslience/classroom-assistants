"""Check candidate tracked files; report locations and rule names, never secret values."""
from pathlib import Path
import io,json,re,subprocess,sys,zipfile

ROOT=Path(__file__).resolve().parents[1]
RULES={
 'private-key':re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|<RSAKeyValue>.*?<D>',re.S),
 'provider-key':re.compile(r'(?<![A-Za-z0-9])sk-[A-Za-z0-9_-]{28,}|AKID[A-Za-z0-9]{24,}|gh[pousr]_[A-Za-z0-9]{25,}|github_pat_[A-Za-z0-9_]{30,}'),
 'personal-endpoint':re.compile(r'classroom-ai-remote-334580|cloud1-d1gbkeavs8f824f83|g2510-classroom\.pages\.dev'),
 'windows-personal-path':re.compile(r'[CF]:[\\/](?:Users[\\/]|希沃科研[\\/])'),
 'nonempty-example-key':re.compile(r'"(?:DeepSeekApiKey|DoubaoApiKey|VoiceApiKey|ApiKey|DeviceSecret|CLOUDBASE_APIKEY)"\s*:\s*"[^"\s]+"'),
}
BLOCKED={'connection.json','remote.local.json','classroom-deployment.json','cloudbaserc.json','接口配置_仅自用.json','.env'}
TEXT={'.cs','.csproj','.ps1','.cmd','.py','.json','.mjs','.js','.md','.html','.css','.example','.yml','.yaml','.props','.targets','.xml'}

def candidates():
 if (ROOT/'.git').is_dir():
  # Include not-yet-added files, but honor .gitignore. Never scan runtime user files.
  result=subprocess.run(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=ROOT,check=True,stdout=subprocess.PIPE)
  return sorted(set(Path(p.decode('utf-8')) for p in result.stdout.split(b'\0') if p))
 return [p.relative_to(ROOT) for p in ROOT.rglob('*') if p.is_file() and not any(x in {'bin','obj','node_modules','installer','artifacts','.git'} for x in p.relative_to(ROOT).parts)]

findings=[];checked=0
for rel in candidates():
 path=ROOT/rel;checked+=1
 if rel.name in BLOCKED or rel.suffix.lower() in {'.protected','.pfx','.p12','.pem','.key','.dmp'}:
  findings.append({'path':rel.as_posix(),'rule':'private-config-or-key-file'});continue
 if path.suffix.lower()=='.zip':
  # The only intended binary archive is the public OCR runtime.
  if rel.as_posix()!='PhotoTutor/LocalOcr/runtime.zip':findings.append({'path':rel.as_posix(),'rule':'unexpected-archive'});continue
  data=path.read_bytes()
  import hashlib
  manifest=json.loads((ROOT/'PhotoTutor/LocalOcr/runtime-manifest.json').read_text(encoding='utf-8-sig'))
  if hashlib.sha256(data).hexdigest()!=manifest['zipSha256']:findings.append({'path':rel.as_posix(),'rule':'ocr-runtime-hash'})
  with zipfile.ZipFile(io.BytesIO(data)) as archive:
   expected={x['name']:x for x in manifest['files']}
   if set(archive.namelist())!=set(expected):findings.append({'path':rel.as_posix(),'rule':'ocr-runtime-entries'})
   for info in archive.infolist():
    payload=archive.read(info)
    if info.filename not in expected or hashlib.sha256(payload).hexdigest()!=expected[info.filename]['sha256']:findings.append({'path':str(rel)+'/'+info.filename,'rule':'ocr-runtime-member-hash'})
    if info.filename.endswith('.exe'):
     for enc in ('utf-8','utf-16-le'):
      s=payload.decode(enc,errors='ignore')
      if RULES['provider-key'].search(s) or RULES['personal-endpoint'].search(s):findings.append({'path':str(rel)+'/'+info.filename,'rule':'embedded-credential-or-endpoint'})
  continue
 if path.suffix.lower() not in TEXT and path.name not in {'LICENSE','.gitignore','.gitattributes','Dockerfile'}:continue
 text=path.read_text(encoding='utf-8-sig',errors='replace')
 for name,pattern in RULES.items():
  # Scanner rule definitions mention forbidden endpoint signatures by design.
  if rel.as_posix()=='scripts/check-public-source.py':continue
  match=pattern.search(text)
  if match:findings.append({'path':rel.as_posix(),'line':text[:match.start()].count('\n')+1,'rule':name})
print(json.dumps({'checkedFiles':checked,'findings':findings,'passed':not findings},ensure_ascii=False,indent=2))
sys.exit(bool(findings))
