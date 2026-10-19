using System.Text;
using Skjermvette.Agent.Service;

namespace Skjermvette.Agent.Tests;

public class DeviceIdentityStoreTests
{
    [Fact]
    public void SavedIdentity_LoadsBack_AndTheTokenIsntReadableInTheFile()
    {
        var folder = Directory.CreateTempSubdirectory("skjermvette-agent-test-").FullName;
        try
        {
            var store = new DeviceIdentityStore(folder);
            var identity = new DeviceIdentity(new Uri("https://skjermvette.test/"), Guid.NewGuid(), Guid.NewGuid(), "secret-device-token");

            store.Save(identity);

            Assert.Equal(identity, store.Load());
            Assert.DoesNotContain("secret-device-token", Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder, "device.dat"))));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void NothingSaved_LoadsNull() =>
        Assert.Null(new DeviceIdentityStore(Path.GetTempPath()).Load());
}
