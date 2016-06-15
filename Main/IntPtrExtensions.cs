using System;
using System.Runtime.InteropServices;

namespace Main
{
    internal static class IntPtrExtensions
    {
        public static T To<T>(this IntPtr pointer)
        {
            return (T) GCHandle.FromIntPtr(pointer).Target;
        }

        public static bool IsZero(this IntPtr pointer)
        {
            return pointer.Equals(IntPtr.Zero);
        }

        public static T ToStruct<T>(this IntPtr pointer)
        {
            return (T) Marshal.PtrToStructure(pointer, typeof (T));
        }

        public static IntPtr Allocate<T>(T @object)
        {
            var handle = GCHandle.Alloc(@object);
            return GCHandle.ToIntPtr(handle);
        }

        public static void Deallocate(this IntPtr pointer)
        {
            if (IsZero(pointer))
            {
                return;
            }

            var handle = GCHandle.FromIntPtr(pointer);
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }

        public static IntPtr AllocateGlobal<T>()
        {
            return Marshal.AllocHGlobal(Marshal.SizeOf(typeof (T)));
        }

        public static void DeallocateGlobal(this IntPtr pointer)
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
