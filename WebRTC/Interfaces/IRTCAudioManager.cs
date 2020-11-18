
using System.Collections.Generic;
using WebRTC.Enums;

namespace WebRTC.Interfaces
{
    public interface IRTCAudioManager 
    {
        void Start(IAudioManagerEvents audioManagerEvents);
        void Stop();
        List<AudioDevice> GetAudioDevices();
        AudioDevice GetSelectedAudioDevice();
        void SetDefaultAudioDevice(AudioDevice defaultDevice);
        void SelectAudioDevice(AudioDevice device);

    }
}
