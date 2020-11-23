using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Preferences;
using Java.Lang;
using Org.Webrtc;
using System.Collections.Generic;
using WebRTC.Enums;
using WebRTC.Interfaces;

namespace WebRTC.Android
{


    public enum AudioManagerState
    {
        Uninitialized,
        PreInitialized,
        Running,
    }


    public class RtcAudioManager : IRTCAudioManager
    {
        private const string TAG = nameof(RtcAudioManager);
        private const string SpeakerPhoneAuto = "auto";
        private const string SpeakerPhoneTrue = "true";
        private const string SpeakerPhoneFalse = "false";
        private string _useSpeakerPhone;
        public static bool _hasWiredHeadset; // why?
        private bool _savedIsSpeackerPhoneOn;
        private bool _savedIsMicrophoneMute;

        private AudioManager _audioManager;

        private IAudioManagerEvents _audioManagerEvents;
        private AudioManagerState _amState;

        private Mode _savedAudioMode = Mode.Invalid;
        private Context _context;

        private List<AudioDevice> _audioDevices = new List<AudioDevice>();

        // Default audio device; speaker phone for video calls or earpiece for audio
        // only calls.
        private AudioDevice _defaultAudioDevice;

        // Contains the currently selected audio device.
        // This device is changed automatically using a certain scheme where e.g.
        // a wired headset "wins" over speaker phone. It is also possible for a
        // user to explicitly select a device (and overrid any predefined scheme).
        // See |userSelectedAudioDevice| for details.
        private AudioDevice _selectedAudioDevice;

        // Contains the user-selected audio device which overrides the predefined
        // selection scheme.
        // explicit selection based on choice by userSelectedAudioDevice.
        private AudioDevice _userSelectedAudioDevice;
        private AudioManager.IOnAudioFocusChangeListener _audioFocusChangeListener;

        private BroadcastReceiver _wiredHeadsetReceiver;

        private RTCProximitySensor _proximitySensor;

        //private delegate Action 
        public static RtcAudioManager Create(Context context)
        {
            return new RtcAudioManager(context);
        }

        public RtcAudioManager(Context context)
        {
            _context = context;
            _audioManager = (AudioManager)context.GetSystemService(Context.AudioService);
            //_wiredHeadsetReceiver = new WiredHeadsetReceiver();
            _amState = AudioManagerState.Uninitialized;

            ISharedPreferences preferences = PreferenceManager.GetDefaultSharedPreferences(context);
            _useSpeakerPhone = preferences.GetString(context.GetString(Resource.String.pref_speakerphone_key),
                context.GetString(Resource.String.pref_speakerphone_default));
            if (_useSpeakerPhone.Equals(SpeakerPhoneFalse))
                _defaultAudioDevice = AudioDevice.Earpiece;
            else
                _defaultAudioDevice = AudioDevice.SpeakerPhone;
            _proximitySensor = RTCProximitySensor.Create(_context, new Runnable(OnProximitySensorChangedState));
            _wiredHeadsetReceiver = new WiredHeadsetReceiver(new Runnable(() => { UpdateAudioDeviceState(); }));
        }

        public void Start(IAudioManagerEvents audioManagerEvents)
        {
            ThreadUtils.CheckIsOnMainThread();
            if (_amState == AudioManagerState.Running)
                return;

            _audioManagerEvents = audioManagerEvents;
            _amState = AudioManagerState.Running;

            // Store current audio state so we can restore it when stop() is called
            _savedAudioMode = _audioManager.Mode;
            _savedIsSpeackerPhoneOn = _audioManager.SpeakerphoneOn;
            _savedIsMicrophoneMute = _audioManager.MicrophoneMute;
            _hasWiredHeadset = HasWiredHeadset();

            _audioFocusChangeListener = new OnAudioFocusChangeListener();

            //var result = _audioManager.RequestAudioFocus(_audioFocusChangeListener,Stream.VoiceCall,AudioFocus.GainTransient);

            _audioManager.Mode = Mode.InCommunication;

            _proximitySensor.Start();
            SetMicrophoneMute(false);
            _userSelectedAudioDevice = AudioDevice.None;
            _selectedAudioDevice = AudioDevice.None;
            _audioDevices.Clear();

            UpdateAudioDeviceState();

            RegisterReceiver(_wiredHeadsetReceiver, new IntentFilter(Intent.ActionHeadsetPlug));

        }

        public void Stop()
        {
            ThreadUtils.CheckIsOnMainThread();
            if (_amState != AudioManagerState.Running)
                return;
            _amState = AudioManagerState.Uninitialized;

            UnregisterReceiver(_wiredHeadsetReceiver);

            //_bluetoothManager.Stop();

            //restored previously stored audio states
            SetSpeakerphoneOn(_savedIsSpeackerPhoneOn);
            SetMicrophoneMute(_savedIsMicrophoneMute);
            _audioManager.Mode = _savedAudioMode;

            _audioManager.AbandonAudioFocus(_audioFocusChangeListener);
            _audioFocusChangeListener = null;

            if (_proximitySensor != null)
            {
                _proximitySensor.Stop();
                _proximitySensor = null;
            }

            _audioManagerEvents = null;
        }

        private bool HasWiredHeadset()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
                return _audioManager.WiredHeadsetOn;

            AudioDeviceInfo[] devices = _audioManager.GetDevices(GetDevicesTargets.All);
            foreach (var device in devices)
            {
                var type = device.Type;
                if (type == AudioDeviceType.UsbHeadset || type == AudioDeviceType.WiredHeadphones || type == AudioDeviceType.WiredHeadset || type == AudioDeviceType.UsbDevice)
                    return true;
            }

