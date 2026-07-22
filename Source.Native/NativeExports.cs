using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SheetSchemaBuilderNative
{
    public static unsafe class NativeExports
    {
        [UnmanagedCallersOnly(EntryPoint = "SheetSchemaBuilder_Process", CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int Process(byte* iniPath, int force, delegate* unmanaged[Cdecl]<byte*, void> logCallback)
        {
            try
            {
                string iniPathText = iniPath == null ? "Sheet-Schema-Builder.ini" : Marshal.PtrToStringUTF8((IntPtr)iniPath) ?? "Sheet-Schema-Builder.ini";
                string[] args = force != 0 ? new[] { iniPathText, "--target", "Unreal", "--force" } : new[] { iniPathText, "--target", "Unreal" };
                DataBuilder.BuilderProcessResult result = DataBuilder.SheetSchemaBuilder.ProcessWithResult(args).GetAwaiter().GetResult();
                SendLog(logCallback, result.CombinedLog);
                return result.ExitCode;
            }
            catch (Exception exception)
            {
                SendLog(logCallback, exception.ToString());
                return 1;
            }
        }

        private static void SendLog(delegate* unmanaged[Cdecl]<byte*, void> logCallback, string log)
        {
            if (logCallback == null || string.IsNullOrEmpty(log))
            {
                return;
            }

            byte[] nullTerminatedLog = Encoding.UTF8.GetBytes(log + '\0');
            fixed (byte* logPointer = nullTerminatedLog)
            {
                logCallback(logPointer);
            }
        }
    }
}
