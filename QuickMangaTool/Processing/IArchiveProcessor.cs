interface IArchiveProcessor
{
    ProcessResult Process(string cbzPath, Action<int, int>? onImageProgress = null);
}
