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
        public Result ResultValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HorizonResultException"/> class. 
        /// </summary>
        /// <param name="result">The result code for the reason for the exception.</param>
        public HorizonResultException(Result result)
        {
            ResultValue = result;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HorizonResultException"/> class with a specified error message.
        /// </summary>
        /// <param name="result">The result code for the reason for the exception.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public HorizonResultException(Result result, string message)
            : base(message)
        {
            ResultValue = result;
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref="HorizonResultException"/> class with a specified error message
        ///  and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="result">The result code for the reason for the exception.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public HorizonResultException(Result result, string message, Exception innerException)
            : base(message, innerException)
        {
            ResultValue = result;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MissingKeyException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/>  that contains contextual information about the source or destination.</param>
        protected HorizonResultException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ResultValue = (Result)info.GetValue(nameof(ResultValue), ResultValue.GetType());
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ResultValue), ResultValue);
        }

        public override string Message
        {
            get
            {
                string baseMessage = base.Message;

                if (!string.IsNullOrWhiteSpace(baseMessage))
                {
                    return $"{ResultValue.ErrorCode}: {baseMessage}";
                }

                return ResultValue.ErrorCode;
            }
        }
    }
}
