using System;
using System.Collections.Generic;
using System.Text;

namespace PuppetMasterGUI.exceptions
{
    [Serializable()]
    public class SendMappingsException : System.Exception
    {
        public SendMappingsException() : base() { }
        public SendMappingsException(string message) : base(message) { }
        public SendMappingsException(string message, System.Exception inner) : base(message, inner) { }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client.
        protected SendMappingsException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
