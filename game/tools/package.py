"""Release packaging: Godot export -> Windows installer (.exe) + macOS disk image (.dmg) + zips, into one folder.

    python game/tools/package.py <output folder> 1.0

Windows: the exported game is zipped and appended to a small installer (tools/Setup.cs, built with the .NET Framework
compiler in every Windows) that unpacks it to %LOCALAPPDATA%/Programs, adds Start-menu / desktop shortcuts and an uninstaller.
macOS: the exported .app (from Godot's zip, file modes kept) goes into a Rock Ridge ISO 9660 image with an Applications
link, saved as .dmg (macOS mounts it like any disk image). Not notarized: first launch = right-click > Open.
"""
import os, shutil, subprocess, sys, zipfile, stat

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GODOT = os.environ.get("GODOT", "godot")   # path to the Godot 4.7 .NET console executable
NAME = "Chaos Rift"
APP = "Yu-Gi-Oh! Chaos Rift"
FILE = "ChaosRift"   # release file prefix


def run(*args, **kw):
    print(">", " ".join(map(str, args)), flush=True)
    subprocess.run(args, check=True, **kw)


def export(out, ver):
    proj = os.path.join(ROOT, "game", "Godot")
    win, mac = os.path.join(out, "build", "windows"), os.path.join(out, "build", "mac")
    for d in (win, mac):
        shutil.rmtree(d, ignore_errors=True)
        os.makedirs(d)
    run(GODOT, "--headless", "--path", proj, "--export-release", "Windows Desktop", os.path.join(win, NAME + ".exe"))
    run(GODOT, "--headless", "--path", proj, "--export-release", "macOS", os.path.join(mac, NAME + ".zip"))
    return win, os.path.join(mac, NAME + ".zip")


def windows_setup(win, out, ver):
    """Setup.exe = tools/Setup.cs (compiled by the .NET Framework csc in every Windows) + the game zip + its length.
    (IExpress was tried first: its CAB can't carry a 1.4 GB game.)"""
    stage = os.path.join(out, "build", "setup")
    shutil.rmtree(stage, ignore_errors=True)
    os.makedirs(stage)
    payload = shutil.make_archive(os.path.join(stage, "game"), "zip", win)
    fw = "C:/Windows/Microsoft.NET/Framework64/v4.0.30319"
    src = os.path.join(stage, "Setup.cs")
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "Setup.cs"), encoding="utf-8") as f:
        code = f.read().replace('"VERSION"', f'"{ver}"').replace("APPNAME", NAME)
    with open(src, "w", encoding="utf-8") as f:
        f.write(code)
    stub = os.path.join(stage, "stub.exe")
    icon = os.path.join(ROOT, "game", "Godot", "assets", "title", "icon.ico")
    logo = os.path.join(ROOT, "game", "Godot", "assets", "title", "rift", "R0.png")   # the setup window shows the logo
    run(os.path.join(fw, "csc.exe"), "/nologo", "/target:winexe", "/optimize+", f"/out:{stub}", "/r:System.IO.Compression.dll",
        "/r:System.IO.Compression.FileSystem.dll", "/r:System.Windows.Forms.dll", "/r:System.Drawing.dll", f"/resource:{logo},Logo.png",
        *([f"/win32icon:{icon}"] if os.path.exists(icon) else []), src)
    target = os.path.join(out, f"{FILE}-{ver}-Setup.exe")
    with open(target, "wb") as o:
        for part in (stub, payload):
            with open(part, "rb") as i:
                shutil.copyfileobj(i, o, 1 << 22)
        o.write(os.path.getsize(payload).to_bytes(8, "little"))
    print("wrote", target, os.path.getsize(target) // (1 << 20), "MB")


def mac_dmg(mac_zip, out, ver):
    import pycdlib
    tree = os.path.join(out, "build", "m")   # short: the .app nests deep (Windows path limit)
    shutil.rmtree(tree, ignore_errors=True)
    modes = {}
    with zipfile.ZipFile(mac_zip) as z:
        for info in z.infolist():
            z.extract(info, tree)
            modes[info.filename.rstrip("/")] = (info.external_attr >> 16) & 0o7777
    # pycdlib needs ISO9660 paths per directory; build them with short unique names while Rock Ridge keeps the real ones.
    iso = pycdlib.PyCdlib()
    iso.new(rock_ridge="1.09", vol_ident="CHAOS_RIFT", interchange_level=4)   # macOS reads the Rock Ridge names and modes
    isopath = {".": ""}
    count = 0
    for base, dirs, files in os.walk(tree):
        rel = os.path.relpath(base, tree).replace("\\", "/")
        for d in sorted(dirs):
            r = d if rel == "." else f"{rel}/{d}"
            count += 1
            isopath[r] = f"{isopath[rel]}/D{count:05d}"
            iso.add_directory(isopath[r], rr_name=d)
        for fn in sorted(files):
            r = fn if rel == "." else f"{rel}/{fn}"
            count += 1
            mode = modes.get(r) or 0o644
            iso.add_file(os.path.join(base, fn), f"{isopath[rel]}/F{count:05d}.;1", rr_name=fn, file_mode=0o100000 | mode)
    iso.add_symlink("/APPS.;1", rr_symlink_name="Applications", rr_path="/Applications")
    target = os.path.join(out, f"{FILE}-{ver}-Mac.dmg")
    iso.write(target)
    iso.close()
    print("wrote", target, os.path.getsize(target) // (1 << 20), "MB")


if __name__ == "__main__":
    out, ver = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    win, mac_zip = export(out, ver)
    shutil.make_archive(os.path.join(out, f"{FILE}-{ver}-Windows"), "zip", win)
    shutil.copy(mac_zip, os.path.join(out, f"{FILE}-{ver}-Mac.zip"))
    windows_setup(win, out, ver)
    mac_dmg(mac_zip, out, ver)
