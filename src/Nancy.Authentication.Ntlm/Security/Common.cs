﻿namespace Nancy.Authentication.Ntlm.Security
{
    using System;
    using System.Runtime.InteropServices;

    public class Common
    {
        #region Private constants
        private const int ISC_REQ_REPLAY_DETECT = 0x00000004;
        private const int ISC_REQ_SEQUENCE_DETECT = 0x00000008;
        private const int ISC_REQ_CONFIDENTIALITY = 0x00000010;
        private const int ISC_REQ_CONNECTION = 0x00000800;
        #endregion

        internal static uint NewContextAttributes = 0;
        internal static Common.SecurityInteger NewLifeTime = new SecurityInteger(0);

        #region Public constants
        public const int StandardContextAttributes = ISC_REQ_CONFIDENTIALITY | ISC_REQ_REPLAY_DETECT | ISC_REQ_SEQUENCE_DETECT | ISC_REQ_CONNECTION;
        public const int SecurityNativeDataRepresentation = 0x10;
        public const int MaximumTokenSize = 12288;
        public const int SecurityCredentialsInbound = 1;
        public const int SuccessfulResult = 0;
        public const int IntermediateResult = 0x90312;
        #endregion

        #region Public enumerations
        public enum SecurityBufferType
        {
            SECBUFFER_VERSION = 0,
            SECBUFFER_EMPTY = 0,
            SECBUFFER_DATA = 1,
            SECBUFFER_TOKEN = 2
        }

        [Flags]
        public enum NtlmFlags : int
        {
            // The client sets this flag to indicate that it supports Unicode strings.
            NegotiateUnicode = 0x00000001,
            // This is set to indicate that the client supports OEM strings.
            NegotiateOem = 0x00000002,
            // This requests that the server send the authentication target with the Type 2 reply.
            RequestTarget = 0x00000004,
            // Indicates that NTLM authentication is supported.
            NegotiateNtlm = 0x00000200,
            // When set, the client will send with the message the name of the domain in which the workstation has membership.
            NegotiateDomainSupplied = 0x00001000,
            // Indicates that the client is sending its workstation name with the message.  
            NegotiateWorkstationSupplied = 0x00002000,
            // Indicates that communication between the client and server after authentication should carry a "dummy" signature.
            NegotiateAlwaysSign = 0x00008000,
            // Indicates that this client supports the NTLM2 signing and sealing scheme; if negotiated, this can also affect the response calculations.
            NegotiateNtlm2Key = 0x00080000,
            // Indicates that this client supports strong (128-bit) encryption.
            Negotiate128 = 0x20000000,
            // Indicates that this client supports medium (56-bit) encryption.
            Negotiate56 = (unchecked((int)0x80000000))
        }

        public enum NtlmAuthLevel
        {
            /* Use LM and NTLM, never use NTLMv2 session security. */
            LM_and_NTLM,

            /* Use NTLMv2 session security if the server supports it,
             * otherwise fall back to LM and NTLM. */
            LM_and_NTLM_and_try_NTLMv2_Session,

            /* Use NTLMv2 session security if the server supports it,
             * otherwise fall back to NTLM.  Never use LM. */
            NTLM_only,

            /* Use NTLMv2 only. */
            NTLMv2_only,
        }
        #endregion

        #region Public structures
        [StructLayout(LayoutKind.Sequential)]
        public struct SecurityHandle
        {
            public IntPtr LowPart;
            public IntPtr HighPart;

            public SecurityHandle(int dummy)
            {
                LowPart = HighPart = IntPtr.Zero;
            }

            /// <summary>
            /// Resets all internal pointers to default value
            /// </summary>
            public void Reset()
            {
                LowPart = HighPart = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SecurityInteger
        {
            public uint LowPart;
            public int HighPart;
            public SecurityInteger(int dummy)
            {
                LowPart = 0;
                HighPart = 0;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SecurityBuffer : IDisposable
        {
            public int cbBuffer;
            public int cbBufferType;
            public IntPtr pvBuffer;

            public SecurityBuffer(int bufferSize)
            {
                cbBuffer = bufferSize;
                cbBufferType = (int)SecurityBufferType.SECBUFFER_TOKEN;
                pvBuffer = Marshal.AllocHGlobal(bufferSize);
            }

            public SecurityBuffer(byte[] secBufferBytes)
            {
                cbBuffer = secBufferBytes.Length;
                cbBufferType = (int)SecurityBufferType.SECBUFFER_TOKEN;
                pvBuffer = Marshal.AllocHGlobal(cbBuffer);
                Marshal.Copy(secBufferBytes, 0, pvBuffer, cbBuffer);
            }

            public SecurityBuffer(byte[] secBufferBytes, SecurityBufferType bufferType)
            {
                cbBuffer = secBufferBytes.Length;
                cbBufferType = (int)bufferType;
                pvBuffer = Marshal.AllocHGlobal(cbBuffer);
                Marshal.Copy(secBufferBytes, 0, pvBuffer, cbBuffer);
            }

            public void Dispose()
            {
                if (pvBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pvBuffer);
                    pvBuffer = IntPtr.Zero;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SecurityBufferDesciption : IDisposable
        {

            public int ulVersion;
            public int cBuffers;
            public IntPtr pBuffers; //Point to SecBuffer

            public SecurityBufferDesciption(int bufferSize)
            {
                ulVersion = (int)SecurityBufferType.SECBUFFER_VERSION;
                cBuffers = 1;
                Common.SecurityBuffer ThisSecBuffer = new Common.SecurityBuffer(bufferSize);
                pBuffers = Marshal.AllocHGlobal(Marshal.SizeOf(ThisSecBuffer));
                Marshal.StructureToPtr(ThisSecBuffer, pBuffers, false);
            }

            public SecurityBufferDesciption(byte[] secBufferBytes)
            {
                ulVersion = (int)SecurityBufferType.SECBUFFER_VERSION;
                cBuffers = 1;
                Common.SecurityBuffer ThisSecBuffer = new Common.SecurityBuffer(secBufferBytes);
                pBuffers = Marshal.AllocHGlobal(Marshal.SizeOf(ThisSecBuffer));
                Marshal.StructureToPtr(ThisSecBuffer, pBuffers, false);
            }

            public SecurityBufferDesciption(BufferWrapper[] secBufferBytesArray)
            {
                if (secBufferBytesArray == null || secBufferBytesArray.Length == 0)
                {
                    throw new ArgumentException("secBufferBytesArray cannot be null or 0 length");
                }

                ulVersion = (int)SecurityBufferType.SECBUFFER_VERSION;
                cBuffers = secBufferBytesArray.Length;

                //Allocate memory for SecBuffer Array....
                pBuffers = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Buffer)) * cBuffers);

                for (int Index = 0; Index < secBufferBytesArray.Length; Index++)
                {
                    //Super hack: Now allocate memory for the individual SecBuffers
                    //and just copy the bit values to the SecBuffer array!!!
                    Common.SecurityBuffer ThisSecBuffer = new Common.SecurityBuffer(secBufferBytesArray[Index].Buffer, secBufferBytesArray[Index].BufferType);

                    //We will write out bits in the following order:
                    //int cbBuffer;
                    //int BufferType;
                    //pvBuffer;
                    //Note that we won't be releasing the memory allocated by ThisSecBuffer until we
                    //are disposed...
                    int CurrentOffset = Index * Marshal.SizeOf(typeof(Buffer));
                    Marshal.WriteInt32(pBuffers, CurrentOffset, ThisSecBuffer.cbBuffer);
                    Marshal.WriteInt32(pBuffers, CurrentOffset + Marshal.SizeOf(ThisSecBuffer.cbBuffer), ThisSecBuffer.cbBufferType);
                    Marshal.WriteIntPtr(pBuffers, CurrentOffset + Marshal.SizeOf(ThisSecBuffer.cbBuffer) + Marshal.SizeOf(ThisSecBuffer.cbBufferType), ThisSecBuffer.pvBuffer);
                }
            }

            public void Dispose()
            {
                if (pBuffers != IntPtr.Zero)
                {
                    if (cBuffers == 1)
                    {
                        Common.SecurityBuffer ThisSecBuffer = (Common.SecurityBuffer)Marshal.PtrToStructure(pBuffers, typeof(Common.SecurityBuffer));
                        ThisSecBuffer.Dispose();
                    }
                    else
                    {
                        for (int Index = 0; Index < cBuffers; Index++)
                        {
                            //The bits were written out the following order:
                            //int cbBuffer;
                            //int BufferType;
                            //pvBuffer;
                            //What we need to do here is to grab a hold of the pvBuffer allocate by the individual
                            //SecBuffer and release it...
                            int CurrentOffset = Index * Marshal.SizeOf(typeof(Buffer));
                            IntPtr SecBufferpvBuffer = Marshal.ReadIntPtr(pBuffers, CurrentOffset + Marshal.SizeOf(typeof(int)) + Marshal.SizeOf(typeof(int)));
                            Marshal.FreeHGlobal(SecBufferpvBuffer);
                        }
                    }

                    Marshal.FreeHGlobal(pBuffers);
                    pBuffers = IntPtr.Zero;
                }
            }

            public byte[] GetBytes()
            {
                byte[] Buffer = null;

                if (pBuffers == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Object has already been disposed!!!");
                }

                if (cBuffers == 1)
                {
                    Common.SecurityBuffer ThisSecBuffer = (Common.SecurityBuffer)Marshal.PtrToStructure(pBuffers, typeof(Common.SecurityBuffer));

                    if (ThisSecBuffer.cbBuffer > 0)
                    {
                        Buffer = new byte[ThisSecBuffer.cbBuffer];
                        Marshal.Copy(ThisSecBuffer.pvBuffer, Buffer, 0, ThisSecBuffer.cbBuffer);
                    }
                }
                else
                {
                    int BytesToAllocate = 0;

                    for (int Index = 0; Index < cBuffers; Index++)
                    {
                        //The bits were written out the following order:
                        //int cbBuffer;
                        //int BufferType;
                        //pvBuffer;
                        //What we need to do here calculate the total number of bytes we need to copy...
                        int CurrentOffset = Index * Marshal.SizeOf(typeof(Buffer));
                        BytesToAllocate += Marshal.ReadInt32(pBuffers, CurrentOffset);
                    }

                    Buffer = new byte[BytesToAllocate];

                    for (int Index = 0, BufferIndex = 0; Index < cBuffers; Index++)
                    {
                        //The bits were written out the following order:
                        //int cbBuffer;
                        //int BufferType;
                        //pvBuffer;
                        //Now iterate over the individual buffers and put them together into a
                        //byte array...
                        int CurrentOffset = Index * Marshal.SizeOf(typeof(Buffer));
                        int BytesToCopy = Marshal.ReadInt32(pBuffers, CurrentOffset);
                        IntPtr SecBufferpvBuffer = Marshal.ReadIntPtr(pBuffers, CurrentOffset + Marshal.SizeOf(typeof(int)) + Marshal.SizeOf(typeof(int)));
                        Marshal.Copy(SecBufferpvBuffer, Buffer, BufferIndex, BytesToCopy);
                        BufferIndex += BytesToCopy;
                    }
                }

                return (Buffer);
            }
        }
        #endregion
    }
}
