using System;
using System.Collections.Generic;
using System.Text;

namespace PuppetMasterGUI.exceptions
{
    [Serializable()]
    public class PCSNotOKException : System.Exception
    {
        public PCSNotOKException() : base() { }
        public PCSNotOKException(string message) : base(message) { }
        public PCSNotOKException(string message, System.Exception inner) : base(message, inner) { }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client.
        protected PCSNotOKException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
