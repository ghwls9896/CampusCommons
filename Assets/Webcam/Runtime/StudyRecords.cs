using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Serialization;

namespace CampusCommons.Webcam
{
    [Serializable]
    public class StudySegment
    {
        public int sequence;
        public long start_ms, end_ms;
        public string state;
    }

    [Serializable]
    public class StudyRecord
    {
        public string user_id, session_id;
        public long started_ms, ended_ms;
        public int acked = -1, frozen = -1;
        public bool end_acked;
        public string sync_error;
        internal bool HasPending => acked < segments.Count - 1 || (ended_ms > 0 && !end_acked);
        public List<StudySegment> segments = new List<StudySegment>();
    }

    [Serializable]
    public class StudyDaily
    {
        public string user_id;
        public long day_ms, total_ms;
    }

    [Serializable]
    public class StudyArchive
    {
        public List<StudyRecord> sessions = new List<StudyRecord>();
        public List<StudyDaily> totals = new List<StudyDaily>();
    }

    public sealed class StudyStore : IDisposable
    {
        [DllImport("libc", SetLastError = true)]
        static extern int flock(int fd, int operation);

        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(StudyArchive));
        readonly string path;
        FileStream lease;

        public StudyArchive Data { get; private set; } = new StudyArchive();

        StudyStore(string path)
        {
            this.path = path;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite,
                windows ? FileShare.None : FileShare.ReadWrite);
            // macOS Unity Mono needs the OS lock across separate game processes.
            if (!windows && flock(lease.SafeFileHandle.DangerousGetHandle().ToInt32(), 2 | 4) != 0)
            {
                Dispose();
                throw new IOException("Study records already in use");
            }
        }

        public static StudyStore Load(string path)
        {
            var store = new StudyStore(path);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return store;

            foreach (string candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    using (var stream = File.OpenRead(candidate))
                    using (var xml = XmlReader.Create(stream, new XmlReaderSettings
                    { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    {
                        store.Data = (StudyArchive)Serializer.Deserialize(xml);
                        return store;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (InvalidOperationException) { }
            }

            store.Dispose();
            throw new IOException("Study history cannot be loaded");
        }

        public void Save()
        {
            using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Serializer.Serialize(file, Data);
                file.Flush(true);
            }

            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
        }

        public StudyRecord Begin(string user, long utc)
        {
            var record = new StudyRecord
            {
                user_id = user,
                session_id = Guid.NewGuid().ToString(),
                started_ms = utc
            };
            Data.sessions.Add(record);
            return record;
        }

        public void Append(StudyRecord record, long start, long end, string state)
        {
            if (end <= start) return;
            var last = record.segments.LastOrDefault();
            if (last != null && last.sequence > record.frozen && last.state == state &&
                last.end_ms == start && end - last.start_ms <= 30000)
            {
                last.end_ms = end;
                return;
            }

            record.segments.Add(new StudySegment
            {
                sequence = record.segments.Count, start_ms = start, end_ms = end, state = state
            });
        }

        public StudySegment[] Batch(StudyRecord record)
        {
            var batch = record.segments.Where(segment => segment.sequence > record.acked).Take(128)
                .Select(segment => new StudySegment
                {
                    sequence = segment.sequence, start_ms = segment.start_ms, end_ms = segment.end_ms, state = segment.state
                }).ToArray();
            if (batch.Length > 0) record.frozen = Math.Max(record.frozen, batch[batch.Length - 1].sequence);
            return batch;
        }

        public void Acknowledge(StudyRecord record, int sequence, long day, long total)
        {
            if (sequence < record.acked || sequence > record.frozen)
                throw new InvalidDataException("Unexpected study acknowledgement");
            SetTotal(record.user_id, day, total);
            record.acked = sequence;
        }

        public void SetTotal(string user, long day, long total)
        {
            if (total < 0 || total > StudyTime.DayMilliseconds)
                throw new InvalidDataException("Invalid daily total");
            var cached = Data.totals.Find(entry => entry.user_id == user);
            if (cached == null)
            {
                cached = new StudyDaily { user_id = user };
                Data.totals.Add(cached);
            }
            if (day < cached.day_ms) return;
            cached.day_ms = day;
            cached.total_ms = total;
        }

        public long Today(string user, long now)
        {
            long day = StudyTime.DayStart(now);
            long sum = Data.totals.Find(entry => entry.user_id == user && entry.day_ms == day)?.total_ms ?? 0;
            foreach (var record in Data.sessions)
            {
                if (record.user_id != user || !string.IsNullOrEmpty(record.sync_error)) continue;
                foreach (var segment in record.segments)
                    if (segment.sequence > record.acked && segment.state == "studying")
                        sum += StudyTime.Overlap(segment.start_ms, segment.end_ms, day, day + StudyTime.DayMilliseconds);
            }
            return sum;
        }

        public void Dispose()
        {
            lease?.Dispose();
            lease = null;
        }
    }
}
