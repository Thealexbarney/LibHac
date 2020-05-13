using System;
using System.Runtime.Serialization;

namespace LibHac
{
    [Serializable]
    public class HorizonResultException : LibHacException, ISerializable
    {
        /// <summary>
        /// The result code of the error.
        /// </summary>
        public Result ResultValue { get; set; }

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

        /// <summary>
        /// Initializes a new instance of the <see cref="MissingKeyException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/>  that contains contextual information about the source or destination.</param>
        protected HorizonResultException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            InternalResultValue = (Result)(info.GetValue(nameof(InternalResultValue), InternalResultValue.GetType()) ?? default(Result));
            ResultValue = (Result)(info.GetValue(nameof(ResultValue), ResultValue.GetType()) ?? default(Result));
            InnerMessage = (string)info.GetValue(nameof(InnerMessage), InnerMessage.GetType());
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(InternalResultValue), InternalResultValue);
            info.AddValue(nameof(ResultValue), ResultValue);
            info.AddValue(nameof(InnerMessage), InnerMessage);
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
