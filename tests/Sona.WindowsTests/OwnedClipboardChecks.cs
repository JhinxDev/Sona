using System.Runtime.InteropServices;
using Sona.App.Windows;

internal static class OwnedClipboardChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        static (FakeNative Native, OwnedClipboard Clipboard) Create()
        {
            var native = new FakeNative();
            native.UserCopy((13u, new byte[] { 65, 0, 0, 0 }), (0xC001u, new byte[] { 1, 2, 3 }));
            return (native, new OwnedClipboard(() => 42, native));
        }
        {
            var (n, c) = Create(); var original = n.CopyContents();
            using var snapshot = c.Capture();
            var staged = c.TrySetText(snapshot, "fixture draft");
            Check(staged.Status == ClipboardChangeStatus.Staged && n.Contents.Count == 6, "Complete native text representations and privacy staging failed.");
            Check(n.Contents[0xC002].SequenceEqual(new byte[4]) && n.Contents[0xC003].SequenceEqual(new byte[4]), "Temporary draft must opt out of Windows clipboard history and cloud upload.");
            var restored = c.Restore(snapshot, staged.Sequence);
            Check(restored.Status == ClipboardChangeStatus.Restored && n.SameContents(original), "All original text and HTML bytes/order must be restored.");
            Check(n.Owned.Count == 0 && n.OpenCount == n.CloseCount, "Transferred allocations or clipboard locks leaked.");
        }
        {
            var (n, c) = Create(); var payload = n.CopyContents();
            n.Contents[0xC006] = [99]; n.Contents[0xC007] = [88];
            using var snapshot = c.Capture();
            var staged = c.TrySetText(snapshot, "WPF copy regression");
            Check(staged.Status == ClipboardChangeStatus.Staged && c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored && n.SameContents(payload), "WPF DataObject/Ole Private Data bookkeeping must not block exact supported payload restoration.");
            Check(!n.ReadFormats.Contains(0xC006) && !n.ReadFormats.Contains(0xC007), "Owner-bound OLE metadata must never be copied or replayed.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); n.SynthesizeOnClose = true;
            var staged = c.TrySetText(snapshot, "CloseClipboard synthesis");
            Check(staged.Status == ClipboardChangeStatus.Staged && staged.Sequence == n.Sequence, "Windows close-time text synthesis must not invalidate Sona's own returned sequence.");
            Check(c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored, "Strict restoration must accept the completely materialized staging generation.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var original = n.CopyContents(); n.FailMaterialize = true;
            Check(c.TrySetText(snapshot, "conversion failure").Status == ClipboardChangeStatus.FailedRestored && n.SameContents(original) && n.Owned.Count == 0, "Failed native text conversion must rollback under the same lock without leaking handles.");
        }
        foreach (bool addUnknownPayload in new[] { false, true })
        {
            var (n, c) = Create(); n.UserCopy((0xC006, new byte[] { 99 }), (0xC007, new byte[] { 88 }));
            if (addUnknownPayload) n.Contents[0xC999] = [77];
            bool refused = false; try { using var snapshot = c.Capture(); } catch (ExternalException) { refused = true; }
            Check(refused && n.EmptyCalls == 0, "OLE bookkeeping alone or an unknown real OLE payload must still refuse without mutation.");
        }
        {
            var (n, c) = Create(); var initial = n.Sequence; n.OnRead = n.Materialize;
            using var snapshot = c.Capture();
            Check(snapshot.InitialSequence == initial && snapshot.Sequence == initial + 1, "Snapshot must distinguish pre-render ownership from final materialized sequence.");
            Check(c.TrySetText(snapshot, "after delayed render").Status == ClipboardChangeStatus.Staged, "Own delayed rendering must not be confused with a newer copy.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var original = n.CopyContents(); n.FailOpen = true;
            Check(c.TrySetText(snapshot, "draft").Status == ClipboardChangeStatus.Unavailable && n.SameContents(original) && n.EmptyCalls == 0, "Failed OpenClipboard must not mutate.");
            Check(n.Owned.Count == 0, "Failed lock leaked staged allocations.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture();
            n.BeforeOpen = () => n.UserCopy((13, new byte[] { 66, 0, 0, 0 }));
            Check(c.TrySetText(snapshot, "draft").Status == ClipboardChangeStatus.OwnershipChanged && n.EmptyCalls == 0, "Copy before lock acquisition must win.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var staged = c.TrySetText(snapshot, "draft");
            n.BeforeOpen = () => n.UserCopy((13, new byte[] { 67, 0, 0, 0 }));
            Check(c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.OwnershipChanged && n.EmptyCalls == 1, "New copy before restore lock must win.");
            Check(n.Owned.Count == 0, "Ownership change leaked untransferred restore handles.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var staged = c.TrySetText(snapshot, "draft"); n.FailOpen = true;
            for (int i = 0; i < 3; i++) Check(c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Unavailable, "Busy restore must report failure without hiding retries.");
            Check(n.OpenCount == 5 && n.EmptyCalls == 1 && n.Owned.Count == 0, "Restoration attempts must be bounded and nonmutating while locked.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var original = n.CopyContents(); n.FailAllocationAt = 5;
            Check(c.TrySetText(snapshot, "draft").Status == ClipboardChangeStatus.Unavailable && n.SameContents(original) && n.EmptyCalls == 0, "Rollback allocation failure must precede EmptyClipboard.");
            Check(n.Owned.Count == 0, "Partial preparation leaked native allocations.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var original = n.CopyContents(); n.FailSets.Add(1);
            Check(c.TrySetText(snapshot, "draft").Status == ClipboardChangeStatus.FailedRestored && n.SameContents(original), "Failed staging must restore every original format under the lock.");
            Check(n.Owned.Count == 0, "Failed staging/rollback leaked native allocations.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var original = n.CopyContents(); n.FailSets.UnionWith([1, 2]);
            var failed = c.TrySetText(snapshot, "draft");
            Check(failed.Status == ClipboardChangeStatus.FailedOwnedPartial && failed.Sequence == n.Sequence, "Partial rollback must report its owned mutated sequence.");
            Check(c.Restore(snapshot, failed.Sequence).Status == ClipboardChangeStatus.Restored && n.SameContents(original), "Partial rollback must remain recoverable.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var original = n.CopyContents(); var staged = c.TrySetText(snapshot, "draft"); n.FailSets.Add(5);
            var failed = c.Restore(snapshot, staged.Sequence);
            Check(failed.Status == ClipboardChangeStatus.FailedOwnedPartial && failed.Sequence != staged.Sequence, "Partial restoration must expose updated ownership.");
            Check(c.Restore(snapshot, failed.Sequence).Status == ClipboardChangeStatus.Restored && n.SameContents(original), "A later restore must replace all partially restored formats.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var staged = c.TrySetText(snapshot, "draft"); n.FailEmpty = true;
            Check(c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Unavailable && n.Owned.Count == 0, "Failed EmptyClipboard must retain current clipboard and release preparation.");
        }
        foreach (uint format in new uint[] { 3, 9, 14, 0x80, 0x201, 0xC999 })
        {
            var (n, c) = Create(); n.Contents[format] = [1];
            bool refused = false; try { using var snapshot = c.Capture(); } catch (ExternalException) { refused = true; }
            Check(refused && n.EmptyCalls == 0 && n.OpenCount == n.CloseCount, "Unsupported formats must refuse before any mutation.");
        }
        {
            var (n, c) = Create(); n.FailRead = true;
            bool refused = false; try { using var snapshot = c.Capture(); } catch (ExternalException) { refused = true; }
            Check(refused && n.EmptyCalls == 0 && n.OpenCount == n.CloseCount, "Read failure must not alter clipboard.");
        }
        {
            var (n, c) = Create(); n.OnRead = () => n.Contents[1] = [0];
            bool refused = false; try { using var snapshot = c.Capture(); } catch (ExternalException) { refused = true; }
            Check(refused && n.EmptyCalls == 0, "A delayed renderer adding formats must not produce a lossy snapshot.");
        }
        {
            var (n, c) = Create(); var snapshot = c.Capture(); var heldBytes = snapshot.Formats.Select(f => f.Bytes).ToArray(); snapshot.Dispose(); snapshot.Dispose();
            Check(heldBytes.All(b => b.All(value => value == 0)), "Disposed private clipboard snapshots must be cleared from managed buffers.");
            bool refused = false; try { c.TrySetText(snapshot, "draft"); } catch (InvalidOperationException) { refused = true; }
            Check(refused && n.EmptyCalls == 0, "Disposed snapshots must not be reused.");
        }
        {
            var (n, c) = Create(); n.UserCopy(); using var snapshot = c.Capture(); var staged = c.TrySetText(snapshot, "draft");
            Check(c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored && n.Contents.Count == 0, "Originally empty clipboard must be restored to empty.");
        }
        {
            var (n, c) = Create(); n.Contents[2] = [9, 8, 7]; n.Contents[8] = [9, 8, 7]; n.Contents[17] = [9, 8, 7]; var original = n.CopyContents();
            var snapshot = c.Capture(); Check(n.Owned.Count == 1, "Bitmap snapshot must own an independent GDI object.");
            var staged = c.TrySetText(snapshot, "draft");
            Check(c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored && n.SameContents(original), "Bitmap, DIB and DIBV5 must all survive restoration.");
            snapshot.Dispose(); Check(n.Owned.Count == 0 && n.BitmapFrees == 2, "Untransferred bitmap rollback and snapshot must each be deleted once.");
        }
        {
            var (n, c) = Create(); n.Contents[2] = [9, 8, 7]; n.FailBitmap = true;
            bool refused = false; try { using var snapshot = c.Capture(); } catch (ExternalException) { refused = true; }
            Check(refused && n.EmptyCalls == 0 && n.Owned.Count == 0, "Bitmap copy failure must preserve original clipboard.");
        }
        {
            var (n, c) = Create(); n.Contents[0xC004] = [65, 66, 0]; n.Contents[0xC005] = []; n.Markers.Add(0xC005); var original = n.CopyContents();
            using var snapshot = c.Capture(); var staged = c.TrySetText(snapshot, "draft");
            Check(c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored && n.SameContents(original) && n.Markers.SetEquals([0xC005u]), "Chromium HTML/source URL and presence-only Smart Paste marker must survive together.");
        }
        {
            var (n, c) = Create(); n.Contents[0xC002] = [1, 0, 0, 0]; n.Contents[0xC003] = [1, 0, 0, 0]; var original = n.CopyContents();
            using var snapshot = c.Capture(); var staged = c.TrySetText(snapshot, "draft");
            Check(c.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored && n.SameContents(original), "Original privacy flags must be restored byte-for-byte.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); n.FailRegistration = true;
            Check(c.TrySetText(snapshot, "draft").Status == ClipboardChangeStatus.Unavailable && n.EmptyCalls == 0, "Privacy-format registration failure must not mutate clipboard.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var original = n.CopyContents(); n.FailSets.Add(2);
            Check(c.TrySetText(snapshot, "draft").Status == ClipboardChangeStatus.FailedRestored && n.SameContents(original), "Privacy-format publish failure must remove all staged formats and restore original.");
        }
        {
            var (n, c) = Create(); using var snapshot = c.Capture(); var original = n.CopyContents(); n.FailSets.Add(2); n.FailEmptyAt = 2;
            var partial = c.TrySetText(snapshot, "draft");
            Check(partial.Status == ClipboardChangeStatus.FailedOwnedPartial, "Rollback clear failure must expose partial ownership.");
            Check(!n.Contents.ContainsKey(13), "Rejected privacy flags must prevent draft text from being published even if rollback fails.");
            Check(c.Restore(snapshot, partial.Sequence).Status == ClipboardChangeStatus.Restored && n.SameContents(original) && n.Owned.Count == 0, "Rollback clear failure must remain recoverable without leaked handles.");
        }
        Check(ClipboardNative.BoundBitmapSize(1920, 1080, 7680, 1, 32, OwnedClipboard.MaximumBytes) == 8298496, "Bitmap bound must include pixels and overhead.");
        foreach (var dimensions in new[] { (int.MaxValue, int.MaxValue, int.MaxValue, 32), (1, 1, 4, 0), (100000, 100000, 400000, 32) })
        {
            bool refused = false;
            try { ClipboardNative.BoundBitmapSize(dimensions.Item1, dimensions.Item2, dimensions.Item3, 1, dimensions.Item4, OwnedClipboard.MaximumBytes); }
            catch (ExternalException) { refused = true; }
            Check(refused, "Invalid or overflowing bitmap allocations must refuse before cloning.");
        }
        return checks;
    }

    private sealed class FakeNative : IClipboardNative
    {
        internal Dictionary<uint, byte[]> Contents = [];
        internal readonly Dictionary<nint, (uint Kind, byte[] Bytes)> Owned = [];
        internal readonly HashSet<int> FailSets = [];
        internal readonly HashSet<uint> Markers = [];
        internal readonly HashSet<uint> ReadFormats = [];
        internal bool FailOpen, FailEmpty, FailRead, FailBitmap, FailRegistration, SynthesizeOnClose, FailMaterialize;
        internal int FailAllocationAt, FailEmptyAt, Allocations, EmptyCalls, OpenCount, CloseCount, SetCalls, BitmapFrees;
        internal Action? BeforeOpen, OnRead;
        private bool opened;
        private int nextHandle;
        public uint Sequence { get; private set; } = 1;
        public uint TextLocale => 0x409;
        public bool MaterializeText()
        {
            RequireOpen(); if (FailMaterialize) return false;
            foreach (uint format in new uint[] { 1, 7 })
                if (!Contents.ContainsKey(format)) { Contents[format] = Contents[13].ToArray(); Sequence++; }
            return Contents.ContainsKey(16);
        }
        internal void Materialize() { RequireOpen(); Sequence++; }
        internal void UserCopy(params (uint Format, byte[] Bytes)[] entries)
        {
            if (opened) throw new InvalidOperationException("Another clipboard owner cannot copy while lock held.");
            Contents = entries.ToDictionary(e => e.Format, e => e.Bytes.ToArray()); Markers.Clear(); Sequence++;
        }
        internal Dictionary<uint, byte[]> CopyContents() => Contents.ToDictionary(p => p.Key, p => p.Value.ToArray());
        internal bool SameContents(Dictionary<uint, byte[]> expected) => Contents.Keys.SequenceEqual(expected.Keys) && expected.All(p => Contents[p.Key].SequenceEqual(p.Value));
        public bool Open(nint hwnd)
        {
            if (hwnd == 0 || opened) throw new InvalidOperationException("Invalid clipboard owner/lock.");
            OpenCount++; var before = BeforeOpen; BeforeOpen = null; before?.Invoke();
            if (FailOpen) return false; opened = true; return true;
        }
        public void Close()
        {
            RequireOpen();
            if (SynthesizeOnClose && Contents.ContainsKey(13))
                foreach (uint format in new uint[] { 16, 1, 7 })
                    if (!Contents.ContainsKey(format)) { Contents[format] = format == 16 ? BitConverter.GetBytes(0x409u) : Contents[13].ToArray(); Sequence++; }
            opened = false; CloseCount++;
        }
        public uint[] Formats(int maximum) { RequireOpen(); if (Contents.Count > maximum) throw new ExternalException(); return Contents.Keys.ToArray(); }
        public string FormatName(uint format) => format switch
        {
            0xC001 => "HTML Format", 0xC002 => "CanIncludeInClipboardHistory", 0xC003 => "CanUploadToCloudClipboard",
            0xC004 => "Chromium internal source URL", 0xC005 => "WebKit Smart Paste Format",
            0xC006 => "DataObject", 0xC007 => "Ole Private Data", _ => "unknown owner-specific format"
        };
        public uint RegisterFormat(string name) => FailRegistration ? 0u : name switch
        { "CanIncludeInClipboardHistory" => 0xC002u, "CanUploadToCloudClipboard" => 0xC003u, _ => throw new InvalidOperationException("Unexpected registration.") };
        public byte[] Read(uint format, int maximumBytes)
        {
            ReadFormats.Add(format);
            RequireOpen(); var action = OnRead; OnRead = null; action?.Invoke();
            if (FailRead || Contents[format].Length > maximumBytes || Contents[format].Length == 0 && !Markers.Contains(format)) throw new ExternalException();
            return Contents[format].ToArray();
        }
        public (nint Handle, int Bytes) ReadBitmap(int maximumBytes)
        {
            RequireOpen(); if (FailBitmap || Contents[2].Length > maximumBytes) throw new ExternalException();
            var handle = ++nextHandle; Owned[handle] = (2, Contents[2].ToArray()); return (handle, Contents[2].Length);
        }
        public nint CopyBitmap(nint handle, int maximumBytes)
        {
            if (FailBitmap || Owned[handle].Bytes.Length > maximumBytes) throw new ExternalException();
            var copy = ++nextHandle; Owned[copy] = (2, Owned[handle].Bytes.ToArray()); return copy;
        }
        public nint Allocate(byte[] bytes)
        {
            if (++Allocations == FailAllocationAt) return 0;
            var handle = ++nextHandle; Owned[handle] = (0, bytes.ToArray()); return handle;
        }
        public void Free(uint format, nint handle)
        {
            if (!Owned.Remove(handle, out var allocation) || (format == 2) != (allocation.Kind == 2)) throw new InvalidOperationException("Double free, transferred handle free, or wrong allocator.");
            if (format == 2) BitmapFrees++;
        }
        public bool Empty() { RequireOpen(); EmptyCalls++; if (FailEmpty || EmptyCalls == FailEmptyAt) return false; Contents.Clear(); Markers.Clear(); Sequence++; return true; }
        public bool SetMarker(uint format)
        {
            RequireOpen(); if (FailSets.Contains(++SetCalls)) return false;
            if (FormatName(format) != "WebKit Smart Paste Format") throw new InvalidOperationException("Unknown null marker.");
            Contents[format] = []; Markers.Add(format); Sequence++; return true;
        }
        public bool Set(uint format, nint handle)
        {
            RequireOpen(); if (FailSets.Contains(++SetCalls)) return false;
            if (!Owned.Remove(handle, out var value)) throw new InvalidOperationException("Invalid ownership transfer.");
            Contents[format] = value.Bytes.ToArray(); Sequence++; return true;
        }
        private void RequireOpen() { if (!opened) throw new InvalidOperationException("Clipboard operation without lock."); }
    }
}
