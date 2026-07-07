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
            TextWriter originalOutput = Console.Out;
            TextWriter originalError = Console.Error;
            StringBuilder logBuilder = new StringBuilder();

            try
            {
                using StringWriter output = new StringWriter(logBuilder);
                using StringWriter error = new StringWriter(logBuilder);
                Console.SetOut(output);
                Console.SetError(error);
                string iniPathText = iniPath == null ? "Sheet-Schema-Builder.ini" : Marshal.PtrToStringUTF8((IntPtr)iniPath) ?? "Sheet-Schema-Builder.ini";
                string[] args = force != 0 ? new[] { iniPathText, "--target", "Unreal", "--force" } : new[] { iniPathText, "--target", "Unreal" };
                int exitCode = DataBuilder.SheetSchemaBuilder.Process(args).GetAwaiter().GetResult();
                output.Flush();
                error.Flush();
                SendLog(logCallback, logBuilder.ToString());
                return exitCode;
            }
            catch (Exception exception)
            {
                SendLog(logCallback, logBuilder + exception.ToString());
                return 1;
            }
            finally
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
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
