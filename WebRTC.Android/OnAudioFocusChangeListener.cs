using Android.Media;
using Android.Runtime;
using Java.Interop;
using System;
using Java.Lang;

namespace WebRTC.Android
{
    public class OnAudioFocusChangeListener :Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {

        public void OnAudioFocusChange([GeneratedEnum] AudioFocus focusChange)
        {
            string typeOfChange;
            switch (focusChange)
            {
                case AudioFocus.Gain:
                    typeOfChange = "AUDIOFOCUS_GAIN";
                    break;
                case AudioFocus.GainTransient:
                    typeOfChange = "AUDIOFOCUS_GAIN_TRANSIENT";
                    break;
                case AudioFocus.GainTransientExclusive:
                    typeOfChange = "AUDIOFOCUS_GAIN_TRANSIENT_EXCLUSIVE";
                    break;
                case AudioFocus.GainTransientMayDuck:
                    typeOfChange = "AUDIOFOCUS_GAIN_TRANSIENT_MAY_DUCK";
                    break;
                case AudioFocus.Loss:
                    typeOfChange = "AUDIOFOCUS_LOSS";
                    break;
                case AudioFocus.LossTransient:
                    typeOfChange = "AUDIOFOCUS_LOSS_TRANSIENT";
                    break;
                case AudioFocus.LossTransientCanDuck:
                    typeOfChange = "AUDIOFOCUS_LOSS_TRANSIENT_CAN_DUCK";
                    break;
                default:
                    typeOfChange = "AUDIOFOCUS_INVALID";
                    break;
            }
        }
    }
}