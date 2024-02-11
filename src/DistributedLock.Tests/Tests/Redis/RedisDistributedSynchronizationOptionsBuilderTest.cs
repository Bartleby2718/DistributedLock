﻿using Medallion.Threading.Redis;
using NUnit.Framework;

namespace Medallion.Threading.Tests.Redis;

public class RedisDistributedSynchronizationOptionsBuilderTest
{
    [Test]
    public void TestValidatesExpiry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.Expiry(TimeSpan.FromSeconds(-2))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.Expiry(Timeout.InfiniteTimeSpan)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.Expiry(RedisDistributedSynchronizationOptionsBuilder.MinimumExpiry.TimeSpan - TimeSpan.FromTicks(1))));
        Assert.DoesNotThrow(() => GetOptions(o => o.Expiry(RedisDistributedSynchronizationOptionsBuilder.MinimumExpiry.TimeSpan)));
    }

    [Test]
    public void TestValidatesMinValidityTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.MinValidityTime(TimeSpan.FromSeconds(-2))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.MinValidityTime(TimeSpan.Zero)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.MinValidityTime(Timeout.InfiniteTimeSpan)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.MinValidityTime(RedisDistributedSynchronizationOptionsBuilder.DefaultExpiry.TimeSpan)));
        Assert.DoesNotThrow(() => GetOptions(
            o => o.MinValidityTime(RedisDistributedSynchronizationOptionsBuilder.DefaultExpiry.TimeSpan).Expiry(RedisDistributedSynchronizationOptionsBuilder.DefaultExpiry.TimeSpan + TimeSpan.FromMilliseconds(1))
        ));
    }

    [Test]
    public void TestValidatesExtensionCadence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.ExtensionCadence(TimeSpan.FromSeconds(-2))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.ExtensionCadence(Timeout.InfiniteTimeSpan)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.MinValidityTime(TimeSpan.FromSeconds(1)).ExtensionCadence(TimeSpan.FromSeconds(1))));
    }

    [Test]
    public void TestValidatesBusyWaitSleepTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.BusyWaitSleepTime(Timeout.InfiniteTimeSpan, TimeSpan.FromSeconds(1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.BusyWaitSleepTime(TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.BusyWaitSleepTime(TimeSpan.MaxValue, TimeSpan.FromSeconds(1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.BusyWaitSleepTime(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.BusyWaitSleepTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(-1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.BusyWaitSleepTime(TimeSpan.FromSeconds(1), TimeSpan.MaxValue)));

        Assert.Throws<ArgumentOutOfRangeException>(() => GetOptions(o => o.BusyWaitSleepTime(TimeSpan.FromSeconds(1.1), TimeSpan.FromSeconds(1))));

        Assert.DoesNotThrow(() => GetOptions(o => o.BusyWaitSleepTime(TimeSpan.Zero, TimeSpan.Zero)));
        Assert.DoesNotThrow(() => GetOptions(o => o.BusyWaitSleepTime(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(4))));
    }

    [Test]
    public void TestDefaults()
    {
        var defaultOptions = RedisDistributedSynchronizationOptionsBuilder.GetOptions(null);
        defaultOptions.RedLockTimeouts.Expiry.ShouldEqual(RedisDistributedSynchronizationOptionsBuilder.DefaultExpiry);
        defaultOptions.RedLockTimeouts.MinValidityTime.ShouldEqual(TimeSpan.FromSeconds(27));
        defaultOptions.ExtensionCadence.ShouldEqual(TimeSpan.FromSeconds(9));
        defaultOptions.MinBusyWaitSleepTime.ShouldEqual(TimeSpan.FromMilliseconds(10));
        defaultOptions.MaxBusyWaitSleepTime.ShouldEqual(TimeSpan.FromMilliseconds(800));
    }

    private static void GetOptions(Action<RedisDistributedSynchronizationOptionsBuilder> options) =>
        RedisDistributedSynchronizationOptionsBuilder.GetOptions(options);
}
