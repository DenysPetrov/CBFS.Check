using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Main
{
    public class FileStreamContext
    {
        public FileStreamContext()
        {
            OpenCount = 0;
            Stream = null;
        }
        public void IncrementCounter()
        {
            ++OpenCount;
        }
        public void DecrementCounter()
        {
            --OpenCount;
        }
        public int OpenCount { get; set; }
        public FileStream Stream { get; set; }
    }
}
