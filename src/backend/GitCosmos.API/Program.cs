using GitCosmos.API;

internal static class Program
{
    public static Task Main(string[] args) => ApiHost.Build(args).RunAsync();
}
