using System;

namespace LibHac
{
    public class HorizonResultException : LibHacException
    {
        /// <summary>
        /// The result code of the error.
        /// </summary>
        public Result ResultValue { get; }

        /// <summary>
        /// The original, internal result code if it was converted to a more general external result code.
        /// </summary>
        public Result InternalResultValue { get; }

        public string InnerMessage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HorizonResultException"/> class. 
        /// </summary>
        /// <param name="result">The result code for the reason for the exception.</param>
        public HorizonResultException(Result result)
        {
            InternalResultValue = result;
            ResultValue = result;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HorizonResultException"/> class with a specified error message.
        /// </summary>
        /// <param name="result">The result code for the reason for the exception.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public HorizonResultException(Result result, string message)
        {
            InternalResultValue = result;
            ResultValue = result;
            InnerMessage = message;
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref="HorizonResultException"/> class with a specified error message
        ///  and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="result">The result code for the reason for the exception.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public HorizonResultException(Result result, string message, Exception innerException)
            : base(string.Empty, innerException)
        {
            InternalResultValue = result;
            ResultValue = result;
            InnerMessage = message;
        }

        public override string Message
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(InnerMessage))
                {
                    return $"{ResultValue.ToStringWithName()}: {InnerMessage}";
                }

                return ResultValue.ToStringWithName();
            }
        }
    }
}
