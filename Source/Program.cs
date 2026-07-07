namespace DataBuilder
{
    internal static class Program
    {
        public static Task<int> Main(string[] args)
        {
            return SheetSchemaBuilder.Process(args);
        }
    }
}
