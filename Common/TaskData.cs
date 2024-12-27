namespace Common;

public class TaskData : Message
{
    public TaskData(MessageCode code, int token, byte[] data)
        : base(code, token)
    {
        Data = data;
    }

    public DateTime Date { get; set; }
    public int TaskId { get; set; }
    public byte[] Data { get; set; }
}