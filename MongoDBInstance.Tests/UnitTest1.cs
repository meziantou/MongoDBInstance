using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace MongoDBInstances.Tests;
public class UnitTest1
{
    private readonly ITestOutputHelper _testOutputHelper;

    public UnitTest1(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task Test1()
    {
        _testOutputHelper.WriteLine(Environment.OSVersion.ToString());
        _testOutputHelper.WriteLine(Environment.OSVersion.Platform.ToString());
        _testOutputHelper.WriteLine(Environment.OSVersion.VersionString);
        _testOutputHelper.WriteLine(Environment.OSVersion.Version.ToString());

        var instance = new MongoDBInstance();
        instance.MongoOutput += (sender, e) => _testOutputHelper.WriteLine(e.Source + ": " + e.Data);

        _testOutputHelper.WriteLine("starting");
        instance.Start();

        _testOutputHelper.WriteLine("connecting");
        var client = new MongoClient(instance.ConnectionString);

        _testOutputHelper.WriteLine("Creating db");
        var db = client.GetDatabase("test");
        var collection = db.GetCollection<Customer>("collection");
        await collection.InsertOneAsync(new Customer { Name = "Alice" });
        await collection.InsertOneAsync(new Customer { Name = "John" });

        Assert.Equal(2, await collection.CountDocumentsAsync(Builders<Customer>.Filter.Empty));

        Console.WriteLine("stopping");
        await instance.StopAsync();
    }

    private sealed class Customer
    {
        public string? Name { get; set; }
    }
}