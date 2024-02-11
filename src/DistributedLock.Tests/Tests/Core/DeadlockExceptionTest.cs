using NUnit.Framework;
using System.Runtime.Serialization.Formatters.Binary;

namespace Medallion.Threading.Tests.Core;

[Category("CI")]
public class DeadlockExceptionTest
{
#if NETFRAMEWORK // binary serialization has been removed from newer versions of .NET
    [Test]
    public void TestDeadlockExceptionSerialization()
    {
        void ThrowDeadlockException() => throw new DeadlockException(nameof(TestDeadlockExceptionSerialization), new InvalidOperationException("foo"));
        var deadlockException = Assert.Throws<DeadlockException>(ThrowDeadlockException);

        var formatter = new BinaryFormatter();
        var stream = new MemoryStream();
        formatter.Serialize(stream, deadlockException);

        stream.Position = 0;
        var deserialized = (DeadlockException)formatter.Deserialize(stream);
        deserialized.Message.ShouldEqual(deadlockException.Message);
        deserialized.StackTrace.ShouldEqual(deadlockException.StackTrace);
        (deserialized.InnerException?.Message).ShouldEqual(deadlockException.InnerException?.Message);
    }
#endif
}
