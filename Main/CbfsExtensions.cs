﻿using System;
using CallbackFS;

namespace Main
{
    public static class CbfsExtensions
    {
        public static void DeallocateUserContext(this CbFsFileInfo sender)
        {
        // dsfs65r567yy665b5g6
            sender.UserContext.Deallocate();
            sender.UserContext = IntPtr.Zero;
        }

        public static void DeallocateUserContext(this CbFsDirectoryEnumerationInfo sender)
        {
            sender.UserContext.Deallocate();
            sender.UserContext = IntPtr.Zero;
        }
    }
}
