namespace Common;

public class TaskResult : Message
{
    public TaskResult(MessageCode code, int token) 
        : base(code, token)
    {
    }

    public DateTime Date { get; set; }
    public int TaskId { get; set; }
    public byte[] Data { get; set; }
}