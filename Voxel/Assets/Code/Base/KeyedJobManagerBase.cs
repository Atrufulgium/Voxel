using Atrufulgium.Voxel.Collections;
using System;
using System.Collections.Generic;
using Unity.Jobs;

namespace Atrufulgium.Voxel.Base {
    /// <summary>
    /// <para>
    /// Represents the shared content of
    /// <list type="bullet">
    /// <item><see cref="KeyedJobManager{K, J1, TInput, TResult}"/></item>
    /// <item><see cref="KeyedJobManager{K, J1, J2, TInput, TResult}"/></item>
    /// <item>etc.</item>
    /// </list>
    /// and these should be the only classes inheriting from this class.
    /// </para>
    /// <para>
    /// These <tt>KeyedJobManager</tt>s represent managers that can run one /
    /// a chain of Unity <see cref="IJob"/>s that are identified by some key,
    /// in the order of the generic arguments.
    /// </para>
    /// <para>
    /// The only non-shared content are the methods
    /// <code>
    ///     Setup(TInput input, out (multiple jobs));
    ///     PostProcess(ref TResult result, in (multiple jobs));
    /// </code>
    /// which are called in <see cref="SetupJobs(TInput)"/> and
    /// <see cref="PostProcessCaller(ref TResult)"/> respectively.
    /// </para>
    /// </summary>
    /// <typeparam name="K"> The key identifying the jobs. </typeparam>
    /// <typeparam name="TInput"> The input of the jobs. For multiple, use a tuple. </typeparam>
    /// <typeparam name="TResult"> The result of the jobs. For multiple, use a tuple. </typeparam>
    /// <typeparam name="Self"> The type you're defining. </typeparam>
    /// <remarks>
    /// <para>
    /// For all derived classes you will not be working with instances
    /// directly. These simply represent jobs that the static methods will be
    /// working with. So do not <tt>new()</tt> any derived classes yourself.
    /// (Also, implicitely, do not call any instance methods yourself.)
    /// </para>
    /// <para>
    /// Instead, what is interesting is the static signature that binds some
    /// jobs to some key for easy many-job processing, and also caching.
    /// These functions are:
    /// <list type="bullet">
    /// <item><see cref="RunAsynchronously{TManager}(K, TInput)"/></item>
    /// <item><see cref="TryComplete(K, ref TResult)"/></item>
    /// <item><see cref="GetAllCompletedJobs(int)"/></item>
    /// <item><see cref="JobExists(K)"/></item>
    /// </list>
    /// If you're impatient, there also exists
    /// <list type="bullet">
    /// <item><see cref="RunSynchronously{TManager}(TInput, ref TResult)"/></item>
    /// </list>
    /// but this is not recommended as it blocks the main thread instead of
    /// using one of the many worker threads. <br/>
    /// Finally, there is also
    /// <list type="bullet">
    /// <item><see cref="DisposeStatic"/></item>
    /// </list>
    /// to force-finish all jobs and clean up all data.
    /// </para>
    /// </remarks>
    // The KeyedJobManager<>s should inheritdoc just the remarks.
    // I'm not doing `J[]` instead of the thousand `J1, J2, ..` generic
    // overloads because unity doesn't like actually working with the IJob
    // interface and it's bad for performance due to boxing anyawy.
    public abstract class KeyedJobManagerBase<K, TInput, TResult, Self> : IDisposable
        where K : IEquatable<K>
        where Self : KeyedJobManagerBase<K, TInput, TResult, Self> {

        /// <summary>
        /// The final handle in the chain of jobs.
        /// </summary>
        protected JobHandle finalHandle;

        /// <summary>
        /// Whether this instance is already running jobs.
        /// </summary>
        /// <remarks>
        /// Do not access this in non-abstract child classes.
        /// </remarks>
        protected bool unavailable = false;

        /// <summary>
        /// <para>
        /// Starts running this job. The completion can be polled with
        /// <see cref="PostProcessCaller(ref TResult)"/>.
        /// </para>
        /// </summary>
        /// <remarks>
        /// All implementations of this look alike. For a representive example,
        /// see <see cref="KeyedJobManager{K, J1, J2, TInput, TResult}.SetupJobs(TInput)"/>.
        /// </remarks>
        protected abstract void SetupJobs(TInput input);

        /// <summary>
        /// Wrapper method for <see cref="SetupJobs(TInput)"/>.
        /// </summary>
        void RunAsynchronously(TInput input) {
            if (unavailable)
                throw new InvalidOperationException("Cannot use the same KeyedJob for multiple tasks. Use multiple instances. (Don't use this class in the first place, use the manager.)");
            SetupJobs(input);
            unavailable = true;
        }

        /// <remarks>
        /// All implementations of this look alike. For a representive example,
        /// see <see cref="KeyedJobManager{K, J1, J2, TInput, TResult}.PostProcessCaller(ref TResult)"/>.
        /// </remarks>
        protected abstract void PostProcessCaller(ref TResult result);

        /// <summary>
        /// <para>
        /// Polls whether the job has been completed. If so, it puts the
        /// result into <paramref name="result"/>. This is a <tt>ref</tt>
        /// param so you can safe on garbage.
        /// </para>
        /// <para>
        /// When this returns false, <paramref name="result"/> should remain
        /// unaffected.
        /// </para>
        /// </summary>
        bool PollJobCompleted(ref TResult result) {
            if (!unavailable)
                throw new InvalidOperationException("Have not started any jobs to complete!");
            if (!finalHandle.IsCompleted) {
                return false;
            }
            // I don't know *why* a handle.IsCompleted job needs a Complete()
            // call, but it does, so here we are.
            finalHandle.Complete();

            PostProcessCaller(ref result);

            finalHandle = default;
            unavailable = false;
            return true;
        }

        /// <summary>
        /// Runs this job, while blocking the thread that called this until
        /// complete.
        /// </summary>
        protected void RunSynchronously(TInput input, ref TResult result) {
            RunAsynchronously(input);
            finalHandle.Complete();
            PostProcessCaller(ref result);
        }

        /// <summary>
        /// Whether this job is a newly created job, or re-used instead.
        /// When this job is re-used, do not overwrite the existing arrays with
        /// new references, or at least not without disposing.
        /// </summary>
        protected bool Reused { get; private set; } = false;

        // Jobs always do nativearray stuff so just leave this here to overwrite
        public virtual void Dispose() { }

        protected static readonly Stack<Self> idleJobbers = new();
        protected static readonly Dictionary<K, Self> activeJobbers = new();

        /// <summary>
        /// This class introduces static disposables. If you care, you can
        /// dispose them here. Usually with jobs you're using
        /// NativeCollections, and then this is kind of required eventually.
        /// </summary>
        public static void DisposeStatic() {
            if (activeJobbers.Count > 0) {
                UnityEngine.Debug.LogWarning($"There are still {activeJobbers.Count} active jobs. Forcing them to finish before disposing them, but this might take a while!");

                foreach (var jobber in activeJobbers.Values) {
                    jobber.finalHandle.Complete();
                    jobber.Dispose();
                }
                activeJobbers.Clear();
            }
            while (idleJobbers.Count > 0) {
                idleJobbers.Pop().Dispose();
            }
        }


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
        public static void RunAsynchronously<TManager>(K key, TInput input) where TManager : Self, new() {
            if (JobExists(key))
                throw new ArgumentException("The given key already has an asssociated job, so it cannot have a new one.");

            Self keyedJob;
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
        public static void RunSynchronously<TManager>(TInput input, ref TResult result) where TManager : Self, new() {
            Self keyedJob;
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
        /// <para>
        /// Polls whether a job associated to <paramref name="key"/> has been
        /// completed. If so, it puts the result into <paramref name="result"/>.
        /// If note, <paramref name="result"/> remains unaffected.
        /// </para>
        /// <para>
        /// After this, the associated job gets moved from "completed" state to
        /// "idle" state, and calling this again with the same key is an error.
        /// </para>
        /// </summary>
        public static bool TryComplete(K key, ref TResult result) {
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
        /// <see cref="TryComplete(K, ref TResult)"/>.
        /// </summary>
        /// <remarks>
        /// This enumerates a copy of the active collection, so it is safe to
        /// try and complete the operation.
        /// </remarks>
        public static IEnumerable<K> GetAllCompletedJobs(int maxCompletions = int.MaxValue) {
            int completed = 0;
            foreach ((var key, var jobber) in Enumerators.EnumerateCopy(activeJobbers)) {
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