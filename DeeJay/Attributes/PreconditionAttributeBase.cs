using Discord.Interactions;

namespace DeeJay.Attributes
{
    /// <summary>
    ///    Base class for all precondition attributes
    /// </summary>
    public abstract class PreconditionAttributeBase : PreconditionAttribute
    {
        private static readonly PreconditionResult SUCCESS_RESULT = PreconditionResult.FromSuccess();
        private static readonly Task<PreconditionResult> SUCCESS = Task.FromResult(SUCCESS_RESULT);

        /// <summary>
        ///   Returns a successful precondition result.
        /// </summary>
        /// <returns></returns>
        protected static PreconditionResult SuccessResult() => SUCCESS_RESULT;
        
        /// <summary>
        ///     Returns a successful precondition result.
        /// </summary>
        protected static Task<PreconditionResult> Success() => SUCCESS;
        
        /// <summary>
        ///    Returns a failed precondition result with the specified reason.
        /// </summary>
        /// <param name="reason">The reason for the failure</param>
        /// <returns></returns>
        protected static PreconditionResult FailureResult(string reason) => PreconditionResult.FromError(reason);
        
        /// <summary>
        ///    Returns a failed precondition result with the specified reason.
        /// </summary>
        /// <param name="reason">The reason for the failure</param>
        protected static Task<PreconditionResult> Failure(string reason) => Task.FromResult(PreconditionResult.FromError(reason));
    }
}