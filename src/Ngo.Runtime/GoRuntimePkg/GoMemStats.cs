using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    [GoType("struct", Name = "MemStats", Package = "runtime")]
    public class GoMemStats
    {
        [GoField(Name = "Alloc")]
        public long Alloc;

        [GoField(Name = "TotalAlloc")]
        public long TotalAlloc;

        [GoField(Name = "Sys")]
        public long Sys;

        [GoField(Name = "Lookups")]
        public long Lookups;

        [GoField(Name = "Mallocs")]
        public long Mallocs;

        [GoField(Name = "Frees")]
        public long Frees;

        [GoField(Name = "HeapAlloc")]
        public long HeapAlloc;

        [GoField(Name = "HeapSys")]
        public long HeapSys;

        [GoField(Name = "HeapIdle")]
        public long HeapIdle;

        [GoField(Name = "HeapInuse")]
        public long HeapInuse;

        [GoField(Name = "HeapReleased")]
        public long HeapReleased;

        [GoField(Name = "HeapObjects")]
        public long HeapObjects;

        [GoField(Name = "NumGC")]
        public long NumGC;

        [GoField(Name = "PauseNs")]
        public Slice<long> PauseNs;

        [GoField(Name = "PauseTotalNs")]
        public long PauseTotalNs;

        [GoField(Name = "GCSys")]
        public long GCSys;

        [GoField(Name = "StackInuse")]
        public long StackInuse;

        [GoField(Name = "StackSys")]
        public long StackSys;

        [GoField(Name = "MSpanInuse")]
        public long MSpanInuse;

        [GoField(Name = "MSpanSys")]
        public long MSpanSys;

        [GoField(Name = "MCacheInuse")]
        public long MCacheInuse;

        [GoField(Name = "MCacheSys")]
        public long MCacheSys;

        [GoField(Name = "BuckHashSys")]
        public long BuckHashSys;

        [GoField(Name = "OtherSys")]
        public long OtherSys;

        [GoField(Name = "NextGC")]
        public long NextGC;

        [GoField(Name = "LastGC")]
        public long LastGC;

        [GoField(Name = "PauseEnd", Type = "[256]uint64")]
        public Slice<long> PauseEnd;

        [GoField(Name = "NumForcedGC")]
        public long NumForcedGC;

        [GoField(Name = "GCCPUFraction")]
        public double GCCPUFraction;

        [GoField(Name = "DebugGC")]
        public bool DebugGC;

        [GoField(Name = "EnableGC")]
        public bool EnableGC;

        public GoMemStats()
        {
            PauseNs = new Slice<long>(new long[256]);
            PauseEnd = new Slice<long>(new long[256]);
        }
    }
}
