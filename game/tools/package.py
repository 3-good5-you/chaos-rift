"""Release packaging: Godot export -> Windows installer (.exe) + macOS disk image (.dmg) + zips, into one folder.

    python game/tools/package.py "D:/Yu-Gi-Oh Extras/releases/v1.1" 1.1

Windows: the exported game is zipped and appended to a small installer (tools/Setup.cs, built with the .NET Framework
compiler in every Windows) that unpacks it to %LOCALAPPDATA%/Programs, adds Start-menu / desktop shortcuts and an uninstaller.
macOS: the exported .app (from Godot's zip, file modes kept) goes into a Rock Ridge ISO 9660 image with an Applications
link, saved as .dmg (macOS mounts it like any disk image). Not notarized: first launch = right-click > Open.
Linux (when Godot's Linux export templates are installed): the exported folder, zipped.

Small updates (2026-11-15): with --base <an earlier full release folder made by this script> it also writes one patch
per platform: only what changed since that base (Godot's --export-patch pack as patch.pck, plus the changed exe/DLL
files and patch.json {version, base}). Patches are cumulative from the base, so the game only ever needs the newest.
    python game/tools/package.py "D:/Yu-Gi-Oh Extras/releases/Chaos Rift 1.2.1" 1.2.1 --base "D:/Yu-Gi-Oh Extras/releases/Chaos Rift 1.2.0"
  ChaosRift-<v>-Patch-Windows.exe  the Setup window program + the patch zip (installs over the folder, no wipe)
  ChaosRift-<v>-Patch-Mac.zip      paths inside Chaos Rift.app; the game unpacks it itself
  ChaosRift-<v>-Patch-Linux.zip    paths inside the game folder; the game unpacks it itself
"""
import filecmp, glob, json, os, re, shutil, subprocess, sys, zipfile, stat

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GODOT = r"C:\Users\norma\tools\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
NAME = "Chaos Rift"
APP = "Yu-Gi-Oh! Chaos Rift"
FILE = "ChaosRift"   # release file prefix
PROJ = os.path.join(ROOT, "game", "Godot")
LINUX = os.path.exists(os.path.join(os.environ.get("APPDATA", ""), "Godot", "export_templates", "4.7.2.stable.mono", "linux_release.x86_64"))


def run(*args, **kw):
    print(">", " ".join(map(str, args)), flush=True)
    subprocess.run(args, check=True, **kw)


def export(out, ver):
    proj = PROJ
    win, mac, lin = (os.path.join(out, "build", p) for p in ("windows", "mac", "linux"))
    for d in (win, mac, lin):
        shutil.rmtree(d, ignore_errors=True)
        os.makedirs(d)
    run(GODOT, "--headless", "--path", proj, "--export-release", "Windows Desktop", os.path.join(win, NAME + ".exe"))
    run(GODOT, "--headless", "--path", proj, "--export-release", "macOS", os.path.join(mac, NAME + ".zip"))
    if LINUX: run(GODOT, "--headless", "--path", proj, "--export-release", "Linux", os.path.join(lin, FILE + ".x86_64"))
    else: print("! no Linux export templates: skipping Linux")
    return win, os.path.join(mac, NAME + ".zip"), lin if LINUX else None


# ---------------------------------------------------------------- small updates (patches against a base release)

def patch_pck(preset, base_pck, target):
    """Godot's --export-patch: the resources that differ from the base release's pack. None when none do."""
    r = subprocess.run([GODOT, "--headless", "--path", PROJ, "--export-patch", preset, target, "--patches", base_pck],
                       capture_output=True, text=True, errors="replace")
    if r.returncode == 0 and os.path.exists(target): return target
    if "No files or changes to export" in r.stdout + r.stderr:
        if os.path.exists(target): os.remove(target)
        return None
    sys.exit(r.stdout[-4000:] + r.stderr[-4000:] + "\npatch export failed: " + preset)


def folder_changes(new, old):
    """(path, name in the patch, mode) for every file of the new export that the base export lacks or has different;
    the main .pck is left out (patch.pck carries its changes)."""
    for base, _, files in os.walk(new):
        for f in files:
            p = os.path.join(base, f)
            rel = os.path.relpath(p, new).replace("\\", "/")
            if rel.endswith(".pck") and "/" not in rel: continue
            q = os.path.join(old, rel)
            if not (os.path.exists(q) and filecmp.cmp(p, q, shallow=False)): yield p, rel, 0o755 if rel.endswith(".x86_64") else 0o644


def unsigned(data):
    """A Mach-O file (thin or universal) with its code-signature blobs zeroed: each export signs with a fresh random
    identifier, and the game signs the .app again after patching, so only the code itself decides what changed."""
    import struct
    def slices(d):
        magic = struct.unpack(">I", d[:4])[0] if len(d) >= 4 else 0
        if magic == 0xCAFEBABE:   # universal: big-endian arch table
            n = struct.unpack(">I", d[4:8])[0]
            return [struct.unpack(">IIIII", d[8 + 20 * i:28 + 20 * i])[2] for i in range(n)]
        return [0] if magic in (0xFEEDFACF, 0xCFFAEDFE) else []
    out = None
    for off in slices(data):
        if struct.unpack("<I", data[off:off + 4])[0] != 0xFEEDFACF: continue   # 64-bit little-endian only
        ncmds = struct.unpack("<I", data[off + 16:off + 20])[0]
        at = off + 32
        for _ in range(ncmds):
            cmd, size = struct.unpack("<II", data[at:at + 8])
            if cmd == 0x1D:   # LC_CODE_SIGNATURE
                doff, dsize = struct.unpack("<II", data[at + 8:at + 16])
                out = out or bytearray(data)
                out[off + doff:off + doff + dsize] = bytes(dsize)
            at += size
    return bytes(out) if out else data


