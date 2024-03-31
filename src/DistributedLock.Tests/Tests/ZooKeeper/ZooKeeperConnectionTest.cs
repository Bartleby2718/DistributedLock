﻿using Medallion.Threading.ZooKeeper;
using NUnit.Framework;
using Medallion.Threading.Internal;

namespace Medallion.Threading.Tests.ZooKeeper;

using Moq;
using org.apache.zookeeper;
using org.apache.zookeeper.data;

public class ZooKeeperConnectionTest
{
    [Test]
    public async Task TestSharesConnections()
    {
        var pool = new ZooKeeperConnection.Pool(maxAge: TimeSpan.FromSeconds(10));
        using var connection1 = await pool.ConnectAsync(GetConnectionInfo(), CancellationToken.None);
        using var connection2 = await pool.ConnectAsync(GetConnectionInfo(), CancellationToken.None);

        Assert.AreNotSame(connection1, connection2);
        Assert.AreSame(connection1.ZooKeeper, connection2.ZooKeeper);

        connection1.Dispose();
        connection2.ZooKeeper.getState().ShouldEqual(ZooKeeper.States.CONNECTED);
    }

    [Test]
    public async Task TestDoesNotShareDifferentConnections()
    {
        var pool = new ZooKeeperConnection.Pool(maxAge: TimeSpan.FromSeconds(10));
        using var connection1 = await pool.ConnectAsync(GetConnectionInfo(connectTimeout: TimeSpan.FromSeconds(30)), CancellationToken.None);
        using var connection2 = await pool.ConnectAsync(GetConnectionInfo(connectTimeout: TimeSpan.FromSeconds(20)), CancellationToken.None);

        Assert.AreNotSame(connection1, connection2);
        Assert.AreNotSame(connection1.ZooKeeper, connection2.ZooKeeper);
    }

    [Test]
    [NonParallelizable, Retry(tryCount: 3)] // timing-sensitive
    public async Task TestConnectionIsClosedAndNotSharedAfterMaxAgeElapses()
    {
        var pool = new ZooKeeperConnection.Pool(maxAge: TimeSpan.FromSeconds(2));

        using var connection1 = await pool.ConnectAsync(GetConnectionInfo(), CancellationToken.None);
        var zooKeeper1 = connection1.ZooKeeper;

        connection1.Dispose();
        Assert.IsTrue(await TestHelper.WaitForAsync(() => (zooKeeper1.getState() == ZooKeeper.States.CLOSED).AsValueTask(), TimeSpan.FromSeconds(3)));

        using var connection2 = await pool.ConnectAsync(GetConnectionInfo(), CancellationToken.None);
        Assert.AreNotSame(zooKeeper1, connection2.ZooKeeper);
    }

    [Test]
    [NonParallelizable, Retry(tryCount: 3)] // timing-sensitive
    public async Task TestConnectionCanBeHeldOpenAfterMaxAgeButDoesNotShareAndClosesAfterwards()
    {
        var pool = new ZooKeeperConnection.Pool(maxAge: TimeSpan.FromSeconds(2));

        using var connection = await pool.ConnectAsync(GetConnectionInfo(), CancellationToken.None);
        Assert.IsTrue(await TestHelper.WaitForAsync(
            async () =>
            {
                using var testConnectionInfo = await pool.ConnectAsync(GetConnectionInfo(), CancellationToken.None);
                return testConnectionInfo.ZooKeeper != connection.ZooKeeper;
            },
            TimeSpan.FromSeconds(3)
        ));

        connection.ZooKeeper.getState().ShouldEqual(ZooKeeper.States.CONNECTED);
        var zooKeeper = connection.ZooKeeper;
        connection.Dispose();
        Assert.IsTrue(await TestHelper.WaitForAsync(() => (zooKeeper.getState() != ZooKeeper.States.CONNECTED).AsValueTask(), TimeSpan.FromSeconds(1)));
    }

    [Test]
    [NonParallelizable, Retry(tryCount: 3)] // timing-sensitive
    public void TestConnectTimeout()
    {
        var pool = new ZooKeeperConnection.Pool(maxAge: TimeSpan.FromSeconds(10));
        Assert.ThrowsAsync<TimeoutException>(() => pool.ConnectAsync(GetConnectionInfo(connectTimeout: TimeSpan.Zero), CancellationToken.None));
    }

    [Test]
    public void TestHandleConnectionLossException()
    {
        var mockConnection = new Mock<ZooKeeperConnection>();

        var callCount = 0;
        mockConnection.Setup(m => m.ZooKeeper.createAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<List<ACL>>(), It.IsAny<CreateMode>()))
            .Callback(() => callCount++)
            .Throws(new KeeperException.ConnectionLossException());

        var exception = Assert.ThrowsAsync<KeeperException.ConnectionLossException>(async () =>
        {
            await mockConnection.Object.CreateEphemeralSequentialNode(directory: new ZooKeeperPath("/test"), namePrefix: "node", new List<ACL>(), ensureDirectoryExists: true);
        });

        Assert.NotNull(exception);
        Assert.AreEqual(3, callCount, "Expected method to be retried 3 times");
    }

    [Test]
    public void TestThreadSafety()
    {
        var connectionInfos = Enumerable.Range(1, 3)
            .Select(i => GetConnectionInfo(connectTimeout: TimeSpan.FromSeconds(10 * i)))
            .ToArray();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(async () =>
            {
                using var connection = await ZooKeeperConnection.DefaultPool.ConnectAsync(connectionInfos[i % connectionInfos.Length], CancellationToken.None);
                await connection.ZooKeeper.existsAsync("/zookeeper");
                await Task.Delay(1);
                await connection.ZooKeeper.existsAsync($"/{Guid.NewGuid()}");
            }))
            .ToArray();
        Assert.DoesNotThrow(() => Task.WaitAll(tasks));
    }

    private static ZooKeeperConnectionInfo GetConnectionInfo(TimeoutValue? connectTimeout = null) =>
        new(
            ZooKeeperPorts.DefaultConnectionString,
            ConnectTimeout: connectTimeout ?? TimeSpan.FromSeconds(30),
            SessionTimeout: TimeSpan.FromSeconds(30),
            new EquatableReadOnlyList<ZooKeeperAuthInfo>(Array.Empty<ZooKeeperAuthInfo>())
        );
}
