namespace Common;

public class TaskResult : Message
{
    public TaskResult(MessageCode code, int token, object body) 
        : base(code, token, body)
    {
    }

    public DateTime Date { get; set; }
    public int TaskId { get; set; }
    public byte[] Data { get; set; }
}