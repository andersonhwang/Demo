namespace Common;

/// <summary>
/// Message
/// </summary>
public class Message
{
    /// <summary>
    /// Message code
    /// </summary>
    public MessageCode Code { get; }
    
    /// <summary>
    /// Token
    /// </summary>
    public int Token { get; }

    /// <summary>
    /// Message body
    /// </summary>
    public object Body { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="code">Message code</param>
    /// <param name="token">Message token</param>
    /// <param name="body">Message body</param>
    public Message(MessageCode code, int token, object body)
    {
        Code = code;
        Token = token;
        Body = body;
    }
}

/// <summary>
/// Message code
/// </summary>
public enum MessageCode
{
    Data,
    Result,
}