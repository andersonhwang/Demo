namespace Common;

public class TaskData : Message
{
    public TaskData(MessageCode code, int token)
        : base(code, token)
    {
    }

    public DateTime Date { get; set; }
    public int TaskId { get; set; }
    public byte[] Data { get; set; }
}