// Copyright (c) 2014 APX Labs, Inc.
// License: AS IS, WITHOUT WARRANTY OF ANY KIND
// 
// Many thanks to Jonathan Pryor from Xamarin for his assistance with this solution

using System.Runtime.InteropServices;
using Android.Runtime;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Collections;

namespace cameraandroid
{
    /// <summary>
    /// A wrapper around a Java array that reads elements directly from the pointer instead of through
    /// expensive JNI calls.  For simplicity, the array is not modifiable.
    /// </summary>
    public sealed class FastJavaByteArray : Java.Lang.Object, IList<byte>
    {
        #region Constructors

        public FastJavaByteArray(int length, bool readOnly = true)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException();

            IsReadOnly = readOnly;

            IntPtr arrayHandle = JniEnvEx.NewByteArray(length);
            if (arrayHandle == IntPtr.Zero)
                throw new OutOfMemoryException();

            SetHandle(arrayHandle, JniHandleOwnership.DoNotTransfer);
            Count = length;
            bool isCopy = false;
            unsafe
            {
                Raw = JniEnvEx.GetByteArrayElements(arrayHandle, ref isCopy);
            }
        }

        public FastJavaByteArray(IntPtr handle, bool readOnly = true) : base(handle, JniHandleOwnership.DoNotTransfer)
        {
            if (handle == IntPtr.Zero)
                throw new ArgumentNullException("handle");

            IsReadOnly = readOnly;

            Count = JNIEnv.GetArrayLength(handle);
            bool isCopy = false;
            unsafe
            {
                Raw = JniEnvEx.GetByteArrayElements(handle, ref isCopy);
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            unsafe
            {
                // tell Java that we're done with this array
                JniEnvEx.ReleaseByteArrayElements(Handle, Raw, IsReadOnly ? PrimitiveArrayReleaseMode.Release : PrimitiveArrayReleaseMode.CommitAndRelease);
            }
            base.Dispose(disposing);
        }

        #region IList<byte> Properties

        public int Count { get; private set; }

        public bool IsReadOnly
        {
            get;
            private set;
        }

        public byte this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException();
                }
                byte retval;
                unsafe
                {
                    retval = Raw[index];
                }
                return retval;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw new NotSupportedException("This FastJavaByteArray is read-only");
                }

                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException();
                }
                unsafe
                {
                    Raw[index] = value;
                }
            }
        }

        #endregion

        #region IList<byte> Methods

        public void Add(byte item)
        {
            throw new NotSupportedException("FastJavaByteArray is fixed length");
        }

        public void Clear()
        {
            throw new NotSupportedException("FastJavaByteArray is fixed length");
        }

        public bool Contains(byte item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(byte[] array, int arrayIndex)
        {
            unsafe
            {
                Marshal.Copy(new IntPtr(Raw), array, arrayIndex, Math.Min(Count, array.Length - arrayIndex));
            }
        }

        [DebuggerHidden]
        public IEnumerator<byte> GetEnumerator()
        {
            return new FastJavaByteArrayEnumerator(this);
        }

        [DebuggerHidden]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new FastJavaByteArrayEnumerator(this);
        }

        public int IndexOf(byte item)
        {
            for (int i = 0; i < Count; ++i)
            {
                byte current;
                unsafe
                {
                    current = Raw[i];
                }
                if (current == item)
                    return i;
            }
            return -1;
        }

        public void Insert(int index, byte item)
        {
            throw new NotSupportedException("FastJavaByteArray is fixed length");
        }

        public bool Remove(byte item)
        {
            throw new NotSupportedException("FastJavaByteArray is fixed length");
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException("FastJavaByteArray is fixed length");
        }

        #endregion

        #region Public Properties

        public unsafe byte* Raw { get; private set; }

        #endregion
    }
}