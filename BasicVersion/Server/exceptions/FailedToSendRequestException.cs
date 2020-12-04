using System;
using System.Collections.Generic;
using System.Text;

namespace MainServer.exceptions
{
    [Serializable()]
    public class FailedToSendRequestException : System.Exception
    {
        public FailedToSendRequestException() : base() { }
        public FailedToSendRequestException(string message) : base(message) { }
        public FailedToSendRequestException(string message, System.Exception inner) : base(message, inner) { }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client.
        protected FailedToSendRequestException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
