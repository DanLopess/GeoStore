using System;
using System.Collections.Generic;
using System.Text;

namespace PuppetMasterGUI.exceptions
{
    [Serializable()]
    public class InvalidCommandException : System.Exception
    {
        public InvalidCommandException() : base() { }
        public InvalidCommandException(string message) : base(message) { }
        public InvalidCommandException(string message, System.Exception inner) : base(message, inner) { }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client.
        protected InvalidCommandException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
