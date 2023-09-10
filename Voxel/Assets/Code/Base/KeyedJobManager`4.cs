using System;
using Unity.Jobs;

namespace Atrufulgium.Voxel.Base {

    /// <remarks><inheritdoc cref="KeyedJobManagerBase{K, TInput, TResult, Self}"/></remarks>
    public abstract class KeyedJobManager<K, J1, TInput, TResult>
        : KeyedJobManagerBase<K, TInput, TResult, KeyedJobManager<K, J1, TInput, TResult>>, IDisposable
        where K : unmanaged, IEquatable<K>
        where J1 : struct, IJob {

        J1 job1;

        public abstract void Setup(TInput input, out J1 job1);
        public abstract void PostProcess(ref TResult result, in J1 job1);

        protected override void SetupJobs(TInput input) {
            Setup(input, out job1);
            finalHandle = job1.Schedule();
        }

        protected override void PostProcessCaller(ref TResult result) {
            PostProcess(ref result, in job1);
        }
    }
}
