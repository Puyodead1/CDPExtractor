namespace CDPExtractor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                System.Console.WriteLine("Usage: CDPExtractor <path to cdp file>");
                return;
            }

            var cdpFile = args[0];
            new Chump(cdpFile);
        }
    }
}