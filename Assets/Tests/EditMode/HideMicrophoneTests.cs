using NUnit.Framework;
using KnocksFromBeneath;

public class HideMicrophoneTests
{
    private static readonly string[] Devices =
    {
        "Микрофон (Steam Streaming Microphone)", "CABLE Output (VB-Audio Virtual Cable)", "Микрофон (DroidCam Audio)",
        "Микрофон (USB PnP Audio Device)", "Headset Microphone (Oculus Virtual Audio Device)"
    };

    [Test]
    public void Order_RealMicrophoneGoesBeforeVirtualOnes()
    {
        Assert.That(HideMicrophone.Order(Devices, null)[0], Is.EqualTo("Микрофон (USB PnP Audio Device)"));
        Assert.That(HideMicrophone.Order(Devices, null).Count, Is.EqualTo(Devices.Length));
    }

    [Test]
    public void Order_DeviceThatWorkedGoesFirst()
    {
        Assert.That(HideMicrophone.Order(Devices, "Микрофон (DroidCam Audio)")[0], Is.EqualTo("Микрофон (DroidCam Audio)"));
    }

    [Test]
    public void Order_NoDevicesIsEmpty()
    {
        Assert.That(HideMicrophone.Order(null, null), Is.Empty);
    }

    [TestCase("Микрофон (Steam Streaming Microphone)", true)]
    [TestCase("Микрофон (Realtek Audio)", false)]
    public void IsVirtual_ByName(string name, bool expected)
    {
        Assert.That(HideMicrophone.IsVirtual(name), Is.EqualTo(expected));
    }
}
