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
    /// Constructor
    /// </summary>
    /// <param name="code">Message code</param>
    /// <param name="token">Message token</param>
    public Message(MessageCode code, int token)
    {
        Code = code;
        Token = token;
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