using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace CampusCommons.Webcam
{
    [Serializable]
    public sealed class RuntimePackageManifest
    {
        public int schema;
        public string platform, archive, sha256, python, minimumOS;
        public long archiveBytes, unpackedBytes;
        public string[] executablePaths;
    }

    // Pure .NET: no Unity API on the extraction worker thread.
    public static class RuntimePackageInstaller
    {
        const long MaximumBytes = 12L * 1024 * 1024 * 1024;
        [DllImport("libc", SetLastError = true)] static extern int chmod(string path, uint mode);
        [DllImport("libc", SetLastError = true)] static extern int flock(int descriptor, int operation);
        static FileStream OpenLock(string path, bool shared)
        {
            bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var stream = new FileStream(path, FileMode.OpenOrCreate, shared ? FileAccess.Read : FileAccess.ReadWrite,
                windows ? (shared ? FileShare.Read : FileShare.None) : FileShare.ReadWrite);
            // Unity Mono does not enforce FileShare across processes on macOS.
            // flock is released by the OS on close/crash; no stale lock-file cleanup is needed.
            if (!windows && flock(stream.SafeFileHandle.DangerousGetHandle().ToInt32(), (shared ? 1 : 2) | 4) != 0)
            { stream.Dispose(); throw new IOException("Runtime lock is in use."); }
            return stream;
        }
        public static string HashFile(string path, CancellationToken cancel)
        {
            using (var hash = SHA256.Create()) using (var input = File.OpenRead(path))
            {
                byte[] buffer = new byte[1024 * 1024]; int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                { cancel.ThrowIfCancellationRequested(); hash.TransformBlock(buffer, 0, read, null, 0); }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
        public static void Validate(RuntimePackageManifest m, string platform)
        {
            if (m == null || m.schema != 1 || m.platform != platform ||
                (platform != "macos-arm64" && platform != "windows-x64") ||
                m.sha256 == null || m.sha256.Length != 64 ||
                Array.Exists(m.sha256.ToCharArray(), c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) ||
                m.archiveBytes <= 0 || m.archiveBytes > MaximumBytes || m.unpackedBytes <= 0 || m.unpackedBytes > MaximumBytes ||
                m.archive == null || Path.GetFileName(m.archive) != m.archive || !m.archive.EndsWith(".zip", StringComparison.Ordinal))
                throw new InvalidDataException("게임에 포함된 공부 인식 패키지가 이 기기와 맞지 않습니다.");
            SafeRelative(m.archive);
            if (m.python != (platform == "windows-x64" ? "python/python.exe" : "python/bin/python3.12"))
                throw new InvalidDataException("잘못된 실행 환경 경로입니다.");
            if (m.executablePaths == null) throw new InvalidDataException("실행 권한 정보가 없습니다.");
            foreach (string p in m.executablePaths) SafeRelative(p);
        }
        static string SafeRelative(string path)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith("/") || path.Contains("\\") || path.Contains(":") || path.IndexOf('\0') >= 0)
                throw new InvalidDataException("안전하지 않은 패키지 경로입니다.");
            foreach (string part in path.TrimEnd('/').Split('/'))
                if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".") || part.EndsWith(" "))
                    throw new InvalidDataException("안전하지 않은 패키지 경로입니다.");
            return path;
        }
        static string Marker(RuntimePackageManifest m) => m.sha256 + "\n" + m.platform + "\n" + m.python + "\n" + m.unpackedBytes;
        static bool Ready(string root, RuntimePackageManifest m)
        {
            try { return File.ReadAllText(Path.Combine(root, "ready")) == Marker(m) &&
                File.Exists(Path.Combine(root, m.python)) && File.Exists(Path.Combine(root, "app/live.py")) &&
                File.Exists(Path.Combine(root, "model/manifest.json")); }
            catch (IOException) { return false; }
        }
        static FileStream Acquire(string path, CancellationToken cancel)
        {
            var until = DateTime.UtcNow.AddMinutes(10);
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                try { return OpenLock(path, false); }
                catch (IOException) { if (DateTime.UtcNow >= until) throw; cancel.WaitHandle.WaitOne(100); }
            }
        }
        public static FileStream AcquireUsage(string root)
        {
            // Serialize lease acquisition with installers; never block the Unity main thread.
            using (OpenLock(root + ".lock", false))
            {
                if (!File.Exists(Path.Combine(root, "ready"))) throw new InvalidDataException("공부 인식을 다시 준비해 주세요.");
                return OpenLock(root + ".use", true);
            }
        }
        static FileStream ExclusiveUsage(string root)
        {
            try { return OpenLock(root + ".use", false); }
            catch (IOException) { throw new InvalidDataException("다른 게임 창에서 공부 인식을 사용 중입니다. 종료한 뒤 다시 준비해 주세요."); }
        }
        public static string Install(RuntimePackageManifest m, string archive, string installBase, string platform,
            Action<float, string> progress, CancellationToken cancel, bool force = false)
        {
            Validate(m, platform); cancel.ThrowIfCancellationRequested();
            if (!File.Exists(archive) || new FileInfo(archive).Length != m.archiveBytes)
                throw new InvalidDataException("공부 인식 파일이 누락되거나 손상됐습니다. 게임 파일을 다시 받아 주세요.");
            Directory.CreateDirectory(installBase);
            // Shorten Windows extraction paths to reduce legacy MAX_PATH failures.
            // The marker still stores and checks the complete digest.
            string root = Path.Combine(installBase, m.sha256.Substring(0, 16)), stage = root + ".installing";
            progress?.Invoke(0, "공부 인식 준비 중");
            using (Acquire(root + ".lock", cancel))
            {
                if (!force && Ready(root, m)) { progress?.Invoke(1, "공부 인식 준비 완료"); return root; }
                string existingMarker = Path.Combine(root, "ready");
                if (File.Exists(existingMarker) && !File.ReadAllText(existingMarker).StartsWith(m.sha256 + "\n", StringComparison.Ordinal))
                    throw new InvalidDataException("공부 인식 패키지 경로가 충돌했습니다.");
                using (ExclusiveUsage(root))
                {
                progress?.Invoke(.01f, "게임 파일 확인 중");
                if (HashFile(archive, cancel) != m.sha256) throw new InvalidDataException("공부 인식 파일 검증에 실패했습니다. 게임 파일을 다시 받아 주세요.");
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(installBase)));
                if (drive.AvailableFreeSpace < m.unpackedBytes + 64L * 1024 * 1024)
                    throw new IOException("공부 인식 준비에 필요한 저장 공간이 부족합니다.");
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                Directory.CreateDirectory(stage);
                try
                {
                    long written = 0; byte[] buffer = new byte[1024 * 1024];
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var prefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    using (var input = File.OpenRead(archive)) using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
                    {
                        if (zip.Entries.Count > 100000) throw new InvalidDataException("패키지 파일 수가 올바르지 않습니다.");
                        foreach (var entry in zip.Entries)
                        {
                            cancel.ThrowIfCancellationRequested(); string relative = SafeRelative(entry.FullName);
                            int kind = (entry.ExternalAttributes >> 16) & 0xF000;
                            if (kind == 0xA000 || (kind != 0 && kind != 0x8000 && kind != 0x4000) || !names.Add(relative.TrimEnd('/')))
                                throw new InvalidDataException("패키지에 링크 또는 중복 경로가 있습니다.");
                            string prefix = "";
                            foreach (string part in relative.TrimEnd('/').Split('/'))
                            {
                                prefix = prefix.Length == 0 ? part : prefix + "/" + part;
                                if (prefixes.TryGetValue(prefix, out var existing) && existing != prefix)
                                    throw new InvalidDataException("패키지 경로의 대소문자가 충돌합니다.");
                                prefixes[prefix] = prefix;
                            }
                            string destination = Path.Combine(stage, relative);
                            if (relative.EndsWith("/")) { Directory.CreateDirectory(destination); continue; }
                            if (entry.Length > m.unpackedBytes - written) throw new InvalidDataException("패키지 크기가 올바르지 않습니다.");
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            using (var source = entry.Open()) using (var target = new FileStream(destination, FileMode.CreateNew))
                            {
                                int read; while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    cancel.ThrowIfCancellationRequested(); written += read;
                                    if (written > m.unpackedBytes) throw new InvalidDataException("패키지 크기를 초과했습니다.");
                                    target.Write(buffer, 0, read); progress?.Invoke(.1f + .85f * (float)((double)written / m.unpackedBytes), "공부 인식 파일 준비 중");
                                }
                            }
                        }
                    }
                    if (written != m.unpackedBytes || !File.Exists(Path.Combine(stage, m.python)) ||
                        !File.Exists(Path.Combine(stage, "app/live.py")) || !File.Exists(Path.Combine(stage, "model/manifest.json")))
                        throw new InvalidDataException("필수 공부 인식 파일이 없습니다.");
                    foreach (string executable in m.executablePaths)
                    {
                        string p = Path.Combine(stage, executable);
                        if (!File.Exists(p)) throw new InvalidDataException("실행 파일이 없습니다.");
                        if (Environment.OSVersion.Platform != PlatformID.Win32NT && chmod(p, 493) != 0)
                            throw new IOException("공부 인식 실행 권한을 설정하지 못했습니다.");
                    }
                    cancel.ThrowIfCancellationRequested();
                    File.WriteAllText(Path.Combine(stage, "ready"), Marker(m));
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                    Directory.Move(stage, root); progress?.Invoke(1, "공부 인식 준비 완료"); return root;
                }
                finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
                }
            }
        }
    }
}
