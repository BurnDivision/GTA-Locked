#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
python3 - <<'PY'
from pathlib import Path
import hashlib, json, os, re, shutil, subprocess, tempfile, zipfile
root = Path.cwd()
version = re.search(r"versionName\s+['\"]([^'\"]+)", (root.parent/'android/app/build.gradle').read_text()).group(1)
if not re.fullmatch(r'[A-Za-z0-9._-]+', version):
    raise SystemExit('Unsafe version string')
objdump = Path('/home/codebox/.local/opt/llvm-mingw/bin/llvm-objdump')
runtime = Path('/home/codebox/.local/opt/llvm-mingw/x86_64-w64-mingw32/bin')
dist = root/'dist'
dist.mkdir(exist_ok=True)
system = {'kernel32.dll', 'user32.dll', 'gdi32.dll', 'shell32.dll', 'advapi32.dll', 'ole32.dll', 'oleaut32.dll', 'winmm.dll', 'opengl32.dll', 'msvcrt.dll', 'ucrtbase.dll', 'ntdll.dll', 'ws2_32.dll', 'imm32.dll', 'version.dll', 'setupapi.dll', 'comdlg32.dll', 'comctl32.dll', 'shlwapi.dll', 'cfgmgr32.dll', 'bcrypt.dll', 'powrprof.dll', 'avrt.dll', 'propsys.dll', 'secur32.dll'}
logs = []
with tempfile.TemporaryDirectory(prefix='package-', dir=dist) as tmp:
    stage = Path(tmp)
    shutil.copy2(root/'build/carnage3d.exe', stage/'GTA-Perspective-Lock.exe')
    shutil.copy2(root/'deps/openal/bin/Win64/soft_oal.dll', stage/'OpenAL32.dll')
    shutil.copy2(root/'deps/sdl2/bin/SDL2.dll', stage/'SDL2.dll')
    shutil.copytree(root/'engine/gamedata', stage/'gamedata')
    # Strip test controls and any local profile/config residue from the staged copy.
    for path in list((stage/'gamedata').rglob('*')):
        if path.is_file() and path.name.lower() in {'test_input.txt', 'player_a.dat', 'player_a.dat.tmp'}:
            path.unlink()
    config_default = stage/'gamedata/config/sys_config.json.default'
    defaults = json.loads(config_default.read_text())
    defaults['r_screenDims'] = '1280 720'
    defaults['r_fullscreen'] = 'false'
    config_default.write_text(json.dumps(defaults, indent=2)+'\n')
    for name in ('sys_config.json', 'inputs.json'):
        shutil.copy2(stage/'gamedata/config'/f'{name}.default', stage/'gamedata/config'/name)
    shutil.copy2(root/'README-windows.txt', stage/'README.txt')
    shutil.copy2(root/'gamecontrollerdb.txt', stage/'gamecontrollerdb.txt')
    licenses = stage/'licenses'
    licenses.mkdir()
    for name, path in [('GLEW.txt', root/'deps/glew/LICENSE.txt'), ('GLFW.txt', root/'deps/glfw/LICENSE.md'), ('SDL2.txt', root/'deps/sdl2/LICENSE.txt'), ('OpenAL.txt', root/'deps/openal/COPYING'), ('Carnage3D.txt', root.parent/'engines/carnage3d/LICENSE'), ('Box2D.txt', root/'engine/third_party/Box2D/LICENSE'), ('GLM.txt', root.parent/'engines/android-deps/glm/copying.txt'), ('llvm-mingw.txt', Path('/home/codebox/.local/opt/llvm-mingw/LICENSE.TXT'))]:
        if path.exists(): shutil.copy2(path, licenses/name)
    pending = list(stage.glob('*.exe')) + list(stage.glob('*.dll'))
    checked = set()
    while pending:
        binary = pending.pop()
        if binary.name.lower() in checked: continue
        checked.add(binary.name.lower())
        output = subprocess.check_output([str(objdump), '-p', str(binary)], text=True)
        imports = re.findall(r'DLL Name:\s*(\S+)', output)
        logs.append(binary.name + ':\n' + '\n'.join('  '+dll for dll in imports))
        for dll in imports:
            low = dll.lower()
            if low in system or low.startswith(('api-ms-win-', 'ext-ms-win-')): continue
            existing = next((p for p in stage.iterdir() if p.name.lower() == low), None)
            if existing: pending.append(existing); continue
            source = next((p for p in runtime.glob('*.dll') if p.name.lower() == low), None)
            if source is None: raise SystemExit(f'Unresolved DLL dependency: {binary.name}: {dll}')
            shutil.copy2(source, stage/dll)
            pending.append(stage/dll)
    (root/'dll-imports.txt').write_text('\n\n'.join(logs)+'\n')
    archive = dist/f'GTA-Perspective-Lock-windows-{version}.zip'
    tempzip = stage/'package.zip'
    with zipfile.ZipFile(tempzip, 'w', zipfile.ZIP_DEFLATED) as z:
        for path in sorted(stage.rglob('*')):
            if path.is_file() and path != tempzip: z.write(path, path.relative_to(stage))
    with zipfile.ZipFile(tempzip) as z:
        bad = z.testzip()
        if bad: raise SystemExit('Corrupt zip member: '+bad)
        (root/'zip-listing.txt').write_text('\n'.join(f'{i.file_size:10d}  {i.filename}' for i in z.infolist())+'\n')
    os.replace(tempzip, archive)
    (root/'SHA256SUMS').write_text(''.join(
        hashlib.sha256(p.read_bytes()).hexdigest()+'  '+str(p.relative_to(root))+'\n'
        for p in (root/'build/carnage3d.exe', archive)))
    (root/'file-type.txt').write_text(subprocess.check_output(['file', 'build/carnage3d.exe'], text=True))
    print(archive)
PY
