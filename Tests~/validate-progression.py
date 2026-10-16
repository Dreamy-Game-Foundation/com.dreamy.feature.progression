#!/usr/bin/env python3
"""Compile runtime/tests/sample and run managed domain regressions against Unity dependencies.
DiskEnvelope test needs Unity Application.persistentDataPath and is run by the Unity suite.
Run from the sandbox root after Unity has generated Library/ScriptAssemblies.
"""
import json
import subprocess
import tempfile
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path.cwd()
package = Path(__file__).resolve().parents[1]
assert json.loads((package / 'Runtime/Dreamy.Progression.Runtime.asmdef').read_text())['noEngineReferences']
for source in (package / 'Runtime').rglob('*.cs'):
    assert 'using UnityEditor' not in source.read_text() and 'using UnityEngine' not in source.read_text(), source

with tempfile.TemporaryDirectory(prefix='dreamy-progression-') as temp:
    temp = Path(temp)
    refs = {p.stem: p for p in (root / 'Library/ScriptAssemblies').glob('*.dll')
            if p.stem in {'Dreamy.Core.Runtime', 'Dreamy.Datasave.Runtime', 'Dreamy.DataConfig.Runtime', 'Dreamy.Economy.Runtime', 'Dreamy.UI.Presentation'}}
    for generated in ['Dreamy.Progression.Runtime.Tests.csproj', 'Dreamy.Progression.Runtime.csproj', 'Dreamy.Template.Runtime.csproj']:
        for reference in ET.parse(root / generated).findall('.//Reference'):
            hint = reference.find('HintPath')
            if hint is None: continue
            path = Path(hint.text)
            if not path.is_absolute(): path = root / path
            name = reference.attrib['Include']
            if name.startswith('UnityEngine') or path.name in ['Newtonsoft.Json.dll', 'nunit.framework.dll']:
                refs[name] = path

    def project(name, sources, executable=False):
        folder = temp / name
        folder.mkdir()
        document = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
        props = ET.SubElement(document, 'PropertyGroup')
        for k, v in dict(TargetFramework='net10.0', OutputType='Exe' if executable else 'Library',
                         AssemblyName=name, EnableDefaultCompileItems='false', GenerateDependencyFile='true' if executable else 'false', NoWarn='0649;0067').items():
            ET.SubElement(props, k).text = v
        items = ET.SubElement(document, 'ItemGroup')
        for source in sources: ET.SubElement(items, 'Compile', Include=str(source))
        for name_ref, path in refs.items():
            reference = ET.SubElement(items, 'Reference', Include=name_ref)
            ET.SubElement(reference, 'HintPath').text = str(path)
        path = folder / 'Harness.csproj'
        ET.ElementTree(document).write(path)
        subprocess.run(['dotnet', 'build', str(path), '--verbosity', 'quiet'], check=True)
        return path, folder / f'bin/Debug/net10.0/{name}.dll'

    _, runtime = project('Dreamy.Progression.Runtime', (package / 'Runtime').rglob('*.cs'))
    refs['Dreamy.Progression.Runtime'] = runtime
    project('Dreamy.Progression.Rewards.Sample', (package / 'Samples~/Progression Rewards').glob('*.cs'))
    runner = temp / 'Runner.cs'
    runner.write_text('''using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
class Runner {
 static int Main() {
  int passed=0, failed=0, skipped=0;
  foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.IsPublic && t.Namespace=="Dreamy.Progression.Tests"))
  foreach(var method in type.GetMethods()) {
   var cases=method.GetCustomAttributes<TestCaseAttribute>().ToArray();
   if(cases.Length==0 && method.GetCustomAttribute<TestAttribute>()==null) continue;
   if(method.Name.StartsWith("DiskEnvelope")) { skipped++; continue; }
   var args=cases.Length==0 ? new[]{Array.Empty<object>()} : cases.Select(c=>c.Arguments).ToArray();
   foreach(var testArgs in args) {
    try { method.Invoke(Activator.CreateInstance(type),testArgs); passed++; }
    catch(Exception e) { failed++; Console.WriteLine("FAIL "+method.Name+" "+(e.InnerException??e)); }
   }
  }
  Console.WriteLine($"Progression managed tests: {passed} passed, {failed} failed, {skipped} Unity-only.");
  return failed==0 ? 0 : 1;
 }
}''')
    path, _ = project('ProgressionHarness', [*(package / 'Tests/Runtime').glob('*.cs'), runner], executable=True)
    subprocess.run(['dotnet', 'run', '--project', str(path), '--no-build', '--verbosity', 'quiet'], check=True)
