namespace MongoDBInstances;

public sealed class MongoDataReceivedEventArgs : EventArgs
{
    public string Data { get; }
    public OutputDataSource Source { get; }

    internal MongoDataReceivedEventArgs(OutputDataSource source, string data)
    {
        Source = source;
        Data = data;
    }
}
