using System;
using Unity.Jobs;

namespace Atrufulgium.Voxel.Base {

    /// <remarks><inheritdoc cref="KeyedJobManagerBase{K, TInput, TResult, Self}"/></remarks>
    public abstract class KeyedJobManager<K, J1, J2, J3, J4, J5, J6, J7, J8, TInput, TResult>
        : KeyedJobManagerBase<K, TInput, TResult, KeyedJobManager<K, J1, J2, J3, J4, J5, J6, J7, J8, TInput, TResult>>, IDisposable
        where K : unmanaged, IEquatable<K>
        where J1 : struct, IJob
        where J2 : struct, IJob
        where J3 : struct, IJob
        where J4 : struct, IJob
        where J5 : struct, IJob
        where J6 : struct, IJob
        where J7 : struct, IJob
        where J8 : struct, IJob {

        J1 job1;
        J2 job2;
        J3 job3;
        J4 job4;
        J5 job5;
        J6 job6;
        J7 job7;
        J8 job8;

        public abstract void Setup(TInput input, out J1 job1, out J2 job2, out J3 job3, out J4 job4, out J5 job5, out J6 job6, out J7 job7, out J8 job8);
        public abstract void PostProcess(ref TResult result, in J1 job1, in J2 job2, in J3 job3, in J4 job4, in J5 job5, in J6 job6, in J7 job7, in J8 job8);

        protected override void SetupJobs(TInput input) {
            Setup(input, out job1, out job2, out job3, out job4, out job5, out job6, out job7, out job8);
            var handle1 = job1.Schedule();
            var handle2 = job2.Schedule(handle1);
            var handle3 = job3.Schedule(handle2);
            var handle4 = job4.Schedule(handle3);
            var handle5 = job5.Schedule(handle4);
            var handle6 = job6.Schedule(handle5);
            var handle7 = job7.Schedule(handle6);
            finalHandle = job8.Schedule(handle7);
        }

        protected override void PostProcessCaller(ref TResult result) {
            PostProcess(ref result, in job1, in job2, in job3, in job4, in job5, in job6, in job7, in job8);
        }
    }
}
