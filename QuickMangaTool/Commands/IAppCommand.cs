interface IAppCommand
{
    int Execute(IReadOnlyList<string> cbzFiles, IRunLogger logger);
}
