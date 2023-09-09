using System;
using Unity.Jobs;

namespace Atrufulgium.Voxel.Base {

    /// <remarks><inheritdoc cref="KeyedJobManagerBase{K, TInput, TResult, Self}"/></remarks>
    public abstract class KeyedJobManager<K, J1, J2, J3, TInput, TResult>
        : KeyedJobManagerBase<K, TInput, TResult, KeyedJobManager<K, J1, J2, J3, TInput, TResult>>, IDisposable
        where K : IEquatable<K>
        where J1 : struct, IJob
        where J2 : struct, IJob
        where J3 : struct, IJob {

        J1 job1;
        J2 job2;
        J3 job3;

        public abstract void Setup(TInput input, out J1 job1, out J2 job2, out J3 job3);
        public abstract void PostProcess(ref TResult result, in J1 job1, in J2 job2, in J3 job3);

        protected override void SetupJobs(TInput input) {
            Setup(input, out job1, out job2, out job3);
            var handle1 = job1.Schedule();
            var handle2 = job2.Schedule(handle1);
            finalHandle = job3.Schedule(handle2);
        }

        protected override void PostProcessCaller(ref TResult result) {
            PostProcess(ref result, in job1, in job2, in job3);
        }
    }
}
