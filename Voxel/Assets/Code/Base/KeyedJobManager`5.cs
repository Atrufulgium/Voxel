using System;
using Unity.Jobs;

namespace Atrufulgium.Voxel.Base {

    /// <remarks><inheritdoc cref="KeyedJobManagerBase{K, TInput, TResult, Self}"/></remarks>
    public abstract class KeyedJobManager<K, J1, J2, TInput, TResult>
        : KeyedJobManagerBase<K, TInput, TResult, KeyedJobManager<K, J1, J2, TInput, TResult>>, IDisposable
        where K : unmanaged, IEquatable<K>
        where J1 : struct, IJob
        where J2 : struct, IJob {

        J1 job1;
        J2 job2;

        public abstract void Setup(TInput input, out J1 job1, out J2 job2);
        public abstract void PostProcess(ref TResult result, in J1 job1, in J2 job2);

        protected override void SetupJobs(TInput input) {
            Setup(input, out job1, out job2);
            var handle1 = job1.Schedule();
            finalHandle = job2.Schedule(handle1);
        }

        protected override void PostProcessCaller(ref TResult result) {
            PostProcess(ref result, in job1, in job2);
        }
    }
}
