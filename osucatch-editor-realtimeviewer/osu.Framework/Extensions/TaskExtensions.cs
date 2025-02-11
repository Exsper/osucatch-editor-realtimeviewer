// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Extensions
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Safe alternative to Task.Result which ensures the calling thread is not a thread pool thread.
        /// </summary>
        public static T GetResultSafely<T>(this Task<T> task)
        {
            // We commonly access `.Result` from within `ContinueWith`, which is a safe usage (the task is guaranteed to be completed).
            // Unfortunately, the only way to allow these usages is to check whether the task is completed or not here.
            // This does mean that there could be edge cases where this safety is skipped (ie. if the majority of executions complete
            // immediately).
            if (!task.IsCompleted && !isWaitingValid(task))
                throw new InvalidOperationException($"Can't use {nameof(GetResultSafely)} from inside an async operation.");

            return task.Result;
        }

        private static bool isWaitingValid(Task task)
        {
            // In the case the task has been started with the LongRunning flag, it will not be in the TPL thread pool and we can allow waiting regardless.
            if (task.CreationOptions.HasFlag(TaskCreationOptions.LongRunning))
                return true;

            // Otherwise only allow waiting from a non-TPL thread pool thread.
            return !Thread.CurrentThread.IsThreadPoolThread;
        }
    }
}