def write_patch(target, files, pck, at, ver, bv):
    """files: (source path or bytes, name in the zip, unix mode). `at`: where patch.pck / patch.json go."""
    files = list(files)
    if pck: files.append((pck, at + "patch.pck", 0o644))
    files.append((json.dumps({"version": ver, "base": bv}).encode(), at + "patch.json", 0o644))
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
        for src, name, mode in files:
            zi = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
            zi.external_attr, zi.compress_type = (0o100000 | mode) << 16, zipfile.ZIP_DEFLATED
            if isinstance(src, bytes): z.writestr(zi, src)
            else:
                with open(src, "rb") as i, z.open(zi, "w") as o: shutil.copyfileobj(i, o, 1 << 22)
            print("  patch:", name)
    print("wrote", target, os.path.getsize(target) >> 10, "KB")
    return target


def patches(out, ver, base, win, mac_zip, lin):
    stage = os.path.join(out, "build", "patch")
    shutil.rmtree(stage, ignore_errors=True)
    os.makedirs(stage)
    found = glob.glob(os.path.join(base, f"{FILE}-*-Setup.exe"))
    if not found: sys.exit("no Setup.exe in the base release " + base)
    bv = re.match(rf"{FILE}-(.+)-Setup\.exe", os.path.basename(found[0])).group(1)
    print(f"patches from {bv} to {ver}")
    # Windows: the Setup window program with the patch zip as its payload (Setup sees patch.json: no folder wipe)
    bwin = os.path.join(base, "build", "windows")
    z = write_patch(os.path.join(stage, "win.zip"), folder_changes(win, bwin),
                    patch_pck("Windows Desktop", os.path.join(bwin, NAME + ".pck"), os.path.join(stage, "win.pck")), "", ver, bv)
    append_payload(setup_stub(stage, ver), z, os.path.join(out, f"{FILE}-{ver}-Patch-Windows.exe"))
    # macOS: compared entry by entry inside the two .app zips (CRC + size); names inside the .app
    with zipfile.ZipFile(os.path.join(base, f"{FILE}-{bv}-Mac.zip")) as o:
        old = {i.filename: (i.CRC, i.file_size) for i in o.infolist()}
        main = next(n for n in old if n.endswith("/Contents/Resources/" + NAME + ".pck"))
        with open(os.path.join(stage, "base-mac.pck"), "wb") as f, o.open(main) as i: shutil.copyfileobj(i, f, 1 << 22)
    with zipfile.ZipFile(mac_zip) as n, zipfile.ZipFile(os.path.join(base, f"{FILE}-{bv}-Mac.zip")) as o:
        files = [(n.read(i), i.filename.split("/", 1)[1], (i.external_attr >> 16) & 0o7777 or 0o644)
                 for i in n.infolist() if not i.is_dir() and i.filename != main and old.get(i.filename) != (i.CRC, i.file_size)
                 and not i.filename.endswith("/_CodeSignature/CodeResources")   # the game signs the .app again
                 and (i.filename not in old or unsigned(n.read(i)) != unsigned(o.read(i.filename)))]
    write_patch(os.path.join(out, f"{FILE}-{ver}-Patch-Mac.zip"), files,
                patch_pck("macOS", os.path.join(stage, "base-mac.pck"), os.path.join(stage, "mac.pck")), "Contents/Resources/", ver, bv)
    # Linux: like Windows, a plain zip the game unpacks itself
    blin = os.path.join(base, "build", "linux")
    if lin and os.path.exists(os.path.join(blin, FILE + ".pck")):
        write_patch(os.path.join(out, f"{FILE}-{ver}-Patch-Linux.zip"), folder_changes(lin, blin),
                    patch_pck("Linux", os.path.join(blin, FILE + ".pck"), os.path.join(stage, "lin.pck")), "", ver, bv)
    elif lin: print("! the base release has no Linux build: no Linux patch (Linux players take the full zip)")


def windows_setup(win, out, ver):
    """Setup.exe = tools/Setup.cs (compiled by the .NET Framework csc in every Windows) + the game zip + its length.
    (IExpress was tried first: its CAB can't carry a 1.4 GB game.)"""
    stage = os.path.join(out, "build", "setup")
    shutil.rmtree(stage, ignore_errors=True)
    os.makedirs(stage)
    payload = shutil.make_archive(os.path.join(stage, "game"), "zip", win)
    append_payload(setup_stub(stage, ver), payload, os.path.join(out, f"{FILE}-{ver}-Setup.exe"))


def setup_stub(stage, ver):
    """The Setup window program alone (tools/Setup.cs, version and name filled in)."""
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
    return stub


def append_payload(stub, payload, target):
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
    base = sys.argv[sys.argv.index("--base") + 1] if "--base" in sys.argv else None
    os.makedirs(out, exist_ok=True)
    win, mac_zip, lin = export(out, ver)
    shutil.make_archive(os.path.join(out, f"{FILE}-{ver}-Windows"), "zip", win)
    shutil.copy(mac_zip, os.path.join(out, f"{FILE}-{ver}-Mac.zip"))
    if lin: shutil.make_archive(os.path.join(out, f"{FILE}-{ver}-Linux"), "zip", lin)
    windows_setup(win, out, ver)
    mac_dmg(mac_zip, out, ver)
    if base: patches(out, ver, base, win, mac_zip, lin)
