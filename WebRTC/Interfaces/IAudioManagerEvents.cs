using System.Collections.Generic;
using WebRTC.Enums;

namespace WebRTC.Interfaces
{
    public interface IAudioManagerEvents
    {
        void OnAudioDeviceChanged(AudioDevice audioDevice, List<AudioDevice> availableAudioDevice);
    }
}
