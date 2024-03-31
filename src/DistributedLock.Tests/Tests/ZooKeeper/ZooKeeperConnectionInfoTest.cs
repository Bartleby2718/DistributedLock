using Medallion.Threading.ZooKeeper;
using NUnit.Framework;

namespace Medallion.Threading.Tests.ZooKeeper;

[Category("CI")]
public class ZooKeeperConnectionInfoTest
{
    [Test]
    public void TestEquality()
    {
        var connectionA = new ZooKeeperConnectionInfo(
            "cs",
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            new EquatableReadOnlyList<ZooKeeperAuthInfo>(new[] { new ZooKeeperAuthInfo("s", new EquatableReadOnlyList<byte>(new byte[] { 10 })) })
        );
        var connectionB = new ZooKeeperConnectionInfo(
            "cs",
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            new EquatableReadOnlyList<ZooKeeperAuthInfo>(new[] { new ZooKeeperAuthInfo("s", new EquatableReadOnlyList<byte>(new byte[] { 10 })) })
        );
        var connectionC = connectionA with { 
            AuthInfo = new EquatableReadOnlyList<ZooKeeperAuthInfo>(new[]
            {
                new ZooKeeperAuthInfo("s", new EquatableReadOnlyList<byte>(new byte[] { 10 })),
                new ZooKeeperAuthInfo("s2", new EquatableReadOnlyList<byte>(new byte[] { 11 })),
            })
        };

        Assert.That(connectionA, Is.EqualTo(connectionB));
        connectionA.GetHashCode().ShouldEqual(connectionB.GetHashCode());
        Assert.Multiple(() =>
        {
            Assert.That(connectionA, Is.Not.EqualTo(connectionC));
            Assert.That(connectionC.GetHashCode(), Is.Not.EqualTo(connectionA.GetHashCode()));
        });
    }
}
