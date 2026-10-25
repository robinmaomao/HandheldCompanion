using HandheldCompanion.Shared;
using System;

namespace HandheldCompanion.Devices;

public class AYANEO2S : AYANEO2
{
    // AYANEO 2S does not expose the fan controller through the 0x4E/0x4F EC-RAM window
    // used by the older AYANEO devices: those ports answer with unrelated data on this model.
    // The fan is driven through the ACPI EC instead (same map as the Linux ayaneo-ec driver):
    //   0x4A = mode (0x01 = software/manual control, 0x00 = hardware/firmware control)
    //   0x4B = duty (0x00 - 0xFF)
    //   0x76 = fan tachometer
    private const byte FanControlRegister = 0x4A;
    private const byte FanDutyRegister = 0x4B;
    private const byte FanControlSoftware = 0x01;
    private const byte FanControlHardware = 0x00;

    public AYANEO2S()
    {
        // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 30 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        this.ECDetails.AddressFanControl = FanControlRegister;
        this.ECDetails.AddressFanDuty = FanDutyRegister;
        this.ECDetails.FanValueMin = 0;
        this.ECDetails.FanValueMax = 255;
    }

    public override void SetFanDuty(double percent)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        // the EC ignores the duty value while it is in hardware (firmware) mode
        if (!hasAppliedSoftwareFanProfile)
            return;

        percent = Math.Clamp(percent, 0.0d, 100.0d);
        double duty = percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100.0d + ECDetails.FanValueMin;
        byte value = Convert.ToByte(Math.Clamp(duty, 0.0d, 255.0d));

        EcWriteByte(FanDutyRegister, value);

        byte readBack = EcReadByte(FanDutyRegister);
        if (readBack != value)
            LogManager.LogWarning("AYANEO 2S fan duty write failed (wrote 0x{0:X2}, read 0x{1:X2})", value, readBack);
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        byte value = enable ? FanControlSoftware : FanControlHardware;
        EcWriteByte(FanControlRegister, value);

        byte readBack = EcReadByte(FanControlRegister);
        if (readBack != value)
        {
            LogManager.LogWarning("AYANEO 2S fan mode write failed (wrote 0x{0:X2}, read 0x{1:X2})", value, readBack);
            return;
        }

        hasAppliedSoftwareFanProfile = enable;
    }

    public override float ReadFanDuty()
    {
        if (!UseOpenLib || !IsOpen)
            return 0.0f;

        byte value = EcReadByte(FanDutyRegister);
        return value * 100.0f / (ECDetails.FanValueMax - ECDetails.FanValueMin);
    }
}
