using Atrufulgium.Voxel.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;

namespace Atrufulgium.Voxel.Base {

    /// <summary>
    /// <para>
    /// A single instance of this represents jobs that that can be run with
    /// the result extracted. However, you will/should never instance this
    /// class.
    /// </para>
    /// <para>
    /// Instead, what's interesting, is the static signature which binds the
    /// jobs to some key for easy many-job processing. The functions are:
    /// <list type="bullet">
    /// <item><see cref="RunAsynchronously{TManager}(K, TInput)"/></item>
    /// <item><see cref="PollJobCompleted(K, ref TResult)"/></item>
    /// <item><see cref="GetAllCompletedJobs(int)"/></item>
    /// <item><see cref="JobExists(K)"/></item>
    /// </list>
    /// If you're impatient, there also exists
    /// <list type="bullet">
    /// <item><see cref="RunSynchronously{TManager}(TInput, ref TResult)"/></item>
    /// </list>
    /// but this is not recommended as it blocks the main thread instead of
    /// using one of the many worker threads.
    /// </para>
    /// </summary>
    /// <typeparam name="K"> The key identifying the jobs. </typeparam>
    /// <typeparam name="J1"> The first job. </typeparam>
    /// <typeparam name="J2"> The second job. </typeparam>
    /// <typeparam name="TInput"> The input of the jobs. </typeparam>
    /// <typeparam name="TResult"> The result of the jobs. </typeparam>
    public abstract class KeyedJobManager<K, J1, J2, TInput, TResult> : IDisposable
        where K : IEquatable<K>
        where J1 : struct, IJob
        where J2 : struct, IJob {

        // Whether this instance is already running jobs
        bool unavailable = false;
        JobHandle finalHandle;
        J1 job1;
        J2 job2;

        /// <summary>
        /// Whether this job is a newly created job, or re-used instead.
        /// When this job is re-used, do not overwrite the existing arrays with
        /// new references, at least not without disposing.
        /// </summary>
        protected bool Reused { get; private set; } = false;

        /// <summary>
        /// Your chance to write the relevant data into <paramref name="job1"/>
        /// and <paramref name="job2"/>.
        /// </summary>
        /// <remarks>
        /// There is no reason to ever call this manually.
        /// </remarks>
        public abstract void Setup(TInput input, out J1 job1, out J2 job2);
        /// <summary>
        /// Here you can read the output of the process into <paramref name="result"/>.
        /// Note that <paramref name="result"/> may be <tt>default</tt>.
        /// </summary>
        /// <remarks>
        /// There is no reason to ever call this manually.
        /// </remarks>
        public abstract void PostProcess(ref TResult result, in J1 job1, in J2 job2);

        /// <summary>
        /// <para>
        /// Starts running this job. The completion can be polled with
        /// <see cref="PollJobCompleted(ref TResult)"/>.
        /// </para>
        /// </summary>
        void RunAsynchronously(TInput input) {
            if (unavailable)
                throw new InvalidOperationException("Cannot use the same KeyedJob for multiple tasks. Use multiple instances. (Don't use this class in the first place, use the manager.)");
            Setup(input, out job1, out job2);
            var firstHandle = job1.Schedule();
            finalHandle = job2.Schedule(firstHandle);
            unavailable = true;
        }

        /// <summary>
        /// Polls whether the job has been completed. If so, it puts the
        /// result into <paramref name="result"/>.
        /// </summary>
        bool PollJobCompleted(ref TResult result) {
            if (!unavailable)
                throw new InvalidOperationException("Have not started any asynchronous meshing!");
            if (!finalHandle.IsCompleted) {
                return false;
            }
            // I don't know *why* a handle.IsCompleted job needs a Complete()
            // call, but it does, so here we are.
            finalHandle.Complete();

            PostProcess(ref result, in job1, in job2);

            finalHandle = default;
            unavailable = false;
            return true;
        }

        /// <summary>
        /// Runs this job, while blocking the thread that called this until
        /// complete.
        /// </summary>
        void RunSynchronously(TInput input, ref TResult result) {
            RunAsynchronously(input);
            finalHandle.Complete();
            PollJobCompleted(ref result);
        }

        // Jobs always do nativearray stuff so just leave this here to overwrite
        public virtual void Dispose() { }
        
        /// <summary>
        /// This class introduces static disposables. If you care, you can
        /// dispose them here. Usually you're using NativeCollections, and then
        /// this is kind of required at some point.
        /// </summary>
        public static void DisposeStatic() {
            if (activeJobbers.Count > 0) {
                Debug.LogWarning($"There are still {activeJobbers.Count} active jobs. Forcing them to finish before disposing them, but this might take a while!");

                foreach(var jobber in activeJobbers.Values) {
                    jobber.finalHandle.Complete();
                    jobber.Dispose();
                }
                activeJobbers.Clear();
            }
            while (idleJobbers.Count > 0) {
                idleJobbers.Pop().Dispose();
            }
        }

        static readonly Stack<KeyedJobManager<K, J1, J2, TInput, TResult>> idleJobbers = new();
        static readonly Dictionary<K, KeyedJobManager<K, J1, J2, TInput, TResult>> activeJobbers = new();

        /// <summary>
        /// Starts running a job, based on <paramref name="input"/>, associating
        /// it with key <paramref name="key"/>. Throws if that key is already
        /// associated with some other job.
        /// </summary>
        /// <remarks>
        /// In 99% of cases, you will run this with the generic argument the
        /// same as the class, like so:
        /// <code> ChunkMesher.RunAsynchronously&lt;ChunkMesher&gt;(..) </code>
        /// </remarks>
        public static void RunAsynchronously<TManager>(K key, TInput input) where TManager : KeyedJobManager<K, J1, J2, TInput, TResult>, new() {
            if (JobExists(key))
                throw new ArgumentException("The given key already has an asssociated job, so it cannot have a new one.");

            KeyedJobManager<K, J1, J2, TInput, TResult> keyedJob;
            if (idleJobbers.Count == 0) {
                keyedJob = new TManager();
            } else {
                keyedJob = idleJobbers.Pop();
                keyedJob.Reused = true;
            }
            keyedJob.RunAsynchronously(input);
            activeJobbers.Add(key, keyedJob);
        }

        /// <summary>
        /// Runs this job, while blocking the thread that called this until
        /// complete. As that is usually the main Unity thread, this method is
        /// a <b>bad idea</b> unless you're forced your hand.
        /// </summary>
        public static void RunSynchronously<TManager>(TInput input, ref TResult result) where TManager : KeyedJobManager<K, J1, J2, TInput, TResult>, new() {
            KeyedJobManager<K, J1, J2, TInput, TResult> keyedJob;
            if (idleJobbers.Count == 0) {
                keyedJob = new TManager();
            } else {
                keyedJob = idleJobbers.Pop();
                keyedJob.Reused = true;
            }
            keyedJob.RunSynchronously(input, ref result);
            idleJobbers.Push(keyedJob);
        }

        /// <summary>
        /// Polls whether a job associated to <paramref name="key"/> has been
        /// completed. If so, it puts the result into <paramref name="result"/>.
        /// </summary>
        public static bool PollJobCompleted(K key, ref TResult result) {
            if (!activeJobbers.TryGetValue(key, out var jobber))
                throw new ArgumentException($"There is no meshing job with ID {key}", nameof(key));

            if (jobber.PollJobCompleted(ref result)) {
                activeJobbers.Remove(key);
                idleJobbers.Push(jobber);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Iterates through all jobs that have been finished and returns the
        /// keys. You can further handle the results of this with
        /// <see cref="PollJobCompleted(K, ref TResult)"/>.
        /// </summary>
        public static IEnumerable<K> GetAllCompletedJobs(int maxCompletions = int.MaxValue) {
            int completed = 0;
            foreach((var key, var jobber) in Enumerators.EnumerateCopy(activeJobbers)) {
                if (jobber.finalHandle.IsCompleted) {
                    yield return key;
                    completed++;
                    if (completed >= maxCompletions)
                        yield break;
                }
            }
        }

        /// <summary>
        /// Whether a given key already has an associated job.
        /// </summary>
        public static bool JobExists(K key)
            => activeJobbers.ContainsKey(key);
    }
}
