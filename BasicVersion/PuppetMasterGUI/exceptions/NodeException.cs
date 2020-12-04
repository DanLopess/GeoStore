using System;
using System.Collections.Generic;
using System.Text;

namespace PuppetMasterGUI.exceptions
{
    [Serializable()]
    public class NodeException : System.Exception
    {
        public NodeException() : base() { }
        public NodeException(string message) : base(message) { }
        public NodeException(string message, System.Exception inner) : base(message, inner) { }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client.
        protected NodeException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