            return false;
        }

        private bool HasEarpiece()
        {
            return _context.PackageManager.HasSystemFeature(PackageManager.FeatureTelephony);
        }

        private void RegisterReceiver(BroadcastReceiver broadcastReceiver, IntentFilter intentFilter)
        {
            _context.RegisterReceiver(broadcastReceiver, intentFilter);
        }

        private void UnregisterReceiver(BroadcastReceiver broadcastReceiver)
        {
            _context.UnregisterReceiver(broadcastReceiver);
        }

        public List<AudioDevice> GetAudioDevices()
        {
            ThreadUtils.CheckIsOnMainThread();
            return _audioDevices;
        }

        public AudioDevice GetSelectedAudioDevice()
        {
            ThreadUtils.CheckIsOnMainThread();
            return _selectedAudioDevice;
        }

        public void SetDefaultAudioDevice(AudioDevice defaultDevice)
        {
            ThreadUtils.CheckIsOnMainThread();
            switch (defaultDevice)
            {
                case AudioDevice.SpeakerPhone:
                    _defaultAudioDevice = defaultDevice;
                    break;
                case AudioDevice.Earpiece:
                    if (HasEarpiece())
                        _defaultAudioDevice = defaultDevice;
                    else
                        _defaultAudioDevice = AudioDevice.SpeakerPhone;
                    break;
            }
            UpdateAudioDeviceState();
        }

        public void SelectAudioDevice(AudioDevice device)
        {
            ThreadUtils.CheckIsOnMainThread();
            _userSelectedAudioDevice = device;
            UpdateAudioDeviceState();
        }

        private void OnProximitySensorChangedState()
        {
            if (!_useSpeakerPhone.Equals(SpeakerPhoneAuto))
                return;

            if (_audioDevices.Count == 2 && _audioDevices.Contains(AudioDevice.Earpiece) &&
                _audioDevices.Contains(AudioDevice.SpeakerPhone))
            {
                if (_proximitySensor.SensorReportNearState())
                    SetAudioDeviceInternal(AudioDevice.Earpiece);
                else
                    SetAudioDeviceInternal(AudioDevice.SpeakerPhone);

            }
        }

        private void SetAudioDeviceInternal(AudioDevice device)
        {
            if (_audioDevices.Contains(device))
            {
                switch (device)
                {
                    case AudioDevice.SpeakerPhone:
                        SetSpeakerphoneOn(true);
                        break;
                    case AudioDevice.Earpiece:
                        SetSpeakerphoneOn(false);
                        break;
                    case AudioDevice.WiredHeadset:
                        SetSpeakerphoneOn(false);
                        break;
                    case AudioDevice.Bluetooth:
                        SetSpeakerphoneOn(false);
                        break;
                }

                _selectedAudioDevice = device;
            }
        }

        private void SetSpeakerphoneOn(bool on)
        {
            bool wasOn = _audioManager.SpeakerphoneOn;
            if (wasOn == on)
                return;
            _audioManager.SpeakerphoneOn = on;
        }

        private void SetMicrophoneMute(bool on)
        {
            bool wasMutted = _audioManager.MicrophoneMute;
            if (wasMutted == on)
                return;
            _audioManager.MicrophoneMute = on;
        }

        public void UpdateAudioDeviceState()
        {
            List<AudioDevice> newAudioDevices = new List<AudioDevice>();

            if (_hasWiredHeadset)
                newAudioDevices.Add(AudioDevice.WiredHeadset);
            else
            {
                newAudioDevices.Add(AudioDevice.SpeakerPhone);
                if (HasEarpiece())
                    newAudioDevices.Add(AudioDevice.Earpiece);
            }

            bool audioDeviceSetUpdated = !_audioDevices.Equals(newAudioDevices);

            _audioDevices = newAudioDevices;

            if (HasWiredHeadset() && _userSelectedAudioDevice == AudioDevice.SpeakerPhone)
                _userSelectedAudioDevice = AudioDevice.WiredHeadset;
            if (!HasWiredHeadset() && _userSelectedAudioDevice == AudioDevice.WiredHeadset)
                _userSelectedAudioDevice = AudioDevice.SpeakerPhone;

            AudioDevice newAudioDevice;

            if (HasWiredHeadset())
                newAudioDevice = AudioDevice.WiredHeadset;
            else
                newAudioDevice = _defaultAudioDevice;

            if (newAudioDevice != _selectedAudioDevice || audioDeviceSetUpdated)
            {
                SetAudioDeviceInternal(newAudioDevice);
                if (_audioManagerEvents != null)
                    _audioManagerEvents.OnAudioDeviceChanged(_selectedAudioDevice, _audioDevices);
            }
        }


        public class WiredHeadsetReceiver : BroadcastReceiver
        {
            private int StateUnplugged = 0;
            private int StatePlugged = 1;
            private int HasNoMic = 0;
            private int HasMic = 1;
            private Runnable _updateAudioDeviceState;

            public WiredHeadsetReceiver(Runnable updateAudioDeviceState)
            {
                _updateAudioDeviceState = updateAudioDeviceState;
            }
            public override void OnReceive(Context context, Intent intent)
            {
                int state = intent.GetIntExtra("state", StateUnplugged);
                int microphone = intent.GetIntExtra("microphone", HasNoMic);
                string name = intent.GetStringExtra("name");
                _hasWiredHeadset = state == StatePlugged;
                _updateAudioDeviceState.Run();
            }
        }
    }

}
