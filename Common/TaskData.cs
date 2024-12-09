using System.Text.Json.Serialization;

namespace Common;

public class TaskData : Message
{
    public TaskData(MessageCode code, int token, object body)
        : base(code, token, body)
    {
    }

    public DateTime Date { get; set; }
    public int TaskId { get; set; }
    public byte[] Data { get; set; }
}