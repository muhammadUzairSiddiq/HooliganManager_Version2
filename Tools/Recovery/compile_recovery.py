from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[2]
editor = Path(r'C:\Users\User\Desktop\D drive Data\Unity Game Engine\6000.0.80f1\Editor\Data')
out = root / 'Artifacts' / 'Recovery'
out.mkdir(parents=True, exist_ok=True)
source = root / 'Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp'
lines = source.read_text(encoding='utf-8-sig').splitlines()
lines = [line for line in lines if not line.startswith(('-out:', '-refout:'))]
lines += ['-out:"Artifacts/Recovery/Assembly-CSharp.dll"', '-refout:"Artifacts/Recovery/Assembly-CSharp.ref.dll"']
for file in (root/'Assets/Scripts').rglob('*.cs'):
    if any(part.lower()=='editor' for part in file.relative_to(root/'Assets/Scripts').parts): continue
    name=file.relative_to(root).as_posix()
    if not any(name in line.replace('\\', '/') for line in lines): lines.append('"'+name+'"')
rsp = out / 'runtime-compile.rsp'
rsp.write_text('\n'.join(lines), encoding='utf-8')
result = subprocess.run([str(editor / 'NetCoreRuntime/dotnet.exe'), str(editor / 'DotNetSdkRoslyn/csc.dll'), '@' + str(rsp)], cwd=root, capture_output=True, text=True)
log = result.stdout + result.stderr
(out / 'compile-runtime.txt').write_text(log, encoding='utf-8')
errors = [line for line in log.splitlines() if 'error ' in line]
warnings = [line for line in log.splitlines() if 'warning ' in line]
print(f'Runtime assembly compile exit={result.returncode}; errors={len(errors)}; warnings={len(warnings)}')
for line in errors:
    print(line)
if result.returncode:
    raise SystemExit(result.returncode)
editor_source=root/'Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp-Editor.rsp'
editor_lines=editor_source.read_text(encoding='utf-8-sig').splitlines()
editor_lines=[line for line in editor_lines if not line.startswith(('-out:','-refout:'))]
editor_lines=[('-r:"Artifacts/Recovery/Assembly-CSharp.ref.dll"' if re.search(r'Assembly-CSharp(?:\.ref)?\.dll"?$',line) and line.startswith('-r:') else line) for line in editor_lines]
for editor_file in (root/'Assets/Scripts/Editor').rglob('*.cs'):
    name=editor_file.relative_to(root).as_posix()
    if not any(name in line.replace('\\','/') for line in editor_lines):editor_lines.append('"'+name+'"')
editor_lines+=['-out:"Artifacts/Recovery/Assembly-CSharp-Editor.dll"','-refout:"Artifacts/Recovery/Assembly-CSharp-Editor.ref.dll"']
editor_rsp=out/'editor-compile.rsp';editor_rsp.write_text('\n'.join(editor_lines),encoding='utf-8')
editor_result=subprocess.run([str(editor/'NetCoreRuntime/dotnet.exe'),str(editor/'DotNetSdkRoslyn/csc.dll'),'@'+str(editor_rsp)],cwd=root,capture_output=True,text=True)
editor_log=editor_result.stdout+editor_result.stderr
(out/'compile-editor.txt').write_text(editor_log,encoding='utf-8')
editor_errors=[line for line in editor_log.splitlines() if 'error ' in line]
print(f'Editor assembly compile exit={editor_result.returncode}; errors={len(editor_errors)}')
for line in editor_errors:print(line)
raise SystemExit(editor_result.returncode)
