using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SheetSchemaBuilderNative
{
    public static unsafe class NativeExports
    {
        [UnmanagedCallersOnly(EntryPoint = "SheetSchemaBuilder_Process", CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int Process(char* iniPath, int force, delegate* unmanaged[Cdecl]<char*, void> logCallback)
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
                string iniPathText = iniPath == null ? "Sheet-Schema-Builder.ini" : new string(iniPath);
                string[] args = force != 0 ? new[] { iniPathText, "--force" } : new[] { iniPathText };
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

        private static void SendLog(delegate* unmanaged[Cdecl]<char*, void> logCallback, string log)
        {
            if (logCallback == null || string.IsNullOrEmpty(log))
            {
                return;
            }

            string nullTerminatedLog = log + '\0';
            fixed (char* logPointer = nullTerminatedLog)
            {
                logCallback(logPointer);
            }
        }
    }
}
