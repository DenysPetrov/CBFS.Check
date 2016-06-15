using System;
using Microsoft.Win32.SafeHandles;

namespace Main
{
    public class AlternateDataStreamContext
    {
        public AlternateDataStreamContext(SafeFileHandle Value)
        {
            hFile = Value;
            Context = IntPtr.Zero;
        }
        public SafeFileHandle hFile;
        public IntPtr Context;
    }
}
