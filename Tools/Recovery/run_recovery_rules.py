from pathlib import Path
import json
import os
import shutil
import subprocess

root=Path(__file__).resolve().parents[2]
out=root/'Artifacts/Recovery'
editor=Path(r'C:\Users\User\Desktop\D drive Data\Unity Game Engine\6000.0.80f1\Editor\Data')
runtime=editor/'NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21'
args=['-noconfig','-nostdlib+','-target:exe','-out:"Artifacts/Recovery/RecoveryRulesHarness.dll"']
managed=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Linq.dll','System.Collections.dll','System.Runtime.Extensions.dll','System.ObjectModel.dll','netstandard.dll']
args += ['-r:"'+str(runtime/name)+'"' for name in managed]
args += ['-r:"Artifacts/Recovery/Assembly-CSharp.dll"', '-r:"'+str(editor/'Managed/UnityEngine/UnityEngine.CoreModule.dll')+'"','"Tools/Recovery/RecoveryRulesHarness.cs"']
rsp=out/'rules-compile.rsp';rsp.write_text('\n'.join(args),encoding='utf-8')
dotnet=str(editor/'NetCoreRuntime/dotnet.exe')
compiled=subprocess.run([dotnet,str(editor/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp)],cwd=root,capture_output=True,text=True)
(out/'compile-rules.txt').write_text(compiled.stdout+compiled.stderr,encoding='utf-8')
if compiled.returncode:
    print(compiled.stdout+compiled.stderr);raise SystemExit(compiled.returncode)
for name in ['UnityEngine.CoreModule.dll','UnityEngine.SharedInternalsModule.dll']:
    shutil.copy2(editor/'Managed/UnityEngine'/name,out/name)
(out/'RecoveryRulesHarness.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net6.0','framework':{'name':'Microsoft.NETCore.App','version':'6.0.21'}}}),encoding='utf-8')
environment=os.environ.copy()
environment['HM_RECOVERY_ENGINE_MANAGED']=str(editor/'Managed/UnityEngine')
environment['HM_RECOVERY_SCRIPT_ASSEMBLIES']=str(root/'Library/ScriptAssemblies')
result=subprocess.run([dotnet,str(out/'RecoveryRulesHarness.dll')],cwd=root,capture_output=True,text=True,env=environment)
log=result.stdout+result.stderr
(out/'rules-checks.txt').write_text(log,encoding='utf-8')
print(log)
raise SystemExit(result.returncode)
