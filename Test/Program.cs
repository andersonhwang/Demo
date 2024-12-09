using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace MyApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var pipe = new Pipe();
            var locker = new object();
            var random = new Random(DateTime.Now.Microsecond);
            var header = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var temp = new List<byte>() { };
            Console.WriteLine("Hello World!");
            Console.ReadLine();
            Task.Run(async () =>
            {
                while (true)
                {
                    ReadResult result = await pipe.Reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    SequencePosition? position = null;
                    do
                    {
                        position = buffer.PositionOf((byte)0xFF);
                    X:
                        if ((position == null) || ((buffer.Length - buffer.GetOffset(position.Value)) < 4)) break;

                        var check = buffer.Slice(position.Value, 4);
                        //var read = new SequenceReader<byte>(check);
                        //header.SequenceEqual(buffer.Slice(position.Value, 4).ToArray());
                        if (check.ToArray().SequenceEqual(header))
                        {
                            position = buffer.GetPosition(4, position.Value);
                            var line = buffer.Slice(0, position.Value);
                            Console.WriteLine("...." + Convert.ToHexString(line.ToArray()));

                            buffer = buffer.Slice(position.Value);  // Move to next
                        }
                        else
                        {
                            position = buffer.GetPosition(1, position.Value);
                            goto X;
                        }
                    }
                    while (position != null);

                    // Tell the PipeReader how much of the buffer has been consumed.
                    pipe.Reader.AdvanceTo(buffer.Start, buffer.End);
                }
            });
int i = 0;
            Task.Run(async () =>
            {
                while (i++ < 20)
                {
                    var r = random.Next(1, 100);
                    var guid = Guid.NewGuid().ToByteArray();
                    Console.WriteLine(">>>>" + Convert.ToHexString(guid));
                    if (r > 10)
                    {
                        await FillPipeAsync(guid[..7], pipe.Writer);
                        await Task.Delay(TimeSpan.FromSeconds(0.5));
                        await FillPipeAsync(guid[7..], pipe.Writer);
                    }
                    else
                    {
                        await FillPipeAsync(guid, pipe.Writer);
                    }
                    await FillPipeAsync(header, pipe.Writer);

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            });
            Console.ReadLine();
        }

        static async Task FillPipeAsync(byte[] data, PipeWriter writer)
        {
            await writer.WriteAsync(data);
            FlushResult result = await writer.FlushAsync();
        }

        static async Task ReadPipeAsync(PipeReader reader)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition? position = buffer.PositionOf((byte)'\n');
                if (position == null) continue;

                var line = buffer.Slice(0, buffer.GetOffset(position.Value) + 1);
                Console.WriteLine(Convert.ToHexString(line.ToArray()));

                // Tell the PipeReader how much of the buffer has been consumed.
                reader.AdvanceTo(line.Start, line.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Mark the PipeReader as complete.
            await reader.CompleteAsync();
        }
    }
}