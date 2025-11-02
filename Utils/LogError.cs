namespace emotions_gateway.Utils
{
    public class LogError
    {
        public static void Log(string context, Exception ex)
        {
            Console.Error.WriteLine($"[ERRO] {context}: {ex.Message}");
        }

    }
}
