using Discord.Interactions;

namespace DeeJay.Attributes
{
    /// <summary>
    ///    Base class for all precondition attributes
    /// </summary>
    public abstract class PreconditionAttributeBase : PreconditionAttribute
    {
        private static readonly Task<PreconditionResult> SUCCESS = Task.FromResult(PreconditionResult.FromSuccess());
        
        /// <summary>
        ///     Returns a successful precondition result.
        /// </summary>
        protected Task<PreconditionResult> Success() => SUCCESS;
        /// <summary>
        ///    Returns a failed precondition result with the specified reason.
        /// </summary>
        /// <param name="reason">The reason for the failure</param>
        protected Task<PreconditionResult> Failure(string reason) => Task.FromResult(PreconditionResult.FromError(reason));
    }
}